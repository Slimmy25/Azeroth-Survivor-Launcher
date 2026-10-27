"""Temporary developer probe: where the game keeps its jewelry and gear numbers. Prints findings as annotations."""
import re, struct, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from game_talents import get, wasm_url, memory

data = get(wasm_url())
mem = memory(data)
u = lambda i: struct.unpack_from("<I", mem, i)[0]
f = lambda i: struct.unpack_from("<f", mem, i)[0]
out = []
def say(x): out.append(x)

def refs(addr):
    b = struct.pack("<I", addr)
    r, i = [], 0
    while True:
        i = mem.find(b, i)
        if i < 0: break
        if i % 4 == 0: r.append(i)
        i += 1
    return r

def dump(at, n=24):
    words = [u(at + 4 * k) for k in range(n)]
    parts = []
    for w in words:
        fl = struct.unpack("<f", struct.pack("<I", w))[0]
        txt = ""
        if 1048576 <= w < len(mem):
            seg = mem[w:w + 24]
            m = re.match(rb"[\x20-\x7e]{3,}", bytes(seg))
            if m: txt = "'" + m.group(0).decode()[:18] + "'"
        if not txt and 0.001 < abs(fl) < 100000 and w > 0x01000000: txt = "%gf" % fl
        parts.append("%x%s" % (w, ("=" + txt) if txt else ""))
    return " ".join(parts)

def rows(start, end, step):
    for a in range(start, end, step):
        say("%x: %s" % (a, dump(a, step // 4)))

# runs of 5-6 increasing small floats (rarity tiers?) anywhere in the data
seen = 0
for a in range(1048576, len(mem) - 24, 4):
    v = [f(a + 4 * k) for k in range(6)]
    if all(0.001 <= x <= 5000 for x in v) and all(v[k] < v[k + 1] for k in range(5)) and v[5] / v[0] < 20 and v[5] / v[0] > 1.5:
        if len(set(round(x, 4) for x in v)) == 6 and all(abs(x * 1000 - round(x * 1000)) < 1e-3 or abs(x - round(x)) < 1e-6 for x in v):
            say("%x: %s" % (a, " ".join("%g" % x for x in v)))
            seen += 1
            if seen > 120: break
rows(0x480378 + 0xe8 * 3, 0x480378 + 0xe8 * 9, 116)
text = "\n".join(out)
# annotations: chunks of about 3500 characters
chunk, n = "", 0
for line in text.split("\n"):
    if len(chunk) + len(line) > 3500:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
