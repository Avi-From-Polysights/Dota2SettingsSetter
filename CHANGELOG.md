# Changelog

## v0.1.0

First release, ready for QA.

- Saves your current Dota hotkeys as **Default**, read from `dotakeys_personal.lst`.
- **Hero profiles** store only the keys that differ from Default. Click a key to rebind it, with swap, unbind or keep-both when it's taken. Alt autocast keys follow their item or ability key.
- **Switch in game** with `exec dss_<profile>` / `exec dss_default`, or by pasting the minimal bind commands the app shows.
- **Switch by keybind file**: writes a profile into Dota's own hotkey file while Dota is closed, with a backup first, and restores Default the same way.
- Warns when Dota's file has changed since Default was saved, and can re-capture Default.
- Import and export profiles to share setups.
