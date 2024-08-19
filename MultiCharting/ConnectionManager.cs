using Avalonia.Threading;
using MercuryMapper.Editor;
using MercuryMapper.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.UndoRedo;
using MercuryMapper.UndoRedo.NoteOperations;
using Websocket.Client;

namespace MercuryMapper.MultiCharting
{
    public class ConnectionManager(MainView main)
    {
        private readonly MainView mainView = main;
        private PeerManager PeerManager => mainView.PeerManager;
        private ChartEditor ChartEditor => mainView.ChartEditor;
        private Chart Chart => mainView.ChartEditor.Chart;
        
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
            VersionChange = 100,
            TitleChange = 101,
            RubiChange = 102,
            ArtistChange = 103,
            AuthorChange = 104,
            DiffChange = 105,
            LevelChange = 106,
            ClearThresholdChange = 107,
            BpmTextChange = 108,
            PreviewStartChange = 109,
            PreviewTimeChange = 110,
            BgmOffsetChange = 111,
            BgaOffsetChange = 112,

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
            ConvertToInstantMask = 209, // Obsolete and Unused. TODO: remove from backend.
            InsertGimmick = 210,
            EditGimmick = 211,
            DeleteGimmick = 212,
            ClientTimestamp = 213
        }

        public enum OperationDirection
        {
            Undo = 0,
            Redo = 1
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
            ClientWebSocket factory()
            {
                ClientWebSocket client = new();

                client.Options.AddSubProtocol("mercury-multi-mapper");

                return client;
            }

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
            byte[] bytes = File.ReadAllBytes(ChartEditor.Chart.BgmFilepath);
            string fileData = Convert.ToBase64String(bytes);

            // I swear - if someone uses pipes in file names - they're insane.
            SendMessage(MessageTypes.SongFile, receivingClientId + "|" + Path.GetFileName(ChartEditor.Chart.BgmFilepath) + "|" + fileData);
        }

        private void SendChartData(string receivingClientId)
        {
            string chartData = FormatHandler.WriteFileToNetwork(mainView.ChartEditor.Chart);

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

        public void SendOperationMessage(IOperation operation, OperationDirection operationDirection)
        {
            string opDir = operationDirection == OperationDirection.Redo ? "1\n" : "0\n";
            
            switch (operation)
            {
                case CompositeOperation compositeOperation:
                {
                    foreach (IOperation op in compositeOperation.Operations.Reverse())
                    {
                        SendOperationMessage(op, operationDirection);
                    }
                    break;
                }
                
                case InsertNote insertNote:
                {
                    string opData = insertNote.Note.ToNetworkString();
                    SendMessage(MessageTypes.InsertNote, opDir + opData);
                    break;
                }
                
                case InsertHoldNote insertHoldNote:
                {
                    string opData = $"{insertHoldNote.Note.ToNetworkString()}\n" +
                                    $"{insertHoldNote.LastPlacedNote.ToNetworkString()}";
                    SendMessage(MessageTypes.InsertHoldNote, opDir + opData);
                    break;
                }
                
                case InsertHoldSegment insertHoldSegment:
                {
                    string opData = $"{insertHoldSegment.NewNote.ToNetworkString()}\n" +
                                    $"{insertHoldSegment.Previous.ToNetworkString()}\n" +
                                    $"{insertHoldSegment.Next.ToNetworkString()}";
                    SendMessage(MessageTypes.InsertHoldSegment, opDir + opData);
                    break;
                }
                
                case DeleteNote deleteNote:
                {
                    string opData = deleteNote.Note.ToNetworkString();
                    SendMessage(MessageTypes.DeleteNote, opDir + opData);
                    break;
                }
                
                case DeleteHoldNote deleteHoldNote:
                {
                    string opData = $"{deleteHoldNote.DeletedNote.ToNetworkString()}\n{(int)deleteHoldNote.BonusType}";
                    SendMessage(MessageTypes.DeleteHoldNote, opDir + opData);
                    break;
                }
                
                case EditNote editNote:
                {
                    string opData = $"{editNote.BaseNote.ToNetworkString()}\n" +
                                    $"{editNote.OldNote.ToNetworkString()}\n" + 
                                    $"{editNote.NewNote.ToNetworkString()}";
                    SendMessage(MessageTypes.EditNote, opDir + opData);
                    break;
                }
                
                case BakeHold bakeHold:
                {
                    string opData = $"{bakeHold.Start.ToNetworkString()}\n" +
                                    $"{bakeHold.End.ToNetworkString()}\n";
                    foreach (Note note in bakeHold.Segments)
                    {
                        opData += $"{note.ToNetworkString()}\n";
                    }
                    
                    SendMessage(MessageTypes.BakeHold, opDir + opData);
                    break;
                }
                
                case SplitHold splitHold:
                {
                    string opData = $"{splitHold.Segment.ToNetworkString()}\n" +
                                    $"{splitHold.NewStart.ToNetworkString()}\n" + 
                                    $"{splitHold.NewEnd.ToNetworkString()}";
                    SendMessage(MessageTypes.SplitHold, opDir + opData);
                    break;
                }

                case StitchHold stitchHold:
                {
                    string opData = $"{stitchHold.First.ToNetworkString()}\n" +
                                    $"{stitchHold.Second.ToNetworkString()}\n" + 
                                    $"{(int)stitchHold.SecondType}";
                    SendMessage(MessageTypes.StitchHold, opDir + opData);
                    break;
                }

                case InsertGimmick insertGimmick:
                {
                    string opData = $"{insertGimmick.Gimmick.ToNetworkString()}";
                    SendMessage(MessageTypes.InsertGimmick, opDir + opData);
                    break;
                }
                
                case DeleteGimmick deleteGimmick:
                {
                    string opData = $"{deleteGimmick.Gimmick.ToNetworkString()}";
                    SendMessage(MessageTypes.DeleteGimmick, opDir + opData);
                    break;
                }

                case EditGimmick editGimmick:
                {
                    string opData = $"{editGimmick.BaseGimmick.ToNetworkString()}\n" +
                                    $"{editGimmick.OldGimmick.ToNetworkString()}\n" + 
                                    $"{editGimmick.NewGimmick.ToNetworkString()}";
                    SendMessage(MessageTypes.EditGimmick, opDir + opData);
                    break;
                }
            }
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
            string[] operationData = trimmedMessage.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
                
                //VersionChange = 100,
                //TitleChange = 101,
                //RubiChange = 102,
                //ArtistChange = 103,
                //AuthorChange = 104,
                //DiffChange = 105,
                //LevelChange = 106,
                //ClearThresholdChange = 107,
                //BpmTextChange = 108,
                //PreviewStartChange = 109,
                //PreviewTimeChange = 110,
                //BgmOffsetChange = 111,
                //BgaOffsetChange = 112,
                
                case MessageTypes.VersionChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Version = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.TitleChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Title = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.RubiChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Rubi = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.ArtistChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Artist = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.AuthorChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Author = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.DiffChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.Diff = Convert.ToInt32(trimmedMessage);
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
                
                case MessageTypes.BpmTextChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.BpmText = trimmedMessage;
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.PreviewStartChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewStart = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.PreviewTimeChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.PreviewTime = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.BgmOffsetChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.BgmOffset = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
                        mainView.SetChartInfo();
                    });
                    break;
                }
                
                case MessageTypes.BgaOffsetChange:
                {
                    Dispatcher.UIThread.Post(() => {
                        mainView.ChartEditor.Chart.BgaOffset = Convert.ToDecimal(trimmedMessage, CultureInfo.InvariantCulture);
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
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] noteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            if (note == null) return;
                        
                            InsertNote operation = new(Chart, ChartEditor.SelectedNotes, note);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            InsertNote operation = new(Chart, ChartEditor.SelectedNotes, Note.ParseNetworkString(Chart, noteData));
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.InsertHoldNote:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] noteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] lastPlacedNoteData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            Note? lastPlacedNote = Chart.FindNoteByGuid(lastPlacedNoteData[0]);
                            if (note == null || lastPlacedNote == null) return;
                            
                            InsertHoldNote operation = new(Chart, ChartEditor.SelectedNotes, note, lastPlacedNote);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note note = Note.ParseNetworkString(Chart, noteData);
                            Note lastPlacedNote = Chart.FindNoteByGuid(lastPlacedNoteData[0]) ?? Note.ParseNetworkString(Chart, lastPlacedNoteData);

                            InsertHoldNote operation = new(Chart, ChartEditor.SelectedNotes, note, lastPlacedNote);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        
                        // Clear UndoRedoHistory, because otherwise this action FUCKS SHIT UP. REAL BAD.
                        // Hold notes are painfully complicated :]
                        // TODO: Current vulnerabilities:
                        // - A stitches hold
                        // - A hits undo
                        // - B continues hold
                        // - A hits redo
                        ChartEditor.UndoRedoManager.Clear();
                    });
                    
                    break;
                }
                
                case MessageTypes.InsertHoldSegment:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] noteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] prevReferencedNoteData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] nextReferencedNoteData = operationData[3].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            Note? prevNote = Chart.FindNoteByGuid(prevReferencedNoteData[0]);
                            Note? nextNote = Chart.FindNoteByGuid(nextReferencedNoteData[0]);
                            if (note == null || prevNote == null || nextNote == null) return;
                            
                            InsertHoldSegment operation = new(Chart, ChartEditor.SelectedNotes, note, prevNote, nextNote);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note note = Note.ParseNetworkString(Chart, noteData);
                            Note? prevNote = Chart.FindNoteByGuid(prevReferencedNoteData[0]);
                            Note? nextNote = Chart.FindNoteByGuid(nextReferencedNoteData[0]);
                            if (prevNote == null || nextNote == null) return;

                            InsertHoldSegment operation = new(Chart, ChartEditor.SelectedNotes, note, prevNote, nextNote);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    break;
                }
                
                case MessageTypes.DeleteNote:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] noteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                        if (operationData[0] == "0")
                        {
                            // Undo
                            DeleteNote operation = new(Chart, ChartEditor.SelectedNotes, Note.ParseNetworkString(Chart, noteData));
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            if (note == null) return;
                        
                            DeleteNote operation = new(Chart, ChartEditor.SelectedNotes, note);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.DeleteHoldNote:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] noteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        BonusType bonusType = (BonusType)Convert.ToInt32(operationData[2]);

                        if (operationData[0] == "0")
                        {
                            // Undo
                            DeleteHoldNote operation = new(Chart, ChartEditor.SelectedNotes, Note.ParseNetworkString(Chart, noteData), bonusType);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            if (note == null) return;
                        
                            DeleteHoldNote operation = new(Chart, ChartEditor.SelectedNotes, note, bonusType);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        
                        // Clear UndoRedoHistory, because otherwise this action FUCKS SHIT UP. REAL BAD.
                        // Hold notes are painfully complicated :]
                        // TODO: Current vulnerabilities:
                        // - A bakes hold
                        // - B deletes part of baked hold
                        // - A hits undo
                        ChartEditor.UndoRedoManager.Clear();
                    });
                    
                    break;
                }
                
                case MessageTypes.EditNote:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] noteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] oldNoteData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] newNoteData = operationData[3].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            if (note == null) return;

                            Note oldNote = Note.ParseNetworkString(Chart, oldNoteData);
                            
                            EditNote operation = new(note, oldNote);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note? note = Chart.FindNoteByGuid(noteData[0]);
                            if (note == null) return;

                            Note newNote = Note.ParseNetworkString(Chart, newNoteData);
                        
                            EditNote operation = new(note, newNote);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.BakeHold:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] startData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] endData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        List<string[]> segmentData = [];
                        
                        for (int i = 3; i < operationData.Length; i++)
                        {
                            segmentData.Add(operationData[i].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        }

                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? start = Chart.FindNoteByGuid(startData[0]);
                            Note? end = Chart.FindNoteByGuid(endData[0]);
                            if (start == null || end == null) return;

                            List<Note> segments = [];
                            foreach (string[] data in segmentData)
                            {
                                Note? note = Chart.FindNoteByGuid(data[0]);
                                if (note == null) continue;
                                
                                segments.Add(note);
                            }

                            BakeHold operation = new(Chart, ChartEditor.SelectedNotes, segments, start, end);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note? start = Chart.FindNoteByGuid(startData[0]);
                            Note? end = Chart.FindNoteByGuid(endData[0]);
                            if (start == null || end == null) return;

                            List<Note> segments = [];
                            foreach (string[] data in segmentData)
                            {
                                segments.Add(Note.ParseNetworkString(Chart, data));
                            }

                            for (int i = 0; i < segments.Count; i++)
                            {
                                Note segment = segments[i];
                                string[] data = segmentData[i];

                                // Repair references that weren't picked up by Note.ParseNetworkString
                                if (data[7] != "null" && segment.NextReferencedNote == null ) segment.NextReferencedNote = segments.LastOrDefault(x => x.Guid.ToString() == data[7]);
                                if (data[8] != "null" && segment.PrevReferencedNote == null ) segment.PrevReferencedNote = segments.LastOrDefault(x => x.Guid.ToString() == data[8]);
                            }

                            BakeHold operation = new(Chart, ChartEditor.SelectedNotes, segments, start, end);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.SplitHold:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] segmentData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] newStartData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] newEndData = operationData[3].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? newStart = Chart.FindNoteByGuid(newStartData[0]);
                            Note? newEnd = Chart.FindNoteByGuid(newEndData[0]);
                            if (newStart == null || newEnd == null) return;

                            Note segment = Note.ParseNetworkString(Chart, segmentData);
                            
                            SplitHold operation = new(Chart, segment, newStart, newEnd);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note? segment = Chart.FindNoteByGuid(segmentData[0]);
                            if (segment == null) return;

                            Note newStart = Note.ParseNetworkString(Chart, newStartData);
                            Note newEnd = Note.ParseNetworkString(Chart, newEndData);
                            
                            SplitHold operation = new(Chart, segment, newStart, newEnd);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.StitchHold:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] firstData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] secondData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        if (operationData[0] == "0")
                        {
                            // Undo
                            Note? first = Chart.FindNoteByGuid(firstData[0]);
                            Note? second = Chart.FindNoteByGuid(secondData[0]);
                            if (first == null || second == null) return;

                            NoteType secondType = (NoteType)Convert.ToInt32(operationData[3], CultureInfo.InvariantCulture);

                            StitchHold operation = new(Chart, first, second, secondType);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Note? first = Chart.FindNoteByGuid(firstData[0]);
                            Note? second = Chart.FindNoteByGuid(secondData[0]);
                            if (first == null || second == null) return;

                            NoteType secondType = (NoteType)Convert.ToInt32(operationData[3], CultureInfo.InvariantCulture);

                            StitchHold operation = new(Chart, first, second, secondType);

                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.InsertGimmick:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] gimmickData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Gimmick? gimmick = Chart.FindGimmickByGuid(gimmickData[0]);
                            if (gimmick == null) return;
                            
                            InsertGimmick operation = new(Chart, gimmick);
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            InsertGimmick operation = new(Chart, Gimmick.ParseNetworkString(gimmickData));
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    break;
                }
                
                case MessageTypes.EditGimmick:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] gimmickData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] oldGimmickData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        string[] newGimmickData = operationData[3].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                        if (operationData[0] == "0")
                        {
                            // Undo
                            Gimmick? gimmick = Chart.FindGimmickByGuid(gimmickData[0]);
                            if (gimmick == null) return;

                            Gimmick oldGimmick = Gimmick.ParseNetworkString(oldGimmickData);
                            
                            EditGimmick operation = new(Chart, gimmick, oldGimmick);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Gimmick? gimmick = Chart.FindGimmickByGuid(gimmickData[0]);
                            if (gimmick == null) return;

                            Gimmick newGimmick = Gimmick.ParseNetworkString(newGimmickData);
                        
                            EditGimmick operation = new(Chart, gimmick, newGimmick);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
                    break;
                }
                
                case MessageTypes.DeleteGimmick:
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        string[] gimmickData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                        if (operationData[0] == "0")
                        {
                            // Undo
                            DeleteGimmick operation = new(Chart, Gimmick.ParseNetworkString(gimmickData));
                            operation.Undo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                        else
                        {
                            // Redo
                            Gimmick? gimmick = Chart.FindGimmickByGuid(gimmickData[0]);
                            if (gimmick == null) return;
                        
                            DeleteGimmick operation = new(Chart, gimmick);
                            operation.Redo();
                            ChartEditor.UndoRedoManager.Invoke();
                        }
                    });
                    
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
