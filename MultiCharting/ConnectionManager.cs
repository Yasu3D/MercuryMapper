using Avalonia.Threading;
using MercuryMapper.Editor;
using MercuryMapper.Views;
using System;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
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
        
        public string LobbyCode = "";
        private string audioFilePath = "";
        private string receivedChartData = "";

        private bool audioReceived;
        private bool chartReceived;
        
        public NetworkConnectionState NetworkState = NetworkConnectionState.Local;
        public enum NetworkConnectionState
        {
            Local,
            Host,
            Client 
        }
        
        // UI
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
                        mainView.MenuItemCreateSession.IsEnabled = true;
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

        // Lobby Setup
        public void CreateLobby(string address, string username, string color)
        {
            string connection = CheckConnectionOrConnect(address);

            if (connection == "failed")
            {
                Dispatcher.UIThread.Post(() => mainView.ShowWarningMessage(Assets.Lang.Resources.Online_ConnectionFailed, Assets.Lang.Resources.Online_ConnectionFailedDetails));
                return;
            }

            SendMessage(MessageTypes.CreateLobby, color + username);
        }

        public void JoinLobby(string address, string username, string color, string lobbyCode)
        {
            string connection = CheckConnectionOrConnect(address);

            if (connection == "failed") return;

            LobbyCode = lobbyCode.ToUpperInvariant();

            SendMessage(MessageTypes.JoinLobby, LobbyCode + color + username);
        }

        public void LeaveLobby()
        {
            if (webSocketClient == null || !connectionGood) return;

            SendMessage(MessageTypes.LeaveLobby, "");

            LobbyCode = "";

            webSocketClient.Dispose();
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

                PeerManager.RemoveAllPeers();
            });

            SetNetworkConnectionState(NetworkConnectionState.Local);
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
            
            // Prep server Uri and return false if it's invalid.
            Uri serverUri;
            
            try
            {
                serverUri = new(serverUrl);
            }
            catch
            {
                return "failed";
            }

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

        // File Transfer
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

        private void LoadChartAndAudio()
        {
            Dispatcher.UIThread.Post(() => mainView.OpenChartFromNetwork(receivedChartData, audioFilePath));
            
            mainView.HideReceivingDataMessage();
            SendMessage(MessageTypes.SyncDone, "");
        }
        
        // Messages
        public void SendMessage(MessageTypes? messageType, string messageData)
        {
            if (webSocketClient == null) return;
            
            if (messageType == null)
            {
                webSocketClient.Send(messageData);
                return;
            }

            webSocketClient.Send(((uint)messageType.Value).ToString(RequestTypeFormat) + messageData);
        }
        
        private void HandleMessage(ResponseMessage message)
        {
            // Verify message type is the kind expected
            if (message.MessageType != WebSocketMessageType.Text || message.Text == null) return;

            // Check for initial valid response and ignore data until it's received
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
                // Host, Join, Sync
                case MessageTypes.LobbyCreated:
                {
                    LobbyCode = trimmedMessage;
                    Dispatcher.UIThread.Post(() => {
                        mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionOpened} {LobbyCode}");
                    });
                    SetNetworkConnectionState(NetworkConnectionState.Host);
                    break;
                }

                case MessageTypes.JoinLobby:
                {
                    string[] joinSplitMessage = trimmedMessage.Split('|');
                    Dispatcher.UIThread.Post(() => PeerManager.AddPeer(int.Parse(joinSplitMessage[0]), joinSplitMessage[1][HexColorLength..], joinSplitMessage[1][..HexColorLength]));
                    break;
                }
                
                case MessageTypes.LeaveLobby:
                {
                    Dispatcher.UIThread.Post(() => PeerManager.RemovePeer(int.Parse(trimmedMessage)));
                    break;
                }
                
                case MessageTypes.BadLobbyCode:
                {
                    Dispatcher.UIThread.Post(() => mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_InvalidSessionCode}"));
                    LobbyCode = "";
                    webSocketClient?.Dispose();
                    break;
                }
                
                case MessageTypes.GoodLobbyCode:
                {
                    SetNetworkConnectionState(NetworkConnectionState.Client);
                    SendMessage(MessageTypes.SyncRequest, "");
                    mainView.ShowReceivingDataMessage();
                    break;
                }
                
                case MessageTypes.SyncRequest:
                {
                    SendChartData(trimmedMessage);
                    SendSongFile(trimmedMessage);
                    break;
                }
                
                case MessageTypes.ChartData:
                {
                    receivedChartData = trimmedMessage;

                    // Report back that this data has been received.
                    // Then check if the other was received, and begin loading if it was.
                    chartReceived = true;
                    if (audioReceived) LoadChartAndAudio();
                    break;
                }
                
                case MessageTypes.SongFile:
                {
                    string fileName = trimmedMessage[..trimmedMessage.IndexOf('|')];
                    audioFilePath = Path.GetFullPath("tmp/" + fileName);
            
                    string audioFileData = trimmedMessage[(trimmedMessage.IndexOf('|') + 1)..];
                    byte[] fileData = Convert.FromBase64String(audioFileData);
            
                    if (!Directory.Exists("tmp")) Directory.CreateDirectory("tmp");
            
                    File.WriteAllBytes(audioFilePath, fileData);

                    // Report back that this data has been received.
                    // Then check if the other was received, and begin loading if it was.
                    audioReceived = true;
                    if (chartReceived) LoadChartAndAudio();
                    break;
                }
                
                // Metadata
                case MessageTypes.ChartAuthorChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Author = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.LevelChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Level = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.ClearThresholdChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.ClearThreshold = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.PreviewStartChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewTime = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.PreviewLengthChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewLength = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.AudioOffsetChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Offset = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.MovieOffsetChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.MovieOffset = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.ClientTimestamp:
                {
                    string[] timestampSplitMessage = trimmedMessage.Split('|');
                    Dispatcher.UIThread.Post(() => PeerManager.SetPeerMarkerTimestamp(int.Parse(timestampSplitMessage[0]), uint.Parse(timestampSplitMessage[1])));
                    break;
                }
                
                case MessageTypes.LobbyClosed:
                {
                    webSocketClient?.Dispose();
                    break;
                }
                
                // Realtime Changes
                case MessageTypes.InsertNote:
                {
                    break;
                }
                
                case MessageTypes.InsertHoldNote:
                {
                    break;
                }
                
                case MessageTypes.InsertHoldSegment:
                {
                    break;
                }
                
                case MessageTypes.DeleteNote:
                {
                    break;
                }
                
                case MessageTypes.DeleteHoldNote:
                {
                    break;
                }
                
                case MessageTypes.EditNote:
                {
                    break;
                }
                
                case MessageTypes.BakeHold:
                {
                    break;
                }
                
                case MessageTypes.SplitHold:
                {
                    break;
                }
                
                case MessageTypes.StitchHold:
                {
                    break;
                }
                
                case MessageTypes.ConvertToInstantMask:
                {
                    break;
                }
                
                case MessageTypes.InsertGimmick:
                {
                    break;
                }
                
                case MessageTypes.EditGimmick:
                {
                    break;
                }
                
                case MessageTypes.DeleteGimmick:
                {
                    break;
                }
                
                default:
                {
                    Console.WriteLine($"^ Got an unknown or unhandled request type: {requestType}");
                    break;
                }
            }
        }
    }
}
