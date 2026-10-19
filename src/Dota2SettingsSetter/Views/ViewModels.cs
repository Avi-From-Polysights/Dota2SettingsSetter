using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Dota2SettingsSetter.Core;

namespace Dota2SettingsSetter.Views;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One hotkey row in the editor.</summary>
public sealed class BindingRow : Observable
{
    public ActionInfo Action { get; }
    public string Label => Action.Label;
    public string GroupHeader { get; }

    string chordText, wasText, conflictText;
    bool isOverridden, isEditable, isUnbound;

    public BindingRow(ActionInfo action)
    {
        Action = action;
        GroupHeader = action.Group.ToUpperInvariant();
    }

    public string ChordText { get => chordText; private set => Set(ref chordText, value); }
    public string WasText { get => wasText; private set => Set(ref wasText, value); }
    public bool IsOverridden { get => isOverridden; private set => Set(ref isOverridden, value); }
    public bool IsEditable { get => isEditable; private set => Set(ref isEditable, value); }
    public bool IsUnbound { get => isUnbound; private set => Set(ref isUnbound, value); }

    public string ConflictText
    {
        get => conflictText;
        private set
        {
            if (Set(ref conflictText, value))
                Raise(nameof(HasConflict));
        }
    }

    public bool HasConflict => !string.IsNullOrEmpty(conflictText);

    /// <summary>Lower-case label, key and group for the search box.</summary>
    public string SearchText { get; private set; }

    public void Update(DefaultSnapshot defaults, Profile profile)
    {
        var chord = Keymap.Effective(defaults, profile, Action.Name);
        ChordText = chord.Display;
        IsUnbound = chord.IsEmpty;
        IsEditable = profile != null;
        IsOverridden = profile != null && profile.Overrides.ContainsKey(Action.Name);
        WasText = IsOverridden ? "was " + defaults.Get(Action.Name).Display : "";

        var conflicts = Keymap.ConflictsFor(defaults, profile, Action, chord);
        ConflictText = conflicts.Count == 0
            ? ""
            : chord.Display + " is also used by " + string.Join(", ", conflicts.Select(c => "\"" + c.Label + "\"")) + ".";
        SearchText = (Action.Label + " " + chord.Display + " " + Action.Group).ToLowerInvariant();
    }
}

/// <summary>An entry in the profile list; a null profile stands for Default.</summary>
public sealed class ProfileItem : Observable
{
    public Profile Profile { get; }
    string title, subtitle;
    bool isInFile;

    public ProfileItem(Profile profile)
    {
        Profile = profile;
    }

    public bool IsDefault => Profile == null;
    public string Title { get => title; set => Set(ref title, value); }
    public string Subtitle { get => subtitle; set => Set(ref subtitle, value); }

    /// <summary>True for whichever entry is currently written into Dota's keybind file.</summary>
    public bool IsInFile { get => isInFile; set => Set(ref isInFile, value); }
}
