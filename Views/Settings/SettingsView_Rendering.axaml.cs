using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

    private void SetValuesFromConfig()
    {
        NumericRefreshRate.Value = mainView.UserConfig.RenderConfig.RefreshRate;
        NumericNoteSize.Value = mainView.UserConfig.RenderConfig.NoteSize;
        NumericNoteSpeed.Value = mainView.UserConfig.RenderConfig.NoteSpeed;
        CheckBoxShowHiSpeed.IsChecked = mainView.UserConfig.RenderConfig.ShowHiSpeed;
        ComboGuideLineType.SelectedIndex = mainView.UserConfig.RenderConfig.GuideLineType;
        NumericBeatDivision.Value = mainView.UserConfig.RenderConfig.BeatDivision;
    }
    
    private void RefreshRate_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        mainView.UserConfig.RenderConfig.RefreshRate = (int)(NumericRefreshRate.Value ?? 60);
    }
    
    private void NoteSize_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        mainView.UserConfig.RenderConfig.NoteSize = (int)(NumericNoteSize.Value ?? 3);
    }
    
    private void NoteSpeed_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        mainView.UserConfig.RenderConfig.NoteSpeed = NumericNoteSpeed.Value ?? 4.5m;
    }

    private void ShowHiSpeed_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        mainView.UserConfig.RenderConfig.ShowHiSpeed = CheckBoxShowHiSpeed.IsChecked ?? true;
    }

    private void GuideLineType_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ComboGuideLineType == null) return; // ??? why the fuck is there a NullReferenceException if this is not here.
        mainView.UserConfig.RenderConfig.GuideLineType = ComboGuideLineType.SelectedIndex;
    }

    private void BeatDivision_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        mainView.UserConfig.RenderConfig.BeatDivision = (int)(NumericBeatDivision.Value ?? 4);
    }
}