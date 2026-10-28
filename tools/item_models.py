# Reads a CMaNGOS / VMaNGOS world database dump (item_template) and prints "Name|item id|display id" for every item
# whose name is in the given name files (lines "kind | slot | name | ..." or plain names): the lowest entry with that
# name. Used by .github/workflows/item-models.yml for the Transmog looks' Wowhead model pictures.
# Usage: python3 item_models.py transmog.txt [more files, or plain lists of names] < world.sql
import re, sys

def rows(sql):
    # every INSERT INTO `item_template` ... VALUES (...),(...);
    for m in re.finditer(r"INSERT INTO `item_template`\s*(\([^)]*\))?\s*VALUES\s*(.*?);\s*$", sql, re.S | re.M):
        cols = [c.strip(" `") for c in m.group(1)[1:-1].split(",")] if m.group(1) else None
        body = m.group(2)
        i, n = 0, len(body)
        while i < n:
            if body[i] != "(":
                i += 1
                continue
            vals, cur, j, q = [], "", i + 1, False
            while j < n:
                ch = body[j]
                if q:
                    if ch == "\\" and j + 1 < n: cur += body[j + 1]; j += 2; continue
                    if ch == "'": q = False
                    else: cur += ch
                elif ch == "'": q = True
                elif ch == ",": vals.append(cur); cur = ""
                elif ch == ")": vals.append(cur); break
                else: cur += ch
                j += 1
            yield cols, [v.strip() for v in vals]
            i = j + 1


wanted = set()
for path in sys.argv[1:]:
    text = open(path, encoding="utf-8-sig").read()
    on = "===" not in text                     # a plain list of names: every line
    for line in text.splitlines():
        line = line.strip()
        if line == "===": on = True; continue
        if not on or not line or line.startswith("#"): continue
        f = [x.strip() for x in line.split("|")]
        wanted.add((f[2] if len(f) >= 3 else f[0]).lower())

sql = sys.stdin.read()
table = re.search(r"CREATE TABLE `item_template` \((.*?)\n\)", sql, re.S)
declared = [m.group(1) for m in re.finditer(r"^\s*`(\w+)`", table.group(1), re.M)] if table else None
best = {}
for cols, v in rows(sql):
    cols = cols or declared
    if cols is None:
        sys.exit("item_template: no column list")
    d = {k.lower(): x for k, x in zip(cols, v)}
    entry = int(d.get("entry") or 0)
    name = (d.get("name") or "").strip()
    disp = d.get("displayid") or d.get("display_id") or "0"
    disp = int(disp) if disp.isdigit() else 0
    if not name or not disp or (wanted and name.lower() not in wanted): continue
    if name not in best or entry < best[name][0]: best[name] = (entry, disp)
for name in sorted(best):
    print(name + "|" + str(best[name][0]) + "|" + str(best[name][1]))
