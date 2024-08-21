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
        Debug = 4
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
    
    public static void Proofread(SelectableTextBlock textBlock, Chart chart)
    {
        checkEndOfChart();
        checkNotesAfterEndOfChart();
        checkNotesBeforeEndOfChart();
        checkSmallNotes();
        checkSmallerThanLegalNotes();
        checkBrokenNotes();
        checkUnbakedHolds();
        checkFullyOverlappingNotes();
        watchYourProfanity();
        
        return;

        void checkEndOfChart()
        {
            if (chart.EndOfChart == null)
            {
                AddMessage(textBlock, MessageType.Error, "End of Chart Note missing.\n");
                AddMessage(textBlock, MessageType.None, "Place an End of Chart note at least a Measure after the last playable note. Without it, the game would crash.\n\n");
            }

            Note[] endOfChartNotes = chart.Notes.Where(x => x.NoteType is NoteType.EndOfChart).ToArray();
            
            if (endOfChartNotes.Length > 1)
            {
                for (int i = 0; i < endOfChartNotes.Length - 1; i++)
                {
                    AddMessage(textBlock, MessageType.Error, $"End of Chart Note @ {endOfChartNotes[i].BeatData.Measure} {endOfChartNotes[i].BeatData.Tick} is redundant.\n");
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
                if (note.NoteType is NoteType.EndOfChart) continue;
                
                AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is after the End of Chart Note.\n");
                error = true;
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Notes should never come after the End of Chart Note, as this causes undefined behavior.\n\n");
            }
        }

        void checkNotesBeforeEndOfChart()
        {
            if (chart.EndOfChart == null) return;

            Note? lastNote = chart.Notes.LastOrDefault(x => x.BeatData.FullTick < chart.EndOfChart.BeatData.FullTick);
            
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
                if (note.NoteType is NoteType.HoldSegment or NoteType.HoldEnd or NoteType.MaskAdd or NoteType.MaskRemove) continue;

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
                if (note.Size < Note.MinSize(note.NoteType, note.BonusType))
                {
                    AddMessage(textBlock, MessageType.Warning, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} is smaller than it's legal minimum size [< {Note.MinSize(note.NoteType, note.BonusType)}.\n");
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
                if (note is { NoteType: NoteType.HoldStart, NextReferencedNote: null })
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has no next referenced Note.\n");
                    error = true;
                }

                if (note is { NoteType: NoteType.HoldSegment, NextReferencedNote: null })
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has no next referenced Note.\n");
                    error = true;
                }

                if (note is { NoteType: NoteType.HoldSegment, PrevReferencedNote: null })
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has no previous referenced Note.\n");
                    error = true;
                }

                if (note is { NoteType: NoteType.HoldEnd, PrevReferencedNote: null })
                {
                    AddMessage(textBlock, MessageType.Error, $"{note.NoteType} @ {note.BeatData.Measure} {note.BeatData.Tick} has no previous referenced Note.\n");
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
            
            for (int i = 0; i < chart.Notes.Count - 1; i++)
            {
                Note current = chart.Notes[i];
                Note next = chart.Notes[i + 1];
                
                if (current.IsHold || current.IsMask || next.IsHold || next.IsMask) continue;
                if (current.BeatData.FullTick != next.BeatData.FullTick) continue;
                
                if (MathExtensions.IsOverlapping(current.Position, current.Position + current.Size, next.Position, next.Position + next.Size))
                {
                    AddMessage(textBlock, MessageType.Error, $"{current.NoteType} @ {current.BeatData.Measure} {current.BeatData.Tick} is overlapping with {next.NoteType}.\n");
                    error = true;
                }
            }
            
            if (error)
            {
                AddMessage(textBlock, MessageType.None, "Fully overlapping Notes may break the judgement engine, and are generally unfair to players.\n\n");
            }
        }

        void checkUnbakedHolds()
        {
            bool error = false;
            
            foreach (Note note in chart.Notes)
            {
                if (note.NoteType is not (NoteType.HoldStart or NoteType.HoldSegment)) continue;
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