"""Reads the talent trees out of the game itself, as wow.aethus.pl serves it, and writes assets/talents.txt.

wow.aethus.pl runs Azeroth Survivors in the browser; the game's data (talents, their names, icons, places in the
tree, ranks and texts, and the English ability names) is inside its WebAssembly file. Nothing comes from any other
site. Run it after a game update:  python tools/game_talents.py   (Actions > Game talents does the same.)

assets/talents.txt, one line per talent, grouped under "@tree Class | Spec":
    key | Name | Icon | row | column | ranks | text
and the ability names under "@abilities":  key | Name
"""
import json, re, struct, sys, urllib.request, os

SITE = "https://wow.aethus.pl/"
UA = {"User-Agent": "Mozilla/5.0 (AzerothSurvivorsLauncher talent export)"}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
        return r.read()


def wasm_url():
    html = get(SITE).decode("utf-8", "replace")
    m = re.search(r'wenilla_bg\.wasm\?v=([0-9a-f]+)', html)
    if not m:
        for js in re.findall(r'src="([^"]+\.js[^"]*)"', html):
            t = get(SITE + js.lstrip("/")).decode("utf-8", "replace")
            m = re.search(r'wenilla_bg\.wasm\?v=([0-9a-f]+)', t)
            if m:
                break
    return SITE + "wenilla_bg.wasm" + ("?v=" + m.group(1) if m else "")


def memory(b):
    """The wasm's data segments laid out as linear memory."""
    p = 8

    def leb():
        nonlocal p
        r = s = 0
        while True:
            x = b[p]; p += 1
            r |= (x & 0x7f) << s; s += 7
            if not x & 0x80:
                return r

    def sleb():
        nonlocal p
        r = s = 0
        while True:
            x = b[p]; p += 1
            r |= (x & 0x7f) << s; s += 7
            if not x & 0x80:
                if x & 0x40:
                    r -= 1 << s
                return r

    while p < len(b):
        sid = b[p]; p += 1
        size = leb()
        if sid == 11:
            break
        p += size
    n = leb()
    segs = []
    for _ in range(n):
        flag = leb()
        off = 0
        if flag == 0:
            p += 1; off = sleb() & 0xffffffff; p += 1
        elif flag == 2:
            leb(); p += 1; off = sleb() & 0xffffffff; p += 1
        ln = leb()
        segs.append((off, p, ln)); p += ln
    end = max(o + l for o, _, l in segs)
    mem = bytearray(end)
    for o, q, l in segs:
        mem[o:o + l] = b[q:q + l]
    return mem


def main():
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    data = open(sys.argv[1], "rb").read() if len(sys.argv) > 1 else get(wasm_url())
    mem = memory(data)
    text = data.decode("latin1")
    u = lambda i: struct.unpack_from("<I", mem, i)[0]

    def s(p, l):
        if p < 1048576 or l <= 0 or l > 3000 or p + l > len(mem):
            return None
        try:
            return mem[p:p + l].decode("utf-8")
        except UnicodeDecodeError:
            return None

    talents = []
    for r in range(1048576, len(mem) - 56, 4):
        kl = u(r + 4)
        if kl < 3 or kl > 40:
            continue
        k = s(u(r), kl)
        if not k or not re.fullmatch(r"[a-z][a-z0-9_]+", k):
            continue
        n = s(u(r + 8), u(r + 12))
        if not n or not re.fullmatch(r"[A-Z][A-Za-z' :\-]+", n):
            continue
        ic = s(u(r + 16), u(r + 20))
        if not ic or not re.fullmatch(r"[A-Za-z]+_[A-Za-z0-9_]+", ic):
            continue
        d = s(u(r + 32), u(r + 36))
        if not d or len(d) < 8:
            continue
        pk = u(r + 52)
        row, col, rk = pk & 255, (pk >> 8) & 255, (pk >> 16) & 255
        if row > 6 or col > 3 or not 1 <= rk <= 5:
            continue
        talents.append((r, k, n, ic, row, col, rk, d))

    # the trees: in memory order, a new tree whenever the rows start over
    trees, cur, last = [], None, 99
    for t in sorted(talents):
        if cur is None or t[4] < last - 2:
            cur = []; trees.append(cur)
        cur.append(t); last = t[4]
    order = [("Warrior", "Arms"), ("Warrior", "Fury"), ("Warrior", "Protection"), ("Paladin", "Holy"),
             ("Paladin", "Protection"), ("Paladin", "Retribution"), ("Hunter", "Beast Mastery"), ("Hunter", "Marksmanship"),
             ("Hunter", "Survival"), ("Rogue", "Assassination"), ("Rogue", "Combat"), ("Rogue", "Subtlety"),
             ("Priest", "Discipline"), ("Priest", "Holy"), ("Priest", "Shadow"), ("Shaman", "Elemental"),
             ("Shaman", "Enhancement"), ("Shaman", "Restoration"), ("Mage", "Arcane"), ("Mage", "Fire"), ("Mage", "Frost"),
             ("Warlock", "Affliction"), ("Warlock", "Demonology"), ("Warlock", "Destruction"), ("Druid", "Balance"),
             ("Druid", "Feral Combat"), ("Druid", "Restoration")]
    if len(trees) != len(order):
        print("::error::found %d trees (%s), expected %d" % (len(trees), [len(t) for t in trees], len(order)))
        sys.exit(1)

    names = {}
    for m in re.finditer(r'"ability\.([a-z0-9_]+)\.name":\s*"((?:[^"\\]|\\.)*)"', text):
        if m.group(1) not in names:
            names[m.group(1)] = json.loads('"' + m.group(2) + '"').encode("latin1").decode("utf-8")

    out = ["Talent trees of Azeroth Survivors, read from the game itself at wow.aethus.pl by tools/game_talents.py.",
           "Do not edit by hand: run the tool again after a game update.",
           "key | Name | Icon | row | column | ranks | text   ({v} is the number that grows with each rank)", "==="]
    for (cls, spec), tr in zip(order, trees):
        out.append("@tree %s | %s" % (cls, spec))
        for _, k, n, ic, row, col, rk, d in sorted(tr, key=lambda t: (t[4], t[5])):
            out.append(" | ".join([k, n, ic, str(row), str(col), str(rk), d.replace("\n", "\\n")]))
    out.append("@abilities")
    for k, n in names.items():
        out.append("%s | %s" % (k, n))
    path = os.path.join(root, "assets", "talents.txt")
    open(path, "w", encoding="utf-8", newline="\r\n").write("\n".join(out) + "\n")
    print("%d trees, %d talents, %d ability names -> %s" % (len(trees), sum(len(t) for t in trees), len(names), path))


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:                                   # shown on the Actions run's page
        print("::error::%s: %s" % (type(e).__name__, e))
        raise
