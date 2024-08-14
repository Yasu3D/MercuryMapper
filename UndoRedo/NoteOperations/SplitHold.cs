using MercuryMapper.Data;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class SplitHold(Chart chart, Note segment, Note newStart, Note newEnd) : IOperation
{
    public readonly Chart Chart = chart;
    public readonly Note Segment = segment;
    
    public readonly Note NewStart = newStart;
    public readonly Note NewEnd = newEnd;

    public void Undo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(NewEnd);
            Chart.Notes.Remove(NewStart);
            Chart.Notes.Add(Segment);
            
            if (Segment.PrevReferencedNote != null) Segment.PrevReferencedNote.NextReferencedNote = Segment;
            if (Segment.NextReferencedNote != null) Segment.NextReferencedNote.PrevReferencedNote = Segment;
        }
    }
 
    public void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(Segment);
            Chart.Notes.Add(NewEnd);
            Chart.Notes.Add(NewStart);

            if (Segment.PrevReferencedNote != null) Segment.PrevReferencedNote.NextReferencedNote = NewEnd;
            if (Segment.NextReferencedNote != null) Segment.NextReferencedNote.PrevReferencedNote = NewStart;
        }
    }
}