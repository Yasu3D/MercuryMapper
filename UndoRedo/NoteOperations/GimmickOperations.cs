using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class InsertGimmick(Chart chart, Gimmick gimmick) : IOperation
{
    public Chart Chart { get; } = chart;
    public Gimmick Gimmick { get; } = gimmick;
    
    public void Undo()
    {
        lock (Chart)
        {
            Chart.Gimmicks.Remove(Gimmick);
            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            Chart.Gimmicks.Add(Gimmick);
            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
        }
    }
}

public class EditGimmick(Chart chart, Gimmick gimmick, Gimmick newGimmick) : IOperation
{
    public Chart Chart { get; } = chart;
    public Gimmick BaseGimmick { get; } = gimmick;
    public Gimmick OldGimmick { get; } = new(gimmick);
    public Gimmick NewGimmick { get; } = newGimmick;
    
    public void Undo()
    {
        lock (Chart)
        {
            BaseGimmick.Bpm = OldGimmick.Bpm;
            BaseGimmick.TimeSig = OldGimmick.TimeSig;
            BaseGimmick.HiSpeed = OldGimmick.HiSpeed;
            
            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            BaseGimmick.Bpm = NewGimmick.Bpm;
            BaseGimmick.TimeSig = NewGimmick.TimeSig;
            BaseGimmick.HiSpeed = NewGimmick.HiSpeed;
            
            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
        }
    }
}

public class DeleteGimmick(Chart chart, Gimmick gimmick) : IOperation
{
    public Chart Chart { get; } = chart;
    public Gimmick Gimmick { get; } = gimmick;
    
    public void Undo()
    {
        lock (Chart)
        {
            Chart.Gimmicks.Add(Gimmick);
            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
        }
    }

    public void Redo()
    {
        lock (Chart)
        {
            Chart.Gimmicks.Remove(Gimmick);
            Chart.GenerateTimeEvents();
            Chart.GenerateTimeScales();
        }
    }
}