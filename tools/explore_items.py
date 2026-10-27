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

for key in [b"accuria", b"quick_strike", b"soulstone", b"mark_of_fordring", b"fire_lord", b"INV_Jewelry_Ring_15", b"Acolyte's", b"Devout", b"Circlet", b"Leggings"]:
    i = mem.find(key)
    hits = []
    j = 0
    while len(hits) < 4:
        j = mem.find(key, j)
        if j < 0: break
        hits.append(j); j += 1
    for h in hits:
        rr = refs(h)
        say("KEY %s at %x refs %s" % (key.decode(), h, [hex(x) for x in rr[:6]]))
        for r in rr[:3]:
            say("  @%x-16: %s" % (r, dump(r - 16, 28)))

text = "\n".join(out)
# annotations: chunks of about 3500 characters
chunk, n = "", 0
for line in text.split("\n"):
    if len(chunk) + len(line) > 3500:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
