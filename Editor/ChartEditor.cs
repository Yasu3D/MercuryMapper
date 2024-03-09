using System;
using System.Collections.Generic;
using System.Linq;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.UndoRedo;
using MercuryMapper.UndoRedo.NoteOperations;
using MercuryMapper.Views;

namespace MercuryMapper.Editor;

public class ChartEditor
{
    public ChartEditor(MainView main)
    {
        mainView = main;
        UndoRedoManager.OperationHistoryChanged += (sender, e) =>
        {
            Chart.Notes = Chart.Notes.OrderBy(x => x.BeatData.FullTick).ToList();
            Chart.Gimmicks = Chart.Gimmicks.OrderBy(x => x.BeatData.FullTick).ToList();
        };
    }
    
    private MainView mainView;
    
    public readonly Cursor Cursor = new();
    public readonly UndoRedoManager UndoRedoManager = new();
    public Chart Chart { get; private set; } = new();
    
    public ChartEditorState EditorState { get; private set; }
    
    public float CurrentMeasure { get; set; }
    public NoteType CurrentNoteType { get; set; } = NoteType.Touch;
    public BonusType CurrentBonusType { get; set; } = BonusType.None;
    public MaskDirection CurrentMaskDirection { get; set; } = MaskDirection.Clockwise;

    public List<Note> SelectedNotes { get; private set; } = [];
    public Note? LastSelectedNote = null;
    public Note? LastPlacedHoldNote = null;

    public void NewChart(string musicFilePath, string author, float bpm, int timeSigUpper, int timeSigLower)
    {
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
    }
    
    public void UpdateCursorNoteType()
    {
        // Reset Editor State
        EndHold();
        mainView.SetHoldContextButton(EditorState);
        
        // TODO: delete hold start if you switch to another notetype
        
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
    }

    // ________________ Edit Menu
    public void Undo()
    {
        if (!UndoRedoManager.CanUndo) return;
        IOperation operation = UndoRedoManager.Undo();

        // TODO: figure out a way to roll back last placed hold note.
        
        // When undoing this:
        // [HoldStart] + [HoldEnd] - undo placing the hold end
        // Automatically undo the [HoldStart] as well.
        //if (operation is InsertHoldNote { LastPlacedNote.NoteType: NoteType.HoldStart })
        //{
        //    if (UndoRedoManager.CanUndo) UndoRedoManager.Undo();
        //}
    }

    public void Redo()
    {
        if (!UndoRedoManager.CanRedo) return;
        IOperation operation = UndoRedoManager.Redo();
        
        // When redoing this:
        // [HoldStart] + [HoldEnd] - redo placing the hold start
        // Automatically redo the [HoldEnd] as well.
        //if (operation is InsertNote { Note.NoteType: NoteType.HoldStart })
        //{
        //    if (UndoRedoManager.CanRedo) UndoRedoManager.Redo();
        //}
    }
    
    public void Cut()
    {
        
    }
    
    public void Copy()
    {
        
    }
    
    public void Paste()
    {
        
    }
    
    // ________________ Selections
    public void SelectNote(Note note)
    {
        lock (SelectedNotes)
        {
            SelectedNotes.Add(note);
        }
    }

    public void DeselectNote(Note note)
    {
        lock (SelectedNotes)
        {
            SelectedNotes.Remove(note);
        }
    }

    public void DeselectAllNotes()
    {
        lock (SelectedNotes)
        {
            SelectedNotes.Clear();
        }
    }
    
    // ________________ Note Operations
    public void InsertNote()
    {
        BeatData data = new(CurrentMeasure);

        if (EditorState is ChartEditorState.InsertHold)
        {
            // Place Hold End
            // Hold end's prevReferencedNote is LastPlacedNote
            // LastPlacedNote's nextReferencedNote is Hold End
            // If previous note is hold end, convert it to hold segment

            if (LastPlacedHoldNote is null) return;
            
            Note note = new()
            {
                BeatData = data,
                GimmickType = GimmickType.None,
                MaskDirection = CurrentMaskDirection,
                NoteType = NoteType.HoldEnd,
                Position = Cursor.Position,
                Size = Cursor.Size,
                PrevReferencedNote = LastPlacedHoldNote
            };

            LastPlacedHoldNote.NextReferencedNote = note;
            if (LastPlacedHoldNote.NoteType is NoteType.HoldEnd)
            {
                LastPlacedHoldNote.NoteType = NoteType.HoldSegment;
            }

            lock (Chart)
            {
                Chart.Notes.Add(note);
            }

            UndoRedoManager.Push(new InsertHoldNote(Chart, SelectedNotes, note, LastPlacedHoldNote));
            Chart.IsSaved = false;
            LastPlacedHoldNote = note;
            
            return;
        }
        
        if (EditorState is ChartEditorState.InsertNote)
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
            
            lock (Chart)
            {
                Chart.Notes.Add(note);
            }

            LastPlacedHoldNote = note;
            Chart.IsSaved = false;
            UndoRedoManager.Push(new InsertNote(Chart, SelectedNotes, note));

            if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                EditorState = ChartEditorState.InsertHold;
                mainView.SetHoldContextButton(EditorState);
            }
            
            return;
        }

        if (EditorState is ChartEditorState.InsertGimmick)
        {
            return;
        }
    }

    public void DeleteNote()
    {
        
    }

    public void EndHold()
    {
        EditorState = ChartEditorState.InsertNote;
        mainView.SetHoldContextButton(EditorState);
        
        if (LastPlacedHoldNote?.NoteType is NoteType.HoldStart)
        {
            lock (Chart)
            {
                Chart.Notes.Remove(LastPlacedHoldNote);
            }
        }
    }
    
    public void EditHold() { }
    
    public void EditSelection() { }
    
    public void MirrorSelection() { }
}