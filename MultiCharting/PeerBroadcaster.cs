using System;
using System.Globalization;
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
    private const int TimestampTickrate = 50; // Update 20 times a second
    private Timer timestampTimer;
    private uint lastTimestamp;

    private void TimestampTimer_Tick(object? sender)
    {
        if (mainView.AudioManager.CurrentSong == null) return;
        if (mainView.AudioManager.CurrentSong.Position == lastTimestamp) return;

        mainView.ConnectionManager.SendMessage(ConnectionManager.MessageTypes.ClientTimestamp, mainView.AudioManager.CurrentSong.Position.ToString(CultureInfo.InvariantCulture));

        lastTimestamp = mainView.AudioManager.CurrentSong.Position;
    }
}