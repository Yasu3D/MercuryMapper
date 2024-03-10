using System;
using System.Collections.Generic;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.UndoRedo.NoteOperations;

public class EditNoteShape(Note note, Note newNote) : IOperation
{
    public Note BaseNote { get; } = note;
    public Note OldNote { get; } = new(note);
    public Note NewNote { get; } = newNote;
    public string Description => "Edit Note Shape";
    
    public void Undo()
    {
        BaseNote.Position = OldNote.Position;
        BaseNote.Size = OldNote.Size;
    }
    
    public void Redo()
    {
        BaseNote.Position = NewNote.Position;
        BaseNote.Size = NewNote.Size;
    }
}

public class EditNoteProperties(Note note, Note newNote) : IOperation
{
    public Note BaseNote { get; } = note;
    public Note OldNote { get; } = new(note);
    public Note NewNote { get; } = newNote;
    public string Description => "Edit Note Properties";
    
    public void Undo()
    {
        BaseNote.NoteType = OldNote.NoteType;
        BaseNote.MaskDirection = OldNote.MaskDirection;
    }
    
    public void Redo()
    {
        BaseNote.NoteType = NewNote.NoteType;
        BaseNote.MaskDirection = NewNote.MaskDirection;
    }
}

public class EditNoteFull(Note note, Note newNote) : IOperation
{
    public Note BaseNote { get; } = note;
    public Note OldNote { get; } = new(note);
    public Note NewNote { get; } = newNote;
    public string Description => "Edit Note";
    
    public void Undo()
    {
        BaseNote.Position = OldNote.Position;
        BaseNote.Size = OldNote.Size;
        BaseNote.NoteType = OldNote.NoteType;
        BaseNote.MaskDirection = OldNote.MaskDirection;
    }
    
    public void Redo()
    {
        BaseNote.Position = NewNote.Position;
        BaseNote.Size = NewNote.Size;
        BaseNote.NoteType = NewNote.NoteType;
        BaseNote.MaskDirection = NewNote.MaskDirection;
    }
}

public class MirrorNote(Note note, Note newNote) : IOperation
{
    public Note BaseNote { get; } = note;
    public Note OldNote { get; } = new(note);
    public Note NewNote { get; } = newNote;
    public string Description => "Mirror Note";
    
    public void Undo()
    {
        BaseNote.Position = OldNote.Position;
        BaseNote.Size = OldNote.Size;
        BaseNote.NoteType = OldNote.NoteType;
    }
    
    public void Redo()
    {
        BaseNote.Position = NewNote.Position;
        BaseNote.Size = NewNote.Size;
        BaseNote.NoteType = NewNote.NoteType;
    }
}