# Azeroth Survivors Launcher

The launcher for **Azeroth Survivors**, a game made by Aethus, styled after the 2006 World of Warcraft website.

- **PLAY / INSTALL**: installs the game client, then starts it
- **Cloud Save**: keeps your save game, AddOns and addon settings in Google Drive, OneDrive or Dropbox and
  shares them between your PCs (downloaded before the game starts, uploaded after it closes, even after a crash)
- **Addons**: switch addons on and off, two saveable profiles, and **Download More** to install addons with one click
- **Changelog**: every patch note, newest first
- **UPDATE**: checks for a new game version when the launcher starts and offers it (never forced)
- Runs in the system tray; only one copy runs at a time

## Download

Get `Azeroth Survivors Launcher.exe` from the [Releases](../../releases) page and put it in the folder where the
game is (or should be installed). It needs nothing else: it runs on the .NET Framework 4.8 that is part of
Windows 10 and 11.

## Build it yourself

Double-click `build.bat`. It uses the C# compiler that comes with Windows, so nothing has to be installed.
The exe appears in the `dist` folder. Every upload to this repository is also built automatically on
GitHub (see the **Actions** tab).

## Editing

| What | Where |
| --- | --- |
| Patch notes | `assets/changelog.txt` (posts separated by a line with only `===`) |
| Download More list | `assets/addons.txt` (format explained at the top) |
| About Launcher page | `assets/about.txt` |
| Update address | `ManifestUrl` in `src/Update.cs` |
| Install link, Discord link | `src/Launcher.cs` |

## Game updates

`make_update.bat` writes `update.json` for a new game version. Attach the game archive and that `update.json`
to a release; the launcher reads
`https://github.com/<owner>/<repo>/releases/latest/download/update.json`.

## Code signing

Create a `sign_config.bat` as described at the top of `sign_build.bat` (never upload it); `build.bat` then
signs the exe automatically.

## Credits

- Launcher: Slimjim
- Cloud save engine: ported from [AzerothSync](https://github.com/KeijinDE/ASDataSync) by KeijinDE (MIT)

## License

The code is under the [MIT License](LICENSE). The game's name, logo and artwork are not covered by it.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the cloud sync engine's license.
