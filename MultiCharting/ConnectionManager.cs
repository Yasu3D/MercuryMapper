using Avalonia.Threading;
using MercuryMapper.Editor;
using MercuryMapper.Views;
using System;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using FluentAvalonia.UI.Controls;
using Websocket.Client;

namespace MercuryMapper.MultiCharting
{
    public class ConnectionManager(MainView main)
    {
        private readonly MainView mainView = main;
        private PeerManager PeerManager => mainView.PeerManager;
        private ChartEditor ChartEditor => mainView.ChartEditor;
        
        public enum MessageTypes : uint
        {
            // 0XX - Host, Join, and Sync
            CreateLobby = 000,
            LobbyCreated = 001,
            LobbyClosed = 002,
            JoinLobby = 003,
            LeaveLobby = 004,
            BadLobbyCode = 005,
            GoodLobbyCode = 006,
            SyncRequest = 007,
            SongFile = 008,
            ChartData = 009,
            SyncDone = 010,

            // 1XX - Metadata
            ChartAuthorChange = 100,
            LevelChange = 101,
            ClearThresholdChange = 102,
            PreviewStartChange = 103,
            PreviewLengthChange = 104,
            AudioOffsetChange = 105,
            MovieOffsetChange = 106,

            // 2XX - Realtime Events
            InsertNote = 200,
            InsertHoldNote = 201,
            InsertHoldSegment = 202,
            DeleteNote = 203,
            DeleteHoldNote = 204,
            EditNote = 205,
            BakeHold = 206,
            SplitHold = 207,
            StitchHold = 208,
            ConvertToInstantMask = 209,
            InsertGimmick = 210,
            EditGimmick = 211,
            DeleteGimmick = 212,
            ClientTimestamp = 213
        }

        private const string RequestTypeFormat = "000";
        private const int RequestTypeLength = 3;
        private const int HexColorLength = 9;

        private WebsocketClient? webSocketClient;
        private bool connectionGood;
        private bool receivedOneOfTwoFiles;
        
        public string LobbyCode = "";
        private string songFilePath = "";
        private string receivedChartData = "";
        
        public NetworkConnectionState NetworkState = NetworkConnectionState.Local;
        public enum NetworkConnectionState
        {
            Local,
            Host,
            Client 
        }
        
        public void SetNetworkConnectionState(NetworkConnectionState connectionState)
        {
            NetworkState = connectionState;

            Dispatcher.UIThread.Post(() =>
            {
                switch (connectionState)
                {
                    case NetworkConnectionState.Local:
                    {
                        mainView.MenuItemCreateSession.IsEnabled = true;
                        mainView.MenuItemJoinSession.IsEnabled = true;
                        mainView.MenuItemDisconnect.IsEnabled = false;
                        
                        mainView.MenuItemMirrorChart.IsEnabled = true;
                        mainView.MenuItemShiftChart.IsEnabled = true;
                        
                        mainView.MenuItemNew.IsEnabled = true;
                        mainView.MenuItemOpen.IsEnabled = true;
                    
                        break;
                    }
            
                    case NetworkConnectionState.Host:
                    {
                        mainView.MenuItemCreateSession.IsEnabled = false;
                        mainView.MenuItemJoinSession.IsEnabled = false;
                        mainView.MenuItemDisconnect.IsEnabled = true;
                        
                        mainView.MenuItemMirrorChart.IsEnabled = true;
                        mainView.MenuItemShiftChart.IsEnabled = true;
                        
                        mainView.MenuItemNew.IsEnabled = false;
                        mainView.MenuItemOpen.IsEnabled = false;
                        
                        break;
                    }
            
                    case NetworkConnectionState.Client:
                    {
                        mainView.MenuItemCreateSession.IsEnabled = false;
                        mainView.MenuItemJoinSession.IsEnabled = false;
                        mainView.MenuItemDisconnect.IsEnabled = true;
                        
                        mainView.MenuItemMirrorChart.IsEnabled = false;
                        mainView.MenuItemShiftChart.IsEnabled = false;
                        
                        mainView.MenuItemNew.IsEnabled = false;
                        mainView.MenuItemOpen.IsEnabled = false;
                        
                        break;
                    }
                }
            });
        }

        public void CreateLobby(string address, string username, string color)
        {
            string connection = CheckConnectionOrConnect(address);

            if (connection == "failed") return;

            SendMessage(MessageTypes.CreateLobby, color + username);
        }

        public void JoinLobby(string address, string username, string color, string lobbyCode)
        {
            string connection = CheckConnectionOrConnect(address);

            if (connection == "failed") return;

            LobbyCode = lobbyCode;

            SendMessage(MessageTypes.JoinLobby, lobbyCode + color + username);
            
            mainView.ShowReceivingDataMessage();
        }

        public void LeaveLobby()
        {
            if (webSocketClient == null || !connectionGood) return;

            SendMessage(MessageTypes.LeaveLobby, "");

            LobbyCode = "";

            webSocketClient.Dispose();
        }

        public void SendTimestamp(uint timestamp)
        {
            SendMessage(MessageTypes.ClientTimestamp, timestamp.ToString());
        }

        private string CheckConnectionOrConnect(string serverUrl)
        {
            if (webSocketClient != null)
            {
                return "connected";
            }

            // Set up WebSocket client factory for custom protocol
            Func<ClientWebSocket> factory = new(() =>
            {
                ClientWebSocket client = new();

                client.Options.AddSubProtocol("mercury-multi-mapper");

                return client;
            });

            // Prep server Uri
            Uri serverUri = new(serverUrl);

            // Create client using Uri and client factory
            webSocketClient = new(serverUri, factory)
            {
                // Disable message timeout
                ReconnectTimeout = null,

                // Disable reconnect on disconnect
                IsReconnectionEnabled = false
            };

            // Log disconnects and destroy/clean up after disconnection
            webSocketClient.DisconnectionHappened.Subscribe(_ =>
            {
                HandleDisconnect();
            });

            // Set up to handle incoming messages
            webSocketClient.MessageReceived.Subscribe(HandleMessage);

            try
            {
                webSocketClient.Start().Wait();

                // Announce connection to server
                SendMessage(null, "Hello MercuryMultiMapperServer!");

                return "connected";
            }
            catch
            {
                return "failed";
            }
        }

        public void SendMessage(MessageTypes? reqType, string reqData)
        {
            if (webSocketClient == null) return;
            
            if (reqType == null)
            {
                webSocketClient.Send(reqData);
                return;
            }

            webSocketClient.Send(((uint)reqType.Value).ToString(RequestTypeFormat) + reqData);
        }

        private void SendSongFile(string receivingClientId)
        {
            byte[] bytes = File.ReadAllBytes(ChartEditor.Chart.AudioFilePath);
            string fileData = Convert.ToBase64String(bytes);

            // I swear - if someone uses pipes in file names - they're insane.
            SendMessage(MessageTypes.SongFile, receivingClientId + "|" + Path.GetFileName(ChartEditor.Chart.AudioFilePath) + "|" + fileData);
        }

        private void SendChartData(string receivingClientId)
        {
            string chartData = mainView.ChartEditor.Chart.WriteChartToNetwork();

            SendMessage(MessageTypes.ChartData, receivingClientId + "|" + chartData);
        }

        private async void ReceiveSongFile(string songData)
        {
            string fileName = songData[..songData.IndexOf('|')];
            songFilePath = Path.GetFullPath("tmp/" + fileName);
            
            string songFileData = songData[(songData.IndexOf('|') + 1)..];
            byte[] fileData = Convert.FromBase64String(songFileData);
            
            if (!Directory.Exists("tmp")) Directory.CreateDirectory("tmp");
            
            await File.WriteAllBytesAsync(songFilePath, fileData);
            
            mainView.HideReceivingDataMessage();

            TryToLoadReceivedChart();
        }

        private void ReceiveChartData(string chartData)
        {
            receivedChartData = chartData;

            TryToLoadReceivedChart();
        }

        private void TryToLoadReceivedChart()
        {
            if (receivedOneOfTwoFiles == false)
            {
                receivedOneOfTwoFiles = true;
                return;
            }

            receivedOneOfTwoFiles = false;

            Dispatcher.UIThread.Post(() => {
                mainView.ChartEditor.LoadChartNetwork(receivedChartData);
                mainView.ChartEditor.Chart.AudioFilePath = songFilePath;
                mainView.AudioManager.SetSong(mainView.ChartEditor.Chart.AudioFilePath, (float)(mainView.UserConfig.AudioConfig.MusicVolume * 0.01), (int)mainView.SliderPlaybackSpeed.Value);
                mainView.SetSongPositionSliderValues();
                mainView.UpdateAudioFilepath();
                mainView.RenderEngine.UpdateVisibleTime();
                mainView.ResetLoopMarkers(mainView.AudioManager.CurrentSong?.Length ?? 0);
            });

            SendMessage(MessageTypes.SyncDone, "");
        }

        private void HandleDisconnect()
        {
            if (webSocketClient == null) return;

            connectionGood = false;
            webSocketClient = null;

            Dispatcher.UIThread.Post(() => {
                if (LobbyCode != "")
                {
                    mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionClosed}");
                    LobbyCode = "";
                }

                Dispatcher.UIThread.Post(() => PeerManager.RemoveAllPeers());
            });

            SetNetworkConnectionState(NetworkConnectionState.Local);
        }

        private void HandleMessage(ResponseMessage message)
        {
            // Verify message type is the kind expected
            if (message.MessageType != WebSocketMessageType.Text || message.Text == null) return;

            // Check for intial valid response and ignore data until it's recieved
            if (connectionGood == false)
            {
                if (message.Text == "Hello MercuryMapper Client!") connectionGood = true;

                return;
            }

            MessageTypes requestType = (MessageTypes)uint.Parse(message.Text[..RequestTypeLength]);

            string trimmedMessage = message.Text[RequestTypeLength..];

            Console.WriteLine(requestType);

            switch (requestType)
            {
                case MessageTypes.LobbyCreated:
                    LobbyCode = trimmedMessage;
                    Dispatcher.UIThread.Post(() => {
                        mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionOpened} {LobbyCode}");
                    });
                    SetNetworkConnectionState(NetworkConnectionState.Host);
                    break;
                case MessageTypes.JoinLobby:
                    string[] joinSplitMessage = trimmedMessage.Split('|');
                    Dispatcher.UIThread.Post(() => PeerManager.AddPeer(int.Parse(joinSplitMessage[0]), joinSplitMessage[1][HexColorLength..], joinSplitMessage[1][..HexColorLength]));
                    break;
                case MessageTypes.LeaveLobby:
                    Dispatcher.UIThread.Post(() => PeerManager.RemovePeer(int.Parse(trimmedMessage)));
                    break;
                case MessageTypes.BadLobbyCode:
                    Dispatcher.UIThread.Post(() => mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_InvalidSessionCode}"));
                    LobbyCode = "";
                    webSocketClient?.Dispose();
                    break;
                case MessageTypes.GoodLobbyCode:
                    SetNetworkConnectionState(NetworkConnectionState.Client);
                    SendMessage(MessageTypes.SyncRequest, "");
                    break;
                case MessageTypes.SyncRequest:
                    SendChartData(trimmedMessage);
                    SendSongFile(trimmedMessage);
                    break;
                case MessageTypes.ChartData:
                    ReceiveChartData(trimmedMessage);
                    break;
                case MessageTypes.SongFile:
                    ReceiveSongFile(trimmedMessage);
                    break;
                case MessageTypes.ChartAuthorChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Author = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.LevelChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Level = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.ClearThresholdChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.ClearThreshold = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.PreviewStartChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewTime = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.PreviewLengthChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewLength = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.AudioOffsetChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Offset = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.MovieOffsetChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.MovieOffset = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.ClientTimestamp:
                    string[] timestampSplitMessage = trimmedMessage.Split('|');
                    Dispatcher.UIThread.Post(() => PeerManager.SetPeerMarkerTimestamp(int.Parse(timestampSplitMessage[0]), uint.Parse(timestampSplitMessage[1])));
                    break;
                case MessageTypes.LobbyClosed:
                    webSocketClient?.Dispose();
                    break;
                default:
                    Console.WriteLine("^ Got an unknown request type???");
                    break;
            }
        }
    }
}
