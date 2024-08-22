using System;
using System.Collections.Generic;
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
            
            UpdateLastPlacedHold();
            
            // Repair holds after every operation to make sure forward references don't get mangled.
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

    public bool LayerNoteActive = true;
    public bool LayerMaskActive = true;
    public bool LayerGimmickActive = true;
    
    public List<Note> SelectedNotes { get; } = [];
    public Note? LastSelectedNote;
    public ChartElement? HighlightedElement;
    public BoxSelect BoxSelect = new();

    public List<Note> NoteClipboard { get; private set; } = [];

    public Note? LastPlacedHold;
    public Note? CurrentHoldStart;
    
    public void NewChart(string bgmFilepath, float bpm, int timeSigUpper, int timeSigLower)
    {
        LastSelectedNote = null;
        LastPlacedHold = null;
        HighlightedElement = null;
        EndHold(true);
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
                TimeStamp = 0
            };

            Gimmick startTimeSig = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.TimeSigChange,
                TimeSig = new(timeSigUpper, timeSigLower),
                TimeStamp = 0
            };

            Note startMask = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.None,
                NoteType = NoteType.MaskAdd,
                MaskDirection = MaskDirection.Center,
                Position = 15,
                Size = 60,
                RenderSegment = true
            };
            
            Chart.Gimmicks.Add(startBpm);
            Chart.Gimmicks.Add(startTimeSig);
            Chart.StartBpm = startBpm;
            Chart.StartTimeSig = startTimeSig;

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
        LastPlacedHold = null;
        HighlightedElement = null;
        EndHold(true);
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
        LastPlacedHold = null;
        HighlightedElement = null;
        EndHold(true);
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
        if (CurrentNoteType is not (NoteType.HoldStart or NoteType.HoldSegment or NoteType.HoldEnd)) EndHold(false);
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        mainView.SetMinNoteSize(CurrentNoteType, CurrentBonusType);
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
            if (insertNoteOperation.Note.NoteType is NoteType.HoldStart && EditorState is ChartEditorState.InsertHold)
            {
                CurrentHoldStart = null;
                EndHold(true);
            }
        }

        if (operation is DeleteHoldNote)
        {
            foreach (Note note in Chart.Notes)
            {
                if (note is not { IsHold: true, NextReferencedNote: null, PrevReferencedNote: null } || !UndoRedoManager.CanUndo) continue; 
                
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
            if (insertNoteOperation.Note.NoteType is NoteType.HoldStart)
            {
                StartHold(insertNoteOperation.Note);
            }
            else
            {
                EndHold(true);
            }
        }
        
        if (operation is DeleteHoldNote)
        {
            foreach (Note note in Chart.Notes)
            {
                if (note is not { IsHold: true, NextReferencedNote: null, PrevReferencedNote: null } || !UndoRedoManager.CanRedo) continue; 
                
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
        foreach (Note note in selectedNotes.Where(x => x.IsHold))
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

            if (note.IsHold)
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
    
    public void SelectHoldReferences()
    {
        List<Note> tempSelected = SelectedNotes.Where(x => x.IsHold).ToList(); // c# 8 syntax is.. a thing that exists. Looks cool I guess?
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

                mainView.SetMinNoteSize(NoteType.None, BonusType.None);
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
                mainView.SetMinNoteSize(CurrentNoteType, CurrentBonusType);
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

                int noteEnd = (x.Position + x.Size) % 60;
                int boxSelectEnd = (BoxSelect.Position + BoxSelect.Size) % 60;
                
                bool case1 = (x.Position <= noteEnd && BoxSelect.Position >= x.Position && BoxSelect.Position <= noteEnd) ||
                             (x.Position > noteEnd && (BoxSelect.Position >= x.Position || BoxSelect.Position <= noteEnd));

                bool case2 = (x.Position <= noteEnd && boxSelectEnd >= x.Position && boxSelectEnd <= noteEnd) ||
                             (x.Position > noteEnd && (boxSelectEnd >= x.Position || boxSelectEnd <= noteEnd));

                bool case3 = (BoxSelect.Position <= boxSelectEnd && x.Position >= BoxSelect.Position && x.Position <= boxSelectEnd) ||
                             (BoxSelect.Position > boxSelectEnd && (x.Position >= BoxSelect.Position || x.Position <= boxSelectEnd));

                bool case4 = (BoxSelect.Position <= boxSelectEnd && noteEnd >= BoxSelect.Position && noteEnd <= boxSelectEnd) ||
                             (BoxSelect.Position > boxSelectEnd && (noteEnd >= BoxSelect.Position || noteEnd <= boxSelectEnd));
                
                return inTimeRange && (BoxSelect.Size == 60 || case1 || case2 || case3 || case4);
            });
            
            foreach (Note note in selectable)
            {
                if (!SelectedNotes.Contains(note)) SelectedNotes.Add(note);
            }
        }
    }

    public void StopBoxSelect()
    {
        mainView.SetMinNoteSize(CurrentNoteType, CurrentBonusType);
        EditorState = ChartEditorState.InsertNote;
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
        LastPlacedHold = LastPlacedHold?.References().LastOrDefault();
    }

    public void RepairHoldsForward()
    {
        foreach (Note note in Chart.Notes)
        {
            if (!note.IsHold) continue;
            if (note.NextReferencedNote != null) note.NextReferencedNote.PrevReferencedNote = note;
        }
    }
    
    // ________________ ChartElement Operations
    public void InsertNote()
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        switch (EditorState)
        {
            case ChartEditorState.InsertNote:
            {
                // Something must have gone terribly wrong and the user is placing hold segments/ends not attached to a hold.
                // Just force start a new hold as that's what they probably expect to happen.
                if (CurrentNoteType is NoteType.HoldSegment or NoteType.HoldEnd)
                {
                    CurrentNoteType = NoteType.HoldStart;
                    UpdateCursorNoteType();
                }
                
                bool endOfChart = CurrentNoteType is NoteType.EndOfChart;
            
                Note note = new()
                {
                    BeatData = CurrentBeatData,
                    GimmickType = GimmickType.None,
                    MaskDirection = CurrentMaskDirection,
                    NoteType = CurrentNoteType,
                    BonusType = CurrentBonusType,
                    Position = endOfChart ? 0 : Cursor.Position,
                    Size = endOfChart ? 60 : Cursor.Size
                };
                
                Chart.IsSaved = false;
                UndoRedoManager.InvokeAndPush(new InsertNote(Chart, SelectedNotes, note));
                
                if (note.NoteType is NoteType.HoldStart)
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

                if (LastPlacedHold is null || CurrentHoldStart is null) return;
                if (CurrentBeatData.FullTick <= CurrentHoldStart.BeatData.FullTick) return;
                if (CurrentBeatData.FullTick <= LastPlacedHold.BeatData.FullTick) return;
            
                Note note = new()
                {
                    BeatData = CurrentBeatData,
                    GimmickType = GimmickType.None,
                    MaskDirection = CurrentMaskDirection,
                    NoteType = NoteType.HoldEnd,
                    BonusType = BonusType.None,
                    Position = Cursor.Position,
                    Size = Cursor.Size,
                    PrevReferencedNote = LastPlacedHold
                };

                LastPlacedHold.NextReferencedNote = note;
                if (LastPlacedHold.NoteType is NoteType.HoldEnd)
                {
                    LastPlacedHold.NoteType = NoteType.HoldSegment;
                }

                UndoRedoManager.InvokeAndPush(new InsertHoldNote(Chart, SelectedNotes, note, LastPlacedHold));
                Chart.IsSaved = false;
                LastPlacedHold = note;
                
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
            GimmickType = GimmickType.BpmChange
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
            GimmickType = GimmickType.TimeSigChange
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
            GimmickType = GimmickType.HiSpeedChange
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
            GimmickType = GimmickType.StopStart
        };

        Gimmick endGimmick = new()
        {
            BeatData = new(end),
            GimmickType = GimmickType.StopEnd
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
            GimmickType = GimmickType.ReverseEffectStart
        };

        Gimmick effectEndGimmick = new()
        {
            BeatData = new(effectEnd),
            GimmickType = GimmickType.ReverseEffectEnd
        };
        
        Gimmick noteEndGimmick = new()
        {
            BeatData = new(noteEnd),
            GimmickType = GimmickType.ReverseNoteEnd
        };
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation([new InsertGimmick(Chart, effectStartGimmick), new InsertGimmick(Chart, effectEndGimmick), new InsertGimmick(Chart, noteEndGimmick)]));
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
                    Bpm = (float)gimmickView.BpmNumberBox.Value
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
                PrimaryButtonText = Assets.Lang.Resources.Generic_Edit
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
                    TimeSig = new((int)gimmickView.TimeSigUpperNumberBox.Value, (int)gimmickView.TimeSigLowerNumberBox.Value)
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
                PrimaryButtonText = Assets.Lang.Resources.Generic_Edit
            };
        
            Dispatcher.UIThread.Post(async () =>
            {
                ContentDialogResult result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary) return;

                Gimmick newGimmick = new(highlightedGimmick)
                {
                    HiSpeed = (float)gimmickView.HiSpeedNumberBox.Value
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
        
        if (EditorState is ChartEditorState.InsertHold && CurrentHoldStart != null && SelectedNotes.Contains(CurrentHoldStart))
        {
            EndHold(true);
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

            if (selected.IsHold)
            {
                // Compare how many references the hold has to how many of those references are in SelectedNotes.
                // If refsInSelected is one less than refsTotal, then add the entire list including the missing one.
                // Then continue to avoid adding segments twice.
                Note[] referencesTotal = selected.References().ToArray();
                Note[] referencesInSelected = SelectedNotes.Intersect(referencesTotal).ToArray();

                if (referencesTotal.Length == referencesInSelected.Length + 1)
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
        if (EditorState == ChartEditorState.InsertHold && LastPlacedHold != null)
        {
            LastPlacedHold = LastPlacedHold.FirstReference();
        }
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            if (note.IsHold)
            {
                if (LastPlacedHold != null && EditorState is ChartEditorState.InsertHold)
                {
                    // If user is currently inserting a hold and trying to delete all segments (or all but one) of the hold they're currently deleting, then end hold.
                    if (!checkedCurrentHolds.Contains(note))
                    {
                        List<Note> unselectedCurrentReferences = LastPlacedHold.References().Where(x => !SelectedNotes.Contains(x)).ToList();
                        if (unselectedCurrentReferences.Count <= 1) EndHold(true);
                        checkedCurrentHolds.AddRange(LastPlacedHold.References());
                    }
                }

                DeleteHoldNote holdOp = new(Chart, SelectedNotes, note, note.References().FirstOrDefault()?.BonusType ?? BonusType.None);
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
                Size = int.Max(newSize, Note.MinSize(newNoteType, newBonusType)),
                NoteType = newNoteType,
                BonusType = newBonusType,
                MaskDirection = newDirection
            };

            operationList.Add(new EditNote(note, newNote));
        }

        NoteType editNoteType(Note note, NoteType currentNoteType)
        {
            // EndOfChart, HoldSegment and HoldEnd are not editable.
            if (note.NoteType is NoteType.EndOfChart or NoteType.HoldSegment or NoteType.HoldEnd) return note.NoteType;
            
            // Cannot edit a note into EndOfChart, HoldSegment or HoldEnd.
            if (currentNoteType is NoteType.EndOfChart or NoteType.HoldSegment or NoteType.HoldEnd) return note.NoteType;

            // HoldStart and HoldStartRNote cannot be edited into other note types.
            if (note.NoteType is NoteType.HoldStart && currentNoteType is not NoteType.HoldStart) return note.NoteType;

            // Other note types cannot be edited into HoldStart and HoldStartRNote.
            if (note.NoteType is not NoteType.HoldStart && currentNoteType is NoteType.HoldStart) return note.NoteType;
            
            return currentNoteType;
        }

        BonusType editBonusType(Note note, BonusType currentBonusType)
        {
            // EndOfChart, HoldSegment and HoldEnd are not editable.
            return note.NoteType is NoteType.EndOfChart or NoteType.HoldSegment or NoteType.HoldEnd ? note.BonusType : currentBonusType;
        }
    }

    public void SetSelectionRenderFlag(bool render)
    {
        // This operation only works on hold segments. All other note types should have a flag of 1 by default.
        List<IOperation> operationList = [];
        foreach (Note selected in SelectedNotes.Where(x => x.NoteType is NoteType.HoldSegment))
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note { NoteType: NoteType.HoldSegment } highlighted)
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
                RenderSegment = render
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
                Size = int.Clamp(note.Size + delta, Note.MinSize(note.NoteType, note.BonusType), 60)
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
                Size = note.Size
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
                BeatData = new(float.Clamp(note.BeatData.MeasureDecimal + divisor, 0, endOfChartMeasureDecimal))
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
            }

            float newMeasureDecimal = gimmick.BeatData.MeasureDecimal + divisor;
            if (MathExtensions.GreaterAlmostEqual(newMeasureDecimal, max) || MathExtensions.LessAlmostEqual(newMeasureDecimal, min)) return;
            
            Gimmick newGimmick = new(gimmick)
            {
                BeatData = new(float.Clamp(gimmick.BeatData.MeasureDecimal + divisor, min, max))
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
                    _ => note.NoteType
                },
                MaskDirection = note.MaskDirection switch
                {
                    MaskDirection.Counterclockwise => MaskDirection.Clockwise,
                    MaskDirection.Clockwise => MaskDirection.Counterclockwise,
                    MaskDirection.Center => MaskDirection.Center,
                    _ => MaskDirection.Center
                }
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }

    private enum HoldDirection
    {
        Clockwise,
        Counterclockwise,
        Symmetrical,
        None
    }

    private enum HoldSide
    {
        Left,
        Right
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
            if (note.NoteType is not (NoteType.HoldStart or NoteType.HoldSegment) || note.NextReferencedNote is null) return;
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
                _ => throw new ArgumentOutOfRangeException()
            };

            int signedRightEdgeOffset = finalDirection switch
            {
                HoldDirection.Clockwise => -rightEdgeOffsetCw,
                HoldDirection.Counterclockwise => rightEdgeOffsetCcw,
                HoldDirection.Symmetrical => shortestSide == HoldSide.Right
                ? shortestDirection == HoldDirection.Counterclockwise ? rightEdgeOffsetCcw : -rightEdgeOffsetCw
                : shortestDirection != HoldDirection.Counterclockwise ? rightEdgeOffsetCcw : -rightEdgeOffsetCw,
                _ => throw new ArgumentOutOfRangeException()
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
                    NoteType = NoteType.HoldSegment,
                    Position = position,
                    Size = size,
                    RenderSegment = renderSegment,
                    PrevReferencedNote = previousNote,
                    NextReferencedNote = endNote
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
        if (!highlighted.IsHold) return;

        Hold hold = new() { Segments = highlighted.References().ToList() };

        if (CurrentBeatData.FullTick <= hold.Segments[0].BeatData.FullTick) return;
        if (CurrentBeatData.FullTick >= hold.Segments[^1].BeatData.FullTick) return;

        Note? previous = hold.Segments.LastOrDefault(x => x.BeatData.FullTick <= CurrentBeatData.FullTick);
        Note? next = hold.Segments.FirstOrDefault(x => x.BeatData.FullTick >= CurrentBeatData.FullTick);

        if (previous is null || next is null) return;

        if (CurrentBeatData.FullTick == previous.BeatData.FullTick ||
            CurrentBeatData.FullTick == next.BeatData.FullTick) return;
        
        Note note = new()
        {
            BeatData = CurrentBeatData,
            GimmickType = GimmickType.None,
            MaskDirection = CurrentMaskDirection,
            NoteType = NoteType.HoldSegment,
            Position = Cursor.Position,
            Size = Cursor.Size,
            NextReferencedNote = next,
            PrevReferencedNote = previous
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
        if (first.NoteType is not NoteType.HoldEnd) return;
        if (second.NoteType is not (NoteType.HoldStart)) return;

        UndoRedoManager.InvokeAndPush(new StitchHold(Chart, first, second, second.NoteType));
        Chart.IsSaved = false;
    }

    public void SplitHold()
    {
        // Must not be in hold placement mode.
        // Must select a Hold Segment note.
        
        if (EditorState is ChartEditorState.InsertHold) return;
        if (HighlightedElement is not Note highlighted) return;
        if (highlighted.NoteType != NoteType.HoldSegment) return;

        Note newStart = new(highlighted, Guid.NewGuid())
        {
            PrevReferencedNote = null,
            NextReferencedNote = highlighted.NextReferencedNote,
            NoteType = NoteType.HoldStart
        };

        Note newEnd = new(highlighted, Guid.NewGuid())
        {
            PrevReferencedNote = highlighted.PrevReferencedNote,
            NextReferencedNote = null,
            NoteType = NoteType.HoldEnd
        };
        
        UndoRedoManager.InvokeAndPush(new SplitHold(Chart, highlighted, newStart, newEnd));
        Chart.IsSaved = false;
        HighlightedElement = null;
    }
    
    public void EditHold()
    {
        if (HighlightedElement is null or Gimmick) return;

        Note note = (Note)HighlightedElement;
        if (!note.IsHold) return;
        
        Note last = note.References().Last();

        HighlightedElement = last;
        StartHold(last);
    }
    
    public void StartHold(Note lastPlacedHold)
    {
        EditorState = ChartEditorState.InsertHold;
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        mainView.RadioNoteHold.IsChecked = true;
        CurrentNoteType = NoteType.HoldEnd;
        UpdateCursorNoteType();
        
        LastPlacedHold = lastPlacedHold;
        CurrentHoldStart = lastPlacedHold.FirstReference();
    }
    
    public void EndHold(bool setNoteType)
    {
        if (EditorState is not ChartEditorState.InsertHold) return;
        
        EditorState = ChartEditorState.InsertNote;
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        if (setNoteType) CurrentNoteType = NoteType.HoldStart;

        if (LastPlacedHold?.NoteType is NoteType.HoldStart)
        {
            lock (Chart) Chart.Notes.Remove(LastPlacedHold);
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
            Note newNote = new(note, note.Guid)
            {
                BeatData = new(note.BeatData.FullTick + ticks)
            };

            operationList.Add(new EditNote(note, newNote));
        }

        void addOperationGimmick(Gimmick gimmick)
        {
            Gimmick newGimmick = new(gimmick)
            {
                BeatData = new(gimmick.BeatData.FullTick + ticks)
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
                    _ => note.NoteType
                },
                MaskDirection = note.MaskDirection switch
                {
                    MaskDirection.Counterclockwise => MaskDirection.Clockwise,
                    MaskDirection.Clockwise => MaskDirection.Counterclockwise,
                    MaskDirection.Center => MaskDirection.Center,
                    _ => MaskDirection.Center
                }
            };

            operationList.Add(new EditNote(note, newNote));
        }
    }

    public void FixOffByOneErrors()
    {
        if (Chart.Notes.Count == 0 && Chart.Gimmicks.Count == 0) return;

        List<IOperation> operationList = [];
        
        foreach (Note note in Chart.Notes.Where(x => x.NoteType is not NoteType.HoldSegment)) addOperationNote(note);
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
        List<Hold> holdNotes = [];
        List<IOperation> operationList = [];
        
        foreach (Note note in SelectedNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            Hold hold = new();

            foreach (Note reference in note.References())
            {
                hold.Segments.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(hold);
        }

        foreach (Hold hold in holdNotes)
        {
            for (int i = 0; i < hold.Segments.Count; i++)
            {
                if (offsetEven ? i % 2 != 0 : i % 2 == 0) continue;

                Note note = hold.Segments[i];

                int position = (note.Position - left) % 60;
                int size = int.Clamp(note.Size + left + right, Note.MinSize(note.NoteType, note.BonusType), 60);
                
                Note newNote = new(note, note.Guid)
                {
                    Position = position,
                    Size = size
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
        List<Hold> holdNotes = [];
        List<IOperation> operationList = [];
        Random random = new();
        
        foreach (Note note in SelectedNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            Hold hold = new();

            foreach (Note reference in note.References())
            {
                hold.Segments.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(hold);
        }

        foreach (Hold hold in holdNotes)
        {
            for (int i = 0; i < hold.Segments.Count; i++)
            {
                if (offsetEven ? i % 2 != 0 : i % 2 == 0) continue;

                Note note = hold.Segments[i];

                // Can't trust the user to keep max >= min
                int lMin = int.Min(leftMin, leftMax);
                int lMax = int.Max(leftMin, leftMax);
                int rMin = int.Min(rightMin, rightMax);
                int rMax = int.Max(rightMin, rightMax);
                
                int left = random.Next(lMin, lMax);
                int right = random.Next(rMin, rMax);

                int position = (note.Position - left) % 60;
                int size = int.Clamp(note.Size + left + right, Note.MinSize(note.NoteType, note.BonusType), 60);
                
                Note newNote = new(note, note.Guid)
                {
                    Position = position,
                    Size = size
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
        List<Hold> holdNotes = [];
        List<IOperation> operationList = [];
        
        foreach (Note note in SelectedNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            Hold hold = new();

            foreach (Note reference in note.References())
            {
                hold.Segments.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(hold);
        }

        int interval = 1920 / beatDivision;
        
        foreach (Hold hold in holdNotes)
        {
            int firstTick = hold.Segments[0].BeatData.FullTick;
            int lastTick = hold.Segments[^1].BeatData.FullTick;

            switch (generatorMethod)
            {
                case 0: holdToHold(hold, hold.Segments[0].BonusType, firstTick, lastTick); break;
                case 1: holdToChain(hold, firstTick, lastTick); break;
            }
            
            deleteHold(hold);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void holdToHold(Hold hold, BonusType bonusType, int firstTick, int lastTick)
        {
            Note? last = null;
            for (int i = firstTick; i <= lastTick; i += interval)
            {
                int pos = hold.Segments.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Position;
                int size = hold.Segments.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Size;
                
                Note note = new()
                {
                    BeatData = new(i),
                    GimmickType = GimmickType.None,
                    MaskDirection = MaskDirection.Center,
                    NoteType = last != null ? NoteType.HoldEnd : NoteType.HoldStart,
                    BonusType = last != null ? BonusType.None : bonusType,
                    Position = pos % 60,
                    Size = int.Max(size, Note.MinSize(last != null ? NoteType.HoldEnd : NoteType.HoldStart, last != null ? BonusType.None : bonusType)),
                    PrevReferencedNote = last,
                };

                if (last != null)
                {
                    last.NextReferencedNote = note;
                    if (last.NoteType == NoteType.HoldEnd) last.NoteType = NoteType.HoldSegment;
                }
                
                last = note;

                operationList.Add(new InsertNote(Chart, SelectedNotes, note));
            }

            if (last != null && last.BeatData.FullTick < lastTick)
            {
                Note segment = hold.Segments[^1];
                
                Note note = new(segment)
                {
                    PrevReferencedNote = last,
                };

                last.NextReferencedNote = note;
                if (last.NoteType == NoteType.HoldEnd) last.NoteType = NoteType.HoldSegment;
                
                operationList.Add(new InsertNote(Chart, SelectedNotes, note));
            }
        }

        void holdToChain(Hold hold, int firstTick, int lastTick)
        {
            for (int i = firstTick; i <= lastTick; i += interval)
            {
                int pos = hold.Segments.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Position;
                int size = hold.Segments.MinBy(x => int.Abs(i - x.BeatData.FullTick))!.Size;
                
                Note note = new()
                {
                    BeatData = new(i),
                    GimmickType = GimmickType.None,
                    MaskDirection = MaskDirection.Center,
                    NoteType = NoteType.Chain,
                    Position = pos % 60,
                    Size = int.Max(size, Note.MinSize(NoteType.Chain, BonusType.None))
                };

                operationList.Add(new InsertNote(Chart, SelectedNotes, note));
            }
        }
        
        void deleteHold(Hold hold)
        {
            List<DeleteHoldNote> holdOperationList = [];
            
            foreach (Note note in hold.Segments.OrderByDescending(x => x.BeatData.FullTick))
            {
                DeleteHoldNote holdOp = new(Chart, SelectedNotes, note, hold.Segments[0].BonusType);
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
                    MaskDirection = MaskDirection.Center
                };

                operationList.Add(new InsertNote(Chart, SelectedNotes, newNote));
            }
            
            operationList.Add(new DeleteNote(Chart, SelectedNotes, note));
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
        Colors.DeepPink
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
                CloseButtonText = Assets.Lang.Resources.Generic_No
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