using System;
using ManagedBass;
using MercuryMapper.Config;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.Views;

namespace MercuryMapper.Audio;

public class AudioManager(MainView mainView)
{
    private readonly MainView mainView = mainView;
    public readonly BassSoundEngine SoundEngine = new();

    public BassSound? CurrentSong { get; private set; }
    
    private BassSampleChannel? touchHitsoundChannel;
    private BassSampleChannel? guideHitsoundChannel;
    private BassSampleChannel? swipeHitsoundChannel;
    private BassSampleChannel? bonusHitsoundChannel;
    private BassSampleChannel? rNoteHitsoundChannel;
    private BassSampleChannel? metronomeChannel;

    private BassSample? touchHitsoundSample;
    private BassSample? guideHitsoundSample;
    private BassSample? swipeHitsoundSample;
    private BassSample? bonusHitsoundSample;
    private BassSample? rNoteHitsoundSample;
    private BassSample? metronomeSample;

    public int HitsoundNoteIndex { get; set; }
    public int MetronomeIndex { get; set; }

    public uint LoopStart { get; set; }
    public uint LoopEnd { get; set; }
    public bool Loop { get; set; }

    public void ResetSong()
    {
        if (CurrentSong == null) return;
        if (CurrentSong.IsPlaying) mainView.SetPlayState(MainView.PlayerState.Paused);
        CurrentSong.Position = 0;
    }
    
    public void SetSong(string filepath, float volume, int tempo)
    {
        if (CurrentSong is { IsPlaying: true }) mainView.SetPlayState(MainView.PlayerState.Paused);
        
        CurrentSong = BassSoundEngine.GetSound(filepath, false, true);
        if (CurrentSong == null) return;
        
        CurrentSong.PlaybackSpeed = tempo;
        CurrentSong.Volume = volume;
    }

    public void UpdateVolume()
    {
        if (CurrentSong != null)
            CurrentSong.Volume = (float)(mainView.UserConfig.AudioConfig.MusicVolume * 0.01);
        
        touchHitsoundChannel?.SetVolume((float)(mainView.UserConfig.AudioConfig.HitsoundVolume * mainView.UserConfig.AudioConfig.TouchVolume * 0.0001));
        guideHitsoundChannel?.SetVolume((float)(mainView.UserConfig.AudioConfig.HitsoundVolume * mainView.UserConfig.AudioConfig.GuideVolume * 0.0001));
        swipeHitsoundChannel?.SetVolume((float)(mainView.UserConfig.AudioConfig.HitsoundVolume * mainView.UserConfig.AudioConfig.SwipeVolume * 0.0001));
        bonusHitsoundChannel?.SetVolume((float)(mainView.UserConfig.AudioConfig.HitsoundVolume * mainView.UserConfig.AudioConfig.BonusVolume * 0.0001));
        rNoteHitsoundChannel?.SetVolume((float)(mainView.UserConfig.AudioConfig.HitsoundVolume * mainView.UserConfig.AudioConfig.RNoteVolume * 0.0001));
        metronomeChannel?.SetVolume((float)(mainView.UserConfig.AudioConfig.HitsoundVolume * mainView.UserConfig.AudioConfig.MetronomeVolume * 0.0001));
    }

    public void LoadHitsoundSamples()
    {
        touchHitsoundSample = new(mainView.UserConfig.AudioConfig.TouchHitsoundPath);
        guideHitsoundSample = new(mainView.UserConfig.AudioConfig.GuideHitsoundPath);
        swipeHitsoundSample = new(mainView.UserConfig.AudioConfig.SwipeHitsoundPath);
        bonusHitsoundSample = new(mainView.UserConfig.AudioConfig.BonusHitsoundPath);
        rNoteHitsoundSample = new(mainView.UserConfig.AudioConfig.RNoteHitsoundPath);
        metronomeSample = new(mainView.UserConfig.AudioConfig.MetronomePath);
        
        touchHitsoundChannel = touchHitsoundSample.Loaded ? touchHitsoundSample.GetChannel() : null;
        guideHitsoundChannel = guideHitsoundSample.Loaded ? guideHitsoundSample.GetChannel() : null;
        swipeHitsoundChannel = swipeHitsoundSample.Loaded ? swipeHitsoundSample.GetChannel() : null;
        bonusHitsoundChannel = bonusHitsoundSample.Loaded ? bonusHitsoundSample.GetChannel() : null;
        rNoteHitsoundChannel = rNoteHitsoundSample.Loaded ? rNoteHitsoundSample.GetChannel() : null;
        metronomeChannel = metronomeSample.Loaded ? metronomeSample.GetChannel() : null;
    }
    
    public void PlayHitsound(Note note)
    {
        HitsoundNoteIndex++;
        
        bool mute = note.NoteType
            is NoteType.MaskAdd
            or NoteType.MaskRemove
            or NoteType.HoldSegment
            or NoteType.TraceStart
            or NoteType.TraceSegment
            or NoteType.TraceEnd;
        
        if (mute) return;

        guideHitsoundChannel?.Play(true);
        
        if (note.IsSnap || note.IsSlide) swipeHitsoundChannel?.Play(true);
        else touchHitsoundChannel?.Play(true);
        
        if (note.IsBonus) bonusHitsoundChannel?.Play(true);
        if (note.IsRNote) rNoteHitsoundChannel?.Play(true);
    }

    public void PlayMetronome()
    {
        MetronomeIndex++;
        metronomeChannel?.Play(true);
    }
}