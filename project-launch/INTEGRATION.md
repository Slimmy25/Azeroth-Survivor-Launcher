# Project-Launch integration checklist

This branch contains experimental prototypes under `project-launch/src/`. These files are **not included in the launcher build**. They are deliberately isolated to avoid breaking `main`.

## Existing work retained
- Existing `src/Talents.cs` has the AZST1 talent serialization and parser.
- Existing `src/GearPlanner.cs` has equipment selections and per-slot stars.
- Existing `src/Guide.cs` has the current class pickers and saved talent choices.

## Integration remaining
1. Replace separate Theorycrafting entry points with a single Build Planner entry.
2. Prompt for class only once; keep the class and selected build when changing tabs.
3. Embed the existing talent and gear controls into the shared secondary menu.
4. Connect New, Save, Rename, Import, and Export to a persistent build store.
5. Migrate existing talent saves without losing them.
6. Preserve unsaved work when flipping tabs, but reset transient work when leaving the planner.
7. Remove search controls only from the unified planner, not the general Guide.
8. Add ASB2 encode/decode after checking the item-key mapping, format test vectors and live website.
9. Confirm which data are supported in both directions. Full armor, stars and talent points must not be silently dropped.
10. Compile, test, review UI appearance against the original early-WoW launcher, and confirm round-trips.

The previous ChatGPT ZIP drafts are separate review artifacts; they are not deployed by this branch.
