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

u = lambda q: struct.unpack_from("<I", mem, q)[0]
fl = lambda q: struct.unpack_from("<f", mem, q)[0]
def pairs(at, n=10):
    return " ".join("(%d,%g)" % (u(at + 8 * k), fl(at + 8 * k + 4)) for k in range(n))
for hv in (82.0, 45.0, 39.0, 29.0, 19.0):
    b = struct.pack("<f", hv)
    q = 0; c = 0
    while c < 12:
        q = mem.find(b, q)
        if q < 0: break
        if q % 4 == 0 and q >= 4 and u(q - 4) < 40:
            say("health %g at %x: %s" % (hv, q, pairs(q - 4 - 16, 8)))
            c += 1
        q += 1
for a in (0x90c588, 0x9fc588, 0xa10544, 0xa1ea04, 0xd1d22c):
    say("0.042 @%x: %s" % (a, pairs(a - 4 - 24, 8)))
funcs = []; want = set()
import gzip, base64, json
names = json.loads(os.environ.get("PROBE_FUNCS", "[]"))
blob = {}
for k, fn in enumerate(funcs):
    m = re.match(r"function (\w+)", fn)
    if (k in want and len(fn) < 30000) or (m and m.group(1) in names): blob[m.group(1) if m else str(k)] = fn
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
