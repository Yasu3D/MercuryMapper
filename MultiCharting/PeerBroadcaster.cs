using System;
using System.Threading;
using MercuryMapper.Views;

namespace MercuryMapper.MultiCharting;

public class PeerBroadcaster
{
    public PeerBroadcaster(MainView main)
    {
        mainView = main;
        timestampTimer = new(TimestampTimer_Tick, null, TimeSpan.FromMilliseconds(TimestampTickrate), TimeSpan.FromMilliseconds(TimestampTickrate));
    }

    private readonly MainView mainView;
    private const int TimestampTickrate = 200;
    private Timer timestampTimer;
    private uint lastTimestamp;

    private void TimestampTimer_Tick(object? sender)
    {
        if (mainView.AudioManager.CurrentSong == null) return;
        if (mainView.AudioManager.CurrentSong.Position == lastTimestamp) return;

        ConnectionManager.SendTimestamp(mainView.AudioManager.CurrentSong.Position);

        lastTimestamp = mainView.AudioManager.CurrentSong.Position;
    }
}