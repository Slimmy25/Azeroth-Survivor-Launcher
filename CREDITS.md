# Credits and sources

The Azeroth Survivors Launcher is made by **Slimjim** ([Slimmy25](https://github.com/Slimmy25)). It stands on a lot of
other people's work. This page says where everything comes from, who owns it, and what this repository does and
does not contain.

## The game: Azeroth Survivors, by Aethus

**Azeroth Survivors** is made by **Aethus**. It is played at <https://wow.aethus.pl/> (in the browser) and as a
Windows client, `AzerothSurvivors.exe`. The game, its code, its data, its patch notes and its logo belong to Aethus.

- **The game client is not in this repository and is not hosted by this project.** The launcher downloads it from
  the game's own download hosts (`src/Launcher.cs`): a Mirror By Community Member (Leo), a Mirror By Community
  Member (Meexa), a Mirror By Discord Mod (Fotze), and, as a last resort, the game's own
  download page, <https://wow.aethus.pl/download>. Each download is checked against the game's published checksums.
- **Game data** the launcher shows (talent trees, spell and talent descriptions, names) is the game's own, by Aethus.
  The Guide was written for the launcher from what the game shows and says.
- **Patch notes** in News and Changelog (`assets/changelog.txt`) are the game's own, by Aethus.
- **The game's addon API** used by the AZS addons is documented by the game.
- **The game's logo** (the gold "AS" on its blue globe: the launcher's shortcut and window icon,
  `assets/AzerothSurvivors.ico`, and the small round picture in the corner of its windows, `assets/portrait.png`) is
  Aethus's work and belongs to Aethus.

## World of Warcraft and Blizzard Entertainment

Azeroth Survivors runs on the World of Warcraft 1.12 interface, and is set in Blizzard's world of Azeroth. World of
Warcraft, Azeroth and every name, picture, model, sound and other asset from it are trademarks and copyrighted work of
**Blizzard Entertainment, Inc.** This project is not affiliated with or endorsed by Blizzard.

**No Blizzard asset is stored in this repository or in the launcher.** Where Blizzard's material appears, it comes
from somewhere else at the moment it is shown:

| What you see | Where it comes from |
| --- | --- |
| The game itself, with its models, art and sounds | The game client, downloaded from the game's hosts (above) |
| Spell, talent, class, race, zone and creature icons and pictures in the launcher | Loaded when shown from **Wowhead**'s public image host, <https://wow.zamimg.com>, and cached only in the launcher's `temp` folder. Which icon belongs to which spell is in `assets/spell-icons.txt` (names only). |
| Icons and textures in the AZS addons | Read from the game client's own files by path (for example `Interface\Icons\...`) |
| Spell, zone, boss and class names | As the game names them |
| Creature models on the STATS page | Model numbers only (`assets/npc-models.txt`), from the **CMaNGOS Classic** world database: <https://github.com/cmangos/classic-db> |
| Gameplay in videos | Recordings of the game, by the people credited below |

Everything the launcher draws for itself (the stone, metal and night-sky window, the buttons, the icons in the side
bar) is original, made by `tools/make_launcher_art.py` or drawn in code.

## Code

### Cloud sync: KeijinDE
The launcher's cloud sync (`src/Sync.cs`) is built on the sync framework by **KeijinDE**, used with his blessing.
Original project: <https://github.com/KeijinDE/ASDataSync>

```
MIT License

Copyright (c) 2026 KeijinDE

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Everything else
The launcher, the AZS addons (AZS Stats, AZS Handbook, AZS Minor Mods, AZSKit) and the tools are by Slimjim, under the
[MIT License](LICENSE).

## Addons in Download More

Each addon belongs to its author. The copies on this project's `addons` release are mirrored from the original
download links in `addons/sources.txt`, so the launcher can download them all from one place.

| Addon | Author | Original |
| --- | --- | --- |
| Azerloot | Kindagrey | <https://github.com/Kindagrey/Azerloot> (its newest release) |
| BlackwellUI (BUI) | Blackwell | <https://github.com/Blackwellcode/BlackwellUI> (its newest release) |
| Bordo UI | BordoBereli | <https://github.com/threenox/BordoUI> (its newest release; GPL-3.0) |
| Damage Meter (AZS Spell Meter) | Kexo | <https://github.com/kex0/AZSSpellMeter> (its newest release) |
| LuckyOdds | Cadoky | <https://github.com/cadoky/LuckyOdds> (its newest release) |
| Keijin UI | KeijinDE | <https://github.com/KeijinDE/KeijinUI_modern-as-ui> |
| Survivor UI (with Survivor Plates) | morderous | <https://github.com/morderous/survivor-ui> (its newest release, the addon zip; GPL-3.0) |
| SurvivorsDragonUi | Mxgwyn | <https://github.com/migwynkriid13/SurvivorsDragonUi> |
| AZS Stats, AZS Handbook, AZS Minor Mods | Slimjim | this repository |

### Classic addons converted for this game
These are World of Warcraft 1.12 addons, rewritten by Slimjim to run in Azeroth Survivors' client. The original
authors keep their credit and their licenses.

| Addon | Original authors | Original | License |
| --- | --- | --- | --- |
| MinimapButtonBag | Tunhadil, continued by LaYt | <https://github.com/laytya/MinimapButtonBag-vanilla> | none stated by the original project; credited to its authors |
| BugSack and !BugGrabber | Fritti and Rabbit (credits: Rowne, Ramble, kergoth, ckknight, Gamefaq, thomasmo, damjau) | BugSack 2.1.0 / BugGrabber 2.1.1 for WoW 1.12 | Creative Commons BY-NC-SA 2.5 (`addons/BugSack/License.txt`); the converted version is shared under the same license |

## Community videos

Videos are never stored in this project or on your PC. They are streamed from YouTube when you play them, with the
creator's name and channel shown with each one.

| Creator | Channel | Where |
| --- | --- | --- |
| Holdherhand | <https://www.youtube.com/@Holdherhand> | Build Guides (11 class guides) |
| Des | <https://www.youtube.com/@Dessiq> | Build Guides (rogue, warrior) |
| Duke | <https://www.youtube.com/@duke-yt> | Build Guides (arcane mage) |
| Asmongold TV | <https://www.youtube.com/@AsmonTV> | Featured on the home screen |
| Sodapoppin and other creators | named on each video in the launcher | Featured on the home screen (`src/Community.cs`) |

The short how-to videos in the Guide (`guide-videos/`: camera distance, the DPS meter, luck, changing your race) are
recordings of the game by Slimjim.

## Fonts, logos and tools

- **The launcher's emblem** (`assets/logo.png`: the two cat heroes with their weapons over the shield, beside the
  title on the home screen) is **Slimjim**'s own art.
- **Source Serif 4** (the launcher's text font, `assets/fonts`, unchanged), by **Adobe**, under the SIL Open Font
  License 1.1: `assets/fonts/SourceSerif4-OFL.txt`. <https://github.com/adobe-fonts/source-serif>
- **The Discord logo** (`assets/discord`), from Discord's brand assets, used to link to the game's Discord.
- **Microsoft Edge** plays the YouTube videos inside the launcher; Windows' own web view plays the Guide's videos.
- **Translations** (`assets/lang`) were machine-made for the launcher. Corrections from native speakers are welcome.
