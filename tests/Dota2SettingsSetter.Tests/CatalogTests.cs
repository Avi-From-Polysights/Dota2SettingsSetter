using System.Linq;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class CatalogTests
{
    [Fact]
    public void Names_are_unique_and_groups_are_known()
    {
        Assert.Equal(ActionCatalog.All.Count, ActionCatalog.All.Select(a => a.Name.ToLowerInvariant()).Distinct().Count());
        Assert.All(ActionCatalog.All, a => Assert.Contains(a.Group, ActionCatalog.Groups));
    }

    [Fact]
    public void Dependencies_point_at_real_actions()
    {
        Assert.All(ActionCatalog.All.Where(a => a.DependsOn != null), a => Assert.NotNull(ActionCatalog.Find(a.DependsOn)));
        Assert.Equal(new[] { "Inventory1AutoCast" }, ActionCatalog.DependentsOf("Inventory1").Select(a => a.Name));
    }

    [Fact]
    public void Console_commands_match_dota_slots()
    {
        Assert.Equal("dota_item_execute 15", ActionCatalog.Find("InventoryTP").ConsoleCommand);
        Assert.Equal("dota_item_quick_cast 16", ActionCatalog.Find("InventoryNeutralQuickCast").ConsoleCommand);
        Assert.Equal("dota_ability_quickcast 5", ActionCatalog.Find("AbilityUltimateQuickCast").ConsoleCommand);
        Assert.False(ActionCatalog.Find("ShopTabConsumables").CanUseConsole);
    }

    [Theory]
    [InlineData("Q", "", "Q")]
    [InlineData("q", "alt", "Alt + Q")]
    [InlineData("MOUSE4", "", "Mouse 4")]
    [InlineData("KP_8", "", "Num 8")]
    [InlineData("SEMICOLON", "", ";")]
    [InlineData("", "ALT", "—")]
    public void Chords_display_friendly_names(string key, string modifier, string display)
    {
        Assert.Equal(display, new KeyChord(key, modifier).Display);
    }

    [Fact]
    public void Chords_compare_without_case()
    {
        Assert.Equal(new KeyChord("q", "alt"), new KeyChord("Q", "ALT"));
        Assert.Equal("q", KeyNames.ToConsole("Q"));
        Assert.Equal("MOUSE4", KeyNames.ToConsole("mouse4"));
    }
}
