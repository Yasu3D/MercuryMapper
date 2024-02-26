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
        IStorageFile? file = await OpenAudioFilePicker();
        if (file == null) return;

        MusicFilePath = file.Path.LocalPath;
        MusicFilePathTextBox.Text = file.Path.LocalPath;
    }
    
    private async Task<IStorageFile?> OpenAudioFilePicker()
    {
        var result = await mainView.GetStorageProvider().OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Audio files")
                {
                    Patterns = new[] {"*.wav","*.flac","*.mp3","*.ogg"},
                    AppleUniformTypeIdentifiers = new[] {"public.item"}
                }
            }
        });

        return result.Count != 1 ? null : result[0];
    }
}