using System;
using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class InsertHoldNote(Chart chart, List<Note> selected, Note note, Note lastPlacedNote) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public Note LastPlacedNote { get; } = lastPlacedNote;
    public List<Note> Selected { get; } = selected;
    public string Description => "Insert Note";
    
    public void Undo()
    {
        if (LastPlacedNote.NoteType is NoteType.HoldSegment)
        {
            LastPlacedNote.NoteType = NoteType.HoldEnd;
        }

        LastPlacedNote.NextReferencedNote = null;
        Note.PrevReferencedNote = null;
        
        lock (Chart)
        {
            Chart.Notes.Remove(Note);
            Selected.Remove(Note);
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Add(Note);
        }

        Note.PrevReferencedNote = LastPlacedNote;
        LastPlacedNote.NextReferencedNote = Note;
        
        if (LastPlacedNote.NoteType is NoteType.HoldEnd)
        {
            LastPlacedNote.NoteType = NoteType.HoldSegment;
        }
    }
}