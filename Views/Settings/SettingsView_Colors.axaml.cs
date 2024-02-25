using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MercuryMapper.Rendering;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Colors : UserControl
{
    public SettingsView_Colors(MainView mainView)
    {
        InitializeComponent();
        
        this.mainView = mainView;
        
        SetPalette();
        SetColors();
    }

    private readonly MainView mainView;
    private Dictionary<string, string> Colors => mainView.UserConfig.ColorConfig.Colors;
    
    private void SetPalette()
    {
        NoteColorPalette palette = new();
        ColorNoteTouch.Palette = palette;
        ColorNoteChain.Palette = palette;
        ColorNoteSlideClockwise.Palette = palette;
        ColorNoteSlideCounterclockwise.Palette = palette;
        ColorNoteSnapForward.Palette = palette;
        ColorNoteSnapBackward.Palette = palette;
        ColorNoteHoldStart.Palette = palette;
        ColorNoteHoldSegment.Palette = palette;
        ColorNoteHoldEnd.Palette = palette;
        ColorNoteHoldSurfaceFar.Palette = palette;
        ColorNoteHoldSurfaceNear.Palette = palette;
        ColorNoteMaskAdd.Palette = palette;
        ColorNoteMaskRemove.Palette = palette;
        ColorNoteEndOfChart.Palette = palette;
        ColorHighlight.Palette = palette;
        ColorSelection.Palette = palette;
        ColorBonus.Palette = palette;
        ColorRNote.Palette = palette;
        ColorGimmickBpmChange.Palette = palette;
        ColorGimmickTimeSigChange.Palette = palette;
        ColorGimmickHiSpeedChange.Palette = palette;
        ColorGimmickStop.Palette = palette;
        ColorGimmickReverse.Palette = palette;
    }

    private void SetColors()
    {
        ColorNoteTouch.Color = Color.Parse("#" + Colors["ColorNoteTouch"]);
        ColorNoteChain.Color = Color.Parse("#" + Colors["ColorNoteChain"]);
        ColorNoteSlideClockwise.Color = Color.Parse("#" + Colors["ColorNoteSlideClockwise"]);
        ColorNoteSlideCounterclockwise.Color = Color.Parse("#" + Colors["ColorNoteSlideCounterclockwise"]);
        ColorNoteSnapForward.Color = Color.Parse("#" + Colors["ColorNoteSnapForward"]);
        ColorNoteSnapBackward.Color = Color.Parse("#" + Colors["ColorNoteSnapBackward"]);
        ColorNoteHoldStart.Color = Color.Parse("#" + Colors["ColorNoteHoldStart"]);
        ColorNoteHoldSegment.Color = Color.Parse("#" + Colors["ColorNoteHoldSegment"]);
        ColorNoteHoldEnd.Color = Color.Parse("#" + Colors["ColorNoteHoldEnd"]);
        ColorNoteHoldSurfaceFar.Color = Color.Parse("#" + Colors["ColorNoteHoldSurfaceFar"]);
        ColorNoteHoldSurfaceNear.Color = Color.Parse("#" + Colors["ColorNoteHoldSurfaceNear"]);
        ColorNoteMaskAdd.Color = Color.Parse("#" + Colors["ColorNoteMaskAdd"]);
        ColorNoteMaskRemove.Color = Color.Parse("#" + Colors["ColorNoteMaskRemove"]);
        ColorNoteEndOfChart.Color = Color.Parse("#" + Colors["ColorNoteEndOfChart"]);
        ColorHighlight.Color = Color.Parse("#" + Colors["ColorHighlight"]);
        ColorSelection.Color = Color.Parse("#" + Colors["ColorSelection"]);
        ColorBonus.Color = Color.Parse("#" + Colors["ColorBonus"]);
        ColorRNote.Color = Color.Parse("#" + Colors["ColorRNote"]);
        ColorGimmickBpmChange.Color = Color.Parse("#" + Colors["ColorGimmickBpmChange"]);
        ColorGimmickTimeSigChange.Color = Color.Parse("#" + Colors["ColorGimmickTimeSigChange"]);
        ColorGimmickHiSpeedChange.Color = Color.Parse("#" + Colors["ColorGimmickHiSpeedChange"]);
        ColorGimmickStop.Color = Color.Parse("#" + Colors["ColorGimmickStop"]);
        ColorGimmickReverse.Color = Color.Parse("#" + Colors["ColorGimmickReverse"]);
    }
    
    private void OnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        if (sender == null) return;
        string name = ((Control)sender).Name ?? "";

        if (!Colors.ContainsKey(name)) return;
        Colors[name] = e.NewColor.ToUInt32().ToString("X8");
    }
}