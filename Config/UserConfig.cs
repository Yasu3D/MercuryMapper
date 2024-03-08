using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Media;

namespace MercuryMapper.Config;

public class UserConfig
{
    public RenderConfig RenderConfig { get; set; } = new();
    public KeymapConfig KeymapConfig { get; set; } = new();
    public ColorConfig ColorConfig { get; set; } = new();
    public AudioConfig AudioConfig { get; set; } = new();
}

public class RenderConfig
{
    public int RefreshRate { get; set; } = 60;
    public int NoteSize { get; set; } = 3;
    public decimal NoteSpeed { get; set; } = 4.5m;
    public bool ShowHiSpeed { get; set; } = true;
    public int GuideLineType { get; set; } = 1;
    public int BeatDivision { get; set; } = 4;
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
        ["EditorNoteTypeEndOfChart"] = new(Key.D0),
        ["EditorTypeRadio1"] = new(Key.D1, false, true, false),
        ["EditorTypeRadio2"] = new(Key.D2, false, true, false),
        ["EditorTypeRadio3"] = new(Key.D3, false, true, false),
        ["EditorEditNoteShape"] = new(Key.E, false, true, false),
        ["EditorEditNoteProperties"] = new(Key.E, true, false, false),
        ["EditorEditNoteShapeProperties"] = new(Key.E, true, true, false),
        ["EditorDelete"] = new(Key.Delete),
        ["RenderIncreaseNoteSpeed"] = new(Key.Add, false, true, false),
        ["RenderDecreaseNoteSpeed"] = new(Key.Subtract, false, true, false)
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
        ["ColorNoteSnapBackward"] = "FF1D96F1",
        ["ColorNoteHoldStart"] = "FF8C6400",
        ["ColorNoteHoldSegment"] = "FFDCB932",
        ["ColorNoteHoldEnd"] = "FFEDAE2A",
        ["ColorNoteHoldSurfaceFar"] = "BEDCA000",
        ["ColorNoteHoldSurfaceNear"] = "BEDCB932",
        ["ColorNoteMaskAdd"] = "FF333333",
        ["ColorNoteMaskRemove"] = "FF808080",
        ["ColorNoteEndOfChart"] = "80000000",
        ["ColorHighlight"] = "FF000000",
        ["ColorSelection"] = "FF000000",
        ["ColorBonus"] = "FF000000",
        ["ColorRNote"] = "FF000000",
        ["ColorNoteCaps"] = "FF2CB5F5",
        ["ColorSync"] = "FF00FFFF",
        ["ColorGimmickBpmChange"] = "FF000000",
        ["ColorGimmickTimeSigChange"] = "FF000000",
        ["ColorGimmickHiSpeedChange"] = "FF000000",
        ["ColorGimmickStop"] = "FF000000",
        ["ColorGimmickReverse"] = "FF000000",
        ["ColorBackgroundNear"] = "FF1D1F32",
        ["ColorBackgroundFar"] = "FF141529",
        ["ColorBackgroundNoMask"] = "FF110A1C",
        ["ColorGuideLines"] = "FFD3EFFF",
        ["ColorJudgementLinePrimary"] = "FFFF009D",
        ["ColorJudgementLineSecondary"] = "FFAE00FF",
        ["ColorMeasureLine"] = "FFFFFFFF",
        ["ColorBeatLine"] = "80FFFFFF",
        ["ColorAngleTicks"] = "AAFFFFFF"
    };
}

public class AudioConfig
{
    public double MusicVolume { get; set; } = 50;
    public double HitsoundVolume { get; set; } = 20;
    public double TouchVolume { get; set; } = 80;
    public double GuideVolume { get; set; } = 30;
    public double SwipeVolume { get; set; } = 80;
    public double BonusVolume { get; set; } = 80;
    public double RNoteVolume { get; set; } = 80;

    public string TouchHitsoundPath { get; set; } = "";
    public string GuideHitsoundPath { get; set; } = "";
    public string SwipeHitsoundPath { get; set; } = "";
    public string BonusHitsoundPath { get; set; } = "";
    public string RNoteHitsoundPath { get; set; } = "";
}