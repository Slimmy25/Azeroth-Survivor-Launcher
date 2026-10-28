<!-- The next release's notes, written as changes are made. On release this file becomes v<version>.md (0.1 higher
     than the last release), its text goes to the top of assets/launcher-changelog.txt, and a new next.md starts. -->

## What's new

### News and Changelog
- Patch notes for the game's 0.123.0 update: co-op holds together on a laggy connection (enemies no longer freeze and
  jump, no more teleporting back, level-up cards no longer come back or pick the wrong card).

### Major Update, part 1
- A "?" beside the window's X explains the path at the top: why there are no rows of buttons (to give pages more room)
  and how the path does their job.
- Class pages: the class's GEAR button now sits at the top, beside TALENT TREES.
- Gear Planner: a piece's stars show as T1 to T5 in its top right corner (in the AZS Handbook too).
- Gear Planner and Talent Tree Builder look sharper: their icons are drawn at a size that keeps them crisp.
- Setup closes any message box that was open when it starts.

### Major Update, part 2
- Gear Planner: two buttons sit where the slot's header was, Single Item and Tier Bonus. Single Item shows the chosen
  slot with the name of the piece it holds, then everything it can hold. Tier Bonus lists every rarity (Common to
  Legendary) with 4 piece and 8 piece side by side: one click puts that set on (4 pieces at T1, or all 8 at T3).
- Guide > Classes: clicking a set chip under a spell ("Frostfire 8-piece") opens the Gear Planner with that whole set on.
- Addons > Download More: no row of category tabs; the category is a step of the path (Addons › Download More › UI ▾).
- Options > Launcher: Language, About Launcher and Run setup again are places in the path (Options › Launcher ›
  Settings ▾) instead of a row of buttons.
- Options > Folders: no CHANGE buttons; click a folder to change it, or use Change ▾ in the path, where importing a
  save is too.

### Major Update, part 3
- STATS > History: clicking a run no longer makes a picture for Discord. It opens a short post-game summary of that run
  instead: its result, time, level, kills and DPS, what it fought and earned, and a few fun highlights (kills a minute,
  biggest crit and heal, most kills in 5 seconds, the spells it leaned on, the spell that did most of the damage, how it
  compares with your other runs of that class, and who killed you).
- About Launcher and CREDITS.md thank the people who peer reviewed this work: Keijin, Kraatos, Maidenhead and the
  Discord's Mod team.

### Major Update, part 4
- Guide > Gameplay: a new Transmog topic with every look that adds nothing: the 14 Appearances (head and back, each
  with the achievement that brings it), the Tabards (earned and bought, with their price) and the Weapon Glows, which
  move there from Gear & the Armory. They come from a small database of looks (assets/transmog.txt).
- AZS Minor Mods: the plain action bar is no wider than its six squares and the two page arrows, so it leaves no empty
  space. New: cooldown numbers on the spell squares, the seconds until each spell is ready again (on by default).

### Major Update, part 5
- README: six new screenshots of the finished launcher, each showing something different (home and Community, STATS,
  the Talent Tree Builder, the Gear Planner, a class's gear page and the path at the top).
- Gear Planner: its note under the totals is shorter, which leaves more room for the set bonuses.

### Fixes and requests
- The "?" beside the window's X now reads "Lost?" (one button, in your language).
- Community: two boxes beside its title, Big Streamers and Content Creators. Tick one and, five seconds later, the
  featured video is picked from that group's shuffle. The choice is remembered.
- The launcher no longer pops back up by itself after the game closes. It stays in the taskbar, and only comes back
  when the game updated, crashed or something needs your attention.
- The game download's temporary folder is now called "Azeroth Survivors Launcher - Temp", so it's clear whose it is.
- AZS Stats, new in game: a bounty board. When a run starts, a WANTED poster shows for a few seconds with the creature
  that killed you last game, how many times it has killed you and whether it's your Nemesis. Take your Nemesis down
  and a "Nemesis slain!" celebration shows. It uses the game's own parchment, frames, icons and sounds, and the
  creature's face once the launcher has put it in AZS Stats. Click it to put it away; /azstats bounty turns it off and on.
- AZS Stats remembers each time you took your Nemesis down and who was at your side (your party, or alone). The
  celebration names them, and the Nemesis tab lists your last five wins over it.
- AZS Stats only keeps your own runs, records and Nemesis log now. It no longer writes down the game's tooltips or the
  icons of the gear your heroes wear for the launcher, and clears what older versions wrote down. Until the launcher has
  another source for gear icons, gear shows its slot letters (in the Gear Planner, the Armory and the AZS Handbook).
- Guide > Transmog: hover a look that is a real World of Warcraft item (20 of them) to see its 3D model picture from
  Wowhead's model viewer, beside how to earn it.
- AZS Minor Mods: new Mark minibosses (on by default). Each zone's bosses before its final boss (as the Guide's Zones &
  Bosses lists them) get a Star, a Triangle and a Diamond, so the Skull and Cross are left for the final boss. Boss
  markers are now Skull, Cross and Diamond (no more Moon).
- AZS Minor Mods: a friend who falls blinks their own marker 15 times, on and off: Square, Moon or Circle, one each,
  so several friends can be down at once. Those three are never put on a boss.
