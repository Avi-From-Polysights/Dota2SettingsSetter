using System;
using System.IO;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class KeybindFileTests
{
    [Fact]
    public void Reads_keys_and_modifiers()
    {
        var file = Fixture.SampleFile();
        Assert.Equal(new KeyChord("Q"), file.GetChord("AbilityPrimary1"));
        Assert.Equal(new KeyChord("Q", "ALT"), file.GetChord("AbilityPrimary1AutoCast"));
        Assert.Equal(new KeyChord("\\"), file.GetChord("Console"));
    }

    [Fact]
    public void Entry_without_key_and_missing_entry_are_both_unbound()
    {
        var file = Fixture.SampleFile();
        Assert.True(file.HasEntry("RecentEvent"));
        Assert.True(file.GetChord("RecentEvent").IsEmpty);
        Assert.False(file.HasEntry("CourierShield"));
        Assert.True(file.GetChord("CourierShield").IsEmpty);
    }

    [Fact]
    public void Changing_a_key_keeps_cast_mode_and_field_order()
    {
        var file = Fixture.SampleFile();
        file.SetChord("AbilityUltimate", new KeyChord("T"));
        var text = file.Serialize();

        var expected = Fixture.SampleText.Replace(
            "\"Action\"\t\t\"dota_ability_execute 5\"\n\t\t\t\"Key\"\t\t\"R\"\n\t\t\t\"Mode\"\t\t\"2\"",
            "\"Action\"\t\t\"dota_ability_execute 5\"\n\t\t\t\"Key\"\t\t\"T\"\n\t\t\t\"Mode\"\t\t\"2\"");
        Assert.NotEqual(Fixture.SampleText, expected);
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Modifier_is_added_and_removed()
    {
        var file = Fixture.SampleFile();
        file.SetChord("Inventory1", new KeyChord("Q", "ALT"));
        Assert.Equal(new KeyChord("Q", "ALT"), file.GetChord("Inventory1"));

        file.SetChord("AbilityPrimary1AutoCast", new KeyChord("Q"));
        Assert.Equal(new KeyChord("Q"), file.GetChord("AbilityPrimary1AutoCast"));
        Assert.DoesNotContain("Modifier", KeyValues.Write(file.Root.Child("Keys").Child("AbilityPrimary1AutoCast")));
    }

    [Fact]
    public void Unbinding_removes_key_but_keeps_the_entry()
    {
        var file = Fixture.SampleFile();
        file.SetChord("ChatGlobal", KeyChord.None);
        Assert.True(file.HasEntry("ChatGlobal"));
        Assert.True(file.GetChord("ChatGlobal").IsEmpty);
        Assert.Equal("say", file.Root.Child("Keys").Child("ChatGlobal").GetValue("Action"));
    }

    [Fact]
    public void Binding_a_new_action_creates_a_minimal_entry()
    {
        var file = Fixture.SampleFile();
        file.SetChord("CourierShield", new KeyChord("F6"));
        var entry = file.Root.Child("Keys").Child("CourierShield");
        Assert.Single(entry.Children);
        Assert.Equal("F6", entry.GetValue("Key"));
    }

    [Fact]
    public void Edits_leave_other_sections_untouched()
    {
        var file = Fixture.SampleFile();
        var before = KeybindFile.Parse(Fixture.SampleText);
        file.SetChord("Inventory1", new KeyChord("Q"));
        file.SetChord("AbilityPrimary1", KeyChord.None);
        foreach (var section in new[] { "Units", "HeroBuilds2", "Items" })
            Assert.Equal(KeyValues.Write(before.Root.Child(section)), KeyValues.Write(file.Root.Child(section)));
        Assert.Equal("8", file.Root.GetValue("AlgorithmicUpdateVersion"));
    }

    [Fact]
    public void Rejects_files_that_are_not_keybinds()
    {
        Assert.Throws<FormatException>(() => KeybindFile.Parse("\"users\" { }"));
    }

    [Fact]
    public void Save_replaces_the_file_without_leaving_a_temp_file()
    {
        var dir = Fixture.TempDir();
        var path = Path.Combine(dir, "dotakeys_personal.lst");
        File.WriteAllText(path, Fixture.SampleText);
        var file = KeybindFile.Load(path);
        file.SetChord("Inventory1", new KeyChord("Q"));
        file.Save(path);

        Assert.Equal(new KeyChord("Q"), KeybindFile.Load(path).GetChord("Inventory1"));
        Assert.Single(Directory.GetFiles(dir));
    }
}
