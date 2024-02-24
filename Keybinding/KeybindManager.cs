using System;
using Avalonia.Input;

namespace MercuryMapper.Keybinding;

public class KeybindManager()
{
    public Keybind KeybindFileNew = new(Key.N, false, true, false);
    public Keybind KeybindFileOpen = new(Key.O, false, true, false);
    public Keybind KeybindFileSave = new(Key.S, false, true, false);
    public Keybind KeybindFileSaveAs = new(Key.S, true, true, false);
    public Keybind KeybindFileSettings = new(Key.S, false, true, true);

    public Keybind KeybindEditUndo = new(Key.Z, false, true, false);
    public Keybind KeybindEditRedo = new(Key.Y, false, true, false);
    public Keybind KeybindEditCut = new(Key.X, false, true, false);
    public Keybind KeybindEditCopy = new(Key.C, false, true, false);
    public Keybind KeybindEditPaste = new(Key.V, false, true, false);

    public Keybind KeybindEditorInsert = new(Key.I);
    public Keybind KeybindEditorPlay = new(Key.Space);
    
    public Keybind KeybindEditorNoteTypeTouch = new(Key.D1);
    public Keybind KeybindEditorNoteTypeSlideClockwise = new(Key.D2);
    public Keybind KeybindEditorNoteTypeSlideCounterclockwise = new(Key.D3);
    public Keybind KeybindEditorNoteTypeSnapForward = new(Key.D4);
    public Keybind KeybindEditorNoteTypeSnapBackward = new(Key.D5);
    public Keybind KeybindEditorNoteTypeChain = new(Key.D6);
    public Keybind KeybindEditorNoteTypeHold = new(Key.D7);
    public Keybind KeybindEditorNoteTypeMaskAdd = new(Key.D8);
    public Keybind KeybindEditorNoteTypeMaskRemove = new(Key.D9);
    public Keybind KeybindEditorNoteTypeEndOfChart = new(Key.D0);
    
    public void OnKeyDown(KeyEventArgs e)
    {
        Keybind keybind = new(e);
        
        if (Keybind.Compare(keybind, KeybindFileNew)) 
        {
            Console.WriteLine("KeybindFileNew");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindFileOpen)) 
        {
            Console.WriteLine("KeybindFileOpen");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindFileSave)) 
        {
            Console.WriteLine("KeybindFileSave");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindFileSaveAs)) 
        {
            Console.WriteLine("KeybindFileSaveAs");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindFileSettings)) 
        {
            Console.WriteLine("OpenSettings");
            e.Handled = true;
            return;
        }

        if (Keybind.Compare(keybind, KeybindEditUndo)) 
        {
            Console.WriteLine("Undo");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditRedo)) 
        {
            Console.WriteLine("Redo");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditCut)) 
        {
            Console.WriteLine("Cut");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditCopy)) 
        {
            Console.WriteLine("Copy");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditPaste)) 
        {
            Console.WriteLine("Paste");
            e.Handled = true;
            return;
        }
        
        if (Keybind.Compare(keybind, KeybindEditorInsert)) 
        {
            Console.WriteLine("InsertNote");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorPlay)) 
        {
            Console.WriteLine("Play");
            e.Handled = true;
            return;
        }
        
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeTouch)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeSlideClockwise)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeSlideCounterclockwise)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeSnapForward)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeSnapBackward)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeChain)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeHold)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeMaskAdd)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeMaskRemove)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, KeybindEditorNoteTypeEndOfChart)) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
    }
}