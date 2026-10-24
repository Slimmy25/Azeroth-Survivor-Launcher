# Azeroth Survivors Launcher

The launcher for **Azeroth Survivors**, a game made by Aethus, styled after the 2006 World of Warcraft website.

- **PLAY / INSTALL**: installs the game client, then starts it
- **Cloud Save**: keeps your save game, AddOns and addon settings in Google Drive, OneDrive or Dropbox and
  shares them between your PCs (downloaded before the game starts, uploaded after it closes, even after a crash)
- **Addons**: switch addons on and off, two saveable profiles, and **Download More** to install addons with one click
- **Changelog**: every patch note, newest first
- **UPDATE**: checks this repository's latest release when the launcher starts and offers the new launcher exe
  (never forced); it downloads it, checks its SHA-256 and restarts itself. Game updates can be switched on too
- Runs in the system tray; only one copy runs at a time

## Download

Get `Azeroth Survivors Launcher.exe` from the [downloads page](https://github.com/Slimmy25/Azeroth-Survivor-Launcher-Releases/releases/latest) and put it in the folder where the
game is (or should be installed). It needs nothing else: it runs on the .NET Framework 4.8 that is part of
Windows 10 and 11.

## Where the launcher keeps its files

Everything the launcher writes for itself goes into a folder named `Azeroth Survivor Launcher` next to the exe:
settings (`config.json`), the cloud sync memory (`state.json`), `log.txt`, backups made before a sync replaces
anything (`backups`), the last 5 cloud downloads (`downloads`) and temporary files (`temp`).

It uses `%LOCALAPPDATA%\Azeroth Survivor Launcher` instead when the exe's folder can't be written to (for example
Program Files), or when the exe sits inside Google Drive, OneDrive, Dropbox or iCloud. The sync memory belongs to one
PC and must not be copied to the others.

Some things stay where they belong: the game itself, its save folder and AddOns, the cloud sync folder, and the
temporary undo folder of a game update, which is kept inside the game folder.

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
| Where launcher updates come from | `Repo` in `src/SelfUpdate.cs` (the latest non-pre-release) |
| Game update address | `ManifestUrl` in `src/Update.cs` |
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
