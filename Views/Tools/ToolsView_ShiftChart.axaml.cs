using Avalonia.Controls;

namespace MercuryMapper.Views.Tools;

public partial class ToolsView_ShiftChart : UserControl
{
    public ToolsView_ShiftChart()
    {
        InitializeComponent();
    }

    public int Ticks => (int)((ShiftMeasureNumeric.Value ?? 0) * 1920 + (ShiftBeatNumeric.Value ?? 0) * (1920 / (ShiftDivisionNumeric.Value ?? 16)));

    private bool blockUpdates;
    
    private void ShiftBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (blockUpdates) return;
        if (ShiftMeasureNumeric.Value == null || ShiftDivisionNumeric.Value == null || ShiftBeatNumeric.Value == null) return;

        blockUpdates = true;
        
        // Overflow
        if (decimal.Abs(e.NewValue ?? 0) >= ShiftDivisionNumeric.Value)
        {
            ShiftMeasureNumeric.Value += decimal.Sign(e.NewValue ?? 0);
            ShiftBeatNumeric.Value = 0;
        }
        
        // Flip
        if (ShiftMeasureNumeric.Value > 0 && e.NewValue < 0)
        {
            ShiftMeasureNumeric.Value--;
            ShiftBeatNumeric.Value = ShiftDivisionNumeric.Value - 1;
        }
        
        if (ShiftMeasureNumeric.Value < 0 && e.NewValue > 0)
        {
            ShiftMeasureNumeric.Value++;
            ShiftBeatNumeric.Value = -ShiftDivisionNumeric.Value + 1;
        }
        
        blockUpdates = false;
    }

    private void ShiftDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ShiftMeasureNumeric.Value == null || ShiftDivisionNumeric.Value == null || ShiftBeatNumeric.Value == null) return;

        if (e.NewValue <= decimal.Abs(ShiftBeatNumeric.Value ?? 0))
        {
            ShiftBeatNumeric.Value = (e.NewValue - 1) * decimal.Sign(ShiftBeatNumeric.Value ?? 0);
        }
    }
}