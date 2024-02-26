using System;
using ManagedBass;

namespace MercuryMapper.Audio;

/// <summary>
/// Taken straight from BAKKA Avalonia, just cleaned up a little.
/// </summary>
public class BassSound
{
    public BassSound(int channel)
    {
        this.channel = channel;
        Bass.ChannelSetAttribute(this.channel, ChannelAttribute.TempoPreventClick, 1);
        double length = Bass.ChannelBytes2Seconds(this.channel, Bass.ChannelGetLength(this.channel));
        if (length < 0) throw new Exception($"Error getting song length: {Bass.LastError}");
        
        Length = (uint)length * 1000;
    }
    
    private uint position;
    private readonly int channel;
    private bool isPlaying = false;
    private int playbackSpeed = 100;
    private float volume = 1.0f;

    public uint Length { get; }
    
    public float Volume
    {
        get => volume;
        set
        {
            if (Math.Abs(volume - value) > 0.001f) Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);
            volume = value;
        }
    }

    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            SetPlayingState(value);
            isPlaying = value;
        }
    }

    public int PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            if (playbackSpeed != value) SetSpeed(value);
            playbackSpeed = value;
        }
    }
    
    private void SetSpeed(int value)
    {
        int tempo = value - 100;
        int pitch = (int)(value * 0.24);
        
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Tempo, tempo);
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Pitch, pitch);
    }
    
    private bool SetPosition(uint pos)
    {
        return Bass.ChannelSetPosition(channel, Bass.ChannelSeconds2Bytes(channel, pos * 0.001));
    }

    private uint GetPosition()
    {
        return (uint)Bass.ChannelBytes2Seconds(channel, Bass.ChannelGetPosition(channel)) * 1000;
    }
    
    private void SetPlayingState(bool playing)
    {
        if (!playing)
        {
            Bass.ChannelPause(channel);
            position = GetPosition();
        }
        else
        {
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);
            SetPosition(position);
            Bass.ChannelPlay(channel);
        }
    }
}