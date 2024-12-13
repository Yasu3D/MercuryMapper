using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAvalonia.Core;
using MercuryMapper.Enums;
using MercuryMapper.Views;

namespace MercuryMapper.Data;

public static class FormatHandler
{
    /// <summary>
    /// Automatically detects a chart file's format, then loads it into the editor.
    /// </summary>
    /// <remarks>Detection is somewhat naive and only looks for .SAT's version marker.</remarks>
    public static void LoadFile(Chart chart, string filepath)
    {
        // TryCatch to handle any IO exceptions, and to handle ArrayOutOfBounds
        // in case a broken file is parsed (and I messed up a check somewhere in the code).
        try
        {
            string[] data = File.ReadLines(filepath).ToArray();
            if (data.Length == 0) return;

            bool isSat = false;

            foreach (string s in data)
            {
                if (!ContainsTag(s, "@SAT_VERSION ", out _)) continue;

                isSat = true;
                break;
            }
            
            // Naively detect .SAT format
            if (isSat)
            {
                SatHandler.LoadFile(chart, filepath, data);
            }
            else
            {
                MerHandler.LoadFile(chart, filepath, data);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Writes a chart file in the specified format.
    /// </summary>
    public static void WriteFile(Chart chart, string filepath, ChartFormatType formatType)
    {
        try
        {
            switch (formatType)
            {
                case ChartFormatType.Saturn:
                case ChartFormatType.Editor:
                {
                    SatHandler.WriteFile(chart, filepath);
                    break;
                }
            
                case ChartFormatType.Mercury:
                {
                    MerHandler.WriteFile(chart, filepath);
                    break;
                }
            
                default:
                    return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static void LoadFileFromNetwork(Chart chart, string data)
    {
        SatHandler.LoadFile(chart, "", data.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries), true);
    }
    public static string WriteFileToNetwork(Chart chart) => SatHandler.WriteFile(chart, "", true);
    
    /// <summary>
    /// Converts a clipboard string to a List of Notes.
    /// </summary>
    public static IEnumerable<Note> ParseClipboard(string clipboard)
    {
        string[] objects = clipboard.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        List<Note> notes = [];
        SatHandler.ParseObjects(objects, notes);
        return notes;
    }

    /// <summary>
    /// Converts a List of Notes to a clipboard-friendly string.
    /// </summary>
    public static string WriteClipboard(IEnumerable<Note> notes)
    {
        string result = "```\n";
        SatHandler.WriteObjects(notes, ref result);
        result += "```";
        return result;
    }
    
    internal static bool ContainsTag(string input, string tag, out string result)
    {
        if (input.Contains(tag))
        {
            result = input[(input.IndexOf(tag, StringComparison.Ordinal) + tag.Length)..].Trim();
            return true;
        }
  
        result = "";
        return false;
    }
}

internal static class MerHandler
{
    /// <summary>
    /// Parses a MER format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
    /// <param name="filepath">Absolute filepath of the opened file.</param>
    /// <param name="data">Chart Data, split into individual lines.</param>
    public static void LoadFile(Chart chart, string filepath, string[] data)
    {
        int contentSeparator = Array.IndexOf(data, "#BODY");
        if (contentSeparator == -1) return;

        string[] metadata = data[..contentSeparator];
        string[] content = data[(contentSeparator + 1)..];

        Dictionary<int, Note> notesByIndex = new();
        Dictionary<int, int> nextReferencedIndex = new();

        lock (chart)
        {
            chart.Clear();
            chart.Filepath = filepath;

            parseMetadata();
            parseContent();
            connectHolds();
            chart.RepairNotes();

            chart.StartBpm = chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick == 0 && x.GimmickType is GimmickType.BpmChange);
            chart.StartTimeSig = chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick == 0 && x.GimmickType is GimmickType.TimeSigChange);

            chart.GenerateTimeEvents();
            chart.GenerateTimeScales();

            chart.IsSaved = false;
            chart.IsNew = Path.GetExtension(filepath) != ".map";
        }

        return;

        void parseMetadata()
        {
            foreach (string line in metadata)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string result;

                if (FormatHandler.ContainsTag(line, "#EDITOR_AUDIO ", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_AUTHOR ", out result)) chart.Author = result;
                if (FormatHandler.ContainsTag(line, "#EDITOR_LEVEL ", out result)) chart.Level = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#EDITOR_CLEAR_THRESHOLD ", out result)) chart.ClearThreshold = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#EDITOR_PREVIEW_TIME ", out result)) chart.PreviewStart = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#EDITOR_PREVIEW_LENGTH ", out result)) chart.PreviewTime = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#EDITOR_OFFSET ", out result)) chart.BgmOffset = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#EDITOR_MOVIEOFFSET ", out result)) chart.BgaOffset = Convert.ToDecimal(result, CultureInfo.InvariantCulture);

                if (FormatHandler.ContainsTag(line, "#AUDIO ", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (FormatHandler.ContainsTag(line, "#AUTHOR ", out result)) chart.Author = result;
                if (FormatHandler.ContainsTag(line, "#LEVEL ", out result)) chart.Level = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#CLEAR_THRESHOLD ", out result)) chart.ClearThreshold = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#PREVIEW_TIME ", out result)) chart.PreviewStart = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#PREVIEW_LENGTH ", out result)) chart.PreviewTime = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#OFFSET ", out result)) chart.BgmOffset = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                if (FormatHandler.ContainsTag(line, "#MOVIEOFFSET ", out result)) chart.BgaOffset = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            }
        }

        void parseContent()
        {
            foreach (string line in content)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 3) continue;

                int measure = Convert.ToInt32(split[0], CultureInfo.InvariantCulture);
                int tick = Convert.ToInt32(split[1], CultureInfo.InvariantCulture);
                int objectId = Convert.ToInt32(split[2], CultureInfo.InvariantCulture);

                // Invalid
                if (objectId is < 1 or > 10) continue;

                // Notes
                if (objectId == 1)
                {
                    int noteTypeId = Convert.ToInt32(split[3], CultureInfo.InvariantCulture);
                    int noteIndex = Convert.ToInt32(split[4], CultureInfo.InvariantCulture);
                    int position = Convert.ToInt32(split[5], CultureInfo.InvariantCulture);
                    int size = Convert.ToInt32(split[6], CultureInfo.InvariantCulture);
                    
                    bool renderSegment = noteTypeId != 10 || Convert.ToBoolean(Convert.ToInt32(split[7], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture); // Set to true by default if note is not a hold segment.

                    // End Of Chart
                    if (noteTypeId == 14)
                    {
                        Gimmick newGimmick = new(measure, tick, GimmickType.EndOfChart, "", "");
                        chart.Gimmicks.Add(newGimmick);
                        continue;
                    }
                    
                    NoteType noteType = Note.NoteTypeFromMerId(noteTypeId);
                    BonusType bonusType = Note.BonusTypeFromMerId(noteTypeId);
                    
                    Note newNote = new(measure, tick, noteType, bonusType, noteIndex, position, size, renderSegment, TraceColor.White);

                    // hold start & segments
                    if (noteTypeId is 9 or 10 or 25 && split.Length >= 9)
                    {
                        nextReferencedIndex[noteIndex] = Convert.ToInt32(split[8], CultureInfo.InvariantCulture);
                    }

                    if (noteTypeId is 12 or 13 && split.Length >= 9)
                    {
                        int direction = Convert.ToInt32(split[8], CultureInfo.InvariantCulture);
                        newNote.MaskDirection = (MaskDirection)direction;
                    }

                    chart.Notes.Add(newNote);
                    notesByIndex[noteIndex] = newNote;
                }

                // Gimmicks
                else
                {
                    string value1 = "";
                    string value2 = "";

                    // avoid IndexOutOfRangeExceptions :]
                    if (objectId is 3 && split.Length > 4)
                    {
                        value1 = split[3];
                        value2 = split[4];
                    }

                    // Edge case. some old charts apparently have broken time sigs.
                    if (objectId is 3 && split.Length == 4)
                    {
                        value1 = split[3];
                        value2 = split[3];
                    }

                    if (objectId is 2 or 5 && split.Length > 3)
                    {
                        value1 = split[3];
                    }

                    Gimmick newGimmick = new(measure, tick, (GimmickType)objectId, value1, value2);
                    chart.Gimmicks.Add(newGimmick);
                }
            }
        }

        void connectHolds()
        {
            foreach (Note note in chart.Notes)
            {
                if (!nextReferencedIndex.TryGetValue(note.ParsedIndex, out int value)) continue;
                if (!notesByIndex.TryGetValue(value, out Note? referencedNote)) continue;

                note.NextReferencedNote = referencedNote;
                referencedNote.PrevReferencedNote = note;
            }
        }
    }

    /// <summary>
    /// Writes a MER format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
    /// <param name="filepath">Absolute filepath to chart file.</param>
    public static void WriteFile(Chart chart, string filepath)
    {
        if (filepath == "") return;

        string result = $"#MUSIC_SCORE_ID 0\n" +
                        $"#MUSIC_SCORE_VERSION 0\n" +
                        $"#GAME_VERSION\n" +
                        $"#MUSIC_FILE_PATH\n" +
                        $"#OFFSET {chart.BgmOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" +
                        $"#MOVIEOFFSET {chart.BgaOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" +
                        $"#BODY\n";

        foreach (Gimmick gimmick in chart.Gimmicks)
        {
            if (gimmick.GimmickType is GimmickType.EndOfChart) continue;
            
            result += $"{gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {(int)gimmick.GimmickType,4:F0}";
            result += gimmick.GimmickType switch
            {
                GimmickType.BpmChange => $" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}\n",
                GimmickType.HiSpeedChange => $" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}\n",
                GimmickType.TimeSigChange => $" {gimmick.TimeSig.Upper,5:F0} {gimmick.TimeSig.Lower,5:F0}\n",
                _ => "\n",
            };
        }
        
        foreach (Note note in chart.Notes)
        {
            if (note.NoteType is NoteType.Trace or NoteType.Damage) continue;
            
            result += $"{note.BeatData.Measure,4:F0} " +
                      $"{note.BeatData.Tick,4:F0} " +
                      $"   1 " +
                      $"{note.NoteToMerId(),4:F0} " +
                      $"{chart.Notes.IndexOf(note),4:F0} " +
                      $"{note.Position,4:F0} " +
                      $"{note.Size,4:F0} " +
                      $"{Convert.ToInt32(note.RenderSegment, CultureInfo.InvariantCulture),4:F0}";

            if (note.IsMask) result += $" {(int)note.MaskDirection,4:F0}";
            if (note.NextReferencedNote != null) result += $" {chart.Notes.IndexOf(note.NextReferencedNote),4:F0}";

            result += "\n";
        }

        if (chart.EndOfChart != null)
        {
            result += $"{chart.EndOfChart.BeatData.Measure,4:F0} " +
                      $"{chart.EndOfChart.BeatData.Tick,4:F0} " +
                      $"   1 " +
                      $"  14 " +
                      $"{chart.Notes.Count} " +
                      $"   0 " +
                      $"  60 " +
                      $"   1";
            
            result += "\n";
        }
        
        File.WriteAllTextAsync(filepath, result);
    }
}

internal static class SatHandler
{
    private const string SatFormatVersion = "2";

    /// <summary>
    /// Parses a SAT format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
    /// <param name="filepath">Absolute filepath of the opened file.</param>
    /// <param name="data">Chart Data, split into individual lines.</param>
    /// <param name="includeGuid">Take GUIDs into account when parsing.</param>
    public static void LoadFile(Chart chart, string filepath, string[] data, bool includeGuid = false)
    {
        int commentIndex = data.IndexOf("@COMMENTS");
        int gimmickIndex = data.IndexOf("@GIMMICKS");
        int objectIndex = data.IndexOf("@OBJECTS");

        if (commentIndex < 0 || gimmickIndex < 0 || objectIndex < 0) return;

        string[] metadata = data[1..commentIndex];
        string[] comments = data[(commentIndex + 1)..gimmickIndex];
        string[] gimmicks = data[(gimmickIndex + 1)..objectIndex];
        string[] objects = data[(objectIndex + 1)..];
        
        lock (chart)
        {
            chart.Clear();
            chart.Filepath = filepath;

            ParseMetadata(metadata, chart);
            
            if (!includeGuid) ParseComments(comments, chart);
            
            if (includeGuid) ParseGimmicksWithGuid(gimmicks, chart); 
            else ParseGimmicks(gimmicks, chart);
            
            if (includeGuid) ParseObjectsWithGuid(objects, chart.Notes); 
            else ParseObjects(objects, chart.Notes);
            
            chart.RepairNotes();
            chart.Notes = chart.Notes.OrderBy(x => x.BeatData.FullTick).ToList();

            chart.StartBpm = chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick == 0 && x.GimmickType is GimmickType.BpmChange);
            chart.StartTimeSig = chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick == 0 && x.GimmickType is GimmickType.TimeSigChange);

            chart.GenerateTimeEvents();
            chart.GenerateTimeScales();

            chart.IsSaved = true;
            chart.IsNew = false;
        }
    }

    /// <summary>
    /// Writes a SAT format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
    /// <param name="filepath">Absolute filepath to chart file.</param>
    /// <param name="includeGuid">Include GUIDs when writing file.</param>
    public static string WriteFile(Chart chart, string filepath, bool includeGuid = false)
    {
        if (filepath == "" && includeGuid == false) return "";

        string result = "";
        
        WriteMetadata(chart, ref result);
        
        if (includeGuid) WriteCommentsWithGuid(chart, ref result);
        else WriteComments(chart, ref result);
        
        if (includeGuid) WriteGimmicksWithGuid(chart, ref result);
        else WriteGimmicks(chart, ref result);
        
        if (includeGuid) WriteObjectsWithGuid(chart.Notes, ref result);
        else WriteObjects(chart.Notes, ref result);

        if (filepath != "") File.WriteAllTextAsync(filepath, result);
        return result;
    }
    
    // Parsing
    private static void ParseMetadata(IEnumerable<string> metadata, Chart chart)
    {
        foreach (string line in metadata)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith('#')) continue;

            if (FormatHandler.ContainsTag(line, "@GUID ", out string result)) chart.Guid = result;
            if (FormatHandler.ContainsTag(line, "@VERSION ", out result)) chart.Version = result;
            if (FormatHandler.ContainsTag(line, "@TITLE ", out result)) chart.Title = result;
            if (FormatHandler.ContainsTag(line, "@RUBI ", out result)) chart.Rubi = result;
            if (FormatHandler.ContainsTag(line, "@ARTIST ", out result)) chart.Artist = result;
            if (FormatHandler.ContainsTag(line, "@AUTHOR ", out result)) chart.Author = result;

            if (FormatHandler.ContainsTag(line, "@DIFF ", out result)) chart.Diff = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            if (FormatHandler.ContainsTag(line, "@LEVEL ", out result)) chart.Level = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            if (FormatHandler.ContainsTag(line, "@CLEAR ", out result)) chart.ClearThreshold = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            if (FormatHandler.ContainsTag(line, "@BPM_TEXT ", out result)) chart.BpmText = result;

            if (FormatHandler.ContainsTag(line, "@PREVIEW_START ", out result)) chart.PreviewStart = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            if (FormatHandler.ContainsTag(line, "@PREVIEW_TIME ", out result)) chart.PreviewTime = Convert.ToDecimal(result, CultureInfo.InvariantCulture);

            if (FormatHandler.ContainsTag(line, "@BGM ", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
            if (FormatHandler.ContainsTag(line, "@BGM_OFFSET ", out result)) chart.BgmOffset = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            if (FormatHandler.ContainsTag(line, "@BGA ", out result)) chart.BgaFilepath = result == "" ? "" : Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
            if (FormatHandler.ContainsTag(line, "@BGA_OFFSET ", out result)) chart.BgaOffset = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
            if (FormatHandler.ContainsTag(line, "@JACKET ", out result)) chart.JacketFilepath = result == "" ? "" : Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
        }
    }
    
    private static void ParseComments(IEnumerable<string> comments, Chart chart)
    {
        // regex is scary D:
        const string pattern = @"^\s*(\d+)\s+(\d+)\s+(\d+)\s+(.*)";
        
        foreach (string line in comments)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            Match match = Regex.Match(line, pattern);

            if (!match.Success) continue;

            string match1 = match.Groups[1].Value;
            string match2 = match.Groups[2].Value;
            // match3 is the index, can be skipped
            string match4 = match.Groups[4].Value;

            int measure = Convert.ToInt32(match1, CultureInfo.InvariantCulture);
            int tick = Convert.ToInt32(match2, CultureInfo.InvariantCulture);

            chart.ChartEditor.AddComment(new(measure, tick), match4);
        }
    }
    
    private static void ParseGimmicks(IEnumerable<string> gimmicks, Chart chart)
    {
        foreach (string line in gimmicks)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 4) continue;

            int measure = Convert.ToInt32(split[0], CultureInfo.InvariantCulture);
            int tick = Convert.ToInt32(split[1], CultureInfo.InvariantCulture);

            GimmickType gimmickType = String2GimmickType(split[3]);

            string value1 = "";
            string value2 = "";

            if (gimmickType is GimmickType.BpmChange or GimmickType.HiSpeedChange && split.Length == 5)
            {
                value1 = split[4];
            }

            if (gimmickType is GimmickType.TimeSigChange && split.Length == 6)
            {
                value1 = split[4];
                value2 = split[5];
            }

            Gimmick gimmick = new(measure, tick, gimmickType, value1, value2);
            chart.Gimmicks.Add(gimmick);
        }
    }

    internal static void ParseObjects(IEnumerable<string> objects, List<Note> notes)
    {
        Note? previousNote = null;

        foreach (string line in objects)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 6) continue;

            int measure = Convert.ToInt32(split[0], CultureInfo.InvariantCulture);
            int tick = Convert.ToInt32(split[1], CultureInfo.InvariantCulture);
            int index = Convert.ToInt32(split[2], CultureInfo.InvariantCulture);
            int position = Convert.ToInt32(split[3], CultureInfo.InvariantCulture);
            int size = Convert.ToInt32(split[4], CultureInfo.InvariantCulture);

            string[] attributes = split[5].Split('.', StringSplitOptions.RemoveEmptyEntries);

            NoteType noteType = String2NoteType(attributes);
            BonusType bonusType = String2BonusType(attributes);
            MaskDirection maskDirection = String2MaskDirection(attributes);
            TraceColor color = String2TraceColor(attributes);
            bool renderSegment = !attributes.Contains("NR");
            
            Note note = new(measure, tick, noteType, bonusType, index, position, size, renderSegment, color);

            if (note.IsMask)
            {
                note.MaskDirection = maskDirection;
                note.BonusType = BonusType.None;
            }

            NoteLinkType tempLinkType = String2NoteLinkType(attributes);
            if (tempLinkType is NoteLinkType.Point or NoteLinkType.End)
            {
                note.PrevReferencedNote = previousNote;
                note.BonusType = BonusType.None;
                
                if (previousNote != null)
                {
                    previousNote.NextReferencedNote = note;
                    note.Color = previousNote.Color;
                }
            }

            notes.Add(note);
            previousNote = note;
        }
    }
    
    private static void ParseGimmicksWithGuid(IEnumerable<string> gimmicks, Chart chart)
    {
        foreach (string line in gimmicks)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 4) continue;

            Guid guid = Guid.Parse(split[0], CultureInfo.InvariantCulture);
            
            int measure = Convert.ToInt32(split[1], CultureInfo.InvariantCulture);
            int tick = Convert.ToInt32(split[2], CultureInfo.InvariantCulture);

            GimmickType gimmickType = String2GimmickType(split[4]);

            string value1 = "";
            string value2 = "";

            if (gimmickType is GimmickType.BpmChange or GimmickType.HiSpeedChange && split.Length == 6)
            {
                value1 = split[5];
            }

            if (gimmickType is GimmickType.TimeSigChange && split.Length == 7)
            {
                value1 = split[5];
                value2 = split[6];
            }

            Gimmick gimmick = new(measure, tick, gimmickType, value1, value2, guid);
            chart.Gimmicks.Add(gimmick);
        }
    }
    
    private static void ParseObjectsWithGuid(IEnumerable<string> objects, List<Note> notes)
    {
        Note? previousNote = null;

        foreach (string line in objects)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 6) continue;

            Guid guid = Guid.Parse(split[0], CultureInfo.InvariantCulture);
            
            int measure = Convert.ToInt32(split[1], CultureInfo.InvariantCulture);
            int tick = Convert.ToInt32(split[2], CultureInfo.InvariantCulture);
            int index = Convert.ToInt32(split[3], CultureInfo.InvariantCulture);
            int position = Convert.ToInt32(split[4], CultureInfo.InvariantCulture);
            int size = Convert.ToInt32(split[5], CultureInfo.InvariantCulture);

            string[] attributes = split[6].Split('.', StringSplitOptions.RemoveEmptyEntries);

            NoteType noteType = String2NoteType(attributes);
            BonusType bonusType = String2BonusType(attributes);
            MaskDirection maskDirection = String2MaskDirection(attributes);
            TraceColor color = String2TraceColor(attributes);
            bool renderSegment = !attributes.Contains("NR");

            Note note = new(measure, tick, noteType, bonusType, index, position, size, renderSegment, color, guid);

            if (note.IsMask)
            {
                note.MaskDirection = maskDirection;
                note.BonusType = BonusType.None;
            }

            NoteLinkType tempLinkType = String2NoteLinkType(attributes);
            if (tempLinkType is NoteLinkType.Point or NoteLinkType.End)
            {
                note.PrevReferencedNote = previousNote;
                note.BonusType = BonusType.None;
                
                if (previousNote != null)
                {
                    previousNote.NextReferencedNote = note;
                    note.Color = previousNote.Color;
                }
            }

            notes.Add(note);
            previousNote = note;
        }
    }
    
    // Writing
    private static void WriteMetadata(Chart chart, ref string input)
    {
        input += $"# Created with MercuryMapper {MainView.AppVersion}\n" +
                 $"{"@SAT_VERSION",-16}{SatFormatVersion}\n" + 
                 $"\n" + $"{"@VERSION",-16}{chart.Version}\n" + 
                 $"{"@GUID", -16}MM{Guid.NewGuid()}\n" +
                 $"{"@TITLE",-16}{chart.Title}\n" + 
                 $"{"@RUBI",-16}{chart.Rubi}\n" + 
                 $"{"@ARTIST",-16}{chart.Artist}\n" + 
                 $"{"@AUTHOR",-16}{chart.Author}\n" + 
                 $"\n" + 
                 $"{"@DIFF",-16}{chart.Diff}\n" + 
                 $"{"@LEVEL",-16}{chart.Level.ToString("F6", CultureInfo.InvariantCulture)}\n" + 
                 $"{"@CLEAR",-16}{chart.ClearThreshold.ToString("F6", CultureInfo.InvariantCulture)}\n" + 
                 $"{"@BPM_TEXT",-16}{chart.BpmText}\n" + 
                 $"\n" + 
                 $"{"@PREVIEW_START",-16}{chart.PreviewStart}\n" + 
                 $"{"@PREVIEW_TIME",-16}{chart.PreviewTime}\n" + 
                 $"\n" + 
                 $"{"@BGM",-16}{Path.GetFileName(chart.BgmFilepath)}\n" +
                 $"{"@BGM_OFFSET",-16}{chart.BgmOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" + 
                 $"{"@BGA",-16}{Path.GetFileName(chart.BgaFilepath)}\n" + 
                 $"{"@BGA_OFFSET",-16}{chart.BgaOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" + 
                 $"{"@JACKET",-16}{Path.GetFileName(chart.JacketFilepath)}\n" + 
                 $"\n";
    }

    private static void WriteComments(Chart chart, ref string input)
    {
        int index = 0;
        input += "@COMMENTS\n";
        
        foreach (KeyValuePair<string, Comment> comment in chart.Comments)
        {
            input += $"{comment.Value.BeatData.Measure,4:F0} {comment.Value.BeatData.Tick,4:F0} {index,4:F0} {comment.Value.Text}";
            
            input += "\n";
            index++;
        }
        
        input += "\n";
    }

    private static void WriteGimmicks(Chart chart, ref string input)
    {
        int index = 0;
        input += "@GIMMICKS\n";
        foreach (Gimmick gimmick in chart.Gimmicks)
        {
            input += $"{gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {index,4:F0} {GimmickType2String(gimmick.GimmickType),-13}";

            if (gimmick.GimmickType is GimmickType.BpmChange) input += $" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}";
            if (gimmick.GimmickType is GimmickType.HiSpeedChange) input += $" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}";
            if (gimmick.GimmickType is GimmickType.TimeSigChange) input += $" {gimmick.TimeSig.Upper,4:F0} {gimmick.TimeSig.Lower,4:F0}";

            input += "\n";
            index++;
        }
        
        input += "\n";
    }

    internal static void WriteObjects(IEnumerable<Note> notes, ref string input)
    {
        int index = 0;

        // Objects
        input += "@OBJECTS\n";
        foreach (Note note in notes)
        {
            if (note.LinkType is NoteLinkType.Point or NoteLinkType.End) continue;

            if (note.LinkType is NoteLinkType.Start)
            {
                IEnumerable<Note> references = note.References();
                foreach (Note reference in references)
                {
                    input += $"{reference.BeatData.Measure,4:F0} {reference.BeatData.Tick,4:F0} {index,4:F0} {reference.Position,4:F0} {reference.Size,4:F0} {NoteType2String(reference.NoteType, reference.LinkType)}{Modifiers2String(reference)}\n";
                    index++;
                }
            }
            else
            {
                input += $"{note.BeatData.Measure,4:F0} {note.BeatData.Tick,4:F0} {index,4:F0} {note.Position,4:F0} {note.Size,4:F0} {NoteType2String(note.NoteType, note.LinkType)}{Modifiers2String(note)}\n";
                index++;
            }
        }
    }

    private static void WriteCommentsWithGuid(Chart chart, ref string input)
    {
        // Skipped - Comments are not shared over network.
        input += "@COMMENTS\n\n";
    }
    
    private static void WriteGimmicksWithGuid(Chart chart, ref string input)
    {
        int index = 0;
        input += "@GIMMICKS\n";
        foreach (Gimmick gimmick in chart.Gimmicks)
        {
            input += $"{gimmick.Guid} {gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {index,4:F0} {GimmickType2String(gimmick.GimmickType),-13}";

            if (gimmick.GimmickType is GimmickType.BpmChange) input += $" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}";
            if (gimmick.GimmickType is GimmickType.HiSpeedChange) input += $" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}";
            if (gimmick.GimmickType is GimmickType.TimeSigChange) input += $" {gimmick.TimeSig.Upper,4:F0} {gimmick.TimeSig.Lower,4:F0}";

            input += "\n";
            index++;
        }
        
        input += "\n";
    }
    
    private static void WriteObjectsWithGuid(IEnumerable<Note> notes, ref string input)
    {
        int index = 0;

        // Objects
        input += "@OBJECTS\n";
        foreach (Note note in notes)
        {
            if (note.LinkType is NoteLinkType.Point or NoteLinkType.End) continue;

            if (note.LinkType is NoteLinkType.Start)
            {
                IEnumerable<Note> references = note.References();
                foreach (Note reference in references)
                {
                    input += $"{reference.Guid} {reference.BeatData.Measure,4:F0} {reference.BeatData.Tick,4:F0} {index,4:F0} {reference.Position,4:F0} {reference.Size,4:F0} {NoteType2String(reference.NoteType, reference.LinkType)}{Modifiers2String(reference)}\n";
                    index++;
                }
            }
            else
            {
                input += $"{note.Guid} {note.BeatData.Measure,4:F0} {note.BeatData.Tick,4:F0} {index,4:F0} {note.Position,4:F0} {note.Size,4:F0} {NoteType2String(note.NoteType, note.LinkType)}{Modifiers2String(note)}\n";
                index++;
            }
        }
    }
    
    // Helpers
    private static NoteType String2NoteType(string[] attributes)
    {
        if (attributes.Length == 0) return NoteType.None;

        return attributes[0] switch
        {
            "TOUCH" => NoteType.Touch,
            "SNAP_FW" => NoteType.SnapForward,
            "SNAP_BW" => NoteType.SnapBackward,
            "SLIDE_CW" => NoteType.SlideClockwise,
            "SLIDE_CCW" => NoteType.SlideCounterclockwise,
            "CHAIN" => NoteType.Chain,
            "HOLD_START" => NoteType.Hold,
            "HOLD_POINT" => NoteType.Hold,
            "HOLD_END" => NoteType.Hold,
            "MASK_ADD" => NoteType.MaskAdd,
            "MASK_SUB" => NoteType.MaskRemove,
            "TRACE_START" => NoteType.Trace,
            "TRACE_POINT" => NoteType.Trace,
            "TRACE_END" => NoteType.Trace,
            "DAMAGE" => NoteType.Damage,
                
            _ => NoteType.None,
        };
    }

    private static BonusType String2BonusType(string[] attributes)
    {
        if (attributes.Length < 2) return BonusType.None;

        return attributes[1] switch
        {
            "NORMAL" => BonusType.None,
            "BONUS" => BonusType.Bonus,
            "RNOTE" => BonusType.RNote,
            _ => BonusType.None,
        };
    }

    private static NoteLinkType String2NoteLinkType(string[] attributes)
    {
        if (attributes.Length == 0) return NoteLinkType.Unlinked;

        return attributes[0] switch
        {
            "HOLD_START" => NoteLinkType.Start,
            "HOLD_POINT" => NoteLinkType.Point,
            "HOLD_END" => NoteLinkType.End,
            "TRACE_START" => NoteLinkType.Start,
            "TRACE_POINT" => NoteLinkType.Point,
            "TRACE_END" => NoteLinkType.End,

            _ => NoteLinkType.Unlinked,
        };
    }
    
    private static MaskDirection String2MaskDirection(string[] attributes)
    {
        if (attributes.Length < 2) return MaskDirection.None;

        return attributes[1] switch
        {
            "CW" => MaskDirection.Clockwise,
            "CCW" => MaskDirection.Counterclockwise,
            "CENTER" => MaskDirection.Center,
            _ => MaskDirection.None,
        };
    }

    private static TraceColor String2TraceColor(string[] attributes)
    {
        if (attributes.Length < 2) return TraceColor.White;

        return attributes[1] switch
        {
            "RED" => TraceColor.Red,
            "ORANGE" => TraceColor.Orange,
            "YELLOW" => TraceColor.Yellow,
            "LIME" => TraceColor.Lime,
            "GREEN" => TraceColor.Green,
            "SKY" => TraceColor.Sky,
            "BLUE" => TraceColor.Blue,
            "VIOLET" => TraceColor.Violet,
            "PINK" => TraceColor.Pink,
            "BLACK" => TraceColor.Black,
            "WHITE" => TraceColor.White,
            _ => TraceColor.White,
        };
    }
    
    private static GimmickType String2GimmickType(string name)
    {
        return name switch
        {
            "BPM" => GimmickType.BpmChange,
            "TIMESIG" => GimmickType.TimeSigChange,
            "HISPEED" => GimmickType.HiSpeedChange,
            "REV_START" => GimmickType.ReverseEffectStart,
            "REV_END" => GimmickType.ReverseEffectEnd,
            "REV_ZONE_END" => GimmickType.ReverseNoteEnd,
            "STOP_START" => GimmickType.StopStart,
            "STOP_END" => GimmickType.StopEnd,
            "CHART_END" => GimmickType.EndOfChart,
            _ => GimmickType.None,
        };
    }
    
    private static string GimmickType2String(GimmickType gimmickType)
    {
        return gimmickType switch
        {
            GimmickType.None => "",
            GimmickType.BpmChange => "BPM",
            GimmickType.TimeSigChange => "TIMESIG",
            GimmickType.HiSpeedChange => "HISPEED",
            GimmickType.ReverseEffectStart => "REV_START",
            GimmickType.ReverseEffectEnd => "REV_END",
            GimmickType.ReverseNoteEnd => "REV_ZONE_END",
            GimmickType.StopStart => "STOP_START",
            GimmickType.StopEnd => "STOP_END",
            GimmickType.EndOfChart => "CHART_END",
            _ => "",
        };
    }
    
    private static string NoteType2String(NoteType noteType, NoteLinkType linkType)
    {
        return (noteType, linkType) switch
        {
            (NoteType.None, _) => "",
            (NoteType.Touch, _) => "TOUCH",
            (NoteType.SnapForward, _) => "SNAP_FW",
            (NoteType.SnapBackward, _) => "SNAP_BW",
            (NoteType.SlideClockwise, _) => "SLIDE_CW",
            (NoteType.SlideCounterclockwise, _) => "SLIDE_CCW",
            (NoteType.Hold, NoteLinkType.Start) => "HOLD_START",
            (NoteType.Hold, NoteLinkType.Point) => "HOLD_POINT",
            (NoteType.Hold, NoteLinkType.End) => "HOLD_END",
            (NoteType.MaskAdd, _) => "MASK_ADD",
            (NoteType.MaskRemove, _) => "MASK_SUB",
            (NoteType.Chain, _) => "CHAIN",
            (NoteType.Trace, NoteLinkType.Start) => "TRACE_START",
            (NoteType.Trace, NoteLinkType.Point) => "TRACE_POINT",
            (NoteType.Trace, NoteLinkType.End) => "TRACE_END",
            (NoteType.Damage, _) => "DAMAGE",
            
            _ => "",
        };
    }

    private static string Modifiers2String(Note note)
    {
        string modifiers = "";

        if (!note.IsSegment && !note.IsMask && note.NoteType is not (NoteType.Trace or NoteType.Damage))
        {
            modifiers += note.BonusType switch
            {
                BonusType.None => "",
                BonusType.Bonus => ".BONUS",
                BonusType.RNote => ".RNOTE",
                _ => "",
            };
        }

        if (note.NoteType == NoteType.Trace && note.LinkType == NoteLinkType.Start)
        {
            modifiers += note.Color switch
            {
                TraceColor.Red => ".RED",
                TraceColor.Orange => ".ORANGE",
                TraceColor.Yellow => ".YELLOW",
                TraceColor.Lime => ".LIME",
                TraceColor.Green => ".GREEN",
                TraceColor.Sky => ".SKY",
                TraceColor.Blue => ".BLUE",
                TraceColor.Violet => ".VIOLET",
                TraceColor.Pink => ".PINK",
                TraceColor.Black => ".BLACK",
                TraceColor.White => ".WHITE",
                _ => ".WHITE",
            };
        }
        
        if (note.IsMask)
            modifiers += note.MaskDirection switch
            {
                MaskDirection.Center => ".CENTER",
                MaskDirection.Clockwise => ".CW",
                MaskDirection.Counterclockwise => ".CCW",
                MaskDirection.None => "",
                _ => "",
            };
        
        if (note.IsNoteCollection && note.LinkType == NoteLinkType.Point && !note.RenderSegment) modifiers += ".NR";

        return modifiers;
    }
}