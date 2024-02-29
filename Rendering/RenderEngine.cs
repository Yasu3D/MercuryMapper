using System;
using System.Collections.Generic;
using System.Linq;
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
            // TODO: Create a cursor class and get data from there instead.
            DrawCursor(canvas, NoteType.Touch, 0, 15);
            DrawAngleTicks(canvas);
        }
        
        DrawMeasureLines(canvas, Chart);
        DrawNotes(canvas, Chart);
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
    private float GetNoteScale(Chart chart, float measureDecimal)
    {
        float note = chart.GetScaledMeasureDecimal(measureDecimal, RenderConfig.ShowHiSpeed);
        float scale = 1 - (note - ScaledCurrentMeasureDecimal) / visibleDistanceMeasureDecimal;
        
        // Huge thanks to CG505 for figuring out the perspective math:
        // https://www.desmos.com/calculator/9a0srmgktj
        return 3.325f * scale / (13.825f - 10.5f * scale);
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

    private void TruncateArc(ref ArcData data)
    {
        data.StartAngle -= 6f;
        data.SweepAngle += 12f;
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
    
    private void DrawNotes(SKCanvas canvas, Chart chart)
    {
        // Reverse to draw from middle out => preserves depth overlap
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x =>
            !x.IsHold
            && x.BeatData.MeasureDecimal >= CurrentMeasureDecimal
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);

            if (data.Rect.Width < 1) continue;
            
            // Masks
            if (note.IsMask)
            {
                canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));
                continue;
            }
            
            // Normal Note
            if (note.Size != 60)
            {
                TruncateArc(ref data);
                drawNoteCaps(data.Rect, data.StartAngle, data.SweepAngle, data.Scale);
            }
            
            canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));
        }

        void drawNoteCaps(SKRect rect, float startAngle, float sweepAngle, float scale)
        {
            const float sweep = 1.6f;
            float start1 = startAngle - 0.1f;
            float start2 = startAngle + sweepAngle - 1.5f;  
            
            canvas.DrawArc(rect, start1, sweep, false, brushes.GetNoteCapPen(canvasScale * scale));
            canvas.DrawArc(rect, start2, sweep, false, brushes.GetNoteCapPen(canvasScale * scale));
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

