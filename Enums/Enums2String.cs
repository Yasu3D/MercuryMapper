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
            _ => type.ToString()
        };
    }

    public static string NoteType2String(NoteType noteType, BonusType bonusType)
    {
        return (noteType, bonusType) switch
        {
            (NoteType.Touch, BonusType.None) => Resources.Note_Touch,
            (NoteType.Touch, BonusType.Bonus) => $"{Resources.Note_Touch} [{Resources.BonusType_Bonus}]",
            (NoteType.Touch, BonusType.RNote) => $"{Resources.Note_Touch} [{Resources.BonusType_RNote}]",

            (NoteType.SnapForward, BonusType.None) => Resources.Note_SnapForward,
            (NoteType.SnapBackward, BonusType.None) => Resources.Note_SnapBackward,

            (NoteType.SnapForward, BonusType.Bonus) => $"{Resources.Note_SnapForward} [{Resources.BonusType_Bonus}]",
            (NoteType.SnapBackward, BonusType.Bonus) => $"{Resources.Note_SnapBackward} [{Resources.BonusType_Bonus}]",

            (NoteType.SnapForward, BonusType.RNote) => $"{Resources.Note_SnapForward} [{Resources.BonusType_RNote}]",
            (NoteType.SnapBackward, BonusType.RNote) => $"{Resources.Note_SnapBackward} [{Resources.BonusType_RNote}]",

            (NoteType.SlideClockwise, BonusType.None) => Resources.Note_SlideClockwise,
            (NoteType.SlideCounterclockwise, BonusType.None) => Resources.Note_SlideCounterclockwise,

            (NoteType.SlideClockwise, BonusType.Bonus) => $"{Resources.Note_SlideClockwise} [{Resources.BonusType_Bonus}]",
            (NoteType.SlideCounterclockwise, BonusType.Bonus) => $"{Resources.Note_SlideCounterclockwise} [{Resources.BonusType_Bonus}]",

            (NoteType.SlideClockwise, BonusType.RNote) => $"{Resources.Note_SlideClockwise} [{Resources.BonusType_RNote}]",
            (NoteType.SlideCounterclockwise, BonusType.RNote) => $"{Resources.Note_SlideCounterclockwise} [{Resources.BonusType_RNote}]",

            (NoteType.Chain, BonusType.None) => Resources.Note_Chain,
            (NoteType.Chain, BonusType.Bonus) => $"{Resources.Note_Chain} [{Resources.BonusType_Bonus}]",
            (NoteType.Chain, BonusType.RNote) => $"{Resources.Note_Chain} [{Resources.BonusType_RNote}]",

            (NoteType.HoldStart, BonusType.None) => Resources.Note_HoldStart,
            (NoteType.HoldStart, BonusType.Bonus) => $"{Resources.Note_HoldStart} [{Resources.BonusType_Bonus}]",
            (NoteType.HoldStart, BonusType.RNote) => $"{Resources.Note_HoldStart} [{Resources.BonusType_RNote}]",

            (NoteType.HoldSegment, _) => Resources.Note_HoldSegment,
            (NoteType.HoldEnd, _) => Resources.Note_HoldEnd,

            (NoteType.MaskAdd, _) => Resources.Note_MaskAdd,
            (NoteType.MaskRemove, _) => Resources.Note_MaskRemove,
            _ => "Unknown Note"
        };
    }

    public static string MaskDirection2String(MaskDirection direction)
    {
        return direction switch
        {
            MaskDirection.Counterclockwise => Resources.MaskDirection_Counterclockwise,
            MaskDirection.Clockwise => Resources.MaskDirection_Clockwise,
            MaskDirection.Center => Resources.MaskDirection_Center,
            _ => direction.ToString()
        };
    }
}