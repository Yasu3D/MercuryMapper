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

    public List<Note> Notes { get; set; } = [];
    public List<Gimmick> Gimmicks { get; set; } = [];

    private List<Gimmick>? TimeEvents { get; set; }
    private List<TimeScaleData> TimeScales { get; set; } = [];

    public string MusicFilePath { get; set; } = "";
    public string EditorMusicFilePath { get; set; } = "";
    public float Level { get; set; } = 0;
    public float ClearThreshold { get; set; } = 0.83f;
    public string Author { get; set; } = "";
    public float PreviewTime { get; set; } = 0; // in seconds
    public float PreviewLength { get; set; } = 10; // in seconds
    public float Offset { get; set; } = 0; // in seconds
    public float MovieOffset { get; set; } = 0; // in seconds

    /// <summary>
    /// Loads a Chart from a .mer file.
    /// </summary>
    public bool LoadFile(string filepath)
    {
        if (filepath == "") return false;
        
        Stream stream = File.OpenRead(filepath);

        var merFile = ReadLines(stream);
        if (merFile.Count == 0) return false;

        int readerIndex = 0;
        Dictionary<int, Note> notesByIndex = new();
        Dictionary<int, int> nextReferencedIndex = new();

        clear();
        readTags(merFile);
        readChartElements(merFile);
        getHoldReferences();
        repairNotes();
        
        GenerateTimeEvents();
        GenerateTimeScales();

        IsSaved = true;
        return true;

        void clear()
        {
            Notes.Clear();
            Gimmicks.Clear();
            TimeEvents?.Clear();
            TimeScales.Clear();
        }
        
        void readTags(List<string> lines)
        {
            do
            {
                string line = lines[readerIndex];

                string? musicFilePath = getTag(line, "#MUSIC_FILE_PATH") ?? getTag(line, "#EDITOR_AUDIO") ?? getTag(line, "#AUDIO");
                if (musicFilePath != null) MusicFilePath = musicFilePath;

                string? editorMusicFilePath = getTag(line, "#EDITOR_MUSIC_FILE_PATH");
                if (editorMusicFilePath != null) EditorMusicFilePath = editorMusicFilePath;

                string? level = getTag(line, "#LEVEL") ?? getTag(line, "#EDITOR_LEVEL");
                if (level != null) Level = Convert.ToSingle(level);

                string? clearThreshold = getTag(line, "#CLEAR_THRESHOLD") ?? getTag(line, "#EDITOR_CLEAR_THRESHOLD");
                if (clearThreshold != null) ClearThreshold = Convert.ToSingle(clearThreshold, CultureInfo.InvariantCulture);

                string? author = getTag(line, "#AUTHOR") ?? getTag(line, "#EDITOR_AUTHOR");
                if (author != null) Author = author;
                
                string? previewTime = getTag(line, "#PREVIEW_TIME") ?? getTag(line, "#EDITOR_PREVIEW_TIME");
                if (clearThreshold != null) PreviewTime = Convert.ToSingle(previewTime, CultureInfo.InvariantCulture);
                
                string? previewLength = getTag(line, "#PREVIEW_LENGTH") ?? getTag(line, "#EDITOR_PREVIEW_LENGTH");
                if (clearThreshold != null) PreviewLength = Convert.ToSingle(previewLength, CultureInfo.InvariantCulture);
                
                string? offset = getTag(line, "#OFFSET") ?? getTag(line, "EDITOR_OFFSET");
                if (offset != null) Offset = Convert.ToSingle(offset, CultureInfo.InvariantCulture);
            
                string? movieOffset = getTag(line, "#MOVIEOFFSET") ?? getTag(line, "EDITOR_MOVIEOFFSET");
                if (movieOffset != null) MovieOffset = Convert.ToSingle(movieOffset, CultureInfo.InvariantCulture);

                if (!line.Contains("#BODY")) continue;
                
                readerIndex++;
                break;

            } while (++readerIndex < lines.Count);

            return;

            static string? getTag(string input, string tag)
            {
                return input.Contains(tag) ? input.Substring(input.IndexOf(tag, StringComparison.Ordinal) + tag.Length) : null;
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
                    bool renderSegment = Convert.ToBoolean(Convert.ToInt32(parsed[7]));

                    Note tempNote = new Note(measure, tick, noteTypeId, position, size, renderSegment);
                    
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
            for (int i = 0; i < Notes.Count; i++)
            {
                if (!nextReferencedIndex.ContainsKey(i)) continue;
                if (!notesByIndex.TryGetValue(nextReferencedIndex[i], out Note? referencedNote)) continue;

                Notes[i].NextReferencedNote = referencedNote;
                referencedNote.PrevReferencedNote = Notes[i];
            }
        }

        void repairNotes()
        {
            foreach (Note note in Notes)
            {
                if (note.Position is < 0 or >= 60)
                    note.Position = MathExtensions.Modulo(note.Position, 60);
            }
        }
    }

    public bool WriteFile(string filepath, ChartWriteType writeType, bool setSaved = true)
    {
        if (filepath == "") return false;
        
        Stream stream = File.OpenWrite(filepath);
        
        stream.SetLength(0);
        using (StreamWriter streamWriter = new(stream, new UTF8Encoding(false)))
        {
            streamWriter.NewLine = "\n";

            if (writeType is ChartWriteType.Editor)
            {
                streamWriter.WriteLine($"#EDITOR_MUSIC_FILE_PATH {EditorMusicFilePath}");
                streamWriter.WriteLine($"#EDITOR_AUDIO {MusicFilePath}");
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
                streamWriter.WriteLine($"#AUDIO {MusicFilePath}");
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
                streamWriter.WriteLine("#GAME_VERSION ");
                streamWriter.WriteLine($"#MUSIC_FILE_PATH {MusicFilePath}");
                streamWriter.WriteLine($"#OFFSET {Offset.ToString("F6", CultureInfo.InvariantCulture)}");
                streamWriter.WriteLine($"#MOVIEOFFSET {MovieOffset.ToString("F6", CultureInfo.InvariantCulture)}");
            }
            
            streamWriter.WriteLine("#BODY");

            foreach (Gimmick gimmick in Gimmicks)
            {
                streamWriter.Write($"{gimmick.BeatData.Measure,4:F0}{gimmick.BeatData.Tick,5:F0}{(int)gimmick.GimmickType,5:F0}");
                switch (gimmick.GimmickType)
                {
                    case GimmickType.BpmChange: streamWriter.WriteLine($" {gimmick.Bpm:F6}");
                        break;
                    case GimmickType.HiSpeedChange: streamWriter.WriteLine($" {gimmick.HiSpeed:F6}");
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
                streamWriter.Write($"{note.BeatData.Measure,4:F0}{note.BeatData.Tick,5:F0}{(int) note.GimmickType,5:F0}{(int) note.NoteType,5:F0}");
                streamWriter.Write($"{Notes.IndexOf(note),5:F0}{note.Position,5:F0}{note.Size,5:F0}{Convert.ToInt32(note.RenderSegment, CultureInfo.InvariantCulture),5:F0}");
                
                if (note.IsMask) streamWriter.Write($"{(int)note.MaskDirection,5:F0}");
                if (note.NextReferencedNote != null) streamWriter.Write($"{Notes.IndexOf(note.NextReferencedNote),5:F0}");
                
                streamWriter.WriteLine();
            }
        }
        
        IsSaved = setSaved;
        return true;
    }
    
    /// <summary>
    /// Generates "Time Events" [= Bpm/TimeSig changes merged into one list]
    /// </summary>
    public void GenerateTimeEvents()
    {
        Gimmicks = Gimmicks.OrderBy(x => x.BeatData.MeasureDecimal).ToList();
        var timeSigChange = Gimmicks.FirstOrDefault(x => x is { GimmickType: GimmickType.TimeSigChange, BeatData.FullTick: 0 });
        var bpmChange = Gimmicks.FirstOrDefault(x => x is { GimmickType: GimmickType.BpmChange, BeatData.FullTick: 0 });
        if (timeSigChange == null || bpmChange == null) return;
        
        float lastBpm = bpmChange.Bpm;
        TimeSig lastTimeSig = timeSigChange.TimeSig;

        TimeEvents = [];
        
        foreach (Gimmick gimmick in Gimmicks)
        {
            if (gimmick.GimmickType is not GimmickType.BpmChange or GimmickType.TimeSigChange) continue;
            
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
            float timeStamp = timeDifference * (4 * previous.TimeSig.Ratio) * (60000.0f / previous.Bpm) + previous.TimeStamp;
        }
    }

    /// <summary>
    /// Generates MeasureDecimals scaled by Time Events and HiSpeed.
    /// </summary>
    public void GenerateTimeScales()
    {
        TimeScales.Clear();
        
        foreach (Gimmick gimmick in Gimmicks)
            TimeScales.Add(CalculateTimeScaleData(gimmick.BeatData.MeasureDecimal));
        
        // I have no idea why this has to exist, but past me said so and it makes things work.
        TimeScales.Add(CalculateTimeScaleData(float.PositiveInfinity));
    }

    /// <summary>
    /// Creates data to quickly calculate scaled MeasureDecimal timestamps at a given measure.
    /// </summary>
    public TimeScaleData CalculateTimeScaleData(float measureDecimal)
    {
        float scaledPosition = 0;
        float lastMeasurePosition = 0;
        float currentHiSpeedValue = 1;
        float currentTimeSigValue = 1;
        float currentBpmValue = 1;

        var relevantGimmicks = Gimmicks.Where(x =>
            x.BeatData.MeasureDecimal < measureDecimal &&
            x.GimmickType is GimmickType.HiSpeedChange or GimmickType.TimeSigChange or GimmickType.BpmChange);

        float startBpm = Gimmicks.First(x => x.GimmickType is GimmickType.BpmChange).Bpm;

        foreach (Gimmick gimmick in relevantGimmicks)
        {
            float distance = gimmick.BeatData.MeasureDecimal - lastMeasurePosition;
            scaledPosition += (distance * currentHiSpeedValue * currentTimeSigValue * currentBpmValue) - distance;

            lastMeasurePosition = gimmick.BeatData.MeasureDecimal;

            switch (gimmick.GimmickType)
            {
                case GimmickType.TimeSigChange:
                    currentTimeSigValue = gimmick.TimeSig.Ratio;
                    break;
                case GimmickType.HiSpeedChange:
                    currentHiSpeedValue = gimmick.HiSpeed;
                    break;
                case GimmickType.BpmChange:
                    currentBpmValue = startBpm / gimmick.Bpm;
                    break;
            }
        }

        return new()
        {
            UnscaledMeasureDecimal = measureDecimal,
            PartialScaledPosition = scaledPosition,
            LastMeasurePosition = lastMeasurePosition,
            CurrentHiSpeed = currentHiSpeedValue,
            CurrentTimeSigRatio = currentTimeSigValue,
            CurrentBpmRatio = currentBpmValue
        };
    }

    /// <summary>
    /// Finds nearest TimeScaleData via binary search and calculates ScaledMeasureDecimal off of it.
    /// </summary>
    public float GetScaledMeasureDecimal(float measureDecimal)
    {
        TimeScaleData? timeScaleData = binarySearchTimeScales(measureDecimal);
        if (timeScaleData == null) return measureDecimal;

        float scaledPosition = measureDecimal + timeScaleData.PartialScaledPosition;
        float distance = measureDecimal - timeScaleData.LastMeasurePosition;

        scaledPosition += distance * timeScaleData.CurrentHiSpeed * timeScaleData.CurrentTimeSigRatio * timeScaleData.CurrentBpmRatio - distance;
        return scaledPosition;
        
        TimeScaleData? binarySearchTimeScales(float measure)
        {
            int left = 0;
            int right = TimeScales.Count - 1;
            TimeScaleData? result = null;

            while (left <= right)
            {
                int center = left + (right - left) / 2;

                if (TimeScales[center].UnscaledMeasureDecimal <= measure)
                {
                    left = center + 1;
                }
                
                else
                {
                    result = TimeScales[center];
                    right = center - 1;
                }
            }

            return result;
        }
    }
    
    

    public BeatData GetBeatDataFromTimestamp(float time)
    {
        if (TimeEvents == null || TimeEvents.Count == 0) return new(-1, 0);

        Gimmick gimmick = TimeEvents.LastOrDefault(x => time >= x.TimeStamp) ?? TimeEvents[0];
        return new((time - gimmick.TimeStamp) / (60000.0f / gimmick.Bpm * 4.0f * gimmick.TimeSig.Ratio) + gimmick.BeatData.MeasureDecimal);
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