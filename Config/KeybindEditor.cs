using System;
using Avalonia.Input;
using MercuryMapper.Views.Settings;

namespace MercuryMapper.Config;

public class KeybindEditor(UserConfig config)
{
    private readonly UserConfig userConfig = config;
    public bool RebindingActive { get; private set; }
    public Keybind? EditedKeybind { get; private set; }
    public SettingsView_Keymap? CurrentSettingsView { get; set; }

    public void StartRebinding(string name)
    {
        if (!userConfig.KeymapConfig.Keybinds.TryGetValue(name, out Keybind? value)) return;
        
        RebindingActive = true;
        EditedKeybind = value;
    }

    public void StopRebinding()
    {
        RebindingActive = false;
        EditedKeybind = null;
        CurrentSettingsView?.SetKeybindTags();
    }

    public void OnKeyDown(KeyEventArgs e)
    {
        if (!RebindingActive || EditedKeybind == null)
            return;
        
        EditedKeybind.Key = e.Key;
        EditedKeybind.Control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        EditedKeybind.Shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        EditedKeybind.Alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        StopRebinding(); 
    }
}