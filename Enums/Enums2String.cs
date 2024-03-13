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
            _ => type.ToString()
        };
    }

    public static string NoteType2String(NoteType type)
    {
        return type switch
        {
            NoteType.Touch => Resources.Note_Touch,
            NoteType.TouchBonus => $"{Resources.Note_Touch} [{Resources.BonusType_Bonus}]",
            NoteType.SnapForward => Resources.Note_SnapForward,
            NoteType.SnapBackward => Resources.Note_SnapBackward,
            NoteType.SlideClockwise => Resources.Note_SlideClockwise,
            NoteType.SlideClockwiseBonus => $"{Resources.Note_SlideClockwise} [{Resources.BonusType_Bonus}]",
            NoteType.SlideCounterclockwise => Resources.Note_SlideCounterclockwise,
            NoteType.SlideCounterclockwiseBonus => $"{Resources.Note_SlideCounterclockwise} [{Resources.BonusType_Bonus}]",
            NoteType.HoldStart => Resources.Note_HoldStart,
            NoteType.HoldSegment => Resources.Note_HoldSegment,
            NoteType.HoldEnd => Resources.Note_HoldEnd,
            NoteType.MaskAdd => Resources.Note_MaskAdd,
            NoteType.MaskRemove => Resources.Note_MaskRemove,
            NoteType.EndOfChart => Resources.Note_EndOfChart,
            NoteType.Chain => Resources.Note_Chain,
            NoteType.TouchRNote => $"{Resources.Note_Touch} [{Resources.BonusType_RNote}]",
            NoteType.SnapForwardRNote => $"{Resources.Note_SnapForward} [{Resources.BonusType_RNote}]",
            NoteType.SnapBackwardRNote => $"{Resources.Note_SnapBackward} [{Resources.BonusType_RNote}]",
            NoteType.SlideClockwiseRNote => $"{Resources.Note_SlideClockwise} [{Resources.BonusType_RNote}]",
            NoteType.SlideCounterclockwiseRNote => $"{Resources.Note_SlideCounterclockwise} [{Resources.BonusType_RNote}]",
            NoteType.HoldStartRNote => $"{Resources.Note_HoldStart} [{Resources.BonusType_RNote}]",
            NoteType.ChainRNote => $"{Resources.Note_Chain} [{Resources.BonusType_RNote}]",
            _ => type.ToString()
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