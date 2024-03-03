using System;
using MercuryMapper.Enums;

namespace MercuryMapper.Data;

public class BeatData
{
    public readonly int Measure;
    public readonly int Tick;
    public int FullTick => Measure * 1920 + Tick;
    
    public readonly float MeasureDecimal;
    

    public BeatData(int measure, int tick)
    {
        Measure = measure;
        
        while (tick >= 1920)
        {
            Measure++;
            tick -= 1920;
        }
        
        Tick = tick;
        MeasureDecimal = GetMeasureDecimal(measure, tick);
    }

    public BeatData(float measureDecimal)
    {
        Measure = (int)measureDecimal;
        Tick = (int)MathF.Round((measureDecimal - Measure) * 1920);
        MeasureDecimal = measureDecimal;
    }

    public BeatData(BeatData data)
    {
        Measure = data.Measure;

        int beat = data.Tick;
        while (beat >= 1920)
        {
            Measure++;
            beat -= 1920;
        }
        
        Tick = beat;
        MeasureDecimal = GetMeasureDecimal(Measure, Tick);
    }

    public static float GetMeasureDecimal(int measure, int beat)
    {
        return measure + beat / 1920f;
    }
}

public class TimeScaleData
{
    public float UnscaledMeasureDecimal { get; set; }
    public float ScaledMeasureDecimal { get; set; }
    public float ScaledMeasureDecimalHiSpeed { get; set; }
    public float GimmickMeasureDecimal { get; set; }

    public float HiSpeed { get; set; }
    public float TimeSigRatio { get; set; }
    public float BpmRatio { get; set; }
}

public class TimeSig(int upper, int lower)
{
    public readonly int Upper = upper;
    public readonly int Lower = lower;
    public float Ratio => Upper / (float)Lower;

    public TimeSig(TimeSig timeSig) : this(timeSig.Upper, timeSig.Lower) { }
}

public class ChartElement
{
    public BeatData BeatData { get; set; } = new(-1, 0);
    public GimmickType GimmickType { get; set; } = GimmickType.None;
}

public class Gimmick : ChartElement
{
    public float Bpm { get; set; }
    public TimeSig TimeSig { get; set; } = new(4, 4);
    public float HiSpeed { get; set; }
    public float TimeStamp { get; set; }

    public Gimmick() { }
    
    public Gimmick(BeatData beatData, GimmickType gimmickType)
    {
        BeatData = beatData;
        GimmickType = gimmickType;
    }

    public Gimmick(Gimmick gimmick) : this(gimmick.BeatData, gimmick.GimmickType)
    {
        switch (GimmickType)
        {
            case GimmickType.BpmChange: Bpm = gimmick.Bpm; break;
            case GimmickType.TimeSigChange: TimeSig = new TimeSig(gimmick.TimeSig); break;
            case GimmickType.HiSpeedChange: HiSpeed = gimmick.HiSpeed; break;
        }
    }

    public Gimmick(int measure, int tick, int objectId, string value1, string value2)
    {
        BeatData = new(measure, tick);
        GimmickType = (GimmickType)objectId;

        switch (GimmickType)
        {
            case GimmickType.BpmChange:
                Bpm = Convert.ToSingle(value1);
                break;
            case GimmickType.TimeSigChange:
                TimeSig = new(Convert.ToInt32(value1), Convert.ToInt32(value2));
                break;
            case GimmickType.HiSpeedChange:
                HiSpeed = Convert.ToSingle(value1);
                break;
        }
    }

    public bool IsReverse => GimmickType is GimmickType.ReverseEffectStart or GimmickType.ReverseEffectEnd or GimmickType.ReverseNoteEnd;
    public bool IsStop => GimmickType is GimmickType.StopStart or GimmickType.StopEnd;
}

public class Note : ChartElement
{
    public NoteType NoteType { get; set; } = NoteType.Touch;
    public int Position { get; set; }
    public int Size { get; set; }
    
    public bool RenderSegment { get; set; }
    public MaskDirection MaskDirection { get; set; }
    public Note? NextReferencedNote { get; set; }
    public Note? PrevReferencedNote { get; set; }

    public Note() { }

    public Note(BeatData beatData)
    {
        BeatData = beatData;
    }

    public Note(Note note) : this(note.BeatData)
    {
        Position = note.Position;
        Size = note.Size;
        NoteType = note.NoteType;
        RenderSegment = note.RenderSegment;
        MaskDirection = note.MaskDirection;

        NextReferencedNote = note.NextReferencedNote;
        PrevReferencedNote = note.PrevReferencedNote;
    }

    public Note(int measure, int tick, int noteTypeId, int position, int size, bool renderSegment)
    {
        BeatData = new(measure, tick);
        GimmickType = GimmickType.None;
        NoteType = (NoteType)noteTypeId;
        Position = position;
        Size = size;
        RenderSegment = renderSegment;
    }
    
    public bool IsHold => NoteType
        is NoteType.HoldStart 
        or NoteType.HoldStartRNote 
        or NoteType.HoldSegment 
        or NoteType.HoldEnd;

    public bool IsSegment => NoteType
        is NoteType.HoldSegment
        or NoteType.HoldEnd;

    public bool IsChain => NoteType
        is NoteType.Chain
        or NoteType.ChainRNote;
    
    public bool IsSlide => NoteType 
        is NoteType.SlideClockwise 
        or NoteType.SlideCounterclockwise
        or NoteType.SlideClockwiseBonus 
        or NoteType.SlideCounterclockwiseBonus
        or NoteType.SlideClockwiseRNote 
        or NoteType.SlideCounterclockwiseRNote;

    public bool IsSnap => NoteType 
        is NoteType.SnapForward 
        or NoteType.SnapBackward 
        or NoteType.SnapForwardRNote
        or NoteType.SnapBackwardRNote;

    public bool IsBonus => NoteType
        is NoteType.TouchBonus
        or NoteType.SlideClockwiseBonus
        or NoteType.SlideCounterclockwiseBonus;

    public bool IsRNote => NoteType
        is NoteType.TouchRNote
        or NoteType.SnapForwardRNote
        or NoteType.SnapBackwardRNote
        or NoteType.SlideClockwiseRNote
        or NoteType.SlideCounterclockwiseRNote
        or NoteType.HoldStartRNote
        or NoteType.ChainRNote;

    public bool IsMask => NoteType
        is NoteType.MaskAdd
        or NoteType.MaskRemove;
}