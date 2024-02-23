using System.Collections.Generic;

namespace MercuryMapper.Data;

public class Chart
{
    public Chart()
    {
        Notes = new List<Note>();
        Gimmicks = new List<Gimmick>();
        Offset = 0;
        MovieOffset = 0;
        SongFileName = "";
        IsSaved = true;
    }

    public bool IsSaved { get; set; }

    public List<Note> Notes { get; set; }
    public List<Gimmick> Gimmicks { get; set; }

    public float Offset { get; set; } // in seconds
    public float MovieOffset { get; set; } // in seconds

    private string SongFileName { get; set; }
    public string? EditorSongFileName { get; set; }
    private List<Gimmick>? TimeEvents { get; set; }
}