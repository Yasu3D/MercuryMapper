using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class StitchHold(Chart chart, Note first, Note second) : IOperation
{
    public readonly Chart Chart = chart;
    public readonly Note First = first;
    public readonly Note Second = second;

    public void Undo()
    {
        lock (Chart)
        {
            First.NextReferencedNote = null;
            Second.PrevReferencedNote = null;
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            First.NextReferencedNote = Second;
            Second.PrevReferencedNote = First;
        }
    }
}