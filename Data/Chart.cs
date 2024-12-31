using System;
using System.Collections.Generic;
using System.Linq;
using MercuryMapper.Editor;
using MercuryMapper.Enums;
using MercuryMapper.Utils;

namespace MercuryMapper.Data;

public class Chart(ChartEditor editor)
{
    public readonly ChartEditor ChartEditor = editor;

    public string Guid { get; set; }

    public bool IsSaved { get; set; } = true;
    public bool IsNew { get; set; } = true;

    public List<Note> Notes { get; set; } = [];
    public List<Gimmick> Gimmicks { get; set; } = [];

    public Dictionary<string, Comment> Comments { get; set; } = [];

    public Gimmick? StartBpm { get; set; }
    public Gimmick? StartTimeSig { get; set; }
    
    public Gimmick? EndOfChart => Gimmicks.LastOrDefault(x => x.GimmickType is GimmickType.EndOfChart);

    private List<Gimmick> MetreEvents { get; set; } = [];
    private List<TimeScaleData>[] TimeScales { get; } = [[], [], [], [], [], [], [], [], [], []]; // 10 Lists

    public string Filepath { get; set; } = "";
    
    public string Version = "";
    public string Title = "";
    public string Rubi = "";
    public string Artist = "";
    public string Author = "";
    public string BpmText = "";

    public int Background;
    
    public int Diff;
    public decimal Level;
    public decimal ClearThreshold;

    public decimal PreviewStart;
    public decimal PreviewTime;

    public string BgmFilepath = "";
    public decimal BgmOffset;
    public string BgaFilepath = "";
    public decimal BgaOffset;
    public string JacketFilepath = "";
    
    public void Clear()
    {
        ChartEditor.ClearCommentMarkers();
        
        Notes.Clear();
        Gimmicks.Clear();
        MetreEvents.Clear();

        foreach (List<TimeScaleData> t in TimeScales)
        {
            t.Clear();
        }

        Filepath = "";
        
        Version = "";
        Title = "";
        Rubi = "";
        Artist = "";
        Author = "";

        Diff = 0;
        Level = 0;
        ClearThreshold = 0.83m;
        BpmText = "";

        PreviewStart = 0;
        PreviewTime = 10;

        BgmFilepath = "";
        BgmOffset = 0;
        BgaFilepath = "";
        BgaOffset = 0;
        JacketFilepath = "";
    }
    
    public void RepairNotes()
    {
        foreach (Note note in Notes)
        {
            if (note.Position is < 0 or >= 60)
                note.Position = MathExtensions.Modulo(note.Position, 60);

            note.Size = int.Clamp(note.Size, 1, 60);

            if (note.IsMask && note.MaskDirection == MaskDirection.None)
            {
                note.MaskDirection = MaskDirection.Center;
            }
        }
    }
    
    /// <summary>
    /// Generates "Metre Events" [= Bpm/TimeSig changes merged into one list]
    /// </summary>
    public void GenerateMetreEvents()
    {
        Gimmicks = Gimmicks.OrderBy(x => x.BeatData.FullTick).ToList();
        Gimmick? timeSigChange = Gimmicks.FirstOrDefault(x => x is { GimmickType: GimmickType.TimeSigChange, BeatData.FullTick: 0 });
        Gimmick? bpmChange = Gimmicks.FirstOrDefault(x => x is { GimmickType: GimmickType.BpmChange, BeatData.FullTick: 0 });
        if (timeSigChange == null || bpmChange == null) return;
        
        float lastBpm = bpmChange.Bpm;
        TimeSig lastTimeSig = timeSigChange.TimeSig;

        MetreEvents.Clear();
        
        foreach (Gimmick gimmick in Gimmicks)
        {
            if (gimmick.GimmickType is not (GimmickType.BpmChange or GimmickType.TimeSigChange)) continue;
            
            if (gimmick.GimmickType is GimmickType.BpmChange)
            {
                gimmick.TimeSig = new(lastTimeSig);
                lastBpm = gimmick.Bpm;
            }

            if (gimmick.GimmickType is GimmickType.TimeSigChange)
            {
                gimmick.Bpm = lastBpm;
                lastTimeSig = gimmick.TimeSig;
            }
            
            MetreEvents.Add(gimmick);
        }

        MetreEvents[0].TimeStamp = 0;
        for (int i = 1; i < MetreEvents.Count; i++)
        {
            Gimmick current = MetreEvents[i];
            Gimmick previous = MetreEvents[i - 1];

            float timeDifference = current.BeatData.MeasureDecimal - previous.BeatData.MeasureDecimal;
            MetreEvents[i].TimeStamp = timeDifference * (4 * previous.TimeSig.Ratio) * (60000.0f / previous.Bpm) + previous.TimeStamp;
        }
    }

    /// <summary>
    /// Generates TimeScaleData objects for each Scroll Layer scaled by MetreEvents and HiSpeedChanges.
    /// </summary>
    public void GenerateTimeScales()
    {
        // Always look for the last Gimmick at Time 0 in case there's multiple stacked on top of each other.
        float startBpm = Gimmicks.Last(x => x.GimmickType is GimmickType.BpmChange && x.BeatData.FullTick == 0).Bpm;
        float startTimeSigRatio = Gimmicks.Last(x => x.GimmickType is GimmickType.TimeSigChange && x.BeatData.FullTick == 0).TimeSig.Ratio;
        
        for (int i = 0; i < 10; i++)
        {
            TimeScales[i].Clear();

            TimeScaleData last = new()
            {
                RawBeatData = new(0, 0),
                UnscaledBeatData = new(0, 0),
                ScaledBeatData = new(0, 0),
                MetreMultiplier = 1,
                SpeedMultiplier = 1,
                IsLast = true,
            };

            TimeScales[i].Add(last);

            foreach (Gimmick gimmick in Gimmicks)
            {
                if ((int)gimmick.ScrollLayer != i) continue;
                if (!gimmick.IsTimeScale) continue;

                BeatData rawBeatData = new(gimmick.BeatData);
                
                float delta = rawBeatData.MeasureDecimal - last.RawBeatData.MeasureDecimal;
                float unscaledMeasureDecimal = last.UnscaledBeatData.MeasureDecimal + delta * last.MetreMultiplier;
                float scaledMeasureDecimal = last.ScaledBeatData.MeasureDecimal + delta * last.MetreMultiplier * last.SpeedMultiplier;

                BeatData unscaledBeatData = new(unscaledMeasureDecimal);
                BeatData scaledBeatData = new(scaledMeasureDecimal);

                Gimmick metreEvent = MetreEvents.Last(x => x.BeatData.FullTick <= rawBeatData.FullTick);
                float metreMultiplier = (startBpm / metreEvent.Bpm) * (metreEvent.TimeSig.Ratio / startTimeSigRatio);
                float speedMultiplier = gimmick.GimmickType switch
                {
                    GimmickType.HiSpeedChange => gimmick.HiSpeed,
                    GimmickType.StopStart => 0,
                    GimmickType.StopEnd => Gimmicks.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.HiSpeedChange)?.HiSpeed ?? 1,
                    _ => last.SpeedMultiplier,
                };
                
                TimeScaleData data = new()
                {
                    RawBeatData = rawBeatData,
                    UnscaledBeatData = unscaledBeatData,
                    ScaledBeatData = scaledBeatData,
                    MetreMultiplier = metreMultiplier,
                    SpeedMultiplier = speedMultiplier,
                };

                TimeScales[i].Add(data);

                last.IsLast = false;
                data.IsLast = true;
                
                last = data;
            }
        }
        
        /*
        // Take Last at 0 instead of First because apparently there's
        // charts with multiple Bpm/TimeSig changes at Measure 0.
        float startBpm = Gimmicks.Last(x => x.GimmickType is GimmickType.BpmChange && x.BeatData.FullTick == 0).Bpm;
        float startTimeSigRatio = Gimmicks.Last(x => x.GimmickType is GimmickType.TimeSigChange && x.BeatData.FullTick == 0).TimeSig.Ratio;

        TimeScales.Clear();
        TimeScaleData lastData = new()
        {
            MeasureDecimal = 0,
            ScaledMeasureDecimal = 0,
            ScaledMeasureDecimalHiSpeed = 0,
            HiSpeed = 1,
            TimeSigRatio = 1,
            BpmRatio = 1,
        };

        for (int i = 0; i < Gimmicks.Count; i++)
        {
            Gimmick gimmick = Gimmicks[i];
            
            TimeScaleData data = calculateTimeScaleData(gimmick);
            lastData = data;
            
            data.IsLast = i == Gimmicks.Count - 1;
            
            TimeScales.Add(data);
        }

        TimeScales.Add(new()
        {
            MeasureDecimal = float.PositiveInfinity,
            ScaledMeasureDecimal = float.PositiveInfinity,
            ScaledMeasureDecimalHiSpeed = float.PositiveInfinity,
            HiSpeed = lastData.HiSpeed,
            TimeSigRatio = lastData.TimeSigRatio,
            BpmRatio = lastData.BpmRatio,
        });
        
        return;
        
        // Creates data to quickly calculate scaled MeasureDecimal timestamps at a given measure.
        TimeScaleData calculateTimeScaleData(Gimmick gimmick)
        {
            float? newBpm = null;
            float? newTimeSig = null;
            float? newHiSpeed = null;
            
            switch (gimmick.GimmickType)
            {
                case GimmickType.TimeSigChange:
                    newTimeSig = gimmick.TimeSig.Ratio / startTimeSigRatio;
                    break;
                case GimmickType.HiSpeedChange:
                    newHiSpeed = gimmick.HiSpeed;
                    break;
                case GimmickType.StopStart:
                    newHiSpeed = 0.0001f;
                    break;
                case GimmickType.StopEnd:
                    newHiSpeed = Gimmicks.LastOrDefault(x => x.BeatData.FullTick < gimmick.BeatData.FullTick && x.GimmickType is GimmickType.HiSpeedChange)?.HiSpeed ?? 1;
                    break;
                case GimmickType.BpmChange:
                    newBpm = startBpm / gimmick.Bpm;
                    break;
            }
            
            return new()
            {
                MeasureDecimal = gimmick.BeatData.MeasureDecimal,
                ScaledMeasureDecimal = lastData.ScaledMeasureDecimal + float.Abs(gimmick.BeatData.MeasureDecimal - lastData.MeasureDecimal) * (lastData.BpmRatio * lastData.TimeSigRatio),
                ScaledMeasureDecimalHiSpeed = lastData.ScaledMeasureDecimalHiSpeed + float.Abs(gimmick.BeatData.MeasureDecimal - lastData.MeasureDecimal) * (lastData.BpmRatio * lastData.TimeSigRatio * lastData.HiSpeed),
                BpmRatio = newBpm ?? lastData.BpmRatio,
                TimeSigRatio = newTimeSig ?? lastData.TimeSigRatio,
                HiSpeed = newHiSpeed ?? lastData.HiSpeed,
            };
        }*/
    }
    
    /// <summary>
    /// Finds nearest previous TimeScaleData via binary search and calculates ScaledMeasureDecimal off of it.
    /// </summary>
    public float GetScaledMeasureDecimal(float measureDecimal, bool showHiSpeed, ScrollLayer scrollLayer)
    {
        TimeScaleData? timeScaleData = BinarySearchTimeScales(measureDecimal, scrollLayer);
        if (timeScaleData == null) return measureDecimal;

        float delta = measureDecimal - timeScaleData.RawBeatData.MeasureDecimal;

        return showHiSpeed
            ? timeScaleData.ScaledBeatData.MeasureDecimal + delta * timeScaleData.MetreMultiplier * timeScaleData.SpeedMultiplier
            : timeScaleData.UnscaledBeatData.MeasureDecimal + delta * timeScaleData.MetreMultiplier;
    }

    /// <summary>
    /// Finds nearest previous TimeScaleData via binary search and calculates the unscaled MeasureDecimal off of it.
    /// This is the inverse of GetScaledMeasureDecimal.
    /// </summary>
    public float GetUnscaledMeasureDecimal(float scaledMeasureDecimal, bool showHiSpeed, ScrollLayer scrollLayer)
    {
        TimeScaleData? timeScaleData = BinarySearchTimeScalesScaled(scaledMeasureDecimal, showHiSpeed, scrollLayer);
        if (timeScaleData == null) return scaledMeasureDecimal;

        if (showHiSpeed)
        {
            float scaledDelta = scaledMeasureDecimal - timeScaleData.ScaledBeatData.MeasureDecimal;
            float delta = scaledDelta / (timeScaleData.MetreMultiplier * timeScaleData.SpeedMultiplier);

            return timeScaleData.UnscaledBeatData.MeasureDecimal + delta;
        }
        else
        {
            float unscaledDelta = scaledMeasureDecimal - timeScaleData.UnscaledBeatData.MeasureDecimal;
            float delta = unscaledDelta / timeScaleData.MetreMultiplier;

            return timeScaleData.UnscaledBeatData.MeasureDecimal + delta;
        }
    }
    
    /// <summary>
    /// Finds nearest TimeScaleData via Binary Search.
    /// </summary>
    public TimeScaleData? BinarySearchTimeScales(float measureDecimal, ScrollLayer scrollLayer)
    {
        int l = (int)scrollLayer;
        
        int min = 0;
        int max = TimeScales[l].Count - 1;
        TimeScaleData? result = null;

        while (min <= max)
        {
            int mid = (min + max) / 2;
            if (TimeScales[l][mid].RawBeatData.MeasureDecimal > measureDecimal)
            {
                max = mid - 1;
            }
            else
            {
                result = TimeScales[l][mid];
                min = mid + 1;
            }
        }

        return result;
    }
    
    /// <summary>
    /// Same as unscaled binary search, but searches with ScaledMeasureDecimal instead.
    /// </summary>
    public TimeScaleData? BinarySearchTimeScalesScaled(float scaledMeasureDecimal, bool showHiSpeed, ScrollLayer scrollLayer)
    {
        int l = (int)scrollLayer;
        
        int min = 0;
        int max = TimeScales[l].Count - 1;
        TimeScaleData? result = null;

        while (min <= max)
        {
            int mid = (min + max) / 2;
            float sampleMeasureDecimal = showHiSpeed ? TimeScales[l][mid].ScaledBeatData.MeasureDecimal : TimeScales[l][mid].UnscaledBeatData.MeasureDecimal;
            
            if (sampleMeasureDecimal > scaledMeasureDecimal)
            {
                max = mid - 1;
            }
            else
            {
                result = TimeScales[l][mid];
                min = mid + 1;
            }
        }

        return result;
    }
    
    /// <summary>
    /// Convert a Timestamp [milliseconds] to MeasureDecimal value
    /// </summary>
    public float Timestamp2MeasureDecimal(float time)
    {
        if (MetreEvents.Count == 0) return -1;

        Gimmick metreEvent = MetreEvents.LastOrDefault(x => time >= x.TimeStamp) ?? MetreEvents[0];
        return (time - metreEvent.TimeStamp) / (60000.0f / metreEvent.Bpm * 4.0f * metreEvent.TimeSig.Ratio) + metreEvent.BeatData.MeasureDecimal;
    }

    /// <summary>
    /// Convert a Timestamp [milliseconds] to BeatData
    /// <b>WARNING! THIS LOSES A NON-NEGLIGIBLE AMOUNT OF DECIMAL PRECISION.</b>
    /// </summary>
    public BeatData Timestamp2BeatData(float time)
    {
        return new(Timestamp2MeasureDecimal(time));
    }

    /// <summary>
    /// Convert MeasureDecimal value to a Timestamp [milliseconds]
    /// </summary>
    public float MeasureDecimal2Timestamp(float measureDecimal)
    {
        if (MetreEvents == null || MetreEvents.Count == 0) return 0;
        
        Gimmick gimmick = MetreEvents.LastOrDefault(x => measureDecimal >= x.BeatData.MeasureDecimal) ?? MetreEvents[0];
        return (60000.0f / gimmick.Bpm * 4 * gimmick.TimeSig.Ratio * (measureDecimal - gimmick.BeatData.MeasureDecimal) + gimmick.TimeStamp);
    }

    /// <summary>
    /// Convert BeatData to a Timestamp [milliseconds]
    /// </summary>
    public float BeatData2Timestamp(BeatData data)
    {
        return MeasureDecimal2Timestamp(data.MeasureDecimal);
    }

    /// <summary>
    /// Returns Note with matching GUID, otherwise returns null.
    /// </summary>
    /// <returns></returns>
    public Note? FindNoteByGuid(string guid)
    {
        return Notes.LastOrDefault(x => x.Guid.ToString() == guid);
    }

    /// <summary>
    /// Returns Gimmick with matching GUID, otherwise returns null.
    /// </summary>
    /// <returns></returns>
    public Gimmick? FindGimmickByGuid(string guid)
    {
        return Gimmicks.LastOrDefault(x => x.Guid.ToString() == guid);
    }

    /// <summary>
    /// Returns all Notes that do not have valid BonusTypes for Mercury.
    /// </summary>
    public List<Note> GetNonMercuryBonusTypeNotes()
    {
        return Notes.Where(x => x.BonusType is BonusType.Bonus && x.NoteType is not (NoteType.Touch or NoteType.SlideClockwise or NoteType.SlideCounterclockwise)).ToList();
    }

    /// <summary>
    /// Converts all Notes that do not have valid BonusTypes for Mercury to BonusType None.
    /// </summary>
    public void ConvertNonMercuryBonusTypeNotes()
    {
        foreach (Note note in Notes)
        {
            if (note.BonusType is BonusType.Bonus && note.NoteType is not (NoteType.Touch or NoteType.SlideClockwise or NoteType.SlideCounterclockwise))
            {
                note.BonusType = BonusType.None;
            }
        }
    }
}