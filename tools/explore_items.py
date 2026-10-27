"""Temporary developer probe. Prints findings as annotations."""
import re, struct, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from game_talents import get, wasm_url, memory
data = get(wasm_url()); mem = memory(data)
u = lambda q: struct.unpack_from("<I", mem, q)[0]
fl = lambda q: struct.unpack_from("<f", mem, q)[0]
out = []; say = out.append
def word(w):
    if 1048576 <= w < len(mem):
        m = re.match(rb"[\x20-\x7e]{3,}", bytes(mem[w:w + 30]))
        if m: return "'%s'" % m.group(0).decode()[:22]
    x = struct.unpack("<f", struct.pack("<I", w))[0]
    if w > 0x01000000 and 0.0001 < abs(x) < 100000: return "%gf" % x
    return "%x" % w
def raw(a, b):
    # mixed view: runs of text shown as text, else words
    q = a; line = []
    while q < b:
        m = re.match(rb"[\x20-\x7e]{4,}", bytes(mem[q:q + 200]))
        if m and q % 1 == 0:
            line.append('"%s"' % m.group(0).decode()); q += len(m.group(0)); continue
        if q % 4: line.append("%02x" % mem[q]); q += 1; continue
        line.append(word(u(q))); q += 4
    return " ".join(line)
say(raw(0x506ab0, 0x507280))
for k in range(10):
    a = 0x480f54 + k * 92
    say("pvp %d: %s" % (k, " ".join(word(u(a + 4 * i)) for i in range(23))))
for k in range(9):
    a = 0x480378 + k * 0xe8
    w = u(a + 60)
    say("cls %d wptr %x n %d: %s" % (k, w, u(a + 64), " ".join(word(u(w + 4 * i)) for i in range(12))))
for p in (0x480d70, 0x480da4, 0x480dec, 0x480e60):
    say("pw %x: %s" % (p, " ".join(word(u(p + 4 * i)) for i in range(12))))
text = "\n".join(out)
chunk, n = "", 0
for line in text.split("\n"):
    while len(line) > 3400:
        print("::notice title=probe %d::%s" % (n, (chunk + line[:3400]).replace("\n", "%0A"))); n += 1; chunk = ""; line = line[3400:]
    if len(chunk) + len(line) > 3400:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
