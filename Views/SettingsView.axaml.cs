using Avalonia.Controls;

namespace MercuryMapper.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void Tabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        string name = ((TreeViewItem)TabsTreeView.SelectedItem!).Name ?? "";

        SettingsViewColors.IsVisible = name == "TreeViewColors";
        SettingsViewGimmicks.IsVisible = name == "TreeViewGimmicks";
        SettingsViewRendering.IsVisible = name == "TreeViewRendering";
        SettingsViewKeymap.IsVisible = name == "TreeViewKeymap";
    }
}