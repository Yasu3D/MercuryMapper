using Avalonia.Controls;

namespace MercuryMapper.Views.Gimmicks;

public partial class GimmickView_Stop : UserControl
{
    public GimmickView_Stop(int measure, int beat, int division)
    {
        InitializeComponent();
        
        StopStartMeasureNumeric.Value = measure;
        StopStartBeatNumeric.Value = beat;
        StopStartDivisionNumeric.Value = division;
        StopEndMeasureNumeric.Value = measure + 1;
        StopEndBeatNumeric.Value = beat;
        StopEndDivisionNumeric.Value = division;
    }
    
    public bool IsValueNull =>
        StopStartMeasureNumeric.Value is null
        || StopStartBeatNumeric.Value is null
        || StopStartDivisionNumeric.Value is null
        || StopEndMeasureNumeric.Value is null
        || StopEndBeatNumeric.Value is null
        || StopEndDivisionNumeric.Value is null;
    
    public float StartMeasureDecimal => (float)StopStartMeasureNumeric.Value! + (float)StopStartBeatNumeric.Value! / (float)StopStartDivisionNumeric.Value!;
    public float EndMeasureDecimal => (float)StopEndMeasureNumeric.Value! + (float)StopEndBeatNumeric.Value! / (float)StopEndDivisionNumeric.Value!;

    private bool blockUpdates;
    
    private void StopStartBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (blockUpdates) return;
        if (StopStartMeasureNumeric.Value == null || StopStartDivisionNumeric.Value == null || StopStartBeatNumeric.Value == null) return;

        blockUpdates = true;
        
        // Overflow
        if (decimal.Abs(e.NewValue ?? 0) >= StopStartDivisionNumeric.Value)
        {
            StopStartMeasureNumeric.Value += decimal.Sign(e.NewValue ?? 0);
            StopStartBeatNumeric.Value = 0;
        }
        
        // Flip
        if (StopStartMeasureNumeric.Value > 0 && e.NewValue < 0)
        {
            StopStartMeasureNumeric.Value--;
            StopStartBeatNumeric.Value = StopStartDivisionNumeric.Value - 1;
        }
        
        // Clamp
        if (StopStartBeatNumeric.Value == -1) StopStartBeatNumeric.Value = 0;
        
        blockUpdates = false;
    }
    
    private void StopStartDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (StopStartMeasureNumeric.Value == null || StopStartDivisionNumeric.Value == null || StopStartBeatNumeric.Value == null) return;

        if (e.NewValue <= decimal.Abs(StopStartBeatNumeric.Value ?? 0))
        {
            StopStartBeatNumeric.Value = (e.NewValue - 1) * decimal.Sign(StopStartBeatNumeric.Value ?? 0);
        }
    }

    private void StopEndBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (blockUpdates) return;
        if (StopEndMeasureNumeric.Value == null || StopEndDivisionNumeric.Value == null || StopEndBeatNumeric.Value == null) return;

        blockUpdates = true;
        
        // Overflow
        if (decimal.Abs(e.NewValue ?? 0) >= StopEndDivisionNumeric.Value)
        {
            StopEndMeasureNumeric.Value += decimal.Sign(e.NewValue ?? 0);
            StopEndBeatNumeric.Value = 0;
        }
        
        // Flip
        if (StopEndMeasureNumeric.Value > 0 && e.NewValue < 0)
        {
            StopEndMeasureNumeric.Value--;
            StopEndBeatNumeric.Value = StopEndDivisionNumeric.Value - 1;
        }
        
        // Clamp
        if (StopEndBeatNumeric.Value == -1) StopEndBeatNumeric.Value = 0;
        
        blockUpdates = false;
    }

    private void StopEndDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (StopEndMeasureNumeric.Value == null || StopEndDivisionNumeric.Value == null || StopEndBeatNumeric.Value == null) return;

        if (e.NewValue <= decimal.Abs(StopEndBeatNumeric.Value ?? 0))
        {
            StopEndBeatNumeric.Value = (e.NewValue - 1) * decimal.Sign(StopEndBeatNumeric.Value ?? 0);
        }
    }
}