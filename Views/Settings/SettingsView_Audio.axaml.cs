using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MercuryMapper.Config;

namespace MercuryMapper.Views.Settings;

public partial class SettingsView_Audio : UserControl
{
    public SettingsView_Audio(MainView mainView)
    {
        InitializeComponent();

        this.mainView = mainView;
        SetPaths();
        SetSliders();

        HitsoundOffsetNumeric.Value = (decimal)AudioConfig.HitsoundOffset;
    }

    private readonly MainView mainView;
    private AudioConfig AudioConfig => mainView.UserConfig.AudioConfig;

    private void SetPaths()
    {
        TextBoxTouch.Text = AudioConfig.TouchHitsoundPath;
        TextBoxGuide.Text = AudioConfig.GuideHitsoundPath;
        TextBoxSwipe.Text = AudioConfig.SwipeHitsoundPath;
        TextBoxBonus.Text = AudioConfig.BonusHitsoundPath;
        TextBoxRNote.Text = AudioConfig.RNoteHitsoundPath;
    }

    private void SetSliders()
    {
        SliderMusic.Value = AudioConfig.MusicVolume;
        SliderHitsound.Value = AudioConfig.HitsoundVolume;
        SliderTouch.Value = AudioConfig.TouchVolume;
        SliderGuide.Value = AudioConfig.GuideVolume;
        SliderSwipe.Value = AudioConfig.SwipeVolume;
        SliderBonus.Value = AudioConfig.BonusVolume;
        SliderRNote.Value = AudioConfig.RNoteVolume;
    }
    
    private async void Touch_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        AudioConfig.TouchHitsoundPath = file.Path.LocalPath;
        TextBoxTouch.Text = file.Path.LocalPath;
    }
    
    private void Touch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        AudioConfig.TouchHitsoundPath = TextBoxTouch.Text ?? "";
    }

    private async void Guide_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        AudioConfig.GuideHitsoundPath = file.Path.LocalPath;
        TextBoxGuide.Text = file.Path.LocalPath;
    }
    
    private void Guide_TextChanged(object? sender, TextChangedEventArgs e)
    {
        AudioConfig.GuideHitsoundPath = TextBoxGuide.Text ?? "";
    }

    private async void Swipe_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        AudioConfig.SwipeHitsoundPath = file.Path.LocalPath;
        TextBoxSwipe.Text = file.Path.LocalPath;
    }
    
    private void Swipe_TextChanged(object? sender, TextChangedEventArgs e)
    {
        AudioConfig.SwipeHitsoundPath = TextBoxSwipe.Text ?? "";
    }

    private async void Bonus_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        AudioConfig.BonusHitsoundPath = file.Path.LocalPath;
        TextBoxBonus.Text = file.Path.LocalPath;
    }
    
    private void Bonus_TextChanged(object? sender, TextChangedEventArgs e)
    {
        AudioConfig.BonusHitsoundPath = TextBoxBonus.Text ?? "";
    }

    private async void RNote_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        AudioConfig.RNoteHitsoundPath = file.Path.LocalPath;
        TextBoxRNote.Text = file.Path.LocalPath;
    }
    
    private void RNote_TextChanged(object? sender, TextChangedEventArgs e)
    {
        AudioConfig.RNoteHitsoundPath = TextBoxRNote.Text ?? "";
    }
    
    private async void Metronome_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        AudioConfig.MetronomePath = file.Path.LocalPath;
        TextBoxMetronome.Text = file.Path.LocalPath;
    }
    
    private void Metronome_TextChanged(object? sender, TextChangedEventArgs e)
    {
        AudioConfig.MetronomePath = TextBoxMetronome.Text ?? "";
    }
    
    private void SliderMusic_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.MusicVolume = SliderMusic.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void SliderHitsound_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.HitsoundVolume = SliderHitsound.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void SliderTouch_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.TouchVolume = SliderTouch.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void SliderGuide_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.GuideVolume = SliderGuide.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void SliderSwipe_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.SwipeVolume = SliderSwipe.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void SliderBonus_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.BonusVolume = SliderBonus.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void SliderRNote_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.RNoteVolume = SliderRNote.Value;
        mainView.AudioManager.UpdateVolume();
    }
    
    private void SliderMetronome_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        AudioConfig.MetronomeVolume = SliderMetronome.Value;
        mainView.AudioManager.UpdateVolume();
    }

    private void HitsoundOffsetNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        AudioConfig.HitsoundOffset = (float)(e.NewValue ?? 0);
    }
}