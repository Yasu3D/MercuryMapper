using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Media;

namespace MercuryMapper.Config;

public class UserConfig
{
    public KeymapConfig KeymapConfig { get; set; } = new();
    public ColorConfig ColorConfig { get; set; } = new();
}

public class KeymapConfig
{
    /// <summary>
    /// Add new keybind:
    /// - Add a Keybind to the dictionary.
    ///   The dictionary key should be the name of the action it performs.
    ///   The constructor is (KeyCode, Ctrl, Shift, Alt)
    ///
    /// Make keybind do something:
    /// - Add an if statement to MainView.OnKeyDown() like this:
    ///   if (Keybind.Compare(keybind, KeybindManager.Keybinds[YourKeybind]))
    ///   {
    ///       // your logic here
    ///       e.Handled = true;
    ///       return;
    ///   }
    ///
    /// Make keybind User-Rebindable:
    /// - Add a new TreeViewItem to SettingsView_Keymap.axaml
    /// - The Name and Dictionary Key *MUST* be identical.
    /// - Set Tag of TreeViewItem in SettingsView_Keymap.SetTag() to:
    ///   Manager.Keybinds["YourKeyHere"].ToString();
    /// </summary>
    
    public Dictionary<string, Keybind> Keybinds { get; set; } = new()
    {
        ["FileNew"] = new(Key.N, true, false, false),
        ["FileOpen"] = new(Key.O, true, false, false),
        ["FileSave"] = new(Key.S, true, false, false),
        ["FileSaveAs"] = new(Key.S, true, true, false),
        ["FileSettings"] = new(Key.S, true, false, true),
        ["EditUndo"] = new(Key.Z, true, false, false),
        ["EditRedo"] = new(Key.Y, true, false, false),
        ["EditCut"] = new(Key.X, true, false, false),
        ["EditCopy"] = new(Key.C, true, false, false),
        ["EditPaste"] = new(Key.V, true, false, false),
        ["EditorInsert"] = new(Key.I),
        ["EditorPlay"] = new(Key.Space),
        ["EditorIncreasePlaybackSpeed"] = new(Key.Add),
        ["EditorDecreasePlaybackSpeed"] = new(Key.Subtract),
        ["EditorNoteTypeTouch"] = new(Key.D1),
        ["EditorNoteTypeSlideClockwise"] = new(Key.D2),
        ["EditorNoteTypeSlideCounterclockwise"] = new(Key.D3),
        ["EditorNoteTypeSnapForward"] = new(Key.D4),
        ["EditorNoteTypeSnapBackward"] = new(Key.D5),
        ["EditorNoteTypeChain"] = new(Key.D6),
        ["EditorNoteTypeHold"] = new(Key.D7),
        ["EditorNoteTypeMaskAdd"] = new(Key.D8),
        ["EditorNoteTypeMaskRemove"] = new(Key.D9),
        ["EditorNoteTypeEndOfChart"] = new(Key.D0)
    };
}

public class ColorConfig
{
    public Dictionary<string, string> Colors { get; set; } = new()
    {
        ["ColorNoteTouch"] = "FFFF00FF",
        ["ColorNoteChain"] = "FFCCBE2D",
        ["ColorNoteSlideClockwise"] = "FFFF8000",
        ["ColorNoteSlideCounterclockwise"] = "FF32CD32",
        ["ColorNoteSnapForward"] = "FFFF0000",
        ["ColorNoteSnapBackward"] = "FF00FFFF",
        ["ColorNoteHoldStart"] = "FF8C6400",
        ["ColorNoteHoldSegment"] = "FFDCB932",
        ["ColorNoteHoldEnd"] = "FFDCB932",
        ["ColorNoteHoldSurfaceFar"] = "BEDCA000",
        ["ColorNoteHoldSurfaceNear"] = "BEDCB932",
        ["ColorNoteMaskAdd"] = "FF000000",
        ["ColorNoteMaskRemove"] = "FF000000",
        ["ColorNoteEndOfChart"] = "FF000000",
        ["ColorHighlight"] = "FF000000",
        ["ColorSelection"] = "FF000000",
        ["ColorBonus"] = "FF000000",
        ["ColorRNote"] = "FF000000",
        ["ColorGimmickBpmChange"] = "FF000000",
        ["ColorGimmickTimeSigChange"] = "FF000000",
        ["ColorGimmickHiSpeedChange"] = "FF000000",
        ["ColorGimmickStop"] = "FF000000",
        ["ColorGimmickReverse"] = "FF000000"
    };
}