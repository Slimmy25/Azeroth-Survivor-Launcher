<!-- The next release's notes, written as changes are made. On release this file becomes v<version>.md (0.1 higher
     than the last release), its text goes to the top of assets/launcher-changelog.txt, and a new next.md starts. -->

## What's new

### First-time setup
- The launcher runs first-time setup whenever it starts in a folder with neither its own folder nor the game beside it
  (a new copy, or the exe moved somewhere new), even if it finds a game, a save or settings from before. Settings made
  for another folder count as new too. A launcher started where it was set up carries on as before.

### Options
- Options › Launcher: no Settings step in the path any more. LANGUAGE, ABOUT LAUNCHER and RUN SETUP AGAIN are buttons
  on the Launcher page itself.

### Gear Planner
- Tier Bonus empties the whole planner before it fills in the set: armor, weapons and jewelry, with their stars. What
  you see is only the set you just picked.

### Guide
- Transmog › Weapon Glows: a new card opens Wowhead's live 3D dressing room in a window of its own, where the glows
  can be seen on a weapon (Wowhead's still pictures don't show them). It closes with the launcher.

### AZS Handbook
- The Handbook's gear planner works like the launcher's: Single Item and Tier Bonus buttons, a 4 piece and an 8 piece
  button for every rarity's set (they empty the planner first, as in the launcher), and the chosen piece's name above
  the list of pieces.

### Community
- Big Streamers is ticked whenever the launcher starts, until you pick Content Creators; that choice is remembered.

### Gear Planner (layout)
- The explanation under the stat boxes is now a button, "How are gear stats calculated?", in the top right corner of
  the character sheet above Hands; click it to read it. The set bonuses move up into its place.
- The T1–T5 tier numbers sit on the inner side of each piece: by the top right corner in the left column and by the top
  left corner in the right column, so both columns lean in towards the middle.

### Addons
- Profile 1 and Profile 2 each keep their own game preferences (options, key bindings, chat windows, camera, layout
  and the addons' saved settings). LOAD stores the preferences you have under the profile you leave and brings back
  the picked profile's own, so a heavy UI addon can't leave the game looking wrong for the next one. Run histories
  (AZS Stats, Azeroth Stats), the save game and cloud saves are never part of a profile.
- New choice above them: No profile [Recommended], the default. It keeps no copy of its own and only switches
  addons; the game's preferences stay shared. Coming back to it from a profile brings back the preferences you had.
- A small "?" beside each of the three explains what it does.
- Download More: two new addons by Maidenhead, Azeroth FieldKit (an in-game helper: elite nameplates, boss frames,
  a damage meter, chests and lairs on the minimap and more) and Azeroth Stats (run history, run reports and a HUD
  you set up yourself).
- Addons update themselves: when an installed Download More addon has a newer copy on GitHub, the launcher updates it
  right away, without asking (at start, and when the game closes). Never while the game is running; the log lists
  each one.

### Fixes
- An "&" in a name (Gear & the Armory, Pickups, Caches & Guardians) shows as "&" in the path's lists and the hover
  notes, no longer as an underline.
