using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using MercuryMapper.Utils;
using MercuryMapper.Views.Gimmicks;
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
        HitsoundTimer = new(TimeSpan.FromMilliseconds(5), DispatcherPriority.Background, HitsoundTimer_Tick) { IsEnabled = false };

        KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);

        VersionText.Text = AppVersion;
        ApplySettings();
        ToggleTypeRadio(false);
        ToggleInsertButton();
        SetSelectionInfo();
    }

    public bool CanShutdown;
    public const string AppVersion = "v0.0.1 [Dev]";
    
    public UserConfig UserConfig = new();
    public readonly KeybindEditor KeybindEditor;
    public readonly ChartEditor ChartEditor;
    public readonly AudioManager AudioManager;
    public readonly RenderEngine RenderEngine;
    public DispatcherTimer UpdateTimer;
    public readonly DispatcherTimer HitsoundTimer;
    
    private TimeUpdateSource timeUpdateSource = TimeUpdateSource.None;
    private enum TimeUpdateSource
    {
        None,
        Slider,
        Numeric,
        Timer
    }

    private PointerState pointerState = PointerState.Released;
    private enum PointerState
    {
        Released,
        Pressed
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

    private void ToggleBonusTypeRadios(bool noBonus, bool bonus, bool rNote)
    {
        if (RadioNoBonus == null || RadioBonus == null || RadioRNote == null) return;
        RadioNoBonus.IsEnabled = noBonus;
        RadioBonus.IsEnabled = bonus;
        RadioRNote.IsEnabled = rNote;

        if ((RadioBonus.IsChecked == true && !bonus) || (RadioRNote.IsChecked == true && !rNote))
            RadioNoBonus.IsChecked = true;
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
        ButtonEditHold.IsVisible = state is not ChartEditorState.InsertHold;
        ButtonEndHold.IsVisible = state is ChartEditorState.InsertHold;

        ButtonEditHold.IsEnabled = state is ChartEditorState.InsertNote or ChartEditorState.InsertHold;
        ButtonEndHold.IsEnabled = state is ChartEditorState.InsertNote or ChartEditorState.InsertHold;
    }

    public void SetSongPositionSliderMaximum()
    {
        SliderSongPosition.Value = 0;
        
        if (AudioManager.CurrentSong == null) return;
        SliderSongPosition.Maximum = AudioManager.CurrentSong.Length;
    }

    public void UpdateTimer_Tick(object? sender, EventArgs e)
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
    
    public void HitsoundTimer_Tick(object? sender, EventArgs e)
    {
        float latency = BassSoundEngine.GetLatency();
        float measure = ChartEditor.CurrentMeasureDecimal + latency;
        
        while (AudioManager.HitsoundNoteIndex < ChartEditor.Chart.Notes.Count
               && ChartEditor.Chart.Notes[AudioManager.HitsoundNoteIndex].BeatData.MeasureDecimal <= measure)
        {
            AudioManager.PlayHitsound(ChartEditor.Chart.Notes[AudioManager.HitsoundNoteIndex]);
        }
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
        
        if (source is not TimeUpdateSource.Timer && !AudioManager.CurrentSong.IsPlaying)
        {
            if (source is TimeUpdateSource.Slider)
            {
                AudioManager.CurrentSong.Position = (uint)SliderSongPosition.Value;
            }

            if (source is TimeUpdateSource.Numeric)
            {
                if (NumericMeasure.Value == null || NumericBeatValue.Value == null || NumericBeatDivisor.Value == null) return;
                
                float measureDecimal = (float)(NumericMeasure.Value + NumericBeatValue.Value / NumericBeatDivisor.Value);
                AudioManager.CurrentSong.Position = (uint)ChartEditor.Chart.BeatData2Timestamp(new(measureDecimal));
            }
        }
        
        BeatData data = ChartEditor.Chart.Timestamp2BeatData(AudioManager.CurrentSong.Position);
        
        if (source is not TimeUpdateSource.Numeric && NumericBeatDivisor.Value != null)
        {
            // The + 0.002f is a hacky "fix". There's some weird rounding issue that has carried over from BAKKA,
            // most likely caused by ManagedBass or AvaloniaUI jank. If you increment NumericBeatValue up,
            // it's often not quite enough and it falls back to the value it was before.
            
            NumericMeasure.Value = data.Measure;
            NumericBeatValue.Value = (int)((data.MeasureDecimal - data.Measure + 0.002f) * (float)NumericBeatDivisor.Value);
        }

        if (source is not TimeUpdateSource.Slider)
        {
            SliderSongPosition.Value = (int)AudioManager.CurrentSong.Position;
        }
        
        ChartEditor.CurrentMeasureDecimal = data.MeasureDecimal;
        ToggleInsertButton();
        
        timeUpdateSource = TimeUpdateSource.None;
    }

    public void ToggleInsertButton()
    {
        int endOfChartCount = ChartEditor.Chart.Notes.Count(x => x.NoteType is NoteType.EndOfChart);
            
        bool blockEndOfChart = endOfChartCount == 1 && ChartEditor.CurrentNoteType is NoteType.EndOfChart;
        bool behindEndOfChart = endOfChartCount != 0 && ChartEditor.CurrentMeasureDecimal >= ChartEditor.Chart.Notes.FirstOrDefault(x => x.NoteType is NoteType.EndOfChart)?.BeatData.MeasureDecimal;
        bool beforeHoldStart = ChartEditor is { CurrentHoldStart: not null, EditorState: ChartEditorState.InsertHold } && ChartEditor.CurrentMeasureDecimal <= ChartEditor.CurrentHoldStart.BeatData.MeasureDecimal;
        bool beforeLastHold = ChartEditor is { LastPlacedHold: not null, EditorState: ChartEditorState.InsertHold } && ChartEditor.CurrentMeasureDecimal <= ChartEditor.LastPlacedHold.BeatData.MeasureDecimal;
            
        ButtonInsert.IsEnabled = !(blockEndOfChart || behindEndOfChart || beforeHoldStart || beforeLastHold);
    }
    
    public void SetSelectionInfo()
        {
            SelectionInfoSelectedNotesValue.Text = ChartEditor.SelectedNotes.Count.ToString();
            
            if (ChartEditor.HighlightedElement is null)
            {
                SelectionInfoMeasureValue.Text = "/";
                SelectionInfoBeatValue.Text = "/";
                
                SelectionInfoNoteType.IsVisible = false;
                SelectionInfoNoteTypeValue.IsVisible = false;
                SelectionInfoGimmickType.IsVisible = false;
                SelectionInfoGimmickTypeValue.IsVisible = false;
                SelectionInfoPosition.IsVisible = false;
                SelectionInfoPositionValue.IsVisible = false;
                SelectionInfoSize.IsVisible = false;
                SelectionInfoSizeValue.IsVisible = false;
                SelectionInfoMaskDirection.IsVisible = false;
                SelectionInfoMaskDirectionValue.IsVisible = false;
                SelectionInfoBpm.IsVisible = false;
                SelectionInfoBpmValue.IsVisible = false;
                SelectionInfoHiSpeed.IsVisible = false;
                SelectionInfoHiSpeedValue.IsVisible = false;
                SelectionInfoTimeSig.IsVisible = false;
                SelectionInfoTimeSigValue.IsVisible = false;
                
                SelectionInfoSeparator1.IsVisible = false;
                SelectionInfoSeparator2.IsVisible = false;

                return;
            }

            SelectionInfoMeasureValue.Text = ChartEditor.HighlightedElement.BeatData.Measure.ToString();

            int tick = ChartEditor.HighlightedElement.BeatData.Tick;
            int gcd = MathExtensions.GreatestCommonDivisor(tick, 1920);
            SelectionInfoBeatValue.Text = $"{tick / gcd} / {1920 / gcd}";
            
            if (ChartEditor.HighlightedElement is Gimmick gimmick)
            {
                SelectionInfoGimmickTypeValue.Text = Enums2String.GimmickType2String(gimmick.GimmickType);
                SelectionInfoBpmValue.Text = gimmick.Bpm.ToString(CultureInfo.CurrentCulture);
                SelectionInfoHiSpeedValue.Text = gimmick.HiSpeed.ToString(CultureInfo.CurrentCulture);
                SelectionInfoTimeSigValue.Text = $"{gimmick.TimeSig.Upper} / {gimmick.TimeSig.Lower}";
                
                SelectionInfoNoteType.IsVisible = false;
                SelectionInfoNoteTypeValue.IsVisible = false;
                SelectionInfoPosition.IsVisible = false;
                SelectionInfoPositionValue.IsVisible = false;
                SelectionInfoSize.IsVisible = false;
                SelectionInfoSizeValue.IsVisible = false;
                SelectionInfoMaskDirection.IsVisible = false;
                SelectionInfoMaskDirectionValue.IsVisible = false;
                
                SelectionInfoGimmickType.IsVisible = true;
                SelectionInfoGimmickTypeValue.IsVisible = true;
                SelectionInfoBpm.IsVisible = gimmick.GimmickType is GimmickType.BpmChange;
                SelectionInfoBpmValue.IsVisible = gimmick.GimmickType is GimmickType.BpmChange;
                SelectionInfoHiSpeed.IsVisible = gimmick.GimmickType is GimmickType.HiSpeedChange;
                SelectionInfoHiSpeedValue.IsVisible = gimmick.GimmickType is GimmickType.HiSpeedChange;
                SelectionInfoTimeSig.IsVisible = gimmick.GimmickType is GimmickType.TimeSigChange;
                SelectionInfoTimeSigValue.IsVisible = gimmick.GimmickType is GimmickType.TimeSigChange;
                
                SelectionInfoSeparator1.IsVisible = true;
                SelectionInfoSeparator2.IsVisible = gimmick is { IsStop: false, IsReverse: false };
            }

            if (ChartEditor.HighlightedElement is Note note)
            {
                SelectionInfoPositionValue.Text = note.Position.ToString();
                SelectionInfoSizeValue.Text = note.Size.ToString();
                SelectionInfoNoteTypeValue.Text = Enums2String.NoteType2String(note.NoteType);
                SelectionInfoMaskDirectionValue.Text = Enums2String.MaskDirection2String(note.MaskDirection);
                
                SelectionInfoNoteType.IsVisible = true;
                SelectionInfoNoteTypeValue.IsVisible = true;
                SelectionInfoGimmickType.IsVisible = false;
                SelectionInfoGimmickTypeValue.IsVisible = false;
                SelectionInfoPosition.IsVisible = true;
                SelectionInfoPositionValue.IsVisible = true;
                SelectionInfoSize.IsVisible = true;
                SelectionInfoSizeValue.IsVisible = true;
                SelectionInfoMaskDirection.IsVisible = note.IsMask;
                SelectionInfoMaskDirectionValue.IsVisible = note.IsMask;
                
                SelectionInfoBpm.IsVisible = false;
                SelectionInfoBpmValue.IsVisible = false;
                SelectionInfoHiSpeed.IsVisible = false;
                SelectionInfoHiSpeedValue.IsVisible = false;
                SelectionInfoTimeSig.IsVisible = false;
                SelectionInfoTimeSigValue.IsVisible = false;
                
                SelectionInfoSeparator1.IsVisible = true;
                SelectionInfoSeparator2.IsVisible = true;
            }
        }

    public void SetChartInfo()
    {
        ChartInfoAudioFilepath.Text = Path.GetFileName(ChartEditor.Chart.AudioFilePath);
        
        ChartInfoAuthor.Text = ChartEditor.Chart.Author;
        ChartInfoLevel.Value = (double)ChartEditor.Chart.Level;
        ChartInfoClearThreshold.Value = (double)ChartEditor.Chart.ClearThreshold;
        ChartInfoPreviewTime.Value = (double)ChartEditor.Chart.PreviewTime;
        ChartInfoPreviewLength.Value = (double)ChartEditor.Chart.PreviewLength;
        ChartInfoOffset.Value = (double)ChartEditor.Chart.Offset;
        ChartInfoMovieOffset.Value = (double)ChartEditor.Chart.MovieOffset;
    }

    public void SetMinNoteSize(NoteType type)
    {
        int minimum = type switch
        {
            NoteType.Touch => 4,
            NoteType.TouchBonus => 5,
            NoteType.SnapForward => 6,
            NoteType.SnapBackward => 6,
            NoteType.SlideClockwise => 5,
            NoteType.SlideClockwiseBonus => 7,
            NoteType.SlideCounterclockwise => 5,
            NoteType.SlideCounterclockwiseBonus => 7,
            NoteType.HoldStart => 2,
            NoteType.HoldSegment => 1,
            NoteType.HoldEnd => 1,
            NoteType.MaskAdd => 1,
            NoteType.MaskRemove => 1,
            NoteType.EndOfChart => 60,
            NoteType.Chain => 4,
            NoteType.TouchRNote => 6,
            NoteType.SnapForwardRNote => 8,
            NoteType.SnapBackwardRNote => 8,
            NoteType.SlideClockwiseRNote => 10,
            NoteType.SlideCounterclockwiseRNote => 10,
            NoteType.HoldStartRNote => 8,
            NoteType.ChainRNote => 10,
            _ => 5
        };

        SliderNoteSize.Minimum = minimum;
        NumericNoteSize.Minimum = minimum;
        ChartEditor.Cursor.MinSize = minimum;

        SliderNoteSize.Value = double.Max(SliderNoteSize.Value, minimum);
    }
    
    // ________________ Input
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            return;
        }
        if (KeybindEditor.RebindingActive)
        {
            // Sink arrow keys and manually "invoke" OnKeyDown to avoid
            // Avalonia arrow key navigation interfering with rebinding.
            if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                KeybindEditor.OnKeyDown(e);
                e.Handled = true;
            }

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
            ChartEditor.Undo();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditRedo"])) 
        {
            ChartEditor.Redo();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditCut"]))
        {
            ChartEditor.Cut();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditCopy"])) 
        {
            ChartEditor.Copy();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditPaste"])) 
        {
            ChartEditor.Paste();
            e.Handled = true;
            return;
        }
        
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorInsert"]))
        {
            if (!ButtonInsert.IsEnabled) return;
            
            ChartEditor.InsertChartElement();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorPlay"])) 
        {
            ButtonPlay_OnClick(null, new());
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSelectAll"])) 
        {
            ChartEditor.SelectAllNotes();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDeselectAll"])) 
        {
            ChartEditor.DeselectAllNotes();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEndHold"]))
        {
            ChartEditor.EndHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditHold"]))
        {
            ChartEditor.EditHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorHighlightNextNote"]))
        {
            ChartEditor.HighlightNextElement();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorHighlightPrevNote"]))
        {
            ChartEditor.HighlightPrevElement();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorHighlightNearestNote"]))
        {
            ChartEditor.HighlightNearestElement();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSelectHighlightedNote"]))
        {
            ChartEditor.SelectHighlightedNote();
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorTypeRadio1"]))
        {
            if (BonusTypePanel.IsVisible) RadioNoBonus.IsChecked = true;
            else RadioMaskClockwise.IsChecked = true;
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorTypeRadio2"]))
        {
            if (BonusTypePanel.IsVisible && RadioBonus.IsEnabled) RadioBonus.IsChecked = true;
            else RadioMaskCounterclockwise.IsChecked = true;
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorTypeRadio3"]))
        {
            if (BonusTypePanel.IsVisible) RadioRNote.IsChecked = true;
            else RadioMaskCenter.IsChecked = true;
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteShape"])) 
        {
            ChartEditor.EditSelection(true, false);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteProperties"])) 
        {
            ChartEditor.EditSelection(false, true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteShapeProperties"])) 
        {
            ChartEditor.EditSelection(true, true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorMirrorNote"])) 
        {
            ChartEditor.MirrorSelection((int?)NumericMirrorAxis.Value ?? 30);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDelete"])) 
        {
            ChartEditor.DeleteSelection();
            ChartEditor.DeleteGimmick();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBakeHold"]))
        {
            ChartEditor.BakeHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditIncreaseSize"]))
        {
            ChartEditor.QuickEditSize(1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditDecreaseSize"]))
        {
            ChartEditor.QuickEditSize(-1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditIncreasePosition"]))
        {
            ChartEditor.QuickEditPosition(1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditDecreasePosition"]))
        {
            ChartEditor.QuickEditPosition(-1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditIncreaseTimestamp"]))
        {
            ChartEditor.QuickEditTimestamp(1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditDecreaseTimestamp"]))
        {
            ChartEditor.QuickEditTimestamp(-1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["RenderIncreaseNoteSpeed"]))
        {
            UserConfig.RenderConfig.NoteSpeed = decimal.Min(UserConfig.RenderConfig.NoteSpeed + 0.1m, 6);
            File.WriteAllText("UserConfig.toml", Toml.FromModel(UserConfig));
            RenderEngine.UpdateVisibleTime();
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["RenderDecreaseNoteSpeed"]))
        {
            UserConfig.RenderConfig.NoteSpeed = decimal.Max(UserConfig.RenderConfig.NoteSpeed - 0.1m, 1);
            File.WriteAllText("UserConfig.toml", Toml.FromModel(UserConfig));
            RenderEngine.UpdateVisibleTime();
            
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
        if (pointerState is not PointerState.Pressed) return;

        PointerPoint p = e.GetCurrentPoint(Canvas);
        
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            OnClick(p, e.KeyModifiers, true);
            return;
        }
        
        float x = (float)(p.Position.X - Canvas.Width * 0.5);
        float y = (float)(p.Position.Y - Canvas.Height * 0.5);
        int theta = MathExtensions.GetThetaNotePosition(x, y);
        
        ChartEditor.Cursor.Drag(theta);
        NumericNotePosition.Value = ChartEditor.Cursor.Position;
        NumericNoteSize.Value = ChartEditor.Cursor.Size;
    }

    private void Canvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint p = e.GetCurrentPoint(Canvas);
        OnClick(p, e.KeyModifiers, false);
    }
    
    private void Canvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        pointerState = PointerState.Released;
        ChartEditor.LastSelectedNote = null;
    }
    
    private void OnClick(PointerPoint p, KeyModifiers modifiers, bool pointerMoved)
    {
        SKPoint point = new((float)p.Position.X, (float)p.Position.Y);
        point.X -= (float)Canvas.Width * 0.5f;
        point.Y -= (float)Canvas.Height * 0.5f;
        point.X /= (float)(Canvas.Width - 30) * 0.5f;
        point.Y /= (float)(Canvas.Height - 30) * 0.5f;
        point.X /= 0.9f;
        point.Y /= 0.9f;
        
        pointerState = PointerState.Pressed;
        
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            // Selecting
            if ((Note?)RenderEngine.GetChartElementAtPointer(ChartEditor.Chart, point, false) is not { } note) return;
            
            if (pointerMoved && note == ChartEditor.LastSelectedNote) return;
            
            ChartEditor.SelectNote(note);
            ChartEditor.LastSelectedNote = note;
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            // Highlighting
            if (RenderEngine.GetChartElementAtPointer(ChartEditor.Chart, point, true) is not { } note) return;
            
            if (pointerMoved && note == ChartEditor.HighlightedElement) return;
            
            ChartEditor.HighlightElement(note);
        }

        else
        {
            // Moving Cursor
            int theta = MathExtensions.GetThetaNotePosition(point.X, point.Y);
            ChartEditor.Cursor.Move(theta);
            NumericNotePosition.Value = ChartEditor.Cursor.Position;
            NumericNoteSize.Value = ChartEditor.Cursor.Size;
        }
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
    
    public async void DragDrop(string path)
    {
        if (await PromptSave()) return;
        OpenChart(path);
    }
    
    private async void MenuItemNew_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;

        NewChartView newChartView = new(this);
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Editor_NewChart,
            Content = newChartView,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
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
                
                AudioManager.ResetSong();
                
                ChartEditor.NewChart(filepath, author, bpm, timeSigUpper, timeSigLower);
                AudioManager.SetSong(filepath, (float)UserConfig.AudioConfig.MusicVolume * 0.01f, (int)SliderPlaybackSpeed.Value);
                SetSongPositionSliderMaximum();
                RenderEngine.UpdateVisibleTime();
            }
        });
    }

    private async void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        
        // Get .mer file
        IStorageFile? file = await OpenChartFilePicker();
        if (file == null) return;

        AudioManager.ResetSong();
        OpenChart(file.Path.LocalPath);
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
            PrimaryButtonText = Assets.Lang.Resources.Settings_RevertToDefault,
            CloseButtonText = Assets.Lang.Resources.Generic_SaveAndClose,
        };

        dialog.PrimaryButtonClick += OnSettingsPrimary;
        dialog.CloseButtonClick += OnSettingsClose;
        
        Dispatcher.UIThread.Post(async () => await dialog.ShowAsync());
    }

    public async void MenuItemExit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (await PromptSave()) return;
        CanShutdown = true;
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void MenuItemUndo_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Undo();

    private void MenuItemRedo_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Redo();

    private void MenuItemCut_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Cut();

    private void MenuItemCopy_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Copy();

    private void MenuItemPaste_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Paste();

    private void RadioNoteType_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (RadioNoteMaskAdd?.IsChecked == null || RadioNoteMaskRemove?.IsChecked == null) return;
        if (sender is not RadioButton selected || selected.IsChecked == false) return;

        NoteType noteType = selected.Name switch
        {
            "RadioNoteTouch" => NoteType.Touch,
            "RadioNoteSlideClockwise" => NoteType.SlideClockwise,
            "RadioNoteSlideCounterclockwise" => NoteType.SlideCounterclockwise,
            "RadioNoteSnapForward" => NoteType.SnapForward,
            "RadioNoteSnapBackward" => NoteType.SnapBackward,
            "RadioNoteChain" => NoteType.Chain,
            "RadioNoteHold" => NoteType.HoldStart,
            "RadioNoteMaskAdd" => NoteType.MaskAdd,
            "RadioNoteMaskRemove" => NoteType.MaskRemove,
            "RadioNoteEndOfChart" => NoteType.EndOfChart,
            _ => throw new ArgumentOutOfRangeException()
        };

        ChartEditor.CurrentNoteType = noteType;
        ChartEditor.UpdateCursorNoteType();

        bool isMask = noteType is NoteType.MaskAdd or NoteType.MaskRemove;
        bool noBonusAvailable = noteType is not NoteType.EndOfChart;
        bool bonusAvailable = noteType is NoteType.Touch or NoteType.SlideClockwise or NoteType.SlideCounterclockwise;
        bool rNoteAvailable = noteType is not NoteType.EndOfChart;
        
        ToggleTypeRadio(isMask);
        ToggleBonusTypeRadios(noBonusAvailable, bonusAvailable, rNoteAvailable);
        
        Console.WriteLine(ChartEditor.CurrentNoteType);
    }

    private void RadioBonusType_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton selected || selected.IsChecked == false) return;
        BonusType bonusType = selected.Name switch
        {
            "RadioNoBonus" => BonusType.None,
            "RadioBonus" => BonusType.Bonus,
            "RadioRNote" => BonusType.RNote,
            _ => throw new ArgumentOutOfRangeException()
        };

        ChartEditor.CurrentBonusType = bonusType;
        ChartEditor.UpdateCursorNoteType();
        
        Console.WriteLine(ChartEditor.CurrentNoteType);
    }

    private void RadioMaskDirection_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton selected || selected.IsChecked == false) return;
        MaskDirection maskDirection = selected.Name switch
        {
            "RadioMaskClockwise" => MaskDirection.Clockwise,
            "RadioMaskCounterclockwise" => MaskDirection.Counterclockwise,
            "RadioMaskCenter" => MaskDirection.Center,
            _ => throw new ArgumentOutOfRangeException()
        };

        ChartEditor.CurrentMaskDirection = maskDirection;
        ChartEditor.UpdateCursorNoteType();
        
        Console.WriteLine(ChartEditor.CurrentNoteType);
    }

    private void ButtonGimmickBpmChange_OnClick(object? sender, RoutedEventArgs e)
    {
        GimmickView_Bpm gimmickView = new();
        ContentDialog dialog = new()
        {
            Content = gimmickView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            ChartEditor.InsertBpmChange((float)gimmickView.BpmNumberBox.Value);
        });
    }

    private void ButtonGimmickTimeSig_OnClick(object? sender, RoutedEventArgs e)
    {
        GimmickView_TimeSig gimmickView = new();
        ContentDialog dialog = new()
        {
            Content = gimmickView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            ChartEditor.InsertTimeSigChange((int)gimmickView.TimeSigUpperNumberBox.Value, (int)gimmickView.TimeSigLowerNumberBox.Value);
        });
    }

    private void ButtonGimmickHiSpeed_OnClick(object? sender, RoutedEventArgs e)
    {
        GimmickView_HiSpeed gimmickView = new();
        ContentDialog dialog = new()
        {
            Content = gimmickView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            ChartEditor.InsertHiSpeedChange((float)gimmickView.HiSpeedNumberBox.Value);
        });
    }

    private void ButtonGimmickStop_OnClick(object? sender, RoutedEventArgs e)
    {
        GimmickView_Stop gimmickView = new((int)(NumericMeasure.Value ?? 0), (int)(NumericBeatValue.Value ?? 0), (int)(NumericBeatDivisor.Value ?? 16));
        ContentDialog dialog = new()
        {
            Content = gimmickView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            if (gimmickView.IsValueNull) return;
            
            ChartEditor.InsertStop(gimmickView.StartMeasureDecimal, gimmickView.EndMeasureDecimal);
        });
    }

    private void ButtonGimmickReverse_OnClick(object? sender, RoutedEventArgs e)
    {
        GimmickView_Reverse gimmickView = new((int)(NumericMeasure.Value ?? 0), (int)(NumericBeatValue.Value ?? 0), (int)(NumericBeatDivisor.Value ?? 16));
        ContentDialog dialog = new()
        {
            Content = gimmickView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            if (gimmickView.IsValueNull) return;
            
            ChartEditor.InsertReverse(gimmickView.EffectStartMeasureDecimal, gimmickView.EffectEndMeasureDecimal, gimmickView.NoteEndMeasureDecimal);
        });
    }
    
    private void ButtonInsert_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.InsertChartElement();
    }

    private void ButtonEditHold_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.EditHold();
    }

    private void ButtonEndHold_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.EndHold();
    }
    
    private void SliderPosition_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => Position_OnValueChanged(true);
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

        ChartEditor.Cursor.Position = (int)NumericNotePosition.Value;
    }
    
    private void SliderSize_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => Size_OnValueChanged(true);
    private void SliderNoteSize_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
        int value = double.Sign(e.Delta.Y);
        SliderNoteSize.Value += value;
    }
    private void NumericSize_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => Size_OnValueChanged(false);
    private void Size_OnValueChanged(bool fromSlider)
    {
        if (fromSlider) NumericNoteSize.Value = (decimal?)SliderNoteSize.Value;
        else SliderNoteSize.Value = (double)(NumericNoteSize.Value ?? 15); // default to a typical note size if null

        ChartEditor.Cursor.Size = (int)(NumericNoteSize.Value ?? 15);
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
        if (AudioManager.CurrentSong is { IsPlaying: true }) return;
        
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
        if (e.NewValue == null || NumericMeasure?.Value == null || NumericBeatValue?.Value == null) return;
        if (AudioManager.CurrentSong is { IsPlaying: true }) return;

        decimal oldValue = e.OldValue ?? 16;
        decimal ratio = (decimal)e.NewValue / oldValue;
        NumericBeatValue.Value = decimal.Round((decimal)NumericBeatValue.Value * ratio);
    }
    
    private void ButtonPlay_OnClick(object? sender, RoutedEventArgs e)
    {
        if (AudioManager.CurrentSong == null) return;
        SetPlayState(!AudioManager.CurrentSong.IsPlaying);
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

    private async void ChartInfoSelectAudio_OnClick(object? sender, RoutedEventArgs e)
    {
        // prompt user to create new chart if no start time events exist -> no chart exists
        if (ChartEditor.Chart.StartBpm == null || ChartEditor.Chart.StartTimeSig == null)
        {
            MenuItemNew_OnClick(sender, e);
            return;
        }
        
        IStorageFile? audioFile = await OpenAudioFilePicker();
        if (audioFile == null) return;
        if (!File.Exists(audioFile.Path.LocalPath))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidAudio);
            return;
        }

        ChartEditor.Chart.AudioFilePath = audioFile.Path.LocalPath;
        
        AudioManager.SetSong(ChartEditor.Chart.AudioFilePath, (float)(UserConfig.AudioConfig.MusicVolume * 0.01), (int)SliderPlaybackSpeed.Value);
        SetSongPositionSliderMaximum();
        RenderEngine.UpdateVisibleTime();
    }
    
    private void ChartInfoAuthor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.Author = ChartInfoAuthor.Text ?? "";
    }
    
    private void ChartInfoLevel_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.Level = (decimal)ChartInfoLevel.Value;
    }
    
    private void ChartInfoClearThreshold_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.ClearThreshold = (decimal)ChartInfoClearThreshold.Value;
    }
    
    private void ChartInfoPreviewTime_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.PreviewTime = (decimal)ChartInfoPreviewTime.Value;
    }
    
    private void ChartInfoPreviewLength_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.PreviewLength = (decimal)ChartInfoPreviewLength.Value;
    }
    
    private void ChartInfoOffset_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.Offset = (decimal)ChartInfoOffset.Value;
    }
    
    private void ChartInfoMovieOffset_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.MovieOffset = (decimal)ChartInfoMovieOffset.Value;
    }
    
    private void SelectionInfoHighlightNext_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.HighlightNextElement();
    
    private void SelectionInfoHighlightPrev_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.HighlightPrevElement();
    
    private void SelectionInfoHighlightNearest_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.HighlightNearestElement();
    
    private void QuickSettingsNoteSpeed_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        UserConfig.RenderConfig.NoteSpeed = QuickSettingsNumericNoteSpeed.Value ?? 4.5m;
        RenderEngine.UpdateVisibleTime();
    }

    private void QuickSettingsBeatDivision_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        UserConfig.RenderConfig.BeatDivision = (int)(QuickSettingsNumericBeatDivision.Value ?? 4);
        RenderEngine.UpdateVisibleTime();
    }
    
    private void QuickSettingsSliderMusic_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UserConfig.AudioConfig.MusicVolume = QuickSettingsSliderMusic.Value;
        AudioManager.UpdateVolume();
    }

    private void QuickSettingsSliderHitsound_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UserConfig.AudioConfig.HitsoundVolume = QuickSettingsSliderHitsound.Value;
        AudioManager.UpdateVolume();
    }
    
    private void ButtonEditSelectionShape_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(true, false);
    
    private void ButtonEditSelectionProperties_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(false, true);
    
    private void ButtonEditSelectionFull_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(true, true);

    private void ButtonMirrorSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.MirrorSelection((int?)NumericMirrorAxis.Value ?? 30);
    
    private void ButtonDeleteSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeleteSelection();

    private void NumericMirrorAxis_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        NumericMirrorAxis.Value ??= 30;
        if (NumericMirrorAxis.Value > 59) NumericMirrorAxis.Value = 0;
        if (NumericMirrorAxis.Value < 0) NumericMirrorAxis.Value = 59;

        RenderEngine.MirrorAxis = (int)NumericMirrorAxis.Value;
    }
    
    private void NumericMirrorAxis_OnPointerEntered(object? sender, PointerEventArgs e) => RenderEngine.IsHoveringOverMirrorAxis = true;
    
    private void NumericMirrorAxis_OnPointerExited(object? sender, PointerEventArgs e) => RenderEngine.IsHoveringOverMirrorAxis = false;

    private void ButtonEditGimmick_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditGimmick();

    private void ButtonDeleteGimmick_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeleteGimmick();
    
    // ________________ UI Dialogs & Misc
    
    // This should probably be sorted but I can't be arsed rn.
    private void OnSettingsClose(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ApplySettings();
    }

    private void OnSettingsPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Settings_RevertWarning,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
            CloseButtonText = Assets.Lang.Resources.Generic_No,
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is ContentDialogResult.Primary)
            {
                UserConfig = new();
                ApplySettings();
            }
        });
    }
    
    private void ApplySettings()
    {
        Console.WriteLine("Applying settings");
        KeybindEditor.StopRebinding(); // Stop rebinding in case it was active.
        SetButtonColors(); // Update button colors if they were changed
        SetMenuItemInputGestureText(); // Update inputgesture text in case stuff was rebound
        RenderEngine.UpdateBrushes();
        RenderEngine.UpdateVisibleTime();
        AudioManager.LoadHitsoundSamples();
        AudioManager.UpdateVolume();

        QuickSettingsNumericBeatDivision.Value = UserConfig.RenderConfig.BeatDivision;
        QuickSettingsNumericNoteSpeed.Value = UserConfig.RenderConfig.NoteSpeed;
        QuickSettingsSliderMusic.Value = UserConfig.AudioConfig.MusicVolume;
        QuickSettingsSliderHitsound.Value = UserConfig.AudioConfig.HitsoundVolume;
        
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
    
    public void SetPlayState(bool play)
    {
        if (AudioManager.CurrentSong == null)
        {
            UpdateTimer.IsEnabled = false;
            HitsoundTimer.IsEnabled = false;
            IconPlay.IsVisible = true;
            IconStop.IsVisible = false;
            return;
        }

        AudioManager.CurrentSong.Volume = (float)(UserConfig.AudioConfig.MusicVolume * 0.01);
        AudioManager.CurrentSong.IsPlaying = play;
        UpdateTimer.IsEnabled = play;
        HitsoundTimer.IsEnabled = play;
        
        IconPlay.IsVisible = !play;
        IconStop.IsVisible = play;
        SliderSongPosition.IsEnabled = !play;

        AudioManager.HitsoundNoteIndex = int.Max(0, ChartEditor.Chart.Notes.FindIndex(x => x.BeatData.MeasureDecimal >= ChartEditor.CurrentMeasureDecimal));
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
                    Patterns = new[] {"*.mer", "*.map"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
        });

        return result.Count != 1 ? null : result[0];
    }
    
    private async Task<IStorageFile?> SaveChartFilePicker(ChartWriteType writeType)
    {
        return await GetStorageProvider().SaveFilePickerAsync(new()
        {
            DefaultExtension = writeType switch
            {
                ChartWriteType.Editor => ".map",
                ChartWriteType.Mercury => ".mer",
                ChartWriteType.Saturn => ".mer",
                _ => ""
            },
            FileTypeChoices = new[]
            {
                new FilePickerFileType(writeType switch
                {
                    ChartWriteType.Editor => "Editor Chart File",
                    ChartWriteType.Mercury => "Mercury Chart File",
                    ChartWriteType.Saturn => "Saturn Chart File",
                    _ => ""
                })
                {
                    Patterns = writeType switch
                    {
                        ChartWriteType.Editor => ["*.map"],
                        ChartWriteType.Mercury => ["*.mer"],
                        ChartWriteType.Saturn => ["*.mer"],
                        _ => []
                    },
                    AppleUniformTypeIdentifiers = ["public.item"]
                }
            }
        });
    }
    
    private async void OpenChart(string path)
    {
        // Load chart
        ChartEditor.LoadChart(path);
        
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
        
        AudioManager.SetSong(ChartEditor.Chart.AudioFilePath, (float)(UserConfig.AudioConfig.MusicVolume * 0.01), (int)SliderPlaybackSpeed.Value);
        SetSongPositionSliderMaximum();
        RenderEngine.UpdateVisibleTime();
    }
    
    public async Task<bool> SaveFile(bool openFilePicker)
    {
        string filepath = "";
        
        if (openFilePicker)
        {
            IStorageFile? file = await SaveChartFilePicker(ChartWriteType.Editor);

            if (file == null) return false;
            filepath = file.Path.LocalPath;
        }

        if (string.IsNullOrEmpty(filepath)) return false;

        ChartEditor.Chart.WriteFile(filepath, ChartWriteType.Editor);
        ChartEditor.Chart.IsNew = false;
        return true;
    }

    public async Task ExportFile(ChartWriteType chartWriteType)
    {
        IStorageFile? file = await SaveChartFilePicker(chartWriteType);

        if (file == null) return;
        string filepath = file.Path.LocalPath;
        
        if (string.IsNullOrEmpty(filepath)) return;
        ChartEditor.Chart.WriteFile(filepath, chartWriteType, false);
    }
}