using MercuryMapper.Data;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class EditNote(Note note, Note newNote) : IOperation
{
    public Note BaseNote { get; } = note;
    public Note OldNote { get; } = new(note, note.Guid);
    public Note NewNote { get; } = newNote;
    
    public void Undo()
    {
        BaseNote.Position = OldNote.Position;
        BaseNote.Size = OldNote.Size;
        BaseNote.NoteType = OldNote.NoteType;
        BaseNote.BonusType = OldNote.BonusType;
        BaseNote.MaskDirection = OldNote.MaskDirection;
        BaseNote.BeatData = OldNote.BeatData;
        BaseNote.RenderSegment = OldNote.RenderSegment;
        BaseNote.Color = OldNote.Color;
    }
    
    public void Redo()
    {
        BaseNote.Position = NewNote.Position;
        BaseNote.Size = NewNote.Size;
        BaseNote.NoteType = NewNote.NoteType;
        BaseNote.BonusType = NewNote.BonusType;
        BaseNote.MaskDirection = NewNote.MaskDirection;
        BaseNote.BeatData = NewNote.BeatData;
        BaseNote.RenderSegment = NewNote.RenderSegment;
        BaseNote.Color = NewNote.Color;
    }
}