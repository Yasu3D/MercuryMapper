using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MercuryMapper.Config;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Rendering : UserControl
{
    public SettingsView_Rendering(MainView mainView)
    {
        InitializeComponent();
        this.mainView = mainView;
        SetValuesFromConfig();
    }

    private readonly MainView mainView;
    private RenderConfig RenderConfig => mainView.UserConfig.RenderConfig;

    private void SetValuesFromConfig()
    {
        NumericRefreshRate.Value = RenderConfig.RefreshRate;
        ComboHoldRenderMethod.SelectedIndex = RenderConfig.HoldRenderMethod;
        NumericNoteSize.Value = RenderConfig.NoteSize;
        NumericNoteSpeed.Value = (decimal?)RenderConfig.NoteSpeed;
        CheckBoxShowHiSpeed.IsChecked = RenderConfig.ShowHiSpeed;
        ComboGuideLineType.SelectedIndex = RenderConfig.GuideLineType;
        NumericBeatDivision.Value = RenderConfig.BeatDivision;
        CheckBoxShowMaskDuringPlayback.IsChecked = RenderConfig.ShowMaskDuringPlayback;
        CheckBoxShowGimmickNotesDuringPlayback.IsChecked = RenderConfig.ShowGimmickNotesDuringPlayback;
    }
    
    private void RefreshRate_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RenderConfig.RefreshRate = (int)(NumericRefreshRate.Value ?? 60);
    }
    
    private void NoteSize_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RenderConfig.NoteSize = (int)(NumericNoteSize.Value ?? 3);
    }
    
    private void GuideLineType_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ComboGuideLineType == null) return; // ??? why the fuck is there a NullReferenceException if this is not here.
        RenderConfig.GuideLineType = ComboGuideLineType.SelectedIndex;
    }

    private void BeatDivision_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RenderConfig.BeatDivision = (int)(NumericBeatDivision.Value ?? 4);
    }
    
    private void NoteSpeed_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RenderConfig.NoteSpeed = (double?)NumericNoteSpeed.Value ?? 4.5;
    }

    private void ShowHiSpeed_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowHiSpeed = CheckBoxShowHiSpeed.IsChecked ?? true;
    }
    
    private void CheckBoxShowMaskDuringPlayback_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowMaskDuringPlayback = CheckBoxShowMaskDuringPlayback.IsChecked ?? false;
    }
    
    private void CheckBoxShowGimmickNotesDuringPlayback_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowGimmickNotesDuringPlayback = CheckBoxShowGimmickNotesDuringPlayback.IsChecked ?? false;
    }

    private void ComboHoldRenderMethod_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ComboHoldRenderMethod == null) return;
        RenderConfig.HoldRenderMethod = ComboHoldRenderMethod.SelectedIndex;
    }
}