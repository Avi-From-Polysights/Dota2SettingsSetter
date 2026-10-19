using System.IO;
using System.Linq;
using Dota2SettingsSetter.Core;
using Xunit;

namespace Dota2SettingsSetter.Tests;

public class SteamLocatorTests
{
    [Fact]
    public void Finds_accounts_with_dota_data_and_their_persona_names()
    {
        var steam = Fixture.TempDir();
        var cfg = Path.Combine(steam, "userdata", "98479851", "570", "remote", "cfg");
        Directory.CreateDirectory(cfg);
        File.WriteAllText(Path.Combine(cfg, "dotakeys_personal.lst"), Fixture.SampleText);
        Directory.CreateDirectory(Path.Combine(steam, "userdata", "555", "730"));
        Directory.CreateDirectory(Path.Combine(steam, "config"));
        File.WriteAllText(Path.Combine(steam, "config", "loginusers.vdf"),
            "\"users\"\n{\n\t\"76561198058745579\"\n\t{\n\t\t\"AccountName\"\t\t\"rare\"\n\t\t\"PersonaName\"\t\t\"Rare\"\n\t}\n}\n");

        var accounts = SteamLocator.FindAccounts(steam);

        var account = Assert.Single(accounts);
        Assert.Equal("98479851", account.AccountId);
        Assert.Equal("Rare", account.PersonaName);
        Assert.True(account.HasKeybindFile);
        Assert.Equal("Rare (98479851)", account.Display);
    }

    [Fact]
    public void Finds_dota_in_a_secondary_library()
    {
        var steam = Fixture.TempDir();
        var library = Fixture.TempDir();
        var cfg = Path.Combine(library, "steamapps", "common", "dota 2 beta", "game", "dota", "cfg");
        Directory.CreateDirectory(cfg);
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        var escaped = library.Replace(@"\", @"\\");
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steam.Replace(@"\", @"\\") + "\"\n\t}\n" +
            "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + escaped + "\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"570\"\t\t\"1\"\n\t\t}\n\t}\n}\n");

        Assert.Equal(cfg, SteamLocator.FindDotaCfgDir(steam));
    }

    [Fact]
    public void Missing_steam_yields_nothing()
    {
        Assert.Empty(SteamLocator.FindAccounts(null));
        Assert.Null(SteamLocator.FindDotaCfgDir(null));
        Assert.Empty(SteamLocator.FindAccounts(Fixture.TempDir()).ToList());
    }
}
