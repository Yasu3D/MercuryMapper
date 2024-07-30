using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class SplitHold(Chart chart, Note segment) : IOperation
{
    public readonly Chart Chart = chart;
    public readonly Note Hold = segment;

    private readonly Note newEnd = new(segment);
    private readonly Note newStart = new(segment);
    
    private readonly Note? prevSegment = segment.PrevReferencedNote;
    private readonly Note? nextSegment = segment.NextReferencedNote;

    public void Undo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(newEnd);
            Chart.Notes.Remove(newStart);
            Chart.Notes.Add(Hold);
            
            if (prevSegment != null) prevSegment.NextReferencedNote = Hold;
            if (nextSegment != null) nextSegment.PrevReferencedNote = Hold;
        }
    }
 
    public void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(Hold);

            newEnd.PrevReferencedNote = prevSegment;
            newEnd.NextReferencedNote = null;
            newEnd.NoteType = NoteType.HoldEnd;
            Chart.Notes.Add(newEnd);

            newStart.PrevReferencedNote = null;
            newStart.NextReferencedNote = nextSegment;
            newStart.NoteType = NoteType.HoldStart;
            Chart.Notes.Add(newStart);

            if (prevSegment != null) prevSegment.NextReferencedNote = newEnd;
            if (nextSegment != null) nextSegment.PrevReferencedNote = newStart;
        }
    }
}