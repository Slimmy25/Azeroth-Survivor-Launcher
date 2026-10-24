"""Builds update.json for the launcher's game updates.

1. Pack the new game version as a .zip, .rar or .7z:
     - a full client (with AzerothSurvivors.exe in it; a wrapper folder is fine), or
     - a patch with only the changed files, laid out as in the game folder (no wrapper folder).
2. Upload that archive somewhere that gives a DIRECT https download link
   (GitHub Releases works well), or a download page the player opens in the browser.
3. Run this script (double-click make_update.bat, or: python make_update.py --help).
4. Upload the update.json it writes to the fixed address set as ManifestUrl in src/Update.cs
   (then run build.bat once). Each new version: new archive + new update.json at that same address.
"""
import argparse
import hashlib
import json
import os
import re
import sys
import zipfile

GAME_EXE = "AzerothSurvivors.exe"


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def kind_of(path):
    with open(path, "rb") as fh:
        head = fh.read(8)
    if head.startswith(b"Rar!\x1a\x07"):
        return "rar"
    if head.startswith((b"PK\x03\x04", b"PK\x05\x06")):
        return "zip"
    if head.startswith(b"7z\xbc\xaf\x27\x1c"):
        return "7z"
    return None


def is_https(url):
    return bool(re.match(r"^https://[^/\s@]+(/\S*)?$", url or ""))


def ask(prompt, default=""):
    text = input(f"{prompt}{f' [{default}]' if default else ''}: ").strip().strip('"')
    return text or default


def main():
    ap = argparse.ArgumentParser(description="Write update.json for the Azeroth Survivors Launcher.")
    ap.add_argument("archive", nargs="?", help="the update archive (.zip / .rar / .7z)")
    ap.add_argument("--version", help="the new game version, e.g. 0.114.0")
    ap.add_argument("--url", default="", help="DIRECT https download link of the archive")
    ap.add_argument("--page", default="", help="https download page (when there is no direct link)")
    ap.add_argument("--notes", default="", help="what is new (short)")
    ap.add_argument("--notes-file", help="read the notes from a text file")
    ap.add_argument("--installer-version", default="",
                    help="the version of the full game the launcher's INSTALL button downloads")
    ap.add_argument("--delete", nargs="*", default=[], help="files the new version no longer has")
    ap.add_argument("--out", default="update.json")
    a = ap.parse_args()

    interactive = not a.archive
    if interactive:
        print(__doc__)
        a.archive = ask("Archive file (you can drag it into this window)")
        a.version = ask("New game version (e.g. 0.114.0)")
        a.url = ask("Direct https download link (leave empty to use a download page)")
        if not a.url:
            a.page = ask("https download page")
        a.notes = ask("What is new (one line, optional)")
        a.installer_version = ask("Version of the full game behind the INSTALL button (optional)")
        gone = ask("Files to delete, separated by ; (optional)")
        a.delete = [g.strip() for g in gone.split(";") if g.strip()]

    problems = []
    if not a.archive or not os.path.isfile(a.archive):
        problems.append(f"Archive not found: {a.archive}")
    elif not kind_of(a.archive):
        problems.append("The archive is not a .zip, .rar or .7z file.")
    if not a.version or not re.match(r"^v?\d+(\.\d+)*", a.version.strip()):
        problems.append("The version must start with a number, e.g. 0.114.0")
    if not a.url and not a.page:
        problems.append("Give a direct download link (--url) or a download page (--page).")
    for name, val in (("url", a.url), ("page", a.page)):
        if val and not is_https(val):
            problems.append(f"The {name} must start with https://")
    for d in a.delete:
        p = d.replace("\\", "/")
        if p.startswith("/") or ":" in p or ".." in p.split("/") or p.split("/")[0].lower() in ("benilla-config", "_internal"):
            problems.append(f"This file may not be deleted by an update: {d}")
    if problems:
        print("\nNot written:\n  " + "\n  ".join(problems))
        return 1

    if kind_of(a.archive) == "zip":
        with zipfile.ZipFile(a.archive) as z:
            names = [n.replace("\\", "/") for n in z.namelist()]
        full = any(n.split("/")[-1] == GAME_EXE for n in names)
        print(f"\nArchive: {'full client' if full else 'patch (no ' + GAME_EXE + ' inside)'}, {len(names)} entries")
        if not full and len({n.split('/')[0] for n in names}) == 1 and any("/" in n for n in names):
            print("  Warning: everything is inside one folder. A patch must have its files at the top level,\n"
                  "  laid out as in the game folder, or they land in that extra folder.")

    notes = a.notes
    if a.notes_file:
        with open(a.notes_file, encoding="utf-8") as fh:
            notes = fh.read().strip()
    print("Computing the SHA-256 checksum...")
    data = {"version": a.version.strip(), "sha256": sha256_of(a.archive), "size": os.path.getsize(a.archive)}
    if a.url:
        data["url"] = a.url
    if a.page:
        data["page"] = a.page
    if notes:
        data["notes"] = notes[:3000]
    if a.installer_version:
        data["installer_version"] = a.installer_version.strip()
    if a.delete:
        data["delete"] = [d.replace("\\", "/") for d in a.delete]
    with open(a.out, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)
    print(f"\nWritten: {os.path.abspath(a.out)}\n")
    print(json.dumps(data, indent=2))
    print("\nNext: upload the archive (if not done yet) and this update.json to the ManifestUrl address (src/Update.cs).")
    print("Upload the archive FIRST, update.json last, so players never see a link that is not there yet.")
    return 0


if __name__ == "__main__":
    code = main()
    if len(sys.argv) == 1:
        input("\nPress Enter to close.")
    sys.exit(code)
