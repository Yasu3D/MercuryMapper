using System.Collections.Generic;
using System.Linq;
using MercuryMapper.Data;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class BakeHold(Chart chart, List<Note> selected, List<Note> segments, Note start, Note end) : IOperation
{
    public readonly Chart Chart = chart;
    public List<Note> Segments { get; } = segments;
    public List<Note> Selected { get; } = selected;
    public Note Start { get; } = start;
    public Note End { get; } = end;
    
    public void Undo()
    {
        lock (Chart)
        {
            foreach (Note note in Segments)
            {
                Chart.Notes.Remove(note);
                Selected.Remove(note);
            }
        }

        Start.NextReferencedNote = End;
        End.PrevReferencedNote = Start;
    }
    
    public void Redo()
    {
        lock (Chart)
        {
            foreach (Note note in Segments)
            {
                Chart.Notes.Add(note);
            }
            
            Start.NextReferencedNote = Segments[0];
            Segments.Last().NextReferencedNote = End;
            End.PrevReferencedNote = Segments.Last();
        }
    }
}