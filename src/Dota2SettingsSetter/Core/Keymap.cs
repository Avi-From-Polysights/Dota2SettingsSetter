using System;
using System.Collections.Generic;
using System.Linq;

namespace Dota2SettingsSetter.Core;

public enum ConflictResolution
{
    /// <summary>The other action takes this action's previous key.</summary>
    Swap,
    UnbindOthers,
    KeepBoth,
}

/// <summary>Reading and editing the hotkeys a profile ends up with on top of Default.</summary>
public static class Keymap
{
    /// <summary>The key an action has under a profile (null profile = Default).</summary>
    public static KeyChord Effective(DefaultSnapshot defaults, Profile profile, string action) =>
        profile != null && profile.TryGetOverride(action, out var chord) ? chord : defaults.Get(action);

    /// <summary>Other actions, live at the same time, that already use <paramref name="chord"/>.</summary>
    public static List<ActionInfo> ConflictsFor(DefaultSnapshot defaults, Profile profile, ActionInfo action, KeyChord chord)
    {
        if (chord.IsEmpty)
            return new List<ActionInfo>();
        return ActionCatalog.All
            .Where(a => a != action && a.Context == action.Context && !AreLinked(a, action)
                && Effective(defaults, profile, a.Name) == chord)
            .ToList();
    }

    public static void Assign(DefaultSnapshot defaults, Profile profile, ActionInfo action, KeyChord chord, ConflictResolution resolution)
    {
        var previous = Effective(defaults, profile, action.Name);
        var conflicts = ConflictsFor(defaults, profile, action, chord);

        SetFollowingDependents(defaults, profile, action, chord);
        if (resolution != ConflictResolution.KeepBoth)
        {
            var replacement = resolution == ConflictResolution.Swap ? previous : KeyChord.None;
            foreach (var other in conflicts)
                SetFollowingDependents(defaults, profile, other, replacement);
        }
        Normalize(defaults, profile);
    }

    /// <summary>Drops the profile's change to an action (and its Alt variants) so it uses Default again.</summary>
    public static void Reset(DefaultSnapshot defaults, Profile profile, ActionInfo action)
    {
        profile.Overrides.Remove(action.Name);
        foreach (var dependent in ActionCatalog.DependentsOf(action.Name))
            profile.Overrides.Remove(dependent.Name);
        Normalize(defaults, profile);
    }

    /// <summary>Removes overrides that match Default, so a profile only ever stores real changes.</summary>
    public static void Normalize(DefaultSnapshot defaults, Profile profile)
    {
        foreach (var action in profile.Overrides.Keys.ToList())
            if (profile.Overrides[action] == defaults.Get(action))
                profile.Overrides.Remove(action);
    }

    static bool AreLinked(ActionInfo a, ActionInfo b) =>
        string.Equals(a.DependsOn, b.Name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(b.DependsOn, a.Name, StringComparison.OrdinalIgnoreCase);

    // Item/ability autocast and "chat to everyone" sit on the same key as their parent
    // action plus a modifier. When they were following the parent, keep them following.
    static void SetFollowingDependents(DefaultSnapshot defaults, Profile profile, ActionInfo action, KeyChord chord)
    {
        var previous = Effective(defaults, profile, action.Name);
        profile.Overrides[action.Name] = chord;

        foreach (var dependent in ActionCatalog.DependentsOf(action.Name))
        {
            var current = Effective(defaults, profile, dependent.Name);
            if (current.IsEmpty || previous.IsEmpty || !string.Equals(current.Key, previous.Key, StringComparison.OrdinalIgnoreCase))
                continue;
            var follow = chord.IsEmpty ? KeyChord.None : new KeyChord(chord.Key, current.HasModifier ? current.Modifier : dependent.DependencyModifier);
            SetFollowingDependents(defaults, profile, dependent, follow);
        }
    }

    /// <summary>Unmodified keys in normal gameplay and the action each one triggers, first action winning on clashes.</summary>
    public static Dictionary<string, ActionInfo> GameKeys(DefaultSnapshot defaults, Profile profile, List<string> clashes = null)
    {
        var keys = new Dictionary<string, ActionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in ActionCatalog.All)
        {
            if (action.Context != BindContext.Game)
                continue;
            var chord = Effective(defaults, profile, action.Name);
            if (chord.IsEmpty || chord.HasModifier)
                continue;
            if (keys.TryGetValue(chord.Key, out var first))
                clashes?.Add($"{chord.Display} is used by both \"{first.Label}\" and \"{action.Label}\"; in-game commands use \"{first.Label}\".");
            else
                keys[chord.Key] = action;
        }
        return keys;
    }

    /// <summary>Every action whose hotkey differs between two states.</summary>
    public static List<ActionInfo> ChangedActions(DefaultSnapshot defaults, Profile from, Profile to) =>
        ActionCatalog.All.Where(a => Effective(defaults, from, a.Name) != Effective(defaults, to, a.Name)).ToList();
}
