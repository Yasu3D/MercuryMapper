using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MercuryMapper.Rendering;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Colors : UserControl
{
    public SettingsView_Colors()
    {
        InitializeComponent();
        SetPalette();
    }

    private void SetPalette()
    {
        NoteColorPalette palette = new();
        ColorPickerNoteTouch.Palette = palette;
        ColorPickerNoteChain.Palette = palette;
        ColorPickerNoteSlideClockwise.Palette = palette;
        ColorPickerNoteSlideCounterclockwise.Palette = palette;
        ColorPickerNoteSnapForward.Palette = palette;
        ColorPickerNoteSnapBackward.Palette = palette;
        ColorPickerNoteHoldStart.Palette = palette;
        ColorPickerNoteHoldSegment.Palette = palette;
        ColorPickerNoteHoldEnd.Palette = palette;
        ColorPickerNoteHoldSurfaceFar.Palette = palette;
        ColorPickerNoteHoldSurfaceNear.Palette = palette;
        ColorPickerNoteMaskAdd.Palette = palette;
        ColorPickerNoteMaskRemove.Palette = palette;
        ColorPickerNoteEndOfChart.Palette = palette;
        ColorPickerHighlight.Palette = palette;
        ColorPickerSelection.Palette = palette;
        ColorPickerBonus.Palette = palette;
        ColorPickerRNote.Palette = palette;
        ColorPickerGimmickBpmChange.Palette = palette;
        ColorPickerGimmickTimeSigChange.Palette = palette;
        ColorPickerGimmickHiSpeedChange.Palette = palette;
        ColorPickerGimmickStop.Palette = palette;
        ColorPickerGimmickReverse.Palette = palette;
    }
}