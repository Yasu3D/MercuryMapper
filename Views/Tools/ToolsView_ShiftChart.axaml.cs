using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Tools;

public partial class ToolsView_ShiftChart : UserControl
{
    public ToolsView_ShiftChart()
    {
        InitializeComponent();
    }

    public int Ticks => (int)((ShiftMeasureNumeric.Value ?? 0) * 1920 + (ShiftBeatNumeric.Value ?? 0) * (1920 / (ShiftDivisionNumeric.Value ?? 16)));
    
    private void ShiftBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ShiftMeasureNumeric.Value == null || ShiftDivisionNumeric.Value == null || ShiftBeatNumeric.Value == null) return;

        if (e.NewValue >= ShiftDivisionNumeric.Value)
        {
            ShiftMeasureNumeric.Value++;
            ShiftBeatNumeric.Value = 0;
        }

        if (e.NewValue < 0)
        {
            ShiftMeasureNumeric.Value--;
            ShiftBeatNumeric.Value = ShiftDivisionNumeric.Value - 1;
        }
    }

    private void ShiftDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ShiftMeasureNumeric.Value == null || ShiftDivisionNumeric.Value == null || ShiftBeatNumeric.Value == null) return;

        if (e.NewValue <= ShiftBeatNumeric.Value)
        {
            ShiftBeatNumeric.Value = e.NewValue - 1;
        }
    }
}