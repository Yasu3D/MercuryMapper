using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Gimmicks;

public partial class GimmickView_Reverse : UserControl
{
    public GimmickView_Reverse(int measure, int beat, int division)
    {
        InitializeComponent();
        
        ReverseEffectStartMeasureNumeric.Value = measure; 
        ReverseEffectStartBeatNumeric.Value = beat;
        ReverseEffectStartDivisionNumeric.Value = division;
        ReverseEffectEndMeasureNumeric.Value = measure + 1;
        ReverseEffectEndBeatNumeric.Value = beat;
        ReverseEffectEndDivisionNumeric.Value = division;
        ReverseNoteEndMeasureNumeric.Value = measure + 2;
        ReverseNoteEndBeatNumeric.Value = beat;
        ReverseNoteEndDivisionNumeric.Value = division;
    }
    
    public bool IsValueNull =>
        ReverseEffectStartMeasureNumeric.Value is null
        || ReverseEffectStartBeatNumeric.Value is null
        || ReverseEffectStartDivisionNumeric.Value is null
        || ReverseEffectEndMeasureNumeric.Value is null
        || ReverseEffectEndBeatNumeric.Value is null
        || ReverseEffectEndDivisionNumeric.Value is null
        || ReverseNoteEndMeasureNumeric.Value is null
        || ReverseNoteEndBeatNumeric.Value is null
        || ReverseNoteEndDivisionNumeric.Value is null;
    
    public float EffectStartMeasureDecimal => (float)ReverseEffectStartMeasureNumeric.Value! + (float)ReverseEffectStartBeatNumeric.Value! / (float)ReverseEffectStartDivisionNumeric.Value!;
    public float EffectEndMeasureDecimal => (float)ReverseEffectEndMeasureNumeric.Value! + (float)ReverseEffectEndBeatNumeric.Value! / (float)ReverseEffectEndDivisionNumeric.Value!;
    public float NoteEndMeasureDecimal => (float)ReverseNoteEndMeasureNumeric.Value! + (float)ReverseNoteEndBeatNumeric.Value! / (float)ReverseNoteEndDivisionNumeric.Value!;

    private bool blockUpdates;
    
    private void ReverseEffectStartBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (blockUpdates) return;
        if (ReverseEffectStartMeasureNumeric.Value == null || ReverseEffectStartDivisionNumeric.Value == null || ReverseEffectStartBeatNumeric.Value == null) return;

        blockUpdates = true;
        
        // Overflow
        if (decimal.Abs(e.NewValue ?? 0) >= ReverseEffectStartDivisionNumeric.Value)
        {
            ReverseEffectStartMeasureNumeric.Value += decimal.Sign(e.NewValue ?? 0);
            ReverseEffectStartBeatNumeric.Value = 0;
        }
        
        // Flip
        if (ReverseEffectStartMeasureNumeric.Value > 0 && e.NewValue < 0)
        {
            ReverseEffectStartMeasureNumeric.Value--;
            ReverseEffectStartBeatNumeric.Value = ReverseEffectStartDivisionNumeric.Value - 1;
        }
        
        // Clamp
        if (ReverseEffectStartBeatNumeric.Value == -1) ReverseEffectStartBeatNumeric.Value = 0;
        
        blockUpdates = false;
    }

    private void ReverseEffectStartDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ReverseEffectStartMeasureNumeric.Value == null || ReverseEffectStartDivisionNumeric.Value == null || ReverseEffectStartBeatNumeric.Value == null) return;

        if (e.NewValue <= decimal.Abs(ReverseEffectStartBeatNumeric.Value ?? 0))
        {
            ReverseEffectStartBeatNumeric.Value = (e.NewValue - 1) * decimal.Sign(ReverseEffectStartBeatNumeric.Value ?? 0);
        }
    }

    private void ReverseEffectEndBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (blockUpdates) return;
        if (ReverseEffectEndMeasureNumeric.Value == null || ReverseEffectEndDivisionNumeric.Value == null || ReverseEffectEndBeatNumeric.Value == null) return;

        blockUpdates = true;
        
        // Overflow
        if (decimal.Abs(e.NewValue ?? 0) >= ReverseEffectEndDivisionNumeric.Value)
        {
            ReverseEffectEndMeasureNumeric.Value += decimal.Sign(e.NewValue ?? 0);
            ReverseEffectEndBeatNumeric.Value = 0;
        }
        
        // Flip
        if (ReverseEffectEndMeasureNumeric.Value > 0 && e.NewValue < 0)
        {
            ReverseEffectEndMeasureNumeric.Value--;
            ReverseEffectEndBeatNumeric.Value = ReverseEffectEndDivisionNumeric.Value - 1;
        }
        
        // Clamp
        if (ReverseEffectEndBeatNumeric.Value == -1) ReverseEffectEndBeatNumeric.Value = 0;
        
        blockUpdates = false;
    }

    private void ReverseEffectEndDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ReverseEffectEndMeasureNumeric.Value == null || ReverseEffectEndDivisionNumeric.Value == null || ReverseEffectEndBeatNumeric.Value == null) return;

        if (e.NewValue <= decimal.Abs(ReverseEffectEndBeatNumeric.Value ?? 0))
        {
            ReverseEffectEndBeatNumeric.Value = (e.NewValue - 1) * decimal.Sign(ReverseEffectEndBeatNumeric.Value ?? 0);
        }
    }

    private void ReverseNoteEndBeatNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (blockUpdates) return;
        if (ReverseNoteEndMeasureNumeric.Value == null || ReverseNoteEndDivisionNumeric.Value == null || ReverseNoteEndBeatNumeric.Value == null) return;

        blockUpdates = true;
        
        // Overflow
        if (decimal.Abs(e.NewValue ?? 0) >= ReverseNoteEndDivisionNumeric.Value)
        {
            ReverseNoteEndMeasureNumeric.Value += decimal.Sign(e.NewValue ?? 0);
            ReverseNoteEndBeatNumeric.Value = 0;
        }
        
        // Flip
        if (ReverseNoteEndMeasureNumeric.Value > 0 && e.NewValue < 0)
        {
            ReverseNoteEndMeasureNumeric.Value--;
            ReverseNoteEndBeatNumeric.Value = ReverseNoteEndDivisionNumeric.Value - 1;
        }
        
        // Clamp
        if (ReverseNoteEndBeatNumeric.Value == -1) ReverseNoteEndBeatNumeric.Value = 0;
        
        blockUpdates = false;
    }

    private void ReverseNoteEndDivisionNumeric_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ReverseNoteEndMeasureNumeric.Value == null || ReverseNoteEndDivisionNumeric.Value == null || ReverseNoteEndBeatNumeric.Value == null) return;

        if (e.NewValue <= decimal.Abs(ReverseNoteEndBeatNumeric.Value ?? 0))
        {
            ReverseNoteEndBeatNumeric.Value = (e.NewValue - 1) * decimal.Sign(ReverseNoteEndBeatNumeric.Value ?? 0);
        }
    }
}