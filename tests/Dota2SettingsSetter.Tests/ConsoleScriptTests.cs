using System.IO;
using System.Linq;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class ConsoleScriptTests
{
    readonly DefaultSnapshot defaults = Fixture.SampleDefault();

    Profile SwapItemOneOntoQ()
    {
        var profile = new Profile("Invoker");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory1"), new KeyChord("Q"), ConflictResolution.Swap);
        return profile;
    }

    [Fact]
    public void Diff_only_binds_the_keys_that_change()
    {
        var result = ConsoleScript.Diff(defaults, null, SwapItemOneOntoQ());
        Assert.Equal(new[]
        {
            "bind \"1\" \"dota_ability_execute 0\"",
            "bind \"q\" \"dota_item_execute 0\"",
        }, result.Commands);
        Assert.Empty(result.Warnings);
        Assert.Equal("bind \"1\" \"dota_ability_execute 0\"; bind \"q\" \"dota_item_execute 0\"", result.OneLine);
    }

    [Fact]
    public void Diff_back_to_default_reverses_it()
    {
        var result = ConsoleScript.Diff(defaults, SwapItemOneOntoQ(), null);
        Assert.Equal(new[]
        {
            "bind \"q\" \"dota_ability_execute 0\"",
            "bind \"1\" \"dota_item_execute 0\"",
        }, result.Commands);
    }

    [Fact]
    public void Freed_keys_are_unbound()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory6"), KeyChord.None, ConflictResolution.Swap);
        Assert.Equal(new[] { "unbind \"c\"" }, ConsoleScript.Diff(defaults, null, profile).Commands);
    }

    [Fact]
    public void Modifier_combos_become_warnings()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("Inventory2"), new KeyChord("W", "ALT"), ConflictResolution.Swap);
        var result = ConsoleScript.Diff(defaults, null, profile);

        Assert.Equal(new[] { "unbind \"2\"" }, result.Commands);
        Assert.Contains(result.Warnings, w => w.Contains("Activate Inventory Slot 2") && w.Contains("Alt + W"));
    }

    [Fact]
    public void Menu_hotkeys_become_warnings()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("ShopTabConsumables"), new KeyChord("Y"), ConflictResolution.Swap);
        var result = ConsoleScript.Diff(defaults, null, profile);
        Assert.Empty(result.Commands);
        Assert.Contains(result.Warnings, w => w.Contains("Shop Tab: Consumables"));
    }

    [Fact]
    public void Actions_without_a_console_command_are_skipped_with_a_warning()
    {
        var profile = new Profile("p");
        Keymap.Assign(defaults, profile, Fixture.Action("ChatTeam"), new KeyChord("Y"), ConflictResolution.Swap);
        var result = ConsoleScript.Diff(defaults, null, profile);
        Assert.Empty(result.Commands);
        Assert.Contains(result.Warnings, w => w.Contains("\"Chat to Team\" on Y can't be set from the console"));
        Assert.Contains(result.Warnings, w => w.Contains("Enter still triggers \"Chat to Team\""));
    }

    [Fact]
    public void Absolute_scripts_reset_keys_other_profiles_change()
    {
        var invoker = SwapItemOneOntoQ();
        var kez = new Profile("Kez");
        Keymap.Assign(defaults, kez, Fixture.Action("Inventory6"), new KeyChord("V"), ConflictResolution.Swap);
        var all = new[] { invoker, kez };

        var toInvoker = ConsoleScript.Absolute(defaults, all, invoker).Commands;
        Assert.Contains("bind \"q\" \"dota_item_execute 0\"", toInvoker);
        Assert.Contains("bind \"c\" \"dota_item_execute 5\"", toInvoker);
        Assert.Contains("unbind \"v\"", toInvoker);

        var toDefault = ConsoleScript.Absolute(defaults, all, null).Commands;
        Assert.Contains("bind \"q\" \"dota_ability_execute 0\"", toDefault);
        Assert.Contains("bind \"1\" \"dota_item_execute 0\"", toDefault);
        Assert.Contains("bind \"c\" \"dota_item_execute 5\"", toDefault);
        Assert.Contains("unbind \"v\"", toDefault);
        Assert.Equal(4, toDefault.Count);
    }

    [Fact]
    public void Cfg_files_carry_the_marker_and_an_echo()
    {
        var cfg = ConsoleScript.ToCfg(ConsoleScript.Diff(defaults, null, SwapItemOneOntoQ()), "Switched to Invoker");
        Assert.StartsWith(ExecFiles.Marker, cfg);
        Assert.Contains("bind \"q\" \"dota_item_execute 0\"\n", cfg);
        Assert.EndsWith("echo \"[Dota 2 Settings Setter] Switched to Invoker\"\n", cfg);
    }

    [Fact]
    public void Exec_files_are_written_and_stale_ones_removed()
    {
        var dir = Fixture.TempDir();
        var foreign = Path.Combine(dir, "dss_mine.cfg");
        File.WriteAllText(foreign, "// someone else's file\n");
        File.WriteAllText(Path.Combine(dir, "dss_deleted_hero.cfg"), ExecFiles.Marker + " - old\n");

        var invoker = SwapItemOneOntoQ();
        ExecFiles.Write(dir, defaults, new[] { invoker });

        var names = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "dss_default.cfg", "dss_invoker.cfg", "dss_mine.cfg" }, names);
        Assert.Equal("exec dss_invoker", ExecFiles.ExecCommand(invoker));
        Assert.Equal("exec dss_default", ExecFiles.ExecCommand(null));
    }
}
