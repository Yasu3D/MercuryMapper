 using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MercuryMapper.Audio;
using MercuryMapper.Config;
using MercuryMapper.Data;
using MercuryMapper.Editor;
using MercuryMapper.Enums;
using MercuryMapper.MultiCharting;
using MercuryMapper.Rendering;
using MercuryMapper.Utils;
using MercuryMapper.Views.Gimmicks;
using MercuryMapper.Views.Misc;
using MercuryMapper.Views.Online;
using MercuryMapper.Views.Select;
using MercuryMapper.Views.Tools;
using SkiaSharp;
using Tomlyn;

namespace MercuryMapper.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        ChartEditor = new(this);

        InitializeComponent();
        LoadUserConfig();
        LoadUsageTime();

        KeybindEditor = new(UserConfig);
        AudioManager = new(this);
        RenderEngine = new(this);

        PeerManager = new(this);
        PeerBroadcaster = new(this);
        ConnectionManager = new(this);
        
        usageTimer = new(UsageTimer_Tick, null, 0, 1000);
        updateInterval = TimeSpan.FromSeconds(1.0 / UserConfig.RenderConfig.RefreshRate);
        UpdateTimer = new(UpdateTimer_Tick, null, Timeout.Infinite, Timeout.Infinite);
        HitsoundTimer = new(TimeSpan.FromMilliseconds(1), DispatcherPriority.Background, HitsoundTimer_Tick) { IsEnabled = false };
        AutosaveTimer = new(TimeSpan.FromMinutes(1), DispatcherPriority.Background, AutosaveTimer_Tick) { IsEnabled = true };

        KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);

        VersionText.Text = AppVersion;
        MigrateOldSettings();
        ApplySettings();
        ToggleTypeRadio(false);
        ToggleInsertButton();
        SetSelectionInfo();
        SetQuickSettings();

        Dispatcher.UIThread.Post(async () => await CheckAutosaves(), DispatcherPriority.Background);
    }

    public bool CanShutdown;
    public const string AppVersion = "v4.0.1";
    public const string ServerVersion = "1.0.1";
    private const string ConfigPath = "UserConfig.toml";
    private const string TimeTrackerPath = "TimeTracker";

    public UserConfig UserConfig = new();
    public readonly KeybindEditor KeybindEditor;
    public readonly ChartEditor ChartEditor;
    public readonly AudioManager AudioManager;
    public readonly RenderEngine RenderEngine;

    public readonly PeerManager PeerManager;
    public readonly PeerBroadcaster PeerBroadcaster;
    public readonly ConnectionManager ConnectionManager;

    private TimeSpan updateInterval;
    public readonly Timer UpdateTimer;
    public readonly DispatcherTimer HitsoundTimer;
    public readonly DispatcherTimer AutosaveTimer;

    private long usageTime;
    private long sessionTime;
    private Timer usageTimer;

    public TimeUpdateSource UpdateSource = TimeUpdateSource.None;
    public enum TimeUpdateSource
    {
        None,
        Slider,
        Numeric,
        Timer,
        MeasureDecimal,
    }

    private PointerState pointerState = PointerState.Released;
    private enum PointerState
    {
        Released,
        Pressed,
    }

    private PlayerState playerState = PlayerState.Paused;
    public enum PlayerState
    {
        Paused,
        Playing,
        Preview,
    }

    public UiLockState LockState = UiLockState.Empty;
    public enum UiLockState
    {
        Empty,
        Loaded,
    }

    // ________________ Setup & UI Updates

    private void LoadUserConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, Toml.FromModel(UserConfig));
            return;
        }

        try
        {
            UserConfig = Toml.ToModel<UserConfig>(File.ReadAllText(ConfigPath));
        }
        catch (Exception e)
        {
            File.Delete($"{ConfigPath}.bak"); // Clear any old backup.
            File.Copy(ConfigPath, $"{ConfigPath}.bak"); // Copy original settings to back them up.
        }
    }

    private void LoadUsageTime()
    {
        if (!File.Exists(TimeTrackerPath)) return;

        try
        {
            usageTime = BitConverter.ToInt64(File.ReadAllBytes(TimeTrackerPath));
        }
        catch (Exception _)
        {
            // ignored, most likely a BitConverter exception because of a corrupt/invalid file.
        }
    }
    
    private void MigrateOldSettings()
    {
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorEndHold", out Keybind? k)) UserConfig.KeymapConfig.Keybinds["EditorEndNoteCollection"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorEditHold", out k)) UserConfig.KeymapConfig.Keybinds["EditorEditNoteCollection"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorBakeHold", out k)) UserConfig.KeymapConfig.Keybinds["EditorBakeNoteCollection"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorBakeHoldNoRender", out k)) UserConfig.KeymapConfig.Keybinds["EditorBakeNoteCollectionNoRender"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorStitchHold", out k)) UserConfig.KeymapConfig.Keybinds["EditorStitchNoteCollection"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorSplitHold", out k)) UserConfig.KeymapConfig.Keybinds["EditorSplitNoteCollection"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorInsertHoldSegtment", out k)) UserConfig.KeymapConfig.Keybinds["EditorInsertSegtment"] = k;
        if (UserConfig.KeymapConfig.Keybinds.TryGetValue("EditorSelectHoldReferences", out k)) UserConfig.KeymapConfig.Keybinds["EditorSelectNoteCollectionReferences"] = k;
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
        RadioNoteTrace.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteTrace"], 16));
        RadioNoteDamage.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteDamage"], 16));
        RadioNoteMaskAdd.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteMaskAdd"], 16));
        RadioNoteMaskRemove.BorderBrush = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorNoteMaskRemove"], 16));
    }

    private void SetTraceComboBoxItemColors()
    {
        TraceColorWhite.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceWhite"], 16));
        TraceColorBlack.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceBlack"], 16));
        TraceColorRed.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceRed"], 16));
        TraceColorOrange.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceOrange"], 16));
        TraceColorYellow.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceYellow"], 16));
        TraceColorLime.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceLime"], 16));
        TraceColorGreen.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceGreen"], 16));
        TraceColorSky.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceSky"], 16));
        TraceColorBlue.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceBlue"], 16));
        TraceColorViolet.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTraceViolet"], 16));
        TraceColorPink.Background = new SolidColorBrush(Convert.ToUInt32(UserConfig.ColorConfig.Colors["ColorTracePink"], 16));
    }
    
    private void ToggleTypeRadio(bool isMask)
    {
        if (BonusTypePanel == null || MaskDirectionPanel == null) return;

        BonusTypePanel.IsVisible = !isMask;
        MaskDirectionPanel.IsVisible = isMask;
    }

    private void ToggleBonusTypeRadios(bool bonus, bool rnote)
    {
        if (RadioNoBonus == null || RadioBonus == null || RadioRNote == null) return;

        RadioRNote.IsEnabled = rnote;

        if (!rnote && RadioRNote.IsChecked == true)
        {
            RadioBonus.IsChecked = true;
        }

        RadioBonus.IsEnabled = bonus;

        if (!bonus && RadioBonus.IsChecked == true)
        {
            RadioNoBonus.IsChecked = true;
        }
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
        MenuItemSelectAll.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorSelectAll"].ToGesture();
        MenuItemDeselectAll.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorDeselectAll"].ToGesture();
        MenuItemCheckerDeselect.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorCheckerDeselect"].ToGesture();
        MenuItemBoxSelect.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorBoxSelect"].ToGesture();
        MenuItemSelectHighlightedNote.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorSelectHighlightedNote"].ToGesture();
        MenuItemSelectNoteCollectionReferences.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorSelectNoteCollectionReferences"].ToGesture();
    }

    public void SetHoldContextButton(ChartEditorState state)
    {
        ButtonEditHold.IsVisible = state is not ChartEditorState.InsertHold;
        ButtonEndHold.IsVisible = state is ChartEditorState.InsertHold;

        ButtonEditHold.IsEnabled = state is ChartEditorState.InsertNote or ChartEditorState.InsertHold;
        ButtonEndHold.IsEnabled = state is ChartEditorState.InsertNote or ChartEditorState.InsertHold;
    }

    public void SetSongPositionSliderValues()
    {
        SliderSongPosition.Value = 0;
        SliderSongPosition.IsEnabled = true;

        if (AudioManager.CurrentSong == null) return;
        SliderSongPosition.Maximum = AudioManager.CurrentSong.Length;
    }

    public void UpdateTimer_Tick(object? sender)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (AudioManager.CurrentSong == null) return;

            if (AudioManager.Loop && AudioManager.CurrentSong.Position >= AudioManager.LoopEnd && AudioManager.LoopStart < AudioManager.LoopEnd)
            {
                AudioManager.CurrentSong.Position = AudioManager.LoopStart;

                float measure = ChartEditor.Chart.Timestamp2MeasureDecimal(AudioManager.CurrentSong.Position);
                
                AudioManager.HitsoundNoteIndex = ChartEditor.Chart.Notes.FindIndex(x => x.BeatData.MeasureDecimal >= measure);
                
                Gimmick? timeSig = ChartEditor.Chart.Gimmicks.LastOrDefault(x => x.GimmickType == GimmickType.TimeSigChange && x.BeatData.MeasureDecimal < measure);
                if (timeSig != null)
                {
                    int clicks = timeSig.TimeSig.Upper;
                    AudioManager.MetronomeTime = float.Floor(measure * clicks) / clicks;
                }
                else
                {
                    AudioManager.MetronomeTime = 0;
                }
            }

            if (AudioManager.CurrentSong.Position >= AudioManager.CurrentSong.Length && AudioManager.CurrentSong.IsPlaying)
            {
                SetPlayState(PlayerState.Paused);
                AudioManager.CurrentSong.Position = 0;
            }

            if (playerState is PlayerState.Preview && AudioManager.CurrentSong.Position >= (int)((ChartEditor.Chart.PreviewStart + ChartEditor.Chart.PreviewTime) * 1000))
            {
                SetPlayState(PlayerState.Paused);
                AudioManager.CurrentSong.Position = (uint)(ChartEditor.Chart.PreviewTime * 1000);
            }

            if (UpdateSource == TimeUpdateSource.None)
                UpdateTime(TimeUpdateSource.Timer);
        });
    }

    public void HitsoundTimer_Tick(object? sender, EventArgs e)
    {
        if (AudioManager.CurrentSong == null) return;
        if (playerState is PlayerState.Preview && UserConfig.AudioConfig.MuteHitsoundsOnPreview) return;

        float measure = ChartEditor.Chart.Timestamp2MeasureDecimal(AudioManager.CurrentSong.Position + BassSoundEngine.GetLatency() + UserConfig.AudioConfig.HitsoundOffset);

        playHitsounds();

        if (UserConfig.AudioConfig.ConstantMetronome || (UserConfig.AudioConfig.StartMetronome && measure < 1))
        {
            playMetronome();
        }
        
        return;

        void playHitsounds()
        {
            if (AudioManager.HitsoundNoteIndex == -1) return;
            
            while (AudioManager.HitsoundNoteIndex < ChartEditor.Chart.Notes.Count && ChartEditor.Chart.Notes[AudioManager.HitsoundNoteIndex].BeatData.MeasureDecimal <= measure)
            {
                AudioManager.PlayHitsound(ChartEditor.Chart.Notes[AudioManager.HitsoundNoteIndex]);
            }
        }

        void playMetronome()
        {
            if (AudioManager.MetronomeTime < 0) return;

            Gimmick? timeSig = ChartEditor.Chart.Gimmicks.LastOrDefault(x => x.GimmickType == GimmickType.TimeSigChange && x.BeatData.MeasureDecimal < measure);
            if (timeSig == null) return;
            
            int clicks = timeSig.TimeSig.Upper;
            float nextClick = float.Ceiling(measure * clicks) / clicks;

            if (AudioManager.MetronomeTime < nextClick)
            {
                bool isWhole = AudioManager.MetronomeTime % 1 < float.Epsilon * 100;
                bool isStart = measure < 1 && UserConfig.AudioConfig.StartMetronome;

                AudioManager.PlayMetronome(isStart, isWhole);
                AudioManager.MetronomeTime = nextClick;
            }
        }
    }

    public void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        if (ChartEditor.Chart.IsSaved) return;

        string filepath = Path.GetTempFileName().Replace(".tmp", ".autosave.mer");
        FormatHandler.WriteFile(ChartEditor.Chart, filepath, ChartFormatType.Saturn);
    }

    private void UsageTimer_Tick(object? sender)
    {
        usageTime++;
        sessionTime++;

        Dispatcher.UIThread.Post(() =>
        {
            TimeSpan u = TimeSpan.FromSeconds(usageTime);
            UsageTimeText.Text = $"{Assets.Lang.Resources.Menu_UsageTime} {(int)u.TotalHours}:{u.Minutes}:{u.Seconds}";
            
            TimeSpan s = TimeSpan.FromSeconds(sessionTime);
            SessionTimeText.Text = $"{Assets.Lang.Resources.Menu_SessionTime} {(int)s.TotalHours}:{s.Minutes}:{s.Seconds}";
        });
        
        try
        {
            File.WriteAllBytes(TimeTrackerPath, BitConverter.GetBytes(usageTime));
        }
        catch (Exception _)
        {
            // ignored, most likely an exception caused by two instances running at the same time.
        }
    }
    
    private async Task CheckAutosaves()
    {
        string[] autosaves = Directory.GetFiles(Path.GetTempPath(), "*.autosave.mer").OrderByDescending(File.GetCreationTime).ToArray();
        if (autosaves.Length == 0) return;

        ContentDialogResult result = await showSelectAudioPrompt(File.GetCreationTime(autosaves[0]).ToString("yyyy-MM-dd HH:mm:ss"));
        if (result != ContentDialogResult.Primary) return;

        OpenChart(autosaves[0]);
        ChartEditor.Chart.IsSaved = false; // Force saved prompt
        ChartEditor.Chart.IsNew = true; // Set new to force a new save.
        ChartEditor.Chart.Filepath = ""; // Clear path to not overwrite temp file.

        ClearAutosaves();
        return;

        Task<ContentDialogResult> showSelectAudioPrompt(string timestamp)
        {
            ContentDialog dialog = new()
            {
                Title = $"{Assets.Lang.Resources.Generic_AutosaveFound} {timestamp}",
                Content = Assets.Lang.Resources.Generic_AutosavePrompt,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                CloseButtonText = Assets.Lang.Resources.Generic_No,
            };

            return dialog.ShowAsync();
        }
    }

    private static void ClearAutosaves()
    {
        string[] autosaves = Directory.GetFiles(Path.GetTempPath(), "*.autosave.mer");
        foreach (string file in autosaves) File.Delete(file);
    }

    private static void ClearMultiChartingAudio()
    {
        string[] audioFiles = Directory.GetFiles(Path.GetTempPath(), "*.mmm.audio");
        foreach (string file in audioFiles) File.Delete(file);
    }

    /// <summary>
    /// (Don't confuse this with UpdateTimer_Tick please)
    /// Updates the current time/measureDecimal the user is looking at.
    /// Requires a persistent "source" to track, otherwise the
    /// OnValueChanged events firing at each other causes an exponential
    /// chain reaction that turns your computer into a nuclear reactor.
    /// </summary>
    public void UpdateTime(TimeUpdateSource source)
    {
        if (AudioManager.CurrentSong == null) return;
        UpdateSource = source;

        // Update Audio Position
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

            if (source is TimeUpdateSource.MeasureDecimal)
            {
                AudioManager.CurrentSong.Position = (uint)ChartEditor.Chart.MeasureDecimal2Timestamp(ChartEditor.CurrentMeasureDecimal);
            }
        }

        // Avoid imprecision from Timestamp2BeatData when paused.
        // This was causing a lot of off-by-one errors on BeatData.Tick values. >:[
        BeatData data = UpdateSource is TimeUpdateSource.Timer or TimeUpdateSource.Slider or TimeUpdateSource.MeasureDecimal || NumericMeasure.Value is null || NumericBeatValue.Value is null || NumericBeatDivisor.Value is null
            ? ChartEditor.Chart.Timestamp2BeatData(AudioManager.CurrentSong.Position)
            : new((int)NumericMeasure.Value, (int)(NumericBeatValue.Value / NumericBeatDivisor.Value * 1920));

        // Update Numeric
        if (source is not TimeUpdateSource.Numeric && NumericBeatDivisor.Value != null)
        {
            // The + 0.002f is a hacky "fix". There's some weird rounding issue that has carried over from BAKKA,
            // most likely caused by ManagedBass or AvaloniaUI jank. If you increment NumericBeatValue up,
            // it's often not quite enough, and it falls back to the value it was before.
            NumericMeasure.Value = data.Measure;
            NumericBeatValue.Value = (int)((data.MeasureDecimal - data.Measure + 0.002f) * (float)NumericBeatDivisor.Value);
        }

        // Update Slider
        if (source is not TimeUpdateSource.Slider)
        {
            SliderSongPosition.Value = (int)AudioManager.CurrentSong.Position;
        }

        ChartEditor.CurrentMeasureDecimal = data.MeasureDecimal;
        TimestampDetailed.Text = TimeSpan.FromMilliseconds(AudioManager.CurrentSong.Position).ToString(@"hh\:mm\:ss\.fff");
        TimestampSeconds.Text = $"{AudioManager.CurrentSong.Position * 0.001f:F3}";
        ToggleInsertButton();
        UpdateCurrentTimeScaleInfo();

        UpdateSource = TimeUpdateSource.None;
    }

    public void ToggleInsertButton()
    {
        bool behindEndOfChart = ChartEditor.Chart.EndOfChart != null && ChartEditor.CurrentBeatData.FullTick >= ChartEditor.Chart.EndOfChart.BeatData.FullTick;
        bool beforeHoldStart = ChartEditor is { CurrentCollectionStart: not null, EditorState: ChartEditorState.InsertHold } && ChartEditor.CurrentMeasureDecimal <= ChartEditor.CurrentCollectionStart.BeatData.MeasureDecimal;
        bool beforeLastHold = ChartEditor is { LastPlacedNote: not null, EditorState: ChartEditorState.InsertHold } && ChartEditor.CurrentMeasureDecimal <= ChartEditor.LastPlacedNote.BeatData.MeasureDecimal;

        ButtonInsert.IsEnabled = !(behindEndOfChart || beforeHoldStart || beforeLastHold);
    }

    public void SetSelectionInfo()
    {
        SelectionInfoSelectedNotesValue.Text = ChartEditor.SelectedNotes.Count.ToString();
        SelectionCountText.Text = $"{Assets.Lang.Resources.Editor_SelectionInfo_SelectedNotes}: {ChartEditor.SelectedNotes.Count}";
        SelectionCountText.IsVisible = ChartEditor.SelectedNotes.Count != 0;

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
            SelectionInfoNoteTypeValue.Text = Enums2String.NoteType2String(note.NoteType, note.BonusType, note.LinkType);
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
        ChartInfoChartFilepath.Text = Path.GetFileName(ChartEditor.Chart.Filepath);

        ChartInfoVersion.Text = ChartEditor.Chart.Version;
        ChartInfoTitle.Text = ChartEditor.Chart.Title;
        ChartInfoRubi.Text = ChartEditor.Chart.Rubi;
        ChartInfoArtist.Text = ChartEditor.Chart.Artist;
        ChartInfoAuthor.Text = ChartEditor.Chart.Author;

        ChartInfoDiff.SelectedIndex = ChartEditor.Chart.Diff;
        ChartInfoLevel.Value = (double)ChartEditor.Chart.Level;
        ChartInfoClearThreshold.Value = (double)ChartEditor.Chart.ClearThreshold;
        ChartInfoBpmText.Text = ChartEditor.Chart.BpmText;

        ChartInfoPreviewStart.Value = (double)ChartEditor.Chart.PreviewStart;
        ChartInfoPreviewTime.Value = (double)ChartEditor.Chart.PreviewTime;

        ChartInfoBgmFilepath.Text = Path.GetFileName(ChartEditor.Chart.BgmFilepath);
        ChartInfoBgmOffset.Value = (double)ChartEditor.Chart.BgmOffset;
        ChartInfoBgaFilepath.Text = ChartEditor.Chart.BgaFilepath == "" ? "" : Path.GetFileName(ChartEditor.Chart.BgaFilepath);
        ChartInfoBgaOffset.Value = (double)ChartEditor.Chart.BgaOffset;
        ChartInfoJacketFilepath.Text = ChartEditor.Chart.JacketFilepath == "" ? "" : Path.GetFileName(ChartEditor.Chart.JacketFilepath);

        if (File.Exists(ChartEditor.Chart.JacketFilepath))
        {
            ChartInfoJacket.IsVisible = true;
            ChartInfoJacketFallback.IsVisible = false;

            ChartInfoJacket.Source = new Bitmap(ChartEditor.Chart.JacketFilepath);
        }
        else
        {
            ChartInfoJacket.IsVisible = false;
            ChartInfoJacketFallback.IsVisible = true;
        }
    }

    public void SetChartNoteInfo()
    {
        ChartInfoNoteCountText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.LinkType is not (NoteLinkType.Point or NoteLinkType.End) && x.IsNote)}";
        ChartInfoRNoteCountText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.BonusType is BonusType.RNote)}";

        ChartInfoNoteCountTouchText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.NoteType is NoteType.Touch)}";
        ChartInfoNoteCountChainText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.NoteType is NoteType.Chain)}";
        ChartInfoNoteCountHoldText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.NoteType is NoteType.Hold && x.LinkType is not (NoteLinkType.Point or NoteLinkType.End))}";
        
        ChartInfoNoteCountSlideText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.IsSlide)}";
        ChartInfoNoteCountSnapText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.IsSnap)}";
        
        ChartInfoNoteCountDamageText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.NoteType is NoteType.Damage)}";
        
        ChartInfoNoteCountMaskText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.IsMask)}";
        ChartInfoNoteCountTraceText.Text = $"{ChartEditor.Chart.Notes.Count(x => x.NoteType is NoteType.Trace && x.LinkType is not (NoteLinkType.Point or NoteLinkType.End))}";
    }
    
    public void SetQuickSettings()
    {
        QuickSettingsSliderHitsound.Value = UserConfig.AudioConfig.HitsoundVolume;
        QuickSettingsSliderMusic.Value = UserConfig.AudioConfig.MusicVolume;
        QuickSettingsCheckBoxStartMetronome.IsChecked = UserConfig.AudioConfig.StartMetronome;
        QuickSettingsCheckBoxConstantMetronome.IsChecked = UserConfig.AudioConfig.ConstantMetronome;
        QuickSettingsNumericBeatDivision.Value = UserConfig.RenderConfig.BeatDivision;
        QuickSettingsNumericNoteSpeed.Value = (decimal?)UserConfig.RenderConfig.NoteSpeed;
        QuickSettingsCheckBoxShowHiSpeed.IsChecked = UserConfig.RenderConfig.ShowHiSpeed;
        QuickSettingsCheckBoxDrawNoRenderHoldSegments.IsChecked = UserConfig.RenderConfig.DrawNoRenderSegments;
        QuickSettingsCheckBoxShowJudgementWindowMarvelous.IsChecked = UserConfig.RenderConfig.ShowJudgementWindowMarvelous;
        QuickSettingsCheckBoxShowJudgementWindowGreat.IsChecked = UserConfig.RenderConfig.ShowJudgementWindowGreat;
        QuickSettingsCheckBoxShowJudgementWindowGood.IsChecked = UserConfig.RenderConfig.ShowJudgementWindowGood;
        QuickSettingsCheckBoxCutEarlyJudgementWindowOnHolds.IsChecked = UserConfig.RenderConfig.CutEarlyJudgementWindowOnHolds;
        QuickSettingsCheckBoxCutOverlappingJudgementWindows.IsChecked = UserConfig.RenderConfig.CutOverlappingJudgementWindows;
        QuickSettingsHideNotesOnDifferentLayers.IsChecked = UserConfig.RenderConfig.HideNotesOnDifferentLayers;
    }

    public void SetNoteSizeBounds(NoteType noteType, BonusType bonusType, NoteLinkType linkType)
    {
        int minimum = Note.MinSize(noteType, bonusType, linkType);
        int maximum = Note.MaxSize(noteType);
        SliderNoteSize.Minimum = minimum;
        NumericNoteSize.Minimum = minimum;
        ChartEditor.Cursor.MinSize = minimum;

        SliderNoteSize.Maximum = maximum;
        NumericNoteSize.Maximum = maximum;
        ChartEditor.Cursor.MaxSize = maximum;

        SliderNoteSize.Value = double.Clamp(SliderNoteSize.Value, minimum, maximum);
    }

    public void UpdateFilepathsInUi()
    {
        ChartInfoChartFilepath.Text = Path.GetFileName(ChartEditor.Chart.Filepath);
        ChartInfoBgmFilepath.Text = Path.GetFileName(ChartEditor.Chart.BgmFilepath);
        ChartInfoBgaFilepath.Text = Path.GetFileName(ChartEditor.Chart.BgaFilepath);
        ChartInfoJacketFilepath.Text = Path.GetFileName(ChartEditor.Chart.JacketFilepath);
    }

    private void UpdateLoopMarkerPosition()
    {
        if (AudioManager.CurrentSong is null) return;

        double start = AudioManager.LoopStart * (SliderSongPosition.Bounds.Width - 25) / AudioManager.CurrentSong.Length + 12.5;
        double end = AudioManager.LoopEnd * (SliderSongPosition.Bounds.Width - 25) / AudioManager.CurrentSong.Length + 12.5;

        LoopMarkerStart.Margin = new(start, 0, 0, 0);
        LoopMarkerEnd.Margin = new(end, 0, 0, 0);
    }

    public void ResetLoopMarkers(uint length)
    {
        AudioManager.LoopStart = 0;
        AudioManager.LoopEnd = length;

        LoopMarkerStart.Margin = new(12.5, 0, 0, 0);
        LoopMarkerEnd.Margin = new(SliderSongPosition.Bounds.Width - 12.5, 0, 0, 0);
    }

    private void SetLoopMarkerStart()
    {
        if (AudioManager.CurrentSong is null) return;

        AudioManager.LoopStart = AudioManager.CurrentSong.Position;
        LoopMarkerStart.Margin = new(AudioManager.LoopStart * (SliderSongPosition.Bounds.Width - 25) / AudioManager.CurrentSong.Length + 12.5, 0, 0, 0);
    }

    private void SetLoopMarkerEnd()
    {
        if (AudioManager.CurrentSong is null) return;

        AudioManager.LoopEnd = AudioManager.CurrentSong.Position;
        LoopMarkerEnd.Margin = new(AudioManager.LoopEnd * (SliderSongPosition.Bounds.Width - 25) / AudioManager.CurrentSong.Length + 12.5, 0, 0, 0);
    }

    public void SetUiLockState(UiLockState lockState)
    {
        switch (lockState)
        {
            case UiLockState.Loaded:
                {
                    UiLock.IsVisible = false;

                    if (ConnectionManager.NetworkState == ConnectionManager.NetworkConnectionState.Local)
                    {
                        MenuItemCreateSession.IsEnabled = true;
                    }

                    MenuItemEdit.IsEnabled = true;
                    MenuItemSelect.IsEnabled = true;
                    MenuItemTools.IsEnabled = true;
                    break;
                }

            case UiLockState.Empty:
                {
                    UiLock.IsVisible = true;
                    SetPlayState(PlayerState.Paused);

                    MenuItemEdit.IsEnabled = false;
                    MenuItemSelect.IsEnabled = false;
                    MenuItemTools.IsEnabled = false;
                    break;
                }
        }

        LockState = lockState;
    }

    public void UpdateCurrentTimeScaleInfo()
    {
        TimeSig? timeSig = ChartEditor.Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick <= ChartEditor.CurrentBeatData.FullTick && x.GimmickType is GimmickType.TimeSigChange)?.TimeSig;
        float bpm = ChartEditor.Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick <= ChartEditor.CurrentBeatData.FullTick && x.GimmickType is GimmickType.BpmChange)?.Bpm ?? 0;
        float hiSpeed = ChartEditor.Chart.Gimmicks.LastOrDefault(x => x.BeatData.FullTick <= ChartEditor.CurrentBeatData.FullTick && x.GimmickType is GimmickType.HiSpeedChange)?.HiSpeed ?? 1;

        if (timeSig == null) return;

        TextCurrentHiSpeed.Text = $"[ {hiSpeed,3:F} ]";
        TextCurrentVisualSpeed.Text = $"[ {hiSpeed * UserConfig.RenderConfig.NoteSpeed,3:F} ]";
        
        TextCurrentBpm.Text = $"[ {bpm,3:F} ]";
        TextCurrentTimeSig.Text = $"[ {timeSig.Upper} / {timeSig.Lower} ]";
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

        if (e.Key is Key.Escape)
        {
            switch (ChartEditor.EditorState)
            {
                case ChartEditorState.InsertNote:
                    {
                        break;
                    }

                case ChartEditorState.InsertHold:
                    {
                        ChartEditor.EndHold();
                        break;
                    }

                case ChartEditorState.BoxSelectStart:
                case ChartEditorState.BoxSelectEnd:
                    {
                        ChartEditor.StopBoxSelect();
                        break;
                    }
            }

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

        if (LockState == UiLockState.Empty) return;

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

            ChartEditor.InsertNote();
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorCheckerDeselect"]))
        {
            ChartEditor.CheckerDeselect();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEndNoteCollection"]))
        {
            ChartEditor.EndHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditNoteCollection"]))
        {
            ChartEditor.EditHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBakeNoteCollection"]))
        {
            ChartEditor.BakeHold((MathExtensions.HoldEaseType)HoldEaseComboBox.SelectedIndex, false);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBakeNoteCollectionNoRender"]))
        {
            ChartEditor.BakeHold((MathExtensions.HoldEaseType)HoldEaseComboBox.SelectedIndex, true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorStitchNoteCollection"]))
        {
            ChartEditor.StitchHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSplitNoteCollection"]))
        {
            ChartEditor.SplitHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDeleteSegments"]))
        {
            ChartEditor.DeleteSegments();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorInsertSegment"]))
        {
            ChartEditor.InsertHoldSegment();
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSelectNoteCollectionReferences"]))
        {
            ChartEditor.SelectNoteCollectionReferences();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBoxSelect"]))
        {
            ChartEditor.RunBoxSelect(0, 0);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorConvertToInstantMask"]))
        {
            ChartEditor.ConvertToInstantMask();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorFlipNoteDirection"]))
        {
            ChartEditor.FlipNoteDirection();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorPaintTraces"]))
        {
            ChartEditor.PaintTraces((TraceColor)TraceColorComboBox.SelectedIndex);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSetScrollLayer"]))
        {
            ChartEditor.SetScrollLayer((ScrollLayer)ScrollLayerComboBox.SelectedIndex);
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorAddComment"]))
        {
            AddComment();
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeTrace"]))
        {
            RadioNoteTrace.IsChecked = true;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorNoteTypeDamage"]))
        {
            RadioNoteDamage.IsChecked = true;
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorReverseNote"]))
        {
            ChartEditor.ReverseSelection();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorDelete"]))
        {
            ChartEditor.DeleteSelection();
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditIncreaseSizeIterative"]))
        {
            ChartEditor.QuickEditSizeIterative(1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditDecreaseSizeIterative"]))
        {
            ChartEditor.QuickEditSizeIterative(-1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditIncreasePositionIterative"]))
        {
            ChartEditor.QuickEditPositionIterative(1);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorQuickEditDecreasePositionIterative"]))
        {
            ChartEditor.QuickEditPositionIterative(-1);
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSetRenderTrue"]))
        {
            ChartEditor.SetSelectionRenderFlag(true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSetRenderFalse"]))
        {
            ChartEditor.SetSelectionRenderFlag(false);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorToggleLoop"]))
        {
            ToggleButtonLoop.IsChecked = !ToggleButtonLoop.IsChecked;
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSetLoopStart"]))
        {
            SetLoopMarkerStart();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSetLoopEnd"]))
        {
            SetLoopMarkerEnd();
            e.Handled = true;
            return;
        }

        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorJumpMeasureUp"]))
        {
            // I know some gremlin would try this.
            if (NumericMeasure.Value >= NumericMeasure.Maximum && NumericBeatValue.Value >= NumericBeatDivisor.Value - 1) return;
            
            if (AudioManager.CurrentSong == null || AudioManager.CurrentSong.IsPlaying) return;
            NumericMeasure.Value++;
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorJumpMeasureDown"]))
        {
            if (AudioManager.CurrentSong == null || AudioManager.CurrentSong.IsPlaying) return;
            NumericMeasure.Value--;
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorJumpBeatUp"]))
        {
            // I know some gremlin would try this.
            if (NumericMeasure.Value >= NumericMeasure.Maximum && NumericBeatValue.Value >= NumericBeatDivisor.Value - 1) return;
            
            if (AudioManager.CurrentSong == null || AudioManager.CurrentSong.IsPlaying) return;
            NumericBeatValue.Value++;
            
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorJumpBeatDown"]))
        {
            if (AudioManager.CurrentSong == null || AudioManager.CurrentSong.IsPlaying) return;
            NumericBeatValue.Value--;
            
            e.Handled = true;
            return;
        }
        
        
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["RenderIncreaseNoteSpeed"]))
        {
            UserConfig.RenderConfig.NoteSpeed = double.Min(UserConfig.RenderConfig.NoteSpeed + 0.1, 6);
            File.WriteAllText(ConfigPath, Toml.FromModel(UserConfig));
            RenderEngine.UpdateVisibleTime();
            SetQuickSettings();

            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["RenderDecreaseNoteSpeed"]))
        {
            UserConfig.RenderConfig.NoteSpeed = double.Max((UserConfig.RenderConfig.NoteSpeed - 0.1), 1);
            File.WriteAllText(ConfigPath, Toml.FromModel(UserConfig));
            RenderEngine.UpdateVisibleTime();
            SetQuickSettings();

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
        int delta = double.Sign(e.Delta.Y);
        PointerPoint p = e.GetCurrentPoint(Canvas);

        if (p.Properties.IsRightButtonPressed)
        {
            ChartEditor.Cursor.IncrementSize(delta);
            NumericNoteSize.Value = ChartEditor.Cursor.Size;
            SliderNoteSize.Value = ChartEditor.Cursor.Size;
            return;
        }

        // Shift Beat Divisor
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            decimal value = NumericBeatDivisor.Value ?? 16;
            NumericBeatDivisor.Value = Math.Clamp(value + delta, NumericBeatDivisor.Minimum, NumericBeatDivisor.Maximum);
        }

        // Double/Halve Beat Divisor
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            decimal value = NumericBeatDivisor.Value ?? 16;

            if (delta > 0)
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
            if (NumericMeasure.Value >= NumericMeasure.Maximum && NumericBeatValue.Value >= NumericBeatDivisor.Value - 1 && delta > 0) return;

            NumericBeatValue.Value += delta;
        }
    }

    private void Canvas_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (ChartEditor.EditorState is ChartEditorState.BoxSelectEnd)
        {
            PointerPoint pointer = e.GetCurrentPoint(Canvas);
            SKPoint point = new((float)pointer.Position.X, (float)pointer.Position.Y);
            point.X -= (float)Canvas.Width * 0.5f;
            point.Y -= (float)Canvas.Height * 0.5f;
            point.X /= (float)(Canvas.Width - 30) * 0.5f;
            point.Y /= (float)(Canvas.Height - 30) * 0.5f;
            point.X /= 0.9f;
            point.Y /= 0.9f;

            RenderEngine.PointerPosition = point;
            return;
        }

        PointerPoint p = e.GetCurrentPoint(Canvas);

        if (pointerState is not PointerState.Pressed) return;

        if (p.Properties is { IsLeftButtonPressed: true, IsRightButtonPressed: false })
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                OnLeftClick(p, e.KeyModifiers, true);
                return;
            }

            float x = (float)(p.Position.X - Canvas.Width * 0.5);
            float y = (float)(p.Position.Y - Canvas.Height * 0.5);
            int theta = MathExtensions.GetThetaNotePosition(x, y);

            ChartEditor.Cursor.Drag(theta);
            NumericNotePosition.Value = ChartEditor.Cursor.Position;
            NumericNoteSize.Value = ChartEditor.Cursor.Size;
        }
    }

    private void Canvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint p = e.GetCurrentPoint(Canvas);

        if (p.Properties.IsLeftButtonPressed)
        {
            OnLeftClick(p, e.KeyModifiers, false);
        }
    }
    
    private void Canvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        pointerState = PointerState.Released;
        ChartEditor.LastSelectedNote = null;

        if (ChartEditor.EditorState is ChartEditorState.BoxSelectStart or ChartEditorState.BoxSelectEnd)
        {
            PointerPoint p = e.GetCurrentPoint(Canvas);

            if (p.Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonReleased) return;

            SKPoint point = new((float)p.Position.X, (float)p.Position.Y);
            point.X -= (float)Canvas.Width * 0.5f;
            point.Y -= (float)Canvas.Height * 0.5f;
            point.X /= (float)(Canvas.Width - 30) * 0.5f;
            point.Y /= (float)(Canvas.Height - 30) * 0.5f;
            point.X /= 0.9f;
            point.Y /= 0.9f;

            ChartEditor.RunBoxSelect(point.Length, RenderEngine.GetMeasureDecimalAtPointer(point));
        }
    }

    private void OnLeftClick(PointerPoint p, KeyModifiers modifiers, bool pointerMoved)
    {
        SKPoint point = new((float)p.Position.X, (float)p.Position.Y);
        point.X -= (float)Canvas.Width * 0.5f;
        point.Y -= (float)Canvas.Height * 0.5f;
        point.X /= (float)(Canvas.Width - 30) * 0.5f;
        point.Y /= (float)(Canvas.Height - 30) * 0.5f;
        point.X /= 0.9f;
        point.Y /= 0.9f;

        pointerState = PointerState.Pressed;

        if (ChartEditor.EditorState is ChartEditorState.BoxSelectEnd) return;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            // Selecting
            if ((Note?)RenderEngine.GetChartElementAtPointer(ChartEditor.Chart, point, false, ChartEditor.LayerNoteActive, ChartEditor.LayerMaskActive, ChartEditor.LayerGimmickActive, ChartEditor.LayerTraceActive) is not { } note) return;

            if (pointerMoved && note == ChartEditor.LastSelectedNote) return;

            ChartEditor.SelectNote(note);
            ChartEditor.LastSelectedNote = note;
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            // Highlighting
            if (RenderEngine.GetChartElementAtPointer(ChartEditor.Chart, point, true, ChartEditor.LayerNoteActive, ChartEditor.LayerMaskActive, ChartEditor.LayerGimmickActive, ChartEditor.LayerTraceActive) is not { } note) return;

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

        UpdateLoopMarkerPosition();
        PeerManager.UpdatePeerMarkers();
        ChartEditor.UpdateCommentMarkers();
    }

    public async void DragDrop(string path)
    {
        if (!await PromptSave()) return;
        OpenChart(path);
    }

    private async void MenuItemNew_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!await PromptSave()) return;

        NewChartView newChartView = new(this);
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Editor_NewChart,
            Content = newChartView,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();

            if (result is ContentDialogResult.Primary)
            {
                string filepath = newChartView.MusicFilePath;
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

                ChartEditor.NewChart(filepath, bpm, timeSigUpper, timeSigLower);
                AudioManager.SetSong(filepath, (float)UserConfig.AudioConfig.MusicVolume * 0.01f, (int)SliderPlaybackSpeed.Value);
                SetSongPositionSliderValues();
                UpdateFilepathsInUi();
                RenderEngine.UpdateVisibleTime();
                ClearAutosaves();
                ResetLoopMarkers(AudioManager.CurrentSong?.Length ?? 0);
                SetUiLockState(UiLockState.Loaded);
                UpdateCurrentTimeScaleInfo();
            }
        });
    }

    private async void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!await PromptSave()) return;

        SetUiLockState(UiLockState.Empty);

        // Get .mer file
        IStorageFile? file = await OpenChartFilePicker();
        if (file == null) return;

        AudioManager.ResetSong();
        OpenChart(file.Path.LocalPath);
    }

    private async void MenuItemSave_OnClick(object? sender, RoutedEventArgs e)
    {
        if (LockState == UiLockState.Empty) return;
        await SaveFile(ChartEditor.Chart.IsNew, ChartEditor.Chart.Filepath);
    }

    private async void MenuItemSaveAs_OnClick(object? sender, RoutedEventArgs e)
    {
        if (LockState == UiLockState.Empty) return;
        await SaveFile(true, ChartEditor.Chart.Filepath);
    }

    private async void MenuItemExportMercury_OnClick(object? sender, RoutedEventArgs e)
    {
        if (LockState == UiLockState.Empty) return;

        await ExportFile(ChartFormatType.Mercury);
    }

    private async void MenuItemExportSaturn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (LockState == UiLockState.Empty) return;
        if (ChartEditor.Chart.EndOfChart == null)
        {
            ShowWarningMessage(Assets.Lang.Resources.Generic_EndOfChartWarning, Assets.Lang.Resources.Generic_EndOfChartWarningExplanation);
            return;
        }

        await ExportFile(ChartFormatType.Saturn);
    }

    private void MenuItemExportSaturnFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        SaturnFolderExportView exportView = new(this);

        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.SaturnExport_WindowTitle,
            Content = exportView,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Export,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            Resources =
            {
                ["ContentDialogMaxWidth"] = 730.0,
            },
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();

            if (result is ContentDialogResult.Primary)
            {
                if (exportView.FolderPathTextBox.Text is null) return;
                string folderPath = exportView.FolderPathTextBox.Text;

                // Write metadata
                Stream stream = File.OpenWrite(Path.Combine(folderPath, "meta.mer"));
                stream.SetLength(0);

                await using StreamWriter streamWriter = new(stream, new UTF8Encoding(false));
                streamWriter.NewLine = "\n";

                await streamWriter.WriteLineAsync($"#TITLE {exportView.TitleTextBox.Text}");
                await streamWriter.WriteLineAsync($"#RUBI_TITLE {exportView.RubiTextBox.Text}");
                await streamWriter.WriteLineAsync($"#ARTIST {exportView.ArtistTextBox.Text}");
                await streamWriter.WriteLineAsync($"#GENRE {exportView.GenreTextBox.Text}");
                await streamWriter.WriteLineAsync($"#BPM {exportView.BpmTextBox.Text}");

                try
                {
                    if (File.Exists(exportView.NormalPathTextBox.Text)) File.Copy(exportView.NormalPathTextBox.Text, Path.Combine(folderPath, "0.mer"), true);
                    if (File.Exists(exportView.HardPathTextBox.Text)) File.Copy(exportView.HardPathTextBox.Text, Path.Combine(folderPath, "1.mer"), true);
                    if (File.Exists(exportView.ExpertPathTextBox.Text)) File.Copy(exportView.ExpertPathTextBox.Text, Path.Combine(folderPath, "2.mer"), true);
                    if (File.Exists(exportView.InfernoPathTextBox.Text)) File.Copy(exportView.InfernoPathTextBox.Text, Path.Combine(folderPath, "3.mer"), true);
                    if (File.Exists(exportView.BeyondPathTextBox.Text)) File.Copy(exportView.BeyondPathTextBox.Text, Path.Combine(folderPath, "4.mer"), true);

                    if (File.Exists(exportView.MusicTextBox.Text)) File.Copy(exportView.MusicTextBox.Text, Path.Combine(folderPath, Path.GetFileName(exportView.MusicTextBox.Text)), true);
                    if (File.Exists(exportView.JacketTextBox.Text)) File.Copy(exportView.JacketTextBox.Text, Path.Combine(folderPath, $"jacket{Path.GetExtension(exportView.JacketTextBox.Text)}"), true);
                }
                catch (Exception ignored)
                {
                    // ignored. most likely caused by: copying a file to itself => file already being used by another process.
                }
            }
        });
    }

    private void MenuItemCreateSession_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ConnectionManager.SessionCode != "")
        {
            ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionOpened} {ConnectionManager.SessionCode}");
            return;
        }

        OnlineView_CreateSession createSessionView = new();
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Menu_CreateSession,
            Content = createSessionView,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();

            if (result is ContentDialogResult.Primary)
            {
                // TODO: Validate user input.
                Color color = createSessionView.UserColor.Color;
                ConnectionManager.CreateSession(createSessionView.ServerAddressTextBox.Text ?? "", createSessionView.UsernameTextbox.Text ?? "", $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
            }
        });
    }

    private async void MenuItemJoinSession_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!await PromptSave()) return;

        OnlineView_JoinSession joinSessionView = new();
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Menu_JoinSession,
            Content = joinSessionView,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Join,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();

            if (result is ContentDialogResult.Primary)
            {
                // TODO: Validate user input.
                Color color = joinSessionView.UserColor.Color;
                ConnectionManager.JoinSession(joinSessionView.ServerAddressTextBox.Text ?? "", joinSessionView.UsernameTextbox.Text ?? "", $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}", joinSessionView.SessionCodeTextBox.Text ?? "");
            }
        });
    }

    private void MenuItemDisconnect_OnClick(object? sender, RoutedEventArgs e)
    {
        ConnectionManager.LeaveSession();
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
        bool save = await PromptSave();
        if (!save) return;

        ClearMultiChartingAudio();
        
        CanShutdown = true;
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void MenuItemUndo_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Undo();

    private void MenuItemRedo_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Redo();

    private void MenuItemCut_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Cut();

    private void MenuItemCopy_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Copy();

    private void MenuItemPaste_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Paste();

    private void MenuItemSelectAll_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.SelectAllNotes();

    private void MenuItemDeselectAll_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeselectAllNotes();

    private void MenuItemCheckerDeselect_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.CheckerDeselect();

    private void MenuItemSelectNoteCollectionReferences_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.SelectNoteCollectionReferences();

    private void MenuItemSelectHighlightedNote_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.SelectHighlightedNote();

    private async void MenuItemSelectSimilarByPosition_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.HighlightedElement is not Note)
        {
            ShowWarningMessage(Assets.Lang.Resources._Editor_NoNoteHighlighted);
            return;
        }
            
        SelectView_Threshold threshold = new();
        
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Editor_SelectSimilar,
            Content = threshold,
            PrimaryButtonText = Assets.Lang.Resources.MenuHeader_Select,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
        };
        
        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;
        
        if (threshold.CheckBoxFilterSelection.IsChecked ?? false) ChartEditor.FilterSelection(SelectSimilarType.Position, (int)threshold.SliderThreshold.Value, 0, 0);
        else ChartEditor.SelectSimilar(SelectSimilarType.Position, (int)threshold.SliderThreshold.Value, 0, 0);
    }
    
    private async void MenuItemSelectSimilarBySize_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.HighlightedElement is not Note)
        {
            ShowWarningMessage(Assets.Lang.Resources._Editor_NoNoteHighlighted);
            return;
        }
            
        SelectView_Threshold threshold = new();
        
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Editor_SelectSimilar,
            Content = threshold,
            PrimaryButtonText = Assets.Lang.Resources.MenuHeader_Select,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
        };
        
        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;
        
        if (threshold.CheckBoxFilterSelection.IsChecked ?? false) ChartEditor.FilterSelection(SelectSimilarType.Size, (int)threshold.SliderThreshold.Value, 0, 0);
        else ChartEditor.SelectSimilar(SelectSimilarType.Size, (int)threshold.SliderThreshold.Value, 0, 0);
    }
    
    private async void MenuItemSelectSimilarByType_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectView_Type type = new();
        
        ContentDialog dialog = new()
        {
            Title = Assets.Lang.Resources.Editor_SelectSimilar,
            Content = type,
            PrimaryButtonText = Assets.Lang.Resources.MenuHeader_Select,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
        };
        
        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;
        
        int noteType = type.ComboBoxNoteType.SelectedIndex switch
        {
            0 => -1,
            1 => 1,
            2 => 4,
            3 => 5,
            4 => 2,
            5 => 3,
            6 => 12,
            7 => 6,
            8 => 7,
            9 => 8,
            10 => 9,
            11 => 10,
            _ => -1,
        };
        
        int bonusType = type.ComboBoxBonusType.SelectedIndex switch
        {
            0 => -1,
            1 => 0,
            2 => 1,
            3 => 2,
            _ => -1,
        };
        
        if (type.CheckBoxFilterSelection.IsChecked ?? false) ChartEditor.FilterSelection(SelectSimilarType.Type, 0, noteType, bonusType);
        else ChartEditor.SelectSimilar(SelectSimilarType.Type, 0, noteType, bonusType);
    }
    
    private void MenuItemBoxSelect_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.EditorState is not ChartEditorState.InsertNote) return;
        ChartEditor.RunBoxSelect(0, 0);
    }

    private void MenuItemMirrorChart_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.MirrorChart((int?)NumericMirrorAxis.Value ?? 30);

    private void MenuItemShiftChart_OnClick(object? sender, RoutedEventArgs e)
    {
        ToolsView_ShiftChart shiftView = new();
        ContentDialog dialog = new()
        {
            Content = shiftView,
            Title = Assets.Lang.Resources.Menu_ShiftChart,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Ok,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;

            ChartEditor.ShiftChart(shiftView.Ticks);
        });
    }

    private async void MenuItemFixOffByOne_OnClick(object? sender, RoutedEventArgs e)
    {
        ContentDialogResult result = await showPrompt();
        if (result != ContentDialogResult.Primary) return;
        ChartEditor.FixOffByOneErrors();
        return;

        Task<ContentDialogResult> showPrompt()
        {
            ContentDialog dialog = new()
            {
                Title = Assets.Lang.Resources.Tools_FixOffByOneTitle,
                Content = Assets.Lang.Resources.Tools_FixOffByOneDescription,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                CloseButtonText = Assets.Lang.Resources.Generic_No,
            };

            return dialog.ShowAsync();
        }
    }

    private async void MenuItemScaleSelection_OnClick(object? sender, RoutedEventArgs e)
    {
        ToolsView_ScaleSelection scaleView = new();
        ContentDialog dialog = new()
        {
            Content = scaleView,
            Title = Assets.Lang.Resources.Menu_ScaleSelection,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Ok,
        };
        
        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        ChartEditor.ScaleSelection((float)(scaleView.ScalarNumbericUpDown.Value ?? 1));
    }
    
    private void MenuItemGenerateJaggedHolds_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ChartEditor.SelectedNotes.Exists(x => x.IsNoteCollection))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NoNoteCollectionSelected, Assets.Lang.Resources.Editor_NoNoteCollectionSelectedTip);
            return;
        }

        ToolsView_GenerateJaggedHolds generatorView = new();
        ContentDialog dialog = new()
        {
            Content = generatorView,
            Title = Assets.Lang.Resources.Menu_JaggedNoteCollections,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Generate,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;

            switch (generatorView.GeneratorMethod.SelectedIndex)
            {
                case 0:
                {
                    ChartEditor.GenerateSpikeHolds(generatorView.OffsetEven.IsChecked ?? true, (int?)generatorView.LeftEdge.Value ?? 0, (int?)generatorView.RightEdge.Value ?? 0);
                    break;
                }
                case 1:
                {
                    ChartEditor.GenerateNoiseHolds(generatorView.OffsetEven.IsChecked ?? true, (int?)generatorView.LeftEdgeMin.Value ?? 0, (int?)generatorView.LeftEdgeMax.Value ?? 0, (int?)generatorView.RightEdgeMin.Value ?? 0, (int?)generatorView.RightEdgeMax.Value ?? 0);
                    break;
                }
            }
        });
    }

    private void MenuItemReconstructHolds_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ChartEditor.SelectedNotes.Exists(x => x.IsNoteCollection))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NoNoteCollectionSelected, Assets.Lang.Resources.Editor_NoNoteCollectionSelectedTip);
            return;
        }

        ToolsView_ReconstructHolds reconstructView = new();
        ContentDialog dialog = new()
        {
            Content = reconstructView,
            Title = Assets.Lang.Resources.Menu_ReconstructNoteCollections,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Generate,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;

            ChartEditor.ReconstructHold(reconstructView.GeneratorMethod.SelectedIndex, (int?)reconstructView.Interval.Value ?? 0);
        });
    }

    private void MenuItemProofread_OnClick(object? sender, RoutedEventArgs e)
    {
        MiscView_Proofread proofreadView = new();
        Proofreader.Proofread(proofreadView.TextBlockProofreadResults, ChartEditor.Chart, UserConfig.EditorConfig.LimitToMercuryBonusTypes);

        if (proofreadView.TextBlockProofreadResults.Inlines?.Count == 0)
        {
            Dispatcher.UIThread.Invoke(() => proofreadView.TextBlockProofreadResults.Inlines.Add(new Avalonia.Controls.Documents.Run("No issues found! :]") { FontWeight = FontWeight.Bold, Foreground = Avalonia.Media.Brushes.Turquoise }));
        }

        ContentDialog dialog = new()
        {
            Content = proofreadView,
            Title = Assets.Lang.Resources.Menu_Proofread,
            CloseButtonText = Assets.Lang.Resources.Generic_Ok,
        };

        dialog.ShowAsync();
    }

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
            "RadioNoteHold" => NoteType.Hold,
            "RadioNoteTrace" => NoteType.Trace,
            "RadioNoteDamage" => NoteType.Damage,
            "RadioNoteMaskAdd" => NoteType.MaskAdd,
            "RadioNoteMaskRemove" => NoteType.MaskRemove,
            _ => throw new ArgumentOutOfRangeException(),
        };

        ChartEditor.CurrentNoteType = noteType;
        ChartEditor.UpdateCursorNoteType();
        
        ToggleTypeRadio(noteType is NoteType.MaskAdd or NoteType.MaskRemove);
        ToggleBonusTypeRadios(ChartEditor.BonusAvailable(noteType), ChartEditor.RNoteAvailable(noteType));
    }

    private void RadioBonusType_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton selected || selected.IsChecked == false) return;
        BonusType bonusType = selected.Name switch
        {
            "RadioNoBonus" => BonusType.None,
            "RadioBonus" => BonusType.Bonus,
            "RadioRNote" => BonusType.RNote,
            _ => throw new ArgumentOutOfRangeException(),
        };

        ChartEditor.CurrentBonusType = bonusType;
        ChartEditor.UpdateCursorNoteType();
    }

    private void RadioMaskDirection_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton selected || selected.IsChecked == false) return;
        MaskDirection maskDirection = selected.Name switch
        {
            "RadioMaskClockwise" => MaskDirection.Clockwise,
            "RadioMaskCounterclockwise" => MaskDirection.Counterclockwise,
            "RadioMaskCenter" => MaskDirection.Center,
            _ => throw new ArgumentOutOfRangeException(),
        };

        ChartEditor.CurrentMaskDirection = maskDirection;
        ChartEditor.UpdateCursorNoteType();
    }

    private void ButtonGimmickBpmChange_OnClick(object? sender, RoutedEventArgs e)
    {
        GimmickView_Bpm gimmickView = new();
        ContentDialog dialog = new()
        {
            Content = gimmickView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;

            if (gimmickView.BpmNumberBox.Value <= 0)
            {
                ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidBpm);
                return;
            }

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
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;

            if ((int)gimmickView.TimeSigUpperNumberBox.Value <= 0 || (int)gimmickView.TimeSigLowerNumberBox.Value <= 0)
            {
                ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidTimeSig);
                return;
            }

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
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
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
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            if (gimmickView.IsValueNull) return;
            
            if (gimmickView.StartMeasureDecimal > gimmickView.EndMeasureDecimal)
            {
                ShowWarningMessage(Assets.Lang.Resources.Warning_CouldntInsertGimmick, Assets.Lang.Resources.Warning_StopOrder);
                return;
            }

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
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create,
        };

        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            if (gimmickView.IsValueNull) return;
            
            if (gimmickView.EffectStartMeasureDecimal > gimmickView.EffectEndMeasureDecimal ||
                gimmickView.EffectStartMeasureDecimal > gimmickView.NoteEndMeasureDecimal ||
                gimmickView.EffectEndMeasureDecimal > gimmickView.NoteEndMeasureDecimal)
            {
                ShowWarningMessage(Assets.Lang.Resources.Warning_CouldntInsertGimmick, Assets.Lang.Resources.Warning_ReverseOrder);
                return;
            }

            ChartEditor.InsertReverse(gimmickView.EffectStartMeasureDecimal, gimmickView.EffectEndMeasureDecimal, gimmickView.NoteEndMeasureDecimal);
        });
    }

    private void ButtonGimmickEndOfChart_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.InsertEndOfChart();

    private void ButtonInsert_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.InsertNote();
    }

    private void ButtonEditHold_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.EditHold();
    }

    private void ButtonEndHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EndHold();

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
        if (NumericNoteSize == null) return;
        if (SliderNoteSize == null) return;
        
        if (fromSlider)
        {
            NumericNoteSize.Value = (decimal?)SliderNoteSize.Value;
        }
        else
        {
            NumericNoteSize.Value ??= Note.MinSize(ChartEditor.CurrentNoteType, ChartEditor.CurrentBonusType, NoteLinkType.Unlinked);
            SliderNoteSize.Value = (double)NumericNoteSize.Value;
        }

        ChartEditor.Cursor.Size = (int)NumericNoteSize.Value;
    }

    private void NumericMeasure_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (NumericMeasure == null) return;
        if (e.NewValue == null || NumericMeasure.Value == null)
        {
            NumericMeasure.Value = 0;
            return;
        }
        
        NumericMeasure.Value = Math.Clamp((decimal)e.NewValue, NumericMeasure.Minimum, NumericMeasure.Maximum);

        if (UpdateSource is TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Numeric);
    }

    private void NumericBeatValue_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (NumericBeatValue == null) return;
        if (NumericBeatDivisor == null) return;
        if (e.NewValue == null || NumericBeatValue.Value == null)
        {
            NumericBeatValue.Value = 0;
            return;
        }

        if (NumericBeatDivisor.Value == null) return;
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

        if (UpdateSource is TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Numeric);
    }

    private void NumericBeatDivisor_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (NumericBeatDivisor == null) return;
        if (NumericBeatValue == null) return;
        if (e.NewValue == null || NumericBeatDivisor.Value == null)
        {
            NumericBeatDivisor.Value = 1;
            return;
        }

        if (NumericBeatValue.Value == null) return;
        if (AudioManager.CurrentSong is { IsPlaying: true }) return;

        decimal oldValue = e.OldValue ?? 16;
        decimal ratio = (decimal)e.NewValue / oldValue;
        NumericBeatValue.Value = decimal.Round((decimal)NumericBeatValue.Value * ratio);
    }

    private void ButtonPlay_OnClick(object? sender, RoutedEventArgs e)
    {
        if (AudioManager.CurrentSong == null) return;
        SetPlayState(AudioManager.CurrentSong.IsPlaying ? PlayerState.Paused : PlayerState.Playing);
    }

    private void SliderSongPosition_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (UpdateSource is TimeUpdateSource.None)
            UpdateTime(TimeUpdateSource.Slider);
    }

    private void SliderPlaybackSpeed_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        TextBlockPlaybackSpeed.Text = $"{SliderPlaybackSpeed.Value,3:F0}%";
        if (AudioManager.CurrentSong == null) return;

        AudioManager.CurrentSong.PlaybackSpeed = (int)SliderPlaybackSpeed.Value;
    }

    private void ChartInfoVersion_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.Version = ChartInfoVersion.Text ?? "";
    }
    private void ChartInfoVersion_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.VersionChange, [ChartEditor.Chart.Version]));

    private void ChartInfoTitle_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.Title = ChartInfoTitle.Text ?? "";
    }
    private void ChartInfoTitle_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.TitleChange, [ChartEditor.Chart.Title]));

    private void ChartInfoRubi_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.Rubi = ChartInfoRubi.Text ?? "";
    }
    private void ChartInfoRubi_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.RubiChange, [ChartEditor.Chart.Rubi]));

    private void ChartInfoArtist_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.Artist = ChartInfoArtist.Text ?? "";
    }
    private void ChartInfoArtist_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.ArtistChange, [ChartEditor.Chart.Artist]));

    private void ChartInfoAuthor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.Author = ChartInfoAuthor.Text ?? "";
    }
    private void ChartInfoAuthor_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.AuthorChange, [ChartEditor.Chart.Author]));

    private void ChartInfoBpmText_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChartEditor.Chart.BpmText = ChartInfoBpmText.Text ?? "";
    }
    private void ChartInfoBpmText_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.BpmTextChange, [ ChartEditor.Chart.BpmText ]));

    private void ChartInfoBackground_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ChartInfoBackground is null) return;
        ChartEditor.Chart.Background = ChartInfoBackground.SelectedIndex;
    }
    private void ChartInfoBackground_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (ChartInfoBackground is null) return;
        ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.BackgroundChange, null, [ ChartEditor.Chart.Background ]));
    }
    
    private void ChartInfoDiff_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ChartInfoDiff is null) return;
        ChartEditor.Chart.Diff = ChartInfoDiff.SelectedIndex;
    }
    private void ChartInfoDiff_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (ChartInfoDiff is null) return;
        ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.DiffChange, null, [ ChartEditor.Chart.Diff ]));
    }

    private void ChartInfoLevel_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.Level = double.IsNaN(ChartInfoLevel.Value) ? 0 : (decimal)ChartInfoLevel.Value;
    }
    private void ChartInfoLevel_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.LevelChange, null, null, [ ChartEditor.Chart.Level ]));

    private void ChartInfoClearThreshold_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.ClearThreshold = double.IsNaN(ChartInfoLevel.Value) ? 0 : (decimal)ChartInfoClearThreshold.Value;
    }
    private void ChartInfoClearThreshold_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.ClearThresholdChange, null, null, [ ChartEditor.Chart.ClearThreshold ]));
    
    private void ChartInfoPreviewStart_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.PreviewStart = (decimal)ChartInfoPreviewStart.Value;
    }
    private void ChartInfoPreviewStart_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.PreviewStartChange, null, null, [ ChartEditor.Chart.PreviewStart ]));

    private void ChartInfoPreviewTime_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.PreviewTime = (decimal)ChartInfoPreviewTime.Value;
    }
    private void ChartInfoPreviewTime_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.PreviewTimeChange, null, null, [ ChartEditor.Chart.PreviewTime ]));

    private void ChartInfoBgmOffset_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.BgmOffset = double.IsNaN(ChartInfoLevel.Value) ? 0 : (decimal)ChartInfoBgmOffset.Value;
    }
    private void ChartInfoBgmOffset_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.BgmOffsetChange, null, null, [ ChartEditor.Chart.BgmOffset ]));

    private void ChartInfoBgaOffset_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ChartEditor.Chart.BgaOffset = double.IsNaN(ChartInfoLevel.Value) ? 0 : (decimal)ChartInfoBgaOffset.Value;
    }
    private void ChartInfoBgaOffset_LostFocus(object? sender, RoutedEventArgs e) => ConnectionManager.SendMessage(new ConnectionManager.MessageSerializer(ConnectionManager.MessageTypes.BgaOffsetChange, null, null, [ ChartEditor.Chart.BgaOffset ]));

    private async void ChartInfoSelectBgm_OnClick(object? sender, RoutedEventArgs e)
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

        ChartEditor.Chart.BgmFilepath = audioFile.Path.LocalPath;
        ChartEditor.Chart.IsSaved = false;

        AudioManager.SetSong(ChartEditor.Chart.BgmFilepath, (float)(UserConfig.AudioConfig.MusicVolume * 0.01), (int)SliderPlaybackSpeed.Value);
        SetSongPositionSliderValues();
        UpdateFilepathsInUi();
        RenderEngine.UpdateVisibleTime();
    }

    private async void ChartInfoSelectBga_OnClick(object? sender, RoutedEventArgs e)
    {
        // prompt user to create new chart if no start time events exist -> no chart exists
        if (ChartEditor.Chart.StartBpm == null || ChartEditor.Chart.StartTimeSig == null)
        {
            MenuItemNew_OnClick(sender, e);
            return;
        }

        IStorageFile? videoFile = await OpenVideoFilePicker();
        if (videoFile == null) return;
        if (!File.Exists(videoFile.Path.LocalPath))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidAudio);
            return;
        }

        ChartEditor.Chart.BgaFilepath = videoFile.Path.LocalPath;
        ChartEditor.Chart.IsSaved = false;

        UpdateFilepathsInUi();
    }

    private async void ChartInfoSelectJacket_OnClick(object? sender, RoutedEventArgs e)
    {
        // prompt user to create new chart if no start time events exist -> no chart exists
        if (ChartEditor.Chart.StartBpm == null || ChartEditor.Chart.StartTimeSig == null)
        {
            MenuItemNew_OnClick(sender, e);
            return;
        }

        IStorageFile? jacketFile = await OpenJacketFilePicker();

        if (jacketFile == null) return;

        if (!File.Exists(jacketFile.Path.LocalPath))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NewChartInvalidAudio);
            return;
        }

        ChartEditor.Chart.JacketFilepath = jacketFile.Path.LocalPath;
        ChartEditor.Chart.IsSaved = false;

        UpdateFilepathsInUi();
        SetChartInfo();
    }

    private void ChartInfoPlayPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.Chart.PreviewTime == 0 || AudioManager.CurrentSong == null) return;

        AudioManager.CurrentSong.Position = (uint)(ChartEditor.Chart.PreviewStart * 1000);
        UpdateTime(TimeUpdateSource.Timer);
        SetPlayState(PlayerState.Preview);
    }
    
    private void SelectionInfoHighlightNext_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.HighlightNextElement();
    
    private void SelectionInfoHighlightPrev_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.HighlightPrevElement();
    
    private void SelectionInfoHighlightNearest_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.HighlightNearestElement();
    
    private void QuickSettingsNoteSpeed_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        UserConfig.RenderConfig.NoteSpeed = (double?)QuickSettingsNumericNoteSpeed.Value ?? 4.5;
        RenderEngine.UpdateVisibleTime();
        ApplySettings();
    }

    private void QuickSettingsBeatDivision_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        UserConfig.RenderConfig.BeatDivision = (int)(QuickSettingsNumericBeatDivision.Value ?? 4);
        RenderEngine.UpdateVisibleTime();
        ApplySettings();
    }
    
    private void QuickSettingsSliderMusic_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UserConfig.AudioConfig.MusicVolume = QuickSettingsSliderMusic.Value;
        AudioManager.UpdateVolume();
        ApplySettings();
    }

    private void QuickSettingsSliderHitsound_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UserConfig.AudioConfig.HitsoundVolume = QuickSettingsSliderHitsound.Value;
        AudioManager.UpdateVolume();
        ApplySettings();
    }
    
    private void QuickSettingsStartMetronome_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.AudioConfig.StartMetronome = QuickSettingsCheckBoxStartMetronome.IsChecked ?? false;
    }
    
    private void QuickSettingsConstantMetronome_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.AudioConfig.ConstantMetronome = QuickSettingsCheckBoxConstantMetronome.IsChecked ?? false;
    }
    
    private void QuickSettingsShowHiSpeed_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.ShowHiSpeed = QuickSettingsCheckBoxShowHiSpeed.IsChecked ?? true;
        ApplySettings();
    }
    
    private void QuickSettingsDrawNoRenderHoldSegments_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.DrawNoRenderSegments = QuickSettingsCheckBoxDrawNoRenderHoldSegments.IsChecked ?? true;
        ApplySettings();
    }
    
    private void QuickSettingsShowJudgementWindowMarvelous_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.ShowJudgementWindowMarvelous = QuickSettingsCheckBoxShowJudgementWindowMarvelous.IsChecked ?? true;
        ApplySettings();
    }
    
    private void QuickSettingsShowJudgementWindowGreat_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.ShowJudgementWindowGreat = QuickSettingsCheckBoxShowJudgementWindowGreat.IsChecked ?? true;
        ApplySettings();
    }
    
    private void QuickSettingsShowJudgementWindowGood_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.ShowJudgementWindowGood = QuickSettingsCheckBoxShowJudgementWindowGood.IsChecked ?? true;
        ApplySettings();
    }
    
    private void QuickSettingsCutEarlyJudgementWindowOnHolds_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.CutEarlyJudgementWindowOnHolds = QuickSettingsCheckBoxCutEarlyJudgementWindowOnHolds.IsChecked ?? true;
        ApplySettings();
    }
    
    private void QuickSettingsCutOverlappingJudgementWindows_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.CutOverlappingJudgementWindows = QuickSettingsCheckBoxCutOverlappingJudgementWindows.IsChecked ?? true;
        ApplySettings();
    }

    private void QuickSettingsHideNotesOnDifferentLayers_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.HideNotesOnDifferentLayers = QuickSettingsHideNotesOnDifferentLayers.IsChecked ?? true;
        ApplySettings();
    }
    
    private void ButtonEditSelectionShape_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(true, false);
    
    private void ButtonEditSelectionProperties_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(false, true);
    
    private void ButtonEditSelectionFull_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(true, true);

    private void ButtonMirrorSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.MirrorSelection((int?)NumericMirrorAxis.Value ?? 30);

    private void ButtonReverseSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.ReverseSelection();
    
    private void ButtonDeleteSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeleteSelection();

    private void ButtonBakeHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.BakeHold((MathExtensions.HoldEaseType)HoldEaseComboBox.SelectedIndex, false);

    private void ButtonStitchHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.StitchHold();

    private void ButtonSplitHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.SplitHold();
    
    private void ButtonInsertHoldSegment_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.InsertHoldSegment();

    private void ButtonDeleteSegments_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeleteSegments();
    
    private void NumericMirrorAxis_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        NumericMirrorAxis.Value ??= 30;
        if (NumericMirrorAxis.Value > 59) NumericMirrorAxis.Value = 0;
        if (NumericMirrorAxis.Value < 0) NumericMirrorAxis.Value = 59;

        RenderEngine.MirrorAxis = (int)NumericMirrorAxis.Value;
    }
    
    private void NumericMirrorAxis_OnPointerEntered(object? sender, PointerEventArgs e) => RenderEngine.IsHoveringOverMirrorAxis = true;
    
    private void NumericMirrorAxis_OnPointerExited(object? sender, PointerEventArgs e) => RenderEngine.IsHoveringOverMirrorAxis = false;

    private void ButtonSetRenderTrue_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.SetSelectionRenderFlag(true);
    }

    private void ButtonSetRenderFalse_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.SetSelectionRenderFlag(false);
    }

    private void ButtonFlipNoteDirection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.FlipNoteDirection();
    
    private void ButtonConvertToInstantMask_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.ConvertToInstantMask();

    private void ButtonPaintTraces_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.PaintTraces((TraceColor)TraceColorComboBox.SelectedIndex);

    private void ButtonSetScrollLayer_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.SetScrollLayer((ScrollLayer)ScrollLayerComboBox.SelectedIndex);
    
    private void ButtonSetLoopStart_OnClick(object? sender, RoutedEventArgs e) => SetLoopMarkerStart();

    private void ButtonSetLoopEnd_OnClick(object? sender, RoutedEventArgs e) => SetLoopMarkerEnd();

    private void ButtonAddComment_OnClick(object? sender, RoutedEventArgs e) => AddComment();
    
    private void ToggleButtonLoop_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        bool loop = (button.IsChecked ?? false) && AudioManager.CurrentSong != null;
        
        AudioManager.Loop = loop;
        LoopMarkerStart.IsVisible = loop;
        LoopMarkerEnd.IsVisible = loop;
    }
    
    private void ToggleNoteLayer_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        ChartEditor.LayerNoteActive = button.IsChecked ?? true;
    }
    
    private void ToggleMaskLayer_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        ChartEditor.LayerMaskActive = button.IsChecked ?? true;
    }
    
    private void ToggleGimmickLayer_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        ChartEditor.LayerGimmickActive = button.IsChecked ?? true;
    }

    private void ToggleTraceLayer_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        ChartEditor.LayerTraceActive = button.IsChecked ?? true;
    }
    
    // ________________ UI Dialogs & Misc
    
    // This should probably be sorted, but I can't be arsed rn.
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
        KeybindEditor.StopRebinding(); // Stop rebinding in case it was active.
        SetButtonColors(); // Update button colors if they were changed
        SetTraceComboBoxItemColors(); // Update trace comboboxitem colors if they were changed
        SetMenuItemInputGestureText(); // Update InputGesture text in case stuff was rebound
        SetQuickSettings();
        UpdateCurrentTimeScaleInfo();
        RenderEngine.UpdateBrushes();
        RenderEngine.UpdateVisibleTime();
        AudioManager.LoadHitsoundSamples();
        AudioManager.UpdateVolume();
        ToggleBonusTypeRadios(ChartEditor.BonusAvailable(ChartEditor.CurrentNoteType), ChartEditor.RNoteAvailable(ChartEditor.CurrentNoteType));
        
        // I know some maniac is going to change their refresh rate while playing a song.
        updateInterval = TimeSpan.FromSeconds(1.0 / UserConfig.RenderConfig.RefreshRate);
        if (playerState is not PlayerState.Paused) UpdateTimer.Change(updateInterval, updateInterval);

        File.WriteAllText(ConfigPath, Toml.FromModel(UserConfig));
    }

    internal void ShowWarningMessage(string title, string? text = null)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = text,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Ok,
        };

        dialog.ShowAsync();
    }
    
    private readonly ContentDialog transmittingDataDialog = new()
    {
        Title = Assets.Lang.Resources.Online_TransmittingData,
        PrimaryButtonText = Assets.Lang.Resources.Menu_Disconnect,
    };

    public void ShowTransmittingDataMessage()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await showReceivingDataDialog();
            if (result is ContentDialogResult.Primary) ConnectionManager.LeaveSession();
        });
        return;
        
        Task<ContentDialogResult> showReceivingDataDialog()
        {
            return transmittingDataDialog.ShowAsync();
        }
    }

    public void HideTransmittingDataMessage() => Dispatcher.UIThread.Post(() => transmittingDataDialog.Hide());

    private readonly ContentDialog sendingDataDialog = new()
    {
        Title = Assets.Lang.Resources.Online_TransmittingData,
    };

    public void SetPlayState(PlayerState state)
    {
        if (AudioManager.CurrentSong == null)
        {
            UpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            HitsoundTimer.IsEnabled = false;
            IconPlay.IsVisible = true;
            IconStop.IsVisible = false;
            return;
        }

        playerState = state;
        bool playing = state is not PlayerState.Paused;
        
        AudioManager.CurrentSong.Volume = (float)(UserConfig.AudioConfig.MusicVolume * 0.01);
        AudioManager.CurrentSong.IsPlaying = playing;
        
        if (playing) UpdateTimer.Change(updateInterval, updateInterval);
        else UpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        HitsoundTimer.IsEnabled = playing;
        
        IconPlay.IsVisible = !playing;
        IconStop.IsVisible = playing;
        SliderSongPosition.IsEnabled = !playing;

        if (!playing && UserConfig.EditorConfig.QuantizeOnPause)
        {
            ChartEditor.CurrentMeasureDecimal = MathExtensions.RoundToInterval(ChartEditor.CurrentMeasureDecimal, 1 / (float)(NumericBeatDivisor.Value ?? 16));
            UpdateTime(TimeUpdateSource.MeasureDecimal);
        }

        AudioManager.HitsoundNoteIndex = ChartEditor.Chart.Notes.FindIndex(x => x.BeatData.MeasureDecimal >= ChartEditor.CurrentMeasureDecimal);

        Gimmick? timeSig = ChartEditor.Chart.Gimmicks.LastOrDefault(x => x.GimmickType == GimmickType.TimeSigChange && x.BeatData.MeasureDecimal < ChartEditor.CurrentMeasureDecimal);
        if (timeSig != null)
        {
            int clicks = timeSig.TimeSig.Upper;
            AudioManager.MetronomeTime = float.Ceiling(ChartEditor.CurrentMeasureDecimal * clicks) / clicks;
        }
        else
        {
            AudioManager.MetronomeTime = 0;
        }
    }
    
    /// <summary>
    /// Prompts the user to save their work.
    /// </summary>
    /// <returns>True if file is saved.</returns>
    private async Task<bool> PromptSave()
    {
        if (ChartEditor.Chart.IsSaved) return true;
        if (LockState is UiLockState.Empty) return true;

        ContentDialogResult result = await showSavePrompt();

        return result switch
        {
            ContentDialogResult.Primary => await SaveFile(false, ChartEditor.Chart.Filepath),
            ContentDialogResult.Secondary => true,
            _ => false,
        };

        Task<ContentDialogResult> showSavePrompt()
        {
            ContentDialog dialog = new()
            {
                Title = Assets.Lang.Resources.Generic_SaveWarning,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                SecondaryButtonText = Assets.Lang.Resources.Generic_No,
                CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
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
                CloseButtonText = Assets.Lang.Resources.Generic_No,
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
        IReadOnlyList<IStorageFile> result = await GetStorageProvider().OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Audio files")
                {
                    Patterns = new[] {"*.wav","*.flac","*.mp3","*.ogg"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"},
                },
            },
        });

        return result.Count != 1 ? null : result[0];
    }
    
    public async Task<IStorageFile?> OpenChartFilePicker()
    {
        IReadOnlyList<IStorageFile> result = await GetStorageProvider().OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Chart files")
                {
                    Patterns = new[] {"*.mer", "*.map", "*.sat"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"},
                },
            },
        });

        return result.Count != 1 ? null : result[0];
    }
    
    public async Task<IStorageFile?> OpenJacketFilePicker()
    {
        IReadOnlyList<IStorageFile> result = await GetStorageProvider().OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Image files")
                {
                    Patterns = new[] {"*.png","*.jpg","*.jpeg"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"},
                },
            },
        });

        return result.Count != 1 ? null : result[0];
    }
    
    public async Task<IStorageFile?> OpenVideoFilePicker()
    {
        IReadOnlyList<IStorageFile> result = await GetStorageProvider().OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Video files")
                {
                    Patterns = new[] {"*.mp4","*.webm"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"},
                },
            },
        });

        return result.Count != 1 ? null : result[0];
    }
    
    public async Task<IStorageFolder?> OpenFolderPicker()
    {
        IReadOnlyList<IStorageFolder> result = await GetStorageProvider().OpenFolderPickerAsync(new()
        {
            AllowMultiple = false,
        });

        return result.Count != 1 ? null : result[0];
    }
    
    private async Task<IStorageFile?> SaveChartFilePicker(ChartFormatType formatType)
    {
        return await GetStorageProvider().SaveFilePickerAsync(new()
        {
            DefaultExtension = formatType switch
            {
                ChartFormatType.Editor => ".map",
                ChartFormatType.Mercury => ".mer",
                ChartFormatType.Saturn => ".sat",
                _ => "",
            },
            FileTypeChoices = new[]
            {
                new FilePickerFileType(formatType switch
                {
                    ChartFormatType.Editor => "Editor Chart File",
                    ChartFormatType.Mercury => "Mercury Chart File",
                    ChartFormatType.Saturn => "Saturn Chart File",
                    _ => "",
                })
                {
                    Patterns = formatType switch
                    {
                        ChartFormatType.Editor => ["*.map"],
                        ChartFormatType.Mercury => ["*.mer"],
                        ChartFormatType.Saturn => ["*.sat"],
                        _ => [],
                    },
                    AppleUniformTypeIdentifiers = ["public.item"],
                },
            },
        });
    }
    
    private async void OpenChart(string path)
    {
        // Load chart
        ChartEditor.LoadChart(path);
        
        // Oopsie, audio not found.
        if (!File.Exists(ChartEditor.Chart.BgmFilepath))
        {
            // Prompt user to select audio.
            if (!await PromptSelectAudio())
            {
                // User said no, clear chart again and return.
                ChartEditor.Chart.Clear();
                UpdateFilepathsInUi();
                SetChartInfo();
                SetSelectionInfo();
                ResetLoopMarkers(AudioManager.CurrentSong?.Length ?? 0);
                SliderSongPosition.IsEnabled = false;
                SetUiLockState(UiLockState.Empty);
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

            ChartEditor.Chart.BgmFilepath = audioFile.Path.LocalPath;
        }
        
        AudioManager.SetSong(ChartEditor.Chart.BgmFilepath, (float)(UserConfig.AudioConfig.MusicVolume * 0.01), (int)SliderPlaybackSpeed.Value);
        SetSongPositionSliderValues();
        UpdateFilepathsInUi();
        RenderEngine.UpdateVisibleTime();
        ChartEditor.UpdateCommentMarkers();
        ResetLoopMarkers(AudioManager.CurrentSong?.Length ?? 0);
        SetUiLockState(UiLockState.Loaded);
        UpdateCurrentTimeScaleInfo();

        if (UserConfig.EditorConfig.LimitToMercuryBonusTypes && ChartEditor.Chart.GetNonMercuryBonusTypeNotes().Count != 0)
        { 
            ContentDialog mercuryBonusTypeWarning = new()
            {
                Title = Assets.Lang.Resources.Editor_MercuryBonusTypeTitle,
                Content = Assets.Lang.Resources.Editor_MercuryBonusTypeReplacePrompt,
                PrimaryButtonText = Assets.Lang.Resources.Editor_MercuryBonusType_ReplaceNotes,
                SecondaryButtonText = Assets.Lang.Resources.Editor_MercuryBonusType_SwitchMode,
            };
            
            ContentDialogResult result = await mercuryBonusTypeWarning.ShowAsync();
            
            if (result is ContentDialogResult.Primary) ChartEditor.Chart.ConvertNonMercuryBonusTypeNotes();
            if (result is ContentDialogResult.Secondary)
            {
                UserConfig.EditorConfig.LimitToMercuryBonusTypes = false;
                ApplySettings();
            }
        }
    }

    public void OpenChartFromNetwork(string data, string bgmFilepath)
    {
        ChartEditor.LoadChartNetwork(data);
        ChartEditor.Chart.BgmFilepath = bgmFilepath;
        AudioManager.SetSong(ChartEditor.Chart.BgmFilepath, (float)(UserConfig.AudioConfig.MusicVolume * 0.01), (int)SliderPlaybackSpeed.Value);
        SetSongPositionSliderValues();
        UpdateFilepathsInUi();
        RenderEngine.UpdateVisibleTime();
        ResetLoopMarkers(AudioManager.CurrentSong?.Length ?? 0);
        SetUiLockState(UiLockState.Loaded);
    }
    
    public async Task<bool> SaveFile(bool openFilePicker, string filepath)
    {
        if (openFilePicker || string.IsNullOrEmpty(filepath))
        {
            IStorageFile? file = await SaveChartFilePicker(ChartFormatType.Editor);

            if (file == null) return false;
            filepath = file.Path.LocalPath;
        }

        if (string.IsNullOrEmpty(filepath)) return false;

        FormatHandler.WriteFile(ChartEditor.Chart, filepath, ChartFormatType.Editor);
        ChartEditor.Chart.Filepath = filepath;
        ChartEditor.Chart.IsNew = false;
        ChartEditor.Chart.IsSaved = true;
        ClearAutosaves();
        SetChartInfo();
        return true;
    }

    public async Task ExportFile(ChartFormatType chartFormatType)
    {
        IStorageFile? file = await SaveChartFilePicker(chartFormatType);

        if (file == null) return;
        string filepath = file.Path.LocalPath;
        
        if (string.IsNullOrEmpty(filepath)) return;
        
        MiscView_Proofread proofreadView = new();
        Proofreader.Proofread(proofreadView.TextBlockProofreadResults, ChartEditor.Chart, UserConfig.EditorConfig.LimitToMercuryBonusTypes);

        if (proofreadView.TextBlockProofreadResults.Inlines?.Count != 0)
        {
            ContentDialog dialog = new()
            {
                Content = proofreadView,
                Title = Assets.Lang.Resources.Menu_Proofread,
                CloseButtonText = Assets.Lang.Resources.Generic_Ok,
            };
        
            await dialog.ShowAsync();
        }
        
        FormatHandler.WriteFile(ChartEditor.Chart, filepath, chartFormatType);
    }

    public async void AddComment()
    {
        if (AudioManager.CurrentSong == null) return;
        
        MiscView_AddComment addCommentView = new();
            
        ContentDialog dialog = new()
        {
            Content = addCommentView,
            Title = Assets.Lang.Resources.Editor_AddComment,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Add,
        };
        
        ContentDialogResult result = await dialog.ShowAsync();
        if (result is ContentDialogResult.Primary) ChartEditor.AddComment(ChartEditor.CurrentBeatData, addCommentView.CommentTextBox.Text ?? "");
    }
}