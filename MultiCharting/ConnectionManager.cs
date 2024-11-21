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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MercuryMapper.MultiCharting;

public class ConnectionManager(MainView main)
{
    private readonly MainView mainView = main;
    private PeerManager PeerManager => mainView.PeerManager;
    private ChartEditor ChartEditor => mainView.ChartEditor;
    private Chart Chart => mainView.ChartEditor.Chart;
        
    public enum MessageTypes : int
    {
        // 000 - Connect, Host, Join, and Sync
        InitConnection = 0,
        OutdatedClient = 1,
        CreateSession = 2,
        SessionCreated = 3,
        SessionClosed = 4,
        JoinSession = 5,
        LeaveSession = 6,
        BadSessionCode = 7,
        GoodSessionCode = 8,
        SyncRequest = 9,
        File = 10,
        ChartData = 11,
        SyncBegin = 12,
        SyncEnd = 13,

        // 100 - Metadata
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

        // 200 - Realtime Events
        InsertNote = 200,
        InsertHoldNote = 201,
        InsertHoldSegment = 202,
        DeleteNote = 203,
        DeleteHoldNote = 204,
        EditNote = 205,
        BakeHold = 206,
        SplitHold = 207,
        StitchHold = 208,
        InsertGimmick = 209,
        EditGimmick = 210,
        DeleteGimmick = 211,
        ClientTimestamp = 212,
    }
    public enum OperationDirection : int
    {
        Undo = 0,
        Redo = 1,
    }

    public class MessageSerializer(MessageTypes messageType, string[]? stringData = null, int[]? intData = null, decimal[]? decimalData = null)
    {
        [JsonInclude]
        public readonly int MessageType = (int)messageType;
        [JsonInclude]
        public readonly string[] StringData = stringData ?? [];
        [JsonInclude]
        public readonly int[] IntData = intData ?? [];
        [JsonInclude]
        public readonly decimal[] DecimalData = decimalData ?? [];
    }

    private class MessageDeserializer
    {
        public MessageTypes MessageType { get; set; }
        public string[]? StringData { get; set; }
        public int[]? IntData { get; set; }
        public decimal[]? DecimalData { get; set; }
    }

    private WebsocketClient? webSocketClient;
    private bool connectionGood;
        
    public string SessionCode = "";
    private string audioFilePath = "";
    private string receivedChartData = "";

    private bool audioReceived;
    private bool chartReceived;
        
    public NetworkConnectionState NetworkState = NetworkConnectionState.Local;
    public enum NetworkConnectionState
    {
        Local,
        Host,
        Client,
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

    // Session Setup
    public void CreateSession(string address, string username, string color)
    {
        string connection = CheckConnectionOrConnect(address);

        if (connection == "failed")
        {
            Dispatcher.UIThread.Post(() => mainView.ShowWarningMessage(Assets.Lang.Resources.Online_ConnectionFailed, Assets.Lang.Resources.Online_ConnectionFailedDetails));
            return;
        }

        SendMessage(new(MessageTypes.CreateSession, [ username, color ]));
    }

    public void JoinSession(string address, string username, string color, string sessionCode)
    {
        audioReceived = false;
        chartReceived = false;
        
        string connection = CheckConnectionOrConnect(address);

        if (connection == "failed") return;

        SessionCode = sessionCode.ToUpperInvariant();

        SendMessage(new(MessageTypes.JoinSession, [ SessionCode, username, color ]));
    }

    public void LeaveSession()
    {
        if (webSocketClient == null || !connectionGood) return;

        SessionCode = "";

        webSocketClient.Dispose();
    }

    private void HandleDisconnect()
    {
        if (webSocketClient == null) return;

        connectionGood = false;
        webSocketClient = null;

        Dispatcher.UIThread.Post(() => {
            if (SessionCode != "")
            {
                mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionClosed}");
                SessionCode = "";
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

        ClientWebSocket factory()
        {
            ClientWebSocket client = new();

            client.Options.AddSubProtocol("mercury-multi-mapper");

            return client;
        }

        Uri serverUri;
            
        try
        {
            serverUri = new(serverUrl);
        }
        catch
        {
            return "failed";
        }

        webSocketClient = new(serverUri, factory)
        {
            ReconnectTimeout = null,
            IsReconnectionEnabled = false,
        };

        webSocketClient.DisconnectionHappened.Subscribe(_ =>
        {
            HandleDisconnect();
        });

        webSocketClient.MessageReceived.Subscribe(HandleMessage);

        try
        {
            webSocketClient.Start().Wait();

            SendMessage(new(MessageTypes.InitConnection, [ "Hello MercuryMultiMapperServer!", MainView.ServerVersion ]));

            return "connected";
        }
        catch
        {
            return "failed";
        }
    }

    private void SendFile(int receivingClientId)
    {
        byte[] bytes = File.ReadAllBytes(ChartEditor.Chart.BgmFilepath);
        string fileData = Convert.ToBase64String(bytes);

        SendMessage(new(MessageTypes.File, [ Path.GetFileName(ChartEditor.Chart.BgmFilepath), fileData ], [ receivingClientId ]));
    }

    private void SendChartData(int receivingClientId)
    {
        string chartData = FormatHandler.WriteFileToNetwork(mainView.ChartEditor.Chart);

        SendMessage(new(MessageTypes.ChartData, [ chartData ], [ receivingClientId ]));
    }

    private void LoadChartAndAudio()
    {
        Dispatcher.UIThread.Post(() => mainView.OpenChartFromNetwork(receivedChartData, audioFilePath));
        
        SendMessage(new(MessageTypes.SyncEnd));
    }
        
    public void SendMessage(MessageSerializer messageObject)
    {
        webSocketClient?.Send(JsonSerializer.Serialize(messageObject));
    }

    public void SendOperationMessage(IOperation operation, OperationDirection operationDirection)
    {
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
                SendMessage(new(MessageTypes.InsertNote, [ opData ], [ (int)operationDirection ]));
                break;
            }

            case InsertHoldNote insertHoldNote:
            {
                string opData = $"{insertHoldNote.Note.ToNetworkString()}\n" +
                                $"{insertHoldNote.LastPlacedNote.ToNetworkString()}";
                SendMessage(new(MessageTypes.InsertHoldNote, [ opData ], [ (int)operationDirection ]));
                break;
            }
                
            case InsertHoldSegment insertHoldSegment:
            {
                string opData = $"{insertHoldSegment.NewNote.ToNetworkString()}\n" +
                                $"{insertHoldSegment.Previous.ToNetworkString()}\n" +
                                $"{insertHoldSegment.Next.ToNetworkString()}";
                SendMessage(new(MessageTypes.InsertHoldSegment, [ opData ], [ (int)operationDirection ]));
                break;
            }
                
            case DeleteNote deleteNote:
            {
                string opData = deleteNote.Note.ToNetworkString();
                SendMessage(new(MessageTypes.DeleteNote, [ opData ], [ (int)operationDirection ]));
                break;
            }
                
            case DeleteHoldNote deleteHoldNote:
            {
                string opData = $"{deleteHoldNote.DeletedNote.ToNetworkString()}\n{(int)deleteHoldNote.NextNoteOriginalBonusType}";
                SendMessage(new(MessageTypes.DeleteHoldNote, [ opData ], [ (int)operationDirection ]));
                break;
            }
                
            case EditNote editNote:
            {
                string opData = $"{editNote.BaseNote.ToNetworkString()}\n" +
                                $"{editNote.OldNote.ToNetworkString()}\n" + 
                                $"{editNote.NewNote.ToNetworkString()}";
                SendMessage(new(MessageTypes.EditNote, [ opData ], [ (int)operationDirection ]));
                break;
            }
                
            case BakeHold bakeHold:
            {
                List<string> opData =
                [
                    $"{bakeHold.Start.ToNetworkString()}",
                    $"{bakeHold.End.ToNetworkString()}",
                ];

                foreach (Note note in bakeHold.Segments)
                {
                    opData.Add($"{note.ToNetworkString()}");
                }
                    
                SendMessage(new(MessageTypes.BakeHold, opData.ToArray(), [ (int)operationDirection ]));
                break;
            }
                
            case SplitHold splitHold:
            {
                string opData = $"{splitHold.Segment.ToNetworkString()}\n" +
                                $"{splitHold.NewStart.ToNetworkString()}\n" + 
                                $"{splitHold.NewEnd.ToNetworkString()}";
                SendMessage(new(MessageTypes.SplitHold, [ opData ], [ (int)operationDirection ]));
                break;
            }

            case StitchHold stitchHold:
            {
                string opData = $"{stitchHold.First.ToNetworkString()}\n" +
                                $"{stitchHold.Second.ToNetworkString()}\n";
                SendMessage(new(MessageTypes.StitchHold, [ opData ], [ (int)operationDirection ]));
                break;
            }

            case InsertGimmick insertGimmick:
            {
                string opData = $"{insertGimmick.Gimmick.ToNetworkString()}";
                SendMessage(new(MessageTypes.InsertGimmick, [ opData ], [ (int)operationDirection ]));
                break;
            }
                
            case DeleteGimmick deleteGimmick:
            {
                string opData = $"{deleteGimmick.Gimmick.ToNetworkString()}";
                SendMessage(new(MessageTypes.DeleteGimmick, [ opData ], [ (int)operationDirection ]));
                break;
            }

            case EditGimmick editGimmick:
            {
                string opData = $"{editGimmick.BaseGimmick.ToNetworkString()}\n" +
                                $"{editGimmick.OldGimmick.ToNetworkString()}\n" + 
                                $"{editGimmick.NewGimmick.ToNetworkString()}";
                SendMessage(new(MessageTypes.EditGimmick, [ opData ], [ (int)operationDirection ]));
                break;
            }
        }
    }
        
    private void HandleMessage(ResponseMessage message)
    {
        if (message.MessageType != WebSocketMessageType.Text || message.Text == null) return;
        
        MessageDeserializer? messageData = JsonSerializer.Deserialize<MessageDeserializer>(message.Text);

        if (messageData?.StringData == null || messageData.IntData == null || messageData.DecimalData == null) return;

        if (connectionGood  == false)
        {
            if (messageData.MessageType == MessageTypes.InitConnection && messageData.StringData[0] == "Hello MercuryMapper Client!") connectionGood = true;

            return;
        }
        
        switch (messageData.MessageType)
        {
            // Host, Join, Sync
            case MessageTypes.SessionCreated:
            {
                SessionCode = messageData.StringData[0];
                Dispatcher.UIThread.Post(() => {
                    mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_SessionOpened} {SessionCode}");
                });
                SetNetworkConnectionState(NetworkConnectionState.Host);
                break;
            }

            case MessageTypes.OutdatedClient:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ShowWarningMessage(Assets.Lang.Resources.Online_OutdatedClient, Assets.Lang.Resources.Online_OutdatedClientDetails);
                });
                break;
            }

            case MessageTypes.JoinSession:
            {
                Dispatcher.UIThread.Post(() => PeerManager.AddPeer(messageData.IntData[0], messageData.StringData[0], messageData.StringData[1]));
                break;
            }
                
            case MessageTypes.LeaveSession:
            {
                Dispatcher.UIThread.Post(() => PeerManager.RemovePeer(messageData.IntData[0]));
                break;
            }
                
            case MessageTypes.BadSessionCode:
            {
                Dispatcher.UIThread.Post(() => mainView.ShowWarningMessage($"{Assets.Lang.Resources.Online_InvalidSessionCode}"));
                SessionCode = "";
                webSocketClient?.Dispose();
                break;
            }
                
            case MessageTypes.GoodSessionCode:
            {
                SetNetworkConnectionState(NetworkConnectionState.Client);
                SendMessage(new(MessageTypes.SyncRequest));
                break;
            }
                
            case MessageTypes.SyncRequest:
            {
                SendChartData(messageData.IntData[0]);
                SendFile(messageData.IntData[0]);
                break;
            }
                
            case MessageTypes.ChartData:
            {
                receivedChartData = messageData.StringData[0];

                // Report back that this data has been received.
                // Then check if the other was received, and begin loading if it was.
                chartReceived = true;
                if (audioReceived) LoadChartAndAudio();
                break;
            }
                
            case MessageTypes.File:
            {
                string fileName = messageData.StringData[0] + ".mmm.audio";
                audioFilePath = Path.Combine(Path.GetTempPath(), fileName);
            
                string audioFileData = messageData.StringData[1];
                byte[] fileData = Convert.FromBase64String(audioFileData);
                    
                File.WriteAllBytes(audioFilePath, fileData);

                // Report back that this data has been received.
                // Then check if the other was received, and begin loading if it was.
                audioReceived = true;
                if (chartReceived) LoadChartAndAudio();
                break;
            }

            case MessageTypes.SyncBegin:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    mainView.SetPlayState(MainView.PlayerState.Paused);
                    mainView.ShowTransmittingDataMessage();
                });
                break;   
            }

            case MessageTypes.SyncEnd:
            {
                Dispatcher.UIThread.Post(() => mainView.HideTransmittingDataMessage());
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
                    mainView.ChartEditor.Chart.Version = messageData.StringData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.TitleChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.Title = messageData.StringData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.RubiChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.Rubi = messageData.StringData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.ArtistChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.Artist = messageData.StringData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.AuthorChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.Author = messageData.StringData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.DiffChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.Diff = messageData.IntData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.LevelChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.Level = messageData.DecimalData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.ClearThresholdChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.ClearThreshold = messageData.DecimalData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.BpmTextChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.BpmText = messageData.StringData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.PreviewStartChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.PreviewStart = messageData.DecimalData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.PreviewTimeChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.PreviewTime = messageData.DecimalData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.BgmOffsetChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.BgmOffset = messageData.DecimalData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.BgaOffsetChange:
            {
                Dispatcher.UIThread.Post(() => {
                    mainView.ChartEditor.Chart.BgaOffset = messageData.DecimalData[0];
                    mainView.SetChartInfo();
                });
                break;
            }
                
            case MessageTypes.ClientTimestamp:
            {
                Dispatcher.UIThread.Post(() => PeerManager.SetPeerMarkerTimestamp(messageData.IntData[0], (uint)messageData.IntData[1]));
                break;
            }
                
            case MessageTypes.SessionClosed:
            {
                webSocketClient?.Dispose();
                break;
            }
                
            // Realtime Changes
            case MessageTypes.InsertNote:
            {
                Dispatcher.UIThread.Post(() =>
                {
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] noteData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] noteData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] lastPlacedNoteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] noteData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] prevReferencedNoteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] nextReferencedNoteData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] noteData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] noteData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    BonusType bonusType = (BonusType)Convert.ToInt32(operationData[1]);

                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] noteData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] oldNoteData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] newNoteData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] startData = messageData.StringData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] endData = messageData.StringData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    List<string[]> segmentData = [];
                        
                    for (int i = 2; i < messageData.StringData.Length; i++)
                    {
                        segmentData.Add(messageData.StringData[i].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    }

                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                            if (data[8] != "null" && segment.NextReferencedNote == null ) segment.NextReferencedNote = segments.FirstOrDefault(x => x.Guid.ToString() == data[8]);
                            if (data[9] != "null" && segment.PrevReferencedNote == null ) segment.PrevReferencedNote = segments.FirstOrDefault(x => x.Guid.ToString() == data[9]);
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] segmentData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] newStartData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] newEndData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] firstData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] secondData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
                    {
                        // Undo
                        Note? first = Chart.FindNoteByGuid(firstData[0]);
                        Note? second = Chart.FindNoteByGuid(secondData[0]);
                        if (first == null || second == null) return;
                        
                        StitchHold operation = new(Chart, first, second);
                        operation.Undo();
                        ChartEditor.UndoRedoManager.Invoke();
                    }
                    else
                    {
                        // Redo
                        Note? first = Chart.FindNoteByGuid(firstData[0]);
                        Note? second = Chart.FindNoteByGuid(secondData[0]);
                        if (first == null || second == null) return;
                        
                        StitchHold operation = new(Chart, first, second);

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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] gimmickData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] gimmickData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] oldGimmickData = operationData[1].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] newGimmickData = operationData[2].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                    string[] operationData = messageData.StringData[0].Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] gimmickData = operationData[0].Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if ((OperationDirection)messageData.IntData[0] == OperationDirection.Undo)
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
                Console.WriteLine($"Got an unknown or unhandled message type: {messageData.MessageType}");
                break;
            }
        }
    }
}