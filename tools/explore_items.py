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

# the Priest's legendary (Faith) pieces as the game shows them: look for their numbers stored together
import math
def near(x, y): return abs(x - y) <= max(1e-4, abs(y) * 2e-3)
targets = [0.042, 0.071, 0.024, 0.095, 0.051, 0.039, 1.93, 0.036, 0.081, 0.048, 0.24, 0.39, 0.19, 0.13, 0.019, 0.39]
floats = {}
for a in range(1048576, len(mem) - 4, 4):
    x = f(a)
    for t in (0.042, 1.93, 0.081, 0.095, 0.071, 0.39, 0.019):
        if near(x, t): floats.setdefault(t, []).append(a)
for t, l in floats.items():
    say("f32 %g at %d places: %s" % (t, len(l), " ".join("%x" % a for a in l[:40])))
# doubles too
for a in range(1048576, len(mem) - 8, 8):
    x = struct.unpack_from("<d", mem, a)[0]
    for t in (0.042, 1.93, 0.081, 0.095, 0.071):
        if near(x, t): say("f64 %g at %x" % (t, a))
text = "\n".join(out)
# annotations: chunks of about 3500 characters
chunk, n = "", 0
for line in text.split("\n"):
    if len(chunk) + len(line) > 3500:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
