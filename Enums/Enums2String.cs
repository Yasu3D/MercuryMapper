using System;
using MercuryMapper.Assets.Lang;

namespace MercuryMapper.Enums;

public static class Enums2String
{
    public static string GimmickType2String(GimmickType type)
    {
        return type switch
        {
            GimmickType.None => Resources.Generic_None,
            GimmickType.BpmChange => Resources.Gimmick_BpmChange,
            GimmickType.TimeSigChange => Resources.Gimmick_TimeSigChange,
            GimmickType.HiSpeedChange => Resources.Gimmick_HiSpeedChange,
            GimmickType.ReverseEffectStart => Resources.Gimmick_ReverseEffectStart,
            GimmickType.ReverseEffectEnd => Resources.Gimmick_ReverseEffectEnd,
            GimmickType.ReverseNoteEnd => Resources.Gimmick_ReverseNoteEnd,
            GimmickType.StopStart => Resources.Gimmick_StopStart,
            GimmickType.StopEnd => Resources.Gimmick_StopEnd, 
            GimmickType.EndOfChart => Resources.Gimmick_EndOfChart,
            _ => type.ToString(),
        };
    }

    public static string NoteType2String(NoteType noteType, BonusType bonusType, NoteLinkType linkType)
    {
        return (noteType, bonusType, linkType) switch
        {
            (NoteType.Touch, BonusType.None, _) => Resources.Note_Touch,
            (NoteType.Touch, BonusType.Bonus, _) => $"{Resources.Note_Touch} [{Resources.BonusType_Bonus}]",
            (NoteType.Touch, BonusType.RNote, _) => $"{Resources.Note_Touch} [{Resources.BonusType_RNote}]",

            (NoteType.SnapForward, BonusType.None, _) => Resources.Note_SnapForward,
            (NoteType.SnapBackward, BonusType.None, _) => Resources.Note_SnapBackward,

            (NoteType.SnapForward, BonusType.Bonus, _) => $"{Resources.Note_SnapForward} [{Resources.BonusType_Bonus}]",
            (NoteType.SnapBackward, BonusType.Bonus, _) => $"{Resources.Note_SnapBackward} [{Resources.BonusType_Bonus}]",

            (NoteType.SnapForward, BonusType.RNote, _) => $"{Resources.Note_SnapForward} [{Resources.BonusType_RNote}]",
            (NoteType.SnapBackward, BonusType.RNote, _) => $"{Resources.Note_SnapBackward} [{Resources.BonusType_RNote}]",

            (NoteType.SlideClockwise, BonusType.None, _) => Resources.Note_SlideClockwise,
            (NoteType.SlideCounterclockwise, BonusType.None, _) => Resources.Note_SlideCounterclockwise,

            (NoteType.SlideClockwise, BonusType.Bonus, _) => $"{Resources.Note_SlideClockwise} [{Resources.BonusType_Bonus}]",
            (NoteType.SlideCounterclockwise, BonusType.Bonus, _) => $"{Resources.Note_SlideCounterclockwise} [{Resources.BonusType_Bonus}]",

            (NoteType.SlideClockwise, BonusType.RNote, _) => $"{Resources.Note_SlideClockwise} [{Resources.BonusType_RNote}]",
            (NoteType.SlideCounterclockwise, BonusType.RNote, _) => $"{Resources.Note_SlideCounterclockwise} [{Resources.BonusType_RNote}]",

            (NoteType.Chain, BonusType.None, _) => Resources.Note_Chain,
            (NoteType.Chain, BonusType.Bonus, _) => $"{Resources.Note_Chain} [{Resources.BonusType_Bonus}]",
            (NoteType.Chain, BonusType.RNote, _) => $"{Resources.Note_Chain} [{Resources.BonusType_RNote}]",

            (NoteType.Hold, BonusType.None, NoteLinkType.Start) => Resources.Note_HoldStart,
            (NoteType.Hold, BonusType.Bonus, NoteLinkType.Start) => $"{Resources.Note_HoldStart} [{Resources.BonusType_Bonus}]",
            (NoteType.Hold, BonusType.RNote, NoteLinkType.Start) => $"{Resources.Note_HoldStart} [{Resources.BonusType_RNote}]",

            (NoteType.Hold, _, NoteLinkType.Point) => Resources.Note_HoldSegment,
            (NoteType.Hold, _, NoteLinkType.End) => Resources.Note_HoldEnd,

            (NoteType.MaskAdd, _, _) => Resources.Note_MaskAdd,
            (NoteType.MaskRemove, _, _) => Resources.Note_MaskRemove,
            
            (NoteType.Trace, _, NoteLinkType.Start) => Resources.Note_TraceStart,
            (NoteType.Trace, _, NoteLinkType.Point) => Resources.Note_TraceSegment,
            (NoteType.Trace, _, NoteLinkType.End) => Resources.Note_TraceEnd,
            
            (NoteType.Damage, _, _) => Resources.Note_Damage,
            
            _ => "Unknown Note",
        };
    }

    public static string MaskDirection2String(MaskDirection direction)
    {
        return direction switch
        {
            MaskDirection.Counterclockwise => Resources.MaskDirection_Counterclockwise,
            MaskDirection.Clockwise => Resources.MaskDirection_Clockwise,
            MaskDirection.Center => Resources.MaskDirection_Center,
            _ => direction.ToString(),
        };
    }
}