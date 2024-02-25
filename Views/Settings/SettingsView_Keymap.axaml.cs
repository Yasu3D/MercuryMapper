using System;
using Avalonia.Controls;
using Avalonia.Input;
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
    }
    
    private void KeybindsTreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        Control item = (Control)KeybindsTreeView.SelectedItem!;
        if (item.Classes.Contains("HideKeybind")) return;
        
        string name = item.Name ?? "";
        item.Tag = "Waiting for input...";
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