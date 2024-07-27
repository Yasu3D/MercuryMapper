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
        
        KeybindEditor = new(UserConfig);
        AudioManager = new(this);
        RenderEngine = new(this);
        
        updateInterval = TimeSpan.FromSeconds(1.0 / UserConfig.RenderConfig.RefreshRate);
        UpdateTimer = new(UpdateTimer_Tick, null, Timeout.Infinite, Timeout.Infinite);
        HitsoundTimer = new(TimeSpan.FromMilliseconds(1), DispatcherPriority.Background, HitsoundTimer_Tick) { IsEnabled = false };
        AutosaveTimer = new(TimeSpan.FromMinutes(1), DispatcherPriority.Background, AutosaveTimer_Tick) { IsEnabled = true };
        
        KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);

        VersionText.Text = AppVersion;
        ApplySettings();
        ToggleTypeRadio(false);
        ToggleInsertButton();
        SetSelectionInfo();
        SetQuickSettings();
        
        Dispatcher.UIThread.Post(async () => await CheckAutosaves(), DispatcherPriority.Background);
    }

    public bool CanShutdown;
    public const string AppVersion = "v2.3.1";
    private const string ConfigPath = "UserConfig.toml";
    
    public UserConfig UserConfig = new();
    public readonly KeybindEditor KeybindEditor;
    public readonly ChartEditor ChartEditor;
    public readonly AudioManager AudioManager;
    public readonly RenderEngine RenderEngine;

    private TimeSpan updateInterval;
    public readonly Timer UpdateTimer;
    public readonly DispatcherTimer HitsoundTimer;
    public readonly DispatcherTimer AutosaveTimer;
    
    private TimeUpdateSource timeUpdateSource = TimeUpdateSource.None;
    private enum TimeUpdateSource
    {
        None,
        Slider,
        Numeric,
        Timer,
        MeasureDecimal
    }

    private PointerState pointerState = PointerState.Released;
    private enum PointerState
    {
        Released,
        Pressed
    }

    private PlayerState playerState = PlayerState.Paused;
    public enum PlayerState
    {
        Paused,
        Playing,
        Preview
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
            Console.WriteLine(e.Message);
            // basically ignored for now
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
        MenuItemSelectAll.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorSelectAll"].ToGesture();
        MenuItemDeselectAll.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorDeselectAll"].ToGesture();
        MenuItemBoxSelect.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorBoxSelect"].ToGesture();
        MenuItemSelectHighlightedNote.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorSelectHighlightedNote"].ToGesture();
        MenuItemSelectHoldReferences.InputGesture = UserConfig.KeymapConfig.Keybinds["EditorSelectHoldReferences"].ToGesture();
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
    
    public void UpdateTimer_Tick(object? sender)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (AudioManager.CurrentSong == null) return;

            if (AudioManager.Loop && AudioManager.CurrentSong.Position >= AudioManager.LoopEnd && AudioManager.LoopStart < AudioManager.LoopEnd)
            {
                AudioManager.CurrentSong.Position = AudioManager.LoopStart;
                AudioManager.HitsoundNoteIndex = ChartEditor.Chart.Notes.FindIndex(x => x.BeatData.MeasureDecimal >= ChartEditor.Chart.Timestamp2MeasureDecimal(AudioManager.CurrentSong.Position));
                
                if (ChartEditor.Chart.StartTimeSig != null)
                {
                    int clicks = ChartEditor.Chart.StartTimeSig.TimeSig.Upper;
                    float interval = 1.0f / clicks;

                    AudioManager.MetronomeIndex = (int)float.Ceiling(ChartEditor.CurrentMeasureDecimal / interval);
                }
            }
            
            if (AudioManager.CurrentSong.Position >= AudioManager.CurrentSong.Length && AudioManager.CurrentSong.IsPlaying)
            {
                SetPlayState(PlayerState.Paused);
                AudioManager.CurrentSong.Position = 0;
            }

            if (playerState is PlayerState.Preview && AudioManager.CurrentSong.Position >= (int)((ChartEditor.Chart.PreviewTime + ChartEditor.Chart.PreviewLength) * 1000))
            {
                SetPlayState(PlayerState.Paused);
                AudioManager.CurrentSong.Position = (uint)(ChartEditor.Chart.PreviewTime * 1000);
            }
        
            if (timeUpdateSource == TimeUpdateSource.None) 
                UpdateTime(TimeUpdateSource.Timer);
        });
    }
    
    public void HitsoundTimer_Tick(object? sender, EventArgs e)
    {
        if (AudioManager.HitsoundNoteIndex == -1 || AudioManager.CurrentSong is null) return;
        
        float measure = ChartEditor.Chart.Timestamp2MeasureDecimal(AudioManager.CurrentSong.Position + BassSoundEngine.GetLatency() + UserConfig.AudioConfig.HitsoundOffset);

        while (AudioManager.HitsoundNoteIndex < ChartEditor.Chart.Notes.Count && ChartEditor.Chart.Notes[AudioManager.HitsoundNoteIndex].BeatData.MeasureDecimal <= measure)
        {
            AudioManager.PlayHitsound(ChartEditor.Chart.Notes[AudioManager.HitsoundNoteIndex]);
        }

        
        if (AudioManager.MetronomeIndex == -1 || ChartEditor.Chart.StartTimeSig is null || measure >= 1) return;

        int clicks = ChartEditor.Chart.StartTimeSig.TimeSig.Upper;
        float interval = 1.0f / clicks;
        float nextClick = interval * AudioManager.MetronomeIndex;
        
        
        
        while (AudioManager.MetronomeIndex < clicks && nextClick <= measure)
        {
            AudioManager.PlayMetronome();
            nextClick = interval * AudioManager.MetronomeIndex;
        }
    }

    public void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        if (ChartEditor.Chart.IsSaved) return;
        
        string filepath = Path.GetTempFileName().Replace(".tmp",".autosave.mer");
        ChartEditor.Chart.WriteFile(filepath, ChartWriteType.Editor);
        
        Console.WriteLine(filepath);
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
        ChartEditor.Chart.FilePath = ""; // Clear path to not overwrite temp file.
        
        ClearAutosaves();
        return;

        Task<ContentDialogResult> showSelectAudioPrompt(string timestamp)
        {
            ContentDialog dialog = new()
            {
                Title = $"{Assets.Lang.Resources.Generic_AutosaveFound} {timestamp}",
                Content = Assets.Lang.Resources.Generic_AutosavePrompt,
                PrimaryButtonText = Assets.Lang.Resources.Generic_Yes,
                CloseButtonText = Assets.Lang.Resources.Generic_No
            };
            
            return dialog.ShowAsync();
        }
    }

    private static void ClearAutosaves()
    {
        Console.WriteLine("Clearing Autosaves");
        
        string[] autosaves = Directory.GetFiles(Path.GetTempPath(), "*.autosave.mer");
        foreach (string file in autosaves) File.Delete(file);
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
        BeatData data = timeUpdateSource is TimeUpdateSource.Timer or TimeUpdateSource.Slider || NumericMeasure.Value is null || NumericBeatValue.Value is null || NumericBeatDivisor.Value is null
            ? ChartEditor.Chart.Timestamp2BeatData(AudioManager.CurrentSong.Position)
            : new((int)NumericMeasure.Value, (int)(NumericBeatValue.Value / NumericBeatDivisor.Value * 1920));
        
        // Update Numeric
        if (source is not TimeUpdateSource.Numeric && NumericBeatDivisor.Value != null)
        {
            // The + 0.002f is a hacky "fix". There's some weird rounding issue that has carried over from BAKKA,
            // most likely caused by ManagedBass or AvaloniaUI jank. If you increment NumericBeatValue up,
            // it's often not quite enough and it falls back to the value it was before.
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
        ChartInfoChartFilepath.Text = Path.GetFileName(ChartEditor.Chart.FilePath);
        ChartInfoAudioFilepath.Text = Path.GetFileName(ChartEditor.Chart.AudioFilePath);
        
        ChartInfoAuthor.Text = ChartEditor.Chart.Author;
        ChartInfoLevel.Value = (double)ChartEditor.Chart.Level;
        ChartInfoClearThreshold.Value = (double)ChartEditor.Chart.ClearThreshold;
        ChartInfoPreviewTime.Value = (double)ChartEditor.Chart.PreviewTime;
        ChartInfoPreviewLength.Value = (double)ChartEditor.Chart.PreviewLength;
        ChartInfoOffset.Value = (double)ChartEditor.Chart.Offset;
        ChartInfoMovieOffset.Value = (double)ChartEditor.Chart.MovieOffset;
    }

    public void SetQuickSettings()
    {
        QuickSettingsSliderHitsound.Value = UserConfig.AudioConfig.HitsoundVolume;
        QuickSettingsSliderMusic.Value = UserConfig.AudioConfig.MusicVolume;
        QuickSettingsNumericBeatDivision.Value = UserConfig.RenderConfig.BeatDivision;
        QuickSettingsNumericNoteSpeed.Value = (decimal?)UserConfig.RenderConfig.NoteSpeed;
        QuickSettingsCheckBoxShowHiSpeed.IsChecked = UserConfig.RenderConfig.ShowHiSpeed;
    }
    
    public void SetMinNoteSize(NoteType type)
    {
        int minimum = Note.MinSize(type);
        SliderNoteSize.Minimum = minimum;
        NumericNoteSize.Minimum = minimum;
        ChartEditor.Cursor.MinSize = minimum;

        SliderNoteSize.Value = double.Max(SliderNoteSize.Value, minimum);
    }

    public void UpdateAudioFilepath()
    {
        ChartInfoAudioFilepath.Text = Path.GetFileName(ChartEditor.Chart.AudioFilePath);
    }
    
    private void UpdateLoopMarkerPosition()
    {
        if (AudioManager.CurrentSong is null) return;

        double start = AudioManager.LoopStart * (SliderSongPosition.Bounds.Width - 25) / AudioManager.CurrentSong.Length + 12.5;
        double end = AudioManager.LoopEnd * (SliderSongPosition.Bounds.Width - 25) / AudioManager.CurrentSong.Length + 12.5;
        
        LoopMarkerStart.Margin = new(start, 0, 0, 0);
        LoopMarkerEnd.Margin = new(end, 0, 0, 0);
    }

    private void ResetLoopMarkers(uint length)
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
                    ChartEditor.EndHold(true);
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEndHold"]))
        {
            ChartEditor.EndHold(true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorEditHold"]))
        {
            ChartEditor.EditHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBakeHold"]))
        {
            ChartEditor.BakeHold((MathExtensions.HoldEaseType)HoldEaseComboBox.SelectedIndex, false);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBakeHoldNoRender"]))
        {
            ChartEditor.BakeHold((MathExtensions.HoldEaseType)HoldEaseComboBox.SelectedIndex, true);
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorStitchHold"]))
        {
            ChartEditor.StitchHold();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorInsertHoldSegment"]))
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
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorSelectHoldReferences"]))
        {
            ChartEditor.SelectHoldReferences();
            e.Handled = true;
            return;
        }
        if (Keybind.Compare(keybind, UserConfig.KeymapConfig.Keybinds["EditorBoxSelect"]))
        {
            ChartEditor.RunBoxSelect();
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
            ChartEditor.DeleteGimmick();
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
            
            ChartEditor.RunBoxSelect(RenderEngine.GetMeasureDecimalAtPointer(ChartEditor.Chart, point));
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
            if ((Note?)RenderEngine.GetChartElementAtPointer(ChartEditor.Chart, point, false, ChartEditor.LayerNoteActive, ChartEditor.LayerMaskActive, ChartEditor.LayerGimmickActive) is not { } note) return;
            
            if (pointerMoved && note == ChartEditor.LastSelectedNote) return;
            
            ChartEditor.SelectNote(note);
            ChartEditor.LastSelectedNote = note;
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            // Highlighting
            if (RenderEngine.GetChartElementAtPointer(ChartEditor.Chart, point, true, ChartEditor.LayerNoteActive, ChartEditor.LayerMaskActive, ChartEditor.LayerGimmickActive) is not { } note) return;
            
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
                UpdateAudioFilepath();
                RenderEngine.UpdateVisibleTime();
                ClearAutosaves();
                ResetLoopMarkers(AudioManager.CurrentSong?.Length ?? 0);
            }
        });
    }

    private async void MenuItemOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!await PromptSave()) return;
        
        // Get .mer file
        IStorageFile? file = await OpenChartFilePicker();
        if (file == null) return;

        AudioManager.ResetSong();
        OpenChart(file.Path.LocalPath);
    }
    
    private async void MenuItemSave_OnClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile(ChartEditor.Chart.IsNew, ChartEditor.Chart.FilePath);
    }

    private async void MenuItemSaveAs_OnClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile(true, ChartEditor.Chart.FilePath);
    }

    private async void MenuItemExportMercury_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.Chart.EndOfChart == null)
        {
            ShowWarningMessage(Assets.Lang.Resources.Generic_EndOfChartWarning, Assets.Lang.Resources.Generic_EndOfChartWarningExplanation);
            return;
        }
        
        await ExportFile(ChartWriteType.Mercury);
    }

    private async void MenuItemExportSaturn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.Chart.EndOfChart == null)
        {
            ShowWarningMessage(Assets.Lang.Resources.Generic_EndOfChartWarning, Assets.Lang.Resources.Generic_EndOfChartWarningExplanation);
            return;
        }
        
        await ExportFile(ChartWriteType.Saturn);
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
                ["ContentDialogMaxWidth"] = 730
            }
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

        CanShutdown = true;
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void MenuItemUndo_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Undo();

    private void MenuItemRedo_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Redo();

    private void MenuItemCut_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Cut();

    private void MenuItemCopy_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Copy();

    private void MenuItemPaste_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.Paste();

    private void MenuItemSelectAll_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.SelectAllNotes();
    }

    private void MenuItemDeselectAll_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.DeselectAllNotes();
    }

    private void MenuItemSelectHoldReferences_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.SelectHoldReferences();
    }
    
    private void MenuItemSelectHighlightedNote_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.SelectHighlightedNote();
    }

    private void MenuItemBoxSelect_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.EditorState is not ChartEditorState.InsertNote) return;
        ChartEditor.RunBoxSelect();
    }
    
    private void MenuItemMirrorChart_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.MirrorChart((int?)NumericMirrorAxis.Value ?? 30);

    private void MenuItemShiftChart_OnClick(object? sender, RoutedEventArgs e)
    {
        ToolsView_ShiftChart shiftView = new();
        ContentDialog dialog = new()
        {
            Content = shiftView,
            Title = Assets.Lang.Resources.Editor_AddGimmick,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Ok
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
                CloseButtonText = Assets.Lang.Resources.Generic_No
            };
            
            return dialog.ShowAsync();
        }
    }

    private void MenuItemGenerateJaggedHolds_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ChartEditor.SelectedNotes.Exists(x => x.IsHold))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NoHoldsSelected, Assets.Lang.Resources.Editor_NoHoldsSelectedTip);
            return;
        }
        
        ToolsView_GenerateJaggedHolds generatorView = new();
        ContentDialog dialog = new()
        {
            Content = generatorView,
            Title = Assets.Lang.Resources.Menu_JaggedHolds,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Generate
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
        if (!ChartEditor.SelectedNotes.Exists(x => x.IsHold))
        {
            ShowWarningMessage(Assets.Lang.Resources.Editor_NoHoldsSelected, Assets.Lang.Resources.Editor_NoHoldsSelectedTip);
            return;
        }
        
        ToolsView_ReconstructHolds reconstructView = new();
        ContentDialog dialog = new()
        {
            Content = reconstructView,
            Title = Assets.Lang.Resources.Menu_ReconstructHolds,
            CloseButtonText = Assets.Lang.Resources.Generic_Cancel,
            PrimaryButtonText = Assets.Lang.Resources.Generic_Generate
        };
        
        Dispatcher.UIThread.Post(async () =>
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result is not ContentDialogResult.Primary) return;
            
            ChartEditor.ReconstructHold(reconstructView.GeneratorMethod.SelectedIndex, (int?)reconstructView.Interval.Value ?? 0);
        });
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
            PrimaryButtonText = Assets.Lang.Resources.Generic_Create
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
        ChartEditor.InsertNote();
    }

    private void ButtonEditHold_OnClick(object? sender, RoutedEventArgs e)
    {
        ChartEditor.EditHold();
    }

    private void ButtonEndHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EndHold(true);
    
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
        SetPlayState(AudioManager.CurrentSong.IsPlaying ? PlayerState.Paused : PlayerState.Playing);
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
        UpdateAudioFilepath();
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
    
    private void ChartInfoPlayPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ChartEditor.Chart.PreviewLength == 0 || AudioManager.CurrentSong == null) return;
        
        AudioManager.CurrentSong.Position = (uint)(ChartEditor.Chart.PreviewTime * 1000);
        UpdateTime(TimeUpdateSource.Timer);
        SetPlayState(PlayerState.Preview);
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
    
    private void QuickSettingsShowHiSpeed_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UserConfig.RenderConfig.ShowHiSpeed = QuickSettingsCheckBoxShowHiSpeed.IsChecked ?? true;
    }
    
    private void ButtonEditSelectionShape_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(true, false);
    
    private void ButtonEditSelectionProperties_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(false, true);
    
    private void ButtonEditSelectionFull_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditSelection(true, true);

    private void ButtonMirrorSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.MirrorSelection((int?)NumericMirrorAxis.Value ?? 30);
    
    private void ButtonDeleteSelection_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeleteSelection();

    private void ButtonBakeHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.BakeHold((MathExtensions.HoldEaseType)HoldEaseComboBox.SelectedIndex, false);

    private void ButtonStitchHold_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.StitchHold();
    
    private void ButtonInsertHoldSegment_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.InsertHoldSegment();
    
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
    
    private void ButtonEditGimmick_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.EditGimmick();

    private void ButtonDeleteGimmick_OnClick(object? sender, RoutedEventArgs e) => ChartEditor.DeleteGimmick();
    
    private void ButtonSetLoopStart_OnClick(object? sender, RoutedEventArgs e) => SetLoopMarkerStart();

    private void ButtonSetLoopEnd_OnClick(object? sender, RoutedEventArgs e) => SetLoopMarkerEnd();

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
        SetMenuItemInputGestureText(); // Update InputGesture text in case stuff was rebound
        SetQuickSettings();
        RenderEngine.UpdateBrushes();
        RenderEngine.UpdateVisibleTime();
        AudioManager.LoadHitsoundSamples();
        AudioManager.UpdateVolume();
        
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
            PrimaryButtonText = Assets.Lang.Resources.Generic_Ok
        };

        dialog.ShowAsync();
    }
    
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

        if (ChartEditor.Chart.StartTimeSig != null)
        {
            int clicks = ChartEditor.Chart.StartTimeSig.TimeSig.Upper;
            float interval = 1.0f / clicks;

            AudioManager.MetronomeIndex = (int)float.Ceiling(ChartEditor.CurrentMeasureDecimal / interval);
        }
    }
    
    /// <summary>
    /// Prompts the user to save their work.
    /// </summary>
    /// <returns>True if file is saved.</returns>
    private async Task<bool> PromptSave()
    {
        if (ChartEditor.Chart.IsSaved) return true;

        ContentDialogResult result = await showSavePrompt();

        return result switch
        {
            ContentDialogResult.Primary => await SaveFile(false, ChartEditor.Chart.FilePath),
            ContentDialogResult.Secondary => true,
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
        IReadOnlyList<IStorageFile> result = await GetStorageProvider().OpenFilePickerAsync(new()
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
    
    public async Task<IStorageFile?> OpenChartFilePicker()
    {
        IReadOnlyList<IStorageFile> result = await GetStorageProvider().OpenFilePickerAsync(new()
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
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
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
        UpdateAudioFilepath();
        RenderEngine.UpdateVisibleTime();
        ResetLoopMarkers(AudioManager.CurrentSong?.Length ?? 0);
    }
    
    public async Task<bool> SaveFile(bool openFilePicker, string filepath)
    {
        if (openFilePicker || string.IsNullOrEmpty(filepath))
        {
            IStorageFile? file = await SaveChartFilePicker(ChartWriteType.Editor);

            if (file == null) return false;
            filepath = file.Path.LocalPath;
        }

        if (string.IsNullOrEmpty(filepath)) return false;

        ChartEditor.Chart.WriteFile(filepath, ChartWriteType.Editor);
        ChartEditor.Chart.FilePath = filepath;
        ChartEditor.Chart.IsNew = false;
        ChartEditor.Chart.IsSaved = true;
        ClearAutosaves();
        SetChartInfo();
        return true;
    }

    public async Task ExportFile(ChartWriteType chartWriteType)
    {
        IStorageFile? file = await SaveChartFilePicker(chartWriteType);

        if (file == null) return;
        string filepath = file.Path.LocalPath;
        
        if (string.IsNullOrEmpty(filepath)) return;
        ChartEditor.Chart.WriteFile(filepath, chartWriteType);
    }
}