using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class InsertNote(Chart chart, List<Note> selected, Note note) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public List<Note> Selected { get; } = selected;
    
    public void Undo()
    {
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
    }
}

public class InsertHoldNote(Chart chart, List<Note> selected, Note note, Note lastPlacedNote) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public Note LastPlacedNote { get; } = lastPlacedNote;
    public List<Note> Selected { get; } = selected;
    
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

public class InsertHoldSegment(Chart chart, List<Note> selected, Note note, Note highlighted) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public Note Highlighted { get; } = highlighted;
    public Note Previous { get; } = highlighted.PrevReferencedNote!;
    public List<Note> Selected { get; } = selected;
    
    public void Undo()
    {
        lock (Chart)
        {
            Highlighted.PrevReferencedNote = Previous;
            Previous.NextReferencedNote = Highlighted;

            Note.PrevReferencedNote = null;
            Note.NextReferencedNote = null;
            
            Chart.Notes.Remove(Note);
            Selected.Remove(Note);
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Add(Note);
            
            Previous.NextReferencedNote = Note;
            Highlighted.PrevReferencedNote = Note;

            Note.PrevReferencedNote = Previous;
            Note.NextReferencedNote = Highlighted;
        }
    }
}