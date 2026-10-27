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
say("functions: %d" % len(funcs))
want = set()
for lit, al in lits.items():
    for a in al:
        for k, fn in enumerate(funcs):
            if re.search(r"\b%d\b" % a, fn):
                want.add(k); say("%s (%d) used in function #%d %s (%d chars)" % (lit.decode(), a, k, fn[:60].replace("\n", " "), len(fn)))
os.makedirs("/tmp/probe", exist_ok=True)
for k in sorted(want):
    open("/tmp/probe/f%d.txt" % k, "w").write(funcs[k])

text = "\n".join(out)
chunk, n = "", 0
for line in text.split("\n"):
    if len(chunk) + len(line) > 3500:
        print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A"))); n += 1; chunk = ""
    chunk += line + "\n"
print("::notice title=probe %d::%s" % (n, chunk.replace("\n", "%0A")))
