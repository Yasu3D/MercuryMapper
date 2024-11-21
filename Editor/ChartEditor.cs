using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.MultiCharting;
using MercuryMapper.UndoRedo;
using MercuryMapper.UndoRedo.NoteOperations;
using MercuryMapper.Utils;
using MercuryMapper.Views;
using MercuryMapper.Views.Gimmicks;

namespace MercuryMapper.Editor;

public class ChartEditor
{
    public ChartEditor(MainView main)
    {
        mainView = main;
        UndoRedoManager = new(mainView);
        Chart = new(this);

        UndoRedoManager.OperationHistoryChanged += (_, _) =>
        {
            Chart.Notes = Chart.Notes.OrderBy(x => x.BeatData.FullTick).ToList();
            Chart.Gimmicks = Chart.Gimmicks.OrderBy(x => x.BeatData.FullTick).ToList();
            Chart.IsSaved = false;
            
            mainView.ToggleInsertButton();
            mainView.SetSelectionInfo();
            mainView.UpdateCurrentTimeScaleInfo();
            
            UpdateLastPlacedHold();
            
            // Repair collections after every operation to make sure forward references don't get mangled.
            if (mainView.ConnectionManager.NetworkState != ConnectionManager.NetworkConnectionState.Local) RepairHoldsForward();
        };
    }
    
    private readonly MainView mainView;
    
    public readonly Cursor Cursor = new();
    public readonly UndoRedoManager UndoRedoManager;
    public Chart Chart { get; private set; }
    
    public ChartEditorState EditorState { get; private set; }
    
    public float CurrentMeasureDecimal { get; set; }
    public BeatData CurrentBeatData => new((int?)mainView.NumericMeasure.Value ?? 0, (int?)(mainView.NumericBeatValue.Value * 1920 / mainView.NumericBeatDivisor.Value) ?? 0);

    public NoteType CurrentNoteType { get; set; } = NoteType.Touch;
    public BonusType CurrentBonusType { get; set; } = BonusType.None;
    public MaskDirection CurrentMaskDirection { get; set; } = MaskDirection.Clockwise;
    public TraceColor CurrentTraceColor => (TraceColor)mainView.TraceColorComboBox.SelectedIndex;

    public bool LayerNoteActive = true;
    public bool LayerMaskActive = true;
    public bool LayerGimmickActive = true;
    public bool LayerTraceActive = true;

    public bool BonusAvailable(NoteType type)
    {
        if (type is NoteType.Trace or NoteType.Damage) return false;

        if (mainView.UserConfig.EditorConfig.LimitToMercuryBonusTypes)
        {
            return type is NoteType.Touch or NoteType.SlideClockwise or NoteType.SlideCounterclockwise;
        }

        return true;
    }

    public bool RNoteAvailable(NoteType type)
    {
        return type is not (NoteType.Trace or NoteType.Damage);
    }
    
    public List<Note> SelectedNotes { get; } = [];
    public Note? LastSelectedNote;
    public ChartElement? HighlightedElement;
    public BoxSelect BoxSelect = new();

    public List<Note> NoteClipboard { get; private set; } = [];

    public Note? LastPlacedNote;
    public Note? CurrentCollectionStart;
    
    public void NewChart(string bgmFilepath, float bpm, int timeSigUpper, int timeSigLower)
    {
        LastSelectedNote = null;
        LastPlacedNote = null;
        HighlightedElement = null;
        EndHold();
        EditorState = ChartEditorState.InsertNote; // manually reset state one more time
        UndoRedoManager.Clear();
        SelectedNotes.Clear();
        Chart.Clear();

        lock (Chart)
        {
            Chart.BgmFilepath = bgmFilepath;
            
            Gimmick startBpm = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.BpmChange,
                Bpm = bpm,
                TimeStamp = 0,
            };

            Gimmick startTimeSig = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.TimeSigChange,
                TimeSig = new(timeSigUpper, timeSigLower),
                TimeStamp = 0,
            };

            Note startMask = new()
            {
                BeatData = new(0, 0),
                NoteType = NoteType.MaskAdd,
                MaskDirection = MaskDirection.Center,
                Position = 15,
                Size = 60,
                RenderSegment = true,
            };
            
            Chart.Gimmicks.Add(startBpm);
            Chart.Gimmicks.Add(startTimeSig);
            Chart.StartBpm = startBpm;
            Chart.StartTimeSig = startTimeSig;
            Chart.BpmText = startBpm.Bpm.ToString(CultureInfo.InvariantCulture);

            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
            
            Chart.Notes.Add(startMask);
        }

        mainView.SetChartInfo();
        mainView.SetSelectionInfo();
    }

    public void LoadChart(string path)
    {
        LastSelectedNote = null;
        LastPlacedNote = null;
        HighlightedElement = null;
        EndHold();
        EditorState = ChartEditorState.InsertNote; // manually reset state one more time
        UndoRedoManager.Clear();
        SelectedNotes.Clear();
        
        FormatHandler.LoadFile(Chart, path);
        
        mainView.SetChartInfo();
        mainView.SetSelectionInfo();
    }

    public void LoadChartNetwork(string chartData)
    {
        LastSelectedNote = null;
        LastPlacedNote = null;
        HighlightedElement = null;
        EndHold();
        EditorState = ChartEditorState.InsertNote; // manually reset state one more time
        UndoRedoManager.Clear();
        SelectedNotes.Clear();
        
        FormatHandler.LoadFileFromNetwork(Chart, chartData);
        mainView.SetChartInfo();
        mainView.SetSelectionInfo();
    }

    public void UpdateCursorNoteType()
    {
        // Reset Editor State
        if (CurrentNoteType is not (NoteType.Hold or NoteType.Trace)) EndHold();
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        mainView.SetNoteSizeBounds(CurrentNoteType, CurrentBonusType, NoteLinkType.Unlinked);
    }

    // ________________ Edit Menu
    public void Undo()
    {
        if (!UndoRedoManager.CanUndo) return;
        IOperation operation = UndoRedoManager.Undo();

        // Update LastPlacedHold
        if (operation is InsertHoldNote insertHoldOperation)
        {
            StartHold(insertHoldOperation.LastPlacedNote);
        }

        if (operation is InsertNote insertNoteOperation)
        {
            // Update CurrentHoldStart + End Hold
            if (insertNoteOperation.Note.IsNoteCollection && insertNoteOperation.Note.LinkType is NoteLinkType.Start && EditorState is ChartEditorState.InsertHold)
            {
                CurrentCollectionStart = null;
                EndHold();
            }
        }

        if (operation is DeleteHoldNote)
        {
            foreach (Note note in Chart.Notes)
            {
                if (!UndoRedoManager.CanUndo) break;
                if (note.NoteType is not (NoteType.Hold or NoteType.Trace)) continue;
                if (note.LinkType != NoteLinkType.Unlinked) continue;

                IOperation op = UndoRedoManager.PeekUndo;
                if (op is not InsertHoldNote) UndoRedoManager.Undo();
                return;
            }
        }
    }

    public void Redo()
    { 
        if (!UndoRedoManager.CanRedo) return;
        IOperation operation = UndoRedoManager.Redo();
        
        // Update LastPlacedHold
        if (operation is InsertHoldNote insertHoldOperation)
        {
            StartHold(insertHoldOperation.Note);
        }
        
        // Update CurrentHoldStart + Start Hold
        if (operation is InsertNote insertNoteOperation)
        {
            if (insertNoteOperation.Note.IsNoteCollection && insertNoteOperation.Note.LinkType is NoteLinkType.Start)
            {
                StartHold(insertNoteOperation.Note);
            }
            else
            {
                EndHold();
            }
        }
        
        if (operation is DeleteHoldNote)
        {
            foreach (Note note in Chart.Notes)
            {
                if (!UndoRedoManager.CanRedo) break;
                if (note.NoteType is not (NoteType.Hold or NoteType.Trace)) continue;
                if (note.LinkType != NoteLinkType.Unlinked) continue;
                
                IOperation op = UndoRedoManager.PeekRedo;
                if (op is InsertHoldNote) return;
                UndoRedoManager.Redo();
                return;
            }
        }
    }
    
    public async void Cut()
    {
        if (TopLevel.GetTopLevel(mainView)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (SelectedNotes.Count == 0) return;
        
        IClipboard? systemClipboard = TopLevel.GetTopLevel(mainView)?.Clipboard;
        if (systemClipboard is null) return;
        
        CopyToNoteClipboard(SelectedNotes);
        DeleteSelection();
        DeselectAllNotes();
        mainView.SetSelectionInfo();
        
        await systemClipboard.SetTextAsync(FormatHandler.WriteClipboard(NoteClipboard));
    }
    
    public async void Copy()
    {
        if (TopLevel.GetTopLevel(mainView)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (SelectedNotes.Count == 0) return;

        IClipboard? systemClipboard = TopLevel.GetTopLevel(mainView)?.Clipboard;
        if (systemClipboard is null) return;
        
        CopyToNoteClipboard(SelectedNotes);
        DeselectAllNotes();
        mainView.SetSelectionInfo();
        
        await systemClipboard.SetTextAsync(FormatHandler.WriteClipboard(NoteClipboard));
    }
    
    public async void Paste()
    {
        if (TopLevel.GetTopLevel(mainView)?.FocusManager?.GetFocusedElement() is TextBox) return;
        
        IClipboard? systemClipboard = TopLevel.GetTopLevel(mainView)?.Clipboard;
        if (systemClipboard is null) return;
        
        DeselectAllNotes();
        mainView.SetSelectionInfo();
        
        List<IOperation> operationList = [];
        
        string? clipboardText = await systemClipboard.GetTextAsync();
        if (clipboardText == null) return;
        
        foreach (Note note in FormatHandler.ParseClipboard(clipboardText))
        {
            SelectedNotes.Add(note);
            note.BeatData = new(CurrentBeatData.FullTick + note.BeatData.FullTick);
            operationList.Add(new InsertNote(Chart, SelectedNotes, note));
        }

        if (operationList.Count == 0) return;
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
    }

    private void CopyToNoteClipboard(List<Note> selectedNotes)
    {
        if (selectedNotes.Count == 0) return;

        // Since you can only copy hold notes as a whole,
        // select every referenced note of a selected hold.

        List<Note> tempSelected = [..selectedNotes]; // c# 8 syntax is.. a thing that exists. Looks cool I guess?
        foreach (Note note in selectedNotes.Where(x => x.IsNoteCollection))
        {
            foreach (Note reference in note.References())
            {
                if (tempSelected.Contains(reference)) continue;
                tempSelected.Add(reference);
            }
        }

        tempSelected = tempSelected.OrderBy(x => x.BeatData.FullTick).ToList();
        BeatData start = tempSelected[0].BeatData;
        
        NoteClipboard.Clear();
        NoteClipboard = DeepCloneNotes(tempSelected);

        foreach (Note note in NoteClipboard)
        {
            note.BeatData = new(note.BeatData.FullTick - start.FullTick);
        }
    }

    private static List<Note> DeepCloneNotes(List<Note> notes)
    {
        Dictionary<Note, Note> originalToCloneMap = new();
        List<Note> newList = [];
        
        foreach (Note note in notes) newList.Add(deepClone(note, originalToCloneMap));
        return newList;
        
        Note deepClone(Note note, Dictionary<Note, Note> cloneDictionary)
        {
            if (cloneDictionary.TryGetValue(note, out Note? existing))
            {
                return existing;
            }

            Note newNote = new(note);
            cloneDictionary.Add(note, newNote);

            if (note.IsNoteCollection)
            {
                if (note.PrevReferencedNote != null) newNote.PrevReferencedNote = deepClone(note.PrevReferencedNote, cloneDictionary);
                if (note.NextReferencedNote != null) newNote.NextReferencedNote = deepClone(note.NextReferencedNote, cloneDictionary);
            }

            return newNote;
        }
    }
    
    // ________________ Selections
    public void SelectNote(Note note)
    {
        lock (SelectedNotes)
        {
            if (!SelectedNotes.Remove(note))
            {
                SelectedNotes.Add(note);
            }
        }
        
        mainView.SetSelectionInfo();
    }

    public void SelectAllNotes()
    {
        lock (SelectedNotes)
        {
            foreach (Note note in Chart.Notes)
            {
                if (SelectedNotes.Contains(note)) continue;
                SelectedNotes.Add(note);
            }
        }
        
        mainView.SetSelectionInfo();
    }
    
    public void DeselectAllNotes()
    {
        lock (SelectedNotes)
        {
            SelectedNotes.Clear();
        }

        HighlightedElement = null;
        
        mainView.SetSelectionInfo();
    }

    public void CheckerDeselect()
    {
        List<Note> sorted = SelectedNotes.OrderBy(x => x.BeatData.FullTick).ToList();
        lock (SelectedNotes)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                if ((i & 1) != 0) continue;
                SelectedNotes.Remove(sorted[i]);
            }
        }
    }
    
    public void SelectNoteCollectionReferences()
    {
        List<Note> tempSelected = SelectedNotes.Where(x => x.IsNoteCollection).ToList();
        foreach (Note note in tempSelected)
        {
            foreach (Note reference in note.References())
            {
                if (SelectedNotes.Contains(reference)) continue;
                SelectedNotes.Add(reference);
            }
        }
        
        mainView.SetSelectionInfo();
    }

    public void RunBoxSelect(float measureDecimal = 0)
    {
        switch (EditorState)
        {
            case ChartEditorState.InsertHold:
            {
                return;
            }

            case ChartEditorState.InsertNote:
            {
                BoxSelect = new();

                mainView.SetNoteSizeBounds(NoteType.None, BonusType.None, NoteLinkType.Unlinked);
                EditorState = ChartEditorState.BoxSelectStart;
                break;
            }

            case ChartEditorState.BoxSelectStart:
            {
                BoxSelect.SelectionStart = CurrentBeatData;
                BoxSelect.Position = Cursor.Position;
                BoxSelect.Size = Cursor.Size;
                
                EditorState = ChartEditorState.BoxSelectEnd;
                break;
            }

            case ChartEditorState.BoxSelectEnd:
            {
                // Handle case where user scrolls backwards
                if (measureDecimal <= BoxSelect.SelectionStart?.MeasureDecimal)
                {
                    BoxSelect.SelectionEnd = BoxSelect.SelectionStart;
                    BoxSelect.SelectionStart = new(measureDecimal);
                }
                else
                {
                    BoxSelect.SelectionEnd = new(measureDecimal);
                }
                
                select();
                mainView.SetSelectionInfo();
                mainView.SetNoteSizeBounds(CurrentNoteType, CurrentBonusType, NoteLinkType.Unlinked);
                EditorState = ChartEditorState.InsertNote;
                break;
            }
        }

        return;

        void select()
        {
            if (BoxSelect.SelectionStart is null || BoxSelect.SelectionEnd is null) return;

            IEnumerable<Note> selectable = Chart.Notes.Where(x =>
            {
                bool inTimeRange = x.BeatData.FullTick >= BoxSelect.SelectionStart.FullTick &&
                                   x.BeatData.FullTick <= BoxSelect.SelectionEnd.FullTick;

                bool isLayerActive = x.IsMask && LayerMaskActive || x.IsNote && LayerNoteActive || x.NoteType == NoteType.Trace && LayerTraceActive;

                return inTimeRange && isLayerActive && MathExtensions.IsPartiallyOverlapping(x.Position, x.Position + x.Size, BoxSelect.Position, BoxSelect.Position + BoxSelect.Size);
            });
            
            foreach (Note note in selectable)
            {
                if (!SelectedNotes.Contains(note)) SelectedNotes.Add(note);
            }
        }
    }

    public void StopBoxSelect()
    {
        mainView.SetNoteSizeBounds(CurrentNoteType, CurrentBonusType, NoteLinkType.Unlinked);
        EditorState = ChartEditorState.InsertNote;
    }
    
    public void SelectSimilar(SelectSimilarType type, int threshold, int noteType, int bonusType)
    {
        if (HighlightedElement is not Note note) return;

        List<Note> similarNotes = type switch
        {
            SelectSimilarType.Position => Chart.Notes.Where(x => int.Abs(x.Position - note.Position) <= threshold).ToList(),
            SelectSimilarType.Size => Chart.Notes.Where(x => int.Abs(x.Size - note.Size) <= threshold).ToList(),
            SelectSimilarType.Type => Chart.Notes.Where(x =>
                {
                    bool sameNoteType = noteType == -1 || x.NoteType == (NoteType)noteType;
                    bool sameBonusType = bonusType == -1 || x.BonusType == (BonusType)noteType;

                    return sameNoteType && sameBonusType;
                }).ToList(),
            _ => [],
        };
        
        foreach (Note similarNote in similarNotes)
        {
            SelectNote(similarNote);
        }
        
        mainView.SetSelectionInfo();
    }

    public void FilterSelection(SelectSimilarType type, int threshold, int noteType, int bonusType)
    {
        if (HighlightedElement is not Note note) return;
        
        List<Note> differentNotes = type switch
        {
            SelectSimilarType.Position => SelectedNotes.Where(x => int.Abs(x.Position - note.Position) > threshold).ToList(),
            SelectSimilarType.Size => SelectedNotes.Where(x => int.Abs(x.Size - note.Size) > threshold).ToList(),
            SelectSimilarType.Type => SelectedNotes.Where(x =>
            {
                bool sameNoteType = noteType == -1 || x.NoteType == (NoteType)noteType;
                bool sameBonusType = noteType == -1 || x.NoteType == (NoteType)noteType;

                return !sameNoteType || !sameBonusType;
            }).ToList(),
            _ => [],
        };

        foreach (Note differentNote in differentNotes)
        {
            SelectedNotes.Remove(differentNote);
        }
        
        mainView.SetSelectionInfo();
    }
    
    // ________________ Highlighting
    public void HighlightElement(ChartElement? element)
    {
        HighlightedElement = HighlightedElement == element ? null : element;
        mainView.SetSelectionInfo();
    }

    public void HighlightNextElement()
    {
        if (HighlightedElement is null) return;
        if (!LayerNoteActive && !LayerGimmickActive && !LayerMaskActive) return;

        List<ChartElement> elements = [];

        if (LayerNoteActive) elements.AddRange(Chart.Notes.Where(x => !x.IsMask));
        if (LayerMaskActive) elements.AddRange(Chart.Notes.Where(x => x.IsMask));
        if (LayerGimmickActive) elements.AddRange(Chart.Gimmicks);

        elements = elements.OrderBy(x => x.BeatData.FullTick).ToList();
        
        int index = elements.IndexOf(HighlightedElement);
        HighlightedElement = elements[MathExtensions.Modulo(index + 1, elements.Count)];
        mainView.SetSelectionInfo();
    }

    public void HighlightPrevElement()
    {
        if (HighlightedElement is null) return;
        if (!LayerNoteActive && !LayerGimmickActive && !LayerMaskActive) return;

        List<ChartElement> elements = [];

        if (LayerNoteActive) elements.AddRange(Chart.Notes.Where(x => !x.IsMask));
        if (LayerMaskActive) elements.AddRange(Chart.Notes.Where(x => x.IsMask));
        if (LayerGimmickActive) elements.AddRange(Chart.Gimmicks);

        elements = elements.OrderBy(x => x.BeatData.FullTick).ToList();
        
        int index = elements.IndexOf(HighlightedElement);
        HighlightedElement = elements[MathExtensions.Modulo(index - 1, elements.Count)];
        mainView.SetSelectionInfo();
    }

    public void HighlightNearestElement()
    {
        List<ChartElement> elements = Chart.Notes.Concat<ChartElement>(Chart.Gimmicks).OrderBy(x => x.BeatData.FullTick).ToList();
        HighlightElement(elements.FirstOrDefault(x => x.BeatData.MeasureDecimal >= CurrentMeasureDecimal) ?? null);
        mainView.SetSelectionInfo();
    }
    
    public void SelectHighlightedNote()
    {
        if (HighlightedElement is null or Gimmick) return;
        SelectNote((Note)HighlightedElement);
    }
    
    // ________________ Misc
    public void UpdateLastPlacedHold()
    {
        if (EditorState != ChartEditorState.InsertHold) return;
        LastPlacedNote = LastPlacedNote?.References().LastOrDefault();
    }

    public void RepairHoldsForward()
    {
        foreach (Note note in Chart.Notes)
        {
            if (!note.IsNoteCollection) continue;
            if (note.NextReferencedNote != null) note.NextReferencedNote.PrevReferencedNote = note;
        }
    }

    public void UpdateVisibleTimeInRenderEngine() => mainView.RenderEngine.UpdateVisibleTime();
    
    // ________________ ChartElement Operations
    public void InsertNote()
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        switch (EditorState)
        {
            case ChartEditorState.InsertNote:
            {
                Note note = new()
                {
                    BeatData = CurrentBeatData,
                    MaskDirection = CurrentMaskDirection,
                    NoteType = CurrentNoteType,
                    BonusType = CurrentBonusType,
                    Position = Cursor.Position,
                    Size = Cursor.Size,
                    Color = CurrentTraceColor,
                };
                
                // Force bonusType to none for masks
                if (note.IsMask) note.BonusType = BonusType.None;
                
                Chart.IsSaved = false;
                UndoRedoManager.InvokeAndPush(new InsertNote(Chart, SelectedNotes, note));
                
                if (note.IsNoteCollection)
                {
                    StartHold(note);
                }
                
                if (mainView.UserConfig.EditorConfig.HighlightPlacedNote) HighlightElement(note);
                break;
            }
            
            case ChartEditorState.InsertHold:
            {
                // Place Hold End.
                // Hold End's prevReferencedNote is LastPlacedNote.
                // LastPlacedNote's nextReferencedNote is Hold End.
                // If previous note is hold end, convert it to Hold Segment.

                if (LastPlacedNote is null || CurrentCollectionStart is null) return;
                if (CurrentBeatData.FullTick <= CurrentCollectionStart.BeatData.FullTick) return;
                if (CurrentBeatData.FullTick <= LastPlacedNote.BeatData.FullTick) return;
                
                Note note = new()
                {
                    BeatData = CurrentBeatData,
                    MaskDirection = CurrentMaskDirection,
                    NoteType = CurrentNoteType,
                    BonusType = BonusType.None,
                    Position = Cursor.Position,
                    Size = Cursor.Size,
                    PrevReferencedNote = LastPlacedNote,
                    Color = LastPlacedNote.Color,
                };

                LastPlacedNote.NextReferencedNote = note;

                UndoRedoManager.InvokeAndPush(new InsertHoldNote(Chart, SelectedNotes, note, LastPlacedNote));
                Chart.IsSaved = false;
                LastPlacedNote = note;
                
                if (mainView.UserConfig.EditorConfig.HighlightPlacedNote) HighlightElement(note);
                break;
            }

            case ChartEditorState.BoxSelectStart:
            case ChartEditorState.BoxSelectEnd:
            {
                return;
            }
        }
    }

    public void InsertBpmChange(float bpm)
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        Gimmick gimmick = new()
        {
            BeatData = CurrentBeatData,
            Bpm = bpm,
            GimmickType = GimmickType.BpmChange,
        };
        
        UndoRedoManager.InvokeAndPush(new InsertGimmick(Chart, gimmick));
        Chart.IsSaved = false;
    }

    public void InsertTimeSigChange(int upper, int lower)
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        Gimmick gimmick = new()
        {
            BeatData = CurrentBeatData,
            TimeSig = new(upper, lower),
            GimmickType = GimmickType.TimeSigChange,
        };
        
        UndoRedoManager.InvokeAndPush(new InsertGimmick(Chart, gimmick));
        Chart.IsSaved = false;
    }

    public void InsertHiSpeedChange(float hiSpeed)
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        Gimmick gimmick = new()
        {
            BeatData = CurrentBeatData,
            HiSpeed = hiSpeed,
            GimmickType = GimmickType.HiSpeedChange,
        };
        
        UndoRedoManager.InvokeAndPush(new InsertGimmick(Chart, gimmick));
        Chart.IsSaved = false;
    }

    public void InsertStop(float start, float end)
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;

        if (start > Chart.EndOfChart?.BeatData.MeasureDecimal 
            || end > Chart.EndOfChart?.BeatData.MeasureDecimal) return;
        
        Gimmick startGimmick = new()
        {
            BeatData = new(start),
            GimmickType = GimmickType.StopStart,
        };

        Gimmick endGimmick = new()
        {
            BeatData = new(end),
            GimmickType = GimmickType.StopEnd,
        };
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation([new InsertGimmick(Chart, startGimmick), new InsertGimmick(Chart, endGimmick)]));
        Chart.IsSaved = false;
    }

    public void InsertReverse(float effectStart, float effectEnd, float noteEnd)
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        if (effectStart > Chart.EndOfChart?.BeatData.MeasureDecimal 
            || effectEnd > Chart.EndOfChart?.BeatData.MeasureDecimal
            || noteEnd > Chart.EndOfChart?.BeatData.MeasureDecimal) return;
        
        Gimmick effectStartGimmick = new()
        {
            BeatData = new(effectStart),
            GimmickType = GimmickType.ReverseEffectStart,
        };

        Gimmick effectEndGimmick = new()
        {
            BeatData = new(effectEnd),
            GimmickType = GimmickType.ReverseEffectEnd,
        };
        
        Gimmick noteEndGimmick = new()
        {
            BeatData = new(noteEnd),
            GimmickType = GimmickType.ReverseNoteEnd,
        };
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation([new InsertGimmick(Chart, effectStartGimmick), new InsertGimmick(Chart, effectEndGimmick), new InsertGimmick(Chart, noteEndGimmick)]));
        Chart.IsSaved = false;
    }

    public void InsertEndOfChart()
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        if (Chart.EndOfChart != null) return;
        
        Gimmick gimmick = new()
        {
            BeatData = CurrentBeatData,
            GimmickType = GimmickType.EndOfChart,
        };
        
        UndoRedoManager.InvokeAndPush(new InsertGimmick(Chart, gimmick));
        Chart.IsSaved = false;
    }
    
    public void EditGimmick()
    {
        if (HighlightedElement is null or Note) return;

        Gimmick highlightedGimmick = (Gimmick)HighlightedElement;
        
        if (highlightedGimmick.GimmickType is GimmickType.BpmChange)
        {
            GimmickView_Bpm gimmickView = new(highlightedGimmick.Bpm);
            
            ContentDialog dialog = new()
            {
                Content = gimmickView,
                Title = Assets.Lang.Resources.Editor_EditGimmick,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Edit,
            };
        
            Dispatcher.UIThread.Post(async () =>
            {
                ContentDialogResult result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary) return;

                if (gimmickView.BpmNumberBox.Value <= 0)
                {
                    mainView.ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidBpm);
                    return;
                }

                Gimmick newGimmick = new(highlightedGimmick)
                {
                    Bpm = (float)gimmickView.BpmNumberBox.Value,
                };
                
                UndoRedoManager.InvokeAndPush(new EditGimmick(Chart, highlightedGimmick, newGimmick));
            });
        }
        
        if (highlightedGimmick.GimmickType is GimmickType.TimeSigChange)
        {
            GimmickView_TimeSig gimmickView = new(highlightedGimmick.TimeSig.Upper, highlightedGimmick.TimeSig.Lower);
            ContentDialog dialog = new()
            {
                Content = gimmickView,
                Title = Assets.Lang.Resources.Editor_EditGimmick,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Edit,
            };
        
            Dispatcher.UIThread.Post(async () =>
            {
                ContentDialogResult result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary) return;

                if ((int)gimmickView.TimeSigUpperNumberBox.Value <= 0 || (int)gimmickView.TimeSigLowerNumberBox.Value <= 0)
                {
                    mainView.ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidTimeSig);
                    return;
                }

                Gimmick newGimmick = new(highlightedGimmick)
                {
                    TimeSig = new((int)gimmickView.TimeSigUpperNumberBox.Value, (int)gimmickView.TimeSigLowerNumberBox.Value),
                };
                
                UndoRedoManager.InvokeAndPush(new EditGimmick(Chart, highlightedGimmick, newGimmick));
            });
        }
        
        if (highlightedGimmick.GimmickType is GimmickType.HiSpeedChange)
        {
            GimmickView_HiSpeed gimmickView = new(highlightedGimmick.HiSpeed);
            ContentDialog dialog = new()
            {
                Content = gimmickView,
                Title = Assets.Lang.Resources.Editor_EditGimmick,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Edit,
            };
        
            Dispatcher.UIThread.Post(async () =>
            {
                ContentDialogResult result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary) return;

                Gimmick newGimmick = new(highlightedGimmick)
                {
                    HiSpeed = (float)gimmickView.HiSpeedNumberBox.Value,
                };
                
                UndoRedoManager.InvokeAndPush(new EditGimmick(Chart, highlightedGimmick, newGimmick));
            });
        }

        Chart.IsSaved = false;
    }

    public void DeleteGimmick()
    {
        if (HighlightedElement is null or Note) return;
        if (!Chart.Gimmicks.Contains(HighlightedElement)) return;
        if (HighlightedElement == Chart.StartBpm || HighlightedElement == Chart.StartTimeSig) return;
        if (SelectedNotes.Count != 0) return;
        
        Gimmick gimmick = (Gimmick)HighlightedElement;
        List<IOperation> operationList = [new DeleteGimmick(Chart, gimmick)];

        switch (gimmick.GimmickType)
        {
            case GimmickType.StopStart:
            {
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.First(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.StopEnd)));
                break;
            }
            case GimmickType.StopEnd:
            {
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.Last(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.StopStart)));
                break;
            }

            case GimmickType.ReverseEffectStart:
            {
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.First(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectEnd)));
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.First(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseNoteEnd)));
                break;
            }
            case GimmickType.ReverseEffectEnd:
            {
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.Last(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectStart)));
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.First(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseNoteEnd)));
                break;
            }
            case GimmickType.ReverseNoteEnd:
            {
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.Last(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectStart)));
                operationList.Add(new DeleteGimmick(Chart, Chart.Gimmicks.Last(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectEnd)));
                break;
            }
        }

        HighlightedElement = null;
        mainView.SetSelectionInfo();
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
    }
    
    public void DeleteSelection()
    {
        // So... this is more complicated than expected.
        // Bulk deleting hold notes requires each deletion to reference the state of the last,
        // otherwise hold note references get mangled.
        // The most elegant solution I can think of for that is to
        // pre-apply each hold deletion, undo them and add them to the full operationList,
        // then Redoing all operations together.
        // This handles state and preserves the operation as one whole CompositeOperation.
        List<IOperation> operationList = [];
        List<DeleteHoldNote> holdOperationList = [];
        
        if (EditorState is ChartEditorState.InsertHold && CurrentCollectionStart != null && SelectedNotes.Contains(CurrentCollectionStart))
        {
            EndHold();
        }

        HashSet<Note> checkedHolds = [];
        List<Note> checkedCurrentHolds = [];
        
        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            // If deleting all but one, add the last as well.
            Note[] references = highlighted.References().ToArray();
            if (references.Length == 2)
            {
                addOperation(references[0]);
                addOperation(references[1]);
            }
            else
            {
                addOperation(highlighted);
            }
            HighlightedElement = Chart.Notes.LastOrDefault(x => x.BeatData.FullTick <= highlighted.BeatData.FullTick && x != highlighted);
        }
        
        foreach (Note selected in SelectedNotes.OrderByDescending(x => x.BeatData.FullTick))
        {
            // Segment was already added previously, skip.
            if (checkedHolds.Contains(selected)) continue;

            if (selected.IsNoteCollection)
            {
                // Compare how many references the collection has to how many of those references are in SelectedNotes.
                // If refsInSelected is one less than refsTotal, then add the entire list including the missing one.
                // Then continue to avoid adding segments twice.
                Note[] referencesTotal = selected.References().ToArray();
                int referencesInSelected = SelectedNotes.Intersect(referencesTotal).Count();

                if (referencesTotal.Length == referencesInSelected + 1)
                {
                    foreach (Note reference in referencesTotal)
                    {
                        addOperation(reference);
                        checkedHolds.Add(reference);
                    }

                    continue;
                }
            }
            
            addOperation(selected);   
        }
        
        // Temporarily undo all hold operations, then add them to the operationList
        foreach (DeleteHoldNote deleteHoldOp in holdOperationList)
        {
            UndoRedoManager.Undo(false);
            operationList.Add(deleteHoldOp);
        }
        
        if (operationList.Count == 0) return;

        // Move LastPlacedHold back and let UpdateLastPlacedHold() reset it properly, otherwise UpdateLastPlacedHold() loses reference and gets stuck.
        if (EditorState == ChartEditorState.InsertHold && LastPlacedNote != null)
        {
            LastPlacedNote = LastPlacedNote.FirstReference();
        }
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            if (note.IsNoteCollection)
            {
                if (LastPlacedNote != null && EditorState is ChartEditorState.InsertHold)
                {
                    // If user is currently inserting a hold and trying to delete all segments (or all but one) of the hold they're currently deleting, then end hold.
                    if (!checkedCurrentHolds.Contains(note))
                    {
                        List<Note> unselectedCurrentReferences = LastPlacedNote.References().Where(x => !SelectedNotes.Contains(x)).ToList();
                        if (unselectedCurrentReferences.Count <= 1) EndHold();
                        checkedCurrentHolds.AddRange(LastPlacedNote.References());
                    }
                }

                DeleteHoldNote holdOp = new(Chart, SelectedNotes, note, note.NextReferencedNote?.BonusType ?? BonusType.None);
                holdOperationList.Add(holdOp);
                
                UndoRedoManager.InvokeAndPush(holdOp);
            }
            else
            {
                operationList.Add(new DeleteNote(Chart, SelectedNotes, note));
            }
        }
    }
    
    public void EditSelection(bool shape, bool properties)
    {
        if (!shape && !properties) return;

        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            int newPosition = shape ? Cursor.Position : note.Position;
            int newSize = shape ? Cursor.Size : note.Size;
            NoteType newNoteType = properties ? editNoteType(note, CurrentNoteType) : note.NoteType;
            BonusType newBonusType = properties ? editBonusType(note, CurrentBonusType) : note.BonusType;
            MaskDirection newDirection = properties ? CurrentMaskDirection : note.MaskDirection;

            Note newNote = new(note, note.Guid)
            {
                Position = newPosition,
                Size = int.Clamp(newSize, Note.MinSize(newNoteType, newBonusType, note.LinkType), Note.MaxSize(newNoteType)),
                NoteType = newNoteType,
                BonusType = newBonusType,
                MaskDirection = newDirection,
                RenderSegment = note.LinkType != NoteLinkType.Point || note.RenderSegment,
            };

            operationList.Add(new EditNote(note, newNote));
        }

        NoteType editNoteType(Note note, NoteType currentNoteType)
        {
            // Cannot edit the notetype of notes that are part of a note collection.
            if (note.IsNoteCollection) return note.NoteType;
            
            // Cannot edit a note's notetype to a note collection notetype.
            if (currentNoteType is NoteType.Hold or NoteType.Trace) return note.NoteType;
            
            return currentNoteType;
        }

        BonusType editBonusType(Note note, BonusType currentBonusType)
        {
            // Always default to none for MaskAdd, MaskRemove, Trace.
            if (note.IsMask || note.NoteType == NoteType.Trace)
            {
                return BonusType.None;
            }
            
            // Set BonusType to None if NoteType cannot be Bonus in Mercury.
            if (currentBonusType is BonusType.Bonus && !BonusAvailable(note.NoteType))
            {
                return BonusType.None;
            }

            return currentBonusType;
        }
    }

    public void SetSelectionRenderFlag(bool render)
    {
        // This operation only works on note collection segments. All other note types should have a flag of 1 by default.
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes.Where(x => x.IsNoteCollection))
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note { LinkType: NoteLinkType.Point } highlighted)
        {
            addOperation(highlighted);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note, note.Guid)
            {
                RenderSegment = render,
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }
    
    public void QuickEditSize(int delta)
    {
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note, note.Guid)
            {
                Position = note.Position,
                Size = int.Clamp(note.Size + delta, Note.MinSize(note.NoteType, note.BonusType, note.LinkType), Note.MaxSize(note.NoteType)),
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }
    
    public void QuickEditPosition(int delta)
    {
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }
        
        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note, note.Guid)
            {
                Position = MathExtensions.Modulo(note.Position + delta, 60),
                Size = note.Size,
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }
    
    public void QuickEditTimestamp(int delta)
    {
        // This has the same issue as DeleteSelection() where holds get messed up because operations need to be done in-order.
        
        float divisor = (1 / (float?)mainView.NumericBeatDivisor.Value ?? 0.0625f) * delta;
        
        List<IOperation> operationList = [];
        
        float endOfChartMeasureDecimal = Chart.EndOfChart != null ? Chart.EndOfChart.BeatData.MeasureDecimal : float.PositiveInfinity;
        
        IEnumerable<Note> selectedNotes = delta < 0 ? SelectedNotes.OrderBy(x => x.BeatData.FullTick) : SelectedNotes.OrderByDescending(x => x.BeatData.FullTick);
        foreach (Note selected in selectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0)
        {
            if (HighlightedElement is Note highlighted)
                addOperation(highlighted);

            if (HighlightedElement is Gimmick gimmick)
                addOperationGimmick(gimmick);
        }

        if (operationList.Count == 0) return;
        
        foreach (IOperation unused in operationList)
        {
            UndoRedoManager.Undo();
        }
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note, note.Guid)
            {
                BeatData = new(float.Clamp(note.BeatData.MeasureDecimal + divisor, 0, endOfChartMeasureDecimal)),
            };
            
            if (newNote.PrevReferencedNote != null && newNote.BeatData.FullTick <= newNote.PrevReferencedNote.BeatData.FullTick) return;
            if (newNote.NextReferencedNote != null && newNote.BeatData.FullTick >= newNote.NextReferencedNote.BeatData.FullTick) return;

            EditNote edit = new(note, newNote);
            
            operationList.Add(edit);
            UndoRedoManager.InvokeAndPush(edit);
        }

        void addOperationGimmick(Gimmick gimmick)
        {
            // Can't move start gimmicks, otherwise the editor will explode.
            if (Chart.StartBpm == gimmick) return;
            if (Chart.StartTimeSig == gimmick) return;
            
            float min = 0;
            float max = endOfChartMeasureDecimal;

            switch (gimmick.GimmickType)
            {
                case GimmickType.None:
                case GimmickType.BpmChange:
                case GimmickType.TimeSigChange:
                case GimmickType.HiSpeedChange:
                {
                    break;
                }
                
                case GimmickType.ReverseEffectStart:
                {
                    Gimmick? next = Chart.Gimmicks.FirstOrDefault(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectEnd);
                    if (next != null) max = next.BeatData.MeasureDecimal;
                    break;
                }
                
                case GimmickType.ReverseEffectEnd:
                {
                    Gimmick? prev = Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectStart);
                    if (prev != null) min = prev.BeatData.MeasureDecimal;
                    
                    Gimmick? next = Chart.Gimmicks.FirstOrDefault(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseNoteEnd);
                    if (next != null) max = next.BeatData.MeasureDecimal;
                    break;
                }
                
                case GimmickType.ReverseNoteEnd:
                {
                    Gimmick? prev = Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.ReverseEffectEnd);
                    if (prev != null) min = prev.BeatData.MeasureDecimal;
                    
                    break;
                }
                
                case GimmickType.StopStart:
                {
                    Gimmick? next = Chart.Gimmicks.FirstOrDefault(x => x.BeatData.FullTick > gimmick.BeatData.FullTick && x.GimmickType is GimmickType.StopEnd);
                    if (next != null) max = next.BeatData.MeasureDecimal;
                    
                    break;
                }
                
                case GimmickType.StopEnd:
                {
                    Gimmick? prev = Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.StopStart);
                    if (prev != null) min = prev.BeatData.MeasureDecimal;
                    
                    break;
                }
                
                case GimmickType.EndOfChart:
                {
                    float prevNote = Chart.Notes.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick)?.BeatData.MeasureDecimal ?? 0;
                    float prevGimmick = Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick)?.BeatData.MeasureDecimal ?? 0;

                    min = float.Max(prevNote, prevGimmick);
                    max = float.PositiveInfinity;
                    break;
                }
            }

            float newMeasureDecimal = gimmick.BeatData.MeasureDecimal + divisor;
            if (MathExtensions.GreaterAlmostEqual(newMeasureDecimal, max) || MathExtensions.LessAlmostEqual(newMeasureDecimal, min)) return;
            
            Gimmick newGimmick = new(gimmick)
            {
                BeatData = new(float.Clamp(gimmick.BeatData.MeasureDecimal + divisor, min, max)),
            };

            EditGimmick edit = new(Chart, gimmick, newGimmick);

            operationList.Add(edit);
            UndoRedoManager.InvokeAndPush(edit);
        }
    }   
    
    public void MirrorSelection(int axis = 30)
    {
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note, note.Guid)
            {
                Position = MathExtensions.Modulo(axis - note.Size - note.Position, 60),
                NoteType = note.NoteType switch
                {
                    NoteType.SlideClockwise => NoteType.SlideCounterclockwise,
                    NoteType.SlideCounterclockwise => NoteType.SlideClockwise,
                    _ => note.NoteType,
                },
                MaskDirection = note.MaskDirection switch
                {
                    MaskDirection.Counterclockwise => MaskDirection.Clockwise,
                    MaskDirection.Clockwise => MaskDirection.Counterclockwise,
                    MaskDirection.Center => MaskDirection.Center,
                    _ => MaskDirection.Center,
                },
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }

    private enum HoldDirection
    {
        Clockwise,
        Counterclockwise,
        Symmetrical,
        None,
    }

    private enum HoldSide
    {
        Left,
        Right,
    }
    
    public void BakeHold(MathExtensions.HoldEaseType easeType, bool forceNoRender)
    {
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }

        if (operationList.Count == 0) return;
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            if (!note.IsNoteCollection || note.NextReferencedNote is null) return;
            if (note.Position == note.NextReferencedNote.Position && note.Size == note.NextReferencedNote.Size) return;
            
            BakeHold bakeHold = interpolate(note, note.NextReferencedNote);
            if (bakeHold.Segments.Count != 0)
            {
                operationList.Add(bakeHold);
            }
        }

        BakeHold interpolate(Note startNote, Note endNote)
        {
            // I'm making up coordinate systems here, bear with me.
            // There's also some code repetition or redundant variables,
            // just to keep it easier to follow. As I said, bear with me.
            
            // Get global position.
            int startLeftEdge = startNote.Position;
            int startRightEdge = startNote.Position + startNote.Size;

            int endLeftEdge = endNote.Position;
            int endRightEdge = endNote.Position + endNote.Size;
            
            // Calculate offsets from start note edges.
            int leftEdgeOffsetCcw = MathExtensions.Modulo(endLeftEdge - startLeftEdge, 60);
            int leftEdgeOffsetCw = MathExtensions.Modulo(startLeftEdge - endLeftEdge, 60);
            
            int rightEdgeOffsetCcw = MathExtensions.Modulo(endRightEdge - startRightEdge, 60);
            int rightEdgeOffsetCw = MathExtensions.Modulo(startRightEdge - endRightEdge, 60);
            
            // Find the shortest direction for each edge.
            HoldDirection leftDirection = leftEdgeOffsetCcw < leftEdgeOffsetCw ? HoldDirection.Counterclockwise : HoldDirection.Clockwise;
            if (leftEdgeOffsetCcw == 0 && leftEdgeOffsetCw == 0) leftDirection = HoldDirection.None;
            
            HoldDirection rightDirection = rightEdgeOffsetCcw < rightEdgeOffsetCw ? HoldDirection.Counterclockwise : HoldDirection.Clockwise;
            if (rightEdgeOffsetCcw == 0 && rightEdgeOffsetCw == 0) rightDirection = HoldDirection.None;

            // Direction that's NOT set to none with the smallest offset takes priority.
            // If they're equal, default to left edge's preferred direction.
            // Again, it's quite verbose and could be simplified, but I'd rather
            // keep the logic very readable.
            int leftEdgeOffsetMin = int.Min(leftEdgeOffsetCcw, leftEdgeOffsetCw);
            int rightEdgeOffsetMin = int.Min(rightEdgeOffsetCcw, rightEdgeOffsetCw);
            
            HoldDirection shortestDirection;
            HoldSide shortestSide;

            if (leftDirection == HoldDirection.None)
            {
                shortestDirection = startNote.Size > endNote.Size ? HoldDirection.Clockwise : HoldDirection.Counterclockwise;
                shortestSide = HoldSide.Right;
            }
            else if (rightDirection == HoldDirection.None)
            {
                shortestDirection = startNote.Size > endNote.Size ? HoldDirection.Counterclockwise : HoldDirection.Clockwise;
                shortestSide = HoldSide.Left;
            }
            else if (leftEdgeOffsetMin < rightEdgeOffsetMin)
            {
                shortestDirection = leftDirection;
                shortestSide = HoldSide.Left;
            }
            else
            {
                shortestDirection = rightDirection;
                shortestSide = HoldSide.Right;
            }
            
            // If one hold completely encases the other (overlaps),
            // a special third HoldDirection needs to be used.
            bool isOverlapping = MathExtensions.IsFullyOverlapping(startLeftEdge, startRightEdge, endLeftEdge, endRightEdge);
            HoldDirection finalDirection = isOverlapping ? HoldDirection.Symmetrical : shortestDirection;
            
            // Get final signed offsets
            int signedLeftEdgeOffset = finalDirection switch
            {
                HoldDirection.Clockwise => -leftEdgeOffsetCw,
                HoldDirection.Counterclockwise => leftEdgeOffsetCcw,
                HoldDirection.Symmetrical => shortestSide == HoldSide.Left
                ? shortestDirection == HoldDirection.Counterclockwise ? leftEdgeOffsetCcw : -leftEdgeOffsetCw
                : shortestDirection != HoldDirection.Counterclockwise ? leftEdgeOffsetCcw : -leftEdgeOffsetCw,
                _ => throw new ArgumentOutOfRangeException(),
            };

            int signedRightEdgeOffset = finalDirection switch
            {
                HoldDirection.Clockwise => -rightEdgeOffsetCw,
                HoldDirection.Counterclockwise => rightEdgeOffsetCcw,
                HoldDirection.Symmetrical => shortestSide == HoldSide.Right
                ? shortestDirection == HoldDirection.Counterclockwise ? rightEdgeOffsetCcw : -rightEdgeOffsetCw
                : shortestDirection != HoldDirection.Counterclockwise ? rightEdgeOffsetCcw : -rightEdgeOffsetCw,
                _ => throw new ArgumentOutOfRangeException(),
            };
            
            // Create local positions relative to start note.
            int localStartLeftEdge = startLeftEdge;
            int localStartRightEdge = startRightEdge;
            int localEndLeftEdge = startLeftEdge + signedLeftEdgeOffset;
            int localEndRightEdge = startRightEdge + signedRightEdgeOffset;
            
            // Get number of steps between start and end
            int steps = int.Max(int.Abs(signedLeftEdgeOffset), int.Abs(signedRightEdgeOffset));

            List<Note> segments = [];
            Note previousNote = startNote;

            // Step through and generate interpolated segments.
            for (int i = 0; i < steps - 1; i++)
            {
                float time = (float)(i + 1) / steps;
                float scaledTime = MathExtensions.HoldBakeEase(time, easeType);
                BeatData data = new((int)MathExtensions.Lerp(startNote.BeatData.FullTick, endNote.BeatData.FullTick, scaledTime));

                // To avoid overlapping segments,
                // stop early if the end note's timestamp is already reached.
                if (data.FullTick == endNote.BeatData.FullTick) break;
                
                // To avoid overlapping segments,
                // skip if the current timestamp is the same as the previous note's.
                if (data.FullTick == previousNote.BeatData.FullTick) continue;

                // Decide if a segment should be rendered or not.
                // Set to true if hold doesn't change size and segments are dense enough.
                // Set to true if EaseType is not Linear
                // Always set to false if forceNoRender is true.
                bool renderSegment = !forceNoRender && (data.FullTick - previousNote.BeatData.FullTick <= 30 && startNote.Size == endNote.Size || easeType != MathExtensions.HoldEaseType.Linear);
                
                int leftEdge = (int)float.Round(MathExtensions.Lerp(localStartLeftEdge, localEndLeftEdge, time));
                int rightEdge = (int)float.Round(MathExtensions.Lerp(localStartRightEdge, localEndRightEdge, time));
                
                int position = MathExtensions.Modulo(leftEdge, 60);
                int size = int.Clamp(rightEdge - leftEdge, 1, 60);
                
                Note newNote = new()
                {
                    BeatData = data,
                    NoteType = startNote.NoteType,
                    Position = position,
                    Size = size,
                    RenderSegment = renderSegment,
                    PrevReferencedNote = previousNote,
                    NextReferencedNote = endNote,
                };

                previousNote.NextReferencedNote = newNote;
                endNote.PrevReferencedNote = newNote;

                previousNote = newNote;
                segments.Add(newNote);
            }
            
            return new(Chart, SelectedNotes, segments, startNote, endNote);
        }
    }
    
    public void InsertHoldSegment()
    {
        if (HighlightedElement is not Note highlighted) return;
        if (!highlighted.IsNoteCollection) return;

        NoteCollection noteCollection = new() { Notes = highlighted.References().ToList() };

        if (CurrentBeatData.FullTick <= noteCollection.Notes[0].BeatData.FullTick) return;
        if (CurrentBeatData.FullTick >= noteCollection.Notes[^1].BeatData.FullTick) return;

        Note? previous = noteCollection.Notes.LastOrDefault(x => x.BeatData.FullTick <= CurrentBeatData.FullTick);
        Note? next = noteCollection.Notes.FirstOrDefault(x => x.BeatData.FullTick >= CurrentBeatData.FullTick);

        if (previous is null || next is null) return;

        if (CurrentBeatData.FullTick == previous.BeatData.FullTick ||
            CurrentBeatData.FullTick == next.BeatData.FullTick) return;
        
        Note note = new()
        {
            BeatData = CurrentBeatData,
            MaskDirection = CurrentMaskDirection,
            NoteType = highlighted.NoteType,
            Position = Cursor.Position,
            Size = Cursor.Size,
            NextReferencedNote = next,
            PrevReferencedNote = previous,
        };

        previous.NextReferencedNote = note;
        next.PrevReferencedNote = note;
        
        UndoRedoManager.InvokeAndPush(new InsertHoldSegment(Chart, SelectedNotes, note, previous, next));
        Chart.IsSaved = false;
    }

    public void StitchHold()
    {
        // Must not be in hold placement mode.
        // Must have only 2 notes selected.
        // Second Timestamp must be > First.
        // First must be HoldEnd.
        // Second must be HoldStart.

        if (EditorState is ChartEditorState.InsertHold) return;
        if (SelectedNotes.Count != 2) return;
        
        List<Note> sorted = SelectedNotes.OrderBy(x => x.BeatData.FullTick).ToList();
        Note first = sorted[0];
        Note second = sorted[1];
        
        if (first.BeatData.FullTick == second.BeatData.FullTick) return;
        if (first.NoteType != second.NoteType) return;
        if (first.LinkType != NoteLinkType.End) return;
        if (second.LinkType != NoteLinkType.Start) return;

        UndoRedoManager.InvokeAndPush(new StitchHold(Chart, first, second));
        Chart.IsSaved = false;
    }

    public void SplitHold()
    {
        // Must not be in hold placement mode.
        // Must select a Hold Segment note.
        
        if (EditorState is ChartEditorState.InsertHold) return;
        if (HighlightedElement is not Note highlighted) return;
        if (highlighted.LinkType != NoteLinkType.Point) return;

        Note newStart = new(highlighted, Guid.NewGuid())
        {
            PrevReferencedNote = null,
            NextReferencedNote = highlighted.NextReferencedNote,
            NoteType = highlighted.NoteType,
        };

        Note newEnd = new(highlighted, Guid.NewGuid())
        {
            PrevReferencedNote = highlighted.PrevReferencedNote,
            NextReferencedNote = null,
            NoteType = highlighted.NoteType,
        };
        
        UndoRedoManager.InvokeAndPush(new SplitHold(Chart, highlighted, newStart, newEnd));
        Chart.IsSaved = false;
        HighlightedElement = null;
    }

    public void DeleteSegments()
    {
        List<DeleteHoldNote> holdOperationList = [];
        
        foreach (Note selected in SelectedNotes.OrderByDescending(x => x.BeatData.FullTick))
        {
            if (!selected.IsNoteCollection || selected.RenderSegment) continue;
            if (selected.LinkType != NoteLinkType.Point) continue;
            
            addOperation(selected);   
        }
        
        // Temporarily undo all hold operations
        foreach (DeleteHoldNote _ in holdOperationList)
        {
            UndoRedoManager.Undo(false);
        }
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(holdOperationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            DeleteHoldNote holdOp = new(Chart, SelectedNotes, note, note.NextReferencedNote?.BonusType ?? BonusType.None);
            holdOperationList.Add(holdOp);
            UndoRedoManager.InvokeAndPush(holdOp);
        }
    }
    
    public void EditHold()
    {
        if (HighlightedElement is null or Gimmick) return;

        Note note = (Note)HighlightedElement;
        if (!note.IsNoteCollection) return;
        
        Note last = note.References().Last();

        HighlightedElement = last;
        StartHold(last);
    }
    
    public void StartHold(Note lastPlacedHold)
    {
        EditorState = ChartEditorState.InsertHold;
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();

        switch (lastPlacedHold.NoteType)
        {
            case NoteType.Hold:
            {
                mainView.RadioNoteHold.IsChecked = true;
                break;
            }

            case NoteType.Trace:
            {
                mainView.RadioNoteTrace.IsChecked = true;
                break;
            }
            default: throw new ArgumentOutOfRangeException();
        }
        
        CurrentNoteType = lastPlacedHold.NoteType;
        UpdateCursorNoteType();
        
        LastPlacedNote = lastPlacedHold;
        CurrentCollectionStart = lastPlacedHold.FirstReference();
    }
    
    public void EndHold()
    {
        if (EditorState is not ChartEditorState.InsertHold) return;
        
        EditorState = ChartEditorState.InsertNote;
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();

        if (LastPlacedNote?.LinkType == NoteLinkType.Start)
        {
            lock (Chart) Chart.Notes.Remove(LastPlacedNote);
            mainView.ConnectionManager.SendOperationMessage(new DeleteNote(Chart, SelectedNotes, LastPlacedNote), ConnectionManager.OperationDirection.Redo);
        }
    }
    
    public void ShiftChart(int ticks)
    {
        if (Chart.Notes.Count == 0 && Chart.Gimmicks.Count == 0) return;
        if (ticks == 0) return;
        
        List<IOperation> operationList = [];
        foreach (Note note in Chart.Notes)
        {
            addOperationNote(note);
        }

        foreach (Gimmick gimmick in Chart.Gimmicks)
        {
            if (gimmick == Chart.StartBpm || gimmick == Chart.StartTimeSig) continue;
            addOperationGimmick(gimmick);
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;
        
        void addOperationNote(Note note)
        {
            if (note.BeatData.FullTick == 0) return;
            
            Note newNote = new(note, note.Guid)
            {
                BeatData = new(note.BeatData.FullTick + ticks),
            };

            operationList.Add(new EditNote(note, newNote));
        }

        void addOperationGimmick(Gimmick gimmick)
        {
            if (gimmick.BeatData.FullTick == 0) return;
            
            Gimmick newGimmick = new(gimmick)
            {
                BeatData = new(gimmick.BeatData.FullTick + ticks),
            };

            operationList.Add(new EditGimmick(Chart, gimmick, newGimmick));
        }
    }

    public void MirrorChart(int axis = 30)
    {
        if (Chart.Notes.Count == 0 && Chart.Gimmicks.Count == 0) return;
        
        List<IOperation> operationList = [];
        foreach (Note note in Chart.Notes)
        {
            addOperation(note);
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note, note.Guid)
            {
                Position = MathExtensions.Modulo(axis - note.Size - note.Position, 60),
                NoteType = note.NoteType switch
                {
                    NoteType.SlideClockwise => NoteType.SlideCounterclockwise,
                    NoteType.SlideCounterclockwise => NoteType.SlideClockwise,
                    _ => note.NoteType,
                },
                MaskDirection = note.MaskDirection switch
                {
                    MaskDirection.Counterclockwise => MaskDirection.Clockwise,
                    MaskDirection.Clockwise => MaskDirection.Counterclockwise,
                    MaskDirection.Center => MaskDirection.Center,
                    _ => MaskDirection.Center,
                },
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }

    public void FixOffByOneErrors()
    {
        if (Chart.Notes.Count == 0 && Chart.Gimmicks.Count == 0) return;

        List<IOperation> operationList = [];
        
        foreach (Note note in Chart.Notes.Where(x => x.LinkType != NoteLinkType.Point)) addOperationNote(note);
        foreach (Gimmick gimmick in Chart.Gimmicks) addOperationGimmick(gimmick);
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperationNote(Note note)
        {
            Note newNote = new(note, note.Guid) { BeatData = new(quantize(note)) };
            operationList.Add(new EditNote(note, newNote));
        }

        void addOperationGimmick(Gimmick gimmick)
        {
            Gimmick newGimmick = new(gimmick) { BeatData = new(quantize(gimmick)) };
            operationList.Add(new EditGimmick(Chart, gimmick, newGimmick));
        }

        int quantize(ChartElement element)
        {
            int nearest = (int)float.Round(element.BeatData.FullTick / 10.0f) * 10;
            int difference = int.Abs(element.BeatData.FullTick - nearest);
            return difference != 1 ? element.BeatData.FullTick : nearest;
        }
    }

    public void GenerateSpikeHolds(bool offsetEven, int left, int right)
    {
        if (EditorState is ChartEditorState.InsertHold) return;
        
        HashSet<Note> checkedNotes = [];
        List<NoteCollection> holdNotes = [];
        List<IOperation> operationList = [];
        
        foreach (Note note in SelectedNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            NoteCollection noteCollection = new();

            foreach (Note reference in note.References())
            {
                noteCollection.Notes.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(noteCollection);
        }

        foreach (NoteCollection hold in holdNotes)
        {
            for (int i = 0; i < hold.Notes.Count; i++)
            {
                if (offsetEven ? i % 2 != 0 : i % 2 == 0) continue;

                Note note = hold.Notes[i];

                int position = MathExtensions.Modulo(note.Position - left, 60);
                int size = int.Clamp(note.Size + left + right, Note.MinSize(note.NoteType, note.BonusType, note.LinkType), Note.MaxSize(note.NoteType));
                
                Note newNote = new(note, note.Guid)
                {
                    Position = position,
                    Size = size,
                };

                operationList.Add(new EditNote(note, newNote));
            }
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
    }

    public void GenerateNoiseHolds(bool offsetEven, int leftMin, int leftMax, int rightMin, int rightMax)
    {
        if (EditorState is ChartEditorState.InsertHold) return;
        
        HashSet<Note> checkedNotes = [];
        List<NoteCollection> holdNotes = [];
        List<IOperation> operationList = [];
        Random random = new();
        
        foreach (Note note in SelectedNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            NoteCollection noteCollection = new();

            foreach (Note reference in note.References())
            {
                noteCollection.Notes.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(noteCollection);
        }

        foreach (NoteCollection hold in holdNotes)
        {
            for (int i = 0; i < hold.Notes.Count; i++)
            {
                if (offsetEven ? i % 2 != 0 : i % 2 == 0) continue;

                Note note = hold.Notes[i];

                // Can't trust the user to keep max >= min
                int lMin = int.Min(leftMin, leftMax);
                int lMax = int.Max(leftMin, leftMax);
                int rMin = int.Min(rightMin, rightMax);
                int rMax = int.Max(rightMin, rightMax);
                
                int left = random.Next(lMin, lMax);
                int right = random.Next(rMin, rMax);

                int position = MathExtensions.Modulo(note.Position - left, 60);
                int size = int.Clamp(note.Size + left + right, Note.MinSize(note.NoteType, note.BonusType, note.LinkType), Note.MaxSize(note.NoteType));
                
                
                Note newNote = new(note, note.Guid)
                {
                    Position = position,
                    Size = size,
                };

                operationList.Add(new EditNote(note, newNote));
            }
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
    }

    public void ReconstructHold(int generatorMethod, int beatDivision)
    {
        if (EditorState is ChartEditorState.InsertHold) return;
        
        HashSet<Note> checkedNotes = [];
        List<NoteCollection> holdNotes = [];
        List<IOperation> operationList = [];
        
        foreach (Note note in SelectedNotes)
        {
            if (checkedNotes.Contains(note)) continue;
            if (!note.IsNoteCollection) continue;

            NoteCollection noteCollection = new();

            foreach (Note reference in note.References())
            {
                noteCollection.Notes.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(noteCollection);
        }

        int interval = 1920 / beatDivision;
        
        foreach (NoteCollection hold in holdNotes)
        {
            int firstTick = hold.Notes[0].BeatData.FullTick;
            int lastTick = hold.Notes[^1].BeatData.FullTick;

            switch (generatorMethod)
            {
                case 0: holdToHold(hold, hold.Notes[0].NoteType, hold.Notes[0].BonusType, hold.Notes[0].Color, firstTick, lastTick); break;
                case 1: holdToChain(hold, firstTick, lastTick); break;
            }
            
            deleteHold(hold);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void holdToHold(NoteCollection hold, NoteType noteType, BonusType bonusType, TraceColor traceColor, int firstTick, int lastTick)
        {
            Note? last = null;
            for (int i = firstTick; i <= lastTick; i += interval)
            {
                int pos = hold.Notes.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Position;
                int size = hold.Notes.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Size;
                
                Note note = new()
                {
                    BeatData = new(i),
                    MaskDirection = MaskDirection.Center,
                    NoteType = noteType,
                    BonusType = last != null ? BonusType.None : bonusType,
                    Position = pos % 60,
                    Size = size,
                    PrevReferencedNote = last,
                    Color = traceColor,
                };
                
                note.Size = int.Clamp(note.Size, Note.MinSize(note.NoteType, note.BonusType, note.LinkType), Note.MaxSize(note.NoteType));


                if (last != null)
                {
                    last.NextReferencedNote = note;
                }
                
                last = note;

                operationList.Add(new InsertNote(Chart, SelectedNotes, note));
            }

            if (last != null && last.BeatData.FullTick < lastTick)
            {
                Note segment = hold.Notes[^1];
                
                Note note = new(segment)
                {
                    PrevReferencedNote = last,
                };

                last.NextReferencedNote = note;
                
                operationList.Add(new InsertNote(Chart, SelectedNotes, note));
            }
        }

        void holdToChain(NoteCollection hold, int firstTick, int lastTick)
        {
            for (int i = firstTick; i <= lastTick; i += interval)
            {
                int pos = hold.Notes.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Position;
                int size = hold.Notes.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Size;
                
                Note note = new()
                {
                    BeatData = new(i),
                    MaskDirection = MaskDirection.Center,
                    NoteType = NoteType.Chain,
                    Position = pos % 60,
                    Size = int.Clamp(size, Note.MinSize(NoteType.Chain, BonusType.None, NoteLinkType.Unlinked), Note.MaxSize(NoteType.Chain)),
                };

                operationList.Add(new InsertNote(Chart, SelectedNotes, note));
            }
        }
        
        void deleteHold(NoteCollection hold)
        {
            List<DeleteHoldNote> holdOperationList = [];
            
            foreach (Note note in hold.Notes.OrderByDescending(x => x.BeatData.FullTick))
            {
                DeleteHoldNote holdOp = new(Chart, SelectedNotes, note, note.NextReferencedNote?.BonusType ?? BonusType.None);
                holdOperationList.Add(holdOp);
                
                UndoRedoManager.InvokeAndPush(holdOp);
            }
            
            // Temporarily undo all hold operations, then add them to the operationList
            foreach (DeleteHoldNote deleteHoldOp in holdOperationList)
            {
                UndoRedoManager.Undo();
                operationList.Add(deleteHoldOp);
            }
        }
    }

    public void ConvertToInstantMask()
    {
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        DeselectAllNotes(); 
        return;

        void addOperation(Note note)
        {
            if (!note.IsMask) return;
            if (note is { Size: <= 2, MaskDirection: MaskDirection.Center }) return;

            int count = note.Size / 2;
            bool odd = (note.Size & 1) == 1;

            if (odd) count++;

            for (int i = 0; i < count; i++)
            {
                Note newNote = new()
                {
                    BeatData = new(note.BeatData),
                    Position = MathExtensions.Modulo(note.Position + i * 2, 60),
                    Size = (odd && i == count - 1) ? 1 : 2,
                    NoteType = note.NoteType,
                    MaskDirection = MaskDirection.Center,
                };

                operationList.Add(new InsertNote(Chart, SelectedNotes, newNote));
            }
            
            operationList.Add(new DeleteNote(Chart, SelectedNotes, note));
        }
    }

    public void PaintTraces(TraceColor color)
    {
        List<IOperation> operationList = [];
        HashSet<Note> paintedNotes = [];
        
        foreach (Note selected in SelectedNotes)
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            if (note.NoteType != NoteType.Trace) return;
            
            foreach (Note reference in note.References())
            {
                if (!paintedNotes.Add(reference)) continue;

                Note newNote = new(reference)
                {
                    Color = color,
                };

                operationList.Add(new EditNote(reference, newNote));
            }
        }
    }

    public void FlipNoteDirection()
    {
        List<IOperation> operationList = [];

        foreach (Note note in SelectedNotes)
        {
            addOperation(note);
        }
        
        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            if (!note.IsSlide && !note.IsSnap && !note.IsMask) return;

            Note newNote = new(note)
            {
                NoteType = note.NoteType switch
                {
                    NoteType.SlideClockwise => NoteType.SlideCounterclockwise,
                    NoteType.SlideCounterclockwise => NoteType.SlideClockwise,
                    
                    NoteType.SnapForward => NoteType.SnapBackward,
                    NoteType.SnapBackward => NoteType.SnapForward,
                    
                    _ => note.NoteType,
                },
                MaskDirection = note.MaskDirection switch
                {
                    MaskDirection.Counterclockwise => MaskDirection.Clockwise,
                    MaskDirection.Clockwise => MaskDirection.Counterclockwise,
                    MaskDirection.Center => MaskDirection.Center,
                    
                    _ => MaskDirection.Center,
                },
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }
    
    // ________________ Comments
    private readonly Color[] commentColors =
    [
        Colors.Red,
        Colors.OrangeRed,
        Colors.DarkOrange,
        Colors.Yellow, 
        Colors.GreenYellow, 
        Colors.Lime, 
        Colors.LimeGreen, 
        Colors.SpringGreen, 
        Colors.Aquamarine, 
        Colors.Aqua, 
        Colors.DeepSkyBlue, 
        Colors.DodgerBlue, 
        Colors.RoyalBlue,
        Colors.SlateBlue, 
        Colors.BlueViolet, 
        Colors.MediumPurple, 
        Colors.DeepPink,
    ];
    
    public void AddComment(BeatData beatData, string text)
    {
        Random random = new();
        
        Guid guid = Guid.NewGuid();
        Rectangle marker = new()
        {
            Name = guid.ToString(),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 3,
            RadiusX = 1.5,
            RadiusY = 1.5,
            Fill = new SolidColorBrush { Color = commentColors[random.Next(commentColors.Length)] },
        };

        marker.PointerPressed += Comment_PointerPressed;

        if (mainView.AudioManager.CurrentSong != null)
        {
            marker.Margin = new(Chart.BeatData2Timestamp(beatData) * (mainView.SliderSongPosition.Bounds.Width - 25) / mainView.AudioManager.CurrentSong.Length + 12.5, 0, 0, 0);
        }
        
        Comment newComment = new(guid, beatData, text, marker);
        
        Chart.Comments.Add(newComment.Guid.ToString(), newComment);
        Chart.Comments = Chart.Comments.OrderBy(x => x.Value.BeatData.FullTick).ToDictionary();
        mainView.PanelCommentMarker.Children.Add(marker);
        ToolTip.SetTip(marker, text);
        ToolTip.SetShowDelay(marker, 10);
    }

    public void RemoveComment(string name)
    {
        if (!Chart.Comments.TryGetValue(name, out Comment? comment)) return;

        comment.Marker.PointerPressed -= Comment_PointerPressed;
        mainView.PanelCommentMarker.Children.Remove(comment.Marker);
        Chart.Comments.Remove(comment.Guid.ToString());
    }

    public void ClearCommentMarkers()
    {
        List<KeyValuePair<string,Comment>> comments = Chart.Comments.ToList();
        foreach (KeyValuePair<string, Comment> comment in comments)
        {
            RemoveComment(comment.Key);
        }
    }
    
    public void UpdateCommentMarkers()
    {
        if (mainView.AudioManager.CurrentSong == null) return;
        
        foreach (KeyValuePair<string, Comment> comment in Chart.Comments)
        {
            comment.Value.Marker.Margin = new(Chart.BeatData2Timestamp(comment.Value.BeatData) * (mainView.SliderSongPosition.Bounds.Width - 25) / mainView.AudioManager.CurrentSong.Length + 12.5, 0, 0, 0);
        }
    }

    private async void Comment_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Rectangle marker) return;
        if (marker.Name == null) return;
        if (!Chart.Comments.TryGetValue(marker.Name, out Comment? comment)) return;
        
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Delete
            ContentDialog deleteCommentDialog = new()
            {
                Title = Assets.Lang.Resources.Generic_DeleteComment,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                CloseButtonText = Assets.Lang.Resources.Generic_No,
            };

            ContentDialogResult result = await deleteCommentDialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary) RemoveComment(marker.Name);
        }
        else
        {
            // Jump to comment
            if (mainView.UpdateSource == MainView.TimeUpdateSource.None)
            {
                CurrentMeasureDecimal = comment.BeatData.MeasureDecimal;
                mainView.UpdateTime(MainView.TimeUpdateSource.MeasureDecimal);
            }
        }
    }
}