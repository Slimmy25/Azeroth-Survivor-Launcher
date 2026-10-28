# Prints the name of every gear piece of Azeroth Survivors, one per line, built from assets/items.txt the way the
# launcher builds them (Items.cs): set name + the armor's noun for each slot, the weapons and the PvP pieces (both
# factions' names). Used by .github/workflows/item-icons.yml.
import sys
sec, rows = None, {}
for line in open(sys.argv[1] if len(sys.argv) > 1 else "assets/items.txt", encoding="utf-8-sig"):
    line = line.rstrip("\r\n")
    if line.startswith("@") and " " not in line.strip(): sec = line.strip()[1:]; rows.setdefault(sec, []); continue
    if sec and line.strip() and not line.startswith("#"): rows[sec].append([x.strip() for x in line.split("|")])
armor = {r[0]: r[1] for r in rows.get("armor", [])}
nouns = {}
for a, slot, noun in rows.get("noun", []): nouns.setdefault(a, []).append(noun)
names = set()
for r in rows.get("set", []):
    cls, rar, name, of = r[0], r[1], r[2], r[3]
    for noun in nouns.get(armor.get(cls, "cloth"), []):
        names.add(noun + " of " + name if of == "1" else name + " " + noun)
for r in rows.get("weapon", []): names.add(r[3])
for r in rows.get("pvp", []):
    cls = r[0]
    for side in (r[1], r[2]):
        for noun in nouns.get(armor.get(cls, "cloth"), []): names.add(side + " " + noun)
    for w in r[3:]:
        for n in w.split(" / "): names.add(n.strip())
for n in sorted(names): print(n)
