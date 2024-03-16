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
}