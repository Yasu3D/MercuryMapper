using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Flac;
using System;

namespace MercuryMapper.Audio;

/// <summary>
/// Taken straight from BAKKA Avalonia, just cleaned up a little.
/// </summary>
public class BassSoundEngine
{
    private static bool bassInitialized;

    public BassSoundEngine()
    {
        if (bassInitialized) return;
        
        if (!Bass.Init(Flags: DeviceInitFlags.Latency)) throw new("Couldn't initialize ManagedBass");
        Bass.UpdatePeriod = 100;
        Bass.PlaybackBufferLength = 150;
        bassInitialized = true;
    }

    public static BassSound? GetSound(string filepath, bool loop, bool startPaused)
    {
        // attempt loading flac first
        int decodingChannel = BassFlac.CreateStream(filepath, 0, 0, BassFlags.Decode | BassFlags.Prescan);
        
        // load normally if that failed
        if (decodingChannel == 0 && Bass.LastError == Errors.FileFormat)
            decodingChannel = Bass.CreateStream(filepath, 0, 0, BassFlags.Decode | BassFlags.Prescan);
        
        // explode if that failed too
        if (decodingChannel == 0) throw new($"Couldn't load selected audio file: {Bass.LastError}");

        int bassChannel = BassFx.TempoCreate(decodingChannel, BassFlags.FxFreeSource);
        if (bassChannel == 0)
        {
            // bruh
            Bass.StreamFree(decodingChannel);
            return null;
        }

        BassSound? bassSound = new(bassChannel);
        if (loop) Bass.ChannelAddFlag(bassChannel, BassFlags.Loop);
        if (startPaused) bassSound.IsPlaying = false;
        return bassSound;
    }

    public static float GetLatency()
    {
        bool hasBassInfo = Bass.GetInfo(out BassInfo bassInfo);
        return hasBassInfo ? bassInfo.Latency : 0;
    }
}
