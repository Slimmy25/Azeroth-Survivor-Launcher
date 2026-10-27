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

# constants inside the code (f32.const = 0x43, f64.const = 0x44): where do the Faith numbers sit?
def consts_near(pos, span=600):
    r = []
    q = max(0, pos - span)
    while q < pos + span:
        if data[q] == 0x43:
            x = struct.unpack_from("<f", data, q + 1)[0]
            if 1e-4 < abs(x) < 1e5 and abs(x * 1e4 - round(x * 1e4)) < 1e-2: r.append("f%g" % x)
        elif data[q] == 0x44:
            x = struct.unpack_from("<d", data, q + 1)[0]
            if 1e-4 < abs(x) < 1e5 and abs(x * 1e4 - round(x * 1e4)) < 1e-6: r.append("d%g" % x)
        q += 1
    return r
for t in (0.042, 0.081, 1.93, 0.095, 0.39):
    for kind, fmt in ((0x43, "<f"), (0x44, "<d")):
        b = bytes([kind]) + struct.pack(fmt, t)
        q = 0; n = 0
        while n < 4:
            q = data.find(b, q)
            if q < 0: break
            say("%s %g at %x: %s" % ("f32" if kind == 0x43 else "f64", t, q, " ".join(consts_near(q, 400))[:1500]))
            q += 1; n += 1
text = "\n".join(out)
# annotations: chunks of about 3500 characters
chunk, n = "", 0
for line in text.split("\n"):
    if len(chunk) + len(line) > 3500:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
