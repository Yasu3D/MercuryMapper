using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using MercuryMapper.Config;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Colors : UserControl
{
    public SettingsView_Colors(MainView mainView)
    {
        InitializeComponent();
        
        this.mainView = mainView;
        
        SetPalette();
        SetColors();
    }

    private readonly MainView mainView;
    private Dictionary<string, string> Colors => mainView.UserConfig.ColorConfig.Colors;
    
    private void SetPalette()
    {
        NoteColorPalette palette = new();
        foreach (ColorPicker picker in ColorPickers.GetVisualDescendants().OfType<ColorPicker>())
            picker.Palette = palette;
    }

    private void SetColors()
    {
        foreach (ColorPicker picker in ColorPickers.GetVisualDescendants().OfType<ColorPicker>())
        {
            picker.Color = Color.Parse("#" + Colors[picker.Name!]);
        }
    }
    
    private void OnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        if (sender == null) return;
        string name = ((Control)sender).Name ?? "";

        if (!Colors.ContainsKey(name)) return;
        Colors[name] = e.NewColor.ToUInt32().ToString("X8");
    }
}