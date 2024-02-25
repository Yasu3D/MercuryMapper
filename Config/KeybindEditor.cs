using Avalonia.Input;

namespace MercuryMapper.Config;

public class KeybindEditor(UserConfig config)
{
    private readonly UserConfig userConfig = config;
    public bool RebindingActive { get; private set; }
    public Keybind? EditedKeybind { get; private set; }

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