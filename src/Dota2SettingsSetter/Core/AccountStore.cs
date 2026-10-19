using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dota2SettingsSetter.Core;

/// <summary>
/// Everything the app keeps for one Steam account, under
/// %APPDATA%\Dota2SettingsSetter\accounts\&lt;id&gt;\.
/// </summary>
public sealed class AccountStore
{
    const int BackupsToKeep = 20;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    public string Directory { get; }
    string ProfilesDir => Path.Combine(Directory, "profiles");
    string BackupsDir => Path.Combine(Directory, "backups");
    string DefaultPath => Path.Combine(Directory, "default.kv");
    string StatePath => Path.Combine(Directory, "state.kv");

    public AccountStore(string rootDir, string accountId)
    {
        Directory = Path.Combine(rootDir, "accounts", accountId);
        System.IO.Directory.CreateDirectory(ProfilesDir);
    }

    /// <summary>%APPDATA%\Dota2SettingsSetter, or D2SS_DATA_DIR when set (for testing against a sandbox).</summary>
    public static string DefaultRoot =>
        Environment.GetEnvironmentVariable("D2SS_DATA_DIR") is string dir && dir.Length > 0
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dota2SettingsSetter");

    public DefaultSnapshot LoadDefault() =>
        File.Exists(DefaultPath) ? DefaultSnapshot.Parse(File.ReadAllText(DefaultPath, Encoding.UTF8)) : null;

    public void SaveDefault(DefaultSnapshot snapshot) => File.WriteAllText(DefaultPath, snapshot.Serialize(), Utf8);

    public List<Profile> LoadProfiles(List<string> errors = null)
    {
        var profiles = new List<Profile>();
        foreach (var path in System.IO.Directory.GetFiles(ProfilesDir, "*.kv"))
        {
            try
            {
                profiles.Add(Profile.Load(path));
            }
            catch (Exception ex)
            {
                errors?.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void SaveProfile(Profile profile) =>
        File.WriteAllText(ProfilePath(profile), profile.Serialize(), Utf8);

    public void DeleteProfile(Profile profile)
    {
        var path = ProfilePath(profile);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Saves under a new name, removing the file stored under the old one.</summary>
    public void RenameProfile(Profile profile, string newName)
    {
        DeleteProfile(profile);
        profile.Name = newName;
        SaveProfile(profile);
    }

    string ProfilePath(Profile profile) => Path.Combine(ProfilesDir, profile.Slug + ".kv");

    /// <summary>Slug of the profile currently written into Dota's keybind file, or null for Default.</summary>
    public string LoadAppliedSlug()
    {
        if (!File.Exists(StatePath))
            return null;
        try
        {
            var slug = KeyValues.Load(StatePath).GetValue("AppliedProfile");
            return string.IsNullOrEmpty(slug) ? null : slug;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public void SaveAppliedSlug(string slug)
    {
        var root = new KvNode("Dota2SettingsSetterState");
        root.Children.Add(new KvNode("AppliedProfile", slug ?? ""));
        File.WriteAllText(StatePath, KeyValues.Write(root), Utf8);
    }

    /// <summary>Copies Dota's keybind file aside before the app changes it, keeping the newest few.</summary>
    public string BackupKeybindFile(string keybindPath)
    {
        System.IO.Directory.CreateDirectory(BackupsDir);
        var backup = Path.Combine(BackupsDir, $"dotakeys_personal_{DateTime.Now:yyyyMMdd_HHmmss_fff}.lst");
        File.Copy(keybindPath, backup, overwrite: false);

        var old = new DirectoryInfo(BackupsDir).GetFiles("dotakeys_personal_*.lst")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(BackupsToKeep);
        foreach (var file in old)
            file.Delete();
        return backup;
    }

    public string BackupsDirectory => BackupsDir;
}
