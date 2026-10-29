# Release notes

- `next.md`: the notes of the next release, written as each change is made, for players: what changed and what it
  means for them, grouped by part of the launcher (Getting the game, The Guide, Addons, AZS addons, STATS, Community,
  Everything else).
- `v1.0.md`, `v1.1.md` ...: one file per release. When a release is published, `next.md` becomes the new version's
  file. Starting after v3.0, launcher patch releases advance by 0.01 (v3.01, v3.02, and so on). The text is added to the top of `assets/launcher-changelog.txt`, and
  a new `next.md` starts.

The Build workflow uses `v<version>.md` as that release's notes on GitHub. To fix the notes of a release already out
without building it again: Actions > Release notes > Run workflow.
