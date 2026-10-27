"""Reads the items out of the game itself, as wow.aethus.pl serves it, and writes assets/items.txt.

wow.aethus.pl runs Azeroth Survivors in the browser; its items (the rings, trinkets and amulets with their numbers, and
the names of every class's gear in each rarity) are inside its WebAssembly file. Nothing comes from any other site.
Run it after a game update:  python tools/game_items.py   (Actions > Game items does the same.)

The tables are found by their shape, not by fixed places, so a new game build is read the same way.
The gear's bonuses are NOT in the game's files (the game works them out as it runs): assets/gear-stats.txt holds
those, entered from the game's own item cards.
"""
import json, os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from game_talents import get, wasm_url, memory

CLASSES = ["warrior", "paladin", "hunter", "rogue", "priest", "shaman", "mage", "warlock", "druid"]
ARMOR = {"warrior": "plate", "paladin": "plate", "hunter": "mail", "rogue": "leather", "priest": "cloth",
         "shaman": "mail", "mage": "cloth", "warlock": "cloth", "druid": "leather"}
SLOTS = ["Head", "Shoulder", "Chest", "Waist", "Legs", "Feet", "Wrist", "Hands"]   # the order of the game's nouns
RARITIES = ["Common", "Uncommon", "Rare", "Epic", "Legendary"]                     # the five class sets, in order


def main():
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    data = open(sys.argv[1], "rb").read() if len(sys.argv) > 1 else get(wasm_url())
    if data[:4] != b"\0asm":
        sys.exit("::error::the download is not the game's WebAssembly file")
    mem = memory(data)
    text = data.decode("latin1")
    debug = []
    u = lambda i: struct.unpack_from("<I", mem, i)[0] if 0 <= i <= len(mem) - 4 else 0
    f = lambda i: struct.unpack_from("<f", mem, i)[0]

    def s(p, l):
        if p < 1048576 or l <= 0 or l > 400 or p + l > len(mem):
            return None
        try:
            return mem[p:p + l].decode("utf-8")
        except UnicodeDecodeError:
            return None

    # ---- the English texts (the first of each key in the game's translations)
    en = {}
    for m in re.finditer(r'"([a-z_]+\.[A-Za-z0-9_.]+)":\s*"((?:[^"\\]|\\.)*)"', text):
        if m.group(1) not in en:
            try:
                en[m.group(1)] = json.loads('"' + m.group(2) + '"').encode("latin1").decode("utf-8")
            except Exception:
                pass

    # ---- jewelry: key, name, icon, text (four strings), then its number and its kind
    jewels = []
    for r in range(1048576, len(mem) - 40, 4):
        k = s(u(r), u(r + 4))
        if not k or not re.fullmatch(r"[a-z][a-z0-9_]{1,30}", k):
            continue
        n = s(u(r + 8), u(r + 12))
        if not n or not re.fullmatch(r"[A-Z][A-Za-z' :\-]+", n):
            continue
        ic = s(u(r + 16), u(r + 20))
        if not ic or not re.fullmatch(r"(INV|Spell|Ability)_[A-Za-z0-9_]+", ic):
            continue
        t = s(u(r + 24), u(r + 28))
        if not t or "{v}" not in t:
            continue
        v, kind = f(r + 32), u(r + 36)
        if not (0 < v < 10000) or kind > 0x400:
            continue
        jewels.append((r, k, n, ic, t, v, kind))
    debug.append("jewelry records: %d" % len(jewels))
    for j in jewels:
        debug.append("  %x %s | %s | %s | %s | %g | %x" % j)

    # ---- the amulets: their names are in the translations; where their numbers are, the debug notes say
    amulets = sorted((k[7:], v) for k, v in en.items() if k.startswith("amulet."))
    for key, name in amulets:
        hits = []
        b = name.encode("utf-8")
        q = 0
        while len(hits) < 4:
            q = mem.find(b, q)
            if q < 0:
                break
            hits.append(q); q += 1
        for h in hits:
            refs = []
            pb = struct.pack("<I", h)
            q = 0
            while len(refs) < 3:
                q = mem.find(pb, q)
                if q < 0:
                    break
                if q % 4 == 0:
                    refs.append(q)
                q += 1
            for rf in refs:
                debug.append("amulet %s name at %x ref %x: %s" % (key, h, rf, " ".join(
                    ("%x" % u(rf - 16 + 4 * i)) + ("=%g" % f(rf - 16 + 4 * i) if 0x30000000 < u(rf - 16 + 4 * i) < 0x48000000 else "")
                    for i in range(24))))

    # ---- the class sets: five (name, length, "of" flag) entries in a row, the first the class's common set
    flags = {}
    for cls in CLASSES:
        names = [en.get("set.%s.%d" % (cls, i)) for i in range(5)]
        if None in names:
            continue
        first = names[0].encode("utf-8")
        q = 0
        while cls not in flags:
            q = mem.find(first, q)
            if q < 0:
                break
            pb = struct.pack("<II", q, len(first))
            r = 0
            while True:
                r = mem.find(pb, r)
                if r < 0:
                    break
                if r % 4 == 0 and all(s(u(r + 12 * i), u(r + 12 * i + 4)) == names[i] for i in range(5)):
                    flags[cls] = [u(r + 12 * i + 8) & 1 for i in range(5)]
                    debug.append("sets %s at %x: %s" % (cls, r, flags[cls]))
                    break
                r += 1
            q += 1

    # ---- the PvP sets: the Alliance and Horde names and their weapons
    pvp_debug = []
    for word in ("Field Marshal's", "Grand Marshal's", "High Warlord's"):
        q = 0
        while True:
            q = mem.find(word.encode(), q)
            if q < 0:
                break
            m = re.match(rb"[\x20-\x7e]+", bytes(mem[q:q + 60]))
            pvp_debug.append("%x %s" % (q, m.group(0).decode() if m else ""))
            q += 1
    debug.extend("pvp text " + x for x in pvp_debug[:40])
    for p in [int(x.split()[0], 16) for x in pvp_debug[:20]]:
        for ln in range(8, 40):
            pb = struct.pack("<II", p, ln)
            r = mem.find(pb)
            if r >= 0 and r % 4 == 0:
                debug.append("pvp ref %x len %d at %x: %s" % (p, ln, r, " ".join("%x" % u(r - 8 + 4 * i) for i in range(30))))

    # ---- drop chances: a run of decreasing shares from 60% to 20%
    for r in range(1048576, len(mem) - 24, 4):
        v = [f(r + 4 * i) for i in range(6)]
        if abs(v[0] - 0.6) < 1e-6 and abs(v[5] - 0.2) < 1e-6:
            debug.append("chances at %x: %s" % (r, v))

    # ---- write assets/items.txt
    out = ["Items of Azeroth Survivors, read from the game itself at wow.aethus.pl by tools/game_items.py.",
           "Do not edit by hand: run the tool again after a game update. Gear bonuses are in gear-stats.txt.",
           "@jewelry  key | Name | Icon | kind | stat | value | text   (kind: ring, trinket; value: {v} at its first pick)",
           "@set  class | rarity | set name | of   (of = 1: \"Gloves of Faith\", 0: \"Devout Leggings\")",
           "@noun  armor | slot | noun      @weapon  class | weapon | rarity | name", "==="]
    out.append("@jewelry")
    seen = set()
    for _, k, n, ic, t, v, kind in jewels:
        if k in seen:
            continue
        seen.add(k)
        out.append(" | ".join([k, n, ic, "ring" if kind < 0x100 else "trinket", str(kind), "%g" % v, t]))
    out.append("@amulet")
    for key, name in amulets:
        out.append("%s | %s" % (key, name))
    out.append("@set")
    for cls in CLASSES:
        for i in range(5):
            name = en.get("set.%s.%d" % (cls, i))
            if name:
                out.append(" | ".join([cls, RARITIES[i], name, str(flags.get(cls, [0] * 5)[i])]))
    out.append("@noun")
    for armor in ("cloth", "leather", "mail", "plate"):
        for i, slot in enumerate(SLOTS):
            n = en.get("noun.%s.%d" % (armor, i))
            if n:
                out.append(" | ".join([armor, slot, n]))
    out.append("@weapon")
    for cls in CLASSES:
        for w in range(3):
            for i in range(5):
                n = en.get("weapon.%s.%d.%d" % (cls, w, i))
                if n:
                    out.append(" | ".join([cls, str(w), RARITIES[i], n]))
    out.append("@armor")
    for cls in CLASSES:
        out.append("%s | %s" % (cls, ARMOR[cls]))
    path = os.path.join(root, "assets", "items.txt")
    open(path, "w", encoding="utf-8", newline="\r\n").write("\n".join(out) + "\n")
    open(os.path.join(root, "items-debug.txt"), "w", encoding="utf-8").write("\n".join(debug) + "\n")
    print("%d jewelry, %d amulets, %d class sets -> %s" % (len(seen), len(amulets), len(flags), path))


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:                                   # shown on the Actions run's page
        print("::error::%s: %s" % (type(e).__name__, e))
        raise
