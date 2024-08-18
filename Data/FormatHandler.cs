using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MercuryMapper.Enums;

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

            // Naively detect .SAT format
            if (data[0].Contains("@SAT_VERSION"))
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
}

internal static class MerHandler
{
    /// <summary>
    /// Parses a MER format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
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
            chart.IsNew = true;
        }

        return;

        void parseMetadata()
        {
            foreach (string line in metadata)
            {
                string result;

                if (containsTag(line, "#EDITOR_AUDIO", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (containsTag(line, "#EDITOR_AUTHOR", out result)) chart.Author = result;
                if (containsTag(line, "#EDITOR_LEVEL", out result)) chart.Level = Convert.ToDecimal(result);
                if (containsTag(line, "#EDITOR_CLEAR_THRESHOLD", out result)) chart.ClearThreshold = Convert.ToDecimal(result);
                if (containsTag(line, "#EDITOR_PREVIEW_TIME", out result)) chart.PreviewStart = Convert.ToDecimal(result);
                if (containsTag(line, "#EDITOR_PREVIEW_LENGTH", out result)) chart.PreviewTime = Convert.ToDecimal(result);
                if (containsTag(line, "#EDITOR_OFFSET", out result)) chart.BgmOffset = Convert.ToDecimal(result);
                if (containsTag(line, "#EDITOR_MOVIEOFFSET", out result)) chart.BgaOffset = Convert.ToDecimal(result);

                if (containsTag(line, "#AUDIO", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (containsTag(line, "#AUTHOR", out result)) chart.Author = result;
                if (containsTag(line, "#LEVEL", out result)) chart.Level = Convert.ToDecimal(result);
                if (containsTag(line, "#CLEAR_THRESHOLD", out result)) chart.ClearThreshold = Convert.ToDecimal(result);
                if (containsTag(line, "#PREVIEW_TIME", out result)) chart.PreviewStart = Convert.ToDecimal(result);
                if (containsTag(line, "#PREVIEW_LENGTH", out result)) chart.PreviewTime = Convert.ToDecimal(result);
                if (containsTag(line, "#OFFSET", out result)) chart.BgmOffset = Convert.ToDecimal(result);
                if (containsTag(line, "#MOVIEOFFSET", out result)) chart.BgaOffset = Convert.ToDecimal(result);
            }
        }

        void parseContent()
        {
            foreach (string line in content)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 3) continue;

                int measure = Convert.ToInt32(split[0]);
                int tick = Convert.ToInt32(split[1]);
                int objectId = Convert.ToInt32(split[2]);

                // Invalid
                if (objectId is < 1 or > 10) continue;

                // Notes
                if (objectId == 1)
                {
                    int noteTypeId = Convert.ToInt32(split[3]);
                    int noteIndex = Convert.ToInt32(split[4]);
                    int position = Convert.ToInt32(split[5]);
                    int size = Convert.ToInt32(split[6]);
                    bool renderSegment = noteTypeId != 10 || Convert.ToBoolean(Convert.ToInt32(split[7])); // Set to true by default if note is not a hold segment.

                    Note newNote = new(measure, tick, noteTypeId, noteIndex, position, size, renderSegment);

                    // hold start & segments
                    if (noteTypeId is 9 or 10 or 25 && split.Length >= 9)
                    {
                        nextReferencedIndex[noteIndex] = Convert.ToInt32(split[8]);
                    }

                    if (noteTypeId is 12 or 13 && split.Length >= 9)
                    {
                        int direction = Convert.ToInt32(split[8]);
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

                    Gimmick newGimmick = new(measure, tick, objectId, value1, value2);
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

        bool containsTag(string input, string tag, out string result)
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
                        $"#MUSIC_FILE_PATH {Path.GetFileName(chart.BgmFilepath)}\n" +
                        $"#OFFSET {chart.BgmOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" +
                        $"MOVIEOFFSET {chart.BgaOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" +
                        $"#BODY\n";

        foreach (Gimmick gimmick in chart.Gimmicks)
        {
            result += $"{gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {(int)gimmick.GimmickType,4:F0}";
            result += gimmick.GimmickType switch
            {
                GimmickType.BpmChange => $" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}\n",
                GimmickType.HiSpeedChange => $" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}\n",
                GimmickType.TimeSigChange => $" {gimmick.TimeSig.Upper,5:F0} {gimmick.TimeSig.Lower,5:F0}\n",
                _ => "\n"
            };
        }
        
        foreach (Note note in chart.Notes)
        {
            result += $"{note.BeatData.Measure,4:F0} " +
                      $"{note.BeatData.Tick,4:F0} " +
                      $"{(int)note.GimmickType,4:F0} " +
                      $"{(int)note.NoteType} " +
                      $"{chart.Notes.IndexOf(note),4:F0} " +
                      $"{note.Position,4:F0} " +
                      $"{note.Size,4:F0} " +
                      $"{Convert.ToInt32(note.RenderSegment, CultureInfo.InvariantCulture),4:F0}";

            if (note.IsMask) result += $" {(int)note.MaskDirection,4:F0}";
            if (note.NextReferencedNote != null) result += $" {chart.Notes.IndexOf(note.NextReferencedNote),4:F0}";

            result += "\n";
        }
        
        File.WriteAllTextAsync(filepath, result);
    }
}

internal static class SatHandler
{
    private const string SatFormatVersion = "1";
    
    /// <summary>
    /// Parses a SAT format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
    /// <param name="filepath">Absolute filepath of the opened file.</param>
    /// <param name="data">Chart Data, split into individual lines.</param>
    public static void LoadFile(Chart chart, string filepath, string[] data)
    {
        
    }

    /// <summary>
    /// Writes a SAT format file.
    /// </summary>
    /// <param name="chart">Chart Instance to load new file into.</param>
    /// <param name="filepath">Absolute filepath to chart file.</param>
    public static void WriteFile(Chart chart, string filepath)
    {
        if (filepath == "") return;
        
        int index = 0;
        string result = $"{"@SAT_VERSION",-16}{SatFormatVersion}\n" +
                        $"\n" +
                        $"{"@VERSION",-16}{chart.Version}\n" +
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
                        $"{"@BGM", -16}{Path.GetFileName(chart.BgmFilepath)}\n" +
                        $"{"@BGM_OFFSET", -16}{chart.BgmOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" +
                        $"{"@BGA", -16}{Path.GetFileName(chart.BgaFilepath)}\n" +
                        $"{"@BGA_OFFSET", -16}{chart.BgaOffset.ToString("F6", CultureInfo.InvariantCulture)}\n" +
                        $"{"@JACKET", -16}{chart.JacketFilepath}\n" +
                        $"\n";

        // TODO: COMMENTS
        result += "@COMMENTS\n\n";
        
        // Gimmicks
        result += "@GIMMICKS\n";
        foreach (Gimmick gimmick in chart.Gimmicks)
        {
            result += $"{gimmick.BeatData.Measure,4:F0} {gimmick.BeatData.Tick,4:F0} {index,4:F0} {getGimmickName(gimmick.GimmickType),-13}";

            if (gimmick.GimmickType is GimmickType.BpmChange) result += $" {gimmick.Bpm.ToString("F6", CultureInfo.InvariantCulture)}";
            if (gimmick.GimmickType is GimmickType.HiSpeedChange) result += $" {gimmick.HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}";
            if (gimmick.GimmickType is GimmickType.TimeSigChange) result += $" {gimmick.TimeSig.Upper,4:F0} {gimmick.TimeSig.Lower,4:F0}";

            result += "\n";
            index++;
        }
        
        if (chart.EndOfChart != null) result += $"{chart.EndOfChart.BeatData.Measure,4:F0} {chart.EndOfChart.BeatData.Tick,4:F0} {index,4:F0} {getNoteName(chart.EndOfChart)}\n";

        result += "\n";
        index = 0;
        
        // Objects
        result += "@OBJECTS\n";
        foreach (Note note in chart.Notes)
        {
            if (note.NoteType is NoteType.HoldSegment or NoteType.HoldEnd or NoteType.EndOfChart) continue;
            
            if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                IEnumerable<Note> references = note.References();
                foreach (Note reference in references)
                {
                    result += $"{reference.BeatData.Measure,4:F0} {reference.BeatData.Tick,4:F0} {index,4:F0} {reference.Position,4:F0} {reference.Size,4:F0} {getNoteName(reference)}\n";
                    index++;
                }
            }
            else
            {
                result += $"{note.BeatData.Measure,4:F0} {note.BeatData.Tick,4:F0} {index,4:F0} {note.Position,4:F0} {note.Size,4:F0} {getNoteName(note)}\n";
                index++;
            }
        }
        
        File.WriteAllTextAsync(filepath, result);
        return;

        string getNoteName(Note note)
        {
            return note.NoteType switch
            {
                NoteType.None => "",
                NoteType.Touch => "TOUCH",
                NoteType.TouchBonus => "TOUCH.BONUS",
                NoteType.SnapForward => "SNAP_FW",
                NoteType.SnapBackward => "SNAP_BW",
                NoteType.SlideClockwise => "SLIDE_CW",
                NoteType.SlideClockwiseBonus => "SLIDE_CW.BONUS",
                NoteType.SlideCounterclockwise => "SLIDE_CCW",
                NoteType.SlideCounterclockwiseBonus => "SLIDE_CCW.BONUS",
                NoteType.HoldStart => "HOLD_START",
                NoteType.HoldSegment => "HOLD_POINT",
                NoteType.HoldEnd => "HOLD_END",
                NoteType.MaskAdd => note.MaskDirection switch
                {
                    MaskDirection.Clockwise => "MASK_ADD.CW",
                    MaskDirection.Counterclockwise => "MASK_ADD.CCW",
                    MaskDirection.Center => "MASK_ADD.CENTER",
                    _ => ""
                },
                NoteType.MaskRemove => note.MaskDirection switch
                {
                    MaskDirection.Clockwise => "MASK_SUB.CW",
                    MaskDirection.Counterclockwise => "MASK_SUB.CCW",
                    MaskDirection.Center => "MASK_SUB.CENTER",
                    _ => ""
                },
                NoteType.EndOfChart => "CHART_END",
                NoteType.Chain => "CHAIN",
                NoteType.TouchRNote => "TOUCH.RNOTE",
                NoteType.SnapForwardRNote => "SNAP_FW.RNOTE",
                NoteType.SnapBackwardRNote => "SNAP_BW.RNOTE",
                NoteType.SlideClockwiseRNote => "SLIDE_CW.RNOTE",
                NoteType.SlideCounterclockwiseRNote => "SLIDE_CCW.RNOTE",
                NoteType.HoldStartRNote => "HOLD_START.RNOTE",
                NoteType.ChainRNote => "CHAIN.RNOTE",
                _ => ""
            };
        }
        
        string getGimmickName(GimmickType gimmickType)
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
                _ => ""
            };
        }
    }
}