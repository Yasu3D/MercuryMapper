using System;
using System.Collections.Generic;
using System.Linq;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.UndoRedo;
using MercuryMapper.UndoRedo.NoteOperations;
using MercuryMapper.Utils;
using MercuryMapper.Views;

namespace MercuryMapper.Editor;

// TODO: FIX EVERYTHING! UNDO/REDO FUCKING SUCKS!


public class ChartEditor
{
    public ChartEditor(MainView main)
    {
        mainView = main;
        UndoRedoManager.OperationHistoryChanged += (_, _) =>
        {
            Chart.Notes = Chart.Notes.OrderBy(x => x.BeatData.FullTick).ToList();
            Chart.Gimmicks = Chart.Gimmicks.OrderBy(x => x.BeatData.FullTick).ToList();
        };
    }
    
    private readonly MainView mainView;
    
    public readonly Cursor Cursor = new();
    public readonly UndoRedoManager UndoRedoManager = new();
    public Chart Chart { get; private set; } = new();
    
    public ChartEditorState EditorState { get; private set; }
    
    public float CurrentMeasure { get; set; }
    public NoteType CurrentNoteType { get; set; } = NoteType.Touch;
    public BonusType CurrentBonusType { get; set; } = BonusType.None;
    public MaskDirection CurrentMaskDirection { get; set; } = MaskDirection.Clockwise;

    public List<Note> SelectedNotes { get; private set; } = [];
    public Note? LastSelectedNote;
    public Note? HighlightedNote;

    public Note? LastPlacedHold;
    public Note? CurrentHoldStart;
    
    public void NewChart(string musicFilePath, string author, float bpm, int timeSigUpper, int timeSigLower)
    {
        LastSelectedNote = null;
        LastPlacedHold = null;
        EndHold();
        UndoRedoManager.Clear();
        
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

    public void LoadChart(string path)
    {
        LastSelectedNote = null;
        LastPlacedHold = null;
        EndHold();
        UndoRedoManager.Clear();
        
        Chart.LoadFile(path);
    }
    
    public void UpdateCursorNoteType()
    {
        // Reset Editor State
        EndHold();
        mainView.SetHoldContextButton(EditorState);
        mainView.UpdateControls();
        
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

        // Update LastPlacedHold
        if (operation is InsertHoldNote insertHoldOperation)
        {
            LastPlacedHold = insertHoldOperation.LastPlacedNote;
            if (EditorState is not ChartEditorState.InsertHold) StartHold();
        }

        if (operation is InsertNote insertNoteOperation)
        {
            // Update CurrentHoldStart + End Hold
            if (insertNoteOperation.Note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote && EditorState is ChartEditorState.InsertHold)
            {
                CurrentHoldStart = null;
                EndHold();
            }
        }

        if (operation is DeleteHoldNote)
        {
            foreach (Note note in Chart.Notes)
            {
                if (note is not { IsHold: true, NextReferencedNote: null, PrevReferencedNote: null } || !UndoRedoManager.CanUndo) continue; 
                
                IOperation op = UndoRedoManager.PeekUndo;
                if (op is InsertHoldNote) return;
                UndoRedoManager.Undo();
                return;
            }
        }
        
        mainView.UpdateControls();
    }

    public void Redo()
    { 
        if (!UndoRedoManager.CanRedo) return;
        IOperation operation = UndoRedoManager.Redo();
        
        // End hold automatically when hitting Redo.
        EndHold();
        
        // Update LastPlacedHold
        if (operation is InsertHoldNote insertHoldOperation)
        {
            LastPlacedHold = insertHoldOperation.Note;
        }
        
        if (operation is InsertNote insertNoteOperation)
        {
            // Update CurrentHoldStart + Start Hold
            if (insertNoteOperation.Note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                CurrentHoldStart = insertNoteOperation.Note;
                StartHold();
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
        
        mainView.UpdateControls();
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
            if (!SelectedNotes.Remove(note))
            {
                SelectedNotes.Add(note);
            }
        }
    }

    public void SelectAllNotes()
    {
        lock (SelectedNotes)
        {
            SelectedNotes.AddRange(Chart.Notes);
        }
    }
    
    public void DeselectAllNotes()
    {
        lock (SelectedNotes)
        {
            SelectedNotes.Clear();
        }
    }
    
    // ________________ Highlighting
    public void HighlightNote(Note note)
    {
        HighlightedNote = HighlightedNote == note ? null : note;
    }

    public void HighlightNextNote()
    {
        if (HighlightedNote == null) return;
        
        int index = Chart.Notes.IndexOf(HighlightedNote);
        HighlightedNote = Chart.Notes[MathExtensions.Modulo(index + 1, Chart.Notes.Count)];
    }

    public void HighlightPrevNote()
    {
        if (HighlightedNote == null) return;
        
        int index = Chart.Notes.IndexOf(HighlightedNote);
        HighlightedNote = Chart.Notes[MathExtensions.Modulo(index - 1, Chart.Notes.Count)];
    }

    public void SelectHighlightedNote()
    {
        if (HighlightedNote == null) return;
        SelectNote(HighlightedNote);
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

            lock (Chart)
            {
                Chart.Notes.Add(note);
            }

            UndoRedoManager.Push(new InsertHoldNote(Chart, SelectedNotes, note, LastPlacedHold));
            Chart.IsSaved = false;
            LastPlacedHold = note;
            mainView.UpdateControls();
            
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

            LastPlacedHold = note;
            Chart.IsSaved = false;
            UndoRedoManager.Push(new InsertNote(Chart, SelectedNotes, note));

            if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                StartHold();
                CurrentHoldStart = note;
            }
            
            mainView.UpdateControls();
            
            return;
        }

        if (EditorState is ChartEditorState.InsertGimmick)
        {
            
        }
    }

    public void DeleteSelection()
    {
        List<IOperation> operationList = [];
        
        if (EditorState is ChartEditorState.InsertHold && CurrentHoldStart != null && SelectedNotes.Contains(CurrentHoldStart))
        {
            EndHold();
        }
        
        foreach (Note note in SelectedNotes)
        {
            if (note.IsHold)
            {
                operationList.Add(new DeleteHoldNote(Chart, SelectedNotes, note));
            }
            else
            {
                operationList.Add(new DeleteNote(Chart, SelectedNotes, note));
            }
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList[0].Description, operationList));
        Chart.IsSaved = false;
    }
    
    // TODO: Fix code repetition maybe?
    public void EditSelectionShape()
    {
        List<IOperation> operationList = [];
        foreach (Note note in SelectedNotes)
        {
            Note newNote = new(note)
            {
                Position = Cursor.Position,
                Size = Cursor.Size
            };

            operationList.Add(new EditNoteShape(note, newNote));
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList[0].Description, operationList));
        Chart.IsSaved = false;
    }
    
    public void EditSelectionProperties()
    {
        // This should NEVER be done on a hold note.
        // Maybe im just gonna ditch this entirely.
        if (CurrentNoteType is NoteType.HoldStart or NoteType.HoldStartRNote or NoteType.HoldSegment or NoteType.HoldEnd) return;
        
        List<IOperation> operationList = [];
        foreach (Note note in SelectedNotes)
        {
            if (note.IsHold) continue;
            
            Note newNote = new(note)
            {
                NoteType = CurrentNoteType,
                MaskDirection = CurrentMaskDirection
            };

            operationList.Add(new EditNoteProperties(note, newNote));
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList[0].Description, operationList));
        Chart.IsSaved = false;
    }
    
    public void EditSelectionFull()
    {
        // This should NEVER be done on a hold note.
        // Maybe im just gonna ditch this entirely.
        if (CurrentNoteType is NoteType.HoldStart or NoteType.HoldStartRNote or NoteType.HoldSegment or NoteType.HoldEnd) return;

        List<IOperation> operationList = [];
        foreach (Note note in SelectedNotes)
        {
            if (note.IsHold) continue;
            
            Note newNote = new(note)
            {
                NoteType = CurrentNoteType,
                MaskDirection = CurrentMaskDirection,
                Position = Cursor.Position,
                Size = Cursor.Size
            };

            operationList.Add(new EditNoteFull(note, newNote));
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList[0].Description, operationList));
        Chart.IsSaved = false;
    }

    public void MirrorSelection(int axis = 30)
    {
        List<IOperation> operationList = [];
        foreach (Note note in SelectedNotes)
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
                }
            };

            operationList.Add(new MirrorNote(note, newNote));
        }

        if (operationList.Count == 0) return;
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList[0].Description, operationList));
        Chart.IsSaved = false;
    }

    public void BakeHold()
    {
        List<IOperation> operationList = [];
        foreach (Note note in SelectedNotes)
        {
            if (note.NoteType is not (NoteType.HoldStart or NoteType.HoldStartRNote or NoteType.HoldSegment) || note.NextReferencedNote is null) continue;

            BakeHold bakeHold = interpolate(note, note.NextReferencedNote, note.NextReferencedNote.BeatData.MeasureDecimal - note.BeatData.MeasureDecimal);
            if (bakeHold.Segments.Count != 0)
            {
                operationList.Add(bakeHold);
            }
        }

        if (operationList.Count == 0) return;
        
        UndoRedoManager.InvokeAndPush(new CompositeOperation(operationList[0].Description, operationList));
        Chart.IsSaved = false;
        return;
        
        BakeHold interpolate(Note start, Note end, float length)
        {
            int startPos0 = start.Position;
            int startPos1 = start.Position + start.Size;
            int endPos0 = end.Position;
            int endPos1 = end.Position + end.Size;

            int distance0 = int.Abs(endPos0 - startPos0);
            int distance1 = int.Abs(endPos1 - startPos1);
            distance0 = distance0 > 30 ? 60 - distance0 : distance0;
            distance1 = distance1 > 30 ? 60 - distance1 : distance1;

            int maxDistance = int.Max(distance0, distance1);
            float interval = 1 / (1 / length * maxDistance);

            var lastNote = start;
            List<Note> segments = [];

            bool lerpShort = int.Abs(start.Position - end.Position) > 30;

            for (float i = start.BeatData.MeasureDecimal + interval; i < end.BeatData.MeasureDecimal; i += interval)
            {
                // avoid decimal/floating point errors that would
                // otherwise cause two segments on the same beat
                // if i is just *barely* less than endNote.Measure
                BeatData iData = new(i);
                if (iData.FullTick == end.BeatData.FullTick) break;

                float time = (i - start.BeatData.MeasureDecimal) / (end.BeatData.MeasureDecimal - start.BeatData.MeasureDecimal);
                int newPos0 = (int)Math.Round(MathExtensions.ShortLerp(lerpShort, startPos0, endPos0, time));
                int newPos1 = (int)Math.Round(MathExtensions.ShortLerp(lerpShort, startPos1, endPos1, time));

                var newNote = new Note()
                {
                    BeatData = iData,
                    NoteType = NoteType.HoldSegment,
                    Position = MathExtensions.Modulo(newPos0, 60),
                    Size = MathExtensions.Modulo(newPos1 - newPos0, 60),
                    RenderSegment = true,
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
    
    public void StartHold()
    {
        EditorState = ChartEditorState.InsertHold;
        mainView.SetHoldContextButton(EditorState);
        CurrentNoteType = NoteType.HoldEnd;
    }
    
    public void EndHold()
    {
        if (EditorState is not ChartEditorState.InsertHold) return;
        
        EditorState = ChartEditorState.InsertNote;
        mainView.SetHoldContextButton(EditorState);
        CurrentNoteType = NoteType.HoldStart;

        if (LastPlacedHold?.NoteType is not NoteType.HoldStart) return;
        
        lock (Chart)
        {
            Chart.Notes.Remove(LastPlacedHold);
        }
    }
    
    public void EditHold()
    {
        if (HighlightedNote?.NoteType is not NoteType.HoldEnd) return;
        StartHold();
        LastPlacedHold = HighlightedNote;
    }
}