using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using MercuryMapper.Config;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Keymap : UserControl
{
    public SettingsView_Keymap(MainView mainView)
    {
        InitializeComponent();
        
        this.mainView = mainView;
        SetKeybindTags();
        mainView.KeybindEditor.CurrentSettingsView = this; // a little bit spaghetti but does the trick.
    }

    private readonly MainView mainView;
    private KeymapConfig Keymap => mainView.UserConfig.KeymapConfig;

    public void SetKeybindTags()
    {
        foreach (TreeViewItem item in KeybindsTreeView.GetLogicalDescendants().OfType<TreeViewItem>())
        {
            if (!Keymap.Keybinds.ContainsKey(item.Name ?? "")) continue;
            item.Tag = Keymap.Keybinds[item.Name ?? ""].ToString();
        }
    }
    
    private void KeybindsTreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        Control? item = (Control?)KeybindsTreeView.SelectedItem;
        if (item == null || item.Classes.Contains("HideKeybind")) return;
        
        string name = item.Name ?? "";
        item.Tag = Assets.Lang.Resources.Settings_Keymap_WaitingForInput;
        mainView.KeybindEditor.StartRebinding(name);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        mainView.KeybindEditor.OnKeyDown(e);
    }
}