# Contributing

Thanks for your interest in Dota 2 Settings Setter. This project is open source ([MIT](LICENSE)) and contributions are welcome.

## How to contribute

1. **Check existing [issues](https://github.com/Avi-From-Polysights/Dota2SettingsSetter/issues).** Someone may already be on it, or you can discuss the idea first.
2. **Fork** the repository and create a branch from `master`.
3. **Make your changes.** Keep diffs focused and match the existing code style.
4. **Test locally.** Run `dotnet test`, then run the app against sandboxed copies of your Steam folders (see the README) and check that what you changed works.
5. **Open a pull request** against `master` with a clear description and test notes.

## Pull request reviews

- All pull requests require **approval from the maintainer** ([@Avi-From-Polysights](https://github.com/Avi-From-Polysights)) before merge.
- The maintainer merges approved PRs; please don't merge your own PR unless asked.
- Feedback may take a few days. Ping on the PR if it stalls.

## QA and bug reports

In-game behaviour can only be checked in Dota itself, so test reports are as valuable as code. Use the issue templates:

- **QA result** for reporting the outcome of a test from the QA checklist, whether it passed or failed.
- **Bug report** for anything broken.

Include the app version, the switch method (exec / pasted binds / Write to Dota), and the exact console text.

## Project layout

| Path | What's there |
| --- | --- |
| `src/Dota2SettingsSetter/Core/` | Everything that isn't UI: KeyValues parsing, the action catalog, profiles, console script generation, keybind file sync, Steam discovery |
| `src/Dota2SettingsSetter/Views/`, `MainWindow.xaml` | WPF UI |
| `tests/Dota2SettingsSetter.Tests/` | xUnit tests for `Core` |

`Core/ActionCatalog.cs` lists every hotkey the app edits. Names, actions and labels come from the game's `cfg/dotakeys.lst` and English localization. If Dota adds a hotkey, add it there.

## Code of conduct

Be respectful in issues and PRs. Constructive criticism and good-faith collaboration only.
