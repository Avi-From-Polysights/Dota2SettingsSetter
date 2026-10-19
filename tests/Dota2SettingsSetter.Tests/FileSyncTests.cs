using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class FileSyncTests
{
    readonly DefaultSnapshot defaults = Fixture.SampleDefault();

    Profile Invoker()
    {
        var profile = new Profile("Invoker");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.Swap);
        return profile;
    }

    [Fact]
    public void Writing_a_profile_then_default_restores_the_file_exactly()
    {
        var file = Fixture.SampleFile();
        var invoker = Invoker();
        var all = new[] { invoker };

        FileSync.Apply(file, defaults, all, invoker);
        Assert.Equal(new KeyChord("Q"), file.GetChord("Inventory1"));
        Assert.Equal(new KeyChord("1"), file.GetChord("AbilityPrimary1"));
        Assert.Equal(new KeyChord("Q", "ALT"), file.GetChord("Inventory1AutoCast"));

        FileSync.Apply(file, defaults, all, null);
        Assert.Equal(Fixture.SampleText, file.Serialize());
    }

    [Fact]
    public void Entries_the_app_added_are_removed_on_restore()
    {
        var file = Fixture.SampleFile();
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("CourierShield"), new KeyChord("F6"), ConflictResolution.Swap);

        FileSync.Apply(file, defaults, new[] { profile }, profile);
        Assert.True(file.HasEntry("CourierShield"));
        FileSync.Apply(file, defaults, new[] { profile }, null);
        Assert.Equal(Fixture.SampleText, file.Serialize());
    }

    [Fact]
    public void Switching_between_profiles_resets_keys_the_new_one_doesnt_change()
    {
        var file = Fixture.SampleFile();
        var invoker = Invoker();
        var kez = new Profile("Kez");
        Keymap.Assign(defaults, kez, Fixture.Action("Inventory6"), new KeyChord("V"), ConflictResolution.Swap);
        var all = new[] { invoker, kez };

        FileSync.Apply(file, defaults, all, invoker);
        FileSync.Apply(file, defaults, all, kez);

        Assert.Equal(new KeyChord("1"), file.GetChord("Inventory1"));
        Assert.Equal(new KeyChord("V"), file.GetChord("Inventory6"));
    }

    [Fact]
    public void No_drift_right_after_writing()
    {
        var file = Fixture.SampleFile();
        var invoker = Invoker();
        FileSync.Apply(file, defaults, new[] { invoker }, invoker);
        Assert.Empty(FileSync.Drift(file, defaults, invoker));
        Assert.Empty(FileSync.Drift(Fixture.SampleFile(), defaults, null));
    }

    [Fact]
    public void Drift_spots_changes_made_inside_dota()
    {
        var file = Fixture.SampleFile();
        file.SetChord("ActivateGlyph", new KeyChord("K"));
        Assert.Equal(new[] { "ActivateGlyph" }, FileSync.Drift(file, defaults, null));
    }

    [Fact]
    public void Recapture_keeps_default_values_under_an_applied_profile()
    {
        var file = Fixture.SampleFile();
        var invoker = Invoker();
        FileSync.Apply(file, defaults, new[] { invoker }, invoker);
        file.SetChord("ActivateGlyph", new KeyChord("K"));

        var fresh = FileSync.Recapture(file, defaults, new[] { invoker }, invoker);
        Assert.Equal(new KeyChord("K"), fresh.Get("ActivateGlyph"));
        Assert.Equal(new KeyChord("1"), fresh.Get("Inventory1"));
        Assert.Equal(new KeyChord("Q"), fresh.Get("AbilityPrimary1"));
    }
}
