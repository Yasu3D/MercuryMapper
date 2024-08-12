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
    private float ScaledCurrentMeasureDecimal => Chart.GetScaledMeasureDecimal(CurrentMeasureDecimal, RenderConfig.ShowHiSpeed);
    private RenderConfig RenderConfig => mainView.UserConfig.RenderConfig;

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
        
        DrawPeers(canvas, Chart);
        
        if ((!IsPlaying || (IsPlaying && RenderConfig.ShowGimmickNotesDuringPlayback)) && mainView.ChartEditor.LayerGimmickActive) DrawGimmickNotes(canvas, Chart);
        if ((!IsPlaying || (IsPlaying && RenderConfig.ShowMaskDuringPlayback)) && mainView.ChartEditor.LayerMaskActive) DrawMaskNotes(canvas, Chart);
        
        if (mainView.ChartEditor.LayerNoteActive)
        {
            DrawSyncs(canvas, Chart); // Hold Surfaces normally render under syncs but the syncs poke into the note a bit, and it looks shit.
            
            if (RenderConfig.HoldRenderMethod == 0) DrawHolds(canvas, Chart);
            else DrawHoldsLegacy(canvas, Chart);
            
            DrawNotes(canvas, Chart);
            DrawArrows(canvas, Chart);
        }
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

    public float GetMeasureDecimalAtPointer(Chart chart, SKPoint point)
    {
        float clickRadius = (1 - MathExtensions.InversePerspective(point.Length)) * visibleDistanceMeasureDecimal;
        return chart.GetUnscaledMeasureDecimal(clickRadius + ScaledCurrentMeasureDecimal, RenderConfig.ShowHiSpeed);
    }
    
    public ChartElement? GetChartElementAtPointer(Chart chart, SKPoint point, bool includeGimmicks, bool layerNote, bool layerMask, bool layerGimmick)
    {
        int clickPosition = MathExtensions.GetThetaNotePosition(point.X, point.Y);
        float measureDecimal = GetMeasureDecimalAtPointer(chart, point);
        
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
            .OrderBy(item => item.Note.IsHold)
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
    private float GetNoteScale(Chart chart, float measureDecimal)
    {
        float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(measureDecimal, RenderConfig.ShowHiSpeed);
        float scale = 1 - (scaledMeasureDecimal - ScaledCurrentMeasureDecimal) / visibleDistanceMeasureDecimal;
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

    private enum TruncateMode
    {
        ExcludeCaps,
        IncludeCaps,
        Hold,
        OutlineNote,
        OutlineHoldSegment,
        OutlineMask
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

            case TruncateMode.OutlineMask:
            {
                data.StartAngle += 1f;
                data.SweepAngle -= 2f;
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

    private void DrawBoxSelectCursor(SKCanvas canvas, int position, int size)
    {
        canvas.DrawArc(canvasRect, position * -6, size * -6, false, brushes.GetBoxSelectCursorPen(canvasScale));
    }

    private void DrawBoxSelectArea(SKCanvas canvas, Chart chart)
    {
        if (BoxSelect.SelectionStart is null) return;

        float selectionStartScale = BoxSelect.SelectionStart.MeasureDecimal <= CurrentMeasureDecimal ? 1 : GetNoteScale(chart, BoxSelect.SelectionStart.MeasureDecimal);
        SKRect selectionStartRect = GetRect(selectionStartScale);
        
        float selectionEndScale = float.Min(1, GetNoteScale(chart, GetMeasureDecimalAtPointer(chart, PointerPosition)));
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
        SKPoint p0 = RenderMath.GetPointOnArc(canvasCenter, canvasMaxRadius, angle);
        SKPoint p1 = RenderMath.GetPointOnArc(canvasCenter, canvasMaxRadius, angle + 180);
        
        canvas.DrawLine(p0, p1, brushes.AngleTickPen);
    }

    private void DrawPeers(SKCanvas canvas, Chart chart)
    {
        foreach (KeyValuePair<int, Peer> peer in mainView.PeerManager.Peers)
        {
            float measureDecimal = chart.Timestamp2MeasureDecimal(peer.Value.Timestamp);
            if (!MathExtensions.GreaterAlmostEqual(measureDecimal, CurrentMeasureDecimal) || chart.GetScaledMeasureDecimal(measureDecimal, RenderConfig.ShowHiSpeed) > ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal) return;
            
            float scale = GetNoteScale(chart, measureDecimal);
            SKRect rect = GetRect(scale);
            
            canvas.DrawOval(rect, brushes.GetPeerPen(peer.Value.SkiaColor, scale));
        }
    }
    
    // ____ NOTES
    private void DrawMeasureLines(SKCanvas canvas, Chart chart)
    {
        float interval = 1.0f / RenderConfig.BeatDivision;
        float start = MathF.Ceiling(CurrentMeasureDecimal * RenderConfig.BeatDivision) * interval;
        float end = ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal;
        
        for (float i = start; chart.GetScaledMeasureDecimal(i, RenderConfig.ShowHiSpeed) < end; i += interval)
        {
            TimeScaleData? timeScaleData = chart.BinarySearchTimeScales(i);
            if (timeScaleData == null) break;
            if (timeScaleData is { HiSpeed: <= 0.001f, IsLast: true }) break;
                
            float scale = GetNoteScale(chart, i);
            SKRect rect = GetRect(scale);
            if (!RenderMath.InRange(scale)) continue;

            bool isMeasure = Math.Abs(i - (int)i) < 0.001f;
            canvas.DrawOval(rect, isMeasure ? brushes.MeasurePen : brushes.BeatPen);
        }
    }

    private void DrawSyncs(SKCanvas canvas, Chart chart)
    {
        List<Note> visibleNotes = chart.Notes.Where(x =>
            x is { IsSegment: false, IsMask: false } && (!x.IsChain || x is { IsChain: true, IsRNote: true }) // because apparently R Note chains have syncs. Just being accurate to the game (:
            && MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).ToList();
        
        // This code is from saturn, lol
        for (int i = 1; i < visibleNotes.Count; i++)
        {
            Note current = visibleNotes[i];
            Note previous = visibleNotes[i - 1];
            
            if (current.BeatData.FullTick != previous.BeatData.FullTick) continue;
            if ((current.NoteType is NoteType.HoldStart || previous.NoteType is NoteType.HoldStart) && current.Position == previous.Position && current.Size == previous.Size) continue;
            
            float scale = GetNoteScale(chart, current.BeatData.MeasureDecimal);
            
            if (!RenderMath.InRange(scale)) continue;
            
            SKRect rect = GetRect(scale);
            
            drawSyncConnector(current, previous, rect, scale);
            drawSyncOutline(current, previous, rect, scale);
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

        void drawSyncOutline(Note current, Note previous, SKRect rect, float scale)
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

                SKPoint control1 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f, currentPos + currentSweep - controlOffset);
                SKPoint arcEdge1 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f - outlineOffset, currentPos + currentSweep);
            
                SKPoint control2 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f, currentPos + controlOffset);
                SKPoint arcEdge2 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f + outlineOffset, currentPos);

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
            
                SKPoint control3 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f, previousPos + previousSweep - controlOffset);
                SKPoint arcEdge3 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f - outlineOffset, previousPos + previousSweep);
            
                SKPoint control4 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f, previousPos + controlOffset);
                SKPoint arcEdge4 = RenderMath.GetPointOnArc(canvasCenter, rect.Width * 0.5f + outlineOffset, previousPos);

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

        TruncateMode truncateMode = note.NoteType switch
        {
            NoteType.HoldSegment => TruncateMode.OutlineHoldSegment,
            NoteType.MaskAdd => TruncateMode.OutlineMask,
            NoteType.MaskRemove => TruncateMode.OutlineMask,
            _ => TruncateMode.OutlineNote
        };

        if (note.Size != 60) TruncateArc(ref selectedData, truncateMode);
        else TrimCircleArc(ref selectedData);

        float widthMultiplier = note.NoteType is NoteType.HoldSegment ? 0.75f : 1;
        canvas.DrawArc(selectedData.Rect, selectedData.StartAngle, selectedData.SweepAngle, false, brushes.GetSelectionPen(canvasScale * selectedData.Scale * widthMultiplier));
    }

    private void DrawHighlight(SKCanvas canvas, Chart chart, Note note)
    {
        ArcData selectedData = GetArc(chart, note);

        TruncateMode truncateMode = note.NoteType switch
        {
            NoteType.HoldSegment => TruncateMode.OutlineHoldSegment,
            NoteType.MaskAdd => TruncateMode.OutlineMask,
            NoteType.MaskRemove => TruncateMode.OutlineMask,
            _ => TruncateMode.OutlineNote
        };

        if (note.Size != 60) TruncateArc(ref selectedData, truncateMode);
        else TrimCircleArc(ref selectedData);

        float widthMultiplier = note.NoteType is NoteType.HoldSegment ? 0.75f : 1;
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
            
            path.MoveTo(RenderMath.GetPointOnArc(canvasCenter, radiusInner, isEven ? angleA : angleB));
            path.LineTo(RenderMath.GetPointOnArc(canvasCenter, radiusOuter, isEven ? angleA : angleB));
            path.LineTo(RenderMath.GetPointOnArc(canvasCenter, radiusInner, isEven ? angleB : angleA));
        }

        canvas.DrawPath(path, brushes.BonusFill);
    }
    
    /// <summary>
    /// Performs better than the original by drawing hold-by-hold, not segment-by-segment. Saves dozens, if not hundreds of calls to SkiaSharp and fixes the tv-static-looking seams on dense holds. <br/>
    /// This first batches notes into a "hold" struct, then draws each hold.
    /// </summary>
    private void DrawHolds(SKCanvas canvas, Chart chart)
    {
        List<Note> visibleNotes = chart.Notes.Where(x =>
        {
            float scaledMeasureDecimal = chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed);
            bool inVisionRange = MathExtensions.GreaterAlmostEqual(scaledMeasureDecimal, ScaledCurrentMeasureDecimal) && MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal) && scaledMeasureDecimal <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal;
            bool aroundVisionRange = scaledMeasureDecimal < ScaledCurrentMeasureDecimal && x.NextReferencedNote != null && chart.GetScaledMeasureDecimal(x.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) > ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal;
            
            return x.IsHold && (inVisionRange || aroundVisionRange);
        }).ToList();
        
        HashSet<Note> checkedNotes = [];
        List<Hold> holdNotes = [];
        
        foreach (Note note in visibleNotes)
        {
            if (checkedNotes.Contains(note)) continue;

            Hold hold = new();

            foreach (Note reference in note.References())
            {
                if (visibleNotes.Contains(reference)) hold.Segments.Add(reference);
                checkedNotes.Add(reference);
            }
            
            holdNotes.Add(hold);
        }

        // The brainfuck. If there's any questions, ask me, and I'll help you decipher it.
        foreach (Hold hold in holdNotes)
        {
            SKPath path = new();

            // This must be one of them darn damn dangit hold notes where the first segment is behind the camera
            // and the second is outside of vision range. Aw shucks, we have to do some extra special work.
            if (chart.GetScaledMeasureDecimal(hold.Segments[0].BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) < ScaledCurrentMeasureDecimal && hold.Segments.Count == 1)
            {
                Note prev = hold.Segments[0];
                Note? next = prev.NextReferencedNote;

                if (next == null) continue;
                
                // Aw, nevermind. It ain't one of them darn damn dangit hold notes where the first segment is behind the camera and the second is outside of vision range.
                if (chart.GetScaledMeasureDecimal(next.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal) continue;
                
                ArcData prevData = getArcData(prev, TruncateMode.Hold);
                ArcData nextData = getArcData(next, TruncateMode.Hold);

                float ratio = MathExtensions.InverseLerp(CurrentMeasureDecimal, next.BeatData.MeasureDecimal, prev.BeatData.MeasureDecimal);
                
                if (float.Abs(nextData.StartAngle - prevData.StartAngle) > 180)
                {
                    if (nextData.StartAngle > prevData.StartAngle) nextData.StartAngle -= 360;
                    else prevData.StartAngle -= 360;
                }
                
                ArcData intermediateData = new(canvasRect, 1, MathExtensions.Lerp(nextData.StartAngle, prevData.StartAngle, ratio), MathExtensions.Lerp(nextData.SweepAngle, prevData.SweepAngle, ratio));
                path.ArcTo(intermediateData.Rect, intermediateData.StartAngle, intermediateData.SweepAngle, false);
                path.LineTo(canvasCenter);
                canvas.DrawPath(path, brushes.HoldFill);
                
                continue;
            }
            
            for (int i = 0; i < hold.Segments.Count; i++)
            {
                Note note = hold.Segments[i];
                ArcData currentData = getArcData(note, TruncateMode.Hold);
                
                // First part of the path. Must be an arc.
                if (i == 0)
                {
                    // If the hold start is visible there's no need to interpolate.
                    if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
                    {
                        path.ArcTo(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, true);
                    }
                    
                    // If it's a segment, there must be an earlier note off-screen.
                    else if (note.PrevReferencedNote != null)
                    {
                        Note prevNote = note.PrevReferencedNote;
                        ArcData prevData = getArcData(prevNote, TruncateMode.Hold);
                        
                        float ratio = MathExtensions.InverseLerp(ScaledCurrentMeasureDecimal, chart.GetScaledMeasureDecimal(note.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed), chart.GetScaledMeasureDecimal(prevNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed));
                
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
                
                if ((i == 0 && note.NoteType is NoteType.HoldSegment or NoteType.HoldEnd) || (i != 0 && i != hold.Segments.Count - 1))
                {
                    // Line to right edge
                    path.LineTo(RenderMath.GetPointOnArc(canvasCenter, currentData.Rect.Width * 0.5f, currentData.StartAngle + currentData.SweepAngle));
                } 
                
                if (i == hold.Segments.Count - 1)
                {
                    // If there's a next note there can't be a final arc.
                    if (note.NextReferencedNote != null)
                    {
                        // Line to right edge
                        path.LineTo(RenderMath.GetPointOnArc(canvasCenter, currentData.Rect.Width * 0.5f, currentData.StartAngle + currentData.SweepAngle));
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
            for (int i = hold.Segments.Count - 1; i >= 0; i--)
            {
                Note note = hold.Segments[i];
                
                // *technically unnecessary to skip, but doing it just for consistency.
                if (i == 0 && note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote) continue;
                
                ArcData currentData = getArcData(note, TruncateMode.Hold);
                path.LineTo(RenderMath.GetPointOnArc(canvasCenter, currentData.Rect.Width * 0.5f, currentData.StartAngle));
            }
            
            canvas.DrawPath(path, brushes.HoldFill);
        }
        
        // Second and Third foreach to ensure notes are rendered on top of surfaces.
        // Reversed so notes further away are rendered first, then closer notes
        // are rendered on top.
        visibleNotes.Reverse();
        
        // Hold Ends
        foreach (Note note in visibleNotes)
        {
            if (note.NoteType is not NoteType.HoldEnd) continue;
            
            ArcData currentData = getArcData(note, TruncateMode.Hold);
            
            if (!RenderMath.InRange(currentData.Scale) || note.BeatData.MeasureDecimal < CurrentMeasureDecimal) continue;
            
            canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetHoldEndPen(canvasScale * currentData.Scale));
            
            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }
        
        // Hold Start/Segment
        foreach (Note note in visibleNotes)
        {
            if (note.NoteType is NoteType.HoldEnd) continue;

            ArcData data = GetArc(chart, note);
            
            if (!RenderMath.InRange(data.Scale) || note.BeatData.MeasureDecimal < CurrentMeasureDecimal) continue;

            if (note.IsRNote) DrawRNote(canvas, note, data);
            
            if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                if (note.Size != 60)
                {
                    TruncateArc(ref data, TruncateMode.ExcludeCaps);
                    DrawNoteCaps(canvas, data.Rect, data.StartAngle, data.SweepAngle, data.Scale);
                }
                
                canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));
            }

            if (note.NoteType is NoteType.HoldSegment && !IsPlaying)
            {
                if (note.Size != 60) TruncateArc(ref data, TruncateMode.ExcludeCaps);
                
                canvas.DrawArc(data.Rect, data.StartAngle + 2f, data.SweepAngle - 4f, false, brushes.GetNotePen(note, canvasScale * data.Scale * 0.5f));
            }
            
            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }

        return;

        // Preventing a little bit of code repetition. Not sure if this is cleaner or not :^)
        ArcData getArcData(Note note, TruncateMode mode)
        {
            ArcData arc = GetArc(chart, note);
            if (note.Size != 60) TruncateArc(ref arc, mode);
            else TrimCircleArc(ref arc);

            return arc;
        }
    }

    /// <summary>
    /// Old Hold Rendering Method. See above for new method.
    /// </summary>
    private void DrawHoldsLegacy(SKCanvas canvas, Chart chart)
    {
        // Long-ass line of code. Not a fan but idk how else to write this where it still makes sense. TL;DR:
        //
        // IsHold &&
        // (Note is in vision range || Next note it's referencing is in front of vision range)
        List<Note> visibleNotes = chart.Notes.Where(x =>
            x.IsHold &&
            (
                (MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal) && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal)
                ||
                (x.NextReferencedNote != null && x.BeatData.MeasureDecimal < CurrentMeasureDecimal && chart.GetScaledMeasureDecimal(x.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) > ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal)
            )
        ).ToList();
        
        foreach (Note note in visibleNotes)
        {
            ArcData currentData = getArcData(note);
            
            bool currentVisible = note.BeatData.MeasureDecimal >= CurrentMeasureDecimal;
            bool nextVisible = note.NextReferencedNote != null && chart.GetScaledMeasureDecimal(note.NextReferencedNote.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal;
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
        
        // Second and Third foreach to ensure notes are rendered on top of surfaces.
        // Reversed so notes further away are rendered first, then closer notes
        // are rendered on top.
        visibleNotes.Reverse();

        // Hold Ends
        foreach (Note note in visibleNotes)
        {
            if (note.NoteType is not NoteType.HoldEnd) continue;
            
            ArcData currentData = getArcData(note);
            
            if (!RenderMath.InRange(currentData.Scale) || note.BeatData.MeasureDecimal < CurrentMeasureDecimal) continue;
            
            canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetHoldEndPen(canvasScale * currentData.Scale));
            
            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }
        
        // Hold Start/Segment
        foreach (Note note in visibleNotes)
        {
            if (note.NoteType is NoteType.HoldEnd) continue;

            ArcData currentData = getArcData(note);
            
            if (!RenderMath.InRange(currentData.Scale) || note.BeatData.MeasureDecimal < CurrentMeasureDecimal) continue;

            if (note.IsRNote) DrawRNote(canvas, note, currentData);
            
            if (note.NoteType is NoteType.HoldStart or NoteType.HoldStartRNote)
            {
                if (note.Size != 60) DrawNoteCaps(canvas, currentData.Rect, currentData.StartAngle, currentData.SweepAngle, currentData.Scale);
                canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetNotePen(note, canvasScale * currentData.Scale));
            }

            if (note.NoteType is NoteType.HoldSegment && !IsPlaying)
            {
                canvas.DrawArc(currentData.Rect, currentData.StartAngle, currentData.SweepAngle, false, brushes.GetNotePen(note, canvasScale * currentData.Scale * 0.5f));
            }
            
            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }

        return;

        // Preventing a little bit of code reptition. Not sure if this is cleaner or not :^)
        ArcData getArcData(Note note)
        {
            ArcData arc = GetArc(chart, note);
            if (note.Size != 60) TruncateArc(ref arc, TruncateMode.IncludeCaps);
            else TrimCircleArc(ref arc);

            return arc;
        }
    }
    
    private void DrawNotes(SKCanvas canvas, Chart chart)
    {
        // Reverse to draw from middle out => preserves depth overlap
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x =>
            x is { IsHold: false, IsMask: false }
            && MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);

            if (!RenderMath.InRange(data.Scale)) continue;

            if (note.IsRNote) DrawRNote(canvas, note, data);

            if (note.IsBonus) DrawBonusGlow(canvas, note, data);
            
            // Normal Note
            if (note.Size != 60)
            {
                TruncateArc(ref data, TruncateMode.ExcludeCaps);
                DrawNoteCaps(canvas, data.Rect, data.StartAngle, data.SweepAngle, data.Scale);
            }
            
            canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));

            if (note.IsBonus) DrawBonusFill(canvas, note, data);
            
            if (RenderConfig.ShowChainStripes && note.NoteType is NoteType.Chain or NoteType.ChainRNote)
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
                        SKPoint t0 = RenderMath.GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3 + 3);
                        SKPoint t1 = RenderMath.GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3 + 1.5f);
                        SKPoint t2 = RenderMath.GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3 + 3);
                        
                        path.MoveTo(t0);
                        path.LineTo(t1);
                        path.LineTo(t2);
                    }

                    if (note.Size != 60 && i == stripes - 4)
                    {
                        SKPoint t1 = RenderMath.GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3);
                        SKPoint t2 = RenderMath.GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3);
                        SKPoint t3 = RenderMath.GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3 + 1.5f);
                        
                        path.MoveTo(t1);
                        path.LineTo(t2);
                        path.LineTo(t3);
                        
                        continue;
                    }
                    
                    SKPoint p0 = RenderMath.GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3);
                    SKPoint p1 = RenderMath.GetPointOnArc(canvasCenter, radiusInner, data.StartAngle + i * -3 - 1.5f);
                    SKPoint p2 = RenderMath.GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3);
                    SKPoint p3 = RenderMath.GetPointOnArc(canvasCenter, radiusOuter, data.StartAngle + i * -3 + 1.5f);
                    
                    path.MoveTo(p0);
                    path.LineTo(p1);
                    path.LineTo(p2);
                    path.LineTo(p3);
                }
                
                canvas.DrawPath(path, brushes.ChainStripeFill);
            }
            
            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }
    }

    private void DrawMaskNotes(SKCanvas canvas, Chart chart)
    {
        IEnumerable<Note> visibleNotes = chart.Notes.Where(x =>
            x.IsMask
            && MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();
        
        foreach (Note note in visibleNotes)
        {
            ArcData data = GetArc(chart, note);

            if (!RenderMath.InRange(data.Scale)) continue;
            
            canvas.DrawArc(data.Rect, data.StartAngle, data.SweepAngle, false, brushes.GetNotePen(note, canvasScale * data.Scale));

            if (mainView.ChartEditor.SelectedNotes.Contains(note)) DrawSelection(canvas, chart, note);
            if (note == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, chart, note);
        }
    }

    private void DrawGimmickNotes(SKCanvas canvas, Chart chart)
    {
        IEnumerable<Gimmick> visibleGimmicks = chart.Gimmicks.Where(x =>
            MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Gimmick gimmick in visibleGimmicks)
        {
            ArcData data = GetArc(chart, gimmick);
            
            if (!RenderMath.InRange(data.Scale)) continue;
            
            canvas.DrawOval(data.Rect, brushes.GetGimmickPen(gimmick, canvasScale * data.Scale));
            if (gimmick == mainView.ChartEditor.HighlightedElement) DrawHighlight(canvas, data);
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
            && MathExtensions.GreaterAlmostEqual(x.BeatData.MeasureDecimal, CurrentMeasureDecimal)
            && chart.GetScaledMeasureDecimal(x.BeatData.MeasureDecimal, RenderConfig.ShowHiSpeed) <= ScaledCurrentMeasureDecimal + visibleDistanceMeasureDecimal).Reverse();

        foreach (Note note in visibleNotes)
        {
            float scale = GetNoteScale(chart, note.BeatData.MeasureDecimal);
            if (!RenderMath.InRange(scale)) continue;
            
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

            if (note.IsSnap) drawSnap(note, rect, arrowDirection);
            else drawSlide(note, rect, scale, arrowDirection);
        }

        return;
        
        void drawSnap(Note note, SKRect rect, int arrowDirection)
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
                
                SKPoint  p1 = RenderMath.GetPointOnArc(canvasCenter, radius * snapRadiusOffset, i + snapArrowWidth);
                SKPoint  p2 = RenderMath.GetPointOnArc(canvasCenter, radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint  p3 = RenderMath.GetPointOnArc(canvasCenter, radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint  p4 = RenderMath.GetPointOnArc(canvasCenter, snapArrowThickness + radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint  p5 = RenderMath.GetPointOnArc(canvasCenter, snapArrowThickness + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint  p6 = RenderMath.GetPointOnArc(canvasCenter, snapArrowThickness + radius * snapRadiusOffset, i + snapArrowWidth);
                
                SKPoint  p7 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + radius * snapRadiusOffset, i + snapArrowWidth);
                SKPoint  p8 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint  p9 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint p10 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + snapArrowThickness + radius * snapRadiusOffset, i - snapArrowWidth);
                SKPoint p11 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + snapArrowThickness + radius * (snapRadiusOffset - snapArrowLength * arrowDirection), i);
                SKPoint p12 = RenderMath.GetPointOnArc(canvasCenter, snapRowOffset + snapArrowThickness + radius * snapRadiusOffset, i + snapArrowWidth);

                SKPath path1 = new();
                SKPath path2 = new();
                
                path1.MoveTo(p1);
                path1.LineTo(p2);
                path1.LineTo(p3);
                path1.LineTo(p4);
                path1.LineTo(p5);
                path1.LineTo(p6);
                
                path2.MoveTo(p7);
                path2.LineTo(p8);
                path2.LineTo(p9);
                path2.LineTo(p10);
                path2.LineTo(p11);
                path2.LineTo(p12);
                
                canvas.DrawPath(path1, brushes.GetSnapFill(note.NoteType));
                canvas.DrawPath(path2, brushes.GetSnapFill(note.NoteType));
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
                
                SKPath path = new SKPath();   
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
    public readonly float Scale = scale;
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

    internal static bool InRange(float scale)
    {
        return scale is > 0 and < 1.001f;
    }
}

