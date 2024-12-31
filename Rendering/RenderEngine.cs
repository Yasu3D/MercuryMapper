using System;
using System.Collections.Generic;
using System.Linq;
using MercuryMapper.Config;
using MercuryMapper.Data;
using MercuryMapper.Editor;
using MercuryMapper.Enums;
using MercuryMapper.MultiCharting;
using MercuryMapper.Utils;
using MercuryMapper.Views;
using SkiaSharp;
using static MercuryMapper.Rendering.RenderMath;

namespace MercuryMapper.Rendering;

/// <summary>
/// SkiaSharp witchcraft. I have NO clue how I managed to do this.
/// </summary>
public class RenderEngine(MainView mainView)
{
    private readonly MainView mainView = mainView;
    private readonly Brushes brushes = new(mainView.UserConfig);
    private Chart Chart => mainView.ChartEditor.Chart;
    private BoxSelect BoxSelect => mainView.ChartEditor.BoxSelect;

    private SKPoint canvasCenter;
    private SKRect canvasRect;
    private SKRect canvasMaxRect;
    private float canvasRadius;
    private float canvasMaxRadius;
    private float canvasScale;

    private bool IsPlaying => mainView.AudioManager.CurrentSong is { IsPlaying: true };
    private float visibleDistanceMeasureDecimal;
    private float CurrentMeasureDecimal => mainView.ChartEditor.CurrentMeasureDecimal;
    private RenderConfig RenderConfig => mainView.UserConfig.RenderConfig;
    private readonly Random random = new();

    public bool IsHoveringOverMirrorAxis { get; set; }
    public int MirrorAxis { get; set; } = 30;

    public SKPoint PointerPosition;

    public void Render(SKCanvas canvas)
    {
        DrawBackground(canvas);
        DrawGuideLines(canvas, RenderConfig.GuideLineType);
        DrawJudgementLine(canvas);
        DrawMaskEffect(canvas);
        
        if (!IsPlaying)
        {
            if (mainView.ChartEditor.EditorState is ChartEditorState.InsertNote or ChartEditorState.InsertHold)
            {
                DrawCursor(canvas, mainView.ChartEditor.CurrentNoteType, mainView.ChartEditor.Cursor.Position, mainView.ChartEditor.Cursor.Size);
            }
                
            DrawAngleTicks(canvas);
            if (IsHoveringOverMirrorAxis) DrawMirrorAxis(canvas, MirrorAxis);
        }

        if (mainView.ChartEditor.EditorState is ChartEditorState.BoxSelectStart) DrawBoxSelectCursor(canvas, mainView.ChartEditor.Cursor.Position, mainView.ChartEditor.Cursor.Size);

        if (mainView.ChartEditor.EditorState is ChartEditorState.BoxSelectEnd) DrawBoxSelectArea(canvas, Chart);
        
        DrawMeasureLines(canvas, Chart);
        
        if (!IsPlaying || (IsPlaying && RenderConfig.ShowOtherUsersDuringPlayback)) DrawPeers(canvas, Chart);
        
        if (mainView.ChartEditor.LayerGimmickActive && (!IsPlaying || (IsPlaying && RenderConfig.ShowGimmickNotesDuringPlayback))) DrawGimmickMarkers(canvas, Chart);
        if (mainView.ChartEditor.LayerMaskActive && (!IsPlaying || (IsPlaying && RenderConfig.ShowMaskDuringPlayback))) DrawMaskNotes(canvas, Chart);
        
        if (mainView.ChartEditor.LayerNoteActive && (RenderConfig.ShowJudgementWindowMarvelous || RenderConfig.ShowJudgementWindowGreat || RenderConfig.ShowJudgementWindowGood))
        {
            DrawJudgementWindows(canvas, Chart);
        }

        if (mainView.ChartEditor.LayerNoteActive)
        {
            if (RenderConfig.HoldRenderMethod == 0) DrawHolds(canvas, Chart);
            else DrawHoldsLegacy(canvas, Chart);
        }

        if (mainView.ChartEditor.LayerTraceActive) DrawTraces(canvas, Chart);
            
        if (mainView.ChartEditor.LayerNoteActive) DrawNotes(canvas, Chart);
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
        // A Note scrolling from its spawn point to the judgement line at NoteSpeed 1.0 takes
        // approximately 3266.667 milliseconds.
        float visibleTime = 3266.667f / (float)RenderConfig.NoteSpeed;
        visibleDistanceMeasureDecimal = Chart.Timestamp2MeasureDecimal(visibleTime);
    }
    
    // ________________

    public float GetMeasureDecimalAtPointer(Chart chart, SKPoint point, ScrollLayer scrollLayer)
    {
        float clickRadius = (1 - MathExtensions.InversePerspective(point.Length)) * visibleDistanceMeasureDecimal;
        return chart.GetUnscaledMeasureDecimal(clickRadius + ScaledCurrentMeasureDecimal(scrollLayer), RenderConfig.ShowHiSpeed, scrollLayer);
    }
    
    public ChartElement? GetChartElementAtPointer(Chart chart, SKPoint point, bool includeGimmicks, bool layerNote, bool layerMask, bool layerGimmick)
    {
        int clickPosition = MathExtensions.GetThetaNotePosition(point.X, point.Y);
        float measureDecimal = GetMeasureDecimalAtPointer(chart, point, ScrollLayer.L0); // TODO: REWORK
        
        // Holy mother of LINQ
        List<Note> clickedNotes = chart.Notes
            .Where(x => Math.Abs(x.BeatData.MeasureDecimal - measureDecimal) < 0.008f)
            .Select(note =>
            {
                float center = MathExtensions.Modulo(note.Position + note.Size * 0.5f, 60);
                float distance = float.Min(float.Abs(center - clickPosition), 60 - float.Abs(center - clickPosition));
                return new { Note = note, Distance = distance };
            })
            .Where(item => item.Distance <= item.Note.Size * 0.5f)
            .OrderBy(item => item.Note.NoteType is NoteType.Hold or NoteType.Trace)
            .ThenBy(item => item.Distance)
            .Select(item => item.Note)
            .ToList();

        List<Gimmick> clickedGimmicks = chart.Gimmicks.Where(x => Math.Abs(x.BeatData.MeasureDecimal - measureDecimal) < 0.005f).ToList();

        if (layerGimmick && includeGimmicks && clickedGimmicks.Count > 0) return clickedGimmicks[0];
        if (layerMask && !layerNote) return clickedNotes.FirstOrDefault(x => x.IsMask);
        if (!layerMask && layerNote) return clickedNotes.FirstOrDefault(x => !x.IsMask);
        
        return clickedNotes.FirstOrDefault();
    }
    
    // ________________
    private float GetNoteScale(Chart chart, float measureDecimal, ScrollLayer scrollLayer)
    {
        float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(measureDecimal, RenderConfig.ShowHiSpeed, scrollLayer);
        float scale = 1 - (scaledMeasureDecimal - ScaledCurrentMeasureDecimal(scrollLayer)) / visibleDistanceMeasureDecimal;
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
        float scale = GetNoteScale(chart, note.BeatData.MeasureDecimal, note.ScrollLayer);
        SKRect rect = GetRect(scale);

        return new(rect, scale, startAngle, sweepAngle);
    }

    private ArcData GetArc(Chart chart, Gimmick gimmick)
    {
        float scale = GetNoteScale(chart, gimmick.BeatData.MeasureDecimal, gimmick.ScrollLayer);
        SKRect rect = GetRect(scale);

        return new(rect, scale, 0, 360);
    }

    private ArcData GetTruncatedArc(Chart chart, Note note, TruncateMode mode)
    {
        ArcData arc = GetArc(chart, note);
        if (note.Size != 60) TruncateArc(ref arc, mode);
        else TrimCircleArc(ref arc);

        return arc;
    }
    
    private enum TruncateMode
    {
        ExcludeCaps,
        IncludeCaps,
        Hold,
        OutlineNote,
        OutlineHoldSegment,
        OutlineFull,
        Trace,
        TraceCenter,
    }
    
    private static void TruncateArc(ref ArcData data, TruncateMode mode)
    {
        switch (mode)
        {
            case TruncateMode.ExcludeCaps:
            {
                data.StartAngle -= 6f;
                data.SweepAngle += 12f;
                break;
            }
        
            case TruncateMode.IncludeCaps:
            {
                data.StartAngle -= 4.5f;
                data.SweepAngle += 9f;
                break;
            }
            
            case TruncateMode.Hold:
            {
                data.StartAngle -= 4f;
                data.SweepAngle += 8f;
                break;
            }
            
            case TruncateMode.OutlineNote:
            {
                data.StartAngle -= 3.5f;
                data.SweepAngle += 7f;
                break;
            }
            
            case TruncateMode.OutlineHoldSegment:
            {
                data.StartAngle -= 2.5f;
                data.SweepAngle += 5f;
                break;
            }

            case TruncateMode.OutlineFull:
            {
                data.StartAngle += 1f;
                data.SweepAngle -= 2f;
                break;
            }
            
            case TruncateMode.Trace:
            {
                data.StartAngle -= 4.25f;
                data.SweepAngle += 8.5f;
                break;
            }

            case TruncateMode.TraceCenter:
            {
                data.StartAngle -= 5.15f;
                data.SweepAngle += 10.3f;
                break;
            }
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

    private bool ElementOnSameLayer(ChartElement element) => !RenderConfig.HideNotesOnDifferentLayers || element.ScrollLayer == mainView.ChartEditor.CurrentScrollLayer;

    private float ScaledCurrentMeasureDecimal(ScrollLayer scrollLayer) => Chart.GetScaledMeasureDecimal(CurrentMeasureDecimal, RenderConfig.ShowHiSpeed, scrollLayer);

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
            SKPoint startPoint = GetPointOnArc(canvasCenter, canvasRadius, i);
            SKPoint endPoint = GetPointOnArc(canvasCenter, innerRadius, i);

            canvas.DrawLine(startPoint, endPoint, brushes.GetGuideLinePen(startPoint, endPoint));
        }
    }

    private void DrawMaskEffect(SKCanvas canvas)
    {
        if (mainView.AudioManager.CurrentSong is null) return;
        
        bool[] maskState = new bool[60];
        foreach (Note note in Chart.Notes.Where(x => x.IsMask && x.MaskDirection != MaskDirection.None))
        {
            if (note.BeatData.MeasureDecimal > CurrentMeasureDecimal) break;

            float maskTimestamp = Chart.MeasureDecimal2Timestamp(note.BeatData.MeasureDecimal);
            float timeDifference = mainView.AudioManager.CurrentSong.Position - maskTimestamp;
            float animationDuration = note.Size * (note.MaskDirection is MaskDirection.Center ? 4 : 8); // 8ms per unit. 4ms if its from center since notes are basically cut in half.
            
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
            SKPoint startPoint = GetPointOnArc(canvasCenter, canvasRadius + brushes.NoteWidthMultiplier * 0.5f, i * 6);
            SKPoint endPoint;
            float tickLength = canvasRadius / 14.25f;

            if (i % 15 == 0)
            {
                endPoint = GetPointOnArc(canvasCenter, canvasRadius - tickLength * 3.5f, i * 6);
            }
            else if (i % 5 == 0)
            {
                endPoint = GetPointOnArc(canvasCenter, canvasRadius - tickLength * 2.5f, i * 6);
            }
            else
            {
                endPoint = GetPointOnArc(canvasCenter, canvasRadius - tickLength, i * 6);
            }
            
            canvas.DrawLine(startPoint, endPoint, brushes.AngleTickPen);
        }
    }

    private void DrawCursor(SKCanvas canvas, NoteType noteType, int position, int size)
    {
        canvas.DrawArc(canvasRect, position * -6, size * -6, false, brushes.GetCursorPen(noteType, NoteLinkType.Unlinked, canvasScale));
    }

    private void DrawBoxSelectCursor(SKCanvas canvas, int position, int size)
    {
        canvas.DrawArc(canvasRect, position * -6, size * -6, false, brushes.GetBoxSelectCursorPen(canvasScale));
    }

    private void DrawBoxSelectArea(SKCanvas canvas, Chart chart)
    {
        if (BoxSelect.SelectionStart is null) return;

        float selectionStartScale = BoxSelect.SelectionStart.MeasureDecimal <= CurrentMeasureDecimal ? 1 : GetNoteScale(chart, BoxSelect.SelectionStart.MeasureDecimal, ScrollLayer.L0);
        SKRect selectionStartRect = GetRect(selectionStartScale);
        
        float selectionEndScale = float.Min(1, GetNoteScale(chart, GetMeasureDecimalAtPointer(chart, PointerPosition, ScrollLayer.L0), ScrollLayer.L0));
        SKRect selectionEndRect = GetRect(selectionEndScale);

        float startAngle = BoxSelect.Position * -6;
        float sweepAngle = BoxSelect.Size == 60 ? 359.99f : BoxSelect.Size * -6;
        
        SKPath path = new();

        if (selectionStartScale <= 0)
        {
            path.MoveTo(canvasCenter);
        }
        else
        {
            path.ArcTo(selectionStartRect, startAngle, sweepAngle, true);
        }

        if (BoxSelect.Size != 60)
        {
            path.ArcTo(selectionEndRect, startAngle + sweepAngle, -sweepAngle, false);
            path.Close();
        }
        else
        {
            path.ArcTo(selectionEndRect, startAngle + sweepAngle, -sweepAngle, true);
        }
        
        canvas.DrawPath(path, brushes.BoxSelectFill);
        canvas.DrawPath(path, brushes.GetBoxSelectOutlinePen(canvasScale));
    }
    
    private void DrawMirrorAxis(SKCanvas canvas, int axis)
    {
        int angle = -axis * 3;
        SKPoint p0 = GetPointOnArc(canvasCenter, canvasMaxRadius, angle);
        SKPoint p1 = GetPointOnArc(canvasCenter, canvasMaxRadius, angle + 180);
        
        canvas.DrawLine(p0, p1, brushes.AngleTickPen);
    }

    private void DrawPeers(SKCanvas canvas, Chart chart)
    {
        foreach (KeyValuePair<int, Peer> peer in mainView.PeerManager.Peers)
        {
            float measureDecimal = chart.Timestamp2MeasureDecimal(peer.Value.Timestamp);
            if (!MathExtensions.GreaterAlmostEqual(measureDecimal, CurrentMeasureDecimal) || chart.GetScaledMeasureDecimal(measureDecimal, RenderConfig.ShowHiSpeed, ScrollLayer.L0) > ScaledCurrentMeasureDecimal(ScrollLayer.L0) + visibleDistanceMeasureDecimal) return;
            
            float scale = GetNoteScale(chart, measureDecimal, ScrollLayer.L0);
            SKRect rect = GetRect(scale);
            
            canvas.DrawOval(rect, brushes.GetPeerPen(peer.Value.SkiaColor, scale));
        }
    }
    
    // ____ NOTES
    private void DrawMeasureLines(SKCanvas canvas, Chart chart)
    {
        float interval = 1.0f / RenderConfig.BeatDivision;
        float start = MathF.Ceiling(CurrentMeasureDecimal * RenderConfig.BeatDivision) * interval;
        float end = ScaledCurrentMeasureDecimal(ScrollLayer.L0) + visibleDistanceMeasureDecimal;
        
        for (float i = start; chart.GetScaledMeasureDecimal(i, RenderConfig.ShowHiSpeed, ScrollLayer.L0) < end; i += interval)
        {
            TimeScaleData? timeScaleData = chart.BinarySearchTimeScales(i, ScrollLayer.L0);
            if (timeScaleData == null) break;
            if (timeScaleData.SpeedMultiplier < 0.001f && timeScaleData.IsLast) break;
                
            float scale = GetNoteScale(chart, i, ScrollLayer.L0);
            SKRect rect = GetRect(scale);
            if (!InRange(scale)) continue;

            bool isMeasure = Math.Abs(i - (int)i) < 0.01f;
            canvas.DrawOval(rect, isMeasure ? brushes.MeasurePen : brushes.BeatPen);
        }
    }
    
    private void DrawGimmickMarkers(SKCanvas canvas, Chart chart)
    {
        IEnumerable<Gimmick> visibleGimmicks = chart.Gimmicks.Where(x =>
            MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && ElementOnSameLayer(x)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) <= ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal).Reverse();

        foreach (Gimmick gimmick in visibleGimmicks)
        {
            ArcData data = GetArc(chart, gimmick);
            
            if (!InRange(data.Scale)) continue;
            
            canvas.DrawOval(data.Rect, brushes.GetGimmickPen(gimmick, canvasScale * data.Scale));
            if (gimmick == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, data);
        }
    }
    
    private void DrawMaskNotes(SKCanvas canvas, Chart chart)
    {
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x =>
            x.IsMask
            && MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) <= ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal).Reverse();
        
        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);

            if (!InRange(data.Scale)) continue;
            
            canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));

            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }
    }
    
    private void DrawTraces(SKCanvas canvas, Chart chart)
    {
        List<Note> visibleNotes = chart.Notes.Where(x =>
        {
            Note? next = x.NextVisibleReference(RenderConfig.DrawNoRenderSegments);
            
            float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer);
            float visibleDistance = ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal;

            bool inFrontOfCamera = MathExtensions.GreaterAlmostEqual(scaledMeasureDecimal, ScaledCurrentMeasureDecimal(x.ScrollLayer));
            bool inVisibleDistance = MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal) && scaledMeasureDecimal <= visibleDistance;
            bool nextOutsideVisibleDistance = next != null && chart.GetScaledMeasureDecimal(next.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) > visibleDistance;

            bool inVisionRange = inFrontOfCamera && inVisibleDistance;
            bool aroundVisionRange = !inFrontOfCamera && nextOutsideVisibleDistance;

            return x.NoteType == NoteType.Trace && (inVisionRange || aroundVisionRange) && ElementOnSameLayer(x);
        }).ToList();

        HashSet<Note> checkedNotes = [];
        List<NoteCollection> traces = [];

        foreach (Note note in visibleNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            NoteCollection noteCollection = new();

            foreach (Note reference in note.References())
            {
                if (visibleNotes.Contains(reference) && ((reference.RenderSegment && !RenderConfig.DrawNoRenderSegments) || RenderConfig.DrawNoRenderSegments))
                {
                    noteCollection.Notes.Add(reference);
                }
                checkedNotes.Add(reference);
            }

            traces.Add(noteCollection);
        }

        foreach (NoteCollection trace in traces)
        {
            DrawNoteCollectionSurface(canvas, chart, trace, TruncateMode.Trace, CollectionSurfaceDrawMode.Trace);
            DrawNoteCollectionSurface(canvas, chart, trace, TruncateMode.TraceCenter, CollectionSurfaceDrawMode.TraceCenter);
        }

        if (IsPlaying) return;
        
        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);
            
            if (!InRange(data.Scale) || note.BeatData.MeasureDecimal < CurrentMeasureDecimal) continue;
            
            float outlineOffset = 10 * data.Scale * canvasScale;
            const float controlOffset = 1;
                
            SKPath path1 = new();

            SKRect rectOuter = new(data.Rect.Left, data.Rect.Top, data.Rect.Right, data.Rect.Bottom);
            SKRect rectInner = new(data.Rect.Left, data.Rect.Top, data.Rect.Right, data.Rect.Bottom);
            rectOuter.Inflate(outlineOffset, outlineOffset);
            rectInner.Inflate(-outlineOffset, -outlineOffset);
                
            float currentPos = note.Position * -6 - 3.5f;
            float currentSweep = note.Size * -6 + 7;

            SKPoint control1 = GetPointOnArc(canvasCenter, data.Rect.Width * 0.5f, currentPos + currentSweep - controlOffset);
            SKPoint arcEdge1 = GetPointOnArc(canvasCenter, data.Rect.Width * 0.5f - outlineOffset, currentPos + currentSweep);
                
            SKPoint control2 = GetPointOnArc(canvasCenter, data.Rect.Width * 0.5f, currentPos + controlOffset);
            SKPoint arcEdge2 = GetPointOnArc(canvasCenter, data.Rect.Width * 0.5f + outlineOffset, currentPos);

            path1.ArcTo(rectOuter, currentPos, currentSweep, true);
            path1.QuadTo(control1, arcEdge1);
            path1.ArcTo(rectInner, currentPos + currentSweep, -currentSweep, false);
            path1.QuadTo(control2, arcEdge2);
            path1.Close();

            canvas.DrawPath(path1, brushes.GetTracePen(note.RenderSegment, data.Scale * canvasScale * 0.75f));
            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }
    }
    
    /// <summary>
    /// Performs better than the original by drawing hold-by-hold, not segment-by-segment. Saves dozens, if not hundreds of calls to SkiaSharp and fixes the tv-static-looking seams on dense holds. <br/>
    /// This first batches notes into a "hold" struct, then draws each hold.
    /// </summary>
    private void DrawHolds(SKCanvas canvas, Chart chart)
    {
        List<Note> visibleNotes = chart.Notes.Where(x =>
        {
            Note? next = x.NextVisibleReference(RenderConfig.DrawNoRenderSegments);
            
            float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer);
            float visibleDistance = ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal;

            bool inFrontOfCamera = MathExtensions.GreaterAlmostEqual(scaledMeasureDecimal, ScaledCurrentMeasureDecimal(x.ScrollLayer));
            bool inVisibleDistance = MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal) && scaledMeasureDecimal <= visibleDistance;
            bool nextOutsideVisibleDistance = next != null && chart.GetScaledMeasureDecimal(next.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) > visibleDistance;

            bool inVisionRange = inFrontOfCamera && inVisibleDistance;
            bool aroundVisionRange = !inFrontOfCamera && nextOutsideVisibleDistance;

            return x.NoteType == NoteType.Hold && (inVisionRange || aroundVisionRange) && ElementOnSameLayer(x);
        }).ToList();

        HashSet<Note> checkedNotes = [];
        List<NoteCollection> holds = [];

        foreach (Note note in visibleNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            NoteCollection noteCollection = new();

            foreach (Note reference in note.References())
            {
                if (visibleNotes.Contains(reference) && ((reference.RenderSegment && !RenderConfig.DrawNoRenderSegments) || RenderConfig.DrawNoRenderSegments))
                {
                    noteCollection.Notes.Add(reference);
                }
                checkedNotes.Add(reference);
            }

            holds.Add(noteCollection);
        }

        foreach (NoteCollection hold in holds)
        {
            DrawNoteCollectionSurface(canvas, chart, hold, TruncateMode.Hold, CollectionSurfaceDrawMode.Hold);
        }
    }
    
    /// <summary>
    /// Old Hold Rendering Method. See above for new method.
    /// </summary>
    private void DrawHoldsLegacy(SKCanvas canvas, Chart chart)
    {
        List<Note> visibleNotes = chart.Notes.Where(x =>
        {
            bool inVision = MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal) && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) <= ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal;
            bool nextOutsideVision = x.NextReferencedNote != null && x.BeatData.MeasureDecimal < CurrentMeasureDecimal && chart.GetScaledMeasureDecimal(x.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) > ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal;

            return x.NoteType == NoteType.Hold && (inVision || nextOutsideVision) && ElementOnSameLayer(x);
        }).ToList();

        foreach (Note note in visibleNotes)
        {
            ArcData currentData = GetArc(chart, note);
            if (note.Size != 60) TruncateArc(ref currentData, TruncateMode.IncludeCaps);
            else TrimCircleArc(ref currentData);

            bool currentVisible = note.BeatData.MeasureDecimal >= CurrentMeasureDecimal;
            bool nextVisible = note.NextReferencedNote != null && chart.GetScaledMeasureDecimal(note.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, note.ScrollLayer) <= ScaledCurrentMeasureDecimal(note.ScrollLayer) + visibleDistanceMeasureDecimal;
            bool prevVisible = note.PrevReferencedNote != null && note.PrevReferencedNote.BeatData.MeasureDecimal >= CurrentMeasureDecimal;

            if (currentVisible && nextVisible)
            {
                Note nextNote = note.NextReferencedNote!;
                ArcData nextData = GetArc(chart, nextNote);

                if (nextNote.Size != 60) TruncateArc(ref nextData, TruncateMode.IncludeCaps);
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

                if (prevNote.Size != 60) TruncateArc(ref prevData, TruncateMode.IncludeCaps);
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

                if (nextNote.Size != 60) TruncateArc(ref nextData, TruncateMode.IncludeCaps);
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
    }
    
    private void DrawNotes(SKCanvas canvas, Chart chart)
    {
        Note[] visibleNotes = chart.Notes.Where(x =>
        {
            bool behindCamera = !MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal);
            bool pastVisionLimit = chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, x.ScrollLayer) > ScaledCurrentMeasureDecimal(x.ScrollLayer) + visibleDistanceMeasureDecimal;

            return x.IsNote && !behindCamera && !pastVisionLimit && ElementOnSameLayer(x);
        }).ToArray();
        
        Note[] syncNotes = visibleNotes.Where(x => !x.IsSegment).Reverse().ToArray();
        Note[] renderedNotes = visibleNotes.OrderBy(x => x.LinkType == NoteLinkType.End).ThenBy(x => x.NoteType == NoteType.Hold).ThenBy(x => x.BeatData.FullTick).Reverse().ToArray();

        // Layer 1 // Bonus & R-Note Glow
        foreach (Note note in renderedNotes)
        {
            ArcData data = GetArc(chart, note);
            if (!InRange(data.Scale)) continue;
            
            // R-Note Glow
            if (note.IsRNote)
            {
                DrawRNote(canvas, note, data);
            }

            // Bonus Glow
            if (note.IsBonus)
            {
                DrawBonusGlow(canvas, note, data);
            }
        }
        
        // Layer 2 // Sync
        for (int i = 0; i < syncNotes.Length; i++)
        {
            Note note = syncNotes[i];
            
            ArcData data = GetArc(chart, note);
            if (!InRange(data.Scale)) continue;
            
            // Sync
            if (i != 0)
            {
                Note previous = syncNotes[i - 1];
                DrawSync(canvas, note, previous, data.Rect, data.Scale);
            }
        }

        // Layer 3 // Notes
        foreach (Note note in renderedNotes)
        {
            ArcData data = GetArc(chart, note);
            if (!InRange(data.Scale)) continue;
            
            // Note
            switch (note.NoteType)
            {
                case NoteType.Damage:
                { 
                    canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));

                    float outlineOffset = RenderConfig.NoteSize * 4.5f * data.Scale * canvasScale;
                    SKRect rectOuter = new(data.Rect.Left, data.Rect.Top, data.Rect.Right, data.Rect.Bottom);
                    SKRect rectInner = new(data.Rect.Left, data.Rect.Top, data.Rect.Right, data.Rect.Bottom);
                    rectOuter.Inflate(outlineOffset, outlineOffset);
                    rectInner.Inflate(-outlineOffset, -outlineOffset);
                    
                    if (note.Size == 60)
                    {
                        canvas.DrawArc(rectOuter, 0, 360, false, brushes.GetDamageOutlinePen(data.Scale));
                        canvas.DrawArc(rectInner, 0, 360, false, brushes.GetDamageOutlinePen(data.Scale));
                    }
                    else
                    {
                        SKPath outlinePath = new();
                        outlinePath.ArcTo(rectOuter, data.StartAngle, data.SweepAngle, true);
                        outlinePath.ArcTo(rectInner, data.StartAngle + data.SweepAngle, -data.SweepAngle, false);
                        outlinePath.Close();
                
                        canvas.DrawPath(outlinePath, brushes.GetDamageOutlinePen(data.Scale * canvasScale));
                    }
                    
                    break;
                }

                case NoteType.Hold:
                {
                    if (!note.IsSegment)
                    {
                        if (note.Size != 60)
                        {
                            TruncateArc(ref data, TruncateMode.ExcludeCaps);
                            DrawNoteCaps(canvas, data.Rect, data.StartAngle, data.SweepAngle, data.Scale);
                        }

                        canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));
                    }

                    if (note.LinkType is NoteLinkType.Point && !IsPlaying)
                    {
                        if (note.Size != 60)
                        {
                            TruncateArc(ref data, TruncateMode.ExcludeCaps);
                        }

                        canvas.DrawArc(data.Rect, data.StartAngle + 2f, data.SweepAngle - 4f, false, brushes.GetNotePen(note, canvasScale * data.Scale * 0.5f));
                    }

                    if (note.LinkType is NoteLinkType.End)
                    {
                        if (note.Size != 60)
                        {
                            TruncateArc(ref data, TruncateMode.Hold);    
                        }
                        
                        canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetHoldEndPen(canvasScale * data.Scale));
                    }
                    
                    break;
                }
                
                default:
                { 
                    if (note.Size != 60)
                    {
                        TruncateArc(ref data, TruncateMode.ExcludeCaps);
                        DrawNoteCaps(canvas, data.Rect, data.StartAngle, data.SweepAngle, data.Scale);
                    }
            
                    canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));
                    
                    break;   
                }
            }
            
            // Chain Stripes
            if (note.NoteType is NoteType.Chain && RenderConfig.ShowChainStripes)
            {
                DrawChainStripes(canvas, note, data);
            }
            
            // Bonus Triangles
            if (note.IsBonus)
            {
                DrawBonusFill(canvas, note, data);
            }
            
            // Selection
            if (mainView.ChartEditor.SelectedNotes.Contains(note))
            {
                DrawSelection(canvas, chart, note);
            }
            
            // Highlight
            if (note == mainView.ChartEditor.HighlightedElement)
            {
                DrawHighlight(canvas, chart, note);
            }
            
            // Arrows
            if (note.IsSlide || note.IsSnap)
            {
                DrawArrows(canvas, chart, note, data.Rect, data.Scale);
            }
            
            // Damage
            if (note.NoteType is NoteType.Damage)
            {
                DrawDamage(canvas, note, data);
            }
        }
    }
    
    // ____ NOTE COMPONENTS
    
    private void DrawNoteCaps(SKCanvas canvas, SKRect rect, float startAngle, float sweepAngle, float scale)
    {
        const float sweep = 1.6f;
        float start1 = startAngle - 0.1f;
        float start2 = startAngle + sweepAngle - 1.5f;  
            
        canvas.DrawArc(rect, start1, sweep, false, brushes.GetNoteCapPen(canvasScale * scale));
        canvas.DrawArc(rect, start2, sweep, false, brushes.GetNoteCapPen(canvasScale * scale));
    }

    private void DrawArrows(SKCanvas canvas, Chart chart, Note note, SKRect rect, float scale)
    {
        int arrowDirection = note.NoteType switch
        {
            NoteType.SlideClockwise => 1,
            NoteType.SlideCounterclockwise => -1,
            
            NoteType.SnapForward => 1,
            NoteType.SnapBackward => -1,
            
            _ => 0,
        };

        if (note.IsSnap) drawSnap();
        else drawSlide();

        return;
        
        void drawSnap()
        {
            int arrowCount = note.Size / 3;
            float radius = rect.Width * 0.53f;
            float snapRadiusOffset = arrowDirection > 0 ? 0.78f : 0.65f;
            float snapRowOffset = rect.Width * 0.043f;
            float snapArrowThickness = rect.Width * 0.028f;
            
            const float snapArrowLength = 0.12f;
            const float snapArrowWidth = 4.0f;
            
            float startPoint = note.Position * -6 - 3;
            float endPoint = startPoint + note.Size * -6 + 6;
            float interval = (endPoint - startPoint) / arrowCount;
            float offset = interval * 0.5f;

            for (float i = startPoint + offset; i > endPoint; i += interval)
            {
                //       p2
                //      /  \
                //    /      \
                //  p1   p5    p3
                //  |  /    \  |
                //  |/        \|
                //  p6        p4
                
                SKPoint  p1 = GetPointOnArc(canvasCenter, radius * snapRadiusOffset, i + snapArrowWidth);
                SKPoint  p2 = GetPointOnArc(canvasCenter, radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint  p3 = GetPointOnArc(canvasCenter, radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint  p4 = GetPointOnArc(canvasCenter, snapArrowThickness + radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint  p5 = GetPointOnArc(canvasCenter, snapArrowThickness + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint  p6 = GetPointOnArc(canvasCenter, snapArrowThickness + radius * snapRadiusOffset, i + snapArrowWidth);
                
                SKPoint  p7 = GetPointOnArc(canvasCenter, snapRowOffset + radius * snapRadiusOffset, i + snapArrowWidth);
                SKPoint  p8 = GetPointOnArc(canvasCenter, snapRowOffset + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint  p9 = GetPointOnArc(canvasCenter, snapRowOffset + radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint p10 = GetPointOnArc(canvasCenter, snapRowOffset + snapArrowThickness + radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint p11 = GetPointOnArc(canvasCenter, snapRowOffset + snapArrowThickness + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint p12 = GetPointOnArc(canvasCenter, snapRowOffset + snapArrowThickness + radius * snapRadiusOffset, i + snapArrowWidth);

                SKPath path1 = new();
                
                path1.MoveTo(p1);
                path1.LineTo(p2);
                path1.LineTo(p3);
                path1.LineTo(p4);
                path1.LineTo(p5);
                path1.LineTo(p6);
                path1.Close();
                
                path1.MoveTo(p7);
                path1.LineTo(p8);
                path1.LineTo(p9);
                path1.LineTo(p10);
                path1.LineTo(p11);
                path1.LineTo(p12);
                path1.Close();
                
                canvas.DrawPath(path1, brushes.GetSnapFill(note.NoteType, note.LinkType));
            }
        }
        
        void drawSlide()
        {
            float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(note.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, note.ScrollLayer);
            float spin = 1 - (scaledMeasureDecimal - ScaledCurrentMeasureDecimal(note.ScrollLayer)) / visibleDistanceMeasureDecimal;

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
                
                float radiusOutside0 = float.Max(radiusCenter + (arrowWidth * maskFunction(t0) * scale * canvasScale), radiusCenter);
                float radiusOutside1 = float.Max(radiusCenter + (arrowWidth * maskFunction(t1) * scale * canvasScale), radiusCenter);
                float radiusInside0 = float.Min(radiusCenter - (arrowWidth * maskFunction(t0) * scale * canvasScale), radiusCenter);
                float radiusInside1 = float.Min(radiusCenter - (arrowWidth * maskFunction(t1) * scale * canvasScale), radiusCenter);

                SKPoint p1;
                SKPoint p2;
                SKPoint p3;
                SKPoint p4;
                SKPoint p5;
                SKPoint p6;
                
                if (arrowDirection < 0)
                {
                    // Counterclockwise
                    p1 = GetPointOnArc(canvasCenter, radiusOutside1, float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle));
                    p2 = GetPointOnArc(canvasCenter, radiusCenter,   float.Clamp(startAngle - arrowTipOffset * (1 + (t1 * 0.5f + 0.5f)), maxAngle, minAngle));
                    p3 = GetPointOnArc(canvasCenter, radiusInside1,  float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle));
                    p4 = GetPointOnArc(canvasCenter, radiusInside0,  float.Clamp(startAngle,                                             maxAngle, minAngle));
                    p5 = GetPointOnArc(canvasCenter, radiusCenter,   float.Clamp(startAngle - arrowTipOffset * (t0 * 0.5f + 0.5f),       maxAngle, minAngle));
                    p6 = GetPointOnArc(canvasCenter, radiusOutside0, float.Clamp(startAngle,                                             maxAngle, minAngle));
                }
                else
                {
                    // Clockwise
                    p1 = GetPointOnArc(canvasCenter, radiusOutside1, maxAngle - float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle) + minAngle);
                    p2 = GetPointOnArc(canvasCenter, radiusCenter,   maxAngle - float.Clamp(startAngle - arrowTipOffset * (1 + (t1 * 0.5f + 0.5f)), maxAngle, minAngle) + minAngle);
                    p3 = GetPointOnArc(canvasCenter, radiusInside1,  maxAngle - float.Clamp(startAngle - arrowTipOffset,                            maxAngle, minAngle) + minAngle);
                    p4 = GetPointOnArc(canvasCenter, radiusInside0,  maxAngle - float.Clamp(startAngle,                                             maxAngle, minAngle) + minAngle);
                    p5 = GetPointOnArc(canvasCenter, radiusCenter,   maxAngle - float.Clamp(startAngle - arrowTipOffset * (t0 * 0.5f + 0.5f),       maxAngle, minAngle) + minAngle);
                    p6 = GetPointOnArc(canvasCenter, radiusOutside0, maxAngle - float.Clamp(startAngle,                                             maxAngle, minAngle) + minAngle);
                }
                
                SKPath path = new SKPath();   
                path.MoveTo(p1);   
                path.LineTo(p2);   
                path.LineTo(p3);   
                path.LineTo(p4);   
                path.LineTo(p5);   
                path.LineTo(p6);   
                path.Close();
                
                canvas.DrawPath(path, brushes.GetSwipeFill(note.NoteType, note.LinkType));
            }
        }
        
        // I just traced the Slide Arrow Mask texture from Mercury with a graph.
        // https://www.desmos.com/calculator/ylcsznpfra
        float maskFunction(float t)
        {
            return t < 0.88f ? 0.653f * t + 0.175f : -6.25f * t + 6.25f;
        }
    }

    private void DrawChainStripes(SKCanvas canvas, Note note, ArcData data)
    {
        int stripes = note.Size * 2;
        float radiusInner = 0.5f * (data.Rect.Width - brushes.NoteWidthMultiplier * canvasScale * data.Scale);
        float radiusOuter = 0.5f * (data.Rect.Width + brushes.NoteWidthMultiplier * canvasScale * data.Scale);
                
        SKPath path = new();
                
        for (int i = 0; i < stripes; i++)
        {
            if (i == 0 && note.Size != 60) continue;
            if (i >= stripes - 3 && note.Size != 60) continue;

            if (note.Size != 60 && i == 1)
            {
                SKPoint t0 = GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3 + 3);
                SKPoint t1 = GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3 + 1.5f);
                SKPoint t2 = GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3 + 3);
                        
                path.MoveTo(t0);
                path.LineTo(t1);
                path.LineTo(t2);
            }

            if (note.Size != 60 && i == stripes - 4)
            {
                SKPoint t1 = GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3);
                SKPoint t2 = GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3);
                SKPoint t3 = GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3 + 1.5f);
                        
                path.MoveTo(t1);
                path.LineTo(t2);
                path.LineTo(t3);
                        
                continue;
            }
                    
            SKPoint p0 = GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3);
            SKPoint p1 = GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3 - 1.5f);
            SKPoint p2 = GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3);
            SKPoint p3 = GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3 + 1.5f);
                    
            path.MoveTo(p0);
            path.LineTo(p1);
            path.LineTo(p2);
            path.LineTo(p3);
        }
                
        canvas.DrawPath(path, brushes.ChainStripeFill);
    }
    
    private void DrawSync(SKCanvas canvas, Note current, Note previous, SKRect rect, float scale)
    {
        if (invalidType(current)) return;
        if (invalidType(previous)) return;
        if (current.ScrollLayer != previous.ScrollLayer) return;
        
        bool currentIsHoldStart = current.NoteType == NoteType.Hold && current.LinkType == NoteLinkType.Start;
        bool previousIsHoldStart = previous.NoteType == NoteType.Hold && previous.LinkType == NoteLinkType.Start;
        bool fullOverlap = current.Position == previous.Position && current.Size == previous.Size;
            
        if (current.BeatData.FullTick != previous.BeatData.FullTick) return;
        if ((currentIsHoldStart || previousIsHoldStart) && fullOverlap) return;
        
        drawSyncConnector();
        drawSyncOutline();

        return;

        bool invalidType(Note note)
        {
            return note.IsSegment 
                   || note.IsMask 
                   || note.NoteType is NoteType.Trace or NoteType.Damage 
                   || (note.NoteType == NoteType.Chain && note.BonusType != BonusType.RNote);
        }
        
        void drawSyncConnector()
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

        void drawSyncOutline()
        {
            float outlineOffset = (RenderConfig.NoteSize * 4.5f + 4.5f) * scale * canvasScale;
            const float controlOffset = 2;
            
            SKPath path1 = new();

            SKRect rectOuter = new(rect.Left, rect.Top, rect.Right, rect.Bottom);
            SKRect rectInner = new(rect.Left, rect.Top, rect.Right, rect.Bottom);
            rectOuter.Inflate(outlineOffset, outlineOffset);
            rectInner.Inflate(-outlineOffset, -outlineOffset);

            if (current.Size == 60)
            {
                canvas.DrawArc(rectOuter, 0, 360, false, brushes.GetSyncPen(scale * 0.75f));
                canvas.DrawArc(rectInner, 0, 360, false, brushes.GetSyncPen(scale * 0.75f));
            }
            else
            {
                float currentPos = current.Position * -6 - 2.5f;
                float currentSweep = current.Size * -6 + 5;

                SKPoint control1 = GetPointOnArc(canvasCenter, rect.Width * 0.5f, currentPos + currentSweep - controlOffset);
                SKPoint arcEdge1 = GetPointOnArc(canvasCenter, rect.Width * 0.5f - outlineOffset, currentPos + currentSweep);
            
                SKPoint control2 = GetPointOnArc(canvasCenter, rect.Width * 0.5f, currentPos + controlOffset);
                SKPoint arcEdge2 = GetPointOnArc(canvasCenter, rect.Width * 0.5f + outlineOffset, currentPos);

                path1.ArcTo(rectOuter, currentPos, currentSweep, true);
                path1.QuadTo(control1, arcEdge1);
                path1.ArcTo(rectInner, currentPos + currentSweep, -currentSweep, false);
                path1.QuadTo(control2, arcEdge2);
                path1.Close();
            }

            if (previous.Size == 60)
            {
                canvas.DrawArc(rectOuter, 0, 360, false, brushes.GetSyncPen(scale * 0.75f));
                canvas.DrawArc(rectInner, 0, 360, false, brushes.GetSyncPen(scale * 0.75f));
            }
            else
            {
                float previousPos = previous.Position * -6 - 2.5f;
                float previousSweep = previous.Size * -6 + 5;
            
                SKPoint control3 = GetPointOnArc(canvasCenter, rect.Width * 0.5f, previousPos + previousSweep - controlOffset);
                SKPoint arcEdge3 = GetPointOnArc(canvasCenter, rect.Width * 0.5f - outlineOffset, previousPos + previousSweep);
            
                SKPoint control4 = GetPointOnArc(canvasCenter, rect.Width * 0.5f, previousPos + controlOffset);
                SKPoint arcEdge4 = GetPointOnArc(canvasCenter, rect.Width * 0.5f + outlineOffset, previousPos);

                path1.ArcTo(rectOuter, previousPos, previousSweep, true);
                path1.QuadTo(control3, arcEdge3);
                path1.ArcTo(rectInner, previousPos + previousSweep, -previousSweep, false);
                path1.QuadTo(control4, arcEdge4);
                path1.Close();
            }
            
            if (current.Size != 60 || previous.Size != 60) canvas.DrawPath(path1, brushes.GetSyncPen(scale * canvasScale * 0.75f));
        }
    }

    private void DrawSelection(SKCanvas canvas, Chart chart, Note note)
    {
        ArcData selectedData = GetArc(chart, note);

        TruncateMode truncateMode = (note.NoteType, note.LinkType) switch
        {
            (NoteType.Hold, _) => TruncateMode.OutlineHoldSegment,
            (NoteType.MaskAdd, _) => TruncateMode.OutlineFull,
            (NoteType.MaskRemove, _) => TruncateMode.OutlineFull,
            (NoteType.Damage, _) => TruncateMode.OutlineFull,
            _ => TruncateMode.OutlineNote,
        };

        if (note.Size != 60) TruncateArc(ref selectedData, truncateMode);
        else TrimCircleArc(ref selectedData);

        float widthMultiplier = note.NoteType == NoteType.Hold && note.LinkType == NoteLinkType.Point ? 0.75f : 1;
        canvas.DrawArc(selectedData.Rect, selectedData.StartAngle, selectedData.SweepAngle, false, brushes.GetSelectionPen(canvasScale * selectedData.Scale * widthMultiplier));
    }

    private void DrawHighlight(SKCanvas canvas, Chart chart, Note note)
    {
        ArcData selectedData = GetArc(chart, note);

        TruncateMode truncateMode = (note.NoteType, note.LinkType) switch
        {
            (NoteType.Hold, NoteLinkType.Point) => TruncateMode.OutlineHoldSegment,
            (NoteType.MaskAdd, _) => TruncateMode.OutlineFull,
            (NoteType.MaskRemove, _) => TruncateMode.OutlineFull,
            (NoteType.Damage, _) => TruncateMode.OutlineFull,
            _ => TruncateMode.OutlineNote,
        };

        if (note.Size != 60) TruncateArc(ref selectedData, truncateMode);
        else TrimCircleArc(ref selectedData);

        float widthMultiplier = note.NoteType is NoteType.Hold && note.LinkType is NoteLinkType.Point ? 0.75f : 1;
        canvas.DrawArc(selectedData.Rect, selectedData.StartAngle, selectedData.SweepAngle, false, brushes.GetHighlightPen(canvasScale * selectedData.Scale * widthMultiplier));
    }

    private void DrawHighlight(SKCanvas canvas, ArcData data)
    {
        canvas.DrawOval(data.Rect, brushes.GetHighlightPen(canvasScale * data.Scale));
    }

    private void DrawRNote(SKCanvas canvas, Note note, ArcData data)
    {
        float start = data.StartAngle - (note.Size != 60 ? 1.5f : 0);
        float sweep = data.SweepAngle + (note.Size != 60 ? 3.0f : 0);
        canvas.DrawArc(data.Rect, start, sweep, false, brushes.GetRNotePen(canvasScale * data.Scale));
    }

    private void DrawBonusGlow(SKCanvas canvas, Note note, ArcData data)
    {
        float start = data.StartAngle - (note.Size != 60 ? 1.5f : 0);
        float sweep = data.SweepAngle + (note.Size != 60 ? 3.0f : 0);
        canvas.DrawArc(data.Rect, start, sweep, false, brushes.GetBonusPen(canvasScale * data.Scale));
    }
    
    private void DrawBonusFill(SKCanvas canvas, Note note, ArcData data)
    {
        SKPath path = new();
        
        float radiusInner = 0.5f * (data.Rect.Width - brushes.NoteWidthMultiplier * canvasScale * data.Scale);
        float radiusOuter = 0.5f * (data.Rect.Width + brushes.NoteWidthMultiplier * canvasScale * data.Scale);

        int triangles = note.Size == 60 ? note.Size : note.Size - 2;
        float startAngle = note.Size == 60 ? data.StartAngle + 6 : data.StartAngle;
        
        for (int i = 0; i < triangles; i++)
        {
            // Check if [i] is even or odd to flip the triangle
            // for every other note segment
            bool isEven = (i & 1) == 0;
            
            float angleA = startAngle + i * -6;
            float angleB = startAngle + (i + 1) * -6;
            
            path.MoveTo(GetPointOnArc(canvasCenter, radiusInner, isEven ? angleA : angleB));
            path.LineTo(GetPointOnArc(canvasCenter, radiusOuter, isEven ? angleA : angleB));
            path.LineTo(GetPointOnArc(canvasCenter, radiusInner, isEven ? angleB : angleA));
        }

        canvas.DrawPath(path, brushes.BonusFill);
    }

    private enum CollectionSurfaceDrawMode
    {
        Hold,
        Trace,
        TraceCenter,
    }

    private void DrawNoteCollectionSurface(SKCanvas canvas, Chart chart, NoteCollection collection, TruncateMode truncateMode, CollectionSurfaceDrawMode drawMode)
    {
        if (collection.Notes.Count == 0) return;
        SKPath path = new();

        // This must be one of them darn damn dangit hold notes where the first segment is behind the camera
        // and the second is outside of vision range. Aw shucks, we have to do some extra special work.
        if (chart.GetScaledMeasureDecimal(collection.Notes[0].BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, collection.Notes[0].ScrollLayer) < ScaledCurrentMeasureDecimal(collection.Notes[0].ScrollLayer) && collection.Notes.Count == 1)
        {
            Note prev = collection.Notes[0];
            Note? next = prev.NextVisibleReference(RenderConfig.DrawNoRenderSegments);
                
            if (next == null) return;

            // Aw, nevermind. It ain't one of them darn damn dangit hold notes where the first segment is behind the camera and the second is outside of vision range.
            if (chart.GetScaledMeasureDecimal(next.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, next.ScrollLayer) <= ScaledCurrentMeasureDecimal(next.ScrollLayer) + visibleDistanceMeasureDecimal) return;

            ArcData prevData = GetTruncatedArc(chart, prev, truncateMode);
            ArcData nextData = GetTruncatedArc(chart, next, truncateMode);

            float ratio = MathExtensions.InverseLerp(CurrentMeasureDecimal, next.BeatData.MeasureDecimal, prev.BeatData.MeasureDecimal);

            if (float.Abs(nextData.StartAngle - prevData.StartAngle) > 180)
            {
                if (nextData.StartAngle > prevData.StartAngle) nextData.StartAngle -= 360;
                else prevData.StartAngle -= 360;
            }

            ArcData intermediateData = new(canvasRect, 1, MathExtensions.Lerp(nextData.StartAngle, prevData.StartAngle, ratio), MathExtensions.Lerp(nextData.SweepAngle, prevData.SweepAngle, ratio));
            path.ArcTo(intermediateData.Rect, intermediateData.StartAngle, intermediateData.SweepAngle, false);
            path.LineTo(canvasCenter);
            
            switch (drawMode)
            {
                case CollectionSurfaceDrawMode.Hold:
                {
                    canvas.DrawPath(path, brushes.HoldFill);
                    break;
                }
                
                case CollectionSurfaceDrawMode.Trace:
                {
                    canvas.DrawPath(path, brushes.GetTraceFill(prev.FirstReference()?.Color ?? TraceColor.White));
                    break;
                }
                
                case CollectionSurfaceDrawMode.TraceCenter:
                {
                    canvas.DrawPath(path, brushes.TraceCenterFill);
                    break;
                }
            }

            return;
        }

        for (int i = 0; i < collection.Notes.Count; i++)
        {
            Note note = collection.Notes[i];
            if (note.LinkType == NoteLinkType.Unlinked) continue;
                
            ArcData currentData = GetTruncatedArc(chart, note, truncateMode);

            // First part of the path. Must be an arc.
            if (i == 0)
            {
                // If the hold start is visible there's no need to interpolate.
                if (note.LinkType == NoteLinkType.Start)
                {
                    path.ArcTo(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, true);
                }

                // If it's a segment, there must be an earlier note off-screen.
                else if (note.PrevVisibleReference(RenderConfig.DrawNoRenderSegments) != null)
                {
                    Note prevNote = note.PrevVisibleReference(RenderConfig.DrawNoRenderSegments)!;
                    ArcData prevData = GetTruncatedArc(chart, prevNote, truncateMode);

                    float ratio = MathExtensions.InverseLerp(ScaledCurrentMeasureDecimal(note.ScrollLayer), chart.GetScaledMeasureDecimal(note.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, note.ScrollLayer), chart.GetScaledMeasureDecimal(prevNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed, note.ScrollLayer));

                    if (float.Abs(currentData.StartAngle - prevData.StartAngle) > 180)
                    {
                        if (currentData.StartAngle > prevData.StartAngle) currentData.StartAngle -= 360;
                        else prevData.StartAngle -= 360;
                    }

                    ArcData intermediateData = new(canvasRect, 1, MathExtensions.Lerp(currentData.StartAngle, prevData.StartAngle, ratio), MathExtensions.Lerp(currentData.SweepAngle, prevData.SweepAngle, ratio));
                    path.ArcTo(intermediateData.Rect, intermediateData.StartAngle, intermediateData.SweepAngle, false);
                }

                // Hack to fix a very odd rendering bug where path.Points[0] is at (0,0).
                // It still happens, but this makes it less obvious by moving the spike to the center of the screen instead of the top left.
                if (path.Points.Length == 0)
                {
                    path.MoveTo(canvasCenter);
                }
            }

            if ((i == 0 && note.LinkType == NoteLinkType.Point || note.LinkType == NoteLinkType.End) || (i != 0 && i != collection.Notes.Count - 1))
            {
                // Line to right edge
                path.LineTo(GetPointOnArc(canvasCenter, currentData.Rect.Width * 0.5f, currentData.StartAngle + currentData.SweepAngle));
            }

            if (i == collection.Notes.Count - 1)
            {
                // If there's a next note there can't be a final arc.
                if (note.NextVisibleReference(RenderConfig.DrawNoRenderSegments) != null)
                {
                    // Line to right edge
                    path.LineTo(GetPointOnArc(canvasCenter, currentData.Rect.Width * 0.5f, currentData.StartAngle + currentData.SweepAngle));
                    path.LineTo(canvasCenter);
                }
                else
                {
                    FlipArc(ref currentData);
                    path.ArcTo(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false);
                }
            }
        }

        // >= 0 because last segment in the backwards sequence (so the first segment of the hold) is skipped*.
        for (int i = collection.Notes.Count - 1; i >= 0; i--)
        {
            Note note = collection.Notes[i];
            if (note.LinkType == NoteLinkType.Unlinked) continue;

            // *technically unnecessary to skip, but doing it just for consistency.
            if (i == 0 && note.LinkType == NoteLinkType.Start) continue;

            ArcData currentData = GetTruncatedArc(chart, note, truncateMode);
            path.LineTo(GetPointOnArc(canvasCenter, currentData.Rect.Width * 0.5f, currentData.StartAngle));
        }

        Note? firstNote = collection.Notes[0].FirstReference();
        if (firstNote == null) return;
            
        switch (drawMode)
        {
            case CollectionSurfaceDrawMode.Hold:
            {
                canvas.DrawPath(path, brushes.HoldFill);
                break;
            }
                
            case CollectionSurfaceDrawMode.Trace:
            {
                canvas.DrawPath(path, brushes.GetTraceFill(firstNote.Color));
                break;
            }
                
            case CollectionSurfaceDrawMode.TraceCenter:
            {
                canvas.DrawPath(path, brushes.TraceCenterFill);
                break;
            }
        }
    }

    private void DrawDamage(SKCanvas canvas, Note note, ArcData data)
    {
        SKPath sparkPath = new();
        int steps = int.Min(note.Size + 1, 60);
            
        for (int j = 0; j < steps; j++)
        {
            float offset = random.NextSingle() * 0.06f;
            SKPoint p = GetPointOnArc(canvasCenter, data.Rect.Width * (0.44f + offset), (note.Position + j) * -6);

            if (j == 0) sparkPath.MoveTo(p);
            sparkPath.LineTo(p);
        }
            
        if (note.Size == 60) sparkPath.Close();

        canvas.DrawPath(sparkPath, brushes.GetDamageSparkPen(data.Scale * canvasScale));
    }
    
    private void DrawJudgementWindows(SKCanvas canvas, Chart chart)
    {
        // Iterate backwards
        for (int i = chart.Notes.Count - 1; i >= 0; i--)
        {
            Note note = chart.Notes[i];
            
            if (note.IsMask) continue;
            if (note.IsSegment) continue;
            if (note.NoteType == NoteType.Trace) continue;

            TimingWindow window = GetTimingWindow(note.NoteType);

            // Cut early window on Hold End
            if (RenderConfig.CutEarlyJudgementWindowOnHolds)
            {
                bool holdEndFound = false;
                Note? next = null;
                
                // Iterate forward until next note either doesn't exist, or it's FullTick is > current note's FullTick.
                for (int j = i; j < chart.Notes.Count; j++)
                {
                    next = chart.Notes[j];
                    
                    if (next.BeatData.FullTick > note.BeatData.FullTick) break;
                    if (next.NoteType == NoteType.Hold && next.LinkType == NoteLinkType.End && MathExtensions.IsPartiallyOverlapping(note.Position, note.Position + note.Size, next!.Position, next.Position + next.Size))
                    {
                        holdEndFound = true;
                        break;
                    }
                }

                // If that didnt work, iterate backward until next note either doesn't exist, or it's FullTick is < current note's FullTick.
                if (holdEndFound == false)
                {
                    for (int j = i; j >= 0; j--)
                    {
                        next = chart.Notes[j];
                        
                        if (next.BeatData.FullTick < note.BeatData.FullTick) break;
                        if (next.NoteType == NoteType.Hold && next.LinkType == NoteLinkType.End && MathExtensions.IsPartiallyOverlapping(note.Position, note.Position + note.Size, next!.Position, next.Position + next.Size))
                        {
                            holdEndFound = true;
                            break;
                        }
                    }
                }
                
                // Overlapping Hold end Found, cut window.
                if (holdEndFound)
                {
                    window.GoodEarly = window.MarvelousEarly;
                    window.GreatEarly = window.MarvelousEarly;
                }
            }

            float noteTimestamp = chart.MeasureDecimal2Timestamp(note.BeatData.MeasureDecimal);

            window.MarvelousEarly += noteTimestamp;
            window.MarvelousLate += noteTimestamp;
            window.GreatEarly += noteTimestamp;
            window.GreatLate += noteTimestamp;
            window.GoodEarly += noteTimestamp;
            window.GoodLate += noteTimestamp;

            window.MarvelousEarly = chart.Timestamp2MeasureDecimal(window.MarvelousEarly);
            window.MarvelousLate = chart.Timestamp2MeasureDecimal(window.MarvelousLate);
            window.GreatEarly = chart.Timestamp2MeasureDecimal(window.GreatEarly);
            window.GreatLate = chart.Timestamp2MeasureDecimal(window.GreatLate);
            window.GoodEarly = chart.Timestamp2MeasureDecimal(window.GoodEarly);
            window.GoodLate = chart.Timestamp2MeasureDecimal(window.GoodLate);

            float minEarly = float.Min(float.Min(window.MarvelousEarly, window.GreatEarly), window.GoodEarly);
            float maxLate = float.Max(float.Max(window.MarvelousLate, window.GreatLate), window.GoodLate);
            
            if (maxLate < CurrentMeasureDecimal) continue;
            if (chart.GetScaledMeasureDecimal(minEarly, RenderConfig.ShowHiSpeed, note.ScrollLayer) > ScaledCurrentMeasureDecimal(note.ScrollLayer) + visibleDistanceMeasureDecimal) continue;
            
            // Cut overlapping windows
            if (RenderConfig.CutOverlappingJudgementWindows)
            {
                // Iterate forwards until overlapping note is found, or next note is out of range.
                for (int j = i; j < chart.Notes.Count; j++)
                {
                    Note next = chart.Notes[j];
                    
                    if (next.IsMask || next.IsSegment || next.NoteType == NoteType.Trace) continue;
                    if (next.BeatData.FullTick == note.BeatData.FullTick) continue;
                    if (!MathExtensions.IsPartiallyOverlapping(note.Position, note.Position + note.Size, next.Position, next.Position + next.Size)) continue;
                    
                    TimingWindow nextWindow = GetTimingWindow(next.NoteType);
                    float nextTimestamp = chart.MeasureDecimal2Timestamp(next.BeatData.MeasureDecimal);

                    nextWindow.MarvelousEarly += nextTimestamp;
                    nextWindow.GreatEarly += nextTimestamp;
                    nextWindow.GoodEarly += nextTimestamp;

                    nextWindow.MarvelousEarly = chart.Timestamp2MeasureDecimal(nextWindow.MarvelousEarly);
                    nextWindow.GreatEarly = chart.Timestamp2MeasureDecimal(nextWindow.GreatEarly);
                    nextWindow.GoodEarly = chart.Timestamp2MeasureDecimal(nextWindow.GoodEarly);
                    
                    float minNextEarly = float.Min(float.Min(nextWindow.MarvelousEarly, nextWindow.GreatEarly), nextWindow.GoodEarly);

                    if (minNextEarly >= maxLate) break;

                    float centerMeasureDecimal = (next.BeatData.MeasureDecimal + note.BeatData.MeasureDecimal) * 0.5f;
                    
                    // Cut Timing window
                    window.MarvelousLate = float.Min(window.MarvelousLate, centerMeasureDecimal);
                    window.GreatLate = float.Min(window.GreatLate, centerMeasureDecimal);
                    window.GoodLate = float.Min(window.GoodLate, centerMeasureDecimal);
                }

                // Iterate backwards until overlapping note is found, or prev note is out of range.
                for (int j = i; j >= 0; j--)
                {
                    Note prev = chart.Notes[j];
                    
                    if (prev.IsMask || prev.IsSegment || prev.NoteType == NoteType.Trace) continue;
                    if (prev.BeatData.FullTick == note.BeatData.FullTick) continue;
                    if (!MathExtensions.IsPartiallyOverlapping(note.Position, note.Position + note.Size, prev.Position, prev.Position + prev.Size)) continue;
                    
                    TimingWindow prevWindow = GetTimingWindow(prev.NoteType);
                    float prevTimestamp = chart.MeasureDecimal2Timestamp(prev.BeatData.MeasureDecimal);

                    prevWindow.MarvelousLate += prevTimestamp;
                    prevWindow.GreatLate += prevTimestamp;
                    prevWindow.GoodLate += prevTimestamp;

                    prevWindow.MarvelousLate = chart.Timestamp2MeasureDecimal(prevWindow.MarvelousLate);
                    prevWindow.GreatLate = chart.Timestamp2MeasureDecimal(prevWindow.GreatLate);
                    prevWindow.GoodLate = chart.Timestamp2MeasureDecimal(prevWindow.GoodLate);
                    
                    float maxPrevLate = float.Max(float.Max(prevWindow.MarvelousLate, prevWindow.GreatLate), prevWindow.GoodLate);

                    if (maxPrevLate <= minEarly) break;

                    float centerMeasureDecimal = (prev.BeatData.MeasureDecimal + note.BeatData.MeasureDecimal) * 0.5f;
                    
                    // Cut Timing window
                    window.MarvelousEarly = float.Max(window.MarvelousEarly, centerMeasureDecimal);
                    window.GreatEarly = float.Max(window.GreatEarly, centerMeasureDecimal);
                    window.GoodEarly = float.Max(window.GoodEarly, centerMeasureDecimal);
                }
                
                // Recalculate maxLate/minEarly after cutting timing windows
                minEarly = float.Min(float.Min(window.MarvelousEarly, window.GreatEarly), window.GoodEarly);
                maxLate = float.Max(float.Max(window.MarvelousLate, window.GreatLate), window.GoodLate);
                
                if (maxLate < CurrentMeasureDecimal) continue;
                if (chart.GetScaledMeasureDecimal(minEarly, RenderConfig.ShowHiSpeed, note.ScrollLayer) > ScaledCurrentMeasureDecimal(note.ScrollLayer) + visibleDistanceMeasureDecimal) continue;
            }
            
            bool drawGreatEarly = window.GreatEarly < window.MarvelousEarly;
            bool drawGreatLate = window.GreatLate > window.MarvelousLate;
            bool drawGoodEarly = window.GoodEarly < window.GreatEarly;
            bool drawGoodLate = window.GoodLate > window.GreatLate;
            
            float startAngle = note.Position * -6;
            float sweepAngle = note.Size * -6 + 0.1f;
            
            float marvelousEarlyScale = float.Clamp(GetNoteScale(chart, window.MarvelousEarly, note.ScrollLayer), 0, 1);
            float marvelousLateScale = float.Clamp(GetNoteScale(chart, window.MarvelousLate, note.ScrollLayer), 0, 1);
            float greatEarlyScale = float.Clamp(GetNoteScale(chart, window.GreatEarly, note.ScrollLayer), 0, 1);
            float greatLateScale = float.Clamp(GetNoteScale(chart, window.GreatLate, note.ScrollLayer), 0, 1);
            float goodEarlyScale = float.Clamp(GetNoteScale(chart, window.GoodEarly, note.ScrollLayer), 0, 1);
            float goodLateScale = float.Clamp(GetNoteScale(chart, window.GoodLate, note.ScrollLayer), 0, 1);
            
            // hacky but prevents a graphical glitch caused by scale jank.
            if (marvelousEarlyScale < marvelousLateScale) marvelousEarlyScale = 1; 
            if (greatEarlyScale < greatLateScale) greatEarlyScale = 1;
            if (goodEarlyScale < goodLateScale) goodEarlyScale = 1;

            SKRect marvelousEarlyRect = GetRect(marvelousEarlyScale);
            SKRect marvelousLateRect = GetRect(marvelousLateScale);
            SKRect greatEarlyRect = GetRect(greatEarlyScale);
            SKRect greatLateRect = GetRect(greatLateScale);
            SKRect goodEarlyRect = GetRect(goodEarlyScale);
            SKRect goodLateRect = GetRect(goodLateScale);

            if ((drawGoodEarly || drawGoodLate) && RenderConfig.ShowJudgementWindowGood)
            {
                SKPath goodPath = new();
                if (drawGoodEarly)
                {
                    goodPath.ArcTo(goodEarlyRect, startAngle, sweepAngle, true);
                    goodPath.ArcTo(greatEarlyRect, startAngle + sweepAngle, -sweepAngle, false);
                    goodPath.Close();
                }

                if (drawGoodLate)
                {
                    goodPath.ArcTo(greatLateRect, startAngle, sweepAngle, true);
                    goodPath.ArcTo(goodLateRect, startAngle + sweepAngle, -sweepAngle, false);
                    goodPath.Close();
                }
            
                canvas.DrawPath(goodPath, brushes.JudgementGoodFill);
                canvas.DrawPath(goodPath, brushes.JudgementGoodPen);
            }
            
            if ((drawGreatEarly || drawGreatLate) && RenderConfig.ShowJudgementWindowGreat)
            {
                SKPath greatPath = new();
                if (drawGreatEarly)
                {
                    greatPath.ArcTo(greatEarlyRect, startAngle, sweepAngle, true);
                    greatPath.ArcTo(marvelousEarlyRect, startAngle + sweepAngle, -sweepAngle, false);
                    greatPath.Close();
                }

                if (drawGreatLate)
                {
                    greatPath.ArcTo(marvelousLateRect, startAngle, sweepAngle, true);
                    greatPath.ArcTo(greatLateRect, startAngle + sweepAngle, -sweepAngle, false);
                    greatPath.Close();
                }
            
                canvas.DrawPath(greatPath, brushes.JudgementGreatFill);
                canvas.DrawPath(greatPath, brushes.JudgementGreatPen);
            }
            
            if (RenderConfig.ShowJudgementWindowMarvelous)
            {
                SKPath marvelousPath = new();
                marvelousPath.ArcTo(marvelousEarlyRect, startAngle, sweepAngle, true);
                marvelousPath.ArcTo(marvelousLateRect, startAngle + sweepAngle, -sweepAngle, false);
                marvelousPath.Close();
            
                canvas.DrawPath(marvelousPath, brushes.JudgementMarvelousFill);
                canvas.DrawPath(marvelousPath, brushes.JudgementMarvelousPen);
            }
        }
    }
}

internal struct ArcData(SKRect rect, float scale, float startAngle, float sweepAngle)
{
    public SKRect Rect = rect;
    public readonly float Scale = scale;
    public float StartAngle = startAngle;
    public float SweepAngle = sweepAngle;
}

internal static class RenderMath
{
    internal struct TimingWindow(float marvelousEarly, float marvelousLate, float greatEarly, float greatLate, float goodEarly, float goodLate)
    {
        private const float Frame = 16.6666f;
        
        public float MarvelousEarly = marvelousEarly * Frame;
        public float MarvelousLate = marvelousLate * Frame;
        public float GreatEarly = greatEarly * Frame;
        public float GreatLate = greatLate * Frame;
        public float GoodEarly = goodEarly * Frame;
        public float GoodLate = goodLate * Frame;
    }
    
    internal static TimingWindow GetTimingWindow(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.None => new(0, 0, 0, 0, 0, 0),
            NoteType.Touch => new(-3, 3, -5, 5, -6, 6),
            NoteType.SnapForward => new(-5, 7, -8, 10, -10, 10),
            NoteType.SnapBackward => new(-7, 5, -10, 8, -10, 10),
            NoteType.SlideClockwise => new(-5, 5, -8, 10, -10, 10),
            NoteType.SlideCounterclockwise => new(-5, 5, -8, 10, -10, 10),
            NoteType.Hold => new(-3, 3, -5, 5, -6, 6),
            NoteType.MaskAdd => new(0, 0, 0, 0, 0, 0),
            NoteType.MaskRemove => new(0, 0, 0, 0, 0, 0),
            NoteType.Chain => new(-4, 4, 0, 0, 0, 0),
            NoteType.Trace => new(0, 0, 0, 0, 0, 0),
            NoteType.Damage => new(0, 0, 0, 0, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(noteType), noteType, null),
        };
    }
    
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

    internal static bool InRange(float scale)
    {
        return scale is > 0 and < 1.01f;
    }
}

