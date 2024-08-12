using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MercuryMapper.Views;
using SkiaSharp;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace MercuryMapper.MultiCharting;

public class Peer(int id, string username, Color color, Rectangle marker)
{
    public uint Timestamp;
    
    public readonly int Id = id;
    public readonly string Username = username;
    public readonly Color Color = color;
    public readonly SKColor SkiaColor = new(color.R, color.G, color.B, 0xCC);
    public readonly Rectangle Marker = marker;
}

public class PeerManager(MainView main)
{
    private readonly MainView mainView = main;
    public readonly Dictionary<int, Peer> Peers = [];
    
    public void AddPeer(int id, string username, string color)
    {
        Rectangle marker = new()
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 4,
            RadiusX = 2,
            RadiusY = 2,
            Fill = new SolidColorBrush { Color = Color.Parse(color) },
        };
        
        mainView.PanelClientMarker.Children.Add(marker);
        ToolTip.SetTip(marker, username);
        ToolTip.SetShowDelay(marker, 10);
        
        Peer newPeer = new(id, username, Color.Parse(color), marker);

        Peers.Add(id, newPeer);
    }

    public void RemovePeer(int id)
    {
        mainView.PanelClientMarker.Children.Remove(Peers[id].Marker);
        Peers.Remove(id);
    }

    public void RemoveAllPeers()
    {
        foreach (KeyValuePair<int, Peer> peer in Peers)
        {
            mainView.PanelClientMarker.Children.Remove(Peers[peer.Key].Marker);
            Peers.Remove(peer.Key);
        }
    }

    public void UpdatePeerMarkers()
    {
        if (mainView.AudioManager.CurrentSong == null) return;
        
        foreach (KeyValuePair<int, Peer> client in Peers)
        {
            client.Value.Marker.Margin = new(client.Value.Timestamp * (mainView.SliderSongPosition.Bounds.Width - 25) / mainView.AudioManager.CurrentSong.Length + 12.5, 0, 0, 0);
        }
    }
    
    public void SetPeerMarkerTimestamp(int id, uint timestamp)
    {
        if (mainView.AudioManager.CurrentSong == null) return;
        
        Peers[id].Timestamp = timestamp;
        Peers[id].Marker.Margin = new(timestamp * (mainView.SliderSongPosition.Bounds.Width - 25) / mainView.AudioManager.CurrentSong.Length + 12.5, 0, 0, 0);
    }
}