"""Developer tool: every readable text in the game's WebAssembly file at wow.aethus.pl, one per line, into
game-strings.txt (to find the game's own words: chat lines, item and upgrade texts). Nothing from any other site."""
import re, sys
sys.path.insert(0, __file__.rsplit("/", 1)[0])
from game_talents import get, wasm_url

w = get(wasm_url())
if w[:4] != b"\0asm":
    print("::error::not a wasm file"); sys.exit(1)
out = []
for m in re.finditer(rb"[\x20-\x7e\x0a\xc2-\xf4\x80-\xbf]{4,}", w):
    s = m.group(0).decode("utf-8", "replace").replace("\n", "\\n")
    if re.search(r"[A-Za-z]{3}", s):
        out.append(s)
open("game-strings.txt", "w", encoding="utf-8").write("\n".join(out))
print(len(out), "strings")
