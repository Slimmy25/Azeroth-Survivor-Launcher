# v2.4 (2026-10-28)
## In short
Talent builds now load as soon as you select their saved name, and share codes have a validated, versioned format for reliable importing between players. The test executable is removed; this is the sole launcher release.

## Talent Tree Builder
- Selecting a saved build name immediately loads that build.
- EXPORT creates a compact share code containing the format version, class and talent allocations.
- IMPORT validates the whole code and explains class mismatches or invalid builds instead of silently producing an empty build.

## Release
- Removed the separate test executable from local builds, CI artifacts and published releases.

## Game updates 0.126.0 and 0.127.0
- Added the creator's original game patch notes without rewriting their wording.
- Updated the Guide and AZS Handbook for the level-120 cap, 30-totem limit, Major Soulstone's 2-second revival protection, Mograine Marks and ground-circle escapes.
- Updated guidance for international names and input methods, co-op ping and country display, lobby chat, jewelry stats and amulet replacement confirmation.
- Added Level-Up Rays to the launcher's Graphics options.
- Updated shared item data and its generator so the Guide, Gear Planner and Handbook retain Major Soulstone's revival protection.

## Fixes
- Fixed Talent Builder import errors that prevented the launcher from building.

---

# v2.3 (2026-10-28)
## In short
The Talent Tree Builder's saved-build menu now follows the Gear Planner's proportions, with centred fields and evenly spaced action buttons down to the bottom.

## Talent Tree Builder
- The SAVED BUILDS panel keeps the same 60/40 layout pattern as the Gear Planner, with its title and action buttons centred.
- SAVE, LOAD, EXPORT and IMPORT are evenly spaced vertically, using the panel's available height.

---

# v2.2 (2026-10-28)
## In short
Talent builds can now be saved by name for each class, loaded later, exported as shareable codes, and imported from codes. The Guide and Community video pages also get small usability refinements.

## Talent Tree Builder
- Talent builds can be saved by name for each class, loaded later, exported as a shareable code, and imported from a code.

## Videos
- Guide videos: no EXIT FULLSCREEN button over a fullscreen video any more; the player's own button or Esc brings it back. No "Lost?" while a Guide video plays.
- Community: no line about whose video it is above the featured video (the ticked box says it), and "Lost?" is shown in MORE VIDEOS.

---

# v2.1 (2026-10-28)
## In short
A big one. Addon profiles each keep their own interface, there's a ROLLBACK for addons and an Advanced settings mode, uninstalling happens inside the launcher, and two new addons by Maidenhead are in Download More. Videos open without a white flash and close with Esc, the Guide's own videos have a YouTube-style player, the Gear Planner shows five small stars, the Talent Tree Builder makes room for saving builds, the Guide follows the game's 0.124 and 0.125 updates, and Options › Launcher holds everything on one page.

## First-time setup
- The launcher runs first-time setup whenever it starts in a folder with neither its own folder nor the game beside it
  (a new copy, or the exe moved somewhere new), even if it finds a game, a save or settings from before. Settings made
  for another folder count as new too. A launcher started where it was set up carries on as before.

## Options
- Options › Launcher is the end of its path: nothing on it opens a step of its own any more. LANGUAGE opens a list of
  the languages right under its button, UPDATE LAUNCHER is back (looks for a newer launcher now), and About the
  launcher is written out at the bottom of the page, rewritten in simple words with everything the launcher does.
- Options › Launcher: no Settings step in the path any more. LANGUAGE, ABOUT LAUNCHER and RUN SETUP AGAIN are buttons
  on the Launcher page itself.

## Path and Lost?
- Every step of the path at the top looks the same, the first one included: one font, one size, one colour.
- "Lost?" explains the path in general words (no example page any more), and why it is used: the box is small and
  needs every bit of space.

## Talent Tree Builder
- The trees sit on the left; the right side holds a box for the coming build tools (save a build, export and import
  builds), sized like the Gear Planner's two boxes.
- The "?" beside RESET is pointed at, not clicked.

## Gear Planner
- Tier Bonus empties the whole planner before it fills in the set: armor, weapons and jewelry, with their stars. What
  you see is only the set you just picked.
- A piece's stars show as a column of 5 small stars on the inner side of its box (it was T1 to T5): a left click fills
  the next one, and after the fifth they empty again. The AZS Handbook shows the same 5 stars beside each piece.

## AZS Handbook
- The Handbook's gear planner works like the launcher's: Single Item and Tier Bonus buttons, a 4 piece and an 8 piece
  button for every rarity's set (they empty the planner first, as in the launcher), and the chosen piece's name above
  the list of pieces.

## Community
- A new video in Big Streamers.
- The round X over a playing video goes back to the featured video of your ticked group, also when you had opened it
  from MORE VIDEOS.
- YouTube videos no longer flash a white page before they start: the player stays behind "Loading the video..." until
  the video (or its ad) plays.
- Esc closes a YouTube video (in fullscreen it leaves fullscreen first).
- While a video plays, the right end of the header says "ADS ARE YOUTUBE. NOT ME."

## Guide
- The game's patch notes 0.124.0 are in the Changelog.
- Transmog › Weapon Glows: the new Unholy Weapon glow, from the co-op achievement The Insane (40 minutes in Naxxramas
  on Heroic V with at least two other players).
- Talents updated from the game for 0.124.0: Soul Link's shield holds at most 10% of your max health (was 25%), and
  Improved Demoralizing Shout's extra damage counts for your allies too.
- Transmog › Weapon Glows: a new card opens Wowhead's live 3D dressing room in a window of its own, where the glows
  can be seen on a weapon (Wowhead's still pictures don't show them). It closes with the launcher.
- Links: Power Word: Shield and Shadow Word: Pain link as a whole (they stopped at the colon, as "Power Word" and
  "Shadow Word"), on the Priest page and wherever they are named, such as Co-op.
- Talent names that more than one class has (Flurry, Deflection, Ferocity and others) follow the class page they
  are on: on the Shaman page, Flurry is the Shaman's Flurry, with its own text. Anywhere else, the hover shows each
  class's version.
- Warrior: the Fury tree lists its real milestone, Flurry (it said "Fury", which is the tree, not a talent).
- The game's patch notes 0.125.0 are in the Changelog.
- Co-op: a run needs at least two players, the lobby's party chat and each guest's ping, fewer extra enemies per
  player, enemies 20% weaker in a party, and faster revives with more allies.
- Mage: heals for 0.5% of all the damage it deals.
- Gear & the Armory › Stars: a lower set at 5 stars gets the legendary sets' stats it lacks and reaches 90% (common)
  to 98% (PvP) of a 5-star legendary set (it said 95% for all).
- Gear: the legendary (tier 3) sets' armor has half the echo it had (the Items pages, the Gear Planner and the AZS
  Handbook); their weapons are unchanged.

## Guide videos
- The Guide's own videos have a new control bar that looks like YouTube's: a red progress bar, play, volume, the
  time and fullscreen on the right, hiding when the mouse rests. Space or K plays and pauses, the arrows skip 5
  seconds, M mutes, F goes fullscreen.
- The launcher's own FULLSCREEN button is gone; the player's own fullscreen button does it, now with a single click
  (it used to need two quick clicks).
- Big Streamers is ticked whenever the launcher starts, until you pick Content Creators; that choice is remembered.
- One of the two boxes is always ticked, never neither: unticking the ticked one does nothing, ticking the other
  switches to it.
- A small heart left of the Discord symbol opens the game developer's Buy Me a Coffee page ("Love the game? Support
  the Dev by buying him a coffee"), with the page's address at the bottom of its hover note.
- The Discord symbol's hover note says "Join the community!", with the invite link at the bottom.

## Gear Planner (layout)
- The explanation under the stat boxes is now a button, "How are gear stats calculated?", in the top right corner of
  the character sheet above Hands; click it to read it. The set bonuses move up into its place.
- The T1–T5 tier numbers sit on the inner side of each piece: by the top right corner in the left column and by the top
  left corner in the right column, so both columns lean in towards the middle.

## Addons
- Maidenhead's Azeroth Stats and AZS Stats can't be on together (both record your runs). Maidenhead's addon has the
  right of way: when both are found on, AZS Stats is switched off.
- Fix: the launcher never takes Maidenhead's Azeroth Stats for anything of its own: installing or updating AZS Stats
  leaves its folder and saved data alone.
- Uninstalling an addon no longer opens a window: the question takes the place of the right-hand column of My
  Addons (UNINSTALL or CANCEL), so the whole list stays in view while you decide. UNINSTALL in Download More opens
  it there too.
- The profiles are hidden by default: the Addons section shows only ADVANCED SETTINGS at the bottom of the right
  column. It brings back all three profile choices, with BASIC SETTINGS under them to hide them again (remembered).
- Download More: two new addons by Maidenhead, Azeroth FieldKit (an in-game helper: elite nameplates, boss frames,
  a damage meter, chests and lairs on the minimap and more) and Azeroth Stats (run history, run reports and a HUD
  you set up yourself).
- Addons update themselves: when an installed Download More addon has a newer copy on GitHub, the launcher updates it
  right away, without asking (at start, and when the game closes). Never while the game is running; the log lists
  each one.
- ROLLBACK (Addons › Advanced settings): the launcher keeps the last 10 copies of each Download More addon it
  downloads. Pick an older copy to install it in place of the newest: your addon settings and customization stay as
  they are. A rolled-back addon shows [Outdated] beside its name in My Addons (it still switches on and off) and is
  not updated automatically until you pick Newest again or install it from Download More.
- No profile, Profile 1 and Profile 2 are each a complete state of their own: which addons are on or off, and the
  interface (where its frames sit, chat windows and every addon's saved settings). The game's graphics and other
  options, and key bindings, are not part of a profile: they stay the same in all three.
  Switching stores the one you leave exactly as it is and brings back the one you pick as you left it; a choice
  picked for the first time starts from where you are. LOAD works on a profile that was never saved.
- The "?" notes of No profile, Profile 1 and Profile 2 say what each one is: a full copy of itself in its own isolated
  space, back exactly the way you left it whenever you return to it.
- Profile 1 and Profile 2 can have names of your own: click a profile's name, type one and press Enter (an empty name
  brings back "Profile 1" or "Profile 2").

## AZS Stats
- Every run keeps its Heroic affixes (from the game's 0.124.0); a run's summary on the STATS page lists them.

## Launcher
- When the launcher updates itself while it is in the taskbar or the tray, it does so quietly: no Windows notice.

## Fixes
- An "&" in a name (Gear & the Armory, Pickups, Caches & Guardians) shows as "&" in the path's lists and the hover
  notes, no longer as an underline.
# v2.0 (2026-10-27)
In short: MORE VIDEOS follows your Big Streamers / Content Creators choice, Tier Bonus starts a set fresh, "Lost?" became a small hover note, every step of the path goes straight there, Theorycrafting moved into Gameplay, and the launcher no longer comes back empty from the taskbar.
## Community
- MORE VIDEOS follows the two boxes: with Big Streamers ticked it lists only the big streamers' videos, with Content
  Creators ticked only the content creators' (and the featured video comes from that group too). With neither, it lists
  them all.

## Gear Planner
- Tier Bonus: a set is a starting point, not a pile. Picking one empties every armor slot first, so nothing from the
  set picked before stays (4-piece Common, then 8-piece PvP, then 4-piece Legendary leaves only the 4 Legendary
  pieces). Only more of the same set adds to it (its 4 pieces, then its 8). Weapons and jewelry stay.

## Lost?
- "Lost?" no longer opens a window over the launcher, and it is not clicked: point at it and a small note beside it
  explains the path at the top, with an example (Guide › Classes › Priest) and a small picture of it. The note goes away
  when the pointer leaves.
- "Lost?" shows in every section that has a path to follow, its first page included (the Changelog's too). Only
  Community, which has nothing to find your way around, has none.

## Changelog
- CHANGELOG opens on the list of every patch note to pick from, instead of the second newest one (News already shows
  the newest).

## The path at the top
- Every step of the path goes straight there when clicked, the first one (the section) included, even while a step's
  list is open. The lists still open when you point at a step or click its ▾.
- Options › Launcher › Language: the path no longer lists the languages, so passing over it can't change your language
  by accident. Pick a language on the page itself.
- Options › Folders: no Change Folder ▾ step (it only offered the folders already on the page). IMPORT A SAVE is a
  button on the Folders page now.

## Options
- Cloud Save fits the window without a scroll bar: its log takes the room that is left.

## Guide
- Theorycrafting lives in Gameplay now: it is a card in Gameplay on the Guide's main page, and the path reads Guide ›
  Gameplay › Theorycrafting › Talent Tree Builder (or Gear Planner). Gameplay's list in the path has Theorycrafting,
  so you can get there from the path. The THEORYCRAFTING button on the main screen stays.
- Guide videos: a FULLSCREEN button above the video, the launcher's own, so fullscreen always works (it plays on from
  the same point; EXIT FULLSCREEN, Esc, double-click or F bring it back). The guide's own videos (hosted with the
  launcher) no longer have an OPEN IN BROWSER button.

## Fixes
- The launcher no longer comes back empty from the taskbar. Minimizing keeps it in the taskbar (it used to hide itself
  to the tray a moment later, and clicking it in between showed Windows' opening animation and nothing else), and
  coming back draws everything again, its section included. "Minimize when the game starts" minimizes it to the taskbar
  too, instead of hiding it.

# v1.9 (2026-10-27)
In short: Easier to get around: the big section buttons always open the main page, every step of the path can be clicked, Options › Folders has its OPEN LOCATION and CHANGE FOLDER buttons back, and the Gear Planner's star numbers no longer cover the icons.
## Getting around
- The big section buttons (and the icons on the right) always take you to that section's main page, even when you are
  already on one of its other pages: Options goes back to Options › Folders from Cloud Save or the language list,
  Stats to its first page for every class, Addons to your installed addons, and so on.
- The path at the top: clicking a step's name takes you straight back there. Its list of places beside it opens when
  you point at it or click its ▾.
- "Lost?" only shows when you are on one of a section's other pages, never on its main page (Community's included).
- Message boxes inside the launcher (Lost? and the others) no longer have a black box around them: just the box, over
  the dimmed launcher.

## Options
- Folders: every folder has an OPEN LOCATION button (opens it in Explorer) and a CHANGE FOLDER button beside it again,
  and so does the cloud sync folder. The menu in the path is now called Change Folder ▾ (importing a save is there).

## Gear Planner and Talent Tree Builder
- Gear Planner: a piece's T1 to T5 sits just outside its box, by the top right corner, so it no longer covers the icon.
- Talent Tree Builder: no GEAR PLANNER button above the trees (the path at the top leads there).

# v1.8 (2026-10-27)
In short: The first big part of the Major Update: a new first-time setup that can't be skipped, PLAY waits until setup is done, the back arrow follows the way you came, Guide search works as you type, the Gear Planner gets clickable stars and Single Item / Tier Bonus, and Options and Stats lose their rows of tabs.
This is a very large, but scattered update: it touches many different parts of the launcher, from the first-time setup to
the Guide and the Gear Planner, rather than one big feature. Taken together it is a big step up in how easy and simple
the launcher is to use.

## Gear Planner: stars
- Click a piece on the character sheet to add a star: each click adds one, up to 5, and the next click goes back to 0.
  A small number on the piece shows its stars. The star buttons above the list are gone. Right-click still takes the
  piece off, and a newly picked piece starts at 0 stars.
- Weapons take stars too (all pieces go up to 5).
- Stars decide which set bonuses you reach (4-piece at 1+ stars, 8-piece at 3+). How much each star adds to a piece,
  and what it costs, is not known yet, so the planner's numbers never count it (no more "waiting for clarification").
- AZS Handbook (in game): the same: click a piece under Slots to add a star, its stars shown as [3].

## Guide
- Gear & the Armory: Marks and Stars rewritten from the patch notes. Every boss you kill pays your class marks (half on
  normal, more in harder zones, on higher Heroic levels and for returning endless bosses), a duplicate pays marks when it
  drops, marks are paid the moment the boss dies (even if you fall later), and clearing a zone pays nothing extra. Each
  Heroic level opens the next star (Heroic III also opens the 8-piece bonuses), weapons go to 5 stars too, and the
  patch notes' numbers are listed (the legendary sets, the patch notes' "tier 3", 60% stronger at 5 stars, lower sets 95% of it, last star 30 marks, 5% cheaper
  per other class up to 40%), with what is not known yet.

## Game update 0.122.0
- News and Changelog: the patch notes for 0.122.0 (co-op lag, lobby difficulty and its filter, Heroic clears in co-op,
  Sanguine, Fire Nova, resumed downloads, Again after a run).
- Guide > Co-op: lobbies show their difficulty, the Difficulty filter and "Can join", hosting with Change difficulty,
  everyone seeing the host's zone and difficulty at once (a hero who has not opened them stays behind), and a Heroic
  clear counting the moment the final boss falls.
- Guide > Heroic & Endless: Sanguine pools heal only the horde, not bosses or lair guardians.

## Addons
- New in Download More under UI: AetherHUD by Rehabrocket, a complete interface (run clock with the boss timeline, boss
  bars, enemy plates, unit frames, skill bar, combat meter, ring minimap). Like the other interfaces it is pick-one:
  switching it on switches Survivor UI, SurvivorsDragonUi, Keijin UI, Bordo UI or BlackwellUI off.

## First-time setup (Major Update)
- Setup no longer opens with "Welcome, Adventurer": each page sits at the top of the window like every other section,
  with "Step X of 4" in the top right corner.
- Setup can't be closed or left for later until it is done: no LATER button, and the window's close button waits.
  Other sections stay closed while it runs.
- New order: language, your save, cloud and folders, then AZS Stats, then the other addons.
- Folders that aren't there yet say "Created if not found" and are created when setup is done (no more "not found -
  please choose it" warning and "Finish anyway?" question for the game's own folders).
- When the game isn't on the PC yet, setup ends with a page that says it will download the game, with DOWNLOAD THE GAME
  and CANCEL SETUP. Cancelling setup, or stopping that download, shows a last page that explains what the launcher is
  for, with START SETUP AGAIN.
- PLAY is grayed out until setup is done; without the game it reads REQUIRES INSTALL.

## Everything else (Major Update)
- After the game closes, the launcher notices much sooner: it waits 3 seconds for the game to come back (45 seconds
  after the game updated itself) instead of 10 and 90, without the extra pauses, and it checks for the game far more
  cheaply. While your progress is still uploading, RUN SETUP AGAIN and INSTALL say so instead of "Close the game first".
- The back arrow goes back along the way you came (the Guide page, Options tab or Stats tab you were on), not just one
  level up.
- A click on a word in the path at the top opens its list right there, where the hover list opens (it used to open at
  the bottom right).
- No more NEXT button at the end of Guide pages: hover or click a step of the path at the top to go to any other page or
  topic. The Changelog keeps its PREVIOUS and NEXT.
- Options > GRAPHICS no longer starts with "The game's own settings. They take effect the next time the game starts."
- Guide search: typing no longer jumps to a page at the first letter. A list of suggestions opens under the box as you
  type (topics, pages and the Guide's spells, items, zones and races); click one to go there, or press Enter (or pick
  the last line) to see every page your words are found in.
- Options and STATS have no row of tabs any more: the tab is a step of the path at the top (Options › Folders,
  Stats › Priest › Records). Hover or click it for the list of the other tabs. The page gets that room.
- Guide > Classes: a class page no longer lists its gear piece by piece. A button (PRIEST GEAR ›) opens the class's gear
  page instead: every piece with its bonuses, the bosses that drop it and each set's bonuses.
- Guide > Gear & the Armory: a new "Each class's gear" line links straight to each class's gear page.
- Cancelling a first-time setup now takes away everything the launcher made: its own folder (settings, log, the
  unfinished download) and the folders setup created while they are still empty, so only the exe is left. The game, a
  save imported into it and your cloud files are never touched. RUN SETUP AGAIN can't lose your settings this way:
  cancelling it just goes back with everything as it was.
- With no language chosen yet, the launcher starts in English (setup asks for the language first).
- Gear Planner and Talent Tree Builder fit their box: no scroll bar. The planner's slots and the talent trees' icons are
  sized to the room there is, and the planner's set bonuses sit under its totals.

## Game update 0.123.0
- Patch notes for the game's 0.123.0 update: co-op holds together on a laggy connection (enemies no longer freeze and
  jump, no more teleporting back, level-up cards no longer come back or pick the wrong card).

## Major Update, part 1
- A "?" beside the window's X explains the path at the top: why there are no rows of buttons (to give pages more room)
  and how the path does their job.
- Class pages: the class's GEAR button now sits at the top, beside TALENT TREES.
- Gear Planner: a piece's stars show as T1 to T5 in its top right corner (in the AZS Handbook too).
- Gear Planner and Talent Tree Builder look sharper: their icons are drawn at a size that keeps them crisp.
- Setup closes any message box that was open when it starts.

## Major Update, part 2
- Gear Planner: two buttons sit where the slot's header was, Single Item and Tier Bonus. Single Item shows the chosen
  slot with the name of the piece it holds, then everything it can hold. Tier Bonus lists every rarity (Common to
  Legendary) with 4 piece and 8 piece side by side: one click puts that set on (4 pieces at T1, or all 8 at T3).
- Guide > Classes: clicking a set chip under a spell ("Frostfire 8-piece") opens the Gear Planner with that whole set on.
- Addons > Download More: no row of category tabs; the category is a step of the path (Addons › Download More › UI ▾).
- Options > Launcher: Language, About Launcher and Run setup again are places in the path (Options › Launcher ›
  Settings ▾) instead of a row of buttons.
- Options > Folders: no CHANGE buttons; click a folder to change it, or use Change ▾ in the path, where importing a
  save is too.

## Major Update, part 3
- STATS > History: clicking a run no longer makes a picture for Discord. It opens a short post-game summary of that run
  instead: its result, time, level, kills and DPS, what it fought and earned, and a few fun highlights (kills a minute,
  biggest crit and heal, most kills in 5 seconds, the spells it leaned on, the spell that did most of the damage, how it
  compares with your other runs of that class, and who killed you).
- About Launcher and CREDITS.md thank the people who peer reviewed this work: Keijin, Kraatos, Maidenhead and the
  Discord's Mod team.

## Major Update, part 4
- Guide > Gameplay: a new Transmog topic with every look that adds nothing: the 14 Appearances (head and back, each
  with the achievement that brings it), the Tabards (earned and bought, with their price) and the Weapon Glows, which
  move there from Gear & the Armory. They come from a small database of looks (assets/transmog.txt).
- AZS Minor Mods: the plain action bar is no wider than its six squares and the two page arrows, so it leaves no empty
  space. New: cooldown numbers on the spell squares, the seconds until each spell is ready again (on by default).

## Major Update, part 5
- README: six new screenshots of the finished launcher, each showing something different (home and Community, STATS,
  the Talent Tree Builder, the Gear Planner, a class's gear page and the path at the top).
- Gear Planner: its note under the totals is shorter, which leaves more room for the set bonuses.

## More fixes and requests
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
  icons of the gear your heroes wear for the launcher, and clears what older versions wrote down. Gear icons come from a
  new list instead (below).
- Gear icons: every piece of gear that is a real World of Warcraft item (195 of the 470) shows its real icon, from a
  list made from the CMaNGOS Classic database and Wowhead's item data (assets/item-icons.txt). Pieces the game made up
  say "Image soon" in place of a picture. In the Gear Planner, STATS > Armory, and in game in the AZS Handbook (which
  uses the game's own icon files).
- Guide > Transmog: hover a look that is a real World of Warcraft item (22 of them: the Defias Bandana shows the
  Red Defias Mask and the Zul'Gurub Mask the Bad Mojo Mask) to see its 3D model picture from
  Wowhead's model viewer, beside how to earn it.
- AZS Minor Mods: new Mark minibosses (on by default). Each zone's bosses before its final boss (as the Guide's Zones &
  Bosses lists them) get a Star, a Triangle and a Diamond, so the Skull and Cross are left for the final boss. Boss
  markers are now Skull, Cross and Diamond (no more Moon).
- AZS Minor Mods: a friend who falls blinks their own marker 15 times, on and off: Square, Moon or Circle, one each,
  so several friends can be down at once. Those three are never put on a boss.

# v1.7 (2026-10-27)
In short: Ready for the game's Heroic update: the Guide explains Heroic I to V, marks, stars and set bonuses, the Gear Planner shows stars and set bonuses, and AZS Stats tells you which spells each run leaned on.
## Guide
- Class pages no longer end with a NEXT button: hover the class's name in the path at the top to go to another class.
## News and Changelog
- Patch notes for the game's 0.121.0 update: Heroic I to V, marks, star upgrades, 4- and 8-piece set bonuses.

## Game update 0.121.0 (Heroic and gear upgrades)
- Guide: the Run Options topic is now Heroic & Endless: Heroic I to V and how each level opens, the weekly horde
  affixes (Bolstering, Sanguine, Explosive), and the marks that returning endless bosses pay.
- Guide > Gear & the Armory: new Marks, Stars, Set bonuses (4-piece, 8-piece, the PvP sets' crowd control) and Weapon
  glows sections. Boss gear explains bad luck protection, and that a piece you already own now pays marks.
- Guide > Zones & Bosses: bosses aim ahead of where you are running, more so on each Heroic level.
- Guide > Talents, Achievements and Co-op: talent experience rises with the Heroic level, the new Heroic achievements,
  Against the Odds asking for Heroic III, the host choosing the Heroic level, and co-op scaling 20% less per hero.
- Drop chances in the Items topic are marked as normal-difficulty chances.
- AZS Handbook (in game): the same Guide changes.
- AZS Stats (in game): every run now keeps the game's run modifiers, so Heroic runs can be told apart later.

## Set bonuses and stars
- Guide > Items: every class's gear now ends each rarity with its set's 4-piece and 8-piece bonuses, as the game's
  armory words them, for all nine classes (54 sets). Class pages show them under each rarity's pieces too, and the
  spells named in them are highlighted.
- Guide > Classes: a spell's box now also shows "Set bonuses that change it": the sets whose 8-piece bonus changes that
  spell (Fireball: Frostfire 8-piece), in the set's colour. Point at one to read the bonus.
- Gear Planner: pick 0 to 5 stars for each armor piece (the buttons above the list). Stars decide which set bonuses you
  reach (4-piece at 1+ stars, 8-piece at 3+). What a star adds to a piece's own numbers is waiting for clarification
  from the game's developer, so the totals count every piece at 0 stars for now; weapons have no stars yet for the same
  reason. Everything is ready for those numbers.
- Gear Planner: the Set bonuses list now has its own room under the weapons. Four pieces of one set and four of another
  show and count both 4-piece bonuses, and a full set shows its 4-piece next to its 8-piece; a "Reached" line sums up
  which bonuses you have.
- Item cards (Guide hovers and the planner) show the piece's set with its 4-piece and 8-piece bonuses.
- AZS Handbook (in game): the same star buttons, totals, Set bonuses list, set lines on class pages and "Set bonuses
  that change it" on spells.
- Guide: "Mr. Smite" and "High Priest Venoxis" (and every other boss's name) no longer link to Smite or the Priest
  class; in the in-game Handbook, Smite in Mr. Smite is no longer highlighted as the spell.
- Guide > Gear & the Armory: the 4-piece bonuses, mixing two sets, and which PvP sets are built around crowd control
  (the warrior's is built around Mortal Strike and the shaman's around Earth Shock).

## AZS Stats: spell focus
- At the end of a run, AZS Stats says in chat which spells the run leaned on: each spell's share of the ranks you put
  into your spells ("Spell focus (12 ranks): Fireball 42% · Frost Nova 25% ...").
- History (in game and in the launcher) lists each run's spells with their rank and that share, the biggest first.
- Abilities (in game and in the launcher): each spell also shows its share of every rank you have put into spells, so
  you can see which spell you favour over time.

# v1.6 (2026-10-27)
In short: Small fixes: RUN SETUP AGAIN starts over properly, setup pages scroll instead of running off the edge, and a few buttons no longer spill past their box.
## First-time setup
- RUN SETUP AGAIN (Options > LAUNCHER) now asks everything from the start, as if nothing had been set up: language
  first, every folder at its usual place, cloud saving on with no folder chosen yet. What you had stays until you
  press FINISH, and leaving setup early changes nothing.
- Every setup page stays inside the section window: a long page (or a long language) scrolls instead of running past
  the edges.

## Everything else
- Addons > My Addons: the profiles' LOAD buttons no longer run past the right edge.
- The path at the top of Options shows the tab's name in your language (Folders, Graphics).
- Options > GRAPHICS: the window size buttons wrap onto a second line instead of running past the right edge, and
  every Options tab keeps its contents inside the box.
- About Launcher no longer lists AZS Transparency, which was removed in v1.0.

# v1.5 (2026-10-27)
In short: Every class's gear now has its real bonuses, rogues and shamans get their Off Hand, the in-game AZS Handbook has everything the launcher's Guide has, addons made of several folders switch on and off as one, and first-time setup moved inside the launcher.
## News and Changelog
- Patch notes for the game's 0.120.5 update: co-op works on networks that blocked the co-op server, and an unreachable
  server is reported as such instead of "the host left".

## Gear Planner and Items
- Every Priest piece now has its bonuses: the Common (Acolyte's), Uncommon (Devout), Rare (Proph…67067 tokens truncated…s they are, kept under the one left
                profiles[from] = now;
                AddonProfiles.Store(ss, from);
                if (AddonProfiles.Has(to)) AddonProfiles.Restore(ss, to);
            }
            catch (Exception e)
            {
                Msg.Show(side, e.Message, "Addons");
                return false;
            }
            Cfg.Set("addon_profile_active", to);
            Cfg.Save();
            return true;
        }

        // the picked one's own switches, where it has some (unknown, newer addons are left as they are)
        int ApplyAddonSwitches(string slot, List<string> errors, List<string> unknown)
        {
            var prof = Json.AsDict(Json.Get(Cfg.Section("addon_profiles"), slot));
            return prof == null ? 0 : Addons.ApplyProfile(AddonsDir(), prof, errors, unknown);
        }

        void UseNoAddonProfile()
        {
            if (AddonsLocked()) return;
            if (ActiveAddonProfile() == "none") { SetStatus(Lang.T("No profile in use: the game's preferences are shared again.")); return; }
            if (!SwitchAddonPreferences("none")) return;
            var errors = new List<string>();
            try { ApplyAddonSwitches("none", errors, new List<string>()); }
            catch (Exception e) { errors.Add(e.Message); }
            SetStatus(Lang.T("No profile in use: the game's preferences are shared again."));
            BuildPanel("Addons");
            if (errors.Count > 0)
                Msg.Show(side, "Some addons could not be changed:\n\n" + string.Join("\n", errors), "Addons",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void SaveAddonProfile(string slot)
        {
            var profiles = Cfg.Section("addon_profiles");
            if (profiles.ContainsKey(slot) && Msg.Show(side, "Replace the saved Profile " + slot + " with the current toggles?",
                                                              "Addons", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { profiles[slot] = Addons.MakeProfile(Addons.Scan(AddonsDir())); }
            catch (Exception e)
            {
                Msg.Show(side, e.Message, "Addons");
                return;
            }
            Cfg.Save();
            SetStatus("Profile " + slot + " saved.");
            BuildPanel("Addons");
        }

        void LoadAddonProfile(string slot)
        {
            if (AddonsLocked()) return;
            if (!SwitchAddonPreferences(slot)) return;      // this profile's own game preferences first, then its switches
            var errors = new List<string>();
            var unknown = new List<string>();
            int changed;
            try { changed = ApplyAddonSwitches(slot, errors, unknown); }
            catch (Exception e)
            {
                Msg.Show(side, e.Message, "Addons");
                return;
            }
            string note = "Profile " + slot + " loaded: " + changed + " changed.";
            if (unknown.Count > 0) note += " " + unknown.Count + " newer addon(s) left as they were.";
            SetStatus(note);
            BuildPanel("Addons");
            if (errors.Count > 0)
                Msg.Show(side, "Some addons could not be changed:\n\n" + string.Join("\n", errors), "Addons",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ------------------------------------------------------------------ Cloud Save
        // The last 5 cloud downloads are kept as ZIPs on this PC. Nothing uses them on its own: this menu is the only
        // way back to one, for when the save is broken and the cloud copy cannot fix it.
        void ShowKeptDownloads(Control anchor)
        {
            List<SyncEngine.KeptDownload> kept;
            try { kept = new SyncEngine(SyncSettings()).KeptDownloads(); }
            catch (Exception) { kept = new List<SyncEngine.KeptDownload>(); }
            var menu = new ContextMenuStrip();
            menu.Font = Theme.F(9, FontStyle.Regular);
            var head = new ToolStripMenuItem(Lang.T("Restore a download kept on this PC (last resort):"));
            head.Enabled = false;
            menu.Items.Add(head);
            if (kept.Count == 0)
            {
                var none = new ToolStripMenuItem(Lang.T("No downloads kept yet."));
                none.Enabled = false;
                menu.Items.Add(none);
            }
            foreach (var k in kept)
            {
                var areas = new List<string>();
                foreach (var a in k.Areas) areas.Add(Lang.T(a.Key.Label));
                string stamp = k.Stamp;
                menu.Items.Add(new ToolStripMenuItem(k.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "   (" +
                                                     string.Join(", ", areas) + ")", null,
                                                     delegate { RunSync("restore", false, false, stamp); }));
            }
            if (anchor != null && !anchor.IsDisposed) menu.Show(anchor, new Point(0, anchor.Height));
            else menu.Show(Cursor.Position);
        }

        void PanelCloud()
        {
            ClearPanel("Cloud Save");
            SetCrumbs("Options", (Action)delegate { optionsTab = 1; optionsLang = false; ShowSection("Options"); }, "Cloud Save", null);
            if (!CloudEnabled())
            {
                var t = PutText("Cloud saving is turned off.\nYour progress stays on this PC only.", 12, FontStyle.Regular,
                                Theme.Text, 20, 8, 0);
                Put(new WowButton("TURN ON CLOUD SAVING", delegate { SetCloudEnabled(true); }, 290, 38, 11), 20, BottomOf(t) + 12);
                return;
            }
            var intro = PutText("PLAY downloads the newest state, starts the game and uploads your progress after you close it. " +
                                "One complete state wins; the overwritten one is backed up as a ZIP first.", 10, FontStyle.Regular,
                                Theme.Text, 20, 8, Math.Min(570, PW - 40));
            int y = BottomOf(intro) + 8;
            var lab = PutText("Sync folder", 11, FontStyle.Bold, Theme.Gold, 20, y, 0);
            string cloud = Cfg.Str("cloud_dir");
            var path = PutText(cloud.Length > 0 ? cloud : Lang.T("(not set - choose a folder inside Google Drive, OneDrive or Dropbox)"),
                               9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), 420, false);
            int rowH = BottomOf(path) - y;
            Put(new WowButton("CHOOSE", ChooseCloudDir, 100, 30, 10), PW - 20 - 100, y + Math.Max(0, (rowH - 30) / 2));
            string info = "Not synced yet.";
            try { if (cloud.Length > 0 && Directory.Exists(cloud)) info = "Cloud: " + new SyncEngine(SyncSettings()).CloudInfo(); }
            catch (Exception) { }
            var infoL = PutText(info, 9, FontStyle.Italic, Theme.GoldDim, 20, Math.Max(BottomOf(path), y + 30) + 2, PW - 20 - 170 - 30);
            // last resort: put back one of the last 5 downloads kept on this PC (small, out of the way)
            WowButton keptBtn = null;
            keptBtn = new WowButton("EARLIER DOWNLOADS", delegate { ShowKeptDownloads(keptBtn); }, 170, 24, 8);
            Put(keptBtn, PW - 20 - 170, infoL.Top - 3);
            y = Math.Max(BottomOf(infoL), infoL.Top + 21) + 4;
            Put(new WowButton("CHECK STATUS", delegate { RunSync("status", false, false); }, 170, 34, 10), 20, y);
            Put(new WowButton("DOWNLOAD", delegate { RunSync("pull", true, false); }, 170, 34, 10), 20 + 182, y);
            Put(new WowButton("UPLOAD", delegate { RunSync("push", true, false); }, 170, 34, 10), 20 + 364, y);
            y += 34 + 8;
            var frame = new Bordered(Theme.Bronze, 1);
            frame.Bounds = new Rectangle(20, y, PW - 40, PH - 12 - y);
            Put(frame, 20, y);
            var box = UI.Log(Theme.Ink, 9);
            box.Dock = DockStyle.Fill;
            frame.Controls.Add(box);
            lock (cloudLog)
            {
                int from = Math.Max(0, cloudLog.Count - 200);
                for (int i = from; i < cloudLog.Count; i++) UI.Append(box, cloudLog[i].Key + "\n", UI.LevelColor(cloudLog[i].Value), null);
            }
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            logBox = box;
        }

        // ------------------------------------------------------------------ Options
        // Options: a bar of tabs across the top (beside the back arrow) and one short page per tab
        // FOLDERS, CLOUD SAVE (its settings and its page), GRAPHICS and GAMEPLAY (the game's own settings), LAUNCHER (how
        // the launcher behaves, its language, About, setup)
        static readonly string[] OptionTabs = { "FOLDERS", "CLOUD SAVE", "GRAPHICS", "GAMEPLAY", "LAUNCHER" };
        int optionsTab;
        bool optionsLang;                                     // Options › Launcher › Language: the list of languages
        bool optionsAbout;                                    // Options › Launcher › About Launcher: the launcher's overview

        ScrollPanel optionsPage;                             // the page shown, so a rebuild of the same tab keeps its place
        int optionsPageTab = -1;

        void PanelOptions()
        {
            // a setting changed on this tab rebuilds it: it stays where it was scrolled to
            int keep = optionsPage != null && !optionsPage.IsDisposed && optionsPageTab == optionsTab ? -optionsPage.AutoScrollPosition.Y : 0;
            ClearPanel("Options");
            // no row of tabs: the tab is a step of the path at the top (Options › Folders ▾), its list the other tabs
            int top0 = 4;
            Action home = delegate { optionsLang = false; optionsAbout = false; optionsTab = 0; BuildPanel("Options"); };
            if (optionsAbout)
            {
                SetCrumbs("Options", home, "Launcher", (Action)delegate { optionsAbout = false; optionsLang = false; optionsTab = 4; BuildPanel("Options"); }, "About Launcher", null);
                SetCrumbMenu(1, OptionTabMenu());
            }
            else if (optionsLang)                                 // Options › Launcher › Language ▾ : the languages themselves
            {
                // (no list of languages here: a language is only picked on the page itself, never by passing over the path)
                SetCrumbs("Options", home, "Launcher", (Action)delegate { optionsLang = false; optionsTab = 4; BuildPanel("Options"); }, "Language", null);
                SetCrumbMenu(1, OptionTabMenu());
            }
            else
            {
                SetCrumbs("Options", home, OptionTabName(optionsTab), null);
                SetCrumbMenu(1, OptionTabMenu());
            }
            var sp = MakeScroll(panel, new Rectangle(8, top0, PW - 16, PH - 8 - top0), Theme.Panel, Theme.Panel);
            int pageW = sp.ClientSize.Width - 6;              // the page's width: what shows inside the scroll box, beside its bar
            putInto = sp;
            try
            {
                int end;
                if (optionsAbout) end = OptionsAbout(pageW);
                else if (optionsLang) end = OptionsLanguage(pageW);
                else if (optionsTab == 0) end = OptionsFolders(pageW);
                else if (optionsTab == 1) end = OptionsCloud(pageW);
                else if (optionsTab == 2) end = OptionsGame(pageW, new[] { "DISPLAY", "GRAPHICS" });
                else if (optionsTab == 3) end = OptionsGame(pageW, new[] { "GAMEPLAY", "SOUND" });
                else end = OptionsLauncher(pageW);
                var pad = new Panel();
                pad.BackColor = Theme.Panel;
                pad.Bounds = new Rectangle(0, end, 1, 12);
                sp.Controls.Add(pad);
            }
            finally { putInto = null; }
            optionsPage = sp;
            optionsPageTab = optionsTab;
            if (keep > 0) sp.AutoScrollPosition = new Point(0, keep);
        }

        static string OptionTabName(int i)                 // "CLOUD SAVE" -> "Cloud Save", for the trail
        {
            string[] names = { "Folders", "Cloud Save", "Graphics", "Gameplay", "Launcher" };
            return i >= 0 && i < names.Length ? names[i] : "Options";
        }

        List<KeyValuePair<string, Action>> OptionTabMenu()
        {
            var l = new List<KeyValuePair<string, Action>>();
            for (int i = 0; i < OptionTabs.Length; i++)
            {
                int n = i;
                l.Add(new KeyValuePair<string, Action>(OptionTabName(i), delegate { optionsTab = n; optionsLang = false; BuildPanel("Options"); }));
            }
            return l;
        }

        int OptionCheck(string text, bool value, int y, Action<bool> changed)
        {
            var cb = UI.Check(text, value, Theme.Text, Theme.Panel, 11);
            cb.CheckedChanged += delegate { bool on = cb.Checked; BeginInvoke(new Action(delegate { changed(on); })); };
            cb.Size = VStack.Measure(cb);
            Put(cb, 18, y);
            return y + cb.Height + 4;
        }

        // FOLDERS: where the game, its saves, its config and the cloud copy are, and cloud saving on or off
        int OptionsFolders(int PW)
        {
            SyncSettings ss = SyncSettings();
            var rows = new List<object[]>();
            rows.Add(new object[] { "Game folder", GameDir(), new Action(ChooseGameFolderButton) });
            rows.Add(new object[] { "Save game folder", ss.SaveDir, new Action(ChooseSaveDir) });
            rows.Add(new object[] { "Game config folder (AddOns, addon settings)", ss.ConfigDir, new Action(ChooseConfigDir) });
            int y = 6;
            // each folder: OPEN LOCATION (in Explorer) and CHANGE FOLDER beside it; IMPORT A SAVE under them
            int bw = (int)(140 * Theme.Zoom), gap = (int)(8 * Theme.Zoom), room = PW - 40 - 2 * bw - gap - 10;
            foreach (var r in rows)
            {
                var lab = PutText((string)r[0], 11, FontStyle.Bold, Theme.Gold, 20, y, room);
                string v = (string)r[1];
                var val = PutText(string.IsNullOrEmpty(v) ? Lang.T("(not set)") : v, 9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), room, false);
                Action act = (Action)r[2];
                int by = y + Math.Max(0, (BottomOf(val) - y - (int)(28 * Theme.Zoom)) / 2);
                Put(new WowButton("CHANGE FOLDER", act, 140, 28, 8.5f), PW - 20 - bw, by);
                var open = new WowButton("OPEN LOCATION", delegate { OpenLocation(v); }, 140, 28, 8.5f);
                open.GrayedTip = "That folder does not exist.";
                open.Grayed = string.IsNullOrEmpty(v) || !Directory.Exists(v);
                Put(open, PW - 20 - 2 * bw - gap, by);
                y = Math.Max(BottomOf(val), by + (int)(28 * Theme.Zoom)) + 10;
            }
            var imp = PutText("Import a save", 11, FontStyle.Bold, Theme.Gold, 20, y + 4, PW - 40 - bw - 10);
            var impNote = PutText("Bring in a save from elsewhere (a backup, another PC) into the save game folder.", 9, FontStyle.Italic,
                                  Theme.GoldDim, 20, BottomOf(imp), PW - 40 - bw - 10);
            Put(new WowButton("IMPORT A SAVE", delegate { if (ImportSave(side, SyncSettings().SaveDir) != null) SetStatus("Save imported."); }, 140, 28, 8.5f),
                PW - 20 - bw, y + 4);
            return Math.Max(BottomOf(impNote), y + 4 + (int)(28 * Theme.Zoom)) + 8;
        }

        // a folder in Explorer
        void OpenLocation(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { SetStatus("That folder does not exist."); return; }
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\""); }
            catch (Exception) { SetStatus("That folder does not exist."); }
        }

        // CLOUD SAVE: cloud saving on or off, its sync folder, and its page (sync now, the log, earlier downloads)
        int OptionsCloud(int PW)
        {
            int y = 6;
            y = OptionCheck("Cloud saving", CloudEnabled(), y, delegate(bool on) { SetCloudEnabled(on); });
            if (!CloudEnabled())
            {
                var off = PutText("Turn cloud saving on to keep your save and your addons' settings in Google Drive, OneDrive or Dropbox.",
                                  9.5f, FontStyle.Italic, Theme.GoldDim, 20, y + 2, PW - 40);
                return BottomOf(off);
            }
            y += 6;
            int cbw = (int)(140 * Theme.Zoom), cgap = (int)(8 * Theme.Zoom);
            var lab = PutText("Sync folder (inside your cloud drive)", 11, FontStyle.Bold, Theme.Gold, 20, y, PW - 50 - 2 * cbw - cgap);
            string v = Cfg.Str("cloud_dir");
            var val = PutText(string.IsNullOrEmpty(v) ? Lang.T("(not set)") : v, 9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), PW - 50 - 2 * cbw - cgap, false);
            int cby = y + Math.Max(0, (BottomOf(val) - y - 28) / 2);
            Put(new WowButton("CHANGE FOLDER", ChooseCloudDir, 140, 28, 8.5f), PW - 20 - cbw, cby);
            var openCloud = new WowButton("OPEN LOCATION", delegate { OpenLocation(v); }, 140, 28, 8.5f);
            openCloud.GrayedTip = "That folder does not exist.";
            openCloud.Grayed = string.IsNullOrEmpty(v) || !Directory.Exists(v);
            Put(openCloud, PW - 20 - 2 * cbw - cgap, cby);
            y = Math.Max(BottomOf(val), y + 30) + 6;
            if (!GateOpen())                                  // syncing needs the game installed
            {
                var need = PutText(CloudNeedsGame, 9.5f, FontStyle.Italic, Theme.GoldDim, 20, y, PW - 40);
                return BottomOf(need) + 6;
            }
            // what was the Cloud Save page: the state in the cloud, sync now, earlier downloads and the log
            var intro = PutText("PLAY downloads the newest state, starts the game and uploads your progress after you close it. " +
                                "One complete state wins; the overwritten one is backed up as a ZIP first.", 9.5f, FontStyle.Regular,
                                Theme.Text, 20, y, PW - 40);
            y = BottomOf(intro) + 6;
            string info = "Not synced yet.";
            try { if (v.Length > 0 && Directory.Exists(v)) info = "Cloud: " + new SyncEngine(SyncSettings()).CloudInfo(); }
            catch (Exception) { }
            var infoL = PutText(info, 9, FontStyle.Italic, Theme.GoldDim, 20, y + 4, PW - 20 - 170 - 30);
            WowButton keptBtn = null;
            keptBtn = new WowButton("EARLIER DOWNLOADS", delegate { ShowKeptDownloads(keptBtn); }, 170, 24, 8);
            Put(keptBtn, PW - 20 - (int)(170 * Theme.Zoom), y);
            y = Math.Max(BottomOf(infoL), y + (int)(24 * Theme.Zoom)) + 8;
            int bw = (int)(170 * Theme.Zoom) + 12;
            Put(new WowButton("CHECK STATUS", delegate { RunSync("status", false, false); }, 170, 34, 10), 20, y);
            Put(new WowButton("DOWNLOAD", delegate { RunSync("pull", true, false); }, 170, 34, 10), 20 + bw, y);
            Put(new WowButton("UPLOAD", delegate { RunSync("push", true, false); }, 170, 34, 10), 20 + 2 * bw, y);
            y += (int)(34 * Theme.Zoom) + 8;
            var frame = new Bordered(Theme.Bronze, 1);
            // the log fills what is left of the page, so the Cloud Save tab needs no scroll bar
            int logH = Math.Max(70, PH - 12 - y - 22);
            frame.Bounds = new Rectangle(20, y, PW - 40, logH);
            Put(frame, 20, y);
            var box = UI.Log(Theme.Ink, 9);
            box.Dock = DockStyle.Fill;
            frame.Controls.Add(box);
            lock (cloudLog)
            {
                int from = Math.Max(0, cloudLog.Count - 200);
                for (int i = from; i < cloudLog.Count; i++) UI.Append(box, cloudLog[i].Key + "\n", UI.LevelColor(cloudLog[i].Value), null);
            }
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            logBox = box;
            return frame.Bottom + 6;
        }

        // GAME: the game's own settings (display mode, window size, graphics, sound), written into its config.toml;
        // the game reads them when it starts
        int OptionsGame(int PW, string[] groups)
        {
            string file = GameSettings.FileIn(SyncSettings().ConfigDir);
            var now = GameSettings.Read(file);
            bool running = GameRunningFlag || Util.GameRunning();
            int y = 6;
            if (running)                                     // only while the game runs: its settings can't be changed then
            {
                var intro = PutText("The game is running: close it first, it saves its own settings when it closes.",
                                    9.5f, FontStyle.Italic, Theme.Hex("#e07050"), 20, y, PW - 40);
                y = BottomOf(intro) + 8;
            }
            if (file == null)
            {
                var none = PutText("The game config folder is not set. Set it in Options > FOLDERS.", 10, FontStyle.Regular, Theme.Text, 20, y, PW - 40);
                return BottomOf(none);
            }
            Action<GameSetting, string> set = delegate(GameSetting gs, string v)
            {
                if (GameRunningFlag || Util.GameRunning()) { SetStatus("Close the game first: it saves its own settings when it closes."); return; }
                try
                {
                    GameSettings.Set(file, gs.Cvar, v);
                    SetStatus(Lang.T("Game setting saved:") + " " + Lang.T(gs.Label));
                }
                catch (UserError e) { SetStatus(e.Message); }
                catch (Exception e) { SetStatus(Lang.T("Could not save the game setting:") + " " + e.Message); }
                BuildPanel("Options");
            };
            string group = null;
            foreach (GameSetting gs in GameSettings.All)
            {
                if (Array.IndexOf(groups, gs.Group) < 0) continue;   // GRAPHICS: display and graphics; GAMEPLAY: gameplay and sound
                string cur = GameSettings.Value(now, gs);
                GameSetting g = gs;
                if (gs.Group != group)                       // a heading for each group: DISPLAY, GRAPHICS, SOUND, GAMEPLAY
                {
                    group = gs.Group;
                    y += 6;
                    var gh = PutText(group, 13, FontStyle.Bold, Theme.Gold, 20, y, 0);
                    Put(UI.Line(Theme.Bronze, PW - 40, 2), 20, BottomOf(gh) + 2);
                    y = BottomOf(gh) + 10;
                }
                if (gs.Check)
                {
                    y = OptionCheck(gs.Label, cur == "1", y, delegate(bool on) { set(g, on ? "1" : "0"); });
                    if (gs.Note != null)
                    {
                        var cn = PutText(gs.Note, 9.5f, FontStyle.Regular, Theme.Text, 44, y - 4, PW - 64);
                        y = BottomOf(cn) + 6;
                    }
                    continue;
                }
                var lab = PutText(gs.Label, 11.5f, FontStyle.Bold, Theme.Hex("#fff2b0"), 20, y, PW - 40);
                y = BottomOf(lab) + 1;
                if (gs.Note != null)
                {
                    var note = PutText(gs.Note, 9.5f, FontStyle.Regular, Theme.Text, 20, y, PW - 40);
                    y = BottomOf(note) + 4;
                }
                var values = new List<string>(gs.Values);
                var names = new List<string>(gs.Names);
                int at = values.FindIndex(delegate(string v) { return SameSetting(v, cur); });
                if (at < 0 && !string.IsNullOrEmpty(cur)) { values.Add(cur); names.Add(cur.Replace("x", "×")); at = values.Count - 1; }
                int n = values.Count, gap = 6;
                int bw = Math.Min(170, (int)(((PW - 40) - gap * (n - 1)) / (float)n / Theme.Zoom));
                int x = 20;
                for (int i = 0; i < n; i++)
                {
                    string v = values[i];
                    int need;                                // a long name gets the room it needs
                    using (var mg = CreateGraphics()) need = (int)(mg.MeasureString(Lang.T(names[i]), Theme.F(9)).Width / Theme.Zoom) + 20;
                    if (x > 20 && x + (int)(Math.Max(bw, need) * Theme.Zoom) > PW - 20) { x = 20; y += (int)(30 * Theme.Zoom) + 6; }
                    var b = new WowButton(names[i], delegate { set(g, v); }, Math.Max(bw, need), 30, 9);
                    b.Selected = i == at;
                    Put(b, x, y);
                    x += b.Width + gap;
                }
                y += (int)(30 * Theme.Zoom) + 14;
            }
            var where = PutText(Lang.T("Saved in") + " " + file, 8.5f, FontStyle.Regular, Theme.LinkBlue, 20, y, PW - 40, false);
            return BottomOf(where);
        }

        static bool SameSetting(string a, string b)
        {
            if (a == null || b == null) return false;
            double x, y;
            if (double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                return Math.Abs(x - y) < 0.001;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // the play time AZS Stats counts (at the end of LAUNCHER)
        int OptionsPlayTime(int PW, int y)
        {
            var head = PutText("Play time", 11, FontStyle.Bold, Theme.Gold, 20, y, 0);
            PlayStats ps = ShownPlayStats();
            long sessions = ps.Sessions;
            string stats = sessions == 0
                ? (Stats.AddonInstalled(AddonsDir()) ? "No play time counted yet. AZS Stats counts it while the game runs."
                                                      : "Play time is counted in game by the AZS Stats addon. Install it on the STATS page.")
                : "Total: " + Duration(ps.Seconds) + " over " + Util.Plural((int)sessions, "session") + "\n" +
                  "Counted in game by AZS Stats; cloud saving carries it to your other PCs.";
            var statsLabel = PutText(stats, 10, FontStyle.Regular, Theme.Text, 20, BottomOf(head) + 2, PW - 40);
            return BottomOf(statsLabel);
        }

        // LANGUAGE: every language built into the launcher, in its own name
        int OptionsLanguage(int PW)
        {
            var langs = Lang.Available();
            int y = 6, x = 20, col = 0, bw = 190, cols = Math.Max(1, (PW - 40) / (int)((bw + 10) * Theme.Zoom));
            foreach (var kv in langs)
            {
                string code = kv.Key;
                var b = new WowButton(kv.Value, delegate { SetLanguage(code); }, bw, 28, 9);
                b.Selected = code == Lang.Code;
                Put(b, x + col * (int)((bw + 10) * Theme.Zoom), y);
                if (++col >= cols) { col = 0; y += (int)(28 * Theme.Zoom) + 6; }
            }
            if (col > 0) y += (int)(28 * Theme.Zoom) + 6;
            return y;
        }

        // LAUNCHER: updating the launcher, About, setup again and installing the game from a file
        int OptionsLauncher(int PW)
        {
            int y = 6, bw = 200;
            Action<string, Action, string> row = delegate(string label, Action act, string note)
            {
                var b = Put(new WowButton(label, act, bw, 30, 9), 20, y);
                var t = PutText(note, 9.5f, FontStyle.Italic, Theme.GoldDim, 20 + b.Width + 12, y + 2, PW - 40 - b.Width - 12);
                y = Math.Max(BottomOf(b), BottomOf(t)) + 8;
            };
            // how the launcher behaves (was GENERAL)
            y = OptionCheck("Close button hides the launcher to the tray", Cfg.Bool("close_to_tray", true), y,
                            delegate(bool on) { Cfg.Set("close_to_tray", on); Cfg.Save(); });
            y = OptionCheck("Minimize the launcher when the game starts", Cfg.Bool("minimize_on_play", true), y,
                            delegate(bool on) { Cfg.Set("minimize_on_play", on); Cfg.Save(); });
            y = OptionCheck("Show the Stats section (your play time and run records)", StatsEnabled(), y,
                            delegate(bool on) { SetStatsEnabled(on); });
            y = OptionCheck("Show play time on the home screen", Cfg.Bool("home_playtime", true), y,
                            delegate(bool on) { Cfg.Set("home_playtime", on); Cfg.Save(); Invalidate(); });
            // Launcher is the end of the path (Options › Launcher): everything it holds is on this page, nothing opens a
            // step of its own. The language is picked from a list under its button; About is written out at the bottom.
            string langName = Lang.Code;
            foreach (var kv in Lang.Available()) if (kv.Key == Lang.Code) langName = kv.Value;
            y += 8;
            row("UPDATE LAUNCHER", delegate { CheckLauncherUpdate(true); }, "Looks for a newer launcher now and installs it (it also does this by itself at every start).");
            if (!string.IsNullOrEmpty(launcherUpdateMessage))
            {
                var message = PutText(launcherUpdateMessage, 9.5f, FontStyle.Regular, Theme.Text, 20, y, PW - 40);
                y = BottomOf(message) + 8;
            }
            WowButton langBtn = null;
            Action pickLang = delegate
            {
                var menu = new ContextMenuStrip();
                menu.Font = Theme.F(9.5f, FontStyle.Regular);
                foreach (var kv in Lang.Available())
                {
                    string code = kv.Key;
                    var it = new ToolStripMenuItem(kv.Value.Replace("&", "&&"), null, delegate { SetLanguage(code); });
                    it.Checked = code == Lang.Code;
                    menu.Items.Add(it);
                }
                if (langBtn != null) menu.Show(langBtn, new Point(0, langBtn.Height));
            };
            langBtn = Put(new WowButton(Lang.T("LANGUAGE") + " ▾", pickLang, bw, 30, 9), 20, y);
            var langNote = PutText(Lang.F("The launcher's language (now {0}).", langName), 9.5f, FontStyle.Italic, Theme.GoldDim,
                                   20 + langBtn.Width + 12, y + 2, PW - 40 - langBtn.Width - 12, false);
            y = Math.Max(BottomOf(langBtn), BottomOf(langNote)) + 8;
            row("RUN SETUP AGAIN", delegate { RunSetup(); }, "The first-time setup's steps again, with your settings as they are.");
            row(Lang.T("ABOUT LAUNCHER"), delegate { optionsAbout = true; optionsLang = false; BuildPanel("Options"); },
                "What the launcher does and how its parts fit together.");
            return y;                                                     // play time: on the home screen, under the Launcher buttons
        }

        int OptionsAbout(int PW)
        {
            int y = 8;
            foreach (string raw in Data.About.Split('\n'))
            {
                string ln = raw.TrimEnd();
                if (ln.Length == 0) continue;
                Control t;
                if (ln.StartsWith("#")) t = PutText(ln.TrimStart('#', ' '), 12, FontStyle.Bold, Theme.Gold, 20, y + 4, PW - 40, false);
                else if (ln.StartsWith("- ")) t = PutText("•  " + ln.Substring(2), 9.5f, FontStyle.Regular, Theme.Text, 30, y, PW - 50, false);
                else t = PutText(ln, 9.5f, FontStyle.Regular, Theme.Text, 20, y, PW - 40, false);
                y = BottomOf(t) + 3;
            }
            return y;
        }

        // the game folder: one that holds the game is the same as installing it (there is no "install from a file")
        void ChooseGameFolderButton() { ChooseGameFolder(); }

        bool ChooseGameFolder()
        {
            string d = AskFolder(this, "Select your Azeroth Survivors game folder (the one with " + Util.GameExe + ")", GameDir());
            if (d == null) return false;
            if (!File.Exists(Path.Combine(d, Util.GameExe)))
            {
                Warn("Game folder", Lang.T("That folder does not have the game in it") + " (" + Util.GameExe + ").");
                return false;
            }
            Cfg.Set("game_dir", d);
            Cfg.Set("installed_by_launcher", true);           // the player's own copy counts as installed
            Cfg.Save();
            SetStatus("Game folder set: " + d);
            RefreshPlayLabel();                              // PLAY/INSTALL and the Cloud Save button follow the new folder
            Invalidate();
            RebuildCurrent();
            return true;
        }

        void ChooseSaveDir()
        {
            string d = AskFolder(side, "Select the folder that holds your save game (normally %APPDATA%\\wenilla-rogue)", SyncSettings().SaveDir);
            if (d == null) return;
            Cfg.Set("save_dir", Path.GetFullPath(d));
            Cfg.Save();
            RebuildCurrent();
        }

        void ChooseConfigDir()
        {
            string d = AskFolder(side, "Select the game's benilla-config folder (contains AddOns and saved)", SyncSettings().ConfigDir);
            if (d == null) return;
            Cfg.Set("config_dir", Path.GetFullPath(d));
            Cfg.Remove("addons_dir");                        // AddOns and addon settings follow the config folder
            Cfg.Remove("saved_dir");
            Cfg.Save();
            RebuildCurrent();
        }
    }
}
