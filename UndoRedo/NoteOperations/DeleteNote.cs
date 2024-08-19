using System;
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

public class DeleteHoldNote(Chart chart, List<Note> selected, Note deletedNote, BonusType bonusType) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note DeletedNote { get; } = deletedNote;
    public Note? NextNote => DeletedNote.NextReferencedNote;
    public Note? PrevNote => DeletedNote.PrevReferencedNote;
    public List<Note> Selected { get; } = selected;
    public BonusType BonusType { get; } = bonusType;

    public void Undo()
    {
        // Add note back.
        lock (Chart)
        {
            Chart.Notes.Add(DeletedNote);
        }
        
        // Set neighbor references to point back at re-added note.
        // Re-added note maintained it's original references from when it was deleted.
        if (NextNote != null) NextNote.PrevReferencedNote = DeletedNote;
        if (PrevNote != null) PrevNote.NextReferencedNote = DeletedNote;
        
        // Safety Check that's relevant to Multi-Charting only:
        // Make sure the re-added note's neighbors are still in the Note list. If they are not, unlink them.
        // This persists throughout the rest of the code, so further checks if any of them are null will reflect this change.
        if (NextNote != null && !Chart.Notes.Contains(NextNote)) DeletedNote.NextReferencedNote = null;
        if (PrevNote != null && !Chart.Notes.Contains(PrevNote)) DeletedNote.PrevReferencedNote = null;
        
        // Repair Hold Types by brute force.
        foreach (Note reference in DeletedNote.References())
        {
            if (reference is { PrevReferencedNote: null, NextReferencedNote: null })
            {
                reference.NoteType = NoteType.HoldStart;
                reference.BonusType = BonusType;
            }

            if (reference is { PrevReferencedNote: not null, NextReferencedNote: null })
            {
                reference.NoteType = NoteType.HoldEnd;
            }

            if (reference is { PrevReferencedNote: null, NextReferencedNote: not null })
            {
                reference.NoteType = NoteType.HoldStart;
                reference.BonusType = BonusType;
            }

            if (reference is { PrevReferencedNote: not null, NextReferencedNote: not null })
            {
                reference.NoteType = NoteType.HoldSegment;
            }
        }
    }

    public void Redo()
    {
        Console.WriteLine(DeletedNote.FirstReference()?.NoteType);
        
        // Make references "pass through" deleted note, effectively unlinking it.
        // Deleted note itself keeps its original references!
        if (NextNote != null) NextNote.PrevReferencedNote = DeletedNote.PrevReferencedNote;
        if (PrevNote != null) PrevNote.NextReferencedNote = DeletedNote.NextReferencedNote;
        
        // Three cases.
        // One: Deleted note is a Segment. Do nothing.
        
        // Two: Deleted note is a HoldStart. Convert Next to HoldStart.
        if (NextNote is { PrevReferencedNote: null })
        {
            NextNote.NoteType = NoteType.HoldStart;
            NextNote.BonusType = BonusType;
        }
        
        // Three: Deleted note is a HoldEnd. Convert Prev to HoldEnd.
        if (PrevNote is { NextReferencedNote: null }) PrevNote.NoteType = NoteType.HoldEnd;
        
        // Remove note.
        lock (Chart)
        {
            Chart.Notes.Remove(DeletedNote);
            Selected.Remove(DeletedNote);
        }
    }
}