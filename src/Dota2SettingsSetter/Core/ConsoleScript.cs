using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dota2SettingsSetter.Core;

public sealed class ConsoleScriptResult
{
    public List<string> Commands { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();

    /// <summary>All commands on one line, ready to paste into the Dota console.</summary>
    public string OneLine => string.Join("; ", Commands);
}

/// <summary>
/// Builds console <c>bind</c>/<c>unbind</c> commands that switch hotkeys in game.
/// The console binds plain keys only: modifier combos, cast modes and shop/talent-tree
/// keys need the keybind file, so those come back as warnings instead of commands.
/// </summary>
public static class ConsoleScript
{
    /// <summary>Only the keys that change when going from one state to another (null = Default).</summary>
    public static ConsoleScriptResult Diff(DefaultSnapshot defaults, Profile from, Profile to)
    {
        var result = new ConsoleScriptResult();
        var fromKeys = Keymap.GameKeys(defaults, from);
        var toKeys = Keymap.GameKeys(defaults, to, result.Warnings);
        var keys = fromKeys.Keys.Union(toKeys.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(k => !ReferenceEquals(Lookup(fromKeys, k), Lookup(toKeys, k)));
        Emit(result, keys, fromKeys, toKeys);
        WarnAboutFileOnlyChanges(result, defaults, from, to);
        return result;
    }

    /// <summary>
    /// Sets every key that any profile changes to what <paramref name="to"/> wants, so the
    /// result is correct no matter which profile was active before.
    /// </summary>
    public static ConsoleScriptResult Absolute(DefaultSnapshot defaults, IEnumerable<Profile> profiles, Profile to)
    {
        var result = new ConsoleScriptResult();
        var baseKeys = Keymap.GameKeys(defaults, null);
        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            var keys = Keymap.GameKeys(defaults, profile);
            foreach (var key in keys.Keys.Union(baseKeys.Keys, StringComparer.OrdinalIgnoreCase))
                if (!ReferenceEquals(Lookup(baseKeys, key), Lookup(keys, key)))
                    managed.Add(key);
        }
        var toKeys = Keymap.GameKeys(defaults, to, result.Warnings);
        Emit(result, managed, baseKeys, toKeys);
        WarnAboutFileOnlyChanges(result, defaults, null, to);
        return result;
    }

    static ActionInfo Lookup(Dictionary<string, ActionInfo> keys, string key) =>
        keys.TryGetValue(key, out var action) ? action : null;

    static void Emit(ConsoleScriptResult result, IEnumerable<string> keys,
        Dictionary<string, ActionInfo> fromKeys, Dictionary<string, ActionInfo> toKeys)
    {
        var ordered = keys
            .Select(k => (Key: k, Action: Lookup(toKeys, k)))
            .OrderBy(x => x.Action == null ? 1 : 0)
            .ThenBy(x => x.Action?.Order ?? 0)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, action) in ordered)
        {
            var consoleKey = KeyNames.ToConsole(key);
            if (action != null && action.CanUseConsole)
            {
                result.Commands.Add($"bind \"{consoleKey}\" \"{action.ConsoleCommand}\"");
                continue;
            }
            if (action != null)
            {
                result.Warnings.Add($"\"{action.Label}\" on {KeyNames.Display(key)} can't be set from the console; use Write to Dota.");
                continue;
            }

            // Unbinding a key that held something the console can't put back (like team chat)
            // would lose it for good, so leave it to the keybind file.
            var previous = Lookup(fromKeys, key);
            if (previous != null && !previous.CanUseConsole)
                result.Warnings.Add($"{KeyNames.Display(key)} still triggers \"{previous.Label}\" until you use Write to Dota.");
            else
                result.Commands.Add($"unbind \"{consoleKey}\"");
        }
    }

    static void WarnAboutFileOnlyChanges(ConsoleScriptResult result, DefaultSnapshot defaults, Profile from, Profile to)
    {
        foreach (var action in Keymap.ChangedActions(defaults, from, to))
        {
            // Alt variants follow their parent key inside Dota, so they need no command of their own.
            if (action.DependsOn != null)
                continue;
            if (action.Context != BindContext.Game)
            {
                result.Warnings.Add($"\"{action.Label}\" is a menu hotkey that only Write to Dota can change.");
                continue;
            }
            var before = Keymap.Effective(defaults, from, action.Name);
            var after = Keymap.Effective(defaults, to, action.Name);
            if (before.HasModifier || after.HasModifier)
                result.Warnings.Add($"\"{action.Label}\" uses {(after.HasModifier ? after.Display : before.Display)}; the console can't bind modifier combos, so use Write to Dota for it.");
        }
    }

    /// <summary>Text of a .cfg file Dota runs with <c>exec</c>.</summary>
    public static string ToCfg(ConsoleScriptResult result, string title)
    {
        var sb = new StringBuilder();
        sb.Append(ExecFiles.Marker).Append(" - ").Append(title).Append('\n');
        sb.Append("// Changes made in the app overwrite this file.\n");
        foreach (var command in result.Commands)
            sb.Append(command).Append('\n');
        sb.Append("echo \"[Dota 2 Settings Setter] ").Append(title.Replace("\"", "'")).Append("\"\n");
        return sb.ToString();
    }
}

/// <summary>
/// Writes one cfg per profile into Dota's cfg folder so switching in game is a short
/// <c>exec dss_name</c> instead of a long paste.
/// </summary>
public static class ExecFiles
{
    public const string Prefix = "dss_";
    public const string DefaultName = "default";
    public const string Marker = "// Generated by Dota 2 Settings Setter";

    public static string ExecName(Profile profile) => Prefix + (profile == null ? DefaultName : profile.Slug);

    public static string ExecCommand(Profile profile) => "exec " + ExecName(profile);

    /// <summary>Writes Default and every profile, and removes cfgs left behind by deleted profiles.</summary>
    public static void Write(string cfgDir, DefaultSnapshot defaults, IReadOnlyList<Profile> profiles)
    {
        var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ExecName(null) + ".cfg"] = ConsoleScript.ToCfg(ConsoleScript.Absolute(defaults, profiles, null), "Switched to Default"),
        };
        foreach (var profile in profiles)
            wanted[ExecName(profile) + ".cfg"] = ConsoleScript.ToCfg(ConsoleScript.Absolute(defaults, profiles, profile), "Switched to " + profile.Name);

        foreach (var pair in wanted)
        {
            var path = Path.Combine(cfgDir, pair.Key);
            if (!File.Exists(path) || File.ReadAllText(path) != pair.Value)
                File.WriteAllText(path, pair.Value, new UTF8Encoding(false));
        }

        foreach (var path in Directory.GetFiles(cfgDir, Prefix + "*.cfg"))
        {
            if (wanted.ContainsKey(Path.GetFileName(path)))
                continue;
            string head;
            using (var reader = new StreamReader(path))
                head = reader.ReadLine() ?? "";
            if (head.StartsWith(Marker, StringComparison.Ordinal))
                File.Delete(path);
        }
    }
}
