using ManagedBass;

namespace MercuryMapper.Audio;

/// <summary>
/// Taken straight from BAKKA Avalonia, just cleaned up a little.
/// </summary>
public class BassSampleChannel
{
    private int bassChannel;

    public bool Loaded => bassChannel != 0;
    private readonly double frequency = 0;

    internal BassSampleChannel(int channel)
    {
        bassChannel = channel;
        frequency = Bass.ChannelGetAttribute(bassChannel, ChannelAttribute.Frequency);
    }

    public void SetVolume(float volume)
    {
        if (!Loaded) return;
        Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Volume, volume);
    }
    
    public void Play(bool restart = false)
    {
        if (!Loaded) return;
        Bass.ChannelPlay(bassChannel, restart);
    }

    public void Reset()
    {
        bassChannel = 0;
    }
}