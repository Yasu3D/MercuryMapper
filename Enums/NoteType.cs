namespace MercuryMapper.Enums;

public enum NoteType
{
    None = 0,
    Touch = 1,
    SnapForward = 2,
    SnapBackward = 3,
    SlideClockwise = 4,
    SlideCounterclockwise = 5,
    HoldStart = 6,
    HoldSegment = 7,
    HoldEnd = 8,
    MaskAdd = 9,
    MaskRemove = 10,
    Chain = 12,
    // 13 to 26 occupied by MER R-Note IDs.
    // Any further notes are not supported in MER anyway
    // and SAT does not use numbers for note types,
    // so the ID skip doesn't matter.
    Damage = 27,
    TraceStart = 28,
    TraceSegment = 29,
    TraceEnd = 30,
}