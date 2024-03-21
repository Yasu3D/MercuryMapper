using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Editor;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class DeleteNote(Chart chart, List<Note> selected, Note note) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public List<Note> Selected { get; } = selected;
    
    public void Undo()
    {
        lock (Chart)
        {
            Chart.Notes.Add(Note);
        }
    }
    
    public void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(Note);
            Selected.Remove(Note);
        }
    }
}

public class DeleteHoldNote(Chart chart, ChartEditor editor, List<Note> selected, Note note, Note? newLastPlacedHold, bool endHold) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public Note? NextNote { get; } = note.NextReferencedNote;
    public Note? PrevNote { get; } = note.PrevReferencedNote;
    public Note? NewLastPlacedHold { get; } = newLastPlacedHold;
    public NoteType NextNoteType { get; } = note.NextReferencedNote?.NoteType ?? NoteType.Touch;
    public NoteType PrevNoteType { get; } = note.PrevReferencedNote?.NoteType ?? NoteType.Touch;
    public List<Note> Selected { get; } = selected;
    
    public void Undo()
    {
        switch (Note.NoteType)
        {
            case NoteType.HoldStart:
            case NoteType.HoldStartRNote:
                if (NextNote == null) break;
                NextNote.PrevReferencedNote = Note;
                NextNote.NoteType = NextNoteType;
                break;
            
            case NoteType.HoldSegment:
                if (PrevNote != null) PrevNote.NextReferencedNote = Note;
                if (NextNote != null) NextNote.PrevReferencedNote = Note;
                break;
            
            case NoteType.HoldEnd:
                if (PrevNote == null) break;
                PrevNote.NextReferencedNote = Note;
                PrevNote.NoteType = PrevNoteType;
                break;
        }
        
        lock (Chart)
        {
            Chart.Notes.Add(Note);
        }

        editor.LastPlacedHold = Note;
        if (endHold) editor.StartHold();
    }
    
    public void Redo()
    {
        switch (Note.NoteType)
        {
            case NoteType.HoldStart:
            case NoteType.HoldStartRNote:
                if (NextNote == null) break;
                NextNote.PrevReferencedNote = null;
                if (NextNote.NoteType == NoteType.HoldSegment) NextNote.NoteType = Note.NoteType;
                break;
            
            case NoteType.HoldSegment:
                if (PrevNote != null) PrevNote.NextReferencedNote = NextNote;
                if (NextNote != null) NextNote.PrevReferencedNote = PrevNote;
                break;
            
            case NoteType.HoldEnd:
                if (PrevNote == null) break;
                PrevNote.NextReferencedNote = null;
                PrevNote.NoteType = PrevNote.IsSegment ? NoteType.HoldEnd : PrevNote.NoteType;
                break;
        }
        
        lock (Chart)
        {
            Chart.Notes.Remove(Note);
            Selected.Remove(Note);
        }

        editor.LastPlacedHold = NewLastPlacedHold;
        if (endHold) editor.EndHold();
    }
}