using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Gimmicks;

public partial class GimmickView_Bpm : UserControl
{
    public GimmickView_Bpm(float bpm = 120)
    {
        InitializeComponent();
        BpmNumberBox.Value = bpm;
    }
}