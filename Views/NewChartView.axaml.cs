using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MercuryMapper.Views;

public partial class NewChartView : UserControl
{
    public NewChartView(MainView mainView)
    {
        this.mainView = mainView;
        InitializeComponent();
    }

    private readonly MainView mainView;
    public string MusicFilePath = "";

    private async void SelectFile_OnClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await mainView.OpenAudioFilePicker();
        if (file == null) return;

        MusicFilePath = file.Path.LocalPath;
        MusicFilePathTextBox.Text = file.Path.LocalPath;
    }
}