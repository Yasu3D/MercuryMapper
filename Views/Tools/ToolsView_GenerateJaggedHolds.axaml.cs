using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Tools;

public partial class ToolsView_GenerateJaggedHolds : UserControl
{
    public ToolsView_GenerateJaggedHolds()
    {
        InitializeComponent();
    }

    private void GeneratorMethod_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GeneratorMethod is null) return;

        switch (GeneratorMethod.SelectedIndex)
        {
            case 0:
            {
                SpikesData.IsVisible = true;
                NoiseData.IsVisible = false;
                break;
            }

            case 1:
            {
                SpikesData.IsVisible = false;
                NoiseData.IsVisible = true;
                break;
            }
        }
    }
}