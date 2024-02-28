using System;
using MercuryMapper.Data;
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

    private float visibleMeasures;
    private float currentMeasureDecimal => mainView.ChartEditor.CurrentMeasure;
    private float scaledCurrentMeasureDecimal => Chart.GetScaledMeasureDecimal(currentMeasureDecimal);
    
    public void Render(SKCanvas canvas)
    {
        DrawBackground(canvas);
        DrawGuideLines(canvas);
        DrawJudgementLine(canvas);
        DrawMaskEffect(canvas);
        //if (mainView.AudioManager.CurrentSong is null or { IsPlaying: false }) DrawTickCircle(canvas);
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
        canvasRect = SKRect.Inflate(canvasRect, canvasRadius - canvasMaxRadius, canvasRadius - canvasMaxRadius);
    }

    public void UpdateBrushes()
    {
        brushes.SetBrushes(canvasCenter, canvasRadius, canvasMaxRadius, canvasScale, mainView.UserConfig);
    }

    public void UpdateNoteSpeed()
    {
        // A Note scrolling from it's spawn point to the judgement line at NoteSpeed 1.0 takes
        // approximately 3266.667 milliseconds.
        float visibleTime = 3266.667f / (float)mainView.UserConfig.RenderConfig.NoteSpeed;
        visibleMeasures = Chart.Timestamp2MeasureDecimal(visibleTime);
    }
    
    // ________________
    private float GetNoteScale(Chart chart, float measureDecimal)
    {
        bool showHiSpeed = mainView.UserConfig.RenderConfig.ShowHiSpeed;
        float note = showHiSpeed ? chart.GetScaledMeasureDecimal(measureDecimal) : measureDecimal;
        float current = showHiSpeed ? scaledCurrentMeasureDecimal : currentMeasureDecimal;
        float visionEnd = scaledCurrentMeasureDecimal + visibleMeasures;

        float scale = 1 - (note - current) / (visionEnd - current);
        
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
        float arcAngle = note.Size * -6;
        float scale = GetNoteScale(chart, note.BeatData.MeasureDecimal);
        SKRect rect = GetRect(scale);

        return new(rect, scale, startAngle, arcAngle);
    }
    
    // ________________
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

    private void DrawGuideLines(SKCanvas canvas, int guideLineSelection = 5)
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
        canvas.DrawArc(canvasMaxRect, -90, -30, true, brushes.MaskFill);
    }

    private void DrawTickCircle(SKCanvas canvas)
    {
        canvas.DrawOval(canvasCenter.X, canvasCenter.Y, canvasRadius + 15, canvasRadius + 14, brushes.TickPen);
        for (int i = 0; i < 60; i++)
        {
            SKPoint startPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius + 14, i * 6);
            SKPoint endPoint;
            float tickLength = canvasRadius / 14.25f;

            if (i % 15 == 0)
            {
                endPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius + 14 - tickLength * 3.5f, i * 6);
            }
            else if (i % 5 == 0)
            {
                endPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius + 14 - tickLength * 2.5f, i * 6);
            }
            else
            {
                endPoint = RenderMath.GetPointOnArc(canvasCenter, canvasRadius + 14 - tickLength, i * 6);
            }
            
            canvas.DrawLine(startPoint, endPoint, brushes.TickPen);
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

