using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class StitchHold(Chart chart, Note first, Note second, NoteType secondType) : IOperation
{
    public readonly Chart Chart = chart;
    public readonly Note First = first;
    public readonly Note Second = second;
    public readonly NoteType SecondType = secondType;

    public void Undo()
    {
        lock (Chart)
        {
            First.NoteType = NoteType.HoldEnd;
            Second.NoteType = SecondType;

            First.NextReferencedNote = null;
            Second.PrevReferencedNote = null;
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            First.NoteType = NoteType.HoldSegment;
            Second.NoteType = NoteType.HoldSegment;

            First.NextReferencedNote = Second;
            Second.PrevReferencedNote = First;
        }
    }
}