using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MercuryMapper.Views;

public partial class SaturnFolderExportView : UserControl
{
    public SaturnFolderExportView(MainView mainView)
    {
        this.mainView = mainView;
        InitializeComponent();
    }

    private readonly MainView mainView;

    private async void SelectFileMusic_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;
        
        MusicTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFileJacket_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenJacketFilePicker();
        if (file == null) return;
        
        JacketTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFileDiffNormal_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenChartFilePicker();
        if (file == null) return;
        
        NormalPathTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFileDiffHard_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenChartFilePicker();
        if (file == null) return;
        
        HardPathTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFileDiffExpert_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenChartFilePicker();
        if (file == null) return;
        
        ExpertPathTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFileDiffInferno_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenChartFilePicker();
        if (file == null) return;
        
        InfernoPathTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFileDiffBeyond_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenChartFilePicker();
        if (file == null) return;
        
        BeyondPathTextBox.Text = file.Path.LocalPath;
    }
    
    private async void SelectFolderPath_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFolder? folder = await mainView.OpenFolderPicker();
        if (folder == null) return;

        FolderPathTextBox.Text = folder.Path.LocalPath;
    }
}