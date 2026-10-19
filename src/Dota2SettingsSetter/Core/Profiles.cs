using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Dota2SettingsSetter.Core;

/// <summary>
/// The player's normal hotkeys, captured from dotakeys_personal.lst. Records which entries
/// existed so restoring can remove entries this app added rather than leave them unbound.
/// </summary>
public sealed class DefaultSnapshot
{
    const string RootKey = "Dota2SettingsSetterDefault";

    readonly Dictionary<string, KeyChord> chords = new Dictionary<string, KeyChord>(StringComparer.OrdinalIgnoreCase);

    public DateTime CapturedAtUtc { get; set; }

    public IEnumerable<string> EntryNames => chords.Keys;

    public bool HasEntry(string action) => chords.ContainsKey(action);

    public KeyChord Get(string action) => chords.TryGetValue(action, out var chord) ? chord : KeyChord.None;

    public void Set(string action, KeyChord chord) => chords[action] = chord;

    public void RemoveEntry(string action) => chords.Remove(action);

    public static DefaultSnapshot FromFile(KeybindFile file)
    {
        var snapshot = new DefaultSnapshot { CapturedAtUtc = DateTime.UtcNow };
        foreach (var name in file.EntryNames)
            snapshot.chords[name] = file.GetChord(name);
        return snapshot;
    }

    public string Serialize()
    {
        var root = new KvNode(RootKey);
        root.Children.Add(new KvNode("Version", "1"));
        root.Children.Add(new KvNode("CapturedAtUtc", CapturedAtUtc.ToString("o", CultureInfo.InvariantCulture)));
        var bindings = root.GetOrAddBlock("Bindings");
        foreach (var pair in chords.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            bindings.Children.Add(ChordNode(pair.Key, pair.Value));
        return KeyValues.Write(root);
    }

    public static DefaultSnapshot Parse(string text)
    {
        var root = KeyValues.Parse(text);
        if (!string.Equals(root.Key, RootKey, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Not a saved Default snapshot.");
        var snapshot = new DefaultSnapshot();
        if (DateTime.TryParse(root.GetValue("CapturedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            snapshot.CapturedAtUtc = at;
        foreach (var node in root.Child("Bindings")?.Children ?? new List<KvNode>())
            if (node.IsBlock)
                snapshot.chords[node.Key] = ReadChord(node);
        return snapshot;
    }

    internal static KvNode ChordNode(string action, KeyChord chord)
    {
        var node = new KvNode(action);
        if (!chord.IsEmpty)
        {
            node.Children.Add(new KvNode("Key", chord.Key));
            if (chord.HasModifier)
                node.Children.Add(new KvNode("Modifier", chord.Modifier));
        }
        return node;
    }

    internal static KeyChord ReadChord(KvNode node) => new KeyChord(node.GetValue("Key"), node.GetValue("Modifier"));
}

/// <summary>
/// A hero setup: only the hotkeys that differ from Default. An empty chord means
/// "unbound in this profile".
/// </summary>
public sealed class Profile
{
    const string RootKey = "Dota2SettingsSetterProfile";

    public string Name { get; set; }
    public Dictionary<string, KeyChord> Overrides { get; } = new Dictionary<string, KeyChord>(StringComparer.OrdinalIgnoreCase);

    public Profile(string name)
    {
        Name = name;
    }

    /// <summary>Short id used for file names and the in-game <c>exec</c> command.</summary>
    public string Slug => Slugify(Name);

    public bool TryGetOverride(string action, out KeyChord chord) => Overrides.TryGetValue(action, out chord);

    public Profile Clone(string name)
    {
        var copy = new Profile(name);
        foreach (var pair in Overrides)
            copy.Overrides[pair.Key] = pair.Value;
        return copy;
    }

    public string Serialize()
    {
        var root = new KvNode(RootKey);
        root.Children.Add(new KvNode("Version", "1"));
        root.Children.Add(new KvNode("Name", Name.Replace("\"", "'")));
        var bindings = root.GetOrAddBlock("Bindings");
        foreach (var pair in Overrides.OrderBy(p => ActionCatalog.Find(p.Key)?.Order ?? int.MaxValue).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            bindings.Children.Add(DefaultSnapshot.ChordNode(pair.Key, pair.Value));
        return KeyValues.Write(root);
    }

    public static Profile Parse(string text)
    {
        var root = KeyValues.Parse(text);
        if (!string.Equals(root.Key, RootKey, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("This file isn't a Dota 2 Settings Setter profile.");
        var name = root.GetValue("Name");
        if (string.IsNullOrWhiteSpace(name))
            throw new FormatException("The profile has no name.");
        var profile = new Profile(name.Trim());
        foreach (var node in root.Child("Bindings")?.Children ?? new List<KvNode>())
            if (node.IsBlock)
                profile.Overrides[node.Key] = DefaultSnapshot.ReadChord(node);
        return profile;
    }

    public static Profile Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public static string Slugify(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in (name ?? "").ToLowerInvariant())
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
                sb.Append('_');
        }
        var slug = sb.ToString().Trim('_');
        return slug.Length == 0 ? "profile" : slug;
    }
}
