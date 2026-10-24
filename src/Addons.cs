// Addons: switched off by renaming the folder to "<Name>.disabled" (the game no longer finds it) and on by
// renaming it back. Nothing is deleted, except by UNINSTALL.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AzerothLauncher
{
    public static class Addons
    {
        public const string DisabledSuffix = ".disabled";
        static readonly HashSet<string> UnsafeTops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "interface", "addons", "wtf", "data", "saved", "benilla-config", "fonts", "logs" };

        // every addon folder: name -> enabled, sorted by name (the game's own Blizzard_ folders are left out)
        public static List<KeyValuePair<string, bool>> Scan(string addonDir)
        {
            var found = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string d in Directory.GetDirectories(addonDir))
            {
                string name = Path.GetFileName(d);
                if (name.StartsWith("blizzard_", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.EndsWith(DisabledSuffix, StringComparison.Ordinal))
                {
                    string baseName = name.Substring(0, name.Length - DisabledSuffix.Length);
                    if (!found.ContainsKey(baseName)) found[baseName] = false;
                }
                else found[name] = true;                    // an enabled copy wins over a stray .disabled
            }
            var list = new List<KeyValuePair<string, bool>>(found);
            list.Sort(delegate(KeyValuePair<string, bool> a, KeyValuePair<string, bool> b)
            {
                return string.Compare(a.Key.ToLowerInvariant(), b.Key.ToLowerInvariant(), StringComparison.Ordinal);
            });
            return list;
        }

        public static void SetEnabled(string addonDir, string name, bool enabled)
        {
            string on = Path.Combine(addonDir, name), off = on + DisabledSuffix;
            if (enabled)
            {
                if (Directory.Exists(on)) return;
                if (!Directory.Exists(off)) throw new IOException(name + " was not found");
                Directory.Move(off, on);
            }
            else
            {
                if (!Directory.Exists(on))
                {
                    if (Directory.Exists(off)) return;
                    throw new IOException(name + " was not found");
                }
                if (Directory.Exists(off)) throw new IOException(name + DisabledSuffix + " already exists next to it");
                Directory.Move(on, off);
            }
        }

        // Everything in the addon settings folder that belongs to addon `folder`: its saved-variable files
        // (Name.lua, Name.lua.bak, ...) in any account or character sub-folder, and any folder named after it.
        static List<string> DataMatches(string savedDir, string folder)
        {
            var found = new List<string>();
            string key = folder.ToLowerInvariant();
            if (string.IsNullOrEmpty(savedDir) || !Directory.Exists(savedDir)) return found;
            var stack = new Stack<string>();
            stack.Push(savedDir);
            while (stack.Count > 0)
            {
                string cur = stack.Pop();
                foreach (string d in Directory.GetDirectories(cur))
                {
                    string low = Path.GetFileName(d).ToLowerInvariant();
                    if (low == key || low == key + DisabledSuffix) found.Add(d);     // its contents go with it
                    else if (!Util.IsLink(d)) stack.Push(d);
                }
                foreach (string f in Directory.GetFiles(cur))
                    if (Path.GetFileName(f).ToLowerInvariant().Split('.')[0] == key) found.Add(f);
            }
            return found;
        }

        static void Drop(string path)
        {
            if (Directory.Exists(path) && !Util.IsLink(path)) Util.DeleteTree(path);
            else if (Directory.Exists(path)) Directory.Delete(path);
            else
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        // Removes everything an addon put on the computer: each folder its .zip unpacked into addonDir, its
        // ".disabled" copy, and the settings it saved (in savedDir). Returns the names that were removed.
        public static List<string> Uninstall(string addonDir, List<string> names, string savedDir)
        {
            var removed = new List<string>();
            string root = Path.GetFullPath(addonDir).TrimEnd('\\');
            string savedRoot = string.IsNullOrEmpty(savedDir) ? null : Path.GetFullPath(savedDir).TrimEnd('\\');
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name == "." || name == ".." ||
                    name.StartsWith("blizzard_", StringComparison.OrdinalIgnoreCase) || UnsafeTops.Contains(name))
                    continue;
                foreach (string item in new[] { name, name + DisabledSuffix })
                {
                    string path = Path.Combine(root, item);
                    if ((Directory.Exists(path) || File.Exists(path)) &&
                        string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), root, StringComparison.OrdinalIgnoreCase))
                    {
                        Drop(path);
                        if (!removed.Contains(name)) removed.Add(name);
                    }
                }
                string baseName = name.EndsWith(DisabledSuffix) ? name.Substring(0, name.Length - DisabledSuffix.Length) : name;
                string ext = Path.GetExtension(baseName).ToLowerInvariant();
                if (ext == ".lua" || ext == ".xml" || ext == ".toc") baseName = Path.GetFileNameWithoutExtension(baseName);
                if (savedRoot != null)
                    foreach (string path in DataMatches(savedRoot, baseName))
                    {
                        string real = Path.GetFullPath(path);
                        if (real.StartsWith(savedRoot + "\\", StringComparison.OrdinalIgnoreCase) &&
                            (Directory.Exists(path) || File.Exists(path)))
                        {
                            Drop(path);
                            if (!removed.Contains(name)) removed.Add(name);
                        }
                    }
            }
            return removed;
        }

        public static Dictionary<string, object> MakeProfile(List<KeyValuePair<string, bool>> addons)
        {
            var on = new List<object>();
            var off = new List<object>();
            foreach (var a in addons) (a.Value ? on : off).Add(a.Key);
            var p = new Dictionary<string, object>();
            p["enabled"] = on;
            p["disabled"] = off;
            p["saved"] = DateTime.Now.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
            return p;
        }

        static HashSet<string> Names(object list)
        {
            var r = new HashSet<string>(StringComparer.Ordinal);
            var l = Json.AsList(list);
            if (l != null) foreach (object o in l) r.Add(Convert.ToString(o));
            return r;
        }

        public static int ProfileCount(Dictionary<string, object> profile, string key)
        {
            var l = Json.AsList(Json.Get(profile, key));
            return l == null ? 0 : l.Count;
        }

        // Applies a saved profile. Addons the profile does not know (installed later) are left alone.
        public static int ApplyProfile(string addonDir, Dictionary<string, object> profile, List<string> errors, List<string> unknown)
        {
            var wantOn = Names(Json.Get(profile, "enabled"));
            var wantOff = Names(Json.Get(profile, "disabled"));
            int changed = 0;
            foreach (var a in Scan(addonDir))
            {
                bool target;
                if (wantOn.Contains(a.Key) && !a.Value) target = true;
                else if (wantOff.Contains(a.Key) && a.Value) target = false;
                else
                {
                    if (!wantOn.Contains(a.Key) && !wantOff.Contains(a.Key)) unknown.Add(a.Key);
                    continue;
                }
                try
                {
                    SetEnabled(addonDir, a.Key, target);
                    changed++;
                }
                catch (Exception e) { errors.Add(a.Key + ": " + e.Message); }
            }
            return changed;
        }

        // The addon folders that exist for a Download More entry, enabled or switched off: what the launcher
        // recorded when it installed it, else a folder named like the entry (ignoring case, spaces and punctuation)
        // plus its "Name_..." module folders.
        public static List<string> FoldersOf(string addonDir, string name, List<string> recorded)
        {
            var result = new List<string>();
            List<KeyValuePair<string, bool>> present;
            try { present = Scan(addonDir); }
            catch (Exception) { return result; }
            var low = new Dictionary<string, string>();
            foreach (var p in present) low[p.Key.ToLowerInvariant()] = p.Key;
            if (recorded != null && recorded.Count > 0)
            {
                foreach (string w in recorded)
                {
                    string real;
                    if (low.TryGetValue(w.ToLowerInvariant(), out real)) result.Add(real);
                }
                return result;
            }
            string key = Norm(name);
            foreach (var p in present)
                if (key.Length > 0 && (Norm(p.Key) == key || p.Key.ToLowerInvariant().StartsWith(name.ToLowerInvariant() + "_")))
                    result.Add(p.Key);
            return result;
        }

        static string Norm(string t) { return Regex.Replace(t.ToLowerInvariant(), "[^a-z0-9]", ""); }
    }
}
