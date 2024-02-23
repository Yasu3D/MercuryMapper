using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace MercuryMapper;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        AddHandler(DragDrop.DropEvent, MainView_Drop);
    }
    
    private static void MainView_Drop(object? sender, DragEventArgs e)
    {
        var path = e.Data.GetFiles()?.First().Path;
        if (path == null) return;
        
        Console.WriteLine(Path.GetFullPath(Uri.UnescapeDataString(path.LocalPath)));
        e.Handled = true;
    }
}