using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
}