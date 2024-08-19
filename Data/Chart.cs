using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MercuryMapper.Enums;
using MercuryMapper.Utils;

namespace MercuryMapper.Data;

public class Chart
{
    public bool IsSaved { get; set; } = true;
    public bool IsNew { get; set; } = true;

    public List<Note> Notes { get; set; } = [];
    public List<Gimmick> Gimmicks { get; set; } = [];

    public Gimmick? StartBpm { get; set; }
    public Gimmick? StartTimeSig { get; set; }
    
    public Note? EndOfChart => Notes.LastOrDefault(x => x.NoteType is NoteType.EndOfChart);
    
    private List<Gimmick>? TimeEvents { get; set; }
    public List<TimeScaleData> TimeScales { get; } = [];

    public string Filepath { get; set; } = "";
    
    public string Version = "";
    public string Title = "";
    public string Rubi = "";
    public string Artist = "";
    public string Author = "";

    public int Diff;
    public decimal Level;
    public decimal ClearThreshold;
    public string BpmText = "";

    public decimal PreviewStart;
    public decimal PreviewTime;

    public string BgmFilepath = "";
    public decimal BgmOffset;
    public string BgaFilepath = "";
    public decimal BgaOffset;
    public string JacketFilepath = "";
    
    public void Clear()
    {
        Notes.Clear();
        Gimmicks.Clear();
        TimeEvents?.Clear();
        TimeScales.Clear();

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
        }
    }
    
    /// <summary>
    /// Generates "Time Events" [= Bpm/TimeSig changes merged into one list]
    /// </summary>
    public void GenerateTimeEvents()
    {
        Gimmicks = Gimmicks.OrderBy(x => x.BeatData.MeasureDecimal).ToList();
        Gimmick? timeSigChange = Gimmicks.FirstOrDefault(x => x is { GimmickType: GimmickType.TimeSigChange, BeatData.FullTick: 0 });
        Gimmick? bpmChange = Gimmicks.FirstOrDefault(x => x is { GimmickType: GimmickType.BpmChange, BeatData.FullTick: 0 });
        if (timeSigChange == null || bpmChange == null) return;
        
        float lastBpm = bpmChange.Bpm;
        TimeSig lastTimeSig = timeSigChange.TimeSig;

        TimeEvents = [];
        
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
            
            TimeEvents.Add(gimmick);
        }

        TimeEvents[0].TimeStamp = 0;
        for (int i = 1; i < TimeEvents.Count; i++)
        {
            Gimmick current = TimeEvents[i];
            Gimmick previous = TimeEvents[i - 1];

            float timeDifference = current.BeatData.MeasureDecimal - previous.BeatData.MeasureDecimal;
            TimeEvents[i].TimeStamp = timeDifference * (4 * previous.TimeSig.Ratio) * (60000.0f / previous.Bpm) + previous.TimeStamp;
        }
    }

    /// <summary>
    /// Generates MeasureDecimals scaled by Time Events and HiSpeed.
    /// </summary>
    public void GenerateTimeScales()
    {
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
            BpmRatio = 1
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
            BpmRatio = lastData.BpmRatio
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
                HiSpeed = newHiSpeed ?? lastData.HiSpeed
            };
        }
    }
    
    /// <summary>
    /// Finds nearest previous TimeScaleData via binary search and calculates ScaledMeasureDecimal off of it.
    /// </summary>
    public float GetScaledMeasureDecimal(float measureDecimal, bool showHiSpeed)
    {
        TimeScaleData? timeScaleData = BinarySearchTimeScales(measureDecimal);
        if (timeScaleData == null) return measureDecimal;
        
        return showHiSpeed
            ? timeScaleData.ScaledMeasureDecimalHiSpeed + (measureDecimal - timeScaleData.MeasureDecimal) * timeScaleData.HiSpeed * timeScaleData.TimeSigRatio * timeScaleData.BpmRatio
            : timeScaleData.ScaledMeasureDecimal + (measureDecimal - timeScaleData.MeasureDecimal) * timeScaleData.TimeSigRatio * timeScaleData.BpmRatio;
    }

    /// <summary>
    /// Finds nearest previous TimeScaleData via binary search and calculates the unscaled MeasureDecimal off of it.
    /// This is the inverse of GetScaledMeasureDecimal.
    /// </summary>
    public float GetUnscaledMeasureDecimal(float scaledMeasureDecimal, bool showHiSpeed)
    {
        TimeScaleData? timeScaleData = BinarySearchTimeScalesScaled(scaledMeasureDecimal, showHiSpeed);
        if (timeScaleData == null) return scaledMeasureDecimal;
        
        return showHiSpeed
            ? (scaledMeasureDecimal - timeScaleData.ScaledMeasureDecimalHiSpeed + timeScaleData.MeasureDecimal * timeScaleData.HiSpeed * timeScaleData.TimeSigRatio * timeScaleData.BpmRatio) / (timeScaleData.HiSpeed * timeScaleData.TimeSigRatio * timeScaleData.BpmRatio)
            : (scaledMeasureDecimal - timeScaleData.ScaledMeasureDecimal + timeScaleData.MeasureDecimal * timeScaleData.TimeSigRatio * timeScaleData.BpmRatio) / (timeScaleData.TimeSigRatio * timeScaleData.BpmRatio);
    }
    
    /// <summary>
    /// Finds nearest TimeScaleData via Binary Search.
    /// </summary>
    public TimeScaleData? BinarySearchTimeScales(float measureDecimal)
    {
        int min = 0;
        int max = TimeScales.Count - 1;
        TimeScaleData? result = null;

        while (min <= max)
        {
            int mid = (min + max) / 2;
            if (TimeScales[mid].MeasureDecimal > measureDecimal)
            {
                max = mid - 1;
            }
            else
            {
                result = TimeScales[mid];
                min = mid + 1;
            }
        }

        return result;
    }
    
    /// <summary>
    /// Same as unscaled binary search, but searches with ScaledMeasureDecimal instead.
    /// </summary>
    public TimeScaleData? BinarySearchTimeScalesScaled(float scaledMeasureDecimal, bool showHiSpeed)
    {
        int min = 0;
        int max = TimeScales.Count - 1;
        TimeScaleData? result = null;

        while (min <= max)
        {
            int mid = (min + max) / 2;
            if ((showHiSpeed ? TimeScales[mid].ScaledMeasureDecimalHiSpeed : TimeScales[mid].ScaledMeasureDecimal) > scaledMeasureDecimal)
            {
                max = mid - 1;
            }
            else
            {
                result = TimeScales[mid];
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
        if (TimeEvents == null || TimeEvents.Count == 0) return -1;

        Gimmick timeEvent = TimeEvents.LastOrDefault(x => time >= x.TimeStamp) ?? TimeEvents[0];
        return (time - timeEvent.TimeStamp) / (60000.0f / timeEvent.Bpm * 4.0f * timeEvent.TimeSig.Ratio) + timeEvent.BeatData.MeasureDecimal;
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
        if (TimeEvents == null || TimeEvents.Count == 0) return 0;
        
        Gimmick gimmick = TimeEvents.LastOrDefault(x => measureDecimal >= x.BeatData.MeasureDecimal) ?? TimeEvents[0];
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
    
    public static List<string> ReadLines(Stream stream)
    {
        List<string> lines = [];
        using StreamReader streamReader = new(stream);

        while (!streamReader.EndOfStream)
        {
            lines.Add(streamReader.ReadLine() ?? "");
        }

        return lines;
    }
}