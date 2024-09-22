using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class StitchHold(Chart chart, Note first, Note second, NoteType secondNoteType, BonusType secondBonusType) : IOperation
{
    public readonly Chart Chart = chart;
    public readonly Note First = first;
    public readonly Note Second = second;
    public readonly NoteType SecondNoteType = secondNoteType;
    public readonly BonusType SecondBonusType = secondBonusType;

    public void Undo()
    {
        lock (Chart)
        {
            First.NoteType = NoteType.HoldEnd;
            Second.NoteType = SecondNoteType;
            Second.BonusType = SecondBonusType;

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
            Second.BonusType = BonusType.None;

            First.NextReferencedNote = Second;
            Second.PrevReferencedNote = First;
        }
    }
}