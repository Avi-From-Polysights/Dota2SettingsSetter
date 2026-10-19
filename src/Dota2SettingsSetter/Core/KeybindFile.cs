using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dota2SettingsSetter.Core;

/// <summary>
/// Dota's dotakeys_personal.lst. Only the "Keys" section is touched; per-hero bindings,
/// item cast modes, hero builds and everything else are written back exactly as read.
/// Within an entry only "Key" and "Modifier" change, so cast modes ("Mode") are preserved.
/// </summary>
public sealed class KeybindFile
{
    public KvNode Root { get; }
    KvNode Keys => Root.GetOrAddBlock("Keys");

    KeybindFile(KvNode root)
    {
        Root = root;
    }

    public static KeybindFile Parse(string text)
    {
        var root = KeyValues.Parse(text);
        if (!string.Equals(root.Key, "KeyBindings", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Not a Dota keybind file (root is '{root.Key}', expected 'KeyBindings').");
        return new KeybindFile(root);
    }

    public static KeybindFile Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public string Serialize() => KeyValues.Write(Root);

    /// <summary>Writes via a temp file so a crash can't leave Dota with a half-written file.</summary>
    public void Save(string path)
    {
        var temp = path + ".d2ss-tmp";
        File.WriteAllText(temp, Serialize(), new UTF8Encoding(false));
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }

    public IEnumerable<string> EntryNames => Keys.Children.Where(c => c.IsBlock).Select(c => c.Key);

    KvNode Entry(string action)
    {
        var node = Keys.Child(action);
        return node != null && node.IsBlock ? node : null;
    }

    public bool HasEntry(string action) => Entry(action) != null;

    public KeyChord GetChord(string action)
    {
        var entry = Entry(action);
        return entry == null ? KeyChord.None : new KeyChord(entry.GetValue("Key"), entry.GetValue("Modifier"));
    }

    /// <summary>Sets an action's key, creating a minimal entry if Dota hasn't written one yet.</summary>
    public void SetChord(string action, KeyChord chord)
    {
        var entry = Entry(action);
        if (entry == null)
        {
            if (chord.IsEmpty)
            {
                Keys.Children.Add(new KvNode(action));
                return;
            }
            entry = new KvNode(action);
            Keys.Children.Add(entry);
        }

        if (chord.IsEmpty)
        {
            entry.Remove("Key");
            entry.Remove("Modifier");
            return;
        }

        entry.SetValue("Key", chord.Key);
        if (chord.HasModifier)
            entry.SetValue("Modifier", chord.Modifier);
        else
            entry.Remove("Modifier");
    }

    public void RemoveEntry(string action)
    {
        var entry = Entry(action);
        if (entry != null)
            Keys.Children.Remove(entry);
    }
}
