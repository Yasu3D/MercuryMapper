using ManagedBass;

namespace MercuryMapper.Audio;

/// <summary>
/// Taken straight from BAKKA Avalonia, just cleaned up a little.
/// </summary>
public class BassSample
{
    private const int HitsoundChannelCount = 1;
    private int channelIndex;
    private readonly BassSampleChannel[] channels = new BassSampleChannel[HitsoundChannelCount];
    private int sample;

    public bool Loaded => sample != 0;

    public BassSample(string filepath)
    {
        sample = Bass.SampleLoad(filepath, 0, 0, HitsoundChannelCount, BassFlags.Default);
        if (!Loaded) return;

        for (int i = 0; i < HitsoundChannelCount; i++)
        {
            channels[i] = new BassSampleChannel(Bass.SampleGetChannel(sample));
        }
    }

    public BassSampleChannel? GetChannel()
    {
        if (!Loaded) return null;
        int index = channelIndex++ % HitsoundChannelCount;
        return channels[index];
    }

    public void Free()
    {
        if (!Loaded) return;
        foreach (BassSampleChannel channel in channels)
        {
            channel.Reset();
        }

        Bass.SampleFree(sample);
        sample = 0;
    }
}