"""Temporary developer probe: the code that makes a piece of gear's bonuses. Prints findings as annotations."""
import re, struct, sys, os, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from game_talents import get, wasm_url, memory

data = get(wasm_url())
open("/tmp/g.wasm", "wb").write(data)
mem = memory(data)
out = []
def say(x): out.append(x)

def addrs(lit):
    r, j = [], 0
    while True:
        j = mem.find(lit, j)
        if j < 0: break
        r.append(j); j += 1
    return r

lits = {}
for lit in [b"lore.", b"item.equip", b"item.health", b"set.", b"noun.", b"weapon."]:
    lits[lit] = [a for a in addrs(lit) if a >= 1048576][:6]
    say("%s at %s" % (lit.decode(), lits[lit]))

subprocess.run(["wasm-decompile", "/tmp/g.wasm", "-o", "/tmp/g.dcmp"], check=True)
src = open("/tmp/g.dcmp", encoding="utf-8", errors="replace").read()
funcs = re.split(r"\n(?=function )", src)
want = set()
keys = {}
for lit in [b"item.name_of", b"item.name_prefix", b"lore.", b"rarity.", b"armory.drops_from", b"armory.zone_drops"]:
    keys[lit] = [a for a in addrs(lit) if a >= 1048576][:3]
for k, fn in enumerate(funcs):
    hit = []
    for lit, al in keys.items():
        for a in al:
            if re.search(r"\b%d\b" % a, fn): hit.append(lit.decode())
    for c in ("6364136223846793005", "-7046029254386353131", "1442695040888963407", "-4658895280553007687", "2685821657736338717"):
        if c in fn: hit.append("rng " + c)
    if hit:
        want.add(k); say("#%d %s (%d chars): %s" % (k, fn[:30].replace("\n", " "), len(fn), hit))
import gzip, base64, json
names = json.loads(os.environ.get("PROBE_FUNCS", "[]"))
blob = {}
for k, fn in enumerate(funcs):
    m = re.match(r"function (\w+)", fn)
    if (k in want and len(fn) < 12000) or (m and m.group(1) in names): blob[m.group(1) if m else str(k)] = fn
packed = base64.b64encode(gzip.compress(json.dumps(blob).encode(), 9)).decode()
say("BLOB %d" % len(packed))
for q in range(0, len(packed), 3400):
    print("::notice title=blob %d::%s" % (q // 3400, packed[q:q + 3400]))
text = "\n".join(out)
chunk, n = "", 0
for line in text.split("\n"):
    if len(chunk) + len(line) > 3500:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
