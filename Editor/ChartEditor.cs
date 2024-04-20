using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MercuryMapper.Data;
using MercuryMapper.Enums;
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
        UndoRedoManager.OperationHistoryChanged += (_, _) =>
        {
            Chart.Notes = Chart.Notes.OrderBy(x => x.BeatData.FullTick).ToList();
            Chart.Gimmicks = Chart.Gimmicks.OrderBy(x => x.BeatData.FullTick).ToList();
            Chart.IsSaved = false;
            
            mainView.ToggleInsertButton();
            mainView.SetSelectionInfo();
        };
    }
    
    private readonly MainView mainView;
    
    public readonly Cursor Cursor = new();
    public readonly UndoRedoManager UndoRedoManager = new();
    public Chart Chart { get; private set; } = new();
    
    public ChartEditorState EditorState { get; private set; }
    
    public float CurrentMeasureDecimal { get; set; }
    public NoteType CurrentNoteType { get; set; } = NoteType.Touch;
    public BonusType CurrentBonusType { get; set; } = BonusType.None;
    public MaskDirection CurrentMaskDirection { get; set; } = MaskDirection.Clockwise;

    public List<Note> SelectedNotes { get; } = [];
    public Note? LastSelectedNote;
    public ChartElement? HighlightedElement;

    public List<Note> Clipboard { get; private set; } = [];

    public Note? LastPlacedHold;
    public Note? CurrentHoldStart;
    
    public void NewChart(string musicFilePath, string author, float bpm, int timeSigUpper, int timeSigLower)
    {
        LastSelectedNote = null;
        LastPlacedHold = null;
        HighlightedElement = null;
        EndHold(true);
        UndoRedoManager.Clear();
        SelectedNotes.Clear();
        
        Chart = new()
        {
            AudioFilePath = musicFilePath,
            Author = author
        };

        lock (Chart)
        {
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
        UndoRedoManager.Clear();
        SelectedNotes.Clear();
        
        Chart.LoadFile(path);
        mainView.SetChartInfo();
        mainView.SetSelectionInfo();
    }
    
    public void UpdateCursorNoteType()
    {
        switch (CurrentNoteType)
        {
            case NoteType.Touch:
            case NoteType.TouchBonus:
            case NoteType.TouchRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.Touch,
                    BonusType.Bonus => NoteType.TouchBonus,
                    BonusType.RNote => NoteType.TouchRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SnapForward:
            case NoteType.SnapForwardRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SnapForward,
                    BonusType.Bonus => NoteType.SnapForward,
                    BonusType.RNote => NoteType.SnapForwardRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SnapBackward:
            case NoteType.SnapBackwardRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SnapBackward,
                    BonusType.Bonus => NoteType.SnapBackward,
                    BonusType.RNote => NoteType.SnapBackwardRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SlideClockwise:
            case NoteType.SlideClockwiseBonus:
            case NoteType.SlideClockwiseRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SlideClockwise,
                    BonusType.Bonus => NoteType.SlideClockwiseBonus,
                    BonusType.RNote => NoteType.SlideClockwiseRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SlideCounterclockwise:
            case NoteType.SlideCounterclockwiseBonus:
            case NoteType.SlideCounterclockwiseRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SlideCounterclockwise,
                    BonusType.Bonus => NoteType.SlideCounterclockwiseBonus,
                    BonusType.RNote => NoteType.SlideCounterclockwiseRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.HoldStart:
            case NoteType.HoldStartRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.HoldStart,
                    BonusType.Bonus => NoteType.HoldStart,
                    BonusType.RNote => NoteType.HoldStartRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.HoldSegment:
            {
                CurrentNoteType = NoteType.HoldSegment;
                break;
            }
            
            case NoteType.HoldEnd:
            {
                CurrentNoteType = NoteType.HoldEnd;
                break;
            }
            
            case NoteType.Chain:
            case NoteType.ChainRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.Chain,
                    BonusType.Bonus => NoteType.Chain,
                    BonusType.RNote => NoteType.ChainRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.MaskAdd:
            {
                CurrentNoteType = NoteType.MaskAdd;
                break;
            }
            
            case NoteType.MaskRemove:
            {
                CurrentNoteType = NoteType.MaskRemove;
                break;
            }
            
            case NoteType.EndOfChart:
            {
                CurrentNoteType = NoteType.EndOfChart;
                break;
            }
            
            default: return;
        }
        
        // Reset Editor State
        EndHold(false);
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        mainView.SetMinNoteSize(CurrentNoteType);
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
            if (insertNoteOperation.Note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote && EditorState is ChartEditorState.InsertHold)
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
            if (insertNoteOperation.Note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
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
    
    public void Cut()
    {
        if (TopLevel.GetTopLevel(mainView)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (SelectedNotes.Count == 0) return;
        
        CopyToClipboard(SelectedNotes);
        DeleteSelection();
        DeselectAllNotes();
    }
    
    public void Copy()
    {
        if (TopLevel.GetTopLevel(mainView)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (SelectedNotes.Count == 0) return;
        
        CopyToClipboard(SelectedNotes);
        DeselectAllNotes();
    }
    
    public void Paste()
    {
        if (TopLevel.GetTopLevel(mainView)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (Clipboard.Count == 0) return;
        
        DeselectAllNotes();
        List<Note> copy = DeepCloneNotes(Clipboard);

        List<IOperation> operationList = [];
        
        foreach (Note note in copy)
        {
            SelectedNotes.Add(note);
            note.BeatData = new(CurrentMeasureDecimal + note.BeatData.MeasureDecimal);
            
            operationList.Add(new InsertNote(Chart, SelectedNotes, note));
        }
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        mainView.SetSelectionInfo();
    }

    private void CopyToClipboard(List<Note> selectedNotes)
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
        
        Clipboard.Clear();
        Clipboard = DeepCloneNotes(tempSelected);

        foreach (Note note in Clipboard)
        {
            note.BeatData = new(note.BeatData.MeasureDecimal - start.MeasureDecimal);
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

        List<ChartElement> elements = Chart.Notes.Concat<ChartElement>(Chart.Gimmicks).OrderBy(x => x.BeatData.FullTick).ToList();
        int index = elements.IndexOf(HighlightedElement);
        HighlightedElement = elements[MathExtensions.Modulo(index + 1, elements.Count)];
        mainView.SetSelectionInfo();
    }

    public void HighlightPrevElement()
    {
        if (HighlightedElement is null) return;
        
        List<ChartElement> elements = Chart.Notes.Concat<ChartElement>(Chart.Gimmicks).OrderBy(x => x.BeatData.FullTick).ToList();
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
    
    // ________________ ChartElement Operations
    public void InsertNote()
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        BeatData data = new(CurrentMeasureDecimal);

        switch (EditorState)
        {
            case ChartEditorState.InsertNote:
            {
                bool endOfChart = CurrentNoteType is NoteType.EndOfChart;
            
                Note note = new()
                {
                    BeatData = data,
                    GimmickType = GimmickType.None,
                    MaskDirection = CurrentMaskDirection,
                    NoteType = CurrentNoteType,
                    Position = endOfChart ? 0 : Cursor.Position,
                    Size = endOfChart ? 60 : Cursor.Size
                };
                
                Chart.IsSaved = false;
                UndoRedoManager.InvokeAndPush(new InsertNote(Chart, SelectedNotes, note));
                
                if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
                {
                    StartHold(note);
                }
                if (mainView.UserConfig.EditorConfig.HighlightPlacedNote) HighlightElement(note);
                break;
            }
            
            case ChartEditorState.InsertHold:
            {
                // Place Hold End
                // Hold end's prevReferencedNote is LastPlacedNote
                // LastPlacedNote's nextReferencedNote is Hold End
                // If previous note is hold end, convert it to hold segment

                if (LastPlacedHold is null || CurrentHoldStart is null) return;
                if (data.FullTick <= CurrentHoldStart.BeatData.FullTick) return;
                if (data.FullTick <= LastPlacedHold.BeatData.FullTick) return;
            
                Note note = new()
                {
                    BeatData = data,
                    GimmickType = GimmickType.None,
                    MaskDirection = CurrentMaskDirection,
                    NoteType = NoteType.HoldEnd,
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
        }
    }

    public void InsertBpmChange(float bpm)
    {
        if (Chart.StartBpm is null || Chart.StartTimeSig is null) return;
        
        Gimmick gimmick = new()
        {
            BeatData = new(CurrentMeasureDecimal),
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
            BeatData = new(CurrentMeasureDecimal),
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
            BeatData = new(CurrentMeasureDecimal),
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
        
        if (HighlightedElement.GimmickType is GimmickType.BpmChange)
        {
            GimmickView_Bpm gimmickView = new();
            ContentDialog dialog = new()
            {
                Content = gimmickView,
                Title = Assets.Lang.Resources.Editor_EditGimmick,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Create
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
                
                Gimmick oldGimmick = (Gimmick)HighlightedElement;
                Gimmick newGimmick = new(oldGimmick)
                {
                    Bpm = (float)gimmickView.BpmNumberBox.Value
                };
                
                UndoRedoManager.InvokeAndPush(new EditGimmick(Chart, oldGimmick, newGimmick));
            });
        }
        
        if (HighlightedElement.GimmickType is GimmickType.TimeSigChange)
        {
            GimmickView_TimeSig gimmickView = new();
            ContentDialog dialog = new()
            {
                Content = gimmickView,
                Title = Assets.Lang.Resources.Editor_EditGimmick,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Create
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
                
                Gimmick oldGimmick = (Gimmick)HighlightedElement;
                Gimmick newGimmick = new(oldGimmick)
                {
                    TimeSig = new((int)gimmickView.TimeSigUpperNumberBox.Value, (int)gimmickView.TimeSigLowerNumberBox.Value)
                };
                
                UndoRedoManager.InvokeAndPush(new EditGimmick(Chart, oldGimmick, newGimmick));
            });
        }
        
        if (HighlightedElement.GimmickType is GimmickType.HiSpeedChange)
        {
            GimmickView_HiSpeed gimmickView = new();
            ContentDialog dialog = new()
            {
                Content = gimmickView,
                Title = Assets.Lang.Resources.Editor_EditGimmick,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Create
            };
        
            Dispatcher.UIThread.Post(async () =>
            {
                ContentDialogResult result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary) return;

                Gimmick oldGimmick = (Gimmick)HighlightedElement;
                Gimmick newGimmick = new(oldGimmick)
                {
                    HiSpeed = (float)gimmickView.HiSpeedNumberBox.Value
                };
                
                UndoRedoManager.InvokeAndPush(new EditGimmick(Chart, oldGimmick, newGimmick));
            });
        }

        Chart.IsSaved = false;
    }

    public void DeleteGimmick()
    {
        if (HighlightedElement is null or Note) return;

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

        List<Note> checkedHolds = [];
        List<Note> checkedCurrentHolds = [];
        
        foreach (Note selected in SelectedNotes.OrderByDescending(x => x.BeatData.FullTick))
        {
            addOperation(selected);
        }

        if (SelectedNotes.Count == 0 && HighlightedElement is Note highlighted)
        {
            addOperation(highlighted);
            HighlightedElement = Chart.Notes.LastOrDefault(x => x.BeatData.FullTick <= highlighted.BeatData.FullTick && x != highlighted);
        }

        // Temporarily undo all hold operations, then add them to the operationList
        foreach (DeleteHoldNote deleteHoldOp in holdOperationList)
        {
            UndoRedoManager.Undo();
            operationList.Add(deleteHoldOp);
        }
        
        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            if (note.IsHold)
            {
                Note? newLastPlacedHold = null;
                
                if (LastPlacedHold != null && EditorState is ChartEditorState.InsertHold)
                {
                    // If user is currently inserting a hold and trying to delete all segments (or all but one) of the hold they're currently deleting, then end hold.
                    if (!checkedCurrentHolds.Contains(note))
                    {
                        List<Note> unselectedCurrentReferences = LastPlacedHold.References().Where(x => !SelectedNotes.Contains(x)).ToList();
                        if (unselectedCurrentReferences.Count <= 1) EndHold(true);
                        checkedCurrentHolds.AddRange(LastPlacedHold.References());
                    }
                    
                    // Update LastPlacedHold to previous if it's being deleted.
                    if (LastPlacedHold == note)
                    {
                        newLastPlacedHold = note.PrevReferencedNote;
                    }
                }
                
                DeleteHoldNote holdOp = new(Chart, this, SelectedNotes, note, newLastPlacedHold);
                holdOperationList.Add(holdOp);
                
                DeleteHoldNote? holdOp2 = null;
                
                // If deleting all but one segment, delete the last one too.
                // Creating holdOp2 early and null checking is just to preserve order of operations.
                if (!checkedHolds.Contains(note))
                {
                    List<Note> unselectedReferences = SelectedNotes.Count != 0
                        ? note.References().Where(x => !SelectedNotes.Contains(x)).ToList()
                        : note.References().Where(x => x != HighlightedElement).ToList();
                    
                    if (unselectedReferences.Count == 1)
                    {
                        holdOp2 = new(Chart, this, SelectedNotes, unselectedReferences[0], newLastPlacedHold);
                        holdOperationList.Add(holdOp2);
                    }
                    checkedHolds.AddRange(note.References());
                }
                
                UndoRedoManager.InvokeAndPush(holdOp);
                if (holdOp2 != null) UndoRedoManager.InvokeAndPush(holdOp2);
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
            NoteType newType = properties && !note.IsHold && CurrentNoteType is not (NoteType.HoldStart or NoteType.HoldStartRNote or NoteType.HoldSegment or NoteType.HoldEnd) ? CurrentNoteType : note.NoteType;
            MaskDirection newDirection = properties ? CurrentMaskDirection : note.MaskDirection;
            
            Note newNote = new(note)
            {
                Position = newPosition,
                Size = int.Max(newSize, Note.MinSize(newType)),
                NoteType = newType,
                MaskDirection = newDirection
            };

            operationList.Add(new EditNote(note, newNote));
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
            Note newNote = new(note)
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
            Note newNote = new(note)
            {
                Position = note.Position,
                Size = int.Clamp(note.Size + delta, Note.MinSize(note.NoteType), 60)
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
            Note newNote = new(note)
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
        }

        if (operationList.Count == 0) return;
        
        foreach (IOperation op in operationList)
        {
            UndoRedoManager.Undo();
        }
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList));
        Chart.IsSaved = false;
        return;

        void addOperation(Note note)
        {
            Note newNote = new(note)
            {
                BeatData = new(float.Clamp(note.BeatData.MeasureDecimal + divisor, 0, endOfChartMeasureDecimal))
            };
            
            if (newNote.PrevReferencedNote != null && newNote.BeatData.FullTick <= newNote.PrevReferencedNote.BeatData.FullTick) return;
            if (newNote.NextReferencedNote != null && newNote.BeatData.FullTick >= newNote.NextReferencedNote.BeatData.FullTick) return;

            EditNote edit = new(note, newNote);
            
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
            Note newNote = new(note)
            {
                Position = MathExtensions.Modulo(axis - note.Size - note.Position, 60),
                NoteType = note.NoteType switch
                {
                    NoteType.SlideClockwise => NoteType.SlideCounterclockwise,
                    NoteType.SlideClockwiseBonus => NoteType.SlideCounterclockwiseBonus,
                    NoteType.SlideClockwiseRNote => NoteType.SlideCounterclockwiseRNote,
                    NoteType.SlideCounterclockwise => NoteType.SlideClockwise,
                    NoteType.SlideCounterclockwiseBonus => NoteType.SlideClockwiseBonus,
                    NoteType.SlideCounterclockwiseRNote => NoteType.SlideClockwiseRNote,
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

    public void BakeHold(MathExtensions.HoldEaseType easeType)
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
            if (note.NoteType is not (NoteType.HoldStart or NoteType.HoldStartRNote or NoteType.HoldSegment) || note.NextReferencedNote is null) return;

            BakeHold bakeHold = interpolate(note, note.NextReferencedNote);
            if (bakeHold.Segments.Count != 0)
            {
                operationList.Add(bakeHold);
            }
        }
        
        BakeHold interpolate(Note start, Note end)
        {
            int startPos0 = start.Position;
            int startPos1 = start.Position + start.Size;
            int endPos0 = end.Position;
            int endPos1 = end.Position + end.Size;

            int distance0 = int.Abs(endPos0 - startPos0);
            int distance1 = int.Abs(endPos1 - startPos1);
            
            if (distance0 > 30 && distance1 > 30)
            {
                distance0 = 60 - distance0;
                distance1 = 60 - distance1;   
            }
            
            int maxDistance = int.Max(distance0, distance1);
            float interval = 1.0f / maxDistance;

            Note lastNote = start;
            List<Note> segments = [];

            bool lerpShort = int.Abs(start.Position - end.Position) > 30;

            for (float i = interval; i < 1; i += interval)
            {
                float scaled = MathExtensions.HoldBakeEase(i, easeType);
                BeatData data = new(MathExtensions.Lerp(start.BeatData.MeasureDecimal, end.BeatData.MeasureDecimal, scaled));
                
                // avoid decimal/floating point errors that would
                // otherwise cause two segments on the same beat
                // if i is just *barely* less than endNote.Measure
                if (data.FullTick == end.BeatData.FullTick) break;
                
                // skip if FullTick is the same as previous note.
                if (data.FullTick == lastNote.BeatData.FullTick) continue;
                
                int newPos0 = (int)Math.Round(MathExtensions.ShortLerp(lerpShort, startPos0, endPos0, i));
                int newPos1 = (int)Math.Round(MathExtensions.ShortLerp(lerpShort, startPos1, endPos1, i));

                bool forceRender = data.FullTick - lastNote.BeatData.FullTick <= 30 && start.Size == end.Size;
                
                Note newNote = new()
                {
                    BeatData = data,
                    NoteType = NoteType.HoldSegment,
                    Position = MathExtensions.Modulo(newPos0, 60),
                    Size = MathExtensions.Modulo(newPos1 - newPos0, 60),
                    RenderSegment = easeType != MathExtensions.HoldEaseType.Linear || forceRender,
                    PrevReferencedNote = lastNote,
                    NextReferencedNote = end
                };

                lastNote.NextReferencedNote = newNote;
                end.PrevReferencedNote = newNote;

                lastNote = newNote;
                segments.Add(newNote);
            }

            return new(Chart, SelectedNotes, segments, start, end);
        }
    }

    public void InsertHoldSegment()
    {
        if (HighlightedElement is not Note highlighted) return;
        if (highlighted.NoteType is not (NoteType.HoldSegment or NoteType.HoldEnd)) return;
        if (highlighted.PrevReferencedNote is null) return;

        BeatData data = new(CurrentMeasureDecimal);
        
        // Current timestamp not between selected and last note
        if ( data.FullTick < highlighted.PrevReferencedNote.BeatData.FullTick 
             || data.FullTick > highlighted.BeatData.FullTick) return;

        Note note = new()
        {
            BeatData = data,
            GimmickType = GimmickType.None,
            MaskDirection = CurrentMaskDirection,
            NoteType = NoteType.HoldSegment,
            Position = Cursor.Position,
            Size = Cursor.Size
        };
        
        UndoRedoManager.InvokeAndPush(new InsertHoldSegment(Chart, SelectedNotes, note, highlighted));
    }
    
    public void EditHold()
    {
        if (HighlightedElement is null or Gimmick) return;
        if (((Note)HighlightedElement).NoteType is not NoteType.HoldEnd) return;
        StartHold((Note)HighlightedElement);
    }
    
    public void StartHold(Note lastPlacedHold)
    {
        EditorState = ChartEditorState.InsertHold;
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        CurrentNoteType = NoteType.HoldEnd;
        
        LastPlacedHold = lastPlacedHold;
        CurrentHoldStart = lastPlacedHold.FirstReference();
    }
    
    public void EndHold(bool setNoteType)
    {
        if (EditorState is not ChartEditorState.InsertHold) return;
        
        EditorState = ChartEditorState.InsertNote;
        mainView.SetHoldContextButton(EditorState);
        mainView.ToggleInsertButton();
        if (setNoteType) CurrentNoteType = CurrentBonusType is BonusType.None ? NoteType.HoldStart : NoteType.HoldStartRNote;

        if (LastPlacedHold?.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
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
            Note newNote = new(note)
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
            Note newNote = new(note)
            {
                Position = MathExtensions.Modulo(axis - note.Size - note.Position, 60),
                NoteType = note.NoteType switch
                {
                    NoteType.SlideClockwise => NoteType.SlideCounterclockwise,
                    NoteType.SlideClockwiseBonus => NoteType.SlideCounterclockwiseBonus,
                    NoteType.SlideClockwiseRNote => NoteType.SlideCounterclockwiseRNote,
                    NoteType.SlideCounterclockwise => NoteType.SlideClockwise,
                    NoteType.SlideCounterclockwiseBonus => NoteType.SlideClockwiseBonus,
                    NoteType.SlideCounterclockwiseRNote => NoteType.SlideClockwiseRNote,
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
}