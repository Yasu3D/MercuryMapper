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
    internal static class ConnectionManager
    {
        private static readonly string RequestTypeFormat = "000";
        private static readonly int RequestTypeLength = 3;
        private static readonly int HexColorLength = 9;

        private static WebsocketClient? WebSocketClient;
        private static bool connectionGood = false;

        public static void CreateLobby(string Address, string Username, string Color)
        {
            Console.WriteLine("Attempting to create a lobby...");

            string Connection = CheckConnectionOrConnect(Address);

            if (Connection == "failed") return;

            SendMessage(RequestTypes.CreateLobby, Color + Username);
        }

        public static void JoinLobby(string Address, string Username, string Color, string LobbyCode)
        {
            Console.WriteLine("Attempting to join a lobby...");

            string Connection = CheckConnectionOrConnect(Address);

            if (Connection == "failed") return;

            SendMessage(RequestTypes.JoinLobby, LobbyCode + Color + Username);
        }

        public static void SendTimestamp(uint Timestamp)
        {
            SendMessage(RequestTypes.ClientTimestamp, Timestamp.ToString());
        }

        private static string CheckConnectionOrConnect(string ServerUrl)
        {
            if (WebSocketClient != null)
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
            WebSocketClient = new(ServerUri, factory);

            // Disable message timeout
            WebSocketClient.ReconnectTimeout = null;

            // Log reconnects for now
            WebSocketClient.ReconnectionHappened.Subscribe(info => Console.WriteLine($"Reconnection happened, type: {info.Type}"));

            // Log disconnects and destroy/clean up after disconnection
            WebSocketClient.DisconnectionHappened.Subscribe(_ =>
            {
                Console.WriteLine("WebSocket connection closed.");
                connectionGood = false;
                WebSocketClient.Dispose();
                WebSocketClient = null;
            });

            // Set up to handle incoming messages
            WebSocketClient.MessageReceived.Subscribe(Message => HandleMessage(Message));

            // Attempt client to server connection
            Console.WriteLine("Attempting connection...");

            try
            {
                WebSocketClient.Start().Wait();

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

        private static void SendMessage(RequestTypes? ReqType, string ReqData)
        {
            if (WebSocketClient == null) return;

            if (ReqType != null)
            {
                WebSocketClient.Send(((uint)ReqType.Value).ToString(RequestTypeFormat) + ReqData);
            } else
            {
                WebSocketClient.Send(ReqData);
            }
        }

        private static void SendSongFile(string RecievingClientID)
        {
            Byte[] bytes = File.ReadAllBytes(""); // Song file path needed
            String fileData = Convert.ToBase64String(bytes);
            SendMessage(RequestTypes.SongFile, RecievingClientID + "|" + fileData);
        }
        private static void SendChartFile(string RecievingClientID)
        {
            Byte[] bytes = File.ReadAllBytes(""); // Chart file path needed
            String fileData = Convert.ToBase64String(bytes);
            SendMessage(RequestTypes.ChartFile, RecievingClientID + "|" + fileData);
        }

        private static void SyncWithClient(string recievingClientID)
        {
            SendSongFile(recievingClientID);
            SendChartFile(recievingClientID);
        }

        private static void HandleMessage(ResponseMessage Message)
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

            RequestTypes RequestType = (RequestTypes)uint.Parse(Message.Text.Substring(0, RequestTypeLength));

            string TrimmedMessage = Message.Text.Substring(RequestTypeLength);

            switch(RequestType)
            {
                case RequestTypes.LobbyCreated:
                    Console.WriteLine("Lobby created.");
                    break;
                case RequestTypes.JoinLobby:
                    //AddClientTimestamp(TrimmedMessage);
                    Console.WriteLine("Got new peer data.");
                    break;
                case RequestTypes.SyncRequest:
                    //SyncWithClient(TrimmedMessage);
                    Console.WriteLine("Got sync request.");
                    break;
                case RequestTypes.ClientTimestamp:
                    //SetClientTimestamp(TrimmedMessage);
                    Console.WriteLine("Got timestamp data.");
                    break;
                default:
                    Console.WriteLine("Got an unknown request type???");
                    break;
            }
        }

        /*private static void SetClientTimestamp(string Message)
        {
            string[] SplitMessage = Message.Split('|');

            PeerManager.SetPeerMarkerTimestamp(int.Parse(SplitMessage[0]), uint.Parse(SplitMessage[1]));
        }
        private static void AddClientTimestamp(string Message)
        {
            string[] SplitMessage = Message.Split('|');
            PeerManager.AddPeer(int.Parse(SplitMessage[0]), SplitMessage[1].Substring(0, HexColorLength), SplitMessage[1].Substring(HexColorLength));
        }*/
    }
}
