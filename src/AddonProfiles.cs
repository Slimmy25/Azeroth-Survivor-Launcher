// Addon profiles' own game preferences. Profile 1 and Profile 2 each keep a copy of the game's preferences (the
// benilla-config folder: options, key bindings, chat windows, camera, layout and the addons' saved settings), so a
// heavy UI addon can change them without leaving them set in a way that makes the next UI addon look wrong. "No
// profile" (the default, recommended) keeps the game's own shared preferences: while it is in use, nothing is copied.
// Switching stores the current preferences under the profile being left, then puts back the ones of the profile
// being picked (a profile picked for the first time starts from the current ones). Nothing is ever lost: what is
// replaced was stored first.
// Never copied or replaced: the AddOns folder (the profile's switches do that), Logs, the save game, the launcher's
// own folder and the run histories (AZS Stats, and Maidenhead's Azeroth Stats), which stay shared so no run is lost or split.
// Copies live in the launcher's folder ("Addon Profiles"), only on this PC; cloud saves are never touched (the next
// sync simply uploads whatever addon settings are in place, as always).
using System;
using System.Collections.Generic;
using System.IO;

namespace AzerothLauncher
{
    public static class AddonProfiles
    {
        public static string StoreOf(string profile)
        {
            return Path.Combine(Path.Combine(Util.DataDir, "Addon Profiles"), profile == "none" ? "No profile" : "Profile " + profile);
        }

        // the folders that hold the preferences: benilla-config, and the saved-settings folder when it is elsewhere
        static List<KeyValuePair<string, string>> Roots(SyncSettings ss)
        {
            var l = new List<KeyValuePair<string, string>>();
            // a folder that holds the game itself is never treated as preferences (a config folder set to the game's own)
            if (!string.IsNullOrEmpty(ss.ConfigDir) && Directory.Exists(ss.ConfigDir) && !File.Exists(Path.Combine(ss.ConfigDir, Util.GameExe))) l.Add(new KeyValuePair<string, string>("config", ss.ConfigDir));
            if (!string.IsNullOrEmpty(ss.SavedDir) && Directory.Exists(ss.SavedDir) && !Under(ss.SavedDir, ss.ConfigDir))
                l.Add(new KeyValuePair<string, string>("saved", ss.SavedDir));
            return l;
        }

        static bool Under(string path, string dir)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dir)) return false;
            string p = Path.GetFullPath(path).TrimEnd('\\', '/') + "\\", d = Path.GetFullPath(dir).TrimEnd('\\', '/') + "\\";
            return p.StartsWith(d, StringComparison.OrdinalIgnoreCase);
        }

        // what a profile never copies or replaces
        static bool Skip(SyncSettings ss, string root, string file)
        {
            string rel = file.Substring(root.Length).TrimStart('\\', '/');
            string top = rel.Split('\\', '/')[0];
            if (top.Equals("AddOns", StringComparison.OrdinalIgnoreCase) || top.Equals("Logs", StringComparison.OrdinalIgnoreCase)) return true;
            if (Under(file, ss.AddonsDir) || Under(file, ss.SaveDir) || Under(file, Util.DataDir)) return true;
            string name = Path.GetFileName(file);
            return name.StartsWith("AZSStats", StringComparison.OrdinalIgnoreCase) || name.StartsWith("AzerothStats", StringComparison.OrdinalIgnoreCase);
        }

        static IEnumerable<string> Files(SyncSettings ss, string root)
        {
            foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (!Skip(ss, root, f)) yield return f;
        }

        // the current preferences, stored under a profile (its old copy is replaced)
        public static void Store(SyncSettings ss, string profile)
        {
            string dest = StoreOf(profile), tmp = dest + ".new";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            foreach (var r in Roots(ss))
                foreach (string f in new List<string>(Files(ss, r.Value)))
                {
                    string to = Path.Combine(Path.Combine(tmp, r.Key), f.Substring(r.Value.Length).TrimStart('\\', '/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(f, to, true);
                }
            Directory.CreateDirectory(tmp);
            if (Directory.Exists(dest)) Directory.Delete(dest, true);   // only once the new copy is complete
            Directory.Move(tmp, dest);
        }

        public static bool Has(string profile) { return Directory.Exists(StoreOf(profile)); }

        // puts a profile's preferences back: what the game has now is replaced by the stored copy (call Store first)
        public static void Restore(SyncSettings ss, string profile)
        {
            string src = StoreOf(profile);
            if (!Directory.Exists(src)) return;
            foreach (var r in Roots(ss))
            {
                foreach (string f in new List<string>(Files(ss, r.Value))) File.Delete(f);
                string from = Path.Combine(src, r.Key);
                if (!Directory.Exists(from)) continue;
                foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                {
                    string to = Path.Combine(r.Value, f.Substring(from.Length).TrimStart('\\', '/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(f, to, true);
                }
            }
        }
    }
}
