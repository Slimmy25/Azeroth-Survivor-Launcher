// Spell icons for the STATS page: assets\spell-icons.txt names each spell's icon on Wowhead, and the picture is
// shown from Wowhead's image host (wow.zamimg.com) at runtime. Nothing is built into the exe; a picture that was
// fetched is kept in the launcher's temp folder so it is not downloaded again. Until a picture is there (or when
// it cannot be fetched) the spell keeps its lettered badge.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public static class SpellIcons
    {
        const string Host = "https://wow.zamimg.com/images/wow/icons/large/";
        static Dictionary<string, string> names;              // lower-case spell name -> icon name
        static readonly Dictionary<string, Image> images = new Dictionary<string, Image>();   // icon name -> picture (null: failed)
        static readonly HashSet<string> loading = new HashSet<string>();
        static readonly List<Control> waiting = new List<Control>();

        static void Load()
        {
            if (names != null) return;
            names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string raw in Util.ReadResource("AS.spell-icons.txt").Replace("\r\n", "\n").Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int bar = line.LastIndexOf('|');
                    if (bar <= 0) continue;
                    string icon = line.Substring(bar + 1).Trim().ToLowerInvariant();
                    if (Regex.IsMatch(icon, "^[a-z0-9_]+$")) names[line.Substring(0, bar).Trim()] = icon;
                }
            }
            catch (Exception) { }
        }

        public static bool Has(string spell)
        {
            Load();
            return !string.IsNullOrEmpty(spell) && names.ContainsKey(Clean(spell));
        }

        // "Frostbolt (Rank 4)" and "Frostbolt 4" are Frostbolt
        static string Clean(string spell)
        {
            string s = Regex.Replace(spell ?? "", @"\s*\(.*?\)\s*$", "");
            return Regex.Replace(s, @"\s+\d+$", "").Trim();
        }

        // the spell's picture, or null for now (`repaint` is redrawn when it arrives)
        public static Image Get(string spell, Control repaint)
        {
            Load();
            string icon;
            if (string.IsNullOrEmpty(spell) || !names.TryGetValue(Clean(spell), out icon)) return null;
            lock (images)
            {
                Image img;
                if (images.TryGetValue(icon, out img)) return img;
                if (repaint != null && !waiting.Contains(repaint)) waiting.Add(repaint);
                if (!loading.Add(icon)) return null;
            }
            ThreadPool.QueueUserWorkItem(delegate { Fetch(icon); });
            return null;
        }

        // the screenshot tool: wait until every picture asked for has arrived (or failed)
        public static void WaitForAll(int ms)
        {
            var until = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < until)
            {
                lock (images) if (loading.Count == 0) return;
                Application.DoEvents();
                Thread.Sleep(50);
            }
        }

        static void Fetch(string icon)
        {
            Image img = null;
            try
            {
                string dir = Path.Combine(Util.TempDir, "spell-icons");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, icon + ".jpg");
                if (!File.Exists(path))
                    using (var resp = Net.Open(Host + icon + ".jpg", 10000, true, "spell icon"))
                    using (Stream s = resp.GetResponseStream())
                    using (var fs = File.Create(path + ".part"))
                    {
                        s.CopyTo(fs);
                        fs.Close();
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(path + ".part", path);
                    }
                if (File.Exists(path))
                    using (var tmp = Image.FromFile(path)) img = new Bitmap(tmp);
            }
            catch (Exception) { }
            Control[] again;
            lock (images)
            {
                images[icon] = img;
                loading.Remove(icon);
                again = waiting.ToArray();
                if (loading.Count == 0) waiting.Clear();
            }
            foreach (Control c in again)
                try { if (!c.IsDisposed && c.IsHandleCreated) c.BeginInvoke(new Action(c.Invalidate)); }
                catch (Exception) { }
        }
    }
}
