# Project instructions

- For current game behavior, the newest creator patch notes take priority over older patch notes and conflicting guide, addon, or launcher content. A newer note supersedes only the behavior it changes; retain unrelated earlier information.
- Preserve the creator's wording in game/developer patch-note entries. Do not paraphrase them. Keep older patch-note entries as historical records.
- Guide explanations outside developer patch notes may be paraphrased to match current behavior.
- Update affected shared data, generated Handbook content, addon behavior, and custom code together. Do not invent undocumented configuration keys, API signatures, or gameplay rules; ask the user when the intended change is unclear.

## WinForms layout: guide hierarchy and passage

- Control bounds are relative to their immediate parent. Before using `Left`, `Right`, or `Bounds` to position another control, check that both controls share the same parent. Convert coordinates explicitly if needed.
- `MakeThinScroll` returns the inner `ScrollPanel`, which is docked inside a bordered frame. Its `Right` is in the frame's coordinate system, not the outer guide panel's. Using `sp.Right` to position a sibling of the frame caused the persistent right-side dead space and partially hidden hierarchy labels.
- Anchor the hierarchy against the outer guide panel's right boundary (`PW`), which already excludes the separate favorites strip. Derive the passage width from that same boundary, the hierarchy width, and the intended gutter. Preserve the passage font size and the favorites boundary.
- The hierarchy column is borderless and anchored to the right boundary. Within that column, labels are left-aligned and children indent to the right, like Wikipedia's contents tree. Long labels wrap within its allocated width. Measure wrapped height using the largest font style the label will use, including its active bold state.
- When a visible gap persists, inspect the parent containers and coordinate systems before adjusting padding again. A successful build only confirms compilation; visually inspect the running build for alignment, wrapping, clipping, and scrolling before claiming those behaviors are verified. If inspection is interrupted, report exactly what remains unverified.

## Breadcrumb favorite heart

- Place the transparent favorite heart ABOVE the breadcrumb bar, horizontally centered on the hovered label's measured visible text glyphs. Exclude separators, arrows and padding. Use the same font, layout and trimming as the label's drawing; never hardcode anchors for particular words or place the heart beside the text.
- Convert the text and header bounds into the heart parent's coordinate system before positioning. Center the visible heart path, not an assumed control rectangle. Preserve a hover corridor between the label and heart, including the associated open dropdown; dismiss on leaving that group or launcher deactivation.
- `SideHead` in `src/Sections.cs` draws the breadcrumb and measures each label in the same paint pass. `crumbTextRects` supplies the favorite anchor; `stepRects` includes dropdown arrows for menu hover. Keep those roles distinct. Earlier labels may be hidden and long labels trimmed; anchor only to the actual visible label bounds.
- Hovering a breadcrumb opens its sibling-location dropdown; clicking a navigable ancestor goes to that location. The heart saves that breadcrumb's route, not whichever page happens to be current. Clicking a favorite opens it; hold left-click to reorder, right-click removes immediately. Keep transient menus/hearts inside the launcher's ownership and dismiss them when it loses focus.

## Guide content and navigation contract

- Maximize passage space with small intentional margins, retaining the established passage font size. Do not shrink passage text to accommodate navigation. Keep borders, scrollbar gutters and the separate favorites strip intact.
- Every class article follows the same consolidated contents hierarchy: [Class] → Spells; Talents → Talent Tree Builder; Gear → Items and Codex (Necklaces). [Class] Build Guides belongs under Additional resources. The Talent Tree Builder opens with the article's class selected. The talent calculator is a link, not an embedded talent-tree picture. Keep each class's own content, item data, and parent categories.
- Archive is a retained, flat alphabetical record of Codex passages. Do not include it in search suggestions or full-guide search results, and do not make links or favorites target it. Keep Codex entries in class guides; any Codex hyperlink elsewhere must lead to the relevant class-guide entry. Preserve the Archive text for reference and maintenance.
- Use themed text hyperlinks, spell/talent/Codex icons and in-article anchors instead of repeated large navigation buttons. Preserve cross-links between spells and talents and the universal Back action's previous scroll position in every class article. The tree stays visible and highlights the passage section currently in view.
- Wheel over the contents tree jumps one in-article section per complete notch (120 delta units); partial deltas accumulate for touchpads. Wheel down advances, wheel up returns, and endpoints do not wrap. This scrolls the passage and updates its highlight much faster than normal passage scrolling. External resources require clicking, so wheel browsing cannot unexpectedly leave the article. Do not add a visible scrollbar to the tree.
- Use the existing `WheelRouter` and `IWheel` interface for wheel handling over both tree labels and empty tree space; do not install an additional global mouse hook. Retain the fonts measured when building the sidebar when changing its active state, because `Theme.Zoom` resets after page construction.
