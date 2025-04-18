using System.Collections.Generic;
using Avalonia.Input;

namespace MercuryMapper.Config;

public class UserConfig
{
    public RenderConfig RenderConfig { get; set; } = new();
    public KeymapConfig KeymapConfig { get; set; } = new();
    public ColorConfig ColorConfig { get; set; } = new();
    public AudioConfig AudioConfig { get; set; } = new();
    public EditorConfig EditorConfig { get; set; } = new();
}

public class RenderConfig
{
    public int RefreshRate { get; set; } = 60;
    public int HoldRenderMethod { get; set; } = 0;
    public int NoteSize { get; set; } = 3;
    public double NoteSpeed { get; set; } = 2.5;
    public int GuideLineType { get; set; } = 1;
    public int BeatDivision { get; set; } = 4;
    public bool ShowHiSpeed { get; set; } = true;
    public bool ShowMaskDuringPlayback { get; set; } = false;
    public bool ShowGimmickNotesDuringPlayback { get; set; } = false;
    public bool ShowOtherUsersDuringPlayback { get; set; } = true;
    public bool ShowChainStripes { get; set; } = true;
    public bool ShowJudgementWindowMarvelous { get; set; } = false;
    public bool ShowJudgementWindowGreat { get; set; } = false;
    public bool ShowJudgementWindowGood { get; set; } = false;
    public bool CutOverlappingJudgementWindows { get; set; } = false;
    public bool CutEarlyJudgementWindowOnHolds { get; set; } = false;
    public bool DrawNoRenderSegments { get; set; } = true;
    public bool HideNotesOnDifferentLayers { get; set; } = true;
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
    /// </summary>
    
    public Dictionary<string, Keybind> Keybinds { get; set; } = new()
    {
        // DEPRECATED KEYBINDS
        ["EditorEndHold"] = new(Key.Enter, false, false, false),
        ["EditorEditHold"] = new(Key.Enter, false, true, false),
        ["EditorBakeHold"] = new(Key.B, false, true, false),
        ["EditorBakeHoldNoRender"] = new(Key.B, true, true, false),
        ["EditorStitchHold"] = new(Key.H, true, false, false),
        ["EditorSplitHold"] = new(Key.H, false, false, true),
        ["EditorInsertHoldSegment"] = new(Key.H, false, true, false),
        ["EditorSelectHoldReferences"] = new(Key.R, false, true, false),
        // END DEPRECATED KEYBINDS
        
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
        ["EditorSelectAll"] = new(Key.A, true, false, false),
        ["EditorDeselectAll"] = new(Key.A, false, false, true),
        ["EditorCheckerDeselect"] = new(Key.A, false, true, true),
        ["EditorEndNoteCollection"] = new(Key.Enter, false, false, false),
        ["EditorEditNoteCollection"] = new(Key.Enter, false, true, false),
        ["EditorBakeNoteCollection"] = new(Key.B, false, true, false),
        ["EditorBakeNoteCollectionNoRender"] = new(Key.B, true, true, false),
        ["EditorStitchNoteCollection"] = new(Key.H, true, false, false),
        ["EditorSplitNoteCollection"] = new(Key.H, false, false, true),
        ["EditorDeleteSegments"] = new(Key.Delete, true, false, false),
        ["EditorInsertSegment"] = new(Key.H, false, true, false),
        ["EditorHighlightNextNote"] = new(Key.W, false, true, false),
        ["EditorHighlightPrevNote"] = new(Key.S, false, true, false),
        ["EditorHighlightNearestNote"] = new(Key.Q, false, true, false),
        ["EditorSelectHighlightedNote"] = new(Key.Space, false, true, false),
        ["EditorSelectNoteCollectionReferences"] = new(Key.R, false, true, false),
        ["EditorBoxSelect"] = new(Key.B, true, false, false),
        ["EditorConvertToInstantMask"] = new(Key.N, true, false, false),
        ["EditorFlipNoteDirection"] = new(Key.M, true, false, false),
        ["EditorPaintTraces"] = new(Key.P, false, true, false),
        ["EditorSetScrollLayer"] = new(Key.L, false, true, false),
        ["EditorIncreasePlaybackSpeed"] = new(Key.Add),
        ["EditorDecreasePlaybackSpeed"] = new(Key.Subtract),
        ["EditorAddComment"] = new(Key.K, true, false, false),
        ["EditorNoteTypeTouch"] = new(Key.D1),
        ["EditorNoteTypeSlideClockwise"] = new(Key.D2),
        ["EditorNoteTypeSlideCounterclockwise"] = new(Key.D3),
        ["EditorNoteTypeSnapForward"] = new(Key.D4),
        ["EditorNoteTypeSnapBackward"] = new(Key.D5),
        ["EditorNoteTypeChain"] = new(Key.D6),
        ["EditorNoteTypeHold"] = new(Key.D7),
        ["EditorNoteTypeMaskAdd"] = new(Key.D8),
        ["EditorNoteTypeMaskRemove"] = new(Key.D9),
        ["EditorNoteTypeTrace"] = new(Key.D0),
        ["EditorNoteTypeDamage"] = new(Key.Oem4),
        ["EditorTypeRadio1"] = new(Key.D1, false, true, false),
        ["EditorTypeRadio2"] = new(Key.D2, false, true, false),
        ["EditorTypeRadio3"] = new(Key.D3, false, true, false),
        ["EditorEditNoteShape"] = new(Key.E, false, true, false),
        ["EditorEditNoteProperties"] = new(Key.E, true, false, false),
        ["EditorEditNoteShapeProperties"] = new(Key.E, true, true, false),
        ["EditorMirrorNote"] = new(Key.M, false, true, false),
        ["EditorReverseNote"] = new(Key.R, true, false, false),
        ["EditorDelete"] = new(Key.Delete),
        ["EditorSetRenderTrue"] = new(Key.V),
        ["EditorSetRenderFalse"] = new(Key.V, false, false, true),
        ["EditorToggleLoop"] = new(Key.L, true, false, false),
        ["EditorSetLoopStart"] = new(Key.OemComma, true, false, false),
        ["EditorSetLoopEnd"] = new(Key.OemPeriod, true, false, false),
        ["EditorQuickEditIncreaseSize"] = new(Key.Up, false, true, false),
        ["EditorQuickEditDecreaseSize"] = new(Key.Down, false, true, false),
        ["EditorQuickEditIncreasePosition"] = new(Key.Right, false, true, false),
        ["EditorQuickEditDecreasePosition"] = new(Key.Left, false, true, false),
        ["EditorQuickEditIncreaseSizeIterative"] = new(Key.Up, true, true, false),
        ["EditorQuickEditDecreaseSizeIterative"] = new(Key.Down, true, true, false),
        ["EditorQuickEditIncreasePositionIterative"] = new(Key.Right, true, true, false),
        ["EditorQuickEditDecreasePositionIterative"] = new(Key.Left, true, true, false),
        ["EditorQuickEditIncreaseTimestamp"] = new(Key.Up, true, false, false),
        ["EditorQuickEditDecreaseTimestamp"] = new(Key.Down, true, false, false),
        ["EditorJumpMeasureUp"] = new(Key.Right),
        ["EditorJumpMeasureDown"] = new(Key.Left),
        ["EditorJumpBeatUp"] = new(Key.Up),
        ["EditorJumpBeatDown"] = new(Key.Down),
        ["RenderIncreaseNoteSpeed"] = new(Key.Add, false, true, false),
        ["RenderDecreaseNoteSpeed"] = new(Key.Subtract, false, true, false),
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
        ["ColorNoteSnapForward"] = "FFFF002D",
        ["ColorNoteSnapBackward"] = "FF1D96F1",
        ["ColorNoteHoldStart"] = "FF666666",
        ["ColorNoteHoldSegment"] = "FFE3E3E3",
        ["ColorNoteHoldSegmentNoRender"] = "FFB8B8B8",
        ["ColorNoteHoldEnd"] = "FFB8B8B8",
        ["ColorNoteHoldSurfaceFar"] = "BEB2B2B2",
        ["ColorNoteHoldSurfaceNear"] = "BEFFFFFF",
        ["ColorNoteTrace"] = "FFEEEEEE",
        ["ColorNoteTraceNoRender"] = "FFBBBBBB",
        ["ColorNoteDamage"] = "FF00009D",
        ["ColorNoteDamageSpark"] = "FF0025FA",
        ["ColorNoteDamageOutline"] = "FFCCCCE0",
        ["ColorNoteMaskAdd"] = "FF9B9CD1",
        ["ColorNoteMaskRemove"] = "FF373480",
        ["ColorNoteEndOfChart"] = "80000000",
        ["ColorHighlight"] = "80FF0000",
        ["ColorSelection"] = "8000FFFF",
        ["ColorBonus"] = "6500A1FF",
        ["ColorBonusFill"] = "70FFFFFF",
        ["ColorRNote"] = "DCFFFED9",
        ["ColorNoteCaps"] = "FF2CB5F5",
        ["ColorSync"] = "FF00FFFF",
        ["ColorTraceCenter"] = "40000000",
        ["ColorTraceWhite"] = "C0EEEEEE",
        ["ColorTraceBlack"] = "C0505050",
        ["ColorTraceRed"] = "C0FF0000",
        ["ColorTraceOrange"] = "C0FF9200",
        ["ColorTraceYellow"] = "E6FFD900",
        ["ColorTraceLime"] = "C06EFF00",
        ["ColorTraceGreen"] = "C000AA55",
        ["ColorTraceSky"] = "C000F7F7",
        ["ColorTraceBlue"] = "C00065F7",
        ["ColorTraceViolet"] = "C08200F7",
        ["ColorTracePink"] = "C0FF00FF",
        ["ColorGimmickBpmChange"] = "96FF0000",
        ["ColorGimmickTimeSigChange"] = "9613FF00",
        ["ColorGimmickHiSpeedChange"] = "960089FF",
        ["ColorGimmickStop"] = "96FF00FF",
        ["ColorGimmickReverse"] = "96FFFFFF",
        ["ColorBackgroundNear"] = "FF1D1F32",
        ["ColorBackgroundFar"] = "FF141529",
        ["ColorBackgroundNoMask"] = "FF110A1C",
        ["ColorGuideLines"] = "FFD3EFFF",
        ["ColorJudgementLinePrimary"] = "FFFF009D",
        ["ColorJudgementLineSecondary"] = "FFAE00FF",
        ["ColorMeasureLine"] = "FFFFFFFF",
        ["ColorBeatLine"] = "80FFFFFF",
        ["ColorAngleTicks"] = "AAFFFFFF",
        ["ColorJudgementMarvelous"] = "FFFF3679",
        ["ColorJudgementGreat"] = "FF8FFF1F",
        ["ColorJudgementGood"] = "FF50B0FF",
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
    public double MetronomeVolume { get; set; } = 80;

    public string TouchHitsoundPath { get; set; } = "";
    public string GuideHitsoundPath { get; set; } = "";
    public string SwipeHitsoundPath { get; set; } = "";
    public string BonusHitsoundPath { get; set; } = "";
    public string RNoteHitsoundPath { get; set; } = "";
    public string MetronomePath { get; set; } = "";
    public string MetronomeDownbeatPath { get; set; } = "";
    public string MetronomeUpbeatPath { get; set; } = "";

    public float HitsoundOffset { get; set; } = 25;

    public bool MuteHitsoundsOnPreview { get; set; } = false;
    public bool StartMetronome { get; set; } = true;
    public bool ConstantMetronome { get; set; } = false;
}

public class EditorConfig
{
    public bool QuantizeOnPause { get; set; }
    public bool HighlightPlacedNote { get; set; }
    public bool LimitToMercuryBonusTypes { get; set; } = true;
}