# Azeroth Survivors Launcher

A community launcher for **Azeroth Survivors**, the game made by Aethus, in a retro classic-launcher look
(night stone and gold, original art drawn by `tools/make_launcher_art.py`).

- **PLAY / INSTALL**: downloads the game client inside the launcher (with a percentage bar, into a `temp` folder that is
  deleted afterwards), installs it next to the launcher, then starts it; first-time setup begins with this; the News panel shows the newest patch notes,
  and a "This week" card sums up your recent runs
- **Cloud Save**: keeps your save game, AddOns and addon settings in Google Drive, OneDrive or Dropbox and
  shares them between your PCs (downloaded before the game starts, uploaded after it closes, even after a crash)
- **Addons**: switch addons on and off, two saveable profiles, and **Download More** to install addons with one
  click (it offers an UPDATE when the copy on GitHub is newer than yours)
- **New Player Guide**: topics and class pages with videos that play inside the launcher (streamed, never saved),
  and a search over every page
- **Changelog**: every patch note, newest first
- **Stats** (optional, can be hidden in Options): with the built-in **AZS Stats** addon (in game: `/azs` or its
  minimap button), every run's result and DPS, DPS over time, your best builds, class, race and zone records,
  a death log with your Nemesis and the bosses you beat, and co-op stats with your friends. Click a run to copy a
  picture of it for Discord
- **UPDATE LAUNCHER**: a newer launcher on this project's downloads page is installed when the launcher starts (its
  SHA-256 is checked, then it restarts itself); game updates can be switched on too
- Runs in the system tray; only one copy runs at a time; 26 languages

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

## What it reaches on the internet

- The game's download hosts (`ClientUrl`, with the shared download login, and the backup `ClientMirrorUrl` in
  `src/Launcher.cs`, one picked at random): the game client
- GitHub: launcher updates, game updates, the addons offered in Download More, and their upload dates
- jsDelivr (a CDN for GitHub files): the guide's videos, streamed while they play
- Wowhead's image host (wow.zamimg.com): spell, class, race, zone and creature pictures on the STATS page, shown only
  when the page is open and cached in the launcher's `temp` folder; a spell missing from `assets/spell-icons.txt` is
  looked up on Wowhead once
- Your cloud drive's folder on this PC (Cloud Save), nothing else of your files

To play videos inside the launcher it uses Windows' own web view, and asks Windows to run that view in its modern
mode for this exe: one value under
`HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION`.

## Build it yourself

Double-click `build.bat`. It uses the C# compiler that comes with Windows, so nothing has to be installed.
The exe appears in the `dist` folder. Every upload to this repository is also built automatically on
GitHub (see the **Actions** tab).

## Editing

| What | Where |
| --- | --- |
| Patch notes | `assets/changelog.txt` (posts separated by a line with only `===`) |
| Download More list | `assets/addons.txt` (format explained at the top) |
| About Launcher: description | `assets/launcher-about.txt` |
| About Launcher: launcher changelog | `assets/launcher-changelog.txt` (newest first) |
| New player guide | `assets/guide.txt` (a video: `@video Title \| address`, see the top of the file) |
| Guide videos | `guide-videos/<topic>/` (published by Actions > Publish guide videos) |
| Spell icons | `assets/spell-icons.txt` (then `tools/make_addon_icons.py` for the addon) |
| The AZS Stats addon | `addons/AZSStats` (built into the exe; the STATS page installs it) |
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
- Cloud save engine: built on the sync framework by KeijinDE, used with his blessing (MIT, see THIRD-PARTY-NOTICES.md)

## License

The code is under the [MIT License](LICENSE). The game's name, logo and artwork are not covered by it.
Third-party parts keep their own licenses, listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): the cloud sync
engine (MIT), the Source Serif 4 font (SIL Open Font License), the creature list from the CMaNGOS Classic database,
and the pictures and videos shown from Wowhead and jsDelivr, which belong to their owners.
