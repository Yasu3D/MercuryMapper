using System;
using MercuryMapper.Audio;
using MercuryMapper.Data;
using MercuryMapper.Enums;
using MercuryMapper.Views;

namespace MercuryMapper.Editor;

public class ChartEditor(MainView main)
{
    private MainView mainView = main;
    public Chart Chart { get; private set; } = new();
    
    public ChartEditorState State { get; private set; }
    
    public void NewChart(string musicFilePath, string author, float bpm, int timeSigUpper, int timeSigLower)
    {
        Chart = new()
        {
            MusicFilePath = musicFilePath,
            Author = author
        };

        lock (Chart)
        {
            Gimmick startBpm = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.BpmChange,
                Bpm = bpm,
                TimeStamp = 0
            };

            Gimmick startTimeSig = new()
            {
                BeatData = new(0, 0),
                GimmickType = GimmickType.TimeSigChange,
                TimeSig = new(timeSigUpper, timeSigLower),
                TimeStamp = 0
            };
            
            Chart.Gimmicks.Add(startBpm);
            Chart.Gimmicks.Add(startTimeSig);
            Chart.StartBpm = startBpm;
            Chart.StartTimeSig = startTimeSig;
        }
    }
}