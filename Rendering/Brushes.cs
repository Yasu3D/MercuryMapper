using System;
using System.Collections.Generic;
using MercuryMapper.Config;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using SkiaSharp;

namespace MercuryMapper.Rendering;

public class Brushes(UserConfig userConfig)
{
    private readonly UserConfig userConfig = userConfig;
    private readonly Dictionary<string, string> colors = userConfig.ColorConfig.Colors;
    
    // ________ Constants
    private const float TickPenStrokeWidth = 1.5f;
    private const float MeasurePenStrokeWidth = 2;
    private const float BeatPenStrokeWidth = 1;
    private const float GuidelinePenStrokeWidth = 1;
    private const float NotePenStrokeWidth = 8;
    private const float HoldEndPenStrokeWidth = 12;
    private const float SnapPenStrokeWidth = 8;
    private const float CursorPenStrokeWidth = 15;
    private const float SelectionPenStrokeWidth = 15;
    private const float SyncPenStrokeWidth = 6;
    private const float RNotePenStrokeWidth = 17;
    
    public float NoteWidthMultiplier = 1;
    private float cursorWidthMultiplier = 1;
    private float selectionWidthMultiplier = 1;
    private float rNoteWidthMultiplier = 1;
    
    private SKColor colorNoteTouch;
    private SKColor colorNoteChain;
    private SKColor colorNoteSlideClockwise;
    private SKColor colorNoteSlideCounterclockwise;
    private SKColor colorNoteSnapForward;
    private SKColor colorNoteSnapBackward;
    private SKColor colorNoteHoldStart;
    private SKColor colorNoteHoldSegment;
    private SKColor colorNoteMaskAdd;
    private SKColor colorNoteMaskRemove;
    private SKColor colorNoteEndOfChart;
    private SKColor colorNoteCaps;

    private SKColor colorGimmickBpmChange;
    private SKColor colorGimmickTimeSigChange;
    private SKColor colorGimmickHiSpeedChange;
    private SKColor colorGimmickReverse;
    private SKColor colorGimmickStop;
    
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
        StrokeWidth = NotePenStrokeWidth,
    };

    private readonly SKPaint snapPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = SnapPenStrokeWidth
    };

    private readonly SKPaint swipeFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    private readonly SKPaint cursorPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = CursorPenStrokeWidth
    };

    private readonly SKPaint selectionPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = SelectionPenStrokeWidth
    };
    
    private readonly SKPaint highlightPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = SelectionPenStrokeWidth
    };
    
    private readonly SKPaint syncPen = new()
    {
        StrokeWidth = SyncPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    private readonly SKPaint holdEndPen = new()
    {
        StrokeWidth = HoldEndPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };

    private readonly SKPaint rNotePen = new()
    {
        StrokeWidth = RNotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    private readonly SKPaint bonusPen = new()
    {
        StrokeWidth = RNotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    // ________ Public Brushes
    public SKColor BackgroundColor = new(0xFF1A1A1A);

    public readonly SKPaint AngleTickPen = new()
    {
        StrokeWidth = TickPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
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
        StrokeWidth = NotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    public readonly SKPaint JudgementLineShadingPen = new()
    {
        StrokeWidth = NotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };
    
    public readonly SKPaint MaskFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false
    };

    public readonly SKPaint HoldFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false
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
        SKShader? shader = SKShader.CreateLinearGradient(startPoint, endPoint, gradient, SKShaderTileMode.Clamp);
        guideLinePen.Shader = shader;
        return guideLinePen;
    }

    public SKPaint GetNotePen(Note note, float scale)
    {
        notePen.StrokeWidth = NoteWidthMultiplier * scale;
        notePen.Color = NoteType2Color(note.NoteType);
        return notePen;
    }

    public SKPaint GetGimmickPen(Gimmick gimmick, float scale)
    {
        // Reusing NotePen again because why not.
        notePen.StrokeWidth = NoteWidthMultiplier * scale;
        notePen.Color = GimmickType2Color(gimmick.GimmickType);
        return notePen;
    }

    public SKPaint GetNoteCapPen(float scale)
    {
        // Rreusing Note pen because why not
        notePen.StrokeWidth = NoteWidthMultiplier * scale;
        notePen.Color = colorNoteCaps;
        return notePen;
    }

    public SKPaint GetSyncPen(float scale)
    {
        syncPen.StrokeWidth = SyncPenStrokeWidth * scale;
        return syncPen;
    }

    public SKPaint GetCursorPen(NoteType type, float scale)
    {
        cursorPen.StrokeWidth = cursorWidthMultiplier * scale;
        cursorPen.Color = NoteType2Color(type).WithAlpha(0x80);
        return cursorPen;
    }
    
    public SKPaint GetSelectionPen(float scale)
    {
        selectionPen.StrokeWidth = selectionWidthMultiplier * scale;
        return selectionPen;
    }

    public SKPaint GetHighlightPen(float scale)
    {
        highlightPen.StrokeWidth = selectionWidthMultiplier * scale;
        return highlightPen;
    }

    public SKPaint GetSnapPen(NoteType type, float scale)
    {
        snapPen.StrokeWidth = SnapPenStrokeWidth * scale;
        snapPen.Color = NoteType2Color(type);
        return snapPen;
    }

    public SKPaint GetSwipeFill(NoteType type)
    {
        swipeFill.Color = NoteType2Color(type);
        return swipeFill;
    }

    public SKPaint GetHoldEndPen(float scale)
    {
        holdEndPen.StrokeWidth = HoldEndPenStrokeWidth * scale;
        return holdEndPen;
    }

    public SKPaint GetRNotePen(float scale)
    {
        rNotePen.StrokeWidth = rNoteWidthMultiplier * scale;
        return rNotePen;
    }
    
    public SKPaint GetBonusPen(float scale)
    {
        bonusPen.StrokeWidth = rNoteWidthMultiplier * scale;
        return bonusPen;
    }
    
    // ________ Other
    private SKColor NoteType2Color(NoteType type)
    {
        return type switch
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
            NoteType.HoldSegment
                or NoteType.HoldEnd => colorNoteHoldSegment,
            NoteType.MaskAdd => colorNoteMaskAdd,
            NoteType.MaskRemove => colorNoteMaskRemove,
            NoteType.EndOfChart => colorNoteEndOfChart,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private SKColor GimmickType2Color(GimmickType type)
    {
        return type switch
        {
            GimmickType.None => SKColor.Empty,
            GimmickType.BpmChange => colorGimmickBpmChange,
            GimmickType.TimeSigChange => colorGimmickTimeSigChange,
            GimmickType.HiSpeedChange => colorGimmickHiSpeedChange,
            GimmickType.ReverseEffectStart
            or GimmickType.ReverseEffectEnd
            or GimmickType.ReverseNoteEnd => colorGimmickReverse,
            GimmickType.StopStart
            or GimmickType.StopEnd => colorGimmickStop,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    
    public void SetBrushes(SKPoint center, float radius, float maxRadius, float scale)
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
            colorNoteMaskAdd = SKColor.Parse(colors["ColorNoteMaskAdd"]);
            colorNoteMaskRemove = SKColor.Parse(colors["ColorNoteMaskRemove"]);
            colorNoteEndOfChart = SKColor.Parse(colors["ColorNoteEndOfChart"]);
            colorNoteCaps = SKColor.Parse(colors["ColorNoteCaps"]);
            
            colorGimmickBpmChange = SKColor.Parse(colors["ColorGimmickBpmChange"]);
            colorGimmickTimeSigChange = SKColor.Parse(colors["ColorGimmickTimeSigChange"]);
            colorGimmickHiSpeedChange = SKColor.Parse(colors["ColorGimmickHiSpeedChange"]);
            colorGimmickReverse = SKColor.Parse(colors["ColorGimmickStop"]);
            colorGimmickStop = SKColor.Parse(colors["ColorGimmickReverse"]);
            
            syncPen.Color = SKColor.Parse(colors["ColorSync"]);
            holdEndPen.Color = SKColor.Parse(colors["ColorNoteHoldEnd"]);
            rNotePen.Color = SKColor.Parse(colors["ColorRNote"]);
            bonusPen.Color = SKColor.Parse(colors["ColorBonus"]);
            selectionPen.Color = SKColor.Parse(colors["ColorSelection"]);
            highlightPen.Color = SKColor.Parse(colors["ColorHighlight"]);
            
            MeasurePen.Color = SKColor.Parse(colors["ColorMeasureLine"]);
            BeatPen.Color = SKColor.Parse(colors["ColorBeatLine"]);
            AngleTickPen.Color = SKColor.Parse(colors["ColorAngleTicks"]);
        
            MaskFill.Color = SKColor.Parse(colors["ColorBackgroundNoMask"]);
            tunnelStripesPen.Color = SKColor.Parse(colors["ColorBackgroundFar"]).WithAlpha(0x80);
        }

        void setStrokeWidths()
        {
            NoteWidthMultiplier = NotePenStrokeWidth * userConfig.RenderConfig.NoteSize;
            cursorWidthMultiplier = CursorPenStrokeWidth * userConfig.RenderConfig.NoteSize;
            selectionWidthMultiplier = SelectionPenStrokeWidth * userConfig.RenderConfig.NoteSize;
            rNoteWidthMultiplier = RNotePenStrokeWidth * userConfig.RenderConfig.NoteSize;
            JudgementLinePen.StrokeWidth = NotePenStrokeWidth * scale * userConfig.RenderConfig.NoteSize;
            JudgementLineShadingPen.StrokeWidth = NotePenStrokeWidth * scale * userConfig.RenderConfig.NoteSize;
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
            float shadingWidth = NoteWidthMultiplier * 0.5f;
            float shadingInside = (radius - shadingWidth) / maxRadius;
            float shadingCenter = radius / maxRadius;
            float shadingOutside = (radius + shadingWidth) / maxRadius;
            float[] shadingColorPos = [shadingInside, shadingCenter, shadingOutside];
            SKShader shadingShader = SKShader.CreateRadialGradient(center, maxRadius, judgementShadingColors, shadingColorPos, SKShaderTileMode.Clamp);
            JudgementLineShadingPen.Shader = shadingShader;
            
            SKColor tunnel0 = SKColor.Parse(colors["ColorBackgroundFar"]);
            SKColor tunnel1 = SKColor.Parse(colors["ColorBackgroundNear"]);
            SKColor[] tunnelGradient = [tunnel0, tunnel1];
            SKShader tunnelShader = SKShader.CreateRadialGradient(center, radius, tunnelGradient, SKShaderTileMode.Clamp);
            TunnelFill.Shader = tunnelShader;
            
            SKColor holdColor0 = SKColor.Parse(colors["ColorNoteHoldSurfaceFar"]);
            SKColor holdColor1 = SKColor.Parse(colors["ColorNoteHoldSurfaceNear"]);
            SKColor[] holdColors = [holdColor0, holdColor1];
            SKShader? holdShader = SKShader.CreateRadialGradient(center, radius, holdColors, SKShaderTileMode.Clamp);
            HoldFill.Shader = holdShader;
        }
    }
}