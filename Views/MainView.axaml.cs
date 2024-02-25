using System;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Tomlyn;
using MercuryMapper.Config;

namespace MercuryMapper.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        
        LoadUserConfig();

        KeybindEditor = new(UserConfig);
        KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        
        SetButtonColors();
    }
    
    public UserConfig UserConfig = new();
    public KeybindEditor KeybindEditor;
    
    // ________________ Setup

    private void LoadUserConfig()
    {
        if (!File.Exists("UserConfig.toml"))
        {
            File.WriteAllText("UserConfig.toml", Toml.FromModel(UserConfig));
            return;
        }
        
        try
        {
            UserConfig = Toml.ToModel<UserConfig>(File.ReadAllText("UserConfig.Toml"));
        }
        catch (Exception e)
        {
            // ignored for now
        }
    }

    private void SetButtonColors()
    {
        RadioNoteTouch.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteTouch"], 16));
        RadioNoteSlideClockwise.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteChain"], 16));
        RadioNoteSlideCounterclockwise.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSlideClockwise"], 16));
        RadioNoteSnapForward.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSlideCounterclockwise"], 16));
        RadioNoteSnapBackward.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSnapForward"], 16));
        RadioNoteChain.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSnapBackward"], 16));
        RadioNoteHold.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteHoldStart"], 16));
        RadioNoteMaskAdd.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteHoldSegment"], 16));
        RadioNoteMaskRemove.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteHoldEnd"], 16));
        RadioNoteEndOfChart.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteHoldSurfaceFar"], 16));
    }
    
    // ________________ Input
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (KeybindEditor.RebindingActive) return;
        
        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            return;
        }
        
        Keybind keybind = new(e);
        
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileNew"])) 
        {
            Console.WriteLine("KeybindFileNew");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileOpen"])) 
        {
            Console.WriteLine("KeybindFileOpen");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileSave"])) 
        {
            Console.WriteLine("KeybindFileSave");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileSaveAs"])) 
        {
            Console.WriteLine("KeybindFileSaveAs");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileSettings"])) 
        {
            Console.WriteLine("OpenSettings");
            e.Handled = true;
            return;
        }

        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditUndo"])) 
        {
            Console.WriteLine("Undo");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditRedo"])) 
        {
            Console.WriteLine("Redo");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditCut"])) 
        {
            Console.WriteLine("Cut");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditCopy"])) 
        {
            Console.WriteLine("Copy");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditPaste"])) 
        {
            Console.WriteLine("Paste");
            e.Handled = true;
            return;
        }
        
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorInsert"])) 
        {
            Console.WriteLine("InsertNote");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorPlay"])) 
        {
            Console.WriteLine("Play");
            e.Handled = true;
            return;
        }
        
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeTouch"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSlideClockwise"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSlideCounterclockwise"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSnapForward"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSnapBackward"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeChain"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeHold"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeMaskAdd"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeMaskRemove"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeEndOfChart"])) 
        {
            Console.WriteLine("Switched NoteType");
            e.Handled = true;
            return;
        }
    }

    private static void OnKeyUp(object sender, KeyEventArgs e) => e.Handled = true;

    // ________________ UI Events
    
    private void MenuItemNew_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemSave_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemSaveAs_OnClick(object? sender, RoutedEventArgs e) { }
    
    private void MenuItemExportMercury_OnClick(object? sender, RoutedEventArgs e) { }
    
    private void MenuItemExportSaturn_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenSettings();
    }
    
    private void MenuItemExit_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemUndo_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemRedo_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemCut_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemCopy_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemPaste_OnClick(object? sender, RoutedEventArgs e) { }

    private void RadioNoteType_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void RadioBonusType_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void MaskDirection_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickBpmChange_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickTimeSig_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickHiSpeed_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickStop_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickReverse_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonInsert_OnClick(object? sender, RoutedEventArgs e) { }

    // ________________ UI Dialogs
    
    private void OpenSettings()
    {
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Menu_Settings,
            Content = new SettingsView(this),
            IsPrimaryButtonEnabled = false,
            CloseButtonText = Assets.Lang.Resources.Generic_SaveAndClose,
        };

        dialog.Closing += OnSettingsClose;
        
        Dispatcher.UIThread.Post(async () => await dialog.ShowAsync());
        return;
    }

    private void OnSettingsClose(ContentDialog sender, ContentDialogClosingEventArgs e)
    {
        // Settings closed, stop rebinding in case it was active.
        KeybindEditor.StopRebinding();
        SetButtonColors();
        File.WriteAllText("UserConfig.toml", Toml.FromModel(UserConfig));
    }
}