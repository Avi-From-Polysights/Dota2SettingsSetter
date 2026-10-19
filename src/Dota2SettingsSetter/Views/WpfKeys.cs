using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;

namespace Dota2SettingsSetter.Views;

/// <summary>Translates WPF key and mouse input into Dota's key names.</summary>
public static class WpfKeys
{
    static readonly Dictionary<Key, string> Names = new Dictionary<Key, string>
    {
        [Key.Space] = "SPACE",
        [Key.Tab] = "TAB",
        [Key.Enter] = "ENTER",
        [Key.Back] = "BACKSPACE",
        [Key.CapsLock] = "CAPSLOCK",
        [Key.Insert] = "INS",
        [Key.Delete] = "DEL",
        [Key.Home] = "HOME",
        [Key.End] = "END",
        [Key.PageUp] = "PGUP",
        [Key.PageDown] = "PGDN",
        [Key.Up] = "UPARROW",
        [Key.Down] = "DOWNARROW",
        [Key.Left] = "LEFTARROW",
        [Key.Right] = "RIGHTARROW",
        [Key.Pause] = "PAUSE",
        [Key.Scroll] = "SCROLLLOCK",
        [Key.NumLock] = "NUMLOCK",
        [Key.Apps] = "APP",
        [Key.LWin] = "LWIN",
        [Key.RWin] = "RWIN",
        [Key.Multiply] = "KP_MULTIPLY",
        [Key.Add] = "KP_PLUS",
        [Key.Subtract] = "KP_MINUS",
        [Key.Divide] = "KP_DIVIDE",
        [Key.Decimal] = "KP_DEL",
        [Key.OemTilde] = "`",
        [Key.OemMinus] = "-",
        [Key.OemPlus] = "=",
        [Key.OemOpenBrackets] = "[",
        [Key.OemCloseBrackets] = "]",
        [Key.OemPipe] = "\\",
        [Key.OemBackslash] = "\\",
        [Key.OemSemicolon] = "SEMICOLON",
        [Key.OemQuotes] = "'",
        [Key.OemComma] = ",",
        [Key.OemPeriod] = ".",
        [Key.OemQuestion] = "/",
        [Key.LeftShift] = "SHIFT",
        [Key.RightShift] = "RSHIFT",
        [Key.LeftCtrl] = "CTRL",
        [Key.RightCtrl] = "RCTRL",
        [Key.LeftAlt] = "ALT",
        [Key.RightAlt] = "RALT",
    };

    static readonly PropertyInfo IsExtendedKeyProperty =
        typeof(KeyEventArgs).GetProperty("IsExtendedKey", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>The physical key behind an event, unwrapping Alt (system) and IME keys.</summary>
    public static Key RealKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.System: return e.SystemKey;
            case Key.ImeProcessed: return e.ImeProcessedKey;
            case Key.DeadCharProcessed: return e.DeadCharProcessedKey;
            default: return e.Key;
        }
    }

    public static bool IsModifier(Key key) =>
        key == Key.LeftShift || key == Key.RightShift || key == Key.LeftCtrl || key == Key.RightCtrl
        || key == Key.LeftAlt || key == Key.RightAlt;

    /// <summary>Dota's name for a key, or null when Dota can't bind it.</summary>
    public static string ToDota(Key key, KeyEventArgs e = null)
    {
        if (key >= Key.A && key <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();
        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return "KP_" + (key - Key.NumPad0);
        if (key >= Key.F1 && key <= Key.F24)
            return "F" + (key - Key.F1 + 1);
        if (key == Key.Enter && e != null && IsExtendedKeyProperty?.GetValue(e) is bool extended && extended)
            return "KP_ENTER";
        return Names.TryGetValue(key, out var name) ? name : null;
    }

    /// <summary>The single modifier Dota stores with a key; Alt wins, then Ctrl, then Shift.</summary>
    public static string CurrentModifier()
    {
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Alt) != 0)
            return "ALT";
        if ((modifiers & ModifierKeys.Control) != 0)
            return "CTRL";
        if ((modifiers & ModifierKeys.Shift) != 0)
            return "SHIFT";
        return "";
    }

    public static string ToDota(MouseButton button)
    {
        switch (button)
        {
            case MouseButton.Middle: return "MOUSE3";
            case MouseButton.XButton1: return "MOUSE4";
            case MouseButton.XButton2: return "MOUSE5";
            default: return null;
        }
    }
}
