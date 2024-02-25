using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MercuryMapper.Config;
using MercuryMapper.Editor;
using MercuryMapper.Enums;
using Tomlyn;

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
        ToggleTypeRadio(false);
    }

    public bool CanShutdown;
    public UserConfig UserConfig = new();
    public readonly KeybindEditor KeybindEditor;
    public readonly ChartEditor ChartEditor = new();
    
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
        RadioNoteSlideClockwise.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSlideClockwise"], 16));
        RadioNoteSlideCounterclockwise.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSlideCounterclockwise"], 16));
        RadioNoteSnapForward.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSnapForward"], 16));
        RadioNoteSnapBackward.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteSnapBackward"], 16));
        RadioNoteChain.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteChain"], 16));
        RadioNoteHold.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteHoldStart"], 16));
        RadioNoteMaskAdd.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteMaskAdd"], 16));
        RadioNoteMaskRemove.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteMaskRemove"], 16));
        RadioNoteEndOfChart.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteEndOfChart"], 16));
    }

    private void ToggleTypeRadio(bool isMask)
    {
        if (BonusTypePanel == null || MaskDirectionPanel == null) return;
        
        BonusTypePanel.IsVisible = !isMask;
        MaskDirectionPanel.IsVisible = isMask;
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
            MenuItemNew_OnClick(null, new());
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileOpen"])) 
        {
            MenuItemOpen_OnClick(null, new());
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileSave"])) 
        {
            MenuItemSave_OnClick(null, new());
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileSaveAs"])) 
        {
            MenuItemSaveAs_OnClick(null, new());
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["FileSettings"]))
        {
            MenuItemSettings_OnClick(null, new());
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorIncreasePlaybackSpeed"])) 
        {
            Console.WriteLine("Plus");
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDecreasePlaybackSpeed"])) 
        {
            Console.WriteLine("Minus");
            e.Handled = true;
            return;
        }
        
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeTouch"]))
        {
            RadioNoteTouch.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSlideClockwise"])) 
        {
            RadioNoteSlideClockwise.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSlideCounterclockwise"])) 
        {
            RadioNoteSlideCounterclockwise.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSnapForward"])) 
        {
            RadioNoteSnapForward.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeSnapBackward"])) 
        {
            RadioNoteSnapBackward.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeChain"])) 
        {
            RadioNoteChain.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeHold"])) 
        {
            RadioNoteHold.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeMaskAdd"])) 
        {
            RadioNoteMaskAdd.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeMaskRemove"])) 
        {
            RadioNoteMaskRemove.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeEndOfChart"])) 
        {
            RadioNoteEndOfChart.IsChecked = true;
            e.Handled = true;
            return;
        }
    }

    private static void OnKeyUp(object sender, KeyEventArgs e) => e.Handled = true;

    // ________________ UI Events

    private async void MenuItemNew_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        ChartEditor.NewChart();
    }

    private async void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        
        var file = await OpenChartFilePicker();
        if (file == null) return;
        
        ChartEditor.Chart.LoadFile(file.Path.LocalPath);
    }

    private async void MenuItemSave_OnClick(object? sender, RoutedEventArgs e) => await SaveFile(ChartEditor.IsNew);

    private async void MenuItemSaveAs_OnClick(object? sender, RoutedEventArgs e) => await SaveFile(true);

    private async void MenuItemExportMercury_OnClick(object? sender, RoutedEventArgs e) => await ExportFile(ChartWriteType.Mercury);

    private async void MenuItemExportSaturn_OnClick(object? sender, RoutedEventArgs e) => await ExportFile(ChartWriteType.Saturn);

    private void MenuItemSettings_OnClick(object? sender, RoutedEventArgs e)
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
    }

    public async void MenuItemExit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        CanShutdown = true;
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void MenuItemUndo_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemRedo_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemCut_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemCopy_OnClick(object? sender, RoutedEventArgs e) { }

    private void MenuItemPaste_OnClick(object? sender, RoutedEventArgs e) { }

    private void RadioNoteType_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (RadioNoteMaskAdd == null || RadioNoteMaskRemove == null || RadioNoteMaskAdd.IsChecked == null || RadioNoteMaskRemove.IsChecked == null) return;
        
        bool isMask = (bool)RadioNoteMaskAdd.IsChecked || (bool)RadioNoteMaskRemove.IsChecked;
        ToggleTypeRadio(isMask);
    }

    private void RadioBonusType_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void MaskDirection_IsCheckedChanged(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickBpmChange_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickTimeSig_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickHiSpeed_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickStop_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonGimmickReverse_OnClick(object? sender, RoutedEventArgs e) { }

    private void ButtonInsert_OnClick(object? sender, RoutedEventArgs e) { }

    // ________________ UI Dialogs

    private void OnSettingsClose(ContentDialog sender, ContentDialogClosingEventArgs e)
    {
        // Settings closed, stop rebinding in case it was active.
        KeybindEditor.StopRebinding();
        SetButtonColors();
        File.WriteAllText("UserConfig.toml", Toml.FromModel(UserConfig));
    }
    
    private IStorageProvider GetStorageProvider()
    {
        if (VisualRoot is TopLevel)
            return (VisualRoot as TopLevel)!.StorageProvider;
        throw new Exception(":3 something went wrong, too bad.");
    }
    
    private async Task<bool> PromptSave()
    {
        if (ChartEditor.Chart.IsSaved) return false;

        ContentDialogResult result = await showSavePrompt();

        switch (result)
        {
            case ContentDialogResult.None: return true;
            case ContentDialogResult.Primary when await SaveFile(true): return true;
            case ContentDialogResult.Secondary: return false;
            default: return false;
        }

        Task<ContentDialogResult> showSavePrompt()
        {
            ContentDialog dialog = new()
            {
                Title = "Chart is unsaved. Would you like to save?",
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                SecondaryButtonText = Assets.Lang.Resources.Generic_No,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel
            };
            
            return dialog.ShowAsync();
        }
    }

    private async Task<IStorageFile?> OpenChartFilePicker()
    {
        var result = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Mercury Chart files")
                {
                    Patterns = new[] {"*.mer"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
        });

        return result.Count != 1 ? null : result[0];
    }
    
    private async Task<IStorageFile?> SaveChartFilePicker()
    {
        return await GetStorageProvider().SaveFilePickerAsync(new FilePickerSaveOptions()
        {
            DefaultExtension = "mer",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Mercury Chart files")
                {
                    Patterns = new[] {"*.mer"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
        });
        
        
    }
    
    public async Task<bool> SaveFile(bool openFilePicker)
    {
        string filepath = "";
        
        if (openFilePicker)
        {
            IStorageFile? file = await SaveChartFilePicker();

            if (file == null) return false;
            filepath = file.Path.LocalPath;
        }

        if (string.IsNullOrEmpty(filepath)) return false;

        ChartEditor.Chart.WriteFile(filepath, ChartWriteType.Editor, true);
        ChartEditor.IsNew = false;
        return true;
    }

    public async Task ExportFile(ChartWriteType chartWriteType)
    {
        string filepath = "";
        IStorageFile? file = await SaveChartFilePicker();

        if (file == null) return;
        filepath = file.Path.LocalPath;
        
        if (string.IsNullOrEmpty(filepath)) return;
        ChartEditor.Chart.WriteFile(filepath, chartWriteType, false);
    }
}