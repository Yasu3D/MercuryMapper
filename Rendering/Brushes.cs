using System;
using System.Collections.Generic;
using MercuryMapper.Config;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using SkiaSharp;

namespace MercuryMapper.Rendering;

public class Brushes(UserConfig userConfig)
{
    private UserConfig userConfig = userConfig;
    private readonly Dictionary<string, string> colors = userConfig.ColorConfig.Colors;
    
    // ________ Constants
    private const float TickPenStrokeWidth = 1.5f;
    private const float MeasurePenStrokeWidth = 2.0f;
    private const float BeatPenStrokeWidth = 1f;
    private const float GuidelinePenStrokeWidth = 1.0f;
    private const float NoteStrokeWidth = 9f;
    private float noteWidthMultiplier = 1;
    
    private SKColor colorNoteTouch = new();
    private SKColor colorNoteChain = new();
    private SKColor colorNoteSlideClockwise = new();
    private SKColor colorNoteSlideCounterclockwise = new();
    private SKColor colorNoteSnapForward = new();
    private SKColor colorNoteSnapBackward = new();
    private SKColor colorNoteHoldStart = new();
    private SKColor colorNoteHoldSegment = new();
    private SKColor colorNoteHoldEnd = new();
    private SKColor colorNoteHoldSurfaceFar = new();
    private SKColor colorNoteHoldSurfaceNear = new();
    private SKColor colorNoteMaskAdd = new();
    private SKColor colorNoteMaskRemove = new();
    private SKColor colorNoteEndOfChart = new();
    
    // ________ Private Brushes
    private readonly SKPaint guideLinePen = new()
    {
        StrokeWidth = GuidelinePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    private readonly SKPaint tunnelStripesPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    private readonly SKPaint notePen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = NoteStrokeWidth,
    };
    
    // ________ Public Brushes
    public SKColor BackgroundColor = new(0xFF1A1A1A);

    public readonly SKPaint TickPen = new()
    {
        StrokeWidth = TickPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        Color = SKColors.White
    };
    
    public readonly SKPaint MeasurePen = new()
    {
        StrokeWidth = MeasurePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public readonly SKPaint BeatPen = new()
    {
        StrokeWidth = BeatPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    public readonly SKPaint TunnelFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
        IsDither = true
    };

    public readonly SKPaint JudgementLinePen = new()
    {
        StrokeWidth = NoteStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public readonly SKPaint JudgementLineShadingPen = new()
    {
        StrokeWidth = NoteStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    public readonly SKPaint MaskFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };
    
    // ________ Dynamic Brushes
    public SKPaint GetTunnelStripes(float strokeWidth)
    {
        tunnelStripesPen.StrokeWidth = strokeWidth;
        return tunnelStripesPen;
    }
    
    public SKPaint GetGuideLinePen(SKPoint startPoint, SKPoint endPoint)
    {
        SKColor gradientColor = SKColor.Parse(colors["ColorGuideLines"]).WithAlpha(0x20);
        SKColor[] gradient = [gradientColor, SKColors.Transparent];
        var shader = SKShader.CreateLinearGradient(startPoint, endPoint, gradient, SKShaderTileMode.Clamp);
        guideLinePen.Shader = shader;
        return guideLinePen;
    }

    public SKPaint GetNotePen(Note note, float scale)
    {
        notePen.StrokeWidth = noteWidthMultiplier * scale;
        notePen.Color = NoteType2Color(note);
        return notePen;
    }
    
    // ________ Other
    private SKColor NoteType2Color(Note note)
    {
        return note.NoteType switch
        {
            NoteType.Touch
                or NoteType.TouchBonus
                or NoteType.TouchRNote => colorNoteTouch,
            NoteType.SnapForward
                or NoteType.SnapForwardRNote => colorNoteSnapForward,
            NoteType.SnapBackward
                or NoteType.SnapBackwardRNote => colorNoteSnapBackward,
            NoteType.SlideClockwise
                or NoteType.SlideClockwiseBonus
                or NoteType.SlideClockwiseRNote => colorNoteSlideClockwise,
            NoteType.SlideCounterclockwise
                or NoteType.SlideCounterclockwiseBonus
                or NoteType.SlideCounterclockwiseRNote => colorNoteSlideCounterclockwise,
            NoteType.Chain
                or NoteType.ChainRNote => colorNoteChain,
            NoteType.HoldStart
                or NoteType.HoldStartRNote => colorNoteHoldStart,
            NoteType.HoldSegment => colorNoteHoldSegment,
            NoteType.HoldEnd => colorNoteHoldEnd,
            NoteType.MaskAdd => colorNoteMaskAdd,
            NoteType.MaskRemove => colorNoteMaskRemove,
            NoteType.EndOfChart => colorNoteEndOfChart,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    public void SetBrushes(SKPoint center, float radius, float maxRadius, float scale, UserConfig config)
    {
        setColors();
        setStrokeWidths();
        setShaders();
        return;

        void setColors()
        {
            colorNoteTouch = SKColor.Parse(colors["ColorNoteTouch"]);
            colorNoteChain = SKColor.Parse(colors["ColorNoteChain"]);
            colorNoteSlideClockwise = SKColor.Parse(colors["ColorNoteSlideClockwise"]);
            colorNoteSlideCounterclockwise = SKColor.Parse(colors["ColorNoteSlideCounterclockwise"]);
            colorNoteSnapForward = SKColor.Parse(colors["ColorNoteSnapForward"]);
            colorNoteSnapBackward = SKColor.Parse(colors["ColorNoteSnapBackward"]);
            colorNoteHoldStart = SKColor.Parse(colors["ColorNoteHoldStart"]);
            colorNoteHoldSegment = SKColor.Parse(colors["ColorNoteHoldSegment"]);
            colorNoteHoldEnd = SKColor.Parse(colors["ColorNoteHoldEnd"]);
            colorNoteHoldSurfaceFar = SKColor.Parse(colors["ColorNoteHoldSurfaceFar"]);
            colorNoteHoldSurfaceNear = SKColor.Parse(colors["ColorNoteHoldSurfaceNear"]);
            colorNoteMaskAdd = SKColor.Parse(colors["ColorNoteMaskAdd"]);
            colorNoteMaskRemove = SKColor.Parse(colors["ColorNoteMaskRemove"]);
            
            MeasurePen.Color = SKColor.Parse(colors["ColorMeasureLine"]);
            BeatPen.Color = SKColor.Parse(colors["ColorBeatLine"]);
        
            MaskFill.Color = SKColor.Parse(colors["ColorBackgroundNoMask"]);
            tunnelStripesPen.Color = SKColor.Parse(colors["ColorBackgroundFar"]).WithAlpha(0x80);
        }

        void setStrokeWidths()
        {
            noteWidthMultiplier = NoteStrokeWidth * config.RenderConfig.NoteSize;
            JudgementLinePen.StrokeWidth = NoteStrokeWidth * scale * config.RenderConfig.NoteSize;
            JudgementLineShadingPen.StrokeWidth = NoteStrokeWidth * scale * config.RenderConfig.NoteSize;
        }
        
        void setShaders()
        {
            SKColor judgementLine0 = SKColor.Parse(colors["ColorJudgementLinePrimary"]);
            SKColor judgementLine1 = SKColor.Parse(colors["ColorJudgementLineSecondary"]);
            SKColor[] judgementColors = [judgementLine0, judgementLine1, judgementLine0, judgementLine1, judgementLine0];
            SKShader judgementShader = SKShader.CreateSweepGradient(center, judgementColors, SKShaderTileMode.Clamp, 0, 360);
            JudgementLinePen.Shader = judgementShader;
            
            SKColor judgementShading0 = SKColors.Black.WithAlpha(0x80);
            SKColor judgementShading1 = SKColors.Empty;
            SKColor[] judgementShadingColors = [judgementShading1, judgementShading0, judgementShading1];
            float shadingWidth = noteWidthMultiplier * 0.5f;
            float shadingInside = (radius - shadingWidth) / maxRadius;
            float shadingCenter = radius / maxRadius;
            float shadingOutside = (radius + shadingWidth) / maxRadius;
            float[] shadingColorPos = [shadingInside, shadingCenter, shadingOutside];
            SKShader shadingShader = SKShader.CreateRadialGradient(center, maxRadius, judgementShadingColors, shadingColorPos, SKShaderTileMode.Clamp);
            JudgementLineShadingPen.Shader = shadingShader;
            
            SKColor tunnel0 = SKColor.Parse(colors["ColorBackgroundFar"]);
            SKColor tunnel1 = SKColor.Parse(colors["ColorBackgroundNear"]);
            SKColor[] tunnelGradient = [tunnel0, tunnel1];
            SKShader shader = SKShader.CreateRadialGradient(center, radius, tunnelGradient, SKShaderTileMode.Clamp);
            TunnelFill.Shader = shader;
        }
    }
}