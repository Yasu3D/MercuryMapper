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
    private const float CursorPenStrokeWidth = 15;
    private const float SelectionPenStrokeWidth = 11.25f;
    private const float SyncPenStrokeWidth = 6;
    private const float RNotePenStrokeWidth = 17;
    private const float BoxSelectOutlinePenStrokeWidth = 4;
    private const float PeerPenStrokeWidth = 15;
    private const float TracePenStrokeWidth = 4;
    
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
    private SKColor colorNoteHoldSegmentNoRender;
    private SKColor colorNoteTrace;
    private SKColor colorNoteTraceNoRender;
    private SKColor colorNoteDamage;
    private SKColor colorNoteMaskAdd;
    private SKColor colorNoteMaskRemove;
    private SKColor colorNoteCaps;
    
    private SKColor colorTraceWhite;
    private SKColor colorTraceBlack;
    private SKColor colorTraceRed;
    private SKColor colorTraceOrange;
    private SKColor colorTraceYellow;
    private SKColor colorTraceLime;
    private SKColor colorTraceGreen;
    private SKColor colorTraceSky;
    private SKColor colorTraceBlue;
    private SKColor colorTraceViolet;
    private SKColor colorTracePink;
    
    private SKColor colorGimmickBpmChange;
    private SKColor colorGimmickTimeSigChange;
    private SKColor colorGimmickHiSpeedChange;
    private SKColor colorGimmickReverse;
    private SKColor colorGimmickStop;
    private SKColor colorGimmickEndOfChart;
    
    // ________ Private Brushes
    private readonly SKPaint guideLinePen = new()
    {
        StrokeWidth = GuidelinePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    private readonly SKPaint tunnelStripesPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };

    private readonly SKPaint notePen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = NotePenStrokeWidth,
    };

    private readonly SKPaint snapFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    private readonly SKPaint swipeFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    private readonly SKPaint cursorPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = CursorPenStrokeWidth,
    };

    private readonly SKPaint boxSelectCursorPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = CursorPenStrokeWidth,
    };

    private readonly SKPaint boxSelectOutlinePen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = BoxSelectOutlinePenStrokeWidth,
    };

    private readonly SKPaint selectionPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = SelectionPenStrokeWidth,
    };
    
    private readonly SKPaint highlightPen = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeWidth = SelectionPenStrokeWidth,
    };
    
    private readonly SKPaint syncPen = new()
    {
        StrokeWidth = SyncPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    private readonly SKPaint holdEndPen = new()
    {
        StrokeWidth = HoldEndPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
    };

    private readonly SKPaint rNotePen = new()
    {
        StrokeWidth = RNotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    private readonly SKPaint bonusPen = new()
    {
        StrokeWidth = RNotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };

    private readonly SKPaint peerPen = new()
    {
        StrokeWidth = PeerPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };

    private readonly SKPaint traceFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };

    private readonly SKPaint tracePen = new()
    {
        StrokeWidth = SyncPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    // ________ Public Brushes
    public SKColor BackgroundColor = new(0xFF1A1A1A);

    public readonly SKPaint AngleTickPen = new()
    {
        StrokeWidth = TickPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    public readonly SKPaint MeasurePen = new()
    {
        StrokeWidth = MeasurePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };

    public readonly SKPaint BeatPen = new()
    {
        StrokeWidth = BeatPenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    public readonly SKPaint TunnelFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
        IsDither = true,
    };

    public readonly SKPaint JudgementLinePen = new()
    {
        StrokeWidth = NotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };

    public readonly SKPaint JudgementLineShadingPen = new()
    {
        StrokeWidth = NotePenStrokeWidth,
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
    };
    
    public readonly SKPaint MaskFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };

    public readonly SKPaint HoldFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    
    public readonly SKPaint ChainStripeFill = new()
    {
        Color = SKColors.Black.WithAlpha(0x35),
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    public readonly SKPaint BoxSelectFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    public readonly SKPaint BonusFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    
    public readonly SKPaint TraceCenterFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    
    public readonly SKPaint JudgementMarvelousPen = new()
    {
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
    };
    
    public readonly SKPaint JudgementGreatPen = new()
    {
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
    };
    
    public readonly SKPaint JudgementGoodPen = new()
    {
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
    };
    
    public readonly SKPaint JudgementMarvelousFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    
    public readonly SKPaint JudgementGreatFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
    };
    
    public readonly SKPaint JudgementGoodFill = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false,
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
        notePen.Color = NoteType2Color(note.NoteType, note.LinkType, note.RenderSegment);
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

    public SKPaint GetCursorPen(NoteType noteType, NoteLinkType linkType, float scale)
    {
        cursorPen.StrokeWidth = cursorWidthMultiplier * scale;
        cursorPen.Color = NoteType2Color(noteType, linkType).WithAlpha(0x80);
        return cursorPen;
    }

    public SKPaint GetBoxSelectCursorPen(float scale)
    {
        boxSelectCursorPen.StrokeWidth = cursorWidthMultiplier * 0.75f * scale;
        return boxSelectCursorPen;
    }

    public SKPaint GetBoxSelectOutlinePen(float scale)
    {
        boxSelectOutlinePen.StrokeWidth = BoxSelectOutlinePenStrokeWidth * scale;
        return boxSelectOutlinePen;
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

    public SKPaint GetSnapFill(NoteType noteType, NoteLinkType linkType)
    {
        snapFill.Color = NoteType2Color(noteType, linkType);
        return snapFill;
    }

    public SKPaint GetSwipeFill(NoteType noteType, NoteLinkType linkType)
    {
        swipeFill.Color = NoteType2Color(noteType, linkType);
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

    public SKPaint GetPeerPen(SKColor color, float scale)
    {
        peerPen.StrokeWidth = PeerPenStrokeWidth * scale;
        peerPen.Color = color;
        return peerPen;
    }

    public SKPaint GetTraceFill(TraceColor color)
    {
        traceFill.Color = TraceColor2Color(color);
        return traceFill;
    }

    public SKPaint GetTracePen(bool render, float scale)
    {
        tracePen.StrokeWidth = TracePenStrokeWidth * scale;
        tracePen.Color = render ? colorNoteTrace : colorNoteTraceNoRender;
        return tracePen;
    }
    
    // ________ Other
    private SKColor NoteType2Color(NoteType noteType, NoteLinkType linkType, bool render = true)
    {
        return (noteType, linkType) switch
        {
            (NoteType.Touch, _) => colorNoteTouch,
            (NoteType.SnapForward, _) => colorNoteSnapForward,
            (NoteType.SnapBackward, _) => colorNoteSnapBackward,
            (NoteType.SlideClockwise, _) => colorNoteSlideClockwise,
            (NoteType.SlideCounterclockwise, _) => colorNoteSlideCounterclockwise,
            (NoteType.Chain, _) => colorNoteChain,
            (NoteType.Hold, NoteLinkType.Start) => colorNoteHoldStart,
            (NoteType.Hold, NoteLinkType.Point) => render ? colorNoteHoldSegment : colorNoteHoldSegmentNoRender,
            (NoteType.Hold, NoteLinkType.End) => colorNoteHoldSegment,
            (NoteType.Hold, _) => colorNoteHoldStart,
            (NoteType.Trace, _) => colorNoteTrace,
            (NoteType.Damage, _) => colorNoteDamage,
            (NoteType.MaskAdd, _) => colorNoteMaskAdd,
            (NoteType.MaskRemove, _) => colorNoteMaskRemove,
            _ => throw new ArgumentOutOfRangeException(),
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
            GimmickType.EndOfChart => colorGimmickEndOfChart,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private SKColor TraceColor2Color(TraceColor color)
    {
        return color switch
        {
            TraceColor.White => colorTraceWhite,
            TraceColor.Black => colorTraceBlack,
            TraceColor.Red => colorTraceRed,
            TraceColor.Orange => colorTraceOrange,
            TraceColor.Yellow => colorTraceYellow,
            TraceColor.Lime => colorTraceLime,
            TraceColor.Green => colorTraceGreen,
            TraceColor.Sky => colorTraceSky,
            TraceColor.Blue => colorTraceBlue,
            TraceColor.Violet => colorTraceViolet,
            TraceColor.Pink => colorTracePink,
            _ => colorTraceWhite,
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
            colorNoteHoldSegmentNoRender = SKColor.Parse(colors["ColorNoteHoldSegmentNoRender"]);
            colorNoteTrace = SKColor.Parse(colors["ColorNoteTrace"]);
            colorNoteTraceNoRender = SKColor.Parse(colors["ColorNoteTraceNoRender"]);
            colorNoteDamage = SKColor.Parse(colors["ColorNoteDamage"]);
            colorNoteMaskAdd = SKColor.Parse(colors["ColorNoteMaskAdd"]);
            colorNoteMaskRemove = SKColor.Parse(colors["ColorNoteMaskRemove"]);
            colorGimmickEndOfChart = SKColor.Parse(colors["ColorNoteEndOfChart"]);
            colorNoteCaps = SKColor.Parse(colors["ColorNoteCaps"]);
            
            TraceCenterFill.Color = SKColor.Parse(colors["ColorTraceCenter"]);
            colorTraceWhite = SKColor.Parse(colors["ColorTraceWhite"]);
            colorTraceBlack = SKColor.Parse(colors["ColorTraceBlack"]);
            colorTraceRed = SKColor.Parse(colors["ColorTraceRed"]);
            colorTraceOrange = SKColor.Parse(colors["ColorTraceOrange"]);
            colorTraceYellow = SKColor.Parse(colors["ColorTraceYellow"]);
            colorTraceLime = SKColor.Parse(colors["ColorTraceLime"]);
            colorTraceGreen = SKColor.Parse(colors["ColorTraceGreen"]);
            colorTraceSky = SKColor.Parse(colors["ColorTraceSky"]);
            colorTraceBlue = SKColor.Parse(colors["ColorTraceBlue"]);
            colorTraceViolet = SKColor.Parse(colors["ColorTraceViolet"]);
            colorTracePink = SKColor.Parse(colors["ColorTracePink"]);
            
            colorGimmickBpmChange = SKColor.Parse(colors["ColorGimmickBpmChange"]);
            colorGimmickTimeSigChange = SKColor.Parse(colors["ColorGimmickTimeSigChange"]);
            colorGimmickHiSpeedChange = SKColor.Parse(colors["ColorGimmickHiSpeedChange"]);
            colorGimmickReverse = SKColor.Parse(colors["ColorGimmickStop"]);
            colorGimmickStop = SKColor.Parse(colors["ColorGimmickReverse"]);

            JudgementMarvelousFill.Color = SKColor.Parse(colors["ColorJudgementMarvelous"]).WithAlpha(0xAA);
            JudgementGreatFill.Color = SKColor.Parse(colors["ColorJudgementGreat"]).WithAlpha(0xAA);
            JudgementGoodFill.Color = SKColor.Parse(colors["ColorJudgementGood"]).WithAlpha(0xAA);
            JudgementMarvelousPen.Color = SKColor.Parse(colors["ColorJudgementMarvelous"]).WithAlpha(0xFF);
            JudgementGreatPen.Color = SKColor.Parse(colors["ColorJudgementGreat"]).WithAlpha(0xFF);
            JudgementGoodPen.Color = SKColor.Parse(colors["ColorJudgementGood"]).WithAlpha(0xFF);
            
            BonusFill.Color = SKColor.Parse(colors["ColorBonusFill"]);
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

            boxSelectOutlinePen.Color = SKColor.Parse(colors["ColorSelection"]).WithAlpha(0xFF);
            boxSelectCursorPen.Color = SKColor.Parse(colors["ColorSelection"]).WithAlpha(0x80);
            BoxSelectFill.Color = SKColor.Parse(colors["ColorSelection"]).WithAlpha(0x20);
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