using System;
using System.Collections.Generic;
using System.Linq;

namespace Dota2SettingsSetter.Core;

/// <summary>Moves profiles in and out of dotakeys_personal.lst.</summary>
public static class FileSync
{
    /// <summary>Actions changed by at least one profile; the only entries this app ever writes.</summary>
    public static HashSet<string> ManagedActions(IEnumerable<Profile> profiles)
    {
        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
            managed.UnionWith(profile.Overrides.Keys);
        return managed;
    }

    /// <summary>
    /// Puts <paramref name="target"/> into the file (null = Default). Every managed action is
    /// set, so the result doesn't depend on which profile the file held before.
    /// </summary>
    public static void Apply(KeybindFile file, DefaultSnapshot defaults, IEnumerable<Profile> profiles, Profile target)
    {
        var managed = ManagedActions(profiles);
        if (target != null)
            managed.UnionWith(target.Overrides.Keys);

        foreach (var action in managed)
        {
            if (target != null && target.TryGetOverride(action, out var chord))
                file.SetChord(action, chord);
            else if (defaults.HasEntry(action))
                file.SetChord(action, defaults.Get(action));
            else
                file.RemoveEntry(action);
        }
    }

    /// <summary>Entries where the file no longer holds what the app last wrote.</summary>
    public static List<string> Drift(KeybindFile file, DefaultSnapshot defaults, Profile applied)
    {
        var names = new HashSet<string>(file.EntryNames, StringComparer.OrdinalIgnoreCase);
        names.UnionWith(defaults.EntryNames);
        if (applied != null)
            names.UnionWith(applied.Overrides.Keys);

        return names
            .Where(name => file.GetChord(name) != Keymap.Effective(defaults, applied, name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Takes a fresh Default from the file, keeping the old Default for actions a profile
    /// currently overrides there so the applied profile doesn't leak into Default.
    /// </summary>
    public static DefaultSnapshot Recapture(KeybindFile file, DefaultSnapshot previous, IEnumerable<Profile> profiles, Profile applied)
    {
        var fresh = DefaultSnapshot.FromFile(file);
        if (applied == null || previous == null)
            return fresh;

        foreach (var action in ManagedActions(profiles.Concat(new[] { applied })))
        {
            if (previous.HasEntry(action))
                fresh.Set(action, previous.Get(action));
            else
                fresh.RemoveEntry(action);
        }
        return fresh;
    }
}
