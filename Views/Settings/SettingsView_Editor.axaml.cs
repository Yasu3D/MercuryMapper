using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MercuryMapper.Config;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Editor : UserControl
{
    public SettingsView_Editor(MainView mainView)
    {
        InitializeComponent();
        this.mainView = mainView;
        SetValues();
    }

    private readonly MainView mainView;
    private EditorConfig Config => mainView.UserConfig.EditorConfig;

    private void SetValues()
    {
        CheckBoxQuantizeOnPause.IsChecked = Config.QuantizeOnPause;
        CheckBoxHighlightPlacedNote.IsChecked = Config.HighlightPlacedNote;
    }
    
    private void QuantizeOnPause_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        Config.QuantizeOnPause = CheckBoxQuantizeOnPause.IsChecked ?? false;
    }

    private void CheckBoxHighlightPlacedNote_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        Config.HighlightPlacedNote = CheckBoxHighlightPlacedNote.IsChecked ?? false;
    }
}