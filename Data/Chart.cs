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

    /// <summary>
    /// Converts a clipboard string to a List of Notes.
    /// </summary>
    public List<Note> ReadClipboard(string clipboard)
    {
        string[] lines = clipboard.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        List<Note> notes = [];
        
        Dictionary<int, Note> notesByIndex = new();
        Dictionary<int, int> nextReferencedIndex = new();
        
        parseClipboard();

        if (notes.Count != 0)
        {
            getHoldReferences();
            errorCheck();
        }
        
        return notes;

        void parseClipboard()
        {
            foreach (string line in lines)
            {
                if (line.Contains("```")) continue;
                string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 8) continue;
            
                try
                {
                    int measure = Convert.ToInt32(split[0]);
                    int tick = Convert.ToInt32(split[1]);
                    int objectId = Convert.ToInt32(split[2]);
                
                    if (objectId != 1) continue;
                
                    int noteTypeId = Convert.ToInt32(split[3]);
                    int noteIndex = Convert.ToInt32(split[4]);
                    int position = Convert.ToInt32(split[5]);
                    int size = Convert.ToInt32(split[6]);
                    bool renderSegment = noteTypeId != 10 || Convert.ToBoolean(Convert.ToInt32(split[7])); // Set to true by default if note is not a hold segment.
                    
                    if (noteTypeId is 15 or 17 or 18 or 19 or > 26) continue; // Invalid note type
                
                    Note tempNote = new(measure, tick, noteTypeId, noteIndex, position, size, renderSegment);
                
                    // hold start & segments
                    if (noteTypeId is 9 or 10 or 25 && split.Length >= 9)
                    {
                        nextReferencedIndex[noteIndex] = Convert.ToInt32(split[8]);
                    }

                    if (noteTypeId is 12 or 13 && split.Length >= 9)
                    {
                        int direction = Convert.ToInt32(split[8]);
                        tempNote.MaskDirection = (MaskDirection)direction;
                    }

                    notes.Add(tempNote);
                    notesByIndex[noteIndex] = tempNote;
                }
                catch (Exception e)
                {
                    // Catch any Convert errors from invalid strings.
                    Console.WriteLine(e);
                }
            }
        }

        void getHoldReferences()
        {
            foreach (Note note in notes)
            {
                if (!nextReferencedIndex.TryGetValue(note.ParsedIndex, value: out int value)) continue;
                if (!notesByIndex.TryGetValue(value, out Note? referencedNote)) continue;
                
                note.NextReferencedNote = referencedNote;
                referencedNote.PrevReferencedNote = note;
            }
        }
        
        void errorCheck()
        {
            List<Note> garbage = [];

            // Remove negative timestamp notes
            foreach (Note note in notes)
            {
                if (note.BeatData.FullTick < 0) garbage.Add(note);
            }
            
            // Clamp size and position
            foreach (Note note in notes)
            {
                note.Size = int.Clamp(note.Size, Note.MinSize(note.NoteType), 60);
                note.Position = MathExtensions.Modulo(note.Position, 60);
            }
            
            // Check for incorrect hold note types
            foreach (Note note in notes)
            {
                if (!note.IsHold) continue;
                
                // Hold segment without a previous note
                if (note.NoteType is NoteType.HoldSegment && note.PrevReferencedNote == null)
                {
                    note.NoteType = NoteType.HoldStart;
                }
                
                // Hold segment without a next note
                if (note.NoteType is NoteType.HoldSegment && note.NextReferencedNote == null)
                {
                    note.NoteType = NoteType.HoldEnd;
                }
                
                // Hold start with a previous note
                if (note.NoteType is NoteType.HoldStart && note.PrevReferencedNote != null)
                {
                    note.NoteType = NoteType.HoldSegment;
                }
                
                // Hold end with a next note
                if (note.NoteType is NoteType.HoldEnd && note.NextReferencedNote != null)
                {
                    note.NoteType = NoteType.HoldSegment;
                }
            }
            
            // Check for missing hold references
            foreach (Note note in notes)
            {
                if (!note.IsHold) continue;
                
                // Hold end without a previous note
                if (note.NoteType is NoteType.HoldEnd && note.PrevReferencedNote == null)
                {
                    garbage.Add(note);
                }
                
                // Hold start without a next note
                if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote && note.NextReferencedNote == null)
                {
                    garbage.Add(note);
                }
            }

            // Remove necessary notes
            foreach (Note note in garbage)
            {
                notes.Remove(note);
            }
            
            // Readjust timestamps if not starting on 0 0
            if (notes[0].BeatData.FullTick != 0)
            {
                int distance = notes[0].BeatData.FullTick;
                foreach (Note note in notes)
                {
                    note.BeatData = new(note.BeatData.FullTick - distance);
                }
            }
        }
    }
    
    /// <summary>
    /// Converts a List of Notes to a clipboard-friendly string.
    /// </summary>
    public string WriteClipboard(List<Note> notes)
    {
        string result = "```\n";

        foreach (Note note in notes)
        {
            result += $"{note.BeatData.Measure,4:F0} {note.BeatData.Tick,4:F0} {(int)note.GimmickType,4:F0} {(int)note.NoteType,4:F0} ";
            result += $"{notes.IndexOf(note),4:F0} {note.Position,4:F0} {note.Size,4:F0} {Convert.ToInt32(note.RenderSegment, CultureInfo.InvariantCulture),4:F0}";
            
            if (note.IsMask) result += $" {(int)note.MaskDirection,4:F0}";
            if (note.NextReferencedNote != null) result += $" {notes.IndexOf(note.NextReferencedNote),4:F0}";

            result += "\n";
        }

        result += "```";
        
        return result;
    }

    public void LoadChartFromNetwork(string data)
    {
        string[] merFile = data.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        if (merFile.Length == 0) return;

        int readerIndex = 0;
        Dictionary<int, Note> notesByIndex = new();
        Dictionary<int, int> nextReferencedIndex = new();

        lock (this)
        {
            Clear();

            FilePath = "";
            
            readTags(merFile);
            readChartElements(merFile);
            getHoldReferences();
            repairNotes();
            getStartTimeEvents();
            
            GenerateTimeEvents();
            GenerateTimeScales();
        }
        
        IsSaved = true;
        IsNew = true;
        return;
        
        void readTags(string[] lines)
        {
            do
            {
                string line = lines[readerIndex];

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

            } while (++readerIndex < lines.Length);

            return;

            static string? getTag(string input, string tag)
            {
                return input.Contains(tag) ? input[(input.IndexOf(tag, StringComparison.Ordinal) + tag.Length)..].TrimStart() : null;
            }
        }

        void readChartElements(string[] lines)
        {
            const string separator = " ";
            for (int i = readerIndex; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(merFile[i])) continue;
                string[] parsed = merFile[i].Split(separator, StringSplitOptions.RemoveEmptyEntries);
                if (parsed.Length < 4) continue;

                Guid guid = Guid.Parse(parsed[0]);
                
                int measure = Convert.ToInt32(parsed[1]);
                int tick = Convert.ToInt32(parsed[2]);
                int objectId = Convert.ToInt32(parsed[3]);
                
                // Invalid
                if (objectId == 0) continue;

                // Note
                if (objectId == 1)
                {
                    int noteTypeId = Convert.ToInt32(parsed[4]);
                    int noteIndex = Convert.ToInt32(parsed[5]);
                    int position = Convert.ToInt32(parsed[6]);
                    int size = Convert.ToInt32(parsed[7]);
                    bool renderSegment = noteTypeId != 10 || Convert.ToBoolean(Convert.ToInt32(parsed[8])); // Set to true by default if note is not a hold segment.

                    Note tempNote = new(measure, tick, noteTypeId, noteIndex, position, size, renderSegment, guid);
                    
                    // hold start & segments
                    if (noteTypeId is 9 or 10 or 25 && parsed.Length >= 10)
                    {
                        nextReferencedIndex[noteIndex] = Convert.ToInt32(parsed[9]);
                    }

                    if (noteTypeId is 12 or 13 && parsed.Length >= 10)
                    {
                        int direction = Convert.ToInt32(parsed[9]);
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
                    if (objectId is 3 && parsed.Length > 5)
                    {
                        value1 = parsed[4];
                        value2 = parsed[5];
                    }
                    
                    // Edge case. some old charts apparently have broken time sigs.
                    if (objectId is 3 && parsed.Length == 5)
                    {
                        value1 = parsed[4];
                        value2 = parsed[4];
                    }
                    
                    if (objectId is 2 or 5 && parsed.Length > 4)
                    {
                        value1 = parsed[4];
                    }

                    Gimmick tempGimmick = new(measure, tick, objectId, value1, value2, guid);
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

    public string WriteChartToNetwork()
    {
        string result = "";
        
        result += $"#EDITOR_AUDIO {Path.GetFileName(AudioFilePath)}\n";
        result += $"#EDITOR_LEVEL {Level.ToString("F6", CultureInfo.InvariantCulture)}\n";
        result += $"#EDITOR_CLEAR_THRESHOLD {ClearThreshold.ToString("F6", CultureInfo.InvariantCulture)}\n";
        result += $"#EDITOR_AUTHOR {Author}\n";
        result += $"#EDITOR_PREVIEW_TIME {PreviewTime.ToString("F6", CultureInfo.InvariantCulture)}\n";
        result += $"#EDITOR_PREVIEW_LENGTH {PreviewLength.ToString("F6", CultureInfo.InvariantCulture)}\n";
        result += $"#EDITOR_OFFSET {Offset.ToString("F6", CultureInfo.InvariantCulture)}\n";
        result += $"#EDITOR_MOVIEOFFSET {MovieOffset.ToString("F6", CultureInfo.InvariantCulture)}\n";
        result += $"#BODY\n";
        
        foreach (Gimmick gimmick in Gimmicks)
        {
            result += $"{gimmick.Guid} {gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {(int)gimmick.GimmickType,4:F0}";
            result += gimmick.GimmickType switch
            {
                GimmickType.BpmChange => $" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}\n",
                GimmickType.HiSpeedChange => $" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}\n",
                GimmickType.TimeSigChange => $" {gimmick.TimeSig.Upper,5:F0}{gimmick.TimeSig.Lower,5:F0}\n",
                _ => "\n"
            };
        }

        foreach (Note note in Notes)
        {
            result += $"{note.Guid} {note.BeatData.Measure,4:F0} {note.BeatData.Tick,4:F0} {(int) note.GimmickType,4:F0} {(int) note.NoteType,4:F0} ";
            result += $"{Notes.IndexOf(note),4:F0} {note.Position,4:F0} {note.Size,4:F0} {Convert.ToInt32(note.RenderSegment, CultureInfo.InvariantCulture),4:F0}";
            
            if (note.IsMask) result += ($" {(int)note.MaskDirection,4:F0}");
            if (note.NextReferencedNote != null) result += ($" {Notes.IndexOf(note.NextReferencedNote),4:F0}");
            
            result += "\n";
        }
        
        return result;
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