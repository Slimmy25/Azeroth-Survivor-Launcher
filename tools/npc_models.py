# Reads a CMaNGOS / VMaNGOS world database dump (creature_template) and prints "Name|DisplayID" for every creature:
# the first model of the lowest entry with that name. Used by .github/workflows/npc-models.yml.
import re, sys

def rows(sql):
    # every INSERT INTO `creature_template` ... VALUES (...),(...);
    for m in re.finditer(r"INSERT INTO `creature_template`\s*(\([^)]*\))?\s*VALUES\s*(.*?);\s*$", sql, re.S | re.M):
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

sql = sys.stdin.read()
# the columns, in order, from the dump's CREATE TABLE (its INSERTs list values only)
table = re.search(r"CREATE TABLE `creature_template` \((.*?)\n\)", sql, re.S)
declared = [m.group(1) for m in re.finditer(r"^\s*`(\w+)`", table.group(1), re.M)] if table else None
best = {}
for cols, v in rows(sql):
    cols = cols or declared
    if cols is None:
        sys.exit("creature_template: no column list")
    d = dict(zip(cols, v))
    entry = int(d.get("Entry") or d.get("entry") or 0)
    name = (d.get("Name") or d.get("name") or "").strip()
    models = [d.get(k) for k in ("ModelId1", "ModelId2", "ModelId3", "ModelId4", "display_id1", "display_id2", "modelid_1", "modelid_2")]
    model = next((int(m) for m in models if m and m.isdigit() and int(m) > 0), 0)
    if not name or not model: continue
    if name not in best or entry < best[name][0]: best[name] = (entry, model)
for name in sorted(best):
    print(name + "|" + str(best[name][1]))
