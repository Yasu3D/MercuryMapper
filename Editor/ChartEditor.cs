using System;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.Views;

namespace MercuryMapper.Editor;

public class ChartEditor(MainView main)
{
    private MainView mainView = main;
    
    public readonly Cursor Cursor = new();
    public Chart Chart { get; private set; } = new();
    
    public ChartEditorState State { get; private set; }
    
    public float CurrentMeasure { get; set; }
    public NoteType CurrentNoteType { get; set; } = NoteType.Touch;
    public BonusType CurrentBonusType { get; set; } = BonusType.None;
    public MaskDirection CurrentMaskDirection { get; set; } = MaskDirection.Clockwise;

    public void NewChart(string musicFilePath, string author, float bpm, int timeSigUpper, int timeSigLower)
    {
        Chart = new()
        {
            AudioFilePath = musicFilePath,
            Author = author
        };

        lock (Chart)
        {
            Gimmick startBpm = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.BpmChange,
                Bpm = bpm,
                TimeStamp = 0
            };

            Gimmick startTimeSig = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.TimeSigChange,
                TimeSig = new(timeSigUpper, timeSigLower),
                TimeStamp = 0
            };
            
            Chart.Gimmicks.Add(startBpm);
            Chart.Gimmicks.Add(startTimeSig);
            Chart.StartBpm = startBpm;
            Chart.StartTimeSig = startTimeSig;
        }
    }

    public void UpdateNoteType()
    {
        switch (CurrentNoteType)
        {
            case NoteType.Touch:
            case NoteType.TouchBonus:
            case NoteType.TouchRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.Touch,
                    BonusType.Bonus => NoteType.TouchBonus,
                    BonusType.RNote => NoteType.TouchRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SnapForward:
            case NoteType.SnapForwardRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SnapForward,
                    BonusType.Bonus => NoteType.SnapForward,
                    BonusType.RNote => NoteType.SnapForwardRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SnapBackward:
            case NoteType.SnapBackwardRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SnapBackward,
                    BonusType.Bonus => NoteType.SnapBackward,
                    BonusType.RNote => NoteType.SnapBackwardRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SlideClockwise:
            case NoteType.SlideClockwiseBonus:
            case NoteType.SlideClockwiseRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SlideClockwise,
                    BonusType.Bonus => NoteType.SlideClockwiseBonus,
                    BonusType.RNote => NoteType.SlideClockwiseRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.SlideCounterclockwise:
            case NoteType.SlideCounterclockwiseBonus:
            case NoteType.SlideCounterclockwiseRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.SlideCounterclockwise,
                    BonusType.Bonus => NoteType.SlideCounterclockwiseBonus,
                    BonusType.RNote => NoteType.SlideCounterclockwiseRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.HoldStart:
            case NoteType.HoldStartRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.HoldStart,
                    BonusType.Bonus => NoteType.HoldStart,
                    BonusType.RNote => NoteType.HoldStartRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.HoldSegment:
            {
                CurrentNoteType = NoteType.HoldSegment;
                break;
            }
            
            case NoteType.HoldEnd:
            {
                CurrentNoteType = NoteType.HoldEnd;
                break;
            }
            
            case NoteType.Chain:
            case NoteType.ChainRNote:
            {
                CurrentNoteType = CurrentBonusType switch
                {
                    BonusType.None => NoteType.Chain,
                    BonusType.Bonus => NoteType.Chain,
                    BonusType.RNote => NoteType.ChainRNote,
                    _ => CurrentNoteType
                };
                break;
            }
            
            case NoteType.MaskAdd:
            {
                CurrentNoteType = NoteType.MaskAdd;
                break;
            }
            
            case NoteType.MaskRemove:
            {
                CurrentNoteType = NoteType.MaskRemove;
                break;
            }
            
            case NoteType.EndOfChart:
            {
                CurrentNoteType = NoteType.EndOfChart;
                break;
            }
            
            default: return;
        }
    }
}