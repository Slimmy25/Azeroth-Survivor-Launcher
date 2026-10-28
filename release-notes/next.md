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

### Addons
- Addons update themselves: when an installed Download More addon has a newer copy on GitHub, the launcher updates it
  right away, without asking (at start, and when the game closes). Never while the game is running; the log lists
  each one.

### Fixes
- An "&" in a name (Gear & the Armory, Pickups, Caches & Guardians) shows as "&" in the path's lists and the hover
  notes, no longer as an underline.
