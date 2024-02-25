using System;
using Avalonia.Controls;
using MercuryMapper.Views.Settings;

namespace MercuryMapper.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(MainView mainView)
    {
        InitializeComponent();
        
        this.mainView = mainView;
    }

    private MainView mainView;

    private void Tabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        string name = ((TreeViewItem)TabsTreeView.SelectedItem!).Name ?? "";

        ViewContainer.Content = name switch
        {
            "TreeViewColors" => new SettingsView_Colors(mainView),
            "TreeViewGimmicks" => new SettingsView_Gimmicks(),
            "TreeViewRendering" => new SettingsView_Rendering(),
            "TreeViewKeymap" => new SettingsView_Keymap(mainView),
            _ => null
        };
        
        // Switched tabs, stop rebinding in case it was active.
        mainView.KeybindEditor.StopRebinding();
    }
}