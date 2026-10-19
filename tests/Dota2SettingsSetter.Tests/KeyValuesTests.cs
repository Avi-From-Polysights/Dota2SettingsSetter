using System;
using System.IO;
using System.Linq;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class KeyValuesTests
{
    [Fact]
    public void Sample_file_round_trips_byte_for_byte()
    {
        var text = Fixture.SampleText;
        Assert.Equal(text, KeyValues.Write(KeyValues.Parse(text)));
    }

    [Fact]
    public void Backslash_is_literal_not_an_escape()
    {
        var root = KeyValues.Parse("\"root\"\n{\n\t\"Key\"\t\t\"\\\"\n\t\"Next\"\t\t\"x\"\n}\n");
        Assert.Equal("\\", root.GetValue("Key"));
        Assert.Equal("x", root.GetValue("Next"));
    }

    [Fact]
    public void Reads_comments_unquoted_tokens_and_platform_conditionals()
    {
        var root = KeyValues.Parse(@"// header comment
root
{
    plain   value // trailing comment
    ""quoted"" ""v"" [$WIN32]
    block { inner ""1"" }
}");
        Assert.Equal("value", root.GetValue("plain"));
        Assert.Equal("v", root.GetValue("quoted"));
        Assert.Equal("1", root.Child("block").GetValue("inner"));
    }

    [Fact]
    public void Lookups_ignore_case_and_keep_duplicates_in_order()
    {
        var root = KeyValues.Parse("\"r\" { \"A\" \"1\" \"a\" \"2\" }");
        Assert.Equal("1", root.GetValue("a"));
        Assert.Equal(new[] { "1", "2" }, root.Children.Select(c => c.Value));
    }

    [Fact]
    public void Unterminated_string_is_rejected()
    {
        Assert.Throws<FormatException>(() => KeyValues.Parse("\"r\" { \"a\" \"oops }"));
    }

    [Fact]
    public void Utf8_bom_is_skipped()
    {
        var root = KeyValues.Parse("﻿\"r\" { \"a\" \"1\" }");
        Assert.Equal("r", root.Key);
    }

    /// <summary>
    /// On a machine with Dota installed, checks the real keybind files round-trip exactly.
    /// Passes trivially elsewhere (CI has no Steam).
    /// </summary>
    [Fact]
    public void Real_keybind_files_on_this_machine_round_trip()
    {
        var steam = SteamLocator.FindSteamPath();
        foreach (var account in SteamLocator.FindAccounts(steam).Where(a => a.HasKeybindFile))
        {
            var text = File.ReadAllText(account.KeybindPath);
            Assert.Equal(text, KeybindFile.Parse(text).Serialize());
        }
    }
}
