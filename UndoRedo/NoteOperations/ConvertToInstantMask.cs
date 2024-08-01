using System;
using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class ConvertToInstantMask(Chart chart, Note oldMask, List<Note> newMasks) : IOperation
{
    public readonly Chart Chart = chart;
    public readonly Note OldMask = oldMask;
    public readonly List<Note> NewMasks = newMasks;

    public void Undo()
    {
        lock (Chart)
        {
            foreach (Note mask in NewMasks)
            {
                Chart.Notes.Remove(mask);
            }

            Chart.Notes.Add(OldMask);
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(OldMask);
            
            foreach (Note mask in NewMasks)
            {
                Chart.Notes.Add(mask);
            }
        }
    }
}