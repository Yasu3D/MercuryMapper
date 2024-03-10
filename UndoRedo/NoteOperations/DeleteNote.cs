using System;
using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class DeleteNote(Chart chart, List<Note> selected, Note note) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public List<Note> Selected { get; } = selected;
    public string Description => "Delete Note";
    
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

public class DeleteHoldNote(Chart chart, List<Note> selected, Note note) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public Note? NextNote { get; } = note.NextReferencedNote;
    public Note? PrevNote { get; } = note.PrevReferencedNote;
    public NoteType NextNoteType { get; } = note.NextReferencedNote?.NoteType ?? NoteType.Touch;
    public NoteType PrevNoteType { get; } = note.PrevReferencedNote?.NoteType ?? NoteType.Touch;
    public List<Note> Selected { get; } = selected;
    public string Description => "Delete Hold Note";
    
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
                PrevNote.NoteType = NoteType.HoldEnd;
                break;
        }
        
        lock (Chart)
        {
            Chart.Notes.Remove(Note);
            Selected.Remove(Note);
        }
    }
}