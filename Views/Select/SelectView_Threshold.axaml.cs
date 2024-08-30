using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Select;

public partial class SelectView_Threshold : UserControl
{
    public SelectView_Threshold()
    {
        InitializeComponent();
    }

    private void SliderThreshold_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e) => ThresholdChanged(true);

    private void NumericThreshold_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) => ThresholdChanged(false);

    private void ThresholdChanged(bool fromSlider)
    {
        if (fromSlider) NumericThreshold.Value = (decimal?)SliderThreshold.Value;
        else SliderThreshold.Value = (double)(NumericThreshold.Value ?? 0);
    }
}