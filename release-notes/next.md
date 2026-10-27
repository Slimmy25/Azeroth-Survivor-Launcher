<!-- The next release's notes, written as changes are made. On release this file becomes v<version>.md (0.1 higher
     than the last release), its text goes to the top of assets/launcher-changelog.txt, and a new next.md starts. -->

## What's new

This is a very large, but scattered update: it touches many different parts of the launcher, from the first-time setup to
the Guide and the Gear Planner, rather than one big feature. Taken together it is a big step up in how easy and simple
the launcher is to use.

### Gear Planner: stars
- Click a piece on the character sheet to add a star: each click adds one, up to 5, and the next click goes back to 0.
  A small number on the piece shows its stars. The star buttons above the list are gone. Right-click still takes the
  piece off, and a newly picked piece starts at 0 stars.
- Weapons take stars too (all pieces go up to 5).
- Stars decide which set bonuses you reach (4-piece at 1+ stars, 8-piece at 3+). How much each star adds to a piece,
  and what it costs, is not known yet, so the planner's numbers never count it (no more "waiting for clarification").
- AZS Handbook (in game): the same: click a piece under Slots to add a star, its stars shown as [3].

### Guide
- Gear & the Armory: Marks and Stars rewritten from the patch notes. Every boss you kill pays your class marks (half on
  normal, more in harder zones, on higher Heroic levels and for returning endless bosses), a duplicate pays marks when it
  drops, marks are paid the moment the boss dies (even if you fall later), and clearing a zone pays nothing extra. Each
  Heroic level opens the next star (Heroic III also opens the 8-piece bonuses), weapons go to 5 stars too, and the
  patch notes' numbers are listed (the legendary sets, the patch notes' "tier 3", 60% stronger at 5 stars, lower sets 95% of it, last star 30 marks, 5% cheaper
  per other class up to 40%), with what is not known yet.

### Game update 0.122.0
- News and Changelog: the patch notes for 0.122.0 (co-op lag, lobby difficulty and its filter, Heroic clears in co-op,
  Sanguine, Fire Nova, resumed downloads, Again after a run).
- Guide > Co-op: lobbies show their difficulty, the Difficulty filter and "Can join", hosting with Change difficulty,
  everyone seeing the host's zone and difficulty at once (a hero who has not opened them stays behind), and a Heroic
  clear counting the moment the final boss falls.
- Guide > Heroic & Endless: Sanguine pools heal only the horde, not bosses or lair guardians.

### Addons
- New in Download More under UI: AetherHUD by Rehabrocket, a complete interface (run clock with the boss timeline, boss
  bars, enemy plates, unit frames, skill bar, combat meter, ring minimap). Like the other interfaces it is pick-one:
  switching it on switches Survivor UI, SurvivorsDragonUi, Keijin UI, Bordo UI or BlackwellUI off.

### First-time setup (Major Update)
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

### Everything else (Major Update)
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
