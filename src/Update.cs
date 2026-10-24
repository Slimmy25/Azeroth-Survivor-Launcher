// Game client updates (not the launcher itself).
// When the launcher starts it reads a small update.json from your update server. If the version in it differs
// from the installed one, the launcher OFFERS the update; the player can say "not now" and nothing is forced.
// make_update.py builds update.json:
//   {"version": "0.114.0", "url": "https://host/file.zip" (direct link) or "page": "https://host/download",
//    "sha256": "<64 hex>" (required with url), "size": bytes, "notes": "...", "installer_version": "0.114.0",
//    "delete": ["old_file.dll"]}
// The archive is a full client (it contains AzerothSurvivors.exe; a wrapper folder is fine) or a patch with just
// the changed files at its top level. Never touched: addons and addon settings (benilla-config) that already
// exist, and the launcher itself. All or nothing: replaced files are kept in a backup and put back if anything
// fails, also after a crash or power cut (the next start of the launcher does that).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace AzerothLauncher
{
    public class UpdateInfo
    {
        public string Version = "", Url = "", Page = "", Sha256 = "", Notes = "", InstallerVersion = "";
        public long Size;
        public List<string> Delete = new List<string>();
    }

    public static class Updater
    {
        // https address of update.json, for example
        //   https://github.com/YOUR-NAME/YOUR-REPO/releases/latest/download/update.json
        // Empty = no update checking yet.
        public const string ManifestUrl = "";

        public const string WorkDir = ".azeroth_update";            // scratch and undo folder inside the game folder
        public const string DownloadName = ".azeroth_update.download";

        public static bool Enabled { get { return ManifestUrl.Trim().Length > 0; } }

        // True when the update should be OFFERED: the installed version is unknown or older.
        public static bool Needed(string installed, string latest)
        {
            installed = (installed ?? "").Trim();
            if (installed.Length == 0) return true;
            int? c = Util.CompareVersions(installed, latest);
            if (c == null) return installed != (latest ?? "").Trim();
            return c.Value < 0;
        }

        // ---- paths from update.json ----
        public static string PlainRel(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            bool bad = p.Length == 0 || p.StartsWith("/") || p.Length > 240 || p.Contains(":");
            if (!bad)
                foreach (string part in p.Split('/'))
                    if (part.Length == 0 || part == "." || part == "..") bad = true;
            if (bad) throw new UserError("The update contains a file path that is not valid: " + Short(path));
            return p;
        }

        public static string SafeRel(string path)
        {
            string p = null;
            try { p = PlainRel(path); } catch (UserError) { }
            string top = p == null ? "" : p.Split('/')[0].ToLowerInvariant();
            if (p == null || top == "benilla-config" || top == "_internal" || top.StartsWith(".azeroth_"))
                throw new UserError("The update lists a file that it may not remove: " + Short(path));
            return p;
        }

        static string Short(string s) { s = s ?? ""; return s.Length > 80 ? s.Substring(0, 80) : s; }

        public static UpdateInfo ParseManifest(object json)
        {
            var d = Json.AsDict(json);
            if (d == null) throw new UserError("The update information is not in the expected format.");
            var u = new UpdateInfo();
            u.Version = Json.Str(d, "version").Trim();
            if (u.Version.Length == 0 || u.Version.Length > 40 || Util.ParseVersion(u.Version) == null)
                throw new UserError("The update information has no valid version number.");
            u.Url = Json.Str(d, "url").Trim();
            u.Page = Json.Str(d, "page").Trim();
            if (u.Url.Length > 0 && !Net.IsHttps(u.Url)) throw new UserError("The update download address must start with https://");
            if (u.Page.Length > 0 && !Net.IsHttps(u.Page)) throw new UserError("The update page address must start with https://");
            if (u.Url.Length == 0 && u.Page.Length == 0) throw new UserError("The update information has no download address.");
            u.Sha256 = Json.Str(d, "sha256").Trim().ToLowerInvariant();
            if (u.Sha256.Length > 0 && !Regex.IsMatch(u.Sha256, "^[0-9a-f]{64}$"))
                throw new UserError("The update checksum (sha256) is not valid.");
            if (u.Url.Length > 0 && u.Sha256.Length == 0)
                throw new UserError("The update has a download address but no sha256 checksum.");
            object size = Json.Get(d, "size");
            if (size is long) u.Size = Math.Max(0, Math.Min((long)size, 1L << 42));
            else if (size is double) u.Size = Math.Max(0, (long)Math.Min((double)size, 1L << 42));
            u.Notes = Json.Str(d, "notes").Trim();
            if (u.Notes.Length > 3000) u.Notes = u.Notes.Substring(0, 3000);
            string inst = Json.Str(d, "installer_version").Trim();
            u.InstallerVersion = Util.ParseVersion(inst) != null ? inst : "";
            object gone = Json.Get(d, "delete");
            if (gone != null)
            {
                var list = Json.AsList(gone);
                if (list == null || list.Count > 500) throw new UserError("The update's list of files to remove is not valid.");
                foreach (object o in list) u.Delete.Add(SafeRel(Convert.ToString(o)));
            }
            return u;
        }

        public static UpdateInfo FetchManifest()
        {
            string text;
            using (var resp = Net.Open(ManifestUrl, 12000, true, "update"))
            using (Stream s = resp.GetResponseStream())
            {
                var ms = new MemoryStream();
                var buf = new byte[8192];
                int n;
                try
                {
                    while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, n);
                        if (ms.Length > 200000) throw new UserError("The update information is much larger than expected.");
                    }
                }
                catch (IOException) { throw new UserError("The connection to the update server was interrupted."); }
                text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
            object json;
            try { json = Json.Parse(text); }
            catch (FormatException) { throw new UserError("The update information could not be read (it is not valid JSON)."); }
            return ParseManifest(json);
        }

        public static void EnsureFreeSpace(string folder, long size, double factor)
        {
            long need = (long)(size * factor);
            if (need <= 0) return;
            long free;
            try { free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).AvailableFreeSpace; }
            catch (Exception) { return; }
            if (free < need)
                throw new UserError("Not enough free disk space for the update: about " + Util.FmtSize(need) +
                                    " is needed on the drive with the game, " + Util.FmtSize(free) + " is free.");
        }

        // os.replace that waits a moment and tries again when Windows (or a virus scanner looking at the file) holds it
        public static Action<string, string> ReplaceHook = null;            // tests only

        static void Move(string src, string dst)
        {
            if (ReplaceHook != null) { ReplaceHook(src, dst); return; }
            for (int i = 0; ; i++)
            {
                try
                {
                    Util.Replace(src, dst);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    if (i >= 5) throw;
                    try { if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal); } catch (Exception) { }
                    Thread.Sleep(500);
                }
            }
        }

        static string Full(string gameDir, string rel) { return Path.Combine(gameDir, rel.Replace('/', '\\')); }

        // the folder inside an unpacked update that is laid over the game folder
        static string UpdateRoot(string stage)
        {
            string exe = Archive.FindGameExe(stage);
            return exe != null ? Path.GetDirectoryName(exe) : stage;
        }

        // Files an update never replaces or removes: its own scratch folders, the launcher itself, and the
        // addons / addon settings in benilla-config (only new files are added there, never replaced).
        static bool Skip(string rel, string dest, bool deleting)
        {
            string low = rel.ToLowerInvariant();
            string top = low.Split('/')[0];
            if (top.StartsWith(".azeroth_") || low == "azeroth survivors launcher.exe") return true;
            try
            {
                if (string.Equals(Path.GetFullPath(dest), Path.GetFullPath(System.Windows.Forms.Application.ExecutablePath),
                                  StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception) { }
            if (top == "benilla-config") return deleting || File.Exists(dest);
            return false;
        }

        static void WriteJournal(string work, List<Dictionary<string, object>> entries)
        {
            string tmp = Path.Combine(work, "journal.tmp");
            var root = new Dictionary<string, object>();
            root["files"] = entries;
            byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(Json.Write(root, false));
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            Move(tmp, Path.Combine(work, "journal.json"));
        }

        // Puts every file an unfinished update touched back as it was, using the journal the update wrote first.
        public static bool Rollback(string gameDir)
        {
            string work = Path.Combine(gameDir, WorkDir);
            string journal = Path.Combine(work, "journal.json");
            if (!File.Exists(journal))
            {
                Util.DeleteTreeQuiet(work);                             // it never got as far as changing anything
                return true;
            }
            var entries = new List<KeyValuePair<string, bool>>();
            try
            {
                var files = Json.AsList(Json.Get(Json.AsDict(Json.Parse(File.ReadAllText(journal))), "files"));
                if (files == null) return false;
                foreach (object o in files)
                {
                    var e = Json.AsDict(o);
                    object ex = Json.Get(e, "existed");
                    if (e == null || !(ex is bool)) return false;
                    entries.Add(new KeyValuePair<string, bool>(PlainRel(Json.Str(e, "rel")), (bool)ex));
                }
            }
            catch (Exception) { return false; }
            bool ok = true;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                string dest = Full(gameDir, entries[i].Key), saved = Full(Path.Combine(work, "backup"), entries[i].Key);
                try
                {
                    if (File.Exists(saved))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        Move(saved, dest);
                    }
                    else if (!entries[i].Value && File.Exists(dest))
                    {
                        File.SetAttributes(dest, FileAttributes.Normal);
                        File.Delete(dest);
                    }
                }
                catch (Exception) { ok = false; }
            }
            if (ok) Util.DeleteTreeQuiet(work);
            return ok;
        }

        // Run when the launcher starts: an update that was cut off is undone. null, "restored" or "failed".
        public static string RecoverInterrupted(string gameDir)
        {
            foreach (string n in new[] { DownloadName, DownloadName + ".part" })
                try { File.Delete(Path.Combine(gameDir, n)); } catch (Exception) { }
            string work = Path.Combine(gameDir, WorkDir);
            if (!Directory.Exists(work)) return null;
            if (!File.Exists(Path.Combine(work, "journal.json")))
            {
                Util.DeleteTreeQuiet(work);
                return null;
            }
            return Rollback(gameDir) ? "restored" : "failed";
        }

        public class Result { public int Added, Replaced, Kept, Removed; }

        // Lays an update archive over the game folder, all or nothing.
        public static Result Apply(string archive, string gameDir, List<string> delete, Action<string> status)
        {
            if (status == null) status = delegate(string s) { };
            string kind = Archive.Kind(archive);
            if (kind == null) throw new UserError("The update file is not a .rar, .zip or .7z archive.");
            string work = Path.Combine(gameDir, WorkDir);
            if (File.Exists(Path.Combine(work, "journal.json")) && !Rollback(gameDir))
                throw new UserError("An earlier update was cut off and its files could not be put back yet.\n\n" +
                                    "The old files are kept in:\n" + Path.Combine(work, "backup"));
            Util.DeleteTreeQuiet(work);
            string stage = Path.Combine(work, "stage"), backup = Path.Combine(work, "backup");
            var plan = new List<string[]>();                        // { src, rel, dest }
            var gone = new List<string[]>();                        // { rel, path }
            int kept = 0, replaced = 0;
            try
            {
                Directory.CreateDirectory(stage);
                Directory.CreateDirectory(backup);
                status("Unpacking the update. This can take a few minutes...");
                Archive.Extract(archive, stage, kind);
                status("Installing the new files...");
                string root = Path.GetFullPath(UpdateRoot(stage)).TrimEnd('\\');
                var files = new List<string>(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
                files.Sort(StringComparer.Ordinal);
                foreach (string src in files)
                {
                    if (Util.IsLink(src)) continue;
                    string rel = src.Substring(root.Length + 1).Replace('\\', '/');
                    string dest = Full(gameDir, rel);
                    if (Skip(rel, dest, false)) kept++;
                    else plan.Add(new[] { src, rel, dest });
                }
                var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in plan) planned.Add(p[1]);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string x in delete ?? new List<string>())
                {
                    string rel = SafeRel(x);
                    if (planned.Contains(rel) || !seen.Add(rel)) continue;   // the update brings it itself / listed twice
                    string f = Full(gameDir, rel);
                    if (File.Exists(f) && !Util.IsLink(f) && !Skip(rel, f, true)) gone.Add(new[] { rel, f });
                }
                if (plan.Count == 0 && gone.Count == 0) throw new UserError("The update did not contain any files to install.");
                var entries = new List<Dictionary<string, object>>();
                foreach (var p in plan)
                {
                    var e = new Dictionary<string, object>();
                    e["rel"] = p[1];
                    e["existed"] = File.Exists(p[2]);
                    entries.Add(e);
                }
                foreach (var g in gone)
                {
                    var e = new Dictionary<string, object>();
                    e["rel"] = g[0];
                    e["existed"] = true;
                    entries.Add(e);
                }
                WriteJournal(work, entries);                        // from here on, a crash is recoverable
                foreach (var p in plan)
                {
                    string bak = Full(backup, p[1]);
                    if (File.Exists(p[2]))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(bak));
                        Move(p[2], bak);
                        replaced++;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(p[2]));
                    Move(p[0], p[2]);
                }
                foreach (var g in gone)
                {
                    string bak = Full(backup, g[0]);
                    Directory.CreateDirectory(Path.GetDirectoryName(bak));
                    Move(g[1], bak);
                }
                if (!File.Exists(Path.Combine(gameDir, Util.GameExe)))
                    throw new UserError(Util.GameExe + " is missing after the update.");
                // the commit: without journal.json nothing is ever rolled back, so this is the last step
                Move(Path.Combine(work, "journal.json"), Path.Combine(work, "journal.done"));
            }
            catch (Exception e)
            {
                bool undone = Rollback(gameDir);
                string text;
                if (e is UnauthorizedAccessException)
                    text = "A game file is in use. Close the game and anything else using its folder, then try again.";
                else if (e is DiskFullException)
                    text = "There is not enough free disk space for the update. Free some space and try again.";
                else if (e is UserError)
                    text = e.Message;
                else
                    text = "The update could not be installed (" + e.Message + ").";
                text += undone ? "\n\nNo game files were changed."
                               : "\n\nSome files could not be put back. The old versions are kept in:\n" + backup;
                throw new UserError(text, e);
            }
            Util.DeleteTreeQuiet(work);                             // the backup of the old files is no longer needed
            try { Directory.CreateDirectory(Path.Combine(gameDir, "benilla-config", "AddOns")); } catch (Exception) { }
            var r = new Result();
            r.Added = plan.Count - replaced;
            r.Replaced = replaced;
            r.Kept = kept;
            r.Removed = gone.Count;
            return r;
        }
    }
}
