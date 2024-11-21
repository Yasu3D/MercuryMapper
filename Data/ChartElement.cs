using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Shapes;
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
    public Guid Guid { get; set; } = Guid.NewGuid();
}

public class Gimmick : ChartElement
{
    public float Bpm { get; set; }
    public TimeSig TimeSig { get; set; } = new(4, 4);
    public float HiSpeed { get; set; }
    public float TimeStamp { get; set; }
    public GimmickType GimmickType { get; set; } = GimmickType.None;

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

    public Gimmick(int measure, int tick, GimmickType gimmickType, string value1, string value2, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = new(measure, tick);
        GimmickType = gimmickType;

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

    public string ToNetworkString()
    {
        string result = $"{Guid} {BeatData.Measure:F0} {BeatData.Tick:F0} {(int)GimmickType:F0}";
        
        result += GimmickType switch
        {
            GimmickType.BpmChange => $" {Bpm.ToString("F6", CultureInfo.InvariantCulture)}\n",
            GimmickType.HiSpeedChange => $" {HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}\n",
            GimmickType.TimeSigChange => $" {TimeSig.Upper:F0} {TimeSig.Lower:F0}\n",
            _ => "\n",
        };
        
        return result;
    }
    
    public static Gimmick ParseNetworkString(string[] data)
    {
        Gimmick gimmick = new()
        {
            Guid = Guid.Parse(data[0]),
            BeatData = new(Convert.ToInt32(data[1]), Convert.ToInt32(data[2])),
            GimmickType = (GimmickType)Convert.ToInt32(data[3]),
        };

        if (gimmick.GimmickType == GimmickType.BpmChange && data.Length == 5) gimmick.Bpm = Convert.ToSingle(data[4]);
        if (gimmick.GimmickType == GimmickType.HiSpeedChange && data.Length == 5) gimmick.HiSpeed = Convert.ToSingle(data[4]);
        if (gimmick.GimmickType == GimmickType.TimeSigChange && data.Length == 6) gimmick.TimeSig = new(Convert.ToInt32(data[4]), Convert.ToInt32(data[5]));

        return gimmick;
    }
}

public class Note : ChartElement
{
    public NoteType NoteType { get; set; } = NoteType.Touch;
    public BonusType BonusType { get; set; }
    public int Position { get; set; }
    public int Size { get; set; }

    public bool RenderSegment { get; set; } = true;
    public MaskDirection MaskDirection { get; set; }
    public Note? NextReferencedNote { get; set; }
    public Note? PrevReferencedNote { get; set; }
    public TraceColor Color { get; set; } = TraceColor.White;

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
        BonusType = note.BonusType;
        RenderSegment = note.RenderSegment;
        MaskDirection = note.MaskDirection;
        Color = note.Color;

        NextReferencedNote = note.NextReferencedNote;
        PrevReferencedNote = note.PrevReferencedNote;
    }

    public Note(int measure, int tick, NoteType noteType, BonusType bonusType, int noteIndex, int position, int size, bool renderSegment, TraceColor color, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = new(measure, tick);
        NoteType = noteType;
        BonusType = bonusType;
        Position = position;
        Size = size;
        RenderSegment = renderSegment;
        Color = color;

        ParsedIndex = noteIndex;
    }

    public NoteLinkType LinkType => this switch
    {
        { PrevReferencedNote:     null, NextReferencedNote:     null } => NoteLinkType.Unlinked,
        { PrevReferencedNote:     null, NextReferencedNote: not null } => NoteLinkType.Start,
        { PrevReferencedNote: not null, NextReferencedNote: not null } => NoteLinkType.Point,
        { PrevReferencedNote: not null, NextReferencedNote:     null } => NoteLinkType.End,
    };

    public bool IsSegment => LinkType
        is NoteLinkType.Point
        or NoteLinkType.End;
    
    public bool IsSlide => NoteType 
        is NoteType.SlideClockwise 
        or NoteType.SlideCounterclockwise;

    public bool IsSnap => NoteType 
        is NoteType.SnapForward 
        or NoteType.SnapBackward;

    public bool IsBonus => BonusType is BonusType.Bonus;

    public bool IsRNote => BonusType is BonusType.RNote;

    public bool IsMask => NoteType
        is NoteType.MaskAdd
        or NoteType.MaskRemove;

    public bool IsNote => NoteType
        is NoteType.Touch
        or NoteType.Chain
        or NoteType.SlideClockwise
        or NoteType.SlideCounterclockwise
        or NoteType.SnapForward
        or NoteType.SnapBackward
        or NoteType.Hold
        or NoteType.Damage;
    
    public bool IsNoteCollection => NoteType
        is NoteType.Hold
        or NoteType.Trace;
    
    public static int MinSize(NoteType noteType, BonusType bonusType, NoteLinkType linkType)
    {
        return (noteType, bonusType, linkType) switch
        {
            (NoteType.Touch, BonusType.None, _) => 4,
            (NoteType.Touch, BonusType.Bonus, _) => 5,
            (NoteType.Touch, BonusType.RNote, _) => 6,
            
            (NoteType.SnapForward, BonusType.None, _) => 6,
            (NoteType.SnapBackward, BonusType.None, _) => 6,
            
            (NoteType.SnapForward, BonusType.Bonus, _) => 6,
            (NoteType.SnapBackward, BonusType.Bonus, _) => 6,
            
            (NoteType.SnapForward, BonusType.RNote, _) => 8,
            (NoteType.SnapBackward, BonusType.RNote, _) => 8,
            
            (NoteType.SlideClockwise, BonusType.None, _) => 5,
            (NoteType.SlideCounterclockwise, BonusType.None, _) => 5,
            
            (NoteType.SlideClockwise, BonusType.Bonus, _) => 7,
            (NoteType.SlideCounterclockwise, BonusType.Bonus, _) => 7,
            
            (NoteType.SlideClockwise, BonusType.RNote, _) => 10,
            (NoteType.SlideCounterclockwise, BonusType.RNote, _) => 10,
            
            (NoteType.Chain, BonusType.None, _) => 4,
            (NoteType.Chain, BonusType.Bonus, _) => 4,
            (NoteType.Chain, BonusType.RNote, _) => 10,
            
            (NoteType.Hold, BonusType.None, NoteLinkType.Start) => 2,
            (NoteType.Hold, BonusType.Bonus, NoteLinkType.Start) => 2,
            (NoteType.Hold, BonusType.RNote, NoteLinkType.Start) => 8,
            (NoteType.Hold, _, NoteLinkType.Point) => 1,
            (NoteType.Hold, _, NoteLinkType.End) => 1,
            (NoteType.Hold, _, _) => 2,
            
            (NoteType.MaskAdd, _, _) => 1,
            (NoteType.MaskRemove, _, _) => 1,
            
            (NoteType.Damage, _, _) => 3,
            (NoteType.Trace, _, _) => 2,
            
            _ => 5,
        };
    }

    public static int MaxSize(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.Trace => 2,
            _ => 60,
        };
    }
    
    public IEnumerable<Note> References()
    {
        List<Note> refs = [this];
        if (NoteType is not (NoteType.Hold or NoteType.Trace)) return refs;

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
        if (NoteType is not (NoteType.Hold or NoteType.Trace)) return null;
        
        Note first = this;
        Note? prev = PrevReferencedNote;
        
        while (prev is not null)
        {
            first = prev;
            prev = prev.PrevReferencedNote;
        }

        return first;
    }

    public Note? PrevVisibleReference(bool skip = false)
    {
        if (NoteType is not (NoteType.Hold or NoteType.Trace)) return null;
        if (skip) return PrevReferencedNote;
        
        Note? prev = PrevReferencedNote;

        while (prev != null)
        {
            if (prev.RenderSegment) return prev;
            prev = prev.PrevReferencedNote;
        }

        return null;
    }
    
    public Note? NextVisibleReference(bool skip = false)
    {
        if (NoteType is not (NoteType.Hold or NoteType.Trace)) return null;
        if (skip) return NextReferencedNote;
        
        Note? next = NextReferencedNote;

        while (next != null)
        {
            if (next.RenderSegment) return next;
            next = next.NextReferencedNote;
        }

        return null;
    }
    
    public string ToNetworkString()
    {
        string result = $"{Guid} {BeatData.Measure:F0} {BeatData.Tick:F0} {(int)NoteType:F0} {(int)BonusType:F0} {Position:F0} {Size:F0} {(RenderSegment ? 1 : 0)}";
        if (IsMask)
        {
            result += $" {(int)MaskDirection:F0}";
        }
        
        if (NoteType is NoteType.Hold or NoteType.Trace)
        {
            result += $" {(NextReferencedNote != null ? NextReferencedNote.Guid : "null")}";
            result += $" {(PrevReferencedNote != null ? PrevReferencedNote.Guid : "null")}";
        }

        if (NoteType is NoteType.Trace)
        {
            result += $" {(int)Color}";
        }
        
        return result;
    }
    
    public static Note ParseNetworkString(Chart chart, string[] data)
    {
        Note note = new()
        {
            Guid = Guid.Parse(data[0]),
            BeatData = new(Convert.ToInt32(data[1]), Convert.ToInt32(data[2])),
            NoteType = (NoteType)Convert.ToInt32(data[3]),
            BonusType = (BonusType)Convert.ToInt32(data[4]),
            Position = Convert.ToInt32(data[5]),
            Size = Convert.ToInt32(data[6]),
            RenderSegment = data[7] != "0",
        };

        if (note.IsMask && data.Length == 9) note.MaskDirection = (MaskDirection)Convert.ToInt32(data[8]);

        if (data.Length >= 10)
        {
            if (data[8] != "null") note.NextReferencedNote = chart.FindNoteByGuid(data[8]);
            if (data[9] != "null") note.PrevReferencedNote = chart.FindNoteByGuid(data[9]);
        }

        if (data.Length >= 11)
        {
            note.Color = (TraceColor)Convert.ToInt32(data[10]);
        }

        return note;
    }

    public int NoteToMerId()
    {
        return (NoteType, BonusType, LinkType) switch
        {
            (NoteType.Touch, BonusType.None, _) => 1,
            (NoteType.Touch, BonusType.Bonus, _) => 2,
            (NoteType.Touch, BonusType.RNote, _) => 20,

            (NoteType.SnapForward, BonusType.None, _) => 3,
            (NoteType.SnapForward, BonusType.Bonus, _) => 3,
            (NoteType.SnapForward, BonusType.RNote, _) => 21,

            (NoteType.SnapBackward, BonusType.None, _) => 4,
            (NoteType.SnapBackward, BonusType.Bonus, _) => 4,
            (NoteType.SnapBackward, BonusType.RNote, _) => 22,

            (NoteType.SlideClockwise, BonusType.None, _) => 5,
            (NoteType.SlideClockwise, BonusType.Bonus, _) => 6,
            (NoteType.SlideClockwise, BonusType.RNote, _) => 23,

            (NoteType.SlideCounterclockwise, BonusType.None, _) => 7,
            (NoteType.SlideCounterclockwise, BonusType.Bonus, _) => 8,
            (NoteType.SlideCounterclockwise, BonusType.RNote, _) => 24,

            (NoteType.Hold, BonusType.None, NoteLinkType.Start) => 9,
            (NoteType.Hold, BonusType.Bonus, NoteLinkType.Start) => 9,
            (NoteType.Hold, BonusType.RNote, NoteLinkType.Start) => 25,

            (NoteType.Hold, _, NoteLinkType.Point) => 10,
            (NoteType.Hold, _, NoteLinkType.End) => 11,

            (NoteType.MaskAdd, _, _) => 12,
            (NoteType.MaskRemove, _, _) => 13,

            (NoteType.Chain, BonusType.None, _) => 16,
            (NoteType.Chain, BonusType.Bonus, _) => 16,
            (NoteType.Chain, BonusType.RNote, _) => 26,
            _ => 1,
        };
    }
    
    public static NoteType NoteTypeFromMerId(int id)
    {
        return id switch
        {
            1 or 2 or 20 => NoteType.Touch,
            3 or 21 => NoteType.SnapForward,
            4 or 22 => NoteType.SnapBackward,
            5 or 6 or 23 => NoteType.SlideClockwise,
            7 or 8 or 24 => NoteType.SlideCounterclockwise,
            9 or 25 or 10 or 11 => NoteType.Hold,
            12 => NoteType.MaskAdd,
            13 => NoteType.MaskRemove,
            16 or 26 => NoteType.Chain,
            _ => throw new ArgumentOutOfRangeException($"Invalid Note ID {id}"),
        };
    }

    public static BonusType BonusTypeFromMerId(int id)
    {
        return id switch
        {
            1 or 3 or 4 or 5 or 7 or 9 or 10 or 11 or 12 or 13 or 14 or 16 => BonusType.None,
            2 or 6 or 8 => BonusType.Bonus,
            20 or 21 or 22 or 23 or 24 or 25 or 26 => BonusType.RNote,
            _ => throw new ArgumentOutOfRangeException($"Invalid Note ID {id}"),
        };
    }
}

public struct NoteCollection()
{
    public List<Note> Notes = [];
}

public class Comment(Guid guid, BeatData beatData, string text, Rectangle marker)
{
    public Guid Guid = guid;
    public BeatData BeatData = beatData;
    public string Text = text;
    public Rectangle Marker = marker;
}