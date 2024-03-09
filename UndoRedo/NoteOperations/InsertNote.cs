using System.Collections.Generic;
using MercuryMapper.Data;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class InsertNote(Chart chart, List<Note> selected, Note note) : IOperation
{
    public Chart Chart { get; } = chart;
    public Note Note { get; } = note;
    public List<Note> Selected { get; } = selected;
    public string Description => "Insert Note";
    
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