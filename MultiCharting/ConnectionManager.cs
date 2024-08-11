using Avalonia.Threading;
using MercuryMapper.Editor;
using MercuryMapper.Views;
using System;
using System.IO;
using System.Net.WebSockets;
using Websocket.Client;

enum RequestTypes : uint
{
    // 0XX - Host, Join, and Sync
    CreateLobby = 000,
    LobbyCreated = 001,
    JoinLobby = 002,
    LeaveLobby = 003,
    SyncRequest = 004,
    SongFile = 005,
    ChartFile = 006,
    SyncDone = 007,
    BadLobbyCode = 008,

    // 1XX - Metadata
    ChartAuthorChange = 100,
    ChartLevelChange = 101,
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

namespace MercuryMapper.MultiCharting
{
    public class ConnectionManager
    {
        private readonly string requestTypeFormat = "000";
        private readonly int requestTypeLength = 3;
        private readonly int hexColorLength = 9;

        private WebsocketClient? webSocketClient;
        private bool connectionGood = false;
        private MainView mainView;
        private PeerManager peerManager => mainView.PeerManager;
        private ChartEditor chartEditor => mainView.ChartEditor;

        public ConnectionManager(MainView main)
        {
            mainView = main;
        }

        public void CreateLobby(string Address, string Username, string Color)
        {
            Console.WriteLine("Attempting to create a lobby...");

            string Connection = CheckConnectionOrConnect(Address);

            if (Connection == "failed") return;

            SendMessage(RequestTypes.CreateLobby, Color + Username);
        }

        public void JoinLobby(string Address, string Username, string Color, string LobbyCode)
        {
            Console.WriteLine("Attempting to join a lobby...");

            string Connection = CheckConnectionOrConnect(Address);

            if (Connection == "failed") return;

            SendMessage(RequestTypes.JoinLobby, LobbyCode + Color + Username);
        }

        public void SendTimestamp(uint Timestamp)
        {
            SendMessage(RequestTypes.ClientTimestamp, Timestamp.ToString());
        }

        private string CheckConnectionOrConnect(string ServerUrl)
        {
            if (webSocketClient != null)
            {
                Console.WriteLine("WebSocket client already connected!");
                return "connected";
            }

            // Set up WebSocket client factory for custom protocol
            Func<ClientWebSocket> factory = new(() =>
            {
                ClientWebSocket client = new ClientWebSocket();

                client.Options.AddSubProtocol("mercury-multi-mapper");

                return client;
            });

            // Prep server Uri
            Uri ServerUri = new(ServerUrl);

            // Create client using Uri and client factory
            webSocketClient = new(ServerUri, factory);

            // Disable message timeout
            webSocketClient.ReconnectTimeout = null;

            // Log reconnects for now
            webSocketClient.ReconnectionHappened.Subscribe(info => Console.WriteLine($"Reconnection happened, type: {info.Type}"));

            // Log disconnects and destroy/clean up after disconnection
            webSocketClient.DisconnectionHappened.Subscribe(_ =>
            {
                Console.WriteLine("WebSocket connection closed.");
                connectionGood = false;
                webSocketClient.Dispose();
                webSocketClient = null;
            });

            // Set up to handle incoming messages
            webSocketClient.MessageReceived.Subscribe(Message => HandleMessage(Message));

            // Attempt client to server connection
            Console.WriteLine("Attempting connection...");

            try
            {
                webSocketClient.Start().Wait();

                // Announce connection to server
                SendMessage(null, "Hello MercuryMultiMapperServer!");

                return "connected";
            }
            catch
            {
                Console.WriteLine("Failed to connect!");
                return "failed";
            }
        }

        private void SendMessage(RequestTypes? ReqType, string ReqData)
        {
            if (webSocketClient == null) return;

            if (ReqType != null)
            {
                webSocketClient.Send(((uint)ReqType.Value).ToString(requestTypeFormat) + ReqData);
            } else
            {
                webSocketClient.Send(ReqData);
            }
        }

        private void SendSongFile(string RecievingClientID)
        {
            Byte[] bytes = File.ReadAllBytes(chartEditor.Chart.AudioFilePath);
            String fileData = Convert.ToBase64String(bytes);

            // I swear - if someone uses pipes in file names - they're insane.
            SendMessage(RequestTypes.SongFile, RecievingClientID + "|" + Path.GetFileName(chartEditor.Chart.AudioFilePath) + "|" + fileData);
        }

        private void SendChartFile(string RecievingClientID)
        {
            Byte[] bytes = File.ReadAllBytes(chartEditor.Chart.FilePath);
            String fileData = Convert.ToBase64String(bytes);

            // I swear - if someone uses pipes in file names - they're insane.
            SendMessage(RequestTypes.ChartFile, RecievingClientID + "|" + Path.GetFileName(chartEditor.Chart.FilePath) + "|" + fileData);
        }

        private void RecieveSongFile(string songData)
        {
            string fileName = songData.Substring(0, songData.IndexOf('|') - 1);
            string songFileData = songData.Substring(songData.IndexOf('|'));

            Byte[] fileData = Convert.FromBase64String(songFileData);
            File.WriteAllBytes("tmp/" + fileName, fileData);

            //SendMessage(RequestTypes.SongFile, RecievingClientID + "|" + fileData);
        }

        private void RecieveChartFile(string chartData)
        {
            Byte[] fileData = Convert.FromBase64String(chartData);
            File.WriteAllBytes("tempFilePath", fileData);

            //SendMessage(RequestTypes.ChartFile, RecievingClientID + "|" + fileData);
        }

        private void SyncWithClient(string recievingClientID)
        {
            SendSongFile(recievingClientID);
            SendChartFile(recievingClientID);
        }

        private void HandleMessage(ResponseMessage Message)
        {
            // Verify message type is the kind expected
            if (Message.MessageType != WebSocketMessageType.Text || Message.Text == null)
            {
                Console.WriteLine("Got some bad data on WebSocket connection!");
                return;
            }

            // Check for intial valid response and ignore data until it's recieved
            if (connectionGood == false)
            {
                if (Message.Text == "Hello MercuryMapper Client!")
                {
                    Console.WriteLine("Connected to server and got initial response!");
                    connectionGood = true;
                }

                return;
            }

            RequestTypes RequestType = (RequestTypes)uint.Parse(Message.Text.Substring(0, requestTypeLength));

            string TrimmedMessage = Message.Text.Substring(requestTypeLength);

            Console.WriteLine(RequestType);

            switch (RequestType)
            {
                case RequestTypes.LobbyCreated:
                    Console.WriteLine("Lobby created.");
                    break;
                case RequestTypes.JoinLobby:
                    AddClientTimestamp(TrimmedMessage);
                    Console.WriteLine("Got new peer data.");
                    break;
                case RequestTypes.SyncRequest:
                    //SyncWithClient(TrimmedMessage);
                    Console.WriteLine("Got sync request.");
                    break;
                case RequestTypes.ClientTimestamp:
                    SetClientTimestamp(TrimmedMessage);
                    Console.WriteLine("Got timestamp data.");
                    break;
                default:
                    Console.WriteLine("Got an unknown request type???");
                    break;
            }
        }

        private void SetClientTimestamp(string Message)
        {
            string[] SplitMessage = Message.Split('|');

            Dispatcher.UIThread.Post(() => peerManager.SetPeerMarkerTimestamp(int.Parse(SplitMessage[0]), uint.Parse(SplitMessage[1])));
        }

        private void AddClientTimestamp(string Message)
        {
            string[] SplitMessage = Message.Split('|');

            Dispatcher.UIThread.Post(() => peerManager.AddPeer(int.Parse(SplitMessage[0]), SplitMessage[1].Substring(hexColorLength), SplitMessage[1].Substring(0, hexColorLength)));
        }
    }
}
