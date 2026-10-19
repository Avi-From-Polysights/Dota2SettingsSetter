using System;
using System.IO;
using Dota2SettingsSetter.Core;

namespace Dota2SettingsSetter.Tests;

static class Fixture
{
    public static string Path(string name) =>
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name);

    public static string SampleText => File.ReadAllText(Path("dotakeys_personal.sample.lst"));

    public static KeybindFile SampleFile() => KeybindFile.Parse(SampleText);

    public static DefaultSnapshot SampleDefault() => DefaultSnapshot.FromFile(SampleFile());

    public static ActionInfo Action(string name) =>
        ActionCatalog.Find(name) ?? throw new ArgumentException("Not in catalog: " + name);

    public static string TempDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d2ss-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
