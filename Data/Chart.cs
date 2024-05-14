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
    private List<TimeScaleData> TimeScales { get; } = [];

    public string FilePath { get; set; } = "";

    public string AudioFilePath { get; set; } = "";
    public decimal Level { get; set; }
    public decimal ClearThreshold { get; set; } = 0.83m;
    public string Author { get; set; } = "";
    public decimal PreviewTime { get; set; } // in seconds
    public decimal PreviewLength { get; set; } = 10; // in seconds
    public decimal Offset { get; set; } // in seconds
    public decimal MovieOffset { get; set; } // in seconds

    /// <summary>
    /// Loads a Chart from a .mer file.
    /// </summary>
    public bool LoadFile(string filepath)
    {
        if (filepath == "") return false;
        bool isMer = Path.GetExtension(filepath) == ".mer";
        
        Stream stream = File.OpenRead(filepath);

        List<string> merFile = ReadLines(stream);
        if (merFile.Count == 0) return false;

        int readerIndex = 0;
        Dictionary<int, Note> notesByIndex = new();
        Dictionary<int, int> nextReferencedIndex = new();

        lock (this)
        {
            Clear();

            FilePath = isMer ? "" : filepath;
            
            readTags(merFile);
            readChartElements(merFile);
            getHoldReferences();
            repairNotes();
            getStartTimeEvents();
            
            GenerateTimeEvents();
            GenerateTimeScales();
        }
        
        IsSaved = true;
        IsNew = isMer;
        return true;
        
        void readTags(List<string> lines)
        {
            do
            {
                string line = lines[readerIndex];

                string? audioFilePath = getTag(line, "#MUSIC_FILE_PATH") ?? getTag(line, "#EDITOR_AUDIO") ?? getTag(line, "#AUDIO");
                if (audioFilePath != null) AudioFilePath = Path.Combine(Path.GetDirectoryName(filepath) ?? "", audioFilePath);

                string? level = getTag(line, "#LEVEL") ?? getTag(line, "#EDITOR_LEVEL");
                if (level != null) Level = Convert.ToDecimal(level, CultureInfo.InvariantCulture);

                string? clearThreshold = getTag(line, "#CLEAR_THRESHOLD") ?? getTag(line, "#EDITOR_CLEAR_THRESHOLD");
                if (clearThreshold != null) ClearThreshold = Convert.ToDecimal(clearThreshold, CultureInfo.InvariantCulture);

                string? author = getTag(line, "#AUTHOR") ?? getTag(line, "#EDITOR_AUTHOR");
                if (author != null) Author = author;
                
                string? previewTime = getTag(line, "#PREVIEW_TIME") ?? getTag(line, "#EDITOR_PREVIEW_TIME");
                if (previewTime != null) PreviewTime = Convert.ToDecimal(previewTime, CultureInfo.InvariantCulture);
                
                string? previewLength = getTag(line, "#PREVIEW_LENGTH") ?? getTag(line, "#EDITOR_PREVIEW_LENGTH");
                if (previewLength != null) PreviewLength = Convert.ToDecimal(previewLength, CultureInfo.InvariantCulture);
                
                string? offset = getTag(line, "#OFFSET") ?? getTag(line, "EDITOR_OFFSET");
                if (offset != null) Offset = Convert.ToDecimal(offset, CultureInfo.InvariantCulture);
            
                string? movieOffset = getTag(line, "#MOVIEOFFSET") ?? getTag(line, "EDITOR_MOVIEOFFSET");
                if (movieOffset != null) MovieOffset = Convert.ToDecimal(movieOffset, CultureInfo.InvariantCulture);

                if (!line.Contains("#BODY")) continue;
                
                readerIndex++;
                break;

            } while (++readerIndex < lines.Count);

            return;

            static string? getTag(string input, string tag)
            {
                return input.Contains(tag) ? input[(input.IndexOf(tag, StringComparison.Ordinal) + tag.Length)..].TrimStart() : null;
            }
        }

        void readChartElements(List<string> lines)
        {
            const string separator = " ";
            for (int i = readerIndex; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(merFile[i])) continue;
                string[] parsed = merFile[i].Split(separator, StringSplitOptions.RemoveEmptyEntries);
                if (parsed.Length < 3) continue;

                int measure = Convert.ToInt32(parsed[0]);
                int tick = Convert.ToInt32(parsed[1]);
                int objectId = Convert.ToInt32(parsed[2]);
                
                // Invalid
                if (objectId == 0) continue;

                // Note
                if (objectId == 1)
                {
                    int noteTypeId = Convert.ToInt32(parsed[3]);
                    int noteIndex = Convert.ToInt32(parsed[4]);
                    int position = Convert.ToInt32(parsed[5]);
                    int size = Convert.ToInt32(parsed[6]);
                    bool renderSegment = noteTypeId != 10 || Convert.ToBoolean(Convert.ToInt32(parsed[7])); // Set to true by default if note is not a hold segment.

                    Note tempNote = new(measure, tick, noteTypeId, noteIndex, position, size, renderSegment);
                    
                    // hold start & segments
                    if (noteTypeId is 9 or 10 or 25 && parsed.Length >= 9)
                    {
                        nextReferencedIndex[noteIndex] = Convert.ToInt32(parsed[8]);
                    }

                    if (noteTypeId is 12 or 13 && parsed.Length >= 9)
                    {
                        int direction = Convert.ToInt32(parsed[8]);
                        tempNote.MaskDirection = (MaskDirection)direction;
                    }

                    Notes.Add(tempNote);
                    notesByIndex[noteIndex] = tempNote;
                }

                // Gimmick
                else
                {
                    string value1 = "";
                    string value2 = "";
                    
                    // avoid IndexOutOfRangeExceptions :]
                    if (objectId is 3 && parsed.Length > 4)
                    {
                        value1 = parsed[3];
                        value2 = parsed[4];
                    }
                    
                    // Edge case. some old charts apparently have broken time sigs.
                    if (objectId is 3 && parsed.Length == 4)
                    {
                        value1 = parsed[3];
                        value2 = parsed[3];
                    }
                    
                    if (objectId is 2 or 5 && parsed.Length > 3)
                    {
                        value1 = parsed[3];
                    }

                    Gimmick tempGimmick = new(measure, tick, objectId, value1, value2);
                    Gimmicks.Add(tempGimmick);
                }
            }
        }

        void getHoldReferences()
        {
            foreach (Note note in Notes)
            {
                if (!nextReferencedIndex.ContainsKey(note.ParsedIndex)) continue;
                if (!notesByIndex.TryGetValue(nextReferencedIndex[note.ParsedIndex], out Note? referencedNote)) continue;
                
                note.NextReferencedNote = referencedNote;
                referencedNote.PrevReferencedNote = note;
            }
        }

        void repairNotes()
        {
            foreach (Note note in Notes)
            {
                if (note.Position is < 0 or >= 60)
                    note.Position = MathExtensions.Modulo(note.Position, 60);

                note.Size = int.Clamp(note.Size, 1, 60);
            }
        }

        void getStartTimeEvents()
        {
            StartBpm = Gimmicks.LastOrDefault(x => x.BeatData.FullTick == 0 && x.GimmickType is GimmickType.BpmChange);
            StartTimeSig = Gimmicks.LastOrDefault(x => x.BeatData.FullTick == 0 && x.GimmickType is GimmickType.TimeSigChange);
        }
    }

    public bool WriteFile(string filepath, ChartWriteType writeType)
    {
        if (filepath == "") return false;
        
        Stream stream = File.OpenWrite(filepath);
        
        stream.SetLength(0);
        using StreamWriter streamWriter = new(stream, new UTF8Encoding(false));
        streamWriter.NewLine = "\n";

        if (writeType is ChartWriteType.Editor)
        {
            streamWriter.WriteLine($"#EDITOR_AUDIO {Path.GetFileName(AudioFilePath)}");
            streamWriter.WriteLine($"#EDITOR_LEVEL {Level.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#EDITOR_CLEAR_THRESHOLD {ClearThreshold.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#EDITOR_AUTHOR {Author}");
            streamWriter.WriteLine($"#EDITOR_PREVIEW_TIME {PreviewTime.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#EDITOR_PREVIEW_LENGTH {PreviewLength.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#EDITOR_OFFSET {Offset.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#EDITOR_MOVIEOFFSET {MovieOffset.ToString("F6", CultureInfo.InvariantCulture)}");
        }
            
        if (writeType is ChartWriteType.Saturn)
        {
            streamWriter.WriteLine($"#LEVEL {Level.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#AUDIO {Path.GetFileName(AudioFilePath)}");
            streamWriter.WriteLine($"#CLEAR_THRESHOLD {ClearThreshold.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#AUTHOR {Author}");
            streamWriter.WriteLine($"#PREVIEW_TIME {PreviewTime.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#PREVIEW_LENGTH {PreviewLength.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#OFFSET {Offset.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#MOVIEOFFSET {MovieOffset.ToString("F6", CultureInfo.InvariantCulture)}");
        }
            
        if (writeType is ChartWriteType.Mercury)
        {
            streamWriter.WriteLine("#MUSIC_SCORE_ID 0");
            streamWriter.WriteLine("#MUSIC_SCORE_VERSION 0");
            streamWriter.WriteLine("#GAME_VERSION");
            streamWriter.WriteLine($"#MUSIC_FILE_PATH");
            streamWriter.WriteLine($"#OFFSET {Offset.ToString("F6", CultureInfo.InvariantCulture)}");
            streamWriter.WriteLine($"#MOVIEOFFSET {MovieOffset.ToString("F6", CultureInfo.InvariantCulture)}");
        }
            
        streamWriter.WriteLine("#BODY");

        foreach (Gimmick gimmick in Gimmicks)
        {
            streamWriter.Write($"{gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {(int)gimmick.GimmickType,4:F0}");
            switch (gimmick.GimmickType)
            {
                case GimmickType.BpmChange: streamWriter.WriteLine($" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}");
                    break;
                case GimmickType.HiSpeedChange: streamWriter.WriteLine($" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}");
                    break;
                case GimmickType.TimeSigChange: streamWriter.WriteLine($"{gimmick.TimeSig.Upper,5:F0}{gimmick.TimeSig.Lower,5:F0}");
                    break;
                default:
                    streamWriter.WriteLine();
                    break;
            }
        }

        foreach (Note note in Notes)
        {
            streamWriter.Write($"{note.BeatData.Measure,4:F0} {note.BeatData.Tick,4:F0} {(int) note.GimmickType,4:F0} {(int) note.NoteType,4:F0} ");
            streamWriter.Write($"{Notes.IndexOf(note),4:F0} {note.Position,4:F0} {note.Size,4:F0} {Convert.ToInt32(note.RenderSegment, CultureInfo.InvariantCulture),4:F0}");
                
            if (note.IsMask) streamWriter.Write($" {(int)note.MaskDirection,4:F0}");
            if (note.NextReferencedNote != null) streamWriter.Write($" {Notes.IndexOf(note.NextReferencedNote),4:F0}");
                
            streamWriter.WriteLine();
        }

        return true;
    }

    public void Clear()
    {
        Notes.Clear();
        Gimmicks.Clear();
        TimeEvents?.Clear();
        TimeScales.Clear();

        FilePath = "";
        AudioFilePath = "";
        Level = 0;
        ClearThreshold = 0.83m;
        Author = "";
        PreviewTime = 0;
        PreviewLength = 10;
        Offset = 0;
        MovieOffset = 0;
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
        
        foreach (Gimmick gimmick in Gimmicks)
        {
            TimeScaleData data = calculateTimeScaleData(gimmick);
            lastData = data;
            
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
    
    public static List<string> ReadLines(Stream stream)
    {
        List<string> lines = new();
        using StreamReader streamReader = new(stream);

        while (!streamReader.EndOfStream)
        {
            lines.Add(streamReader.ReadLine() ?? "");
        }

        return lines;
    }
}