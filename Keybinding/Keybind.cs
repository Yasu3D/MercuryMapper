using Avalonia.Input;

namespace MercuryMapper.Keybinding;

public class Keybind()
{
    public Keybind(KeyEventArgs e) : this()
    {
        Key = e.Key;
        Shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        Control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        Alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
    }

    public Keybind(Key key, bool shift = false, bool control = false, bool alt = false) : this()
    {
        Key = key;
        Shift = shift;
        Control = control;
        Alt = alt;
    }
    
    public Key Key { get; set;}
    public bool Control { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }

    public static bool Compare(Keybind a, Keybind b)
    {
        return a.Key == b.Key 
               && a.Shift == b.Shift 
               && a.Control == b.Control 
               && a.Alt == b.Alt;
    }

    public override string ToString()
    {
        string result = "";
        if (Control) result += "Ctrl + ";
        if (Shift) result += "Shift + ";
        if (Alt) result += "Alt + ";
        result += Key.ToString();

        return result;
    }
}