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
using MercuryMapper.Data;
using MercuryMapper.Editor;
using MercuryMapper.Enums;
using MercuryMapper.Rendering;
using SkiaSharp;
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
        AudioManager = new(this);
        RenderEngine = new(this);

        var interval = TimeSpan.FromSeconds(1.0 / UserConfig.RenderConfig.RefreshRate);
        UpdateTimer = new(interval, DispatcherPriority.Background, UpdateTimer_Tick) { IsEnabled = false };

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
    public readonly AudioManager AudioManager;
    public readonly RenderEngine RenderEngine;
    public DispatcherTimer UpdateTimer;
    
    private TimeUpdateSource timeUpdateSource = TimeUpdateSource.None;
    private enum TimeUpdateSource
    {
        None,
        Slider,
        Numeric,
        Timer
    }
    
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

    public void SetSongPositionSliderMaximum()
    {
        SliderSongPosition.Value = 0;
        
        if (AudioManager.CurrentSong == null) return;
        SliderSongPosition.Maximum = AudioManager.CurrentSong.Length;
    }

    public void UpdateTimer_Tick(object? sender, EventArgs eventArgs)
    { 
        if (AudioManager.CurrentSong == null) return;
        
        if (AudioManager.CurrentSong.Position >= AudioManager.CurrentSong.Length && AudioManager.CurrentSong.IsPlaying)
        {
            SetPlayState(false);
            AudioManager.CurrentSong.Position = 0;
        }
        
        if (timeUpdateSource == TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Timer);
    }

    /// <summary>
    /// (Don't confuse this with UpdateTimer_Tick please)
    /// Updates the current time/measureDecimal the user is looking at.
    /// Requires a persistent "source" to track, otherwise the
    /// OnValueChanged events firing at each other causes an exponential
    /// chain reaction that turns your computer into a nuclear reactor.
    /// </summary>
    private void UpdateTime(TimeUpdateSource source)
    {
        if (AudioManager.CurrentSong == null) return;
        timeUpdateSource = source;
        
        BeatData data = ChartEditor.Chart.Timestamp2BeatData(AudioManager.CurrentSong.Position);
        
        if (source is not TimeUpdateSource.Timer && !AudioManager.CurrentSong.IsPlaying)
        {
            if (source is TimeUpdateSource.Slider) AudioManager.CurrentSong.Position = (uint)SliderSongPosition.Value;
            if (source is TimeUpdateSource.Numeric)
            {
                if (NumericMeasure.Value == null || NumericBeatValue.Value == null || NumericBeatDivisor.Value == null) return;
                
                float measureDecimal = (float)(NumericMeasure.Value + NumericBeatValue.Value / NumericBeatDivisor.Value);
                AudioManager.CurrentSong.Position = (uint)ChartEditor.Chart.BeatData2Timestamp(new(measureDecimal));
            }
        }
        
        if (source is not TimeUpdateSource.Numeric && NumericBeatDivisor.Value != null)
        {
            NumericMeasure.Value = data.Measure;
            NumericBeatValue.Value = (int)((data.MeasureDecimal - data.Measure) * (float)NumericBeatDivisor.Value);
        }

        if (source is not TimeUpdateSource.Slider)
        {
            SliderSongPosition.Value = (int)AudioManager.CurrentSong.Position;
        }
        
        ChartEditor.UpdateCurrentMeasure(data);

        Console.WriteLine(ChartEditor.CurrentMeasure);
        
        timeUpdateSource = TimeUpdateSource.None;
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteShape"])) 
        {
            // ChartEditor.EditNote(true, false);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteProperties"])) 
        {
            // ChartEditor.EditNote(false, true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteShapeProperties"])) 
        {
            // ChartEditor.EditNote(true, true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDelete"])) 
        {
            // Delete
            e.Handled = true;
            return;
        }
    }

    private static void OnKeyUp(object sender, KeyEventArgs e) => e.Handled = true;

    // ________________ Canvas

    private void Canvas_OnRenderSkia(SKCanvas canvas)
    {
        lock (ChartEditor.Chart) RenderEngine.Render(canvas);
    }
    
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
            if (AudioManager.CurrentSong == null || AudioManager.CurrentSong.IsPlaying) return;
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
    
    private void MainView_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty) Dispatcher.UIThread.Post(MainView_OnResize);
    }

    private void MainView_OnResize()
    {
        double width = RightPanel.Bounds.Left - LeftPanel.Bounds.Right;
        double height = SongControlPanel.Bounds.Top - TitleBar.Bounds.Bottom;
        double min = double.Min(width, height);

        Canvas.Width = min;
        Canvas.Height = min;
        RenderEngine.UpdateSize(min);
        RenderEngine.UpdateBrushes();
    }
    
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
                AudioManager.SetSong(filepath, 0.2f, (int)SliderPlaybackSpeed.Value); // TODO: Make Volume Dynamic!!
                SetSongPositionSliderMaximum();
            }
        });
    }

    private async void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        
        // Get .mer file
        IStorageFile? file = await OpenChartFilePicker();
        if (file == null) return;

        OpenChart(file.Path.LocalPath);
    }

    public async void DragDrop(string path)
    {
        if (await PromptSave()) return;
        OpenChart(path);
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
        
        if (timeUpdateSource is TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Numeric);
    }
    
    private void NumericBeatValue_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (e.NewValue == null || NumericMeasure?.Value == null || NumericBeatDivisor?.Value == null) return;

        decimal? value = e.NewValue;
        
        if (value >= NumericBeatDivisor.Value)
        {
            NumericMeasure.Value++;
            NumericBeatValue.Value = 0;
        }

        else if (value < 0)
        {
            if (NumericMeasure.Value > 0)
            {
                NumericMeasure.Value--;
                NumericBeatValue.Value = NumericBeatDivisor.Value - 1;
            }
            else
            {
                NumericMeasure.Value = 0;
                NumericBeatValue.Value = 0;
            }
        }
        
        if (timeUpdateSource is TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Numeric);
    }

    private void NumericBeatDivisor_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        
    }
    
    private void ButtonPlay_OnClick(object? sender, RoutedEventArgs e)
    {
        if (AudioManager.CurrentSong == null) return;
        SetPlayState(!AudioManager.CurrentSong.IsPlaying);
    }

    public void SetPlayState(bool play)
    {
        if (AudioManager.CurrentSong == null)
        {
            UpdateTimer.IsEnabled = false;
            IconPlay.IsVisible = true;
            IconStop.IsVisible = false;
            return;
        }
        
        AudioManager.CurrentSong.Volume = 0.2f;
        AudioManager.CurrentSong.IsPlaying = play;
        UpdateTimer.IsEnabled = play;
        
        IconPlay.IsVisible = !play;
        IconStop.IsVisible = play;
        SliderSongPosition.IsEnabled = !play;
    }
    
    private void SliderSongPosition_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (timeUpdateSource is TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Slider);
    }
    
    private void SliderPlaybackSpeed_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        TextBlockPlaybackSpeed.Text = $"{SliderPlaybackSpeed.Value,3:F0}%";
        if (AudioManager.CurrentSong == null) return;
        
        AudioManager.CurrentSong.PlaybackSpeed = (int)SliderPlaybackSpeed.Value;
    }
    
    // ________________ UI Dialogs & Misc
    // TODO: SORT THIS SHIT!!!
    
    private void OnSettingsClose(ContentDialog sender, ContentDialogClosingEventArgs e)
    {
        KeybindEditor.StopRebinding(); // Stop rebinding in case it was active.
        SetButtonColors(); // Update button colors if they were changed
        SetMenuItemInputGestureText(); // Update inputgesture text in case stuff was rebound
        RenderEngine.UpdateBrushes();
        RenderEngine.UpdateNoteSpeed();
        
        // I know some maniac is gonna change their refresh rate while playing a song.
        var interval = TimeSpan.FromSeconds(1.0 / UserConfig.RenderConfig.RefreshRate);
        UpdateTimer = new(interval, DispatcherPriority.Background, UpdateTimer_Tick) { IsEnabled = AudioManager.CurrentSong?.IsPlaying ?? false };
        
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
                Title = Assets.Lang.Resources.Generic_SaveWarning,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                SecondaryButtonText = Assets.Lang.Resources.Generic_No,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel
            };
            
            return dialog.ShowAsync();
        }
    }

    private async Task<bool> PromptSelectAudio()
    {
        ContentDialogResult result = await showSelectAudioPrompt();
        return result == ContentDialogResult.Primary;

        Task<ContentDialogResult> showSelectAudioPrompt()
        {
            ContentDialog dialog = new()
            {
                Title = Assets.Lang.Resources.Generic_InvalidAudioWarning,
                Content = Assets.Lang.Resources.Generic_SelectAudioPrompt,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                CloseButtonText = Assets.Lang.Resources.Generic_No
            };
            
            return dialog.ShowAsync();
        }
    }
    
    internal IStorageProvider GetStorageProvider()
    {
        if (VisualRoot is TopLevel top) return top.StorageProvider;
        throw new Exception(":3 something went wrong, too bad.");
    }
    
    public async Task<IStorageFile?> OpenAudioFilePicker()
    {
        var result = await GetStorageProvider().OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Audio files")
                {
                    Patterns = new[] {"*.wav","*.flac","*.mp3","*.ogg"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
        });

        return result.Count != 1 ? null : result[0];
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
    
    private async void OpenChart(string path)
    {
        // Load chart
        ChartEditor.Chart.LoadFile(path);
        
        // Oopsie, audio not found.
        if (!File.Exists(ChartEditor.Chart.AudioFilePath))
        {
            // Prompt user to select audio.
            if (!await PromptSelectAudio())
            {
                // User said no, clear chart again and return.
                ChartEditor.Chart.Clear();
                return;
            }
            
            // Get audio file
            IStorageFile? audioFile = await OpenAudioFilePicker();
            if (audioFile == null || !File.Exists(audioFile.Path.LocalPath))
            {
                ChartEditor.Chart.Clear();
                ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidAudio);
                return;
            }

            ChartEditor.Chart.AudioFilePath = audioFile.Path.LocalPath;
        }
        
        AudioManager.SetSong(ChartEditor.Chart.AudioFilePath, 0.2f, (int)SliderPlaybackSpeed.Value); // TODO: Make Volume Dynamic!!
        SetSongPositionSliderMaximum();
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