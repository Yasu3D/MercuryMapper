using System;
using MercuryMapper.Data;
using MercuryMapper.Enums;

namespace MercuryMapper.Editor;

public class ChartEditor
{
    public Chart Chart { get; private set; } = new();
    public bool IsNew { get; set; } = true;

    public ChartEditorState State { get; private set; }

    public void NewChart()
    {
        Chart = new()
        {
            IsSaved = false
        };
        Console.WriteLine("New chart created!");
    }
}