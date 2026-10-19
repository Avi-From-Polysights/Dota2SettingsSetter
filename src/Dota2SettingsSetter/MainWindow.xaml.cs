using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Dota2SettingsSetter.Core;
using Dota2SettingsSetter.Views;
using Microsoft.Win32;

namespace Dota2SettingsSetter;

public partial class MainWindow : Window
{
    public const string RepoUrl = "https://github.com/Avi-From-Polysights/Dota2SettingsSetter";

    readonly string dataRoot = AccountStore.DefaultRoot;
    readonly ObservableCollection<ProfileItem> profileItems = new ObservableCollection<ProfileItem>();
    readonly List<BindingRow> rows = ActionCatalog.All.Where(a => !a.Hidden).Select(a => new BindingRow(a)).ToList();
    readonly ICollectionView rowsView;
    readonly DispatcherTimer dotaTimer;

    string steamPath, cfgDir, execError;
    SteamAccount account;
    AccountStore store;
    DefaultSnapshot defaults;
    List<Profile> profiles = new List<Profile>();
    Profile applied;                // what Dota's keybind file holds (null = Default)
    DateTime keybindStampUtc;
    bool dotaRunning, suppressSelection;

    public static string AppVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    Profile Selected => (ProfileList.SelectedItem as ProfileItem)?.Profile;

    public MainWindow()
    {
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width - 24);
        Height = Math.Min(Height, area.Height - 24);
        Title = "Dota 2 Settings Setter " + AppVersion;
        VersionText.Text = "v" + AppVersion + " · hotkey profiles per hero";

        var source = (CollectionViewSource)FindResource("RowsSource");
        source.Source = rows;
        rowsView = source.View;
        rowsView.Filter = FilterRow;
        ProfileList.ItemsSource = profileItems;

        dotaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        dotaTimer.Tick += (s, e) => UpdateDotaState();
        Loaded += (s, e) =>
        {
            Discover(null);
            UpdateDotaState(force: true);
            dotaTimer.Start();
        };
        Activated += (s, e) => CheckKeybindFileChanged();
    }

    // ---------------------------------------------------------------- loading

    void Discover(string preferAccountId)
    {
        steamPath = SteamLocator.FindSteamPath();
        cfgDir = SteamLocator.FindDotaCfgDir(steamPath);
        var accounts = SteamLocator.FindAccounts(steamPath);

        suppressSelection = true;
        AccountBox.ItemsSource = accounts;
        AccountBox.SelectedItem = accounts.FirstOrDefault(a => a.AccountId == preferAccountId) ?? accounts.FirstOrDefault();
        suppressSelection = false;

        if (accounts.Count == 0)
        {
            account = null;
            store = null;
            defaults = null;
            profiles = new List<Profile>();
            RefreshProfiles(null);
            ShowBanner(steamPath == null
                ? "Couldn't find Steam on this PC. Install Steam and Dota 2, then press Reload."
                : "No Steam account on this PC has Dota 2 data yet. Launch Dota once, then press Reload.");
            return;
        }
        LoadAccount((SteamAccount)AccountBox.SelectedItem, Selected?.Slug);
    }

    void LoadAccount(SteamAccount next, string selectSlug)
    {
        account = next;
        store = new AccountStore(dataRoot, next.AccountId);
        var problems = new List<string>();
        profiles = store.LoadProfiles(problems);
        defaults = store.LoadDefault();

        var appliedSlug = store.LoadAppliedSlug();
        applied = profiles.FirstOrDefault(p => p.Slug == appliedSlug);
        if (appliedSlug != null && applied == null)
            store.SaveAppliedSlug(null);

        var live = ReadKeybindFile(problems);
        if (defaults == null && live != null)
        {
            defaults = DefaultSnapshot.FromFile(live);
            store.SaveDefault(defaults);
            SetStatus("Saved your current Dota hotkeys as Default.");
        }

        WriteExecFiles();
        RefreshProfiles(selectSlug);
        CheckDrift(live, problems);
    }

    KeybindFile ReadKeybindFile(List<string> problems)
    {
        if (account == null || !account.HasKeybindFile)
            return null;
        try
        {
            keybindStampUtc = File.GetLastWriteTimeUtc(account.KeybindPath);
            return KeybindFile.Load(account.KeybindPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
        {
            problems.Add("Couldn't read Dota's keybind file: " + ex.Message);
            return null;
        }
    }

    /// <summary>Re-checks Dota's file when the user comes back to the app, e.g. after changing hotkeys in game.</summary>
    void CheckKeybindFileChanged()
    {
        if (account == null)
            return;
        if (!account.HasKeybindFile || defaults == null)
        {
            if (account.HasKeybindFile)
                LoadAccount(account, Selected?.Slug);
            return;
        }
        if (File.GetLastWriteTimeUtc(account.KeybindPath) == keybindStampUtc)
            return;
        var problems = new List<string>();
        CheckDrift(ReadKeybindFile(problems), problems);
    }

    void CheckDrift(KeybindFile live, List<string> problems)
    {
        if (account != null && !account.HasKeybindFile)
        {
            ShowBanner("Dota hasn't created a keybind file for this account yet. In Dota, change any hotkey once (Settings → Hotkeys) so it saves dotakeys_personal.lst, then press Reload.");
            return;
        }
        if (problems.Count > 0)
        {
            ShowBanner(string.Join("  ", problems));
            return;
        }
        if (live == null || defaults == null)
        {
            HideBanner();
            return;
        }

        var drift = FileSync.Drift(live, defaults, applied);
        if (drift.Count == 0)
        {
            HideBanner();
            return;
        }

        var names = drift.Take(3).Select(n => "\"" + (ActionCatalog.Find(n)?.Label ?? n) + "\"");
        var list = string.Join(", ", names) + (drift.Count > 3 ? $" and {drift.Count - 3} more" : "");
        var holder = applied == null ? "Default" : "\"" + applied.Name + "\"";
        var buttons = new List<(string, Action)> { ("Update Default from Dota", RecaptureDefault) };
        if (applied != null)
            buttons.Add(($"Write {applied.Name} again", () => WriteProfile(applied)));
        ShowBanner($"Dota's keybind file doesn't match {holder} for {list}. That's expected after changing hotkeys inside Dota, or after editing a profile you already wrote.",
            buttons.ToArray());
    }

    void WriteExecFiles()
    {
        execError = null;
        if (defaults == null)
            return;
        if (cfgDir == null)
        {
            execError = "Couldn't find Dota's game folder, so no exec files were saved. Paste the binds directly instead.";
            return;
        }
        try
        {
            ExecFiles.Write(cfgDir, defaults, profiles);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            execError = "Couldn't save exec files in Dota's cfg folder (" + ex.Message + "). Paste the binds directly instead.";
        }
    }

    // ---------------------------------------------------------------- profile list

    void RefreshProfiles(string selectSlug)
    {
        suppressSelection = true;
        profileItems.Clear();
        profileItems.Add(new ProfileItem(null) { Title = "Default", Subtitle = "Your normal Dota hotkeys" });
        foreach (var profile in profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            profileItems.Add(new ProfileItem(profile) { Title = profile.Name, Subtitle = ChangesText(profile) });
        foreach (var item in profileItems)
            item.IsInFile = account != null && account.HasKeybindFile && item.Profile == applied;

        ProfileList.SelectedItem = profileItems.FirstOrDefault(i => i.Profile != null && i.Profile.Slug == selectSlug) ?? profileItems[0];
        suppressSelection = false;
        ShowSelected();
    }

    static int ChangeCount(Profile profile) =>
        profile.Overrides.Keys.Count(k => ActionCatalog.Find(k)?.DependsOn == null);

    static string ChangesText(Profile profile)
    {
        var count = ChangeCount(profile);
        return count == 0 ? "No changes yet" : count == 1 ? "1 change" : count + " changes";
    }

    void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!suppressSelection)
            ShowSelected();
    }

    void ShowSelected()
    {
        var profile = Selected;
        var snapshot = defaults ?? new DefaultSnapshot();
        foreach (var row in rows)
            row.Update(snapshot, profile);
        rowsView.Refresh();

        EditorTitle.Text = profile?.Name ?? "Default";
        if (defaults == null)
            EditorSubtitle.Text = "No hotkeys loaded yet.";
        else if (profile == null)
            EditorSubtitle.Text = $"Your normal Dota hotkeys, read from Dota on {defaults.CapturedAtUtc.ToLocalTime():MMM d, h:mm tt}. Make a hero profile to change keys, or change them in Dota and press Update Default.";
        else
        {
            var count = ChangeCount(profile);
            EditorSubtitle.Text = count == 0
                ? "Same as Default so far. Click any key to change it for this hero."
                : $"{ChangesText(profile)} from Default, marked in gold. Click a key to change it.";
        }

        EmptyText.Text = "No hotkeys to show. Dota hasn't written a keybind file for this account yet.";
        EmptyText.Visibility = defaults == null ? Visibility.Visible : Visibility.Collapsed;
        RowsList.Visibility = defaults == null ? Visibility.Collapsed : Visibility.Visible;

        var editable = profile != null;
        DuplicateButton.IsEnabled = editable;
        RenameButton.IsEnabled = editable;
        DeleteButton.IsEnabled = editable;
        ExportButton.IsEnabled = editable;
        NewButton.IsEnabled = defaults != null;
        RecaptureButton.IsEnabled = account != null && account.HasKeybindFile;

        UpdateSwitchPanel();
    }

    bool FilterRow(object item)
    {
        var row = (BindingRow)item;
        if (OnlyChangesBox.IsChecked == true && !row.IsOverridden)
            return false;
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        return query.Length == 0 || row.SearchText.Contains(query);
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        rowsView.Refresh();
    }

    void OnlyChanges_Changed(object sender, RoutedEventArgs e) => rowsView.Refresh();

    void AccountBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!suppressSelection && AccountBox.SelectedItem is SteamAccount next && next != account)
            LoadAccount(next, null);
    }

    // ---------------------------------------------------------------- editing

    void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        var row = (BindingRow)((FrameworkElement)sender).DataContext;
        var profile = Selected;
        if (profile == null || defaults == null)
            return;

        var current = Keymap.Effective(defaults, profile, row.Action.Name);
        var capture = new KeyCaptureWindow(row.Label, current) { Owner = this };
        if (capture.ShowDialog() != true || capture.Result == current)
            return;
        var chord = capture.Result;

        var resolution = ConflictResolution.Swap;
        var conflicts = Keymap.ConflictsFor(defaults, profile, row.Action, chord);
        if (conflicts.Count > 0)
        {
            var names = string.Join(", ", conflicts.Select(c => "\"" + c.Label + "\""));
            var one = conflicts.Count == 1;
            var options = new List<(string Label, ConflictResolution Resolution)>();
            if (!current.IsEmpty)
                options.Add(($"Swap ({(one ? "it gets" : "they get")} {current.Display})", ConflictResolution.Swap));
            options.Add((one ? "Unbind it" : "Unbind them", ConflictResolution.UnbindOthers));
            options.Add(("Keep both", ConflictResolution.KeepBoth));

            var choice = ChoiceWindow.Show(this, chord.Display + " is already taken",
                $"{names} {(one ? "uses" : "use")} {chord.Display} in this profile. What should happen to {(one ? "it" : "them")}?",
                0, options.Select(o => o.Label).Concat(new[] { "Cancel" }).ToArray());
            if (choice < 0 || choice >= options.Count)
                return;
            resolution = options[choice].Resolution;
        }

        Keymap.Assign(defaults, profile, row.Action, chord, resolution);
        SaveAndRefresh(profile);
        SetStatus($"{row.Label} → {chord.Display}");
    }

    void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var row = (BindingRow)((FrameworkElement)sender).DataContext;
        var profile = Selected;
        if (profile == null || defaults == null)
            return;
        Keymap.Reset(defaults, profile, row.Action);
        SaveAndRefresh(profile);
        SetStatus($"{row.Label} is back to Default ({defaults.Get(row.Action.Name).Display}).");
    }

    void SaveAndRefresh(Profile profile)
    {
        store.SaveProfile(profile);
        foreach (var row in rows)
            row.Update(defaults, profile);
        if (OnlyChangesBox.IsChecked == true)
            rowsView.Refresh();

        var item = profileItems.FirstOrDefault(i => i.Profile == profile);
        if (item != null)
            item.Subtitle = ChangesText(profile);
        EditorSubtitle.Text = ChangeCount(profile) == 0
            ? "Same as Default so far. Click any key to change it for this hero."
            : $"{ChangesText(profile)} from Default, marked in gold. Click a key to change it.";

        WriteExecFiles();
        UpdateSwitchPanel();
        if (profile == applied)
        {
            var problems = new List<string>();
            CheckDrift(ReadKeybindFile(problems), problems);
        }
    }

    // ---------------------------------------------------------------- profile commands

    string ValidateName(string name, Profile except)
    {
        if (name.Length == 0)
            return "Enter a name.";
        if (name.Contains("\""))
            return "Names can't contain quotes.";
        var slug = Profile.Slugify(name);
        if (slug == ExecFiles.DefaultName)
            return "\"Default\" is your normal hotkeys; pick another name.";
        if (profiles.Any(p => p != except && p.Slug == slug))
            return "There's already a profile with that name.";
        return null;
    }

    void AddProfile(Profile profile)
    {
        profiles.Add(profile);
        store.SaveProfile(profile);
        WriteExecFiles();
        RefreshProfiles(profile.Slug);
    }

    void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (store == null || defaults == null)
            return;
        var name = PromptWindow.Ask(this, "New hero profile",
            "Name it after the hero or setup, like Invoker or Kez late. It starts identical to Default; then change only the keys you want.",
            "", n => ValidateName(n, null));
        if (name == null)
            return;
        AddProfile(new Profile(name));
        SetStatus($"Created {name}. Click a key to change it.");
    }

    void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        var source = Selected;
        if (source == null)
            return;
        var name = PromptWindow.Ask(this, "Copy profile", null, source.Name + " copy", n => ValidateName(n, null));
        if (name != null)
            AddProfile(source.Clone(name));
    }

    void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = Selected;
        if (profile == null)
            return;
        var name = PromptWindow.Ask(this, "Rename profile", "The in-game exec command follows the new name.", profile.Name,
            n => ValidateName(n, profile));
        if (name == null || name == profile.Name)
            return;
        store.RenameProfile(profile, name);
        if (profile == applied)
            store.SaveAppliedSlug(profile.Slug);
        WriteExecFiles();
        RefreshProfiles(profile.Slug);
        SetStatus($"Renamed. In game it's now: {ExecFiles.ExecCommand(profile)}");
    }

    void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = Selected;
        if (profile == null)
            return;
        if (profile == applied)
        {
            ChoiceWindow.Info(this, "Restore Default first",
                $"\"{profile.Name}\" is written into Dota's keybind file right now. Close Dota and press Restore Default before deleting it, so your normal hotkeys come back.");
            return;
        }
        var choice = ChoiceWindow.Show(this, $"Delete \"{profile.Name}\"?",
            "This removes the profile and its exec file. Export it first if you might want it back.", 1, "Cancel", "Delete");
        if (choice != 1)
            return;
        store.DeleteProfile(profile);
        profiles.Remove(profile);
        WriteExecFiles();
        RefreshProfiles(null);
        SetStatus($"Deleted {profile.Name}.");
    }

    void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (store == null || defaults == null)
            return;
        var dialog = new OpenFileDialog
        {
            Title = "Import profile",
            Filter = "Settings Setter profile (*.kv)|*.kv|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        Profile profile;
        try
        {
            profile = Profile.Load(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException || ex is FormatException || ex is UnauthorizedAccessException)
        {
            ChoiceWindow.Info(this, "Couldn't import that file", ex.Message);
            return;
        }

        if (ValidateName(profile.Name, null) != null)
        {
            var name = PromptWindow.Ask(this, "Name the imported profile",
                $"You already have a profile called \"{profile.Name}\".", profile.Name + " (imported)", n => ValidateName(n, null));
            if (name == null)
                return;
            profile.Name = name;
        }
        Keymap.Normalize(defaults, profile);
        AddProfile(profile);
        SetStatus($"Imported {profile.Name} ({ChangesText(profile).ToLowerInvariant()}).");
    }

    void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = Selected;
        if (profile == null)
            return;
        var dialog = new SaveFileDialog
        {
            Title = "Export profile",
            FileName = profile.Slug + ".kv",
            Filter = "Settings Setter profile (*.kv)|*.kv",
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            File.WriteAllText(dialog.FileName, profile.Serialize());
            SetStatus($"Exported {profile.Name} to {dialog.FileName}");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            ChoiceWindow.Info(this, "Couldn't save the file", ex.Message);
        }
    }

    // ---------------------------------------------------------------- switching

    void UpdateSwitchPanel()
    {
        var profile = Selected;
        var ready = defaults != null;

        var console = ready ? defaults.Get("Console") : KeyChord.None;
        ConsoleHint.Text = console.IsEmpty
            ? "Open the Dota console and paste one of these. If the console won't open, add -console to Dota's launch options in Steam."
            : $"Open the Dota console ({console.Display}) and paste one of these. If it won't open, add -console to Dota's launch options in Steam.";

        ToLabel.Text = profile == null ? "Switch to Default" : "Switch to " + profile.Name;
        ExecToBox.Text = ExecFiles.ExecCommand(profile);
        ExecBackBox.Text = ExecFiles.ExecCommand(null);
        BackPanel.Visibility = profile == null ? Visibility.Collapsed : Visibility.Visible;

        var execOk = ready && execError == null;
        CopyExecToButton.IsEnabled = execOk;
        CopyExecBackButton.IsEnabled = execOk;
        ExecToBox.Opacity = ExecBackBox.Opacity = execOk ? 1 : 0.5;
        ExecStatusIcon.Text = execOk ? "" : "";
        ExecStatusIcon.Foreground = (Brush)FindResource(execOk ? "OkBrush" : "ChangedBrush");
        ExecStatus.Text = !ready ? "Waiting for Dota's keybind file."
            : execError ?? "Exec files are saved in Dota's cfg folder and update whenever you change a profile.";
        if (!execOk && ready)
            DirectToggle.IsChecked = true;

        var warnings = new List<string>();
        if (!ready)
        {
            DirectToBox.Text = DirectBackBox.Text = "";
        }
        else if (profile == null)
        {
            var reset = ConsoleScript.Absolute(defaults, profiles, null);
            DirectToLabel.Text = $"Reset every key your profiles change ({Commands(reset)})";
            DirectToBox.Text = reset.Commands.Count == 0 ? "(no profiles change anything yet)" : reset.OneLine;
            DirectBackPanel.Visibility = Visibility.Collapsed;
            warnings.AddRange(reset.Warnings);
        }
        else
        {
            var to = ConsoleScript.Diff(defaults, null, profile);
            var back = ConsoleScript.Diff(defaults, profile, null);
            DirectToLabel.Text = $"Default → {profile.Name} ({Commands(to)})";
            DirectToBox.Text = to.Commands.Count == 0 ? "(nothing to switch in game)" : to.OneLine;
            DirectBackLabel.Text = $"{profile.Name} → Default ({Commands(back)})";
            DirectBackBox.Text = back.Commands.Count == 0 ? "(nothing to switch in game)" : back.OneLine;
            DirectBackPanel.Visibility = Visibility.Visible;
            warnings.AddRange(to.Warnings);
        }
        WarningsList.ItemsSource = warnings.Distinct().ToList();

        UpdateFileSection();
    }

    static string Commands(ConsoleScriptResult result) =>
        result.Commands.Count == 1 ? "1 command" : result.Commands.Count + " commands";

    void UpdateFileSection()
    {
        var profile = Selected;
        var hasFile = account != null && account.HasKeybindFile && defaults != null;
        FileStateText.Text = !hasFile
            ? "No keybind file found for this account."
            : "Dota's keybind file currently holds: " + (applied == null ? "Default" : applied.Name);
        WriteButton.Visibility = profile == null ? Visibility.Collapsed : Visibility.Visible;
        WriteButton.Content = profile == null ? "" : "Write " + profile.Name + " to Dota";
        WriteButton.IsEnabled = RestoreButton.IsEnabled = hasFile && !dotaRunning;
        FileHint.Text = dotaRunning
            ? "Dota is running. Close it first: Dota only reads this file at startup and can overwrite it when it exits."
            : "Dota's file is backed up before every write (Keybind backups, below).";
        FileHint.Foreground = (Brush)FindResource(dotaRunning ? "ChangedBrush" : "FaintBrush");
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)((Button)sender).Tag;
        var text = box.Text;
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith("("))
        {
            SetStatus("Nothing to copy.");
            return;
        }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                SetStatus("Copied. Paste it into the Dota console: " + (text.Length > 70 ? text.Substring(0, 70) + "…" : text));
                return;
            }
            catch (COMException)
            {
                // Another app briefly holds the clipboard; try again.
                Thread.Sleep(60);
            }
        }
        SetStatus("Couldn't reach the clipboard. Select the text and press Ctrl+C instead.");
    }

    void WriteToDota_Click(object sender, RoutedEventArgs e) => WriteProfile(Selected);

    void RestoreDefault_Click(object sender, RoutedEventArgs e) => WriteProfile(null);

    void WriteProfile(Profile target)
    {
        if (account == null || !account.HasKeybindFile || defaults == null)
            return;
        if (SteamLocator.IsDotaRunning())
        {
            UpdateDotaState(force: true);
            ChoiceWindow.Info(this, "Close Dota first",
                "Dota reads its hotkeys when it starts and can overwrite the file when it exits. Quit Dota, then try again.");
            return;
        }

        var problems = new List<string>();
        var live = ReadKeybindFile(problems);
        if (live == null)
        {
            ChoiceWindow.Info(this, "Couldn't read Dota's keybind file", string.Join(" ", problems));
            return;
        }

        // Keys a profile manages that were changed inside Dota would be overwritten; offer to keep them.
        var involved = profiles.Concat(target == null ? Enumerable.Empty<Profile>() : new[] { target });
        var managed = FileSync.ManagedActions(involved);
        var changedInDota = FileSync.Drift(live, defaults, applied)
            .Where(a => managed.Contains(a) && (applied == null || !applied.Overrides.ContainsKey(a)))
            .ToList();
        if (changedInDota.Count > 0)
        {
            var names = string.Join(", ", changedInDota.Take(4).Select(n => "\"" + (ActionCatalog.Find(n)?.Label ?? n) + "\""));
            var choice = ChoiceWindow.Show(this, "You changed some of these hotkeys inside Dota",
                $"{names} changed in Dota since Default was saved. Writing now would put them back. Update Default from Dota first to keep your changes?",
                0, "Update Default, then write", "Write anyway", "Cancel");
            if (choice < 0 || choice == 2)
                return;
            if (choice == 0)
            {
                RecaptureDefault();
                live = ReadKeybindFile(problems);
                if (live == null)
                    return;
            }
        }

        string backup;
        try
        {
            backup = store.BackupKeybindFile(account.KeybindPath);
            FileSync.Apply(live, defaults, profiles, target);
            live.Save(account.KeybindPath);
            keybindStampUtc = File.GetLastWriteTimeUtc(account.KeybindPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            ChoiceWindow.Info(this, "Couldn't write Dota's keybind file", ex.Message);
            return;
        }

        applied = target;
        store.SaveAppliedSlug(target?.Slug);
        RefreshProfiles(Selected?.Slug);
        CheckDrift(live, new List<string>());
        SetStatus((target == null
            ? "Restored Default in Dota's keybind file."
            : $"Wrote {target.Name} into Dota's keybind file. Start Dota to play with it.") + " Backup: " + Path.GetFileName(backup));
    }

    void Recapture_Click(object sender, RoutedEventArgs e) => RecaptureDefault();

    void RecaptureDefault()
    {
        if (account == null || !account.HasKeybindFile)
            return;
        var problems = new List<string>();
        var live = ReadKeybindFile(problems);
        if (live == null)
        {
            ChoiceWindow.Info(this, "Couldn't read Dota's keybind file", string.Join(" ", problems));
            return;
        }

        var fresh = FileSync.Recapture(live, defaults, profiles, applied);
        var names = new HashSet<string>(fresh.EntryNames, StringComparer.OrdinalIgnoreCase);
        if (defaults != null)
            names.UnionWith(defaults.EntryNames);
        var changed = defaults == null ? names.Count : names.Count(n => fresh.Get(n) != defaults.Get(n));

        defaults = fresh;
        store.SaveDefault(defaults);
        foreach (var profile in profiles)
        {
            Keymap.Normalize(defaults, profile);
            store.SaveProfile(profile);
        }
        WriteExecFiles();
        RefreshProfiles(Selected?.Slug);
        CheckDrift(live, problems);
        SetStatus(changed == 0 ? "Default already matches Dota." : $"Updated Default from Dota ({changed} hotkey{(changed == 1 ? "" : "s")} changed).");
    }

    // ---------------------------------------------------------------- chrome

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Asks Windows 10/11 for a dark title bar to match the app.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int on = 1;
        // 20 is DWMWA_USE_IMMERSIVE_DARK_MODE; builds before 20H1 used 19.
        if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
    }

    void UpdateDotaState(bool force = false)
    {
        var running = SteamLocator.IsDotaRunning();
        if (running == dotaRunning && !force)
            return;
        var wasRunning = dotaRunning;
        dotaRunning = running;
        DotaDot.Fill = (Brush)FindResource(running ? "OkBrush" : "FaintBrush");
        DotaText.Text = running ? "Dota running" : "Dota closed";
        UpdateFileSection();
        if (wasRunning && !running)
            CheckKeybindFileChanged();
    }

    void ShowBanner(string text, params (string Label, Action OnClick)[] buttons)
    {
        BannerText.Text = text;
        BannerButtons.Children.Clear();
        foreach (var (label, onClick) in buttons)
        {
            var button = new Button { Content = label, Margin = new Thickness(8, 0, 0, 0) };
            button.Click += (s, e) => onClick();
            BannerButtons.Children.Add(button);
        }
        Banner.Visibility = Visibility.Visible;
    }

    void HideBanner() => Banner.Visibility = Visibility.Collapsed;

    void SetStatus(string text) => StatusText.Text = text;

    void Reload_Click(object sender, RoutedEventArgs e)
    {
        Discover(account?.AccountId);
        SetStatus("Reloaded.");
    }

    void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(store?.Directory ?? dataRoot);

    void OpenBackups_Click(object sender, RoutedEventArgs e) => OpenFolder(store?.BackupsDirectory ?? dataRoot);

    void ReportBug_Click(object sender, RoutedEventArgs e) => OpenUrl(RepoUrl + "/issues/new/choose");

    static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start("explorer.exe", "\"" + path + "\"");
    }

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
