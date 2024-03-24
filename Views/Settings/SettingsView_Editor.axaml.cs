using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

    private void SetValues()
    {
        CheckBoxQuantizeOnPause.IsChecked = mainView.UserConfig.EditorConfig.QuantizeOnPause;
    }
    
    private void QuantizeOnPause_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        mainView.UserConfig.EditorConfig.QuantizeOnPause = CheckBoxQuantizeOnPause.IsChecked ?? false;
    }
}