using ManagedBass;
using MercuryMapper.Views;

namespace MercuryMapper.Audio;

public class AudioManager(MainView mainView)
{
    private readonly MainView mainView = mainView;
    public readonly BassSoundEngine SoundEngine = new();

    public BassSound? CurrentSong { get; private set; }

    public void SetSong(string filepath, float volume, int tempo)
    {
        if (CurrentSong is { IsPlaying: true }) mainView.SetPlayState(false);
        
        CurrentSong = BassSoundEngine.GetSound(filepath, false, true);
        if (CurrentSong == null) return;
        
        CurrentSong.PlaybackSpeed = tempo;
        CurrentSong.Volume = volume;
    }
}