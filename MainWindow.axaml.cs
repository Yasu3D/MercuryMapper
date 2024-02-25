using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MercuryMapper.Config;

namespace MercuryMapper;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        AddHandler(DragDrop.DropEvent, MainWindow_Drop);
    }
    
    private static void MainWindow_Drop(object? sender, DragEventArgs e)
    {
        var path = e.Data.GetFiles()?.First().Path;
        if (path == null) return;
        
        Console.WriteLine(Path.GetFullPath(Uri.UnescapeDataString(path.LocalPath)));
        e.Handled = true;
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (MainView.CanShutdown) return;
        e.Cancel = true;
        Dispatcher.UIThread.Post(async () => MainView.MenuItemExit_OnClick(null, new()));
    }
}