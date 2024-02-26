using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MercuryMapper.Audio;
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
        ChartEditor = new(this);
        KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        
        SetButtonColors();
        SetMenuItemInputGestureText();
        ToggleTypeRadio(false);
    }

    public bool CanShutdown;
    
    public UserConfig UserConfig = new();
    public readonly KeybindEditor KeybindEditor;
    public readonly ChartEditor ChartEditor;
    public readonly BassSoundEngine SoundEngine = new();

    private BassSound? currentSong;
    
    public bool IsPlaying = false;
    public int PlaybackSpeed = 100;
    
    // ________________ Setup & UI Updates

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

    private void SetMenuItemInputGestureText()
    {
        MenuItemNew.InputGesture = UserConfig.KeymapConfig.Keybinds["FileNew"].ToGesture();
        MenuItemOpen.InputGesture = UserConfig.KeymapConfig.Keybinds["FileOpen"].ToGesture();
        MenuItemSave.InputGesture = UserConfig.KeymapConfig.Keybinds["FileSave"].ToGesture();
        MenuItemSaveAs.InputGesture = UserConfig.KeymapConfig.Keybinds["FileSaveAs"].ToGesture();
        MenuItemSettings.InputGesture = UserConfig.KeymapConfig.Keybinds["FileSettings"].ToGesture();
        MenuItemUndo.InputGesture = UserConfig.KeymapConfig.Keybinds["EditUndo"].ToGesture();
        MenuItemRedo.InputGesture = UserConfig.KeymapConfig.Keybinds["EditRedo"].ToGesture();
        MenuItemCut.InputGesture = UserConfig.KeymapConfig.Keybinds["EditCut"].ToGesture();
        MenuItemCopy.InputGesture = UserConfig.KeymapConfig.Keybinds["EditCopy"].ToGesture();
        MenuItemPaste.InputGesture = UserConfig.KeymapConfig.Keybinds["EditPaste"].ToGesture();
    }

    public void SetHoldContextButton(ChartEditorState state)
    {
        ButtonHoldContext.Content = state switch
        {
            ChartEditorState.InsertHold => Assets.Lang.Resources.Editor_EndHold,
            _ => Assets.Lang.Resources.Editor_EditHold
        };
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
            ButtonPlay_OnClick(null, new());
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorIncreasePlaybackSpeed"]))
        {
            SliderPlaybackSpeed.Value += 10;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDecreasePlaybackSpeed"])) 
        {
            SliderPlaybackSpeed.Value -= 10;
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

    // ________________ Canvas Input
    
    private void Canvas_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        int direction = double.Sign(e.Delta.Y);
        
        // Unused at the moment - Reserved for cursor depth
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) { }
        
        // Shift Beat Divisor
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            decimal value = NumericBeatDivisor.Value ?? 16;
            NumericBeatDivisor.Value = Math.Clamp(value + direction, NumericBeatDivisor.Minimum, NumericBeatDivisor.Maximum);
        }
        
        // Double/Halve Beat Divisor
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            decimal value = NumericBeatDivisor.Value ?? 16;
            
            if (direction > 0)
            {
                NumericBeatDivisor.Value = Math.Min(value * 2, 1920);
            }
            else
            {
                switch ((int)value)
                {
                    case 1: return;
                    case 2: NumericBeatDivisor.Value = 1; return;
                    default: NumericBeatDivisor.Value = Math.Ceiling(value * 0.5m); break;
                }
            }
        }

        // Shift Time
        else
        {
            // I know some gremlin would try this.
            if (NumericMeasure.Value >= NumericMeasure.Maximum && NumericBeatValue.Value >= NumericBeatDivisor.Value - 1 && direction > 0) return;
            
            NumericBeatValue.Value += direction;
        }
    }
    
    private void Canvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        
    }

    private void Canvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        
    }

    private void Canvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        
    }
    
    // ________________ UI Events

    private async void MenuItemNew_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;

        NewChartView newChartView = new(this);
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Editor_NewChart,
            Content = newChartView,
            PrimaryButtonText = Assets.Lang.Resources.Menu_Save,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();

            if (result is ContentDialogResult.Primary)
            {
                string filepath = newChartView.MusicFilePath;
                string author = newChartView.AuthorTextBox.Text ?? "";
                float bpm = (float)newChartView.BpmNumberBox.Value;
                int timeSigUpper = (int)newChartView.TimeSigUpperNumberBox.Value;
                int timeSigLower = (int)newChartView.TimeSigLowerNumberBox.Value;

                if (bpm <= 0)
                {
                    ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidBpm);
                    return;
                }

                if (timeSigUpper <= 0 || timeSigLower <= 0)
                {
                    ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidTimeSig);
                    return;
                }

                if (!File.Exists(filepath))
                {
                    ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidAudio);
                    return;
                }
                
                ChartEditor.NewChart(filepath, author, bpm, timeSigUpper, timeSigLower);
                currentSong = SoundEngine.Play2d(filepath, false, true);
            }
        });
    }

    private async void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        
        IStorageFile? file = await OpenChartFilePicker();
        if (file == null) return;
        
        ChartEditor.Chart.LoadFile(file.Path.LocalPath);
    }

    private async void MenuItemSave_OnClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile(ChartEditor.Chart.IsNew);
    }

    private async void MenuItemSaveAs_OnClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile(true);
    }

    private async void MenuItemExportMercury_OnClick(object? sender, RoutedEventArgs e)
    {
        await ExportFile(ChartWriteType.Mercury);
    }

    private async void MenuItemExportSaturn_OnClick(object? sender, RoutedEventArgs e)
    {
        await ExportFile(ChartWriteType.Saturn);
    }

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
    
    private void ButtonHoldContext_OnClick(object? sender, RoutedEventArgs e) { }
    
    private void ButtonPlay_OnClick(object? sender, RoutedEventArgs e)
    {
        if (currentSong == null) return;
        
        IsPlaying = !IsPlaying;
        currentSong.Volume = 0.2f;
        currentSong.IsPlaying = IsPlaying;
        
        IconPlay.IsVisible = !IsPlaying;
        IconStop.IsVisible = IsPlaying;
    }
    
    private void SliderSongPosition_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        
    }
    
    private void SliderPlaybackSpeed_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        TextBlockPlaybackSpeed.Text = $"{SliderPlaybackSpeed.Value,3:F0}%";
        PlaybackSpeed = (int)SliderPlaybackSpeed.Value;
    }

    private void SliderPosition_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => Position_OnValueChanged(true);
    private void NumericPosition_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => Position_OnValueChanged(false);
    private void Position_OnValueChanged(bool fromSlider)
    {
        if (fromSlider) NumericNotePosition.Value = (decimal?)SliderNotePosition.Value;
        else
        {
            NumericNotePosition.Value ??= 0;
            if (NumericNotePosition.Value > 59) NumericNotePosition.Value = 0;
            if (NumericNotePosition.Value < 0) NumericNotePosition.Value = 59;

            SliderNotePosition.Value = (double)NumericNotePosition.Value;
        }
    }
    
    private void SliderNotePosition_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        int value = double.Sign(e.Delta.Y);

        switch (SliderNotePosition.Value + value)
        {
            case > 59: SliderNotePosition.Value = 0; break;
            case < 0: SliderNotePosition.Value = 59; break;
            default: SliderNotePosition.Value += value; break;
        }
    }
    
    private void SliderSize_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => Size_OnValueChanged(true);
    private void NumericSize_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => Size_OnValueChanged(false);
    private void Size_OnValueChanged(bool fromSlider)
    {
        if (fromSlider) NumericNoteSize.Value = (decimal?)SliderNoteSize.Value;
        else SliderNoteSize.Value = NumericNoteSize.Value != null ? (double)NumericNoteSize.Value : 10; // default to a typical note size if null
    }
    
    private void SliderNoteSize_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        int value = double.Sign(e.Delta.Y);
        SliderNoteSize.Value += value;
    }

    private void NumericMeasure_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null || NumericMeasure?.Value == null) return;
        NumericMeasure.Value = Math.Clamp((decimal)e.NewValue, NumericMeasure.Minimum, NumericMeasure.Maximum);
    }
    
    private void NumericBeatValue_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null || NumericMeasure?.Value == null || NumericBeatDivisor?.Value == null) return;

        decimal? value = e.NewValue;
        
        if (value >= NumericBeatDivisor.Value)
        {
            NumericMeasure.Value++;
            NumericBeatValue.Value = 0;
            return;
        }

        if (value < 0)
        {
            if (NumericMeasure.Value > 0)
            {
                NumericMeasure.Value--;
                NumericBeatValue.Value = NumericBeatDivisor.Value - 1;
                return;
            }

            if (NumericMeasure.Value <= 0)
            {
                NumericMeasure.Value = 0;
                NumericBeatValue.Value = 0;
            }
        }
    }

    private void NumericBeatDivisor_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        
    }
    
    // ________________ UI Dialogs
    
    private void OnSettingsClose(ContentDialog sender, ContentDialogClosingEventArgs e)
    {
        KeybindEditor.StopRebinding(); // Stop rebinding in case it was active.
        SetButtonColors(); // Update button colors if they were changed
        SetMenuItemInputGestureText(); // Update inputgesture text in case stuff was rebound
        File.WriteAllText("UserConfig.toml", Toml.FromModel(UserConfig));
    }

    private static void ShowWarningMessage(string title, string? text = null)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = text,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Ok
        };

        dialog.ShowAsync();
    }
    
    private async Task<bool> PromptSave()
    {
        if (ChartEditor.Chart.IsSaved) return false;

        ContentDialogResult result = await showSavePrompt();

        return result switch
        {
            ContentDialogResult.None => true,
            ContentDialogResult.Primary when await SaveFile(true) => true,
            ContentDialogResult.Secondary => false,
            _ => false
        };

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

    internal IStorageProvider GetStorageProvider()
    {
        if (VisualRoot is TopLevel top) return top.StorageProvider;
        throw new Exception(":3 something went wrong, too bad.");
    }
    
    private async Task<IStorageFile?> OpenChartFilePicker()
    {
        var result = await GetStorageProvider().OpenFilePickerAsync(new()
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
        return await GetStorageProvider().SaveFilePickerAsync(new()
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
        ChartEditor.Chart.IsNew = false;
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