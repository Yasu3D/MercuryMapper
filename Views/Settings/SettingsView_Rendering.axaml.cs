using Avalonia;
using Avalonia.Controls;
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

    private MainView mainView;

    private void SetValuesFromConfig()
    {
        NumericRefreshRate.Value = mainView.UserConfig.RenderConfig.RefreshRate;
    }
    
    private void RefreshRate_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        mainView.UserConfig.RenderConfig.RefreshRate = (int)(NumericRefreshRate.Value ?? 60);
    }
}