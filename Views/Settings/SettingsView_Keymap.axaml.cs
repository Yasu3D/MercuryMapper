using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MercuryMapper.Config;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Keymap : UserControl
{
    public SettingsView_Keymap(MainView mainView)
    {
        InitializeComponent();
        
        this.mainView = mainView;
        SetKeybindTags();
    }

    private readonly MainView mainView;
    private KeymapConfig Keymap => mainView.UserConfig.KeymapConfig;

    private void SetKeybindTags()
    {
        // This doesn't work because Avalonia is stupid.
        //foreach (TreeViewItem item in KeybindsTreeView.GetVisualDescendants().OfType<TreeViewItem>())
        //    item.Tag = Keymap.Keybinds[item.Name!].ToString();
        
        FileNew.Tag = Keymap.Keybinds["FileNew"].ToString();
        FileOpen.Tag = Keymap.Keybinds["FileOpen"].ToString();
        FileSave.Tag = Keymap.Keybinds["FileSave"].ToString();
        FileSaveAs.Tag = Keymap.Keybinds["FileSaveAs"].ToString();
        FileSettings.Tag = Keymap.Keybinds["FileSettings"].ToString();
        EditUndo.Tag = Keymap.Keybinds["EditUndo"].ToString();
        EditRedo.Tag = Keymap.Keybinds["EditRedo"].ToString();
        EditCut.Tag = Keymap.Keybinds["EditCut"].ToString();
        EditCopy.Tag = Keymap.Keybinds["EditCopy"].ToString();
        EditPaste.Tag = Keymap.Keybinds["EditPaste"].ToString();
        EditorInsert.Tag = Keymap.Keybinds["EditorInsert"].ToString();
        EditorPlay.Tag = Keymap.Keybinds["EditorPlay"].ToString();
        EditorIncreasePlaybackSpeed.Tag = Keymap.Keybinds["EditorIncreasePlaybackSpeed"].ToString();
        EditorDecreasePlaybackSpeed.Tag = Keymap.Keybinds["EditorDecreasePlaybackSpeed"].ToString();
        EditorNoteTypeTouch.Tag = Keymap.Keybinds["EditorNoteTypeTouch"].ToString();
        EditorNoteTypeSlideClockwise.Tag = Keymap.Keybinds["EditorNoteTypeSlideClockwise"].ToString();
        EditorNoteTypeSlideCounterclockwise.Tag = Keymap.Keybinds["EditorNoteTypeSlideCounterclockwise"].ToString();
        EditorNoteTypeSnapForward.Tag = Keymap.Keybinds["EditorNoteTypeSnapForward"].ToString();
        EditorNoteTypeSnapBackward.Tag = Keymap.Keybinds["EditorNoteTypeSnapBackward"].ToString();
        EditorNoteTypeChain.Tag = Keymap.Keybinds["EditorNoteTypeChain"].ToString();
        EditorNoteTypeHold.Tag = Keymap.Keybinds["EditorNoteTypeHold"].ToString();
        EditorNoteTypeMaskAdd.Tag = Keymap.Keybinds["EditorNoteTypeMaskAdd"].ToString();
        EditorNoteTypeMaskRemove.Tag = Keymap.Keybinds["EditorNoteTypeMaskRemove"].ToString();
        EditorNoteTypeEndOfChart.Tag = Keymap.Keybinds["EditorNoteTypeEndOfChart"].ToString();
        EditorTypeRadio1.Tag = Keymap.Keybinds["EditorTypeRadio1"].ToString();
        EditorTypeRadio2.Tag = Keymap.Keybinds["EditorTypeRadio2"].ToString();
        EditorTypeRadio3.Tag = Keymap.Keybinds["EditorTypeRadio3"].ToString();
        EditorEditNoteShape.Tag = Keymap.Keybinds["EditorEditNoteShape"].ToString();
        EditorEditNoteProperties.Tag = Keymap.Keybinds["EditorEditNoteProperties"].ToString();
        EditorEditNoteShapeProperties.Tag = Keymap.Keybinds["EditorEditNoteShapeProperties"].ToString();
        EditorDelete.Tag = Keymap.Keybinds["EditorDelete"].ToString();
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
        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.Escape)
            return;
        
        mainView.KeybindEditor.OnKeyDown(e);
        SetKeybindTags();
    }
}