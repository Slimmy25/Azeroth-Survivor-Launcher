<!-- The next release's notes, written as changes are made. On release this file becomes v<version>.md (0.1 higher
     than the last release), its text goes to the top of assets/launcher-changelog.txt, and a new next.md starts. -->

## What's new

### News and Changelog
- Patch notes for the game's 0.119.1, 0.120.0 and 0.120.1 updates: fewer co-op disconnects, any race with any class
  (Rewards > Races), and boss kills counting for the whole party's achievements.

### Cloud Save
- Cloud Save no longer copies the AddOns folder itself, only your addons' settings and your save. Before, an older copy
  of an addon from another PC could replace one you had just updated.
- When settings arrive from the cloud for an addon in Download More that isn't installed on this PC, the launcher
  installs it, at PLAY or after DOWNLOAD in Cloud Save. Addons switched off stay off.

### Community
- Hovering the Discord symbol on the News line now shows where it goes: https://discord.gg/azerothsurvivors
- MORE VIDEOS: a video you have watched for more than 45 seconds now has a "Watched!" tag over its picture (only the
  time the video really plays counts, not loading or pausing). The tag is remembered between launches.
- MORE VIDEOS: the featured videos stay at the top in their own order; every other video is shuffled into a new random
  order each time you open the page.
- New featured video in the front page's rotation and at the top of MORE VIDEOS: https://www.youtube.com/watch?v=AxcvhgPfYHU

### The Guide
- Races: how to open any race to any class on the Rewards tab (new in the game's 0.120.0).
- Achievements & Rewards: the Races reward, and co-op achievements counting for the whole party.
- Talent Tree Builder: leaving a class's talent trees now clears the points you put in, so every visit starts from an
  empty build (before, the last build came back).

### Addons
- New in Download More > AZS ADDONS: Survivor UI by morderous (with Survivor Plates). The launcher always takes the
  addon zip from its newest release (SurvivorUI-v….zip), never the Survivor UI installer next to it.
- MinimapButtonBag: clicking the bag now opens its bar of buttons. It closed itself a moment after opening, and a
  minimap button the game shows by itself could make the bag think the bar was already open. The bar now stays until
  your pointer has been away from it for 3 seconds (change it in its options, right-click the bag). New: /mbb open
  and /mbb close.
- MinimapButtonBag's bag button now sits on the right of the minimap, no longer under the game's damage meter button.
- Addons that can't be on together are now marked as a conflict. The first pair is Survivor UI and AZS Minor Mods
  (both set raid marks on bosses and party members). In My Addons a red bracket joins the two, with CONFLICT down it.
  Switching one on switches the other off. When both end up on any other way (an install, a profile, TOGGLE ALL, or
  by hand), Survivor UI is kept and AZS Minor Mods goes off; PLAY checks this too.
- AZS Minor Mods, chat: one setting now, "No level-up, stat bonus or new spell lines in chat". It hides
  "Congratulations, you have reached level 12!", "You have gained 3% damage, 12 maximum health…" and "You have
  learned a new spell: Cleave (Level 1)", in every language the game has. "Remove bloat from chat" is folded into it.
- AZS Minor Mods: the separate Say & Party box is gone. Say and Party chat stay in the main chat window as usual.
- AZS Minor Mods, new setting: "Plain action bar". The gryphons and the bar's art go, leaving just the six spell
  squares (the racial button and the character and talent buttons are hidden too). Untick it and the bar comes back
  as it was; the character and talent panels still open with their keys (C and N).

### Other
- GitHub now shows the launcher's license as MIT. The note that the game's name, logo and artwork are not covered
  moved from LICENSE to the README.
