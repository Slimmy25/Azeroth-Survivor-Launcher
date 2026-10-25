# Writes addons/AZSStats/AZSStatsIcons.lua from assets/spell-icons.txt: the spell -> icon table the in-game window
# uses with the game's own icon textures (Interface\Icons\...). Run after editing assets/spell-icons.txt.
import os, re
here = os.path.dirname(os.path.abspath(__file__))
src = os.path.join(here, "..", "assets", "spell-icons.txt")
dst = os.path.join(here, "..", "addons", "AZSStats", "AZSStatsIcons.lua")
pairs = []
for line in open(src, encoding="utf-8-sig"):
    line = line.strip()
    if not line or line.startswith("#") or "|" not in line: continue
    name, icon = line.rsplit("|", 1)
    if re.fullmatch(r"[a-z0-9_-]+", icon.strip()): pairs.append((name.strip(), icon.strip()))
out = ["-- AZS Stats: the icon of each spell (made by tools/make_addon_icons.py from the launcher's assets/spell-icons.txt).",
       "-- The in-game window shows Interface\\Icons\\<icon>; a spell missing here, or an icon the game does not have,",
       "-- gets the question mark icon.",
       "AZSSTATS_SPELL_ICONS = {"]
for name, icon in sorted(pairs, key=lambda p: p[0].lower()):
    out.append('\t["%s"] = "%s",' % (name.lower().replace("\\", "\\\\").replace('"', '\\"'), icon))
out.append("};")
open(dst, "w", encoding="utf-8", newline="\r\n").write("\n".join(out) + "\n")
print(len(pairs), "spells")
