using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.Utils;

namespace MercuryMapper.Editor;

public static class Proofreader
{
    private enum MessageType
    {
        None = 0,
        Suggestion = 1,
        Warning = 2,
        Error = 3,
        Debug = 4,
    }
    
    private static void AddMessage(SelectableTextBlock textBlock, MessageType messageType, string message)
    {
        if (textBlock.Inlines is null) return;

        switch (messageType)
        {
            case MessageType.Suggestion:
                Dispatcher.UIThread.Invoke(() => textBlock.Inlines.Add(new Run("SUGGESTION:  ") { FontWeight = FontWeight.Bold, Foreground = Brushes.Turquoise }));
                break;

            case MessageType.Warning:
                Dispatcher.UIThread.Invoke(() => textBlock.Inlines.Add(new Run("WARNING:  ") { FontWeight = FontWeight.Bold, Foreground = Brushes.Orange }));
                break;

            case MessageType.Error:
                Dispatcher.UIThread.Invoke(() => textBlock.Inlines.Add(new Run("ERROR:  ") { FontWeight = FontWeight.Bold, Foreground = Brushes.Red }));
                break;

            case MessageType.Debug:
                Dispatcher.UIThread.Invoke(() => textBlock.Inlines.Add(new Run("DEBUG:  ") { FontWeight = FontWeight.Bold, Foreground = Brushes.Blue }));
                break;
        }

        Dispatcher.UIThread.Invoke(() => textBlock.Inlines.Add(message));
    }
    
    public static void Proofread(SelectableTextBlock textBlock, Chart chart, bool limitToMercuryBonusTypes)
    {
        checkEndOfChart();
        checkNotesAfterEndOfChart();
        checkNotesBeforeEndOfChart();
        checkDelayedEndOfChart();
        checkSmallNotes();
        checkSmallerThanLegalNotes();
        checkLargerThanLegalNotes();
        if (limitToMercuryBonusTypes) checkBonusNotes();
        checkBrokenNotes();
        checkUnbakedHolds();
        checkFullyOverlappingNotes();
        checkPartiallyOverlappingNotes();
        checkGimmicksOnLowers();
        watchYourProfanity();
        
        return;

        void checkEndOfChart()
        {
            if (chart.EndOfChart == null)
            {
                AddMessage(textBlock, MessageType.Error, "End of Chart Note missing.\n");
                AddMessage(textBlock, MessageType.None, "Place an End of Chart note at least a Measure after the last playable note. Without it, the game would crash.\n\n");
            }

            Gimmick[] endOfChartGimmicks = chart.Gimmicks.Where(x => x.GimmickType is GimmickType.EndOfChart).ToArray();
            
            if (endOfChartGimmicks.Length > 1)
            {
                for (int i = 0; i < endOfChartGimmicks.Length - 1; i++)
                {
                    AddMessage(textBlock, MessageType.Error, $"End of Chart Note @ {endOfChartGimmicks[i].BeatData.Measure} {endOfChartGimmicks[i].BeatData.Tick} is redundant.\n");
                }
                
                AddMessage(textBlock, MessageType.None, "A Chart with more than one End of Chart Note could cause undefined behavior and crash the game.\n\n");
            }
        }

        void checkNotesAfterEndOfChart()
        {
            if (chart.EndOfChart == null) return;
            
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.BeatData.FullTick < chart.EndOfChart.BeatData.FullTick) continue;
                
                AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is after the End of Chart Note.\n");
                error = true;
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Notes should never come after the End of Chart Note, as this causes undefined behavior.\n\n");
            }
        }

        void checkDelayedEndOfChart()
        {
            if (chart.EndOfChart == null) return;

            int lastNote = chart.Notes.LastOrDefault(x => x.IsNote)?.BeatData.FullTick ?? 0;
            int endOfChart = chart.EndOfChart.BeatData.FullTick;
            
            if (int.Abs(endOfChart - lastNote) > 7680)
            {
                AddMessage(textBlock, MessageType.Warning, $"End of Chart @ {chart.EndOfChart.BeatData.Measure} {chart.EndOfChart.BeatData.Tick} comes > 4 Measures after the last Note.\n");
                AddMessage(textBlock, MessageType.None, "The End of Chart Note should come ~1-4 measures after the last Note. Enough time to allow the Navigator to say ALL MARVELOUS!, but not so long that the player has to wait.\n\n");
            }
        }
        
        void checkNotesBeforeEndOfChart()
        {
            if (chart.EndOfChart == null) return;

            Note? lastNote = chart.Notes.LastOrDefault(x => x.IsNote && x.BeatData.FullTick < chart.EndOfChart.BeatData.FullTick);
            
            if (lastNote != null && int.Abs(lastNote.BeatData.FullTick - chart.EndOfChart.BeatData.FullTick) < 1920)
            {
                AddMessage(textBlock, MessageType.Warning, $"End of Chart Note is too early.\n");
                AddMessage(textBlock, MessageType.None, "There should be enough time for the navigator to say \"ALL MARVELOUS!\", \"FULL COMBO!\", or \"MISSLESS!\" after the last Note is hit before \"CLEAR\" or \"FAIL\" shows up.\n\n");
            }
        }

        void checkSmallNotes()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.IsMask || note.IsSegment || note.NoteType == NoteType.Trace) continue;

                if (note.Size < 10)
                {
                    AddMessage(textBlock, MessageType.Suggestion, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is small (< 10).\n");
                    error = true;
                }
            }

            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Notes smaller than size 10 are difficult to read and feel like swatting flies. Only use them when you're 100% sure you know what you're doing.\n\n");
            }
        }
        
        void checkSmallerThanLegalNotes()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.Size < Note.MinSize(note.NoteType, note.BonusType, note.LinkType))
                {
                    AddMessage(textBlock, MessageType.Warning, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is smaller than min size [{note.Size} < {Note.MinSize(note.NoteType, note.BonusType, note.LinkType)}.\n");
                    error = true;
                }
            }

            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Notes smaller than their intended minimum size may look broken or feel unplayable.\n\n");
            }
        }

        void checkLargerThanLegalNotes()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.Size > Note.MaxSize(note.NoteType))
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is larger than max size [{note.Size} > {Note.MaxSize(note.NoteType)}.\n");
                    error = true;
                }
            }

            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Notes larger than their intended maximum size may look broken or crash the game.\n\n");
            }
        }

        void checkBonusNotes()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.BonusType is BonusType.Bonus && note.NoteType is not (NoteType.Touch or NoteType.SlideClockwise or NoteType.SlideCounterclockwise))
                {
                    AddMessage(textBlock, MessageType.Warning, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is unsupported in Mercury..\n");
                    error = true;
                }
            }

            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Notes smaller than their intended minimum size may look broken or feel unplayable.\n\n");
            }
        }
        
        void checkBrokenNotes()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.Size is < 1 or > 60)
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has a broken size of {note.Size}.\n");
                    error = true;
                }

                if (note.Position is < 0 or > 59)
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has a broken position of {note.Position}.\n");
                    error = true;
                }
            }

            foreach (Note note in chart.Notes)
            {
                if (note.IsNoteCollection && note.LinkType == NoteLinkType.Unlinked)
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has no references to other Notes.\n");
                    error = true;
                }
            }

            foreach (Note note in chart.Notes)
            {
                if (note.NoteType is NoteType.MaskAdd or NoteType.MaskRemove && note.MaskDirection is MaskDirection.None)
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has no set direction.\n");
                    error = true;
                }
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Broken notes can crash the game or break visuals.\n\n");
            }
        }

        void checkFullyOverlappingNotes()
        {
            bool error = false;

            HashSet<int> checkedTicks = [];

            foreach (Note note in chart.Notes)
            {
                if (!checkedTicks.Add(note.BeatData.FullTick)) continue;
                Note[] notesOnTick = chart.Notes.Where(x => x.BeatData.FullTick == note.BeatData.FullTick).ToArray();
                
                for (int i = 0; i < notesOnTick.Length - 1; i++)
                for (int j = i + 1; j < notesOnTick.Length; j++)
                {
                    Note current = chart.Notes[i];
                    Note next = chart.Notes[j];
                
                    if (current.IsMask || current.NoteType is NoteType.Hold or NoteType.Trace) continue;
                    if (next.IsMask || next.NoteType is NoteType.Hold or NoteType.Trace) continue;
                    if (current.BeatData.FullTick != next.BeatData.FullTick) continue;
                
                    if (MathExtensions.IsFullyOverlapping(current.Position, current.Position + current.Size, next.Position, next.Position + next.Size))
                    {
                        AddMessage(textBlock, MessageType.Error, $"{current.NoteType} @ {current.BeatData.Measure} {current.BeatData.Tick} is fully overlapping with {next.NoteType}.\n");
                        error = true;
                    }
                }
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Fully overlapping Notes may break the judgement engine, and are generally unfair to players.\n\n");
            }
        }

        void checkPartiallyOverlappingNotes()
        {
            bool error = false;
            
            HashSet<int> checkedTicks = [];

            foreach (Note note in chart.Notes)
            {
                if (!checkedTicks.Add(note.BeatData.FullTick)) continue;
                Note[] notesOnTick = chart.Notes.Where(x => x.BeatData.FullTick == note.BeatData.FullTick).ToArray();
                
                for (int i = 0; i < notesOnTick.Length - 1; i++)
                for (int j = i + 1; j < notesOnTick.Length; j++)
                {
                    Note current = notesOnTick[i];
                    Note next = notesOnTick[j];
                    
                    if (current.IsMask || current.NoteType is NoteType.Hold or NoteType.Trace) continue;
                    if (next.IsMask || next.NoteType is NoteType.Hold or NoteType.Trace) continue;
                    if (current.BeatData.FullTick != next.BeatData.FullTick) continue;
                
                    if (MathExtensions.IsPartiallyOverlapping(current.Position, current.Position + current.Size, next.Position, next.Position + next.Size))
                    {
                        AddMessage(textBlock, MessageType.Warning, $"{current.NoteType} @ {current.BeatData.Measure} {current.BeatData.Tick} is partially overlapping with {next.NoteType}.\n");
                        error = true;
                    }
                }
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Partially overlapping Notes may break the judgement engine, and may be unfair to players.\n\n");
            }
        }

        void checkUnbakedHolds()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.NoteType is not NoteType.Hold) continue;
                if (note.NextReferencedNote == null) continue;

                int a = note.Position;
                int b = note.NextReferencedNote.Position;
                int c = note.Position + note.Size;
                int d = note.NextReferencedNote.Position + note.NextReferencedNote.Size;

                int diff0 = int.Abs(a - b);
                int diff1 = int.Abs(c - d);

                if (diff0 > 30) diff0 = 60 - diff0;
                if (diff1 > 30) diff1 = 60 - diff1;

                if (diff0 > 1 || diff1 > 1)
                {
                    AddMessage(textBlock, MessageType.Warning, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is part of an unbaked Hold Note. {a - b} {c - d}\n");
                    error = true;
                }
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Unbaked Holds have broken judgement zones that may result in the player dropping a Hold even though they were following visuals properly.\n\n");
            }
        }

        void checkGimmicksOnLowers()
        {
            if (chart.Diff > 1) return;
            
            bool error = false;
            
            foreach (Gimmick gimmick in chart.Gimmicks)
            {
                if (gimmick.GimmickType is GimmickType.BpmChange or GimmickType.TimeSigChange or GimmickType.EndOfChart) continue;

                AddMessage(textBlock, MessageType.Warning, $"{gimmick.GimmickType} @ {gimmick.BeatData.Measure} {gimmick.BeatData.Tick} is likely inappropriate for lowers.\n");
                error = true;
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Lowers tend to be for absolute beginners at the game. It's already hard enough for them to get the timing right on notes that are scrolling at their normal speed. Playing with scroll speeds and reverses SIGNIFICANTLY increases difficulty.\n\n");
            }
        }

        void watchYourProfanity()
        {
            foreach (KeyValuePair<string, Comment> comment in chart.Comments)
            {
                if (comment.Value.Text.Contains("FUCK", StringComparison.InvariantCultureIgnoreCase))
                {
                    AddMessage(textBlock, MessageType.Suggestion, $"Watch your profanity. Comment @ {comment.Value.BeatData.Measure} {comment.Value.BeatData.Tick}\n");
                }
            }
        }
    }
}