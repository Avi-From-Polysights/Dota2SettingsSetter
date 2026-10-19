using System.IO;
using System.Linq;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class StorageTests
{
    [Fact]
    public void Profiles_round_trip_including_unbinds_and_modifiers()
    {
        var profile = new Profile("Kez (late game)");
        profile.Overrides["Inventory1"] = new KeyChord("Q");
        profile.Overrides["Inventory2"] = new KeyChord("W", "ALT");
        profile.Overrides["AbilityPrimary1"] = KeyChord.None;

        var back = Profile.Parse(profile.Serialize());
        Assert.Equal("Kez (late game)", back.Name);
        Assert.Equal(3, back.Overrides.Count);
        Assert.Equal(new KeyChord("W", "ALT"), back.Overrides["Inventory2"]);
        Assert.True(back.Overrides["AbilityPrimary1"].IsEmpty);
    }

    [Theory]
    [InlineData("Invoker", "invoker")]
    [InlineData("Kez (late game)", "kez_late_game")]
    [InlineData("  Lone Druid / Bear ", "lone_druid_bear")]
    [InlineData("!!!", "profile")]
    public void Slugs_are_console_safe(string name, string slug)
    {
        Assert.Equal(slug, Profile.Slugify(name));
    }

    [Fact]
    public void Default_snapshot_round_trips_and_remembers_empty_entries()
    {
        var snapshot = Fixture.SampleDefault();
        var back = DefaultSnapshot.Parse(snapshot.Serialize());
        Assert.True(back.HasEntry("RecentEvent"));
        Assert.True(back.Get("RecentEvent").IsEmpty);
        Assert.Equal(new KeyChord("Q", "ALT"), back.Get("AbilityPrimary1AutoCast"));
        Assert.Equal(snapshot.EntryNames.Count(), back.EntryNames.Count());
    }

    [Fact]
    public void Account_store_saves_profiles_state_and_backups()
    {
        var root = Fixture.TempDir();
        var store = new AccountStore(root, "123");
        store.SaveDefault(Fixture.SampleDefault());
        var profile = new Profile("Invoker");
        profile.Overrides["Inventory1"] = new KeyChord("Q");
        store.SaveProfile(profile);
        store.RenameProfile(profile, "Invoker Mid");
        store.SaveAppliedSlug(profile.Slug);

        Assert.NotNull(store.LoadDefault());
        Assert.Equal(new[] { "Invoker Mid" }, store.LoadProfiles().Select(p => p.Name));
        Assert.Equal("invoker_mid", store.LoadAppliedSlug());

        var keybinds = Path.Combine(root, "dotakeys_personal.lst");
        File.WriteAllText(keybinds, Fixture.SampleText);
        var backup = store.BackupKeybindFile(keybinds);
        Assert.Equal(Fixture.SampleText, File.ReadAllText(backup));
    }

    [Fact]
    public void Unreadable_profiles_are_reported_not_fatal()
    {
        var root = Fixture.TempDir();
        var store = new AccountStore(root, "1");
        File.WriteAllText(Path.Combine(store.Directory, "profiles", "broken.kv"), "not kv {");
        var errors = new System.Collections.Generic.List<string>();
        Assert.Empty(store.LoadProfiles(errors));
        Assert.Single(errors);
    }
}
