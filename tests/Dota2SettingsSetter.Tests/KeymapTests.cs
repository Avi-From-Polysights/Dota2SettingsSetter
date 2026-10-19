using System.Linq;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class KeymapTests
{
    readonly DefaultSnapshot defaults = Fixture.SampleDefault();

    [Fact]
    public void Swap_gives_the_other_action_the_old_key()
    {
        var profile = new Profile("Invoker");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.Swap);

        Assert.Equal(new KeyChord("Q"), Keymap.Effective(defaults, profile, "Inventory1"));
        Assert.Equal(new KeyChord("1"), Keymap.Effective(defaults, profile, "AbilityPrimary1"));
    }

    [Fact]
    public void Unbind_others_clears_the_conflicting_action()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.UnbindOthers);
        Assert.True(Keymap.Effective(defaults, profile, "AbilityPrimary1").IsEmpty);
    }

    [Fact]
    public void Keep_both_leaves_the_other_action_alone()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.KeepBoth);
        Assert.Equal(new KeyChord("Q"), Keymap.Effective(defaults, profile, "AbilityPrimary1"));
        Assert.Single(Keymap.ConflictsFor(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q")));
    }

    [Fact]
    public void Conflicts_only_count_hotkeys_live_at_the_same_time()
    {
        // The shop's Consumables tab also sits on Q, but only while the shop is open.
        var conflicts = Keymap.ConflictsFor(defaults, null, Fixture.Action("Inventory1"), new KeyChord("Q"));
        Assert.Equal(new[] { "AbilityPrimary1" }, conflicts.Select(a => a.Name));
    }

    [Fact]
    public void Alt_autocast_follows_its_item_slot()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory4"), new KeyChord("V"), ConflictResolution.Swap);
        Assert.Equal(new KeyChord("V", "ALT"), Keymap.Effective(defaults, profile, "Inventory4AutoCast"));
    }

    [Fact]
    public void Swapped_action_brings_its_alt_autocast_along()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.Swap);

        Assert.Equal(new KeyChord("Q", "ALT"), Keymap.Effective(defaults, profile, "Inventory1AutoCast"));
        Assert.Equal(new KeyChord("1", "ALT"), Keymap.Effective(defaults, profile, "AbilityPrimary1AutoCast"));
    }

    [Fact]
    public void Shift_chat_follows_team_chat()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("ChatTeam"), new KeyChord("KP_ENTER"), ConflictResolution.Swap);
        Assert.Equal(new KeyChord("KP_ENTER", "SHIFT"), Keymap.Effective(defaults, profile, "ChatGlobal"));
    }

    [Fact]
    public void Unbinding_an_item_slot_unbinds_its_autocast()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory4"), KeyChord.None, ConflictResolution.Swap);
        Assert.True(Keymap.Effective(defaults, profile, "Inventory4AutoCast").IsEmpty);
    }

    [Fact]
    public void Moving_back_to_the_default_key_leaves_no_changes()
    {
        var profile = new Profile("p");
        var slot = Fixture.Action("Inventory4");
        Keymap.Assign(defaults, profile, slot, new KeyChord("V"), ConflictResolution.Swap);
        Keymap.Assign(defaults, profile, slot, new KeyChord("Z"), ConflictResolution.Swap);
        Assert.Empty(profile.Overrides);
    }

    [Fact]
    public void Reset_restores_the_action_and_its_dependents()
    {
        var profile = new Profile("p");
        var slot = Fixture.Action("Inventory4");
        Keymap.Assign(defaults, profile, slot, new KeyChord("V"), ConflictResolution.Swap);
        Keymap.Reset(defaults, profile, slot);
        Assert.Empty(profile.Overrides);
    }

    [Fact]
    public void Game_keys_reports_clashes_and_keeps_the_first_action()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.KeepBoth);
        var clashes = new System.Collections.Generic.List<string>();
        var keys = Keymap.GameKeys(defaults, profile, clashes);
        Assert.Equal("AbilityPrimary1", keys["Q"].Name);
        Assert.Single(clashes);
    }
}
