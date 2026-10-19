using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Dota2SettingsSetter.Core;

public sealed class SteamAccount
{
    public string AccountId { get; set; }
    public string PersonaName { get; set; }
    public string KeybindPath { get; set; }
    public DateTime LastWriteUtc { get; set; }

    public bool HasKeybindFile => File.Exists(KeybindPath);

    public string Display => string.IsNullOrEmpty(PersonaName) ? AccountId : $"{PersonaName} ({AccountId})";

    public override string ToString() => Display;
}

/// <summary>Finds Steam, the Steam accounts that have played Dota, and Dota's cfg folder.</summary>
public static class SteamLocator
{
    const string DotaAppId = "570";
    const long SteamId64Base = 76561197960265728L;

    /// <summary>Steam's install folder, or D2SS_STEAM_DIR when set (for testing against a sandbox).</summary>
    public static string FindSteamPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("D2SS_STEAM_DIR");
        if (!string.IsNullOrEmpty(overridePath))
            return Directory.Exists(overridePath) ? Path.GetFullPath(overridePath) : null;

        foreach (var (hive, key, value) in new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
        })
        {
            try
            {
                using (var reg = hive.OpenSubKey(key))
                    if (reg?.GetValue(value) is string path && Directory.Exists(path))
                        return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                // A locked-down registry just means we fall through to the next guess.
            }
        }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    /// <summary>Accounts with Dota data, most recently changed keybinds first.</summary>
    public static List<SteamAccount> FindAccounts(string steamPath)
    {
        var accounts = new List<SteamAccount>();
        var userdata = Path.Combine(steamPath ?? "", "userdata");
        if (steamPath == null || !Directory.Exists(userdata))
            return accounts;

        var personas = ReadPersonaNames(steamPath);
        foreach (var dir in Directory.GetDirectories(userdata))
        {
            var id = Path.GetFileName(dir);
            var dotaDir = Path.Combine(dir, DotaAppId);
            if (!Directory.Exists(dotaDir) || !id.All(char.IsDigit))
                continue;
            var keybinds = Path.Combine(dotaDir, "remote", "cfg", "dotakeys_personal.lst");
            accounts.Add(new SteamAccount
            {
                AccountId = id,
                PersonaName = personas.TryGetValue(id, out var persona) ? persona : null,
                KeybindPath = keybinds,
                LastWriteUtc = File.Exists(keybinds) ? File.GetLastWriteTimeUtc(keybinds) : DateTime.MinValue,
            });
        }
        return accounts.OrderByDescending(a => a.LastWriteUtc).ToList();
    }

    /// <summary>Account id (the userdata folder name) to persona name, from config/loginusers.vdf.</summary>
    static Dictionary<string, string> ReadPersonaNames(string steamPath)
    {
        var names = new Dictionary<string, string>();
        try
        {
            var path = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(path))
                return names;
            foreach (var user in KeyValues.Load(path).Children.Where(c => c.IsBlock))
            {
                if (!long.TryParse(user.Key, out var steamId64) || steamId64 < SteamId64Base)
                    continue;
                var persona = user.GetValue("PersonaName");
                if (!string.IsNullOrEmpty(persona))
                    names[(steamId64 - SteamId64Base).ToString()] = persona;
            }
        }
        catch (Exception)
        {
            // Persona names are cosmetic; the numeric id still works without them.
        }
        return names;
    }

    /// <summary>game\dota\cfg inside whichever Steam library has Dota installed.</summary>
    public static string FindDotaCfgDir(string steamPath)
    {
        if (steamPath == null)
            return null;
        foreach (var library in LibraryFolders(steamPath))
        {
            var cfg = Path.Combine(library, "steamapps", "common", "dota 2 beta", "game", "dota", "cfg");
            if (Directory.Exists(cfg))
                return cfg;
        }
        return null;
    }

    static IEnumerable<string> LibraryFolders(string steamPath)
    {
        yield return steamPath;
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;

        List<string> paths;
        try
        {
            paths = KeyValues.Load(vdf).Children
                .Where(c => c.IsBlock)
                .OrderByDescending(c => c.Child("apps")?.Child(DotaAppId) != null)
                .Select(c => c.GetValue("path"))
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p.Replace(@"\\", @"\"))
                .ToList();
        }
        catch (Exception)
        {
            yield break;
        }
        foreach (var path in paths)
            yield return path;
    }

    public static bool IsDotaRunning()
    {
        var processes = Process.GetProcessesByName("dota2");
        foreach (var process in processes)
            process.Dispose();
        return processes.Length > 0;
    }
}
