using System;
using System.Collections.Generic;
using System.Linq;
using FluentAvalonia.Core;
using MercuryMapper.Config;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.Utils;
using MercuryMapper.Views;
using SkiaSharp;

namespace MercuryMapper.Rendering;

/// <summary>
/// SkiaSharp witchcraft. I have NO clue how I managed to do this.
/// </summary>
public class RenderEngine(MainView mainView)
{
    private readonly MainView mainView = mainView;
    private readonly Brushes brushes = new(mainView.UserConfig);
    private Chart Chart => mainView.ChartEditor.Chart;

    private SKPoint canvasCenter;
    private SKRect canvasRect;
    private SKRect canvasMaxRect;
    private float canvasRadius;
    private float canvasMaxRadius;
    private float canvasScale;

    private bool IsPlaying => mainView.AudioManager.CurrentSong is { IsPlaying: true };
    private float visibleDistanceMeasureDecimal;
    private float CurrentMeasureDecimal => mainView.ChartEditor.CurrentMeasure;
    private float ScaledCurrentMeasureDecimal => Chart.GetScaledMeasureDecimal(CurrentMeasureDecimal, RenderConfig.ShowHiSpeed);
    private RenderConfig RenderConfig => mainView.UserConfig.RenderConfig;
    
    public void Render(SKCanvas canvas)
    {
        DrawBackground(canvas);
        DrawGuideLines(canvas, RenderConfig.GuideLineType);
        DrawJudgementLine(canvas);
        DrawMaskEffect(canvas);
        if (!IsPlaying)
        {
            DrawCursor(canvas, mainView.ChartEditor.CurrentNoteType, mainView.ChartEditor.Cursor.Position, mainView.ChartEditor.Cursor.Size);
            DrawAngleTicks(canvas);
        }
        
        DrawMeasureLines(canvas, Chart);
        if (!IsPlaying || (IsPlaying && RenderConfig.ShowGimmickNotesDuringPlayback)) DrawGimmickNotes(canvas, Chart);
        if (!IsPlaying || (IsPlaying && RenderConfig.ShowMaskDuringPlayback)) DrawMaskNotes(canvas, Chart);
        DrawSyncs(canvas, Chart); // Hold Surfaces normally render under syncs but the syncs poke into the note a bit and it looks shit.
        DrawHolds(canvas, Chart);
        DrawNotes(canvas, Chart);
        DrawArrows(canvas, Chart);
    }

    // ________________
    public void UpdateSize(double size)
    {
        const float margin = 15;
        const float multiplier = 0.9f;
        
        canvasMaxRadius = (float)size * 0.5f - margin;
        canvasMaxRect = new(margin, margin, (float)size - margin, (float)size - margin);

        canvasScale = canvasMaxRadius / 347.5f; // Proporiton of default radius to actual radius
        canvasCenter = new(canvasMaxRadius + margin, canvasMaxRadius + margin);
        
        canvasRadius = canvasMaxRadius * multiplier;
        canvasRect = SKRect.Inflate(canvasMaxRect, canvasRadius - canvasMaxRadius, canvasRadius - canvasMaxRadius);
    }

    public void UpdateBrushes()
    {
        brushes.SetBrushes(canvasCenter, canvasRadius, canvasMaxRadius, canvasScale);
    }

    public void UpdateVisibleTime()
    {
        // A Note scrolling from it's spawn point to the judgement line at NoteSpeed 1.0 takes
        // approximately 3266.667 milliseconds.
        float visibleTime = 3266.667f / (float)RenderConfig.NoteSpeed;
        visibleDistanceMeasureDecimal = Chart.Timestamp2MeasureDecimal(visibleTime);
    }
    
    // ________________

    public Note? GetNoteAtPointer(Chart chart, SKPoint point)
    {
        int clickPosition = MathExtensions.GetThetaNotePosition(point.X, point.Y);
        float clickRadius = (1 - MathExtensions.InversePerspective(point.Length)) * visibleDistanceMeasureDecimal;
        float measureDecimal = CurrentMeasureDecimal + chart.GetUnscaledMeasureDecimal(clickRadius, RenderConfig.ShowHiSpeed);
        
        // Holy mother of LINQ
        List<Note> clickedNotes = chart.Notes
            .Where(x => Math.Abs(x.BeatData.MeasureDecimal - measureDecimal) < 0.005f)
            .Select(note =>
            {
                float center = MathExtensions.Modulo(note.Position + note.Size * 0.5f, 60);
                float maxDistance = note.Size * 0.5f;
                float distance = float.Min(float.Abs(center - clickPosition), 60 - float.Abs(center - clickPosition));

                return new { Note = note, Distance = distance };
            })
            .Where(item => item.Distance < item.Note.Size * 0.5f)
            .OrderBy(item => item.Distance)
            .Select(item => item.Note)
            .ToList();

        Console.WriteLine($"Note HIT! Type {clickedNotes.MinBy(x => x.IsHold)?.NoteType}");
        
        return clickedNotes.MinBy(x => x.IsHold);
    }
    
    // ________________
    private float GetNoteScale(Chart chart, float measureDecimal)
    {
        float note = chart.GetScaledMeasureDecimal(measureDecimal, RenderConfig.ShowHiSpeed);
        float scale = 1 - (note - ScaledCurrentMeasureDecimal) / visibleDistanceMeasureDecimal;
        return MathExtensions.Perspective(scale);
    }

    private SKRect GetRect(float scale)
    {
        float size = canvasRadius * scale;
        return new(canvasCenter.X - size, canvasCenter.Y - size, canvasCenter.X + size, canvasCenter.Y + size);
    }
    
    private ArcData GetArc(Chart chart, Note note)
    {
        float startAngle = note.Position * -6;
        float sweepAngle = note.Size * -6;
        float scale = GetNoteScale(chart, note.BeatData.MeasureDecimal);
        SKRect rect = GetRect(scale);

        return new(rect, scale, startAngle, sweepAngle);
    }

    private ArcData GetArc(Chart chart, Gimmick gimmick)
    {
        float scale = GetNoteScale(chart, gimmick.BeatData.MeasureDecimal);
        SKRect rect = GetRect(scale);

        return new(rect, scale, 0, 360);
    }
    
    private static void TruncateArc(ref ArcData data, bool includeCaps)
    {
        if (includeCaps)
        {
            data.StartAngle -= 4.5f;
            data.SweepAngle += 9f;
        }
        else
        {
            data.StartAngle -= 6f;
            data.SweepAngle += 12f;
        }
    }

    private static void TrimCircleArc(ref ArcData data)
    {
        data.SweepAngle = 359.9999f * float.Sign(data.SweepAngle);
    }

    private static void FlipArc(ref ArcData data)
    {
        data.StartAngle += data.SweepAngle;
        data.SweepAngle *= -1;
    }
    
    // ________________
    // ____ UI
    private void DrawBackground(SKCanvas canvas)
    {
        canvas.Clear(brushes.BackgroundColor);
        
        canvas.DrawCircle(canvasCenter.X, canvasCenter.Y, canvasMaxRadius, brushes.TunnelFill);
        
        canvas.DrawOval(canvasCenter.X, canvasCenter.Y, canvasRadius * 0.25f, canvasRadius * 0.25f, brushes.GetTunnelStripes(30 * canvasScale));
        canvas.DrawOval(canvasCenter.X, canvasCenter.Y, canvasRadius * 0.45f, canvasRadius * 0.45f, brushes.GetTunnelStripes(45 * canvasScale));
        canvas.DrawOval(canvasCenter.X, canvasCenter.Y, canvasRadius * 0.75f, canvasRadius * 0.75f, brushes.GetTunnelStripes(60 * canvasScale));
    }

    private void DrawJudgementLine(SKCanvas canvas)
    {
        canvas.DrawOval(canvasCenter.X, canvasCenter.Y, canvasRadius, canvasRadius, brushes.JudgementLinePen);
        canvas.DrawOval(canvasCenter.X, canvasCenter.Y, canvasRadius, canvasRadius, brushes.JudgementLineShadingPen);
    }

    private void DrawGuideLines(SKCanvas canvas, int guideLineSelection)
    {
        // 0 - offset   0 - interval 00
        // A - offset   0 - interval 06
        // B - offset +06 - interval 12
        // C - offset   0 - interval 18
        // D - offset +06 - interval 24
        // E - offset   0 - interval 30
        // F - offset +30 - interval 60
        // G - offset   0 - interval 90
        
        int offset;
        int interval;
        switch (guideLineSelection)
        {
            default: return;

            case 1:
                offset = 0;
                interval = 6;
                break;

            case 2:
                offset = 6;
                interval = 12;
                break;

            case 3:
                offset = 0;
                interval = 18;
                break;

            case 4:
                offset = 6;
                interval = 24;
                break;

            case 5:
                offset = 0;
                interval = 30;
                break;

            case 6:
                offset = 30;
                interval = 60;
                break;

            case 7:
                offset = 0;
                interval = 90;
                break;
        }

        float tickLength = canvasRadius * 0.8f;
        float innerRadius = canvasRadius - tickLength;

        for (int i = 0 + offset; i < 360 + offset; i += interval)
        {
            SKPoint startPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius, i);
            SKPoint endPoint = RenderMath.GetPointOnArc(canvasCenter, innerRadius, i);

            canvas.DrawLine(startPoint, endPoint, brushes.GetGuideLinePen(startPoint, endPoint));
        }
    }

    private void DrawMaskEffect(SKCanvas canvas)
    {
        if (mainView.AudioManager.CurrentSong is null) return;
        
        bool[] maskState = new bool[60];
        foreach (Note note in Chart.Notes.Where(x => x.IsMask))
        {
            if (note.BeatData.MeasureDecimal > CurrentMeasureDecimal) break;

            float maskTimestamp = Chart.MeasureDecimal2Timestamp(note.BeatData.MeasureDecimal);
            float timeDifference = mainView.AudioManager.CurrentSong.Position - maskTimestamp;
            float animationDuration = note.Size * 8; // 8ms per mask
            
            // In range for animation
            if (timeDifference < animationDuration)
            {
                float progress = timeDifference / animationDuration;
                
                if (note.MaskDirection is MaskDirection.Center)
                {
                    float halfSize = note.Size * 0.5f;
                    int floor = (int)halfSize;
                    int steps = (int)Math.Ceiling(halfSize);
                    int centerClockwise = note.Position + floor;
                    int centerCounterclockwise = note.Size % 2 != 0 ? centerClockwise : centerClockwise + 1;
                    int offset = note.Size % 2 != 0 ? 60 : 59;

                    for (int i = 0; i < (int)(steps * progress); i++)
                    {
                        maskState[(centerClockwise - i + offset) % 60] = note.NoteType is NoteType.MaskAdd;
                        maskState[(centerCounterclockwise + i + offset) % 60] = note.NoteType is NoteType.MaskAdd;
                    }
                }
                
                else if (note.MaskDirection is MaskDirection.Clockwise)
                {
                    // what the fuck is this
                    //for (int i = note.Position + note.Size - 1; i >= note.Position + (int)(note.Size * (1 - progress)); i--)
                    //{
                    //    maskState[i % 60] = note.NoteType is NoteType.MaskAdd;
                    //}

                    for (int i = 0; i < (int)(note.Size * progress); i++)
                    {
                        maskState[(note.Position + note.Size - i + 59) % 60] = note.NoteType is NoteType.MaskAdd;
                    }
                }
                
                else // Counterclockwise
                {
                    //for (int i = note.Position; i < note.Position + (int)(note.Size * progress); i++)
                    //{
                    //    maskState[i % 60] = note.NoteType is NoteType.MaskAdd;
                    //}

                    for (int i = 0; i < (int)(note.Size * progress); i++)
                    {
                        maskState[(i + note.Position + 60) % 60] = note.NoteType is NoteType.MaskAdd;
                    }
                }

                continue;
            }
            
            // Set state without animating
            for (int i = note.Position; i < note.Position + note.Size; i++)
            {
                maskState[i % 60] = note.NoteType is NoteType.MaskAdd;
            }
        }

        for (int i = 0; i < maskState.Length; i++)
        {
            if (!maskState[i]) canvas.DrawArc(canvasMaxRect, i * -6, -6, true, brushes.MaskFill);
        }
    }
 
    private void DrawAngleTicks(SKCanvas canvas)
    {
        for (int i = 0; i < 60; i++)
        {
            SKPoint startPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius + brushes.NoteWidthMultiplier * 0.5f, i * 6);
            SKPoint endPoint;
            float tickLength = canvasRadius / 14.25f;

            if (i % 15 == 0)
            {
                endPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius - tickLength * 3.5f, i * 6);
            }
            else if (i % 5 == 0)
            {
                endPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius - tickLength * 2.5f, i * 6);
            }
            else
            {
                endPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius - tickLength, i * 6);
            }
            
            canvas.DrawLine(startPoint, endPoint, brushes.AngleTickPen);
        }
    }

    private void DrawCursor(SKCanvas canvas, NoteType noteType, int position, int size)
    {
        canvas.DrawArc(canvasRect, position * -6, size * -6, false, brushes.GetCursorPen(noteType, canvasScale));
    }
    
    // ____ NOTES
    private void DrawMeasureLines(SKCanvas canvas, Chart chart)
    {
        float interval = 1.0f / RenderConfig.BeatDivision;
        float start = MathF.Ceiling(CurrentMeasureDecimal * RenderConfig.BeatDivision) * (interval);
        float end = ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal;
        
        for (float i = start; chart.GetScaledMeasureDecimal(i, RenderConfig.ShowHiSpeed) < end; i += interval)
        {
            SKRect rect = GetRect(GetNoteScale(chart, i));
            if (rect.Width < 1) continue;

            bool isMeasure = Math.Abs(i - (int)i) < 0.001f;
            canvas.DrawOval(rect, isMeasure ? brushes.MeasurePen : brushes.BeatPen);
        }
    }

    private void DrawSyncs(SKCanvas canvas, Chart chart)
    {
        List<Note> visibleNotes = chart.Notes.Where(x =>
            x is { IsSegment: false, IsMask: false } && (!x.IsChain || x is { IsChain: true, IsRNote: true }) // because apparently R Note chains have syncs. Just being accurate to the game (:
            && x.BeatData.MeasureDecimal >= CurrentMeasureDecimal
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).ToList();
        
        // This code is from saturn, lol
        for (int i = 1; i < visibleNotes.Count; i++)
        {
            Note current = visibleNotes[i];
            Note previous = visibleNotes[i - 1];
            
            if (current.BeatData.FullTick != previous.BeatData.FullTick) continue;
            
            float scale = GetNoteScale(chart, current.BeatData.MeasureDecimal);
            SKRect rect = GetRect(scale);
            
            drawSyncConnector(current, previous, rect, scale);
        }

        return;

        void drawSyncConnector(Note current, Note previous, SKRect rect, float scale)
        {
            int position0 = MathExtensions.Modulo(current.Position + current.Size - 1, 60); // pos + 1 // size  - 2
            int size0 = MathExtensions.Modulo(previous.Position - position0, 60) + 1;  // pos + 1 // shift - 1
            
            int position1 = MathExtensions.Modulo(previous.Position + previous.Size - 1, 60); // pos + 1 // size  - 2
            int size1 = MathExtensions.Modulo(current.Position - position1, 60) + 1;  // pos + 1 // shift - 1

            int finalPosition = size0 > size1 ? position1 : position0;
            int finalSize = int.Min(size0, size1);

            if (finalSize > 30) return;
            
            canvas.DrawArc(rect, finalPosition * -6, finalSize * -6, false, brushes.GetSyncPen(scale));
        }
    }

    private void DrawHolds(SKCanvas canvas, Chart chart)
    {
        // Long-ass line of code. Not a fan but idk how else to write this where it still makes sense. TL;DR:
        //
        // IsHold &&
        // (Note is in vision range || Next note it's referencing is in front of vision range)
        List<Note> visibleNotes = chart.Notes.Where(x =>
            x.IsHold &&
            (
                (x.BeatData.MeasureDecimal >= CurrentMeasureDecimal && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal)
                ||
                (x.NextReferencedNote != null && x.BeatData.MeasureDecimal < CurrentMeasureDecimal && chart.GetScaledMeasureDecimal(x.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) > ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal)
            )
        ).ToList();
        
        foreach (Note note in visibleNotes)
        {
            ArcData currentData = GetArc(chart, note);
            if (note.Size != 60) TruncateArc(ref currentData, true);
            else TrimCircleArc(ref currentData);
            
            bool currentVisible = note.BeatData.MeasureDecimal >= CurrentMeasureDecimal;
            bool nextVisible = note.NextReferencedNote != null && chart.GetScaledMeasureDecimal(note.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal;
            bool prevVisible = note.PrevReferencedNote != null && note.PrevReferencedNote.BeatData.MeasureDecimal >= CurrentMeasureDecimal;
            
            if (currentVisible && nextVisible)
            {
                Note nextNote = note.NextReferencedNote!;
                ArcData nextData = GetArc(chart, nextNote);
                
                if (nextNote.Size != 60) TruncateArc(ref nextData, true);
                else TrimCircleArc(ref nextData);
                
                FlipArc(ref nextData);
                
                SKPath path = new(); 
                path.ArcTo(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, true);
                path.ArcTo(nextData.Rect, nextData.StartAngle, nextData.SweepAngle, false);
                
                canvas.DrawPath(path, brushes.HoldFill);
            }

            if (currentVisible && !prevVisible && note.PrevReferencedNote != null)
            {
                Note prevNote = note.PrevReferencedNote;
                ArcData prevData = GetArc(chart, prevNote);
                
                if (prevNote.Size != 60) TruncateArc(ref prevData, true);
                else TrimCircleArc(ref prevData);

                float ratio = MathExtensions.InverseLerp(CurrentMeasureDecimal, note.BeatData.MeasureDecimal, prevNote.BeatData.MeasureDecimal);
                
                if (float.Abs(currentData.StartAngle - prevData.StartAngle) > 180)
                {
                    if (currentData.StartAngle > prevData.StartAngle) currentData.StartAngle -= 360;
                    else prevData.StartAngle -= 360;
                }
                
                ArcData intermediateData = new(canvasRect, 1, MathExtensions.Lerp(currentData.StartAngle, prevData.StartAngle, ratio), MathExtensions.Lerp(currentData.SweepAngle, prevData.SweepAngle, ratio));
                FlipArc(ref intermediateData);
                
                SKPath path = new(); 
                path.ArcTo(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, true);
                path.ArcTo(intermediateData.Rect, intermediateData.StartAngle, intermediateData.SweepAngle, false);
                
                canvas.DrawPath(path, brushes.HoldFill);
            }

            if (currentVisible && !nextVisible && note.NextReferencedNote != null )
            {
                canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, true, brushes.HoldFill);
            }
            
            if (!currentVisible && !nextVisible && note.NextReferencedNote != null)
            {
                Note nextNote = note.NextReferencedNote;
                ArcData nextData = GetArc(chart, nextNote);
                
                if (nextNote.Size != 60) TruncateArc(ref nextData, true);
                else TrimCircleArc(ref nextData);
                
                float ratio = MathExtensions.InverseLerp(CurrentMeasureDecimal, note.BeatData.MeasureDecimal, nextNote.BeatData.MeasureDecimal);
                
                if (float.Abs(currentData.StartAngle - nextData.StartAngle) > 180)
                {
                    if (currentData.StartAngle > nextData.StartAngle) currentData.StartAngle -= 360;
                    else nextData.StartAngle -= 360;
                }
                
                ArcData intermediateData = new(canvasRect, 1, MathExtensions.Lerp(currentData.StartAngle, nextData.StartAngle, ratio), MathExtensions.Lerp(currentData.SweepAngle, nextData.SweepAngle, ratio));
                
                canvas.DrawArc(intermediateData.Rect, intermediateData.StartAngle, intermediateData.SweepAngle, true, brushes.HoldFill);
            }
        }
        
        // Second foreach to ensure notes are rendered on top of surfaces.
        // Reverse so notes further away are rendered first, then closer notes
        // are rendered on top.
        visibleNotes.Reverse();
        foreach (Note note in visibleNotes)
        {
            ArcData currentData = GetArc(chart, note);
            if (note.Size != 60) TruncateArc(ref currentData, true);
            else TrimCircleArc(ref currentData);
            
            if (currentData.Rect.Width < 1 || note.BeatData.MeasureDecimal < CurrentMeasureDecimal) continue;
            
            if (note.IsRNote)
            {
                float start = currentData.StartAngle + (note.Size != 60 ? 4.5f : 0);
                float sweep = currentData.SweepAngle - (note.Size != 60 ? 9.0f : 0);
                canvas.DrawArc(currentData.Rect, start, sweep, false, brushes.GetRNotePen(canvasScale * currentData.Scale));
            }
            
            if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                if (note.Size != 60) DrawNoteCaps(canvas, currentData.Rect, currentData.StartAngle, currentData.SweepAngle, currentData.Scale);
                canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetNotePen(note, canvasScale * currentData.Scale));
            }

            if (note.NoteType is NoteType.HoldSegment && !IsPlaying)
            {
                canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetNotePen(note, canvasScale * currentData.Scale * 0.5f));
            }

            if (note.NoteType is NoteType.HoldEnd)
            {
                canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetHoldEndPen(canvasScale * currentData.Scale));
            }
            
            if (mainView.ChartEditor.SelectedNotes.Contains(note))
            {
                ArcData selectedData = GetArc(chart, note);
                canvas.DrawArc(selectedData.Rect, selectedData.StartAngle, selectedData.SweepAngle, false, brushes.GetSelectionPen(canvasScale * selectedData.Scale));
            }
        }
    }
    
    private void DrawNotes(SKCanvas canvas, Chart chart)
    {
        // Reverse to draw from middle out => preserves depth overlap
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x =>
            x is { IsHold: false, IsMask: false }
            && x.BeatData.MeasureDecimal >= CurrentMeasureDecimal
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);

            if (data.Rect.Width < 1) continue;
            
            if (note.IsRNote)
            {
                float start = data.StartAngle - (note.Size != 60 ? 1.5f : 0);
                float sweep = data.SweepAngle + (note.Size != 60 ? 3.0f : 0);
                canvas.DrawArc(data.Rect, start, sweep, false, brushes.GetRNotePen(canvasScale * data.Scale));
            }
            
            // Normal Note
            if (note.Size != 60)
            {
                TruncateArc(ref data, false);
                DrawNoteCaps(canvas, data.Rect, data.StartAngle, data.SweepAngle, data.Scale);
            }
            
            canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));

            if (mainView.ChartEditor.SelectedNotes.Contains(note))
            {
                ArcData selectedData = GetArc(chart, note);
                canvas.DrawArc(selectedData.Rect, selectedData.StartAngle, selectedData.SweepAngle, false, brushes.GetSelectionPen(canvasScale * selectedData.Scale));
            }
        }
    }

    private void DrawMaskNotes(SKCanvas canvas, Chart chart)
    {
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x =>
            x.IsMask
            && x.BeatData.MeasureDecimal >= CurrentMeasureDecimal
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();
        
        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);

            if (data.Rect.Width < 1) continue;
            
            canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));

            if (mainView.ChartEditor.SelectedNotes.Contains(note))
            {
                ArcData selectedData = GetArc(chart, note);
                canvas.DrawArc(selectedData.Rect, selectedData.StartAngle, selectedData.SweepAngle, false, brushes.GetSelectionPen(canvasScale * selectedData.Scale));
            }
        }
    }

    private void DrawGimmickNotes(SKCanvas canvas, Chart chart)
    {
        IEnumerable<Gimmick> visibleGimmicks = chart.Gimmicks.Where(x =>
            x.BeatData.MeasureDecimal >= CurrentMeasureDecimal
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Gimmick gimmick in visibleGimmicks)
        {
            ArcData data = GetArc(chart, gimmick);
            canvas.DrawOval(data.Rect, brushes.GetGimmickPen(gimmick, canvasScale * data.Scale));
        }
    }
    
    private void DrawNoteCaps(SKCanvas canvas, SKRect rect, float startAngle, float sweepAngle, float scale)
    {
        const float sweep = 1.6f;
        float start1 = startAngle - 0.1f;
        float start2 = startAngle + sweepAngle - 1.5f;  
            
        canvas.DrawArc(rect, start1, sweep, false, brushes.GetNoteCapPen(canvasScale * scale));
        canvas.DrawArc(rect, start2, sweep, false, brushes.GetNoteCapPen(canvasScale * scale));
    }

    private void DrawArrows(SKCanvas canvas, Chart chart)
    {
        // Reverse to draw from middle out => preserves depth overlap
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x => 
            (x.IsSlide || x.IsSnap)
            && x.BeatData.MeasureDecimal >= CurrentMeasureDecimal
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Note note in visibleNotes)
        {
            float scale = GetNoteScale(chart, note.BeatData.MeasureDecimal);
            SKRect rect = GetRect(scale);

            int arrowDirection = note.NoteType switch
            {
                NoteType.SlideClockwise => 1,
                NoteType.SlideClockwiseBonus => 1,
                NoteType.SlideClockwiseRNote => 1,
                NoteType.SnapForward => 1,
                NoteType.SnapForwardRNote => 1,
                NoteType.SlideCounterclockwise => -1,
                NoteType.SlideCounterclockwiseBonus => -1,
                NoteType.SlideCounterclockwiseRNote => -1,
                NoteType.SnapBackward => -1,
                NoteType.SnapBackwardRNote => -1,
                _ => 0
            };

            if (note.IsSnap) drawSnap(note, rect, scale, arrowDirection);
            else drawSlide(note, rect, scale, arrowDirection);
        }

        return;
        
        void drawSnap(Note note, SKRect rect, float scale, int arrowDirection)
        {
            int arrowCount = note.Size / 3;
            float radius = rect.Width * 0.53f;
            float snapRadiusOffset = arrowDirection > 0 ? 0.8f : 0.7f;
            float snapRowOffset = rect.Width * 0.045f;
            const float snapArrowLength = 0.1f;
            const float snapArrowWidth = 3.0f;
            
            float startPoint = note.Position * -6;
            float endPoint = startPoint + note.Size * -6;
            float interval = (endPoint - startPoint) / arrowCount;
            float offset = interval * 0.5f;

            for (float i = startPoint + offset; i > endPoint; i += interval)
            {
                SKPoint p1 = RenderMath.GetPointOnArc(canvasCenter, radius * snapRadiusOffset, i + snapArrowWidth);
                SKPoint p2 = RenderMath.GetPointOnArc(canvasCenter, radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint p3 = RenderMath.GetPointOnArc(canvasCenter, radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint p4 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + radius * snapRadiusOffset, i + snapArrowWidth);
                SKPoint p5 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint p6 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + radius * snapRadiusOffset, i - snapArrowWidth);

                var path = new SKPath();
                path.MoveTo(p1);
                path.LineTo(p2);
                path.LineTo(p3);
                path.MoveTo(p4);
                path.LineTo(p5);
                path.LineTo(p6);
                
                canvas.DrawPath(path, brushes.GetSnapPen(note.NoteType, canvasScale * scale));
            }
        }
        
        void drawSlide(Note note, SKRect rect, float scale, int arrowDirection)
        {
            float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(note.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed);
            float spin = 1 - (scaledMeasureDecimal - ScaledCurrentMeasureDecimal) / visibleDistanceMeasureDecimal;

            float radiusCenter = rect.Width * 0.42f;
            float arrowCount = note.Size * 0.5f + 1;
            int arrowCountCeiling = (int)float.Ceiling(arrowCount);

            const float arrowTipOffset = 6;
            const float arrowWidth = 35;
            const float spinSpeed = 6;
            const float arrowSpacing = 12;

            float minAngle = note.Position * -6;
            float maxAngle = (note.Position + note.Size) * -6;

            for (int i = 0; i < arrowCountCeiling; i++)
            {
                // Inside
                //
                // p4____p3    ______
                //  \     \    \     \ 
                //  p5     \p2  \     \
                //   /     /    /     /
                // p6_____p1   /_____/
                //
                // Outside 

                float loopedPosition = MathExtensions.Modulo(i + spin * spinSpeed, arrowCountCeiling);
                float startAngle = minAngle + loopedPosition * -arrowSpacing + arrowSpacing;
                float t0 = loopedPosition / arrowCount;
                float t1 = (loopedPosition + 0.5f) / arrowCount;
                
                float radiusOutside0 = float.Max(radiusCenter + (arrowWidth * maskFunction(t0) * scale), radiusCenter);
                float radiusOutside1 = float.Max(radiusCenter + (arrowWidth * maskFunction(t1) * scale), radiusCenter);
                float radiusInside0 = float.Min(radiusCenter - (arrowWidth * maskFunction(t0) * scale), radiusCenter);
                float radiusInside1 = float.Min(radiusCenter - (arrowWidth * maskFunction(t1) * scale), radiusCenter);

                SKPoint p1;
                SKPoint p2;
                SKPoint p3;
                SKPoint p4;
                SKPoint p5;
                SKPoint p6;
                
                if (arrowDirection < 0)
                {
                    // Counterclockwise
                    p1 = RenderMath.GetPointOnArc(canvasCenter, radiusOutside1, float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle));
                    p2 = RenderMath.GetPointOnArc(canvasCenter, radiusCenter,   float.Clamp(startAngle - arrowTipOffset * (1 + (t1 * 0.5f + 0.5f)), maxAngle, minAngle));
                    p3 = RenderMath.GetPointOnArc(canvasCenter, radiusInside1,  float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle));
                    p4 = RenderMath.GetPointOnArc(canvasCenter, radiusInside0,  float.Clamp(startAngle,                                             maxAngle, minAngle));
                    p5 = RenderMath.GetPointOnArc(canvasCenter, radiusCenter,   float.Clamp(startAngle - arrowTipOffset * (t0 * 0.5f + 0.5f),       maxAngle, minAngle));
                    p6 = RenderMath.GetPointOnArc(canvasCenter, radiusOutside0, float.Clamp(startAngle,                                             maxAngle, minAngle));
                }
                else
                {
                    // Clockwise
                    p1 = RenderMath.GetPointOnArc(canvasCenter, radiusOutside1, maxAngle - float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle) + minAngle);
                    p2 = RenderMath.GetPointOnArc(canvasCenter, radiusCenter,   maxAngle - float.Clamp(startAngle - arrowTipOffset * (1 + (t1 * 0.5f + 0.5f)), maxAngle, minAngle) + minAngle);
                    p3 = RenderMath.GetPointOnArc(canvasCenter, radiusInside1,  maxAngle - float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle) + minAngle);
                    p4 = RenderMath.GetPointOnArc(canvasCenter, radiusInside0,  maxAngle - float.Clamp(startAngle,                                             maxAngle, minAngle) + minAngle);
                    p5 = RenderMath.GetPointOnArc(canvasCenter, radiusCenter,   maxAngle - float.Clamp(startAngle - arrowTipOffset * (t0 * 0.5f + 0.5f),       maxAngle, minAngle) + minAngle);
                    p6 = RenderMath.GetPointOnArc(canvasCenter, radiusOutside0, maxAngle - float.Clamp(startAngle,                                             maxAngle, minAngle) + minAngle);
                }
                
                var path = new SKPath();   
                path.MoveTo(p1);   
                path.LineTo(p2);   
                path.LineTo(p3);   
                path.LineTo(p4);   
                path.LineTo(p5);   
                path.LineTo(p6);   
                path.Close();
                
                canvas.DrawPath(path, brushes.GetSwipeFill(note.NoteType));
            }
        }
        
        // I just traced the Slide Arrow Mask texture from Mercury with a graph.
        // https://www.desmos.com/calculator/ylcsznpfra
        float maskFunction(float t)
        {
            return t < 0.88f ? 0.653f * t + 0.175f : -6.25f * t + 6.25f;
        }
    }
}

internal struct ArcData(SKRect rect, float scale, float startAngle, float sweepAngle)
{
    public SKRect Rect = rect;
    public float Scale = scale;
    public float StartAngle = startAngle;
    public float SweepAngle = sweepAngle;
}

internal static class RenderMath
{
    /// <summary>
    /// Returns an SKPoint on an arc described by the centerpoint (x,y), radius and angle.
    /// </summary>
    /// <param name="center">Centerpoint</param>
    /// <param name="radius">Radius of the arc</param>
    /// <param name="angle">Angle on the arc in degrees</param>
    internal static SKPoint GetPointOnArc(SKPoint center, float radius, float angle)
    {
        return new(
            (float)(radius * Math.Cos(MathExtensions.DegToRad(angle)) + center.X),
            (float)(radius * Math.Sin(MathExtensions.DegToRad(angle)) + center.Y));
    }
    
}

