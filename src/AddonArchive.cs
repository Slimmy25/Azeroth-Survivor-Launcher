// Addon rollback: every Download More addon the launcher downloads is kept as its .zip in the launcher's own folder
// ("Addon Archive\<addon>\<date>.zip", the newest 10 per addon, an identical copy only once). From Addons › Advanced ›
// ROLLBACK the player can deliberately install an older copy: the addon's folders are replaced by that copy, while its
// saved settings and customization (in the game's saved-settings folder, never in the addon's own folder) stay as they
// are. A rolled-back addon is "pinned": the automatic updates leave it alone and My Addons shows [Outdated] beside it,
// until the player picks Newest again (or installs it from Download More).
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace AzerothLauncher
{
    public static class AddonArchive
    {
        const int Keep = 10;

        static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        public static string DirOf(string name) { return Path.Combine(Path.Combine(Util.DataDir, "Addon Archive"), Safe(name)); }

        // where a new download is kept (the time it was downloaded, in UTC)
        public static string NewPath(string name)
        {
            string d = DirOf(name);
            Directory.CreateDirectory(d);
            return Path.Combine(d, DateTime.UtcNow.ToString("yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture) + ".zip");
        }

        // the kept copies, newest first
        public static List<string> Versions(string name)
        {
            var l = new List<string>();
            try { if (Directory.Exists(DirOf(name))) l.AddRange(Directory.GetFiles(DirOf(name), "*.zip")); }
            catch (Exception) { }
            l.Sort(delegate(string a, string b) { return string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)); });
            return l;
        }

        // a copy's date as shown to the player (local time)
        public static string Label(string zip)
        {
            DateTime t;
            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(zip), "yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out t))
                return t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            return Path.GetFileNameWithoutExtension(zip);
        }

        static string Hash(string f)
        {
            using (var s = File.OpenRead(f))
            using (var h = SHA256.Create()) return Convert.ToBase64String(h.ComputeHash(s));
        }

        // after a download was kept: the same file as the newest copy before it is not kept twice; only the newest 10 stay
        public static void Tidy(string name)
        {
            try
            {
                var v = Versions(name);
                if (v.Count >= 2 && new FileInfo(v[0]).Length == new FileInfo(v[1]).Length && Hash(v[0]) == Hash(v[1]))
                {
                    File.Delete(v[0]);
                    v.RemoveAt(0);
                }
                for (int i = Keep; i < v.Count; i++) File.Delete(v[i]);
            }
            catch (Exception) { }
        }
    }
}
