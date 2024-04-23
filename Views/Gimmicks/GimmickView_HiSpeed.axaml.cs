using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Gimmicks;

public partial class GimmickView_HiSpeed : UserControl
{
    public GimmickView_HiSpeed(float hiSpeed = 1)
    {
        InitializeComponent();

        HiSpeedNumberBox.Value = hiSpeed;
    }
}