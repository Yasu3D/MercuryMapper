using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FluentAvalonia.Core;
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
            chart.IsNew = true;
        }

        return;

        void parseMetadata()
        {
            foreach (string line in metadata)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string result;

                if (FormatHandler.ContainsTag(line, "#EDITOR_AUDIO", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_AUTHOR", out result)) chart.Author = result;
                if (FormatHandler.ContainsTag(line, "#EDITOR_LEVEL", out result)) chart.Level = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_CLEAR_THRESHOLD", out result)) chart.ClearThreshold = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_PREVIEW_TIME", out result)) chart.PreviewStart = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_PREVIEW_LENGTH", out result)) chart.PreviewTime = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_OFFSET", out result)) chart.BgmOffset = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#EDITOR_MOVIEOFFSET", out result)) chart.BgaOffset = Convert.ToDecimal(result);

                if (FormatHandler.ContainsTag(line, "#AUDIO", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (FormatHandler.ContainsTag(line, "#AUTHOR", out result)) chart.Author = result;
                if (FormatHandler.ContainsTag(line, "#LEVEL", out result)) chart.Level = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#CLEAR_THRESHOLD", out result)) chart.ClearThreshold = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#PREVIEW_TIME", out result)) chart.PreviewStart = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#PREVIEW_LENGTH", out result)) chart.PreviewTime = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#OFFSET", out result)) chart.BgmOffset = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "#MOVIEOFFSET", out result)) chart.BgaOffset = Convert.ToDecimal(result);
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

                    Note newNote = new(measure, tick, (NoteType)noteTypeId, noteIndex, position, size, renderSegment);

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
        int commentIndex = data.IndexOf("@COMMENTS");
        int gimmickIndex = data.IndexOf("@GIMMICKS");
        int objectIndex = data.IndexOf("@OBJECTS");

        string[] metadata = data[1..commentIndex];
        string[] comments = data[(commentIndex + 1)..gimmickIndex];
        string[] gimmicks = data[(gimmickIndex + 1)..objectIndex];
        string[] objects = data[(objectIndex + 1)..];
        
        lock (chart)
        {
            chart.Clear();
            chart.Filepath = filepath;

            parseMetadata();
            parseComments();
            parseGimmicks();
            parseObjects();
            
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
                if (string.IsNullOrWhiteSpace(line)) continue;
                string result;
                
                if (FormatHandler.ContainsTag(line, "@VERSION", out result)) chart.Version = result;
                if (FormatHandler.ContainsTag(line, "@TITLE", out result)) chart.Title = result;
                if (FormatHandler.ContainsTag(line, "@RUBI", out result)) chart.Rubi = result;
                if (FormatHandler.ContainsTag(line, "@ARTIST", out result)) chart.Artist = result;
                if (FormatHandler.ContainsTag(line, "@AUTHOR", out result)) chart.Author = result;
                
                if (FormatHandler.ContainsTag(line, "@DIFF", out result)) chart.Diff = Convert.ToInt32(result);
                if (FormatHandler.ContainsTag(line, "@LEVEL", out result)) chart.Level = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "@CLEAR", out result)) chart.ClearThreshold = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "@BPM_TEXT", out result)) chart.BpmText = result;
                
                if (FormatHandler.ContainsTag(line, "@PREVIEW_START", out result)) chart.PreviewStart = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "@PREVIEW_TIME", out result)) chart.PreviewTime = Convert.ToDecimal(result);
                
                if (FormatHandler.ContainsTag(line, "@BGM", out result)) chart.BgmFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (FormatHandler.ContainsTag(line, "@BGM_OFFSET", out result)) chart.BgmOffset = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "@BGA", out result)) chart.BgaFilepath = Path.Combine(Path.GetDirectoryName(chart.Filepath) ?? "", result);
                if (FormatHandler.ContainsTag(line, "@BGA_OFFSET", out result)) chart.BgaOffset = Convert.ToDecimal(result);
                if (FormatHandler.ContainsTag(line, "@JACKET", out result)) chart.JacketFilepath = result;
            }
        }
        
        void parseComments()
        {
            // TODO: COMMENTS
        }
        
        void parseGimmicks()
        {
            foreach (string line in gimmicks)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 4) continue;

                int measure = Convert.ToInt32(split[0]);
                int tick = Convert.ToInt32(split[1]);
                
                // slightly jank case since Chart End is a Note
                // internally but grouped with gimmicks for SAT
                if (split[3] == "CHART_END")
                {
                    Note note = new(measure, tick, NoteType.EndOfChart, 0, 0, 60, true);
                    chart.Notes.Add(note);
                    continue;
                }
                
                GimmickType gimmickType = getGimmickType(split[3]);
                
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
        
        void parseObjects()
        {
            Note? previousNote = null;
            
            foreach (string line in objects)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] split = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (split.Length < 6) continue;
                
                int measure = Convert.ToInt32(split[0]);
                int tick = Convert.ToInt32(split[1]);
                int index = Convert.ToInt32(split[2]);
                int position = Convert.ToInt32(split[3]);
                int size = Convert.ToInt32(split[4]);
                
                string[] attributes = split[5].Split('.',StringSplitOptions.RemoveEmptyEntries);
                
                NoteType noteType = getNoteType(attributes);
                BonusType bonusType = getBonusType(attributes);
                MaskDirection maskDirection = getMaskDirection(attributes);
                bool renderSegment = attributes is [_, not "NR"];

                NoteType combinedType = combineTypes(noteType, bonusType);
                
                Note note = new(measure, tick, combinedType, index, position, size, renderSegment);

                if (noteType is NoteType.HoldSegment or NoteType.HoldEnd)
                {
                    note.PrevReferencedNote = previousNote;
                    if (previousNote != null) previousNote.NextReferencedNote = note;
                }

                chart.Notes.Add(note);
                previousNote = note;
            }
        }
        
        NoteType getNoteType(string[] attributes)
        {
            if (attributes.Length == 0) return NoteType.None;
            
            return attributes[0] switch
            {
                "TOUCH" =>  NoteType.Touch,
                "SNAP_FW" =>  NoteType.SnapForward,
                "SNAP_BW" =>  NoteType.SnapBackward,
                "SLIDE_CW" =>  NoteType.SlideClockwise,
                "SLIDE_CCW" =>  NoteType.SlideCounterclockwise,
                "CHAIN" =>  NoteType.Chain,
                "HOLD_START" =>  NoteType.HoldStart,
                "HOLD_POINT" => NoteType.HoldSegment,
                "HOLD_POINT.NR" => NoteType.HoldSegment,
                "HOLD_END" => NoteType.HoldEnd,
                "MASK_ADD" => NoteType.MaskAdd,
                "MASK_SUB" => NoteType.MaskRemove,
                "CHART_END" =>  NoteType.EndOfChart,
                
                _ => NoteType.None
            };
        }

        BonusType getBonusType(string[] attributes)
        {
            if (attributes.Length < 2) return BonusType.None;

            return attributes[1] switch
            {
                "NORMAL" => BonusType.None,
                "BONUS" => BonusType.Bonus,
                "RNOTE" => BonusType.RNote,
                _ => BonusType.None
            };
        }

        MaskDirection getMaskDirection(string[] attributes)
        {
            if (attributes.Length < 2) return MaskDirection.None;

            return attributes[1] switch
            {
                "CW" => MaskDirection.Clockwise,
                "CCW" => MaskDirection.Counterclockwise,
                "CENTER" => MaskDirection.Center,
                _ => MaskDirection.None
            };
        }
        
        GimmickType getGimmickType(string name)
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
                _ => GimmickType.None
            };
        }

        // This sucks. TODO: REWORK NOTETYPE AND BONUSTYPE SYSTEM
        NoteType combineTypes(NoteType noteType, BonusType bonusType)
        {
            return noteType switch
            {
                NoteType.Touch => bonusType switch
                {
                    BonusType.None => NoteType.Touch,
                    BonusType.Bonus => NoteType.TouchBonus,
                    BonusType.RNote => NoteType.TouchRNote,
                    _ => NoteType.Touch
                },
                NoteType.SnapForward => bonusType switch
                {
                    BonusType.None => NoteType.SnapForward,
                    BonusType.Bonus => NoteType.SnapForward,
                    BonusType.RNote => NoteType.SnapForwardRNote,
                    _ => NoteType.SnapForward
                },
                NoteType.SnapBackward => bonusType switch
                {
                    BonusType.None => NoteType.SnapBackward,
                    BonusType.Bonus => NoteType.SnapBackward,
                    BonusType.RNote => NoteType.SnapBackwardRNote,
                    _ => NoteType.SnapBackward
                },
                NoteType.SlideClockwise => bonusType switch
                {
                    BonusType.None => NoteType.SlideClockwise,
                    BonusType.Bonus => NoteType.SlideClockwiseBonus,
                    BonusType.RNote => NoteType.SlideClockwiseRNote,
                    _ => NoteType.SlideClockwise
                },
                NoteType.SlideCounterclockwise => bonusType switch
                {
                    BonusType.None => NoteType.SlideCounterclockwise,
                    BonusType.Bonus => NoteType.SlideCounterclockwiseBonus,
                    BonusType.RNote => NoteType.SlideCounterclockwiseRNote,
                    _ => NoteType.SlideCounterclockwise
                },
                NoteType.Chain => bonusType switch
                {
                    BonusType.None => NoteType.Chain,
                    BonusType.Bonus => NoteType.Chain,
                    BonusType.RNote => NoteType.ChainRNote,
                    _ => NoteType.Chain
                },
                NoteType.HoldStart => bonusType switch
                {
                    BonusType.None => NoteType.HoldStart,
                    BonusType.Bonus => NoteType.HoldStart,
                    BonusType.RNote => NoteType.HoldStartRNote,
                    _ => NoteType.HoldStart
                },
                NoteType.HoldSegment => bonusType switch
                {
                    BonusType.None => NoteType.HoldSegment,
                    BonusType.Bonus => NoteType.HoldSegment,
                    BonusType.RNote => NoteType.HoldSegment,
                    _ => NoteType.HoldSegment
                },
                NoteType.HoldEnd => bonusType switch
                {
                    BonusType.None => NoteType.HoldEnd,
                    BonusType.Bonus => NoteType.HoldEnd,
                    BonusType.RNote => NoteType.HoldEnd,
                    _ => NoteType.HoldEnd
                },
                NoteType.EndOfChart => bonusType switch
                {
                    BonusType.None => NoteType.EndOfChart,
                    BonusType.Bonus => NoteType.EndOfChart,
                    BonusType.RNote => NoteType.EndOfChart,
                    _ => NoteType.EndOfChart
                },
                _ => noteType
            };
        }
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
                NoteType.HoldSegment => note.RenderSegment ? "HOLD_POINT" : "HOLD_POINT.NR",
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