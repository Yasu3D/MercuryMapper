using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Keymap : UserControl
{
    public SettingsView_Keymap()
    {
        InitializeComponent();
    }

    private void KeybindsTreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        
        Console.WriteLine("Double Click!");
    }
}