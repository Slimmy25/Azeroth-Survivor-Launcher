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

### AZS Handbook
- The Handbook's gear planner works like the launcher's: Single Item and Tier Bonus buttons, a 4 piece and an 8 piece
  button for every rarity's set (they empty the planner first, as in the launcher), and the chosen piece's name above
  the list of pieces.

### Community
- YouTube videos no longer flash a white page before they start: the player stays behind "Loading the video..." until
  the video (or its ad) plays.
- Esc closes a YouTube video (in fullscreen it leaves fullscreen first).
- While a video plays, the right end of the header says "ADS ARE YOUTUBE. NOT ME."

### Guide
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

### Guide videos
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

### Fixes
- An "&" in a name (Gear & the Armory, Pickups, Caches & Guardians) shows as "&" in the path's lists and the hover
  notes, no longer as an underline.
