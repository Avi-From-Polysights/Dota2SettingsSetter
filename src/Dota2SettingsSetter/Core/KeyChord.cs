using System;

namespace Dota2SettingsSetter.Core;

/// <summary>
/// A key plus an optional modifier, using Dota's own key names (Q, MOUSE4, KP_8, SEMICOLON...).
/// An empty key means "unbound". Comparison ignores case because Dota writes both "Q" and "q".
/// </summary>
public readonly struct KeyChord : IEquatable<KeyChord>
{
    public static readonly KeyChord None = new KeyChord(null, null);

    public string Key { get; }
    public string Modifier { get; }

    public KeyChord(string key, string modifier = null)
    {
        Key = KeyNames.Normalize(key);
        Modifier = string.IsNullOrEmpty(Key) ? "" : NormalizeModifier(modifier);
    }

    public bool IsEmpty => string.IsNullOrEmpty(Key);
    public bool HasModifier => Modifier.Length > 0;

    public static string NormalizeModifier(string modifier) =>
        string.IsNullOrWhiteSpace(modifier) ? "" : modifier.Trim().ToUpperInvariant();

    public bool Equals(KeyChord other) =>
        string.Equals(Key ?? "", other.Key ?? "", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Modifier ?? "", other.Modifier ?? "", StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object obj) => obj is KeyChord other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Key ?? "") * 31
        + StringComparer.OrdinalIgnoreCase.GetHashCode(Modifier ?? "");

    public static bool operator ==(KeyChord a, KeyChord b) => a.Equals(b);
    public static bool operator !=(KeyChord a, KeyChord b) => !a.Equals(b);

    /// <summary>Stable text form used in profile files and tests, e.g. "ALT+Q".</summary>
    public override string ToString() => IsEmpty ? "" : HasModifier ? Modifier + "+" + Key : Key;

    /// <summary>Friendly text for the UI, e.g. "Alt + Mouse 4".</summary>
    public string Display =>
        IsEmpty ? "—" : HasModifier ? KeyNames.DisplayModifier(Modifier) + " + " + KeyNames.Display(Key) : KeyNames.Display(Key);
}
