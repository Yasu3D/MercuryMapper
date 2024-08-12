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

        private readonly string requestTypeFormat = "000";
        private readonly int requestTypeLength = 3;
        private readonly int hexColorLength = 9;

        private WebsocketClient? webSocketClient;
        private bool connectionGood = false;
        private readonly MainView mainView = main;
        public string lobbyCode = "";
        private PeerManager PeerManager => mainView.PeerManager;
        private ChartEditor ChartEditor => mainView.ChartEditor;
        private bool recievedOneOfTwoFiles = false;
        private string songFilePath = "";
        private string recievedChartData = "";

        public void CreateLobby(string Address, string Username, string Color)
        {
            string Connection = CheckConnectionOrConnect(Address);

            if (Connection == "failed") return;

            SendMessage(MessageTypes.CreateLobby, Color + Username);
        }

        public void JoinLobby(string Address, string Username, string Color, string LobbyCode)
        {
            string Connection = CheckConnectionOrConnect(Address);

            if (Connection == "failed") return;

            lobbyCode = LobbyCode;

            SendMessage(MessageTypes.JoinLobby, LobbyCode + Color + Username);
        }

        public void LeaveLobby()
        {
            if (webSocketClient == null || !connectionGood) return;

            SendMessage(MessageTypes.LeaveLobby, "");

            lobbyCode = "";

            webSocketClient.Dispose();
        }

        public void SendTimestamp(uint Timestamp)
        {
            SendMessage(MessageTypes.ClientTimestamp, Timestamp.ToString());
        }

        private string CheckConnectionOrConnect(string ServerUrl)
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
            Uri ServerUri = new(ServerUrl);

            // Create client using Uri and client factory
            webSocketClient = new(ServerUri, factory)
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
            webSocketClient.MessageReceived.Subscribe(Message => HandleMessage(Message));

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

        public void SendMessage(MessageTypes? ReqType, string ReqData)
        {
            if (webSocketClient == null) return;

            if (ReqType == null && ReqData == null) return;

            if (ReqType == null)
            {
                webSocketClient.Send(ReqData);
                return;
            }

            webSocketClient.Send(((uint)ReqType.Value).ToString(requestTypeFormat) + ReqData);
        }

        private void SendSongFile(string RecievingClientID)
        {
            Byte[] bytes = File.ReadAllBytes(ChartEditor.Chart.AudioFilePath);
            String fileData = Convert.ToBase64String(bytes);

            // I swear - if someone uses pipes in file names - they're insane.
            SendMessage(MessageTypes.SongFile, RecievingClientID + "|" + Path.GetFileName(ChartEditor.Chart.AudioFilePath) + "|" + fileData);
        }

        private void SendChartData(string RecievingClientID)
        {
            String chartData = mainView.ChartEditor.Chart.WriteChartToNetwork();

            SendMessage(MessageTypes.ChartData, RecievingClientID + "|" + chartData);
        }

        private void RecieveSongFile(string songData)
        {
            string fileName = songData[..songData.IndexOf('|')];
            string songFileData = songData[(songData.IndexOf('|') + 1)..];

            songFilePath = Path.GetFullPath("tmp/" + fileName);

            Byte[] fileData = Convert.FromBase64String(songFileData);

            if (!Directory.Exists("tmp")) Directory.CreateDirectory("tmp");

            File.WriteAllBytes(songFilePath, fileData);

            TryToLoadRecievedMap();
        }

        private void RecieveChartData(string chartData)
        {
            recievedChartData = chartData;

            TryToLoadRecievedMap();
        }

        private void TryToLoadRecievedMap()
        {
            if (recievedOneOfTwoFiles == false)
            {
                recievedOneOfTwoFiles = true;
                return;
            }

            recievedOneOfTwoFiles = false;

            Dispatcher.UIThread.Post(() => {
                mainView.ChartEditor.LoadChartNetwork(recievedChartData);
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
                if (lobbyCode != "")
                {
                    mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionClosed}");
                    lobbyCode = "";
                }

                Dispatcher.UIThread.Post(() => PeerManager.RemoveAllPeers());
            });

            EnableButtons();
        }

        private void DisableButtons(bool notHosting)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (notHosting)
                {
                    mainView.MenuItemCreateSession.IsEnabled = false;
                    mainView.MenuItemMirrorChart.IsEnabled = false;
                    mainView.MenuItemShiftChart.IsEnabled = false;
                }
                mainView.MenuItemJoinSession.IsEnabled = false;
                mainView.MenuItemDisconnect.IsEnabled = true;
                mainView.MenuItemNew.IsEnabled = false;
                mainView.MenuItemOpen.IsEnabled = false;
            });
        }

        private void EnableButtons()
        {
            Dispatcher.UIThread.Post(() =>
            {
                mainView.MenuItemCreateSession.IsEnabled = true;
                mainView.MenuItemMirrorChart.IsEnabled = true;
                mainView.MenuItemShiftChart.IsEnabled = true;
                mainView.MenuItemJoinSession.IsEnabled = true;
                mainView.MenuItemDisconnect.IsEnabled = false;
                mainView.MenuItemNew.IsEnabled = true;
                mainView.MenuItemOpen.IsEnabled = true;
            });
        }

        private void HandleMessage(ResponseMessage Message)
        {
            // Verify message type is the kind expected
            if (Message.MessageType != WebSocketMessageType.Text || Message.Text == null) return;

            // Check for intial valid response and ignore data until it's recieved
            if (connectionGood == false)
            {
                if (Message.Text == "Hello MercuryMapper Client!") connectionGood = true;

                return;
            }

            MessageTypes RequestType = (MessageTypes)uint.Parse(Message.Text[..requestTypeLength]);

            string TrimmedMessage = Message.Text[requestTypeLength..];

            Console.WriteLine(RequestType);

            switch (RequestType)
            {
                case MessageTypes.LobbyCreated:
                    lobbyCode = TrimmedMessage;
                    Dispatcher.UIThread.Post(() => {
                        mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionOpened} {lobbyCode}");
                    });
                    DisableButtons(false);
                    break;
                case MessageTypes.JoinLobby:
                    string[] JoinSplitMessage = TrimmedMessage.Split('|');
                    Dispatcher.UIThread.Post(() => PeerManager.AddPeer(int.Parse(JoinSplitMessage[0]), JoinSplitMessage[1][hexColorLength..], JoinSplitMessage[1][..hexColorLength]));
                    break;
                case MessageTypes.LeaveLobby:
                    Dispatcher.UIThread.Post(() => PeerManager.RemovePeer(int.Parse(TrimmedMessage)));
                    break;
                case MessageTypes.BadLobbyCode:
                    Dispatcher.UIThread.Post(() => mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_InvalidSessionCode}"));
                    lobbyCode = "";
                    webSocketClient?.Dispose();
                    break;
                case MessageTypes.GoodLobbyCode:
                    DisableButtons(true);
                    SendMessage(MessageTypes.SyncRequest, "");
                    break;
                case MessageTypes.SyncRequest:
                    SendChartData(TrimmedMessage);
                    SendSongFile(TrimmedMessage);
                    break;
                case MessageTypes.ChartData:
                    RecieveChartData(TrimmedMessage);
                    break;
                case MessageTypes.SongFile:
                    RecieveSongFile(TrimmedMessage);
                    break;
                case MessageTypes.ChartAuthorChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Author = TrimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.LevelChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Level = Convert.ToDecimal(TrimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.ClearThresholdChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.ClearThreshold = Convert.ToDecimal(TrimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.PreviewStartChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewTime = Convert.ToDecimal(TrimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.PreviewLengthChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewLength = Convert.ToDecimal(TrimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.AudioOffsetChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Offset = Convert.ToDecimal(TrimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.MovieOffsetChange:
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.MovieOffset = Convert.ToDecimal(TrimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                case MessageTypes.ClientTimestamp:
                    string[] TimestampSplitMessage = TrimmedMessage.Split('|');
                    Dispatcher.UIThread.Post(() => PeerManager.SetPeerMarkerTimestamp(int.Parse(TimestampSplitMessage[0]), uint.Parse(TimestampSplitMessage[1])));
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
