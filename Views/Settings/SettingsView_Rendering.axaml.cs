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
        CheckBoxShowOtherUsersDuringPlayback.IsChecked = RenderConfig.ShowOtherUsersDuringPlayback;
        CheckBoxShowChainStripes.IsChecked = RenderConfig.ShowChainStripes;
        CheckBoxShowJudgementWindowMarvelous.IsChecked = RenderConfig.ShowJudgementWindowMarvelous;
        CheckBoxShowJudgementWindowGreat.IsChecked = RenderConfig.ShowJudgementWindowGreat;
        CheckBoxShowJudgementWindowGood.IsChecked = RenderConfig.ShowJudgementWindowGood;
        CheckBoxCutEarlyJudgementWindowOnHolds.IsChecked = RenderConfig.CutEarlyJudgementWindowOnHolds;
        CheckBoxCutOverlappingJudgementWindows.IsChecked = RenderConfig.CutOverlappingJudgementWindows;
        CheckBoxDrawNoRenderHoldSegments.IsChecked = RenderConfig.DrawNoRenderSegments;
        CheckBoxHideNotesOnDifferentLayers.IsChecked = RenderConfig.HideNotesOnDifferentLayers;
    }
    
    private void RefreshRate_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RenderConfig.RefreshRate = (int)(NumericRefreshRate.Value ?? 60);
    }
    
    private void NoteSize_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        RenderConfig.NoteSize = (int)(NumericNoteSize.Value ?? 3);
        mainView.RenderEngine.UpdateBrushes();
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
    
    private void CheckBoxShowOtherUsersDuringPlayback_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowOtherUsersDuringPlayback = CheckBoxShowOtherUsersDuringPlayback.IsChecked ?? false;
    }

    private void ComboHoldRenderMethod_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ComboHoldRenderMethod == null) return;
        RenderConfig.HoldRenderMethod = ComboHoldRenderMethod.SelectedIndex;
    }

    private void CheckBoxShowChainStripes_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowChainStripes = CheckBoxShowChainStripes.IsChecked ?? true;
    }
    
    private void CheckBoxShowJudgementWindowMarvelous_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowJudgementWindowMarvelous = CheckBoxShowJudgementWindowMarvelous.IsChecked ?? true;
    }
    
    private void CheckBoxShowJudgementWindowGreat_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowJudgementWindowGreat = CheckBoxShowJudgementWindowGreat.IsChecked ?? true;
    }
    
    private void CheckBoxShowJudgementWindowGood_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.ShowJudgementWindowGood = CheckBoxShowJudgementWindowGood.IsChecked ?? true;
    }
    
    private void CheckBoxCutEarlyJudgementWindowOnHolds_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.CutEarlyJudgementWindowOnHolds = CheckBoxCutEarlyJudgementWindowOnHolds.IsChecked ?? true;
    }
    
    private void CheckBoxCutOverlappingJudgementWindows_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.CutOverlappingJudgementWindows = CheckBoxCutOverlappingJudgementWindows.IsChecked ?? true;
    }
    
    private void CheckBoxDrawNoRenderHoldSegments_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.DrawNoRenderSegments = CheckBoxDrawNoRenderHoldSegments.IsChecked ?? true;
    }
    
    private void CheckBoxHideNotesOnDifferentLayers_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        RenderConfig.HideNotesOnDifferentLayers = CheckBoxHideNotesOnDifferentLayers.IsChecked ?? true;
    }
}