using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MercuryMapper.Enums;
using MercuryMapper.Utils;

namespace MercuryMapper.Data;

public class BeatData
{
    public readonly int Measure;
    public readonly int Tick;
    public int FullTick => Measure * 1920 + Tick;
    
    public readonly float MeasureDecimal;
    
    public BeatData(int measure, int tick)
    {
        // integer division, floors to 0 if tick < 1920.
        Measure = measure + tick / 1920;
        Tick = MathExtensions.Modulo(tick, 1920);
        MeasureDecimal = GetMeasureDecimal(measure, tick);
    }

    public BeatData(float measureDecimal)
    {
        Measure = (int)measureDecimal;
        Tick = (int)MathF.Round((measureDecimal - Measure) * 1920);
        MeasureDecimal = measureDecimal;
    }
    
    public BeatData(int fullTick)
    {
        Measure = fullTick / 1920;
        Tick = fullTick - Measure * 1920;
        MeasureDecimal = GetMeasureDecimal(Measure, Tick);
    }

    public BeatData(BeatData data)
    {
        // integer division, floors to 0 if tick < 1920.
        Measure = data.Measure + data.Tick / 1920;
        Tick = MathExtensions.Modulo(data.Tick, 1920);
        MeasureDecimal = GetMeasureDecimal(Measure, Tick);
    }

    public static float GetMeasureDecimal(int measure, int tick)
    {
        return measure + tick / 1920.0f;
    }
}

public class TimeScaleData
{
    public float MeasureDecimal { get; set; }
    public float ScaledMeasureDecimal { get; set; }
    public float ScaledMeasureDecimalHiSpeed { get; set; }

    public float HiSpeed { get; set; }
    public float TimeSigRatio { get; set; }
    public float BpmRatio { get; set; }

    public bool IsLast { get; set; }
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
    public Guid Guid { get; set; } = Guid.NewGuid();
}

public class Gimmick : ChartElement
{
    public float Bpm { get; set; }
    public TimeSig TimeSig { get; set; } = new(4, 4);
    public float HiSpeed { get; set; }
    public float TimeStamp { get; set; }

    public Gimmick() { }
    
    public Gimmick(BeatData beatData, GimmickType gimmickType, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = beatData;
        GimmickType = gimmickType;
    }

    public Gimmick(Gimmick gimmick, Guid? guid = null) : this(gimmick.BeatData, gimmick.GimmickType)
    {
        Guid = guid ?? Guid;
        switch (GimmickType)
        {
            case GimmickType.BpmChange: Bpm = gimmick.Bpm; break;
            case GimmickType.TimeSigChange: TimeSig = new TimeSig(gimmick.TimeSig); break;
            case GimmickType.HiSpeedChange: HiSpeed = gimmick.HiSpeed; break;
        }
    }

    public Gimmick(int measure, int tick, int objectId, string value1, string value2, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = new(measure, tick);
        GimmickType = (GimmickType)objectId;

        switch (GimmickType)
        {
            case GimmickType.BpmChange:
                Bpm = Convert.ToSingle(value1, CultureInfo.InvariantCulture);
                break;
            case GimmickType.TimeSigChange:
                TimeSig = new(Convert.ToInt32(value1, CultureInfo.InvariantCulture), Convert.ToInt32(value2, CultureInfo.InvariantCulture));
                break;
            case GimmickType.HiSpeedChange:
                HiSpeed = Convert.ToSingle(value1, CultureInfo.InvariantCulture);
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

    public bool RenderSegment { get; set; } = true;
    public MaskDirection MaskDirection { get; set; }
    public Note? NextReferencedNote { get; set; }
    public Note? PrevReferencedNote { get; set; }

    public int ParsedIndex { get; set; }

    public Note() { }

    public Note(BeatData beatData, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = beatData;
    }

    public Note(Note note, Guid? guid = null) : this(note.BeatData)
    {
        Guid = guid ?? Guid;
        Position = note.Position;
        Size = note.Size;
        NoteType = note.NoteType;
        RenderSegment = note.RenderSegment;
        MaskDirection = note.MaskDirection;

        NextReferencedNote = note.NextReferencedNote;
        PrevReferencedNote = note.PrevReferencedNote;
    }

    public Note(int measure, int tick, int noteTypeId, int noteIndex, int position, int size, bool renderSegment, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = new(measure, tick);
        GimmickType = GimmickType.None;
        NoteType = (NoteType)noteTypeId;
        Position = position;
        Size = size;
        RenderSegment = renderSegment;

        ParsedIndex = noteIndex;
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

    public static int MinSize(NoteType type)
    {
        return type switch
        {
            NoteType.None => 1,
            NoteType.Touch => 4,
            NoteType.TouchBonus => 5,
            NoteType.SnapForward => 6,
            NoteType.SnapBackward => 6,
            NoteType.SlideClockwise => 5,
            NoteType.SlideClockwiseBonus => 7,
            NoteType.SlideCounterclockwise => 5,
            NoteType.SlideCounterclockwiseBonus => 7,
            NoteType.HoldStart => 2,
            NoteType.HoldSegment => 1,
            NoteType.HoldEnd => 1,
            NoteType.MaskAdd => 1,
            NoteType.MaskRemove => 1,
            NoteType.EndOfChart => 60,
            NoteType.Chain => 4,
            NoteType.TouchRNote => 6,
            NoteType.SnapForwardRNote => 8,
            NoteType.SnapBackwardRNote => 8,
            NoteType.SlideClockwiseRNote => 10,
            NoteType.SlideCounterclockwiseRNote => 10,
            NoteType.HoldStartRNote => 8,
            NoteType.ChainRNote => 10,
            _ => 5
        };
    }

    public IEnumerable<Note> References()
    {
        List<Note> refs = [this];
        if (!IsHold) return refs;

        Note? prev = PrevReferencedNote;
        Note? next = NextReferencedNote;

        while (prev is not null)
        {
            refs.Add(prev);
            prev = prev.PrevReferencedNote;
        }

        while (next is not null)
        {
            refs.Add(next);
            next = next.NextReferencedNote;
        }

        return refs.OrderBy(x => x.BeatData.FullTick);
    }

    public Note? FirstReference()
    {
        if (!IsHold) return null;
        
        Note? first = this;
        Note? prev = PrevReferencedNote;
        
        while (prev is not null)
        {
            first = prev;
            prev = prev.PrevReferencedNote;
        }

        return first;
    }

    public string ToNetworkString()
    {
        string result = $"{Guid} {BeatData.Measure:F0} {BeatData.Tick:F0} {(int)NoteType:F0} {Position:F0} {Size:F0} {(RenderSegment ? 1 : 0)}";
        if (IsMask) result += $" {(int)MaskDirection:F0}";
        if (NextReferencedNote != null) result += $" {NextReferencedNote.Guid}";
        
        return result;
    }
    
    public static Note ParseNetworkString(Chart chart, string[] data)
    {
        Note note = new()
        {
            Guid = Guid.Parse(data[0]),
            BeatData = new(Convert.ToInt32(data[1]), Convert.ToInt32(data[2])),
            NoteType = (NoteType)Convert.ToInt32(data[3]),
            Position = Convert.ToInt32(data[4]),
            Size = Convert.ToInt32(data[5]),
            RenderSegment = data[6] != "0",
        };

        if (data.Length == 8)
        {
            if (note.IsMask) note.MaskDirection = (MaskDirection)Convert.ToInt32(data[7]);
            else note.NextReferencedNote = chart.FindNoteByGuid(data[7]);
        }

        return note;
    }
}

public struct Hold()
{
    public List<Note> Segments = [];
}