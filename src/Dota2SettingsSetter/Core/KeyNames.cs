using System.Collections.Generic;

namespace Dota2SettingsSetter.Core;

/// <summary>Key name handling for Dota's (Source 2) key names.</summary>
public static class KeyNames
{
    static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["SPACE"] = "Space",
        ["TAB"] = "Tab",
        ["ENTER"] = "Enter",
        ["ESCAPE"] = "Esc",
        ["BACKSPACE"] = "Backspace",
        ["CAPSLOCK"] = "Caps Lock",
        ["SHIFT"] = "Shift",
        ["RSHIFT"] = "Right Shift",
        ["CTRL"] = "Ctrl",
        ["RCTRL"] = "Right Ctrl",
        ["ALT"] = "Alt",
        ["RALT"] = "Right Alt",
        ["LWIN"] = "Win",
        ["RWIN"] = "Right Win",
        ["APP"] = "Menu",
        ["INS"] = "Insert",
        ["DEL"] = "Delete",
        ["HOME"] = "Home",
        ["END"] = "End",
        ["PGUP"] = "Page Up",
        ["PGDN"] = "Page Down",
        ["PAUSE"] = "Pause",
        ["SCROLLLOCK"] = "Scroll Lock",
        ["NUMLOCK"] = "Num Lock",
        ["UPARROW"] = "Up",
        ["DOWNARROW"] = "Down",
        ["LEFTARROW"] = "Left",
        ["RIGHTARROW"] = "Right",
        ["MOUSE1"] = "Mouse 1",
        ["MOUSE2"] = "Mouse 2",
        ["MOUSE3"] = "Mouse 3",
        ["MOUSE4"] = "Mouse 4",
        ["MOUSE5"] = "Mouse 5",
        ["MWHEELUP"] = "Wheel Up",
        ["MWHEELDOWN"] = "Wheel Down",
        ["KP_ENTER"] = "Num Enter",
        ["KP_PLUS"] = "Num +",
        ["KP_MINUS"] = "Num -",
        ["KP_MULTIPLY"] = "Num *",
        ["KP_DIVIDE"] = "Num /",
        ["KP_DEL"] = "Num .",
        ["SEMICOLON"] = ";",
    };

    /// <summary>
    /// Trims but keeps the case as written: Dota stores some keys as "q", and writing them
    /// back the same way keeps restored files identical. Comparisons ignore case.
    /// </summary>
    public static string Normalize(string key) => string.IsNullOrWhiteSpace(key) ? "" : key.Trim();

    public static string Display(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "—";
        if (DisplayNames.TryGetValue(key, out var name))
            return name;
        if (key.Length > 3 && key.StartsWith("KP_", System.StringComparison.OrdinalIgnoreCase))
            return "Num " + key.Substring(3);
        return key.ToUpperInvariant();
    }

    public static string DisplayModifier(string modifier)
    {
        switch (KeyChord.NormalizeModifier(modifier))
        {
            case "ALT": return "Alt";
            case "CTRL": return "Ctrl";
            case "SHIFT": return "Shift";
            default: return modifier;
        }
    }

    /// <summary>Name to use with the console <c>bind</c> command; the engine lists letters in lower case.</summary>
    public static string ToConsole(string key)
    {
        key = Normalize(key);
        return key.Length == 1 ? key.ToLowerInvariant() : key.ToUpperInvariant();
    }
}
