// Pictures for the STATS page, shown from Wowhead's image host (wow.zamimg.com) at runtime; nothing is built into
// the exe. Each picture is fetched only when a row that shows it is on screen, then kept in the launcher's temp
// folder so it is not downloaded again. Until it is there (or when it cannot be fetched) the row keeps its badge.
//   * spells: assets\spell-icons.txt names each spell's icon; a spell missing from it is looked up on Wowhead
//     (Classic first, then the current game) the first time it is shown, and the answer is remembered
//   * classes: Wowhead's class icons (classicon_mage ...)
//   * creatures (Nemesis): assets\npc-models.txt (from the CMaNGOS Classic database) gives the creature's model,
//     and Wowhead's picture of that model is shown
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public static class SpellIcons
    {
        const string IconHost = "https://wow.zamimg.com/images/wow/icons/large/";
        static Dictionary<string, string> names;              // lower-case spell name -> icon name (the list)
        static readonly Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Image> images = new Dictionary<string, Image>();   // picture key -> picture (null: failed)
        static readonly HashSet<string> loading = new HashSet<string>();
        static readonly List<Control> waiting = new List<Control>();

        static string CacheDir { get { return Path.Combine(Util.TempDir, "spell-icons"); } }
        static string FoundFile { get { return Path.Combine(CacheDir, "looked-up.txt"); } }   // name|answer, "" = none

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
                    if (Regex.IsMatch(icon, "^[a-z0-9_-]+$")) names[Key(line.Substring(0, bar))] = icon;
                }
            }
            catch (Exception) { }
            try
            {
                if (File.Exists(FoundFile))
                    foreach (string raw in File.ReadAllLines(FoundFile))
                    {
                        int bar = raw.LastIndexOf('|');
                        if (bar > 0) lock (found) found[raw.Substring(0, bar)] = raw.Substring(bar + 1);
                    }
            }
            catch (Exception) { }
        }

        // "Frostbolt (Rank 4)" and "Frostbolt 4" are Frostbolt
        static string Clean(string spell)
        {
            string s = Regex.Replace(spell ?? "", @"\s*\(.*?\)\s*$", "");
            return Regex.Replace(s, @"\s+\d+$", "").Trim();
        }

        // how names are matched: case, spaces, colons, dashes and apostrophes (straight or curly) do not count, so
        // "Shadow Word: Pain", "Shadow Word Pain" and "shadow_word_pain" are one spell; short forms count as the spell
        static readonly Dictionary<string, string> Short = new Dictionary<string, string> {
            { "swpain", "shadowwordpain" }, { "swp", "shadowwordpain" }, { "pwshield", "powerwordshield" }, { "pws", "powerwordshield" },
            { "swdeath", "shadowworddeath" }, { "pwfortitude", "powerwordfortitude" }, { "mark", "huntersmark" } };

        static string Key(string name)
        {
            string k = Regex.Replace(Clean(name).ToLowerInvariant(), "[^a-z0-9]", "");
            string full;
            return Short.TryGetValue(k, out full) ? full : k;
        }

        // ---- what the rows ask for: the picture, or null for now (`repaint` is redrawn when it arrives)
        public static Image Spell(string spell, Control repaint)
        {
            Load();
            string name = Clean(spell);
            if (name.Length == 0) return null;
            string icon;
            if (names.TryGetValue(Key(name), out icon)) return Icon(icon, repaint);
            return Lookup("spell:" + name, repaint, delegate { return FindSpellIcon(name); }, delegate(string ans) { return IconHost + ans + ".jpg"; });
        }

        public static Image Class(string classKey, Control repaint)
        {
            string k = Regex.Replace((classKey ?? "").ToLowerInvariant(), "[^a-z]", "");
            if (k == "deathknight") k = "deathknight";
            return k.Length == 0 ? null : Icon("classicon_" + k, repaint);
        }

        // races and zones (Classes page): Classic's race portraits, and each zone's own art from the achievements
        static readonly Dictionary<string, string> RaceIcons = new Dictionary<string, string> {
            { "human", "race_human_male" }, { "dwarf", "race_dwarf_male" }, { "nightelf", "race_nightelf_male" },
            { "gnome", "race_gnome_male" }, { "orc", "race_orc_male" }, { "undead", "race_scourge_male" },
            { "scourge", "race_scourge_male" }, { "tauren", "race_tauren_male" }, { "troll", "race_troll_male" } };
        static readonly Dictionary<string, string> ZoneIcons = new Dictionary<string, string> {
            { "elwynnforest", "achievement_zone_elwynnforest" }, { "duskwood", "achievement_zone_duskwood" },
            { "warsonggulch", "achievement_bg_captureflag_wsg" }, { "ungorocrater", "achievement_zone_ungorocrater_01" },
            { "arathibasin", "achievement_bg_winab" }, { "feralas", "achievement_zone_feralas" },
            { "gurubashiarena", "achievement_zone_stranglethorn_01" }, { "stranglethornvale", "achievement_zone_stranglethorn_01" },
            { "scarletmonastery", "spell_holy_righteousfury" }, { "alteracvalley", "achievement_bg_winav" },
            { "blackrockmountain", "achievement_zone_blackrock_01" }, { "gatesofahnqiraj", "achievement_zone_silithus_01" },
            { "ahnqiraj", "achievement_zone_silithus_01" }, { "naxxramas", "achievement_dungeon_naxxramas" } };

        public static Image Race(string race, Control repaint)
        {
            string icon;
            foreach (string part in (race ?? "").Split('|'))       // "key|Name": either one
                if (RaceIcons.TryGetValue(Key(part), out icon)) return Icon(icon, repaint);
            return null;
        }

        public static Image Zone(string zone, Control repaint)
        {
            string icon;
            foreach (string part in (zone ?? "").Split('|'))       // "key|Name": either one
                if (ZoneIcons.TryGetValue(Key(part), out icon)) return Icon(icon, repaint);
            return null;
        }

        public static Image Icon(string icon, Control repaint)
        {
            if (string.IsNullOrEmpty(icon) || !Regex.IsMatch(icon, "^[a-z0-9_-]+$")) return null;
            return Picture("icon_" + icon, IconHost + icon + ".jpg", repaint);
        }

        // the model picture of a creature (Nemesis): assets\npc-models.txt gives its model, Wowhead has the picture
        static Dictionary<string, int> npcModels;

        public static Image Npc(string name, Control repaint)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return null;
            if (npcModels == null)
            {
                npcModels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (string raw in Util.ReadResource("AS.npc-models.txt").Replace("\r\n", "\n").Split('\n'))
                    {
                        int bar = raw.LastIndexOf('|'), id;
                        if (raw.StartsWith("#") || bar <= 0 || !int.TryParse(raw.Substring(bar + 1), out id) || id <= 0) continue;
                        string n = raw.Substring(0, bar);
                        if (!npcModels.ContainsKey(n)) npcModels[n] = id;
                    }
                }
                catch (Exception) { }
            }
            int model;
            if (!npcModels.TryGetValue(name, out model)) return null;
            return Picture("npc_" + model, "https://wow.zamimg.com/modelviewer/classic/webthumbs/npc/" + (model & 255) + "/" + model + ".jpg", repaint, true);
        }

        // ---- a name that has to be looked up first; the answer (or "none") is remembered on disk
        static Image Lookup(string key, Control repaint, Func<string> find, Func<string, string> url)
        {
            string ans;
            bool known;
            lock (found) known = found.TryGetValue(key, out ans);
            if (known) return ans.Length == 0 ? null : Picture(key, url(ans), repaint);
            lock (images)
            {
                if (repaint != null && !waiting.Contains(repaint)) waiting.Add(repaint);
                if (!loading.Add("?" + key)) return null;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string a = null;
                try { a = find(); } catch (Exception) { }
                if (a != null)                               // null: could not ask (offline): try again next time
                {
                    lock (found) found[key] = a;
                    try
                    {
                        Directory.CreateDirectory(CacheDir);
                        lock (found) File.AppendAllText(FoundFile, key + "|" + a + "\r\n", Encoding.UTF8);
                    }
                    catch (Exception) { }
                }
                Done("?" + key, null, false);
            });
            return null;
        }

        static Image Picture(string key, string url, Control repaint) { return Picture(key, url, repaint, false); }

        static Image Picture(string key, string url, Control repaint, bool face)
        {
            lock (images)
            {
                Image img;
                if (images.TryGetValue(key, out img)) return img;
                if (repaint != null && !waiting.Contains(repaint)) waiting.Add(repaint);
                if (!loading.Add(key)) return null;
            }
            ThreadPool.QueueUserWorkItem(delegate { Fetch(key, url, face); });
            return null;
        }

        // a creature's picture cut to its face: find the creature (what differs from the plain background), then a
        // square at the top of it, around the head (a tall figure's head is small; a beast's fills more of it)
        static Bitmap Face(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            Color bg = src.GetPixel(2, 2);
            int minX = w, maxX = -1, minY = h, maxY = -1;
            Func<int, int, bool> body = delegate(int x, int y)
            {
                Color c = src.GetPixel(x, y);
                return Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G) + Math.Abs(c.B - bg.B) > 48;
            };
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                    if (body(x, y)) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) return src;
            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            int side = bh > bw * 1.15 ? (int)(bh * 0.30) : (int)(Math.Min(bw, bh) * 0.52);
            side = Math.Max(24, Math.Min(side, Math.Min(w, h)));
            // the head's middle: the middle of the creature in its top rows
            long sum = 0, n = 0;
            int band = Math.Max(4, side / 2);
            for (int y = minY; y < Math.Min(h, minY + band); y += 2)
                for (int x = minX; x <= maxX; x += 2)
                    if (body(x, y)) { sum += x; n++; }
            int cx = n > 0 ? (int)(sum / n) : (minX + maxX) / 2;
            int left = Math.Max(0, Math.Min(w - side, cx - side / 2));
            int top = Math.Max(0, Math.Min(h - side, minY + side / 24));   // a little below the very top: hair and crests
            var crop = new Bitmap(side, side);
            using (var g = Graphics.FromImage(crop))
                g.DrawImage(src, new Rectangle(0, 0, side, side), new Rectangle(left, top, side, side), GraphicsUnit.Pixel);
            return crop;
        }

        static void Fetch(string key, string url) { Fetch(key, url, false); }

        static void Fetch(string key, string url, bool face)
        {
            Image img = null;
            try
            {
                Directory.CreateDirectory(CacheDir);
                string path = Path.Combine(CacheDir, Regex.Replace(key, "[^A-Za-z0-9_.-]", "_") + ".jpg");
                if (!File.Exists(path))
                {
                    byte[] data = Get(url);
                    if (data != null && data.Length > 0)
                    {
                        File.WriteAllBytes(path + ".part", data);
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(path + ".part", path);
                    }
                }
                if (File.Exists(path))
                    using (var tmp = Image.FromFile(path)) img = new Bitmap(tmp);
                if (face && img != null)
                {
                    Bitmap f = Face((Bitmap)img);
                    if (f != img) { img.Dispose(); img = f; }
                }
            }
            catch (Exception) { }
            Done(key, img, true);
        }

        static void Done(string key, Image img, bool picture)
        {
            Control[] again;
            lock (images)
            {
                if (picture) images[key] = img;
                loading.Remove(key);
                again = waiting.ToArray();
                if (loading.Count == 0) waiting.Clear();
            }
            foreach (Control c in again)
                try { if (!c.IsDisposed && c.IsHandleCreated) c.BeginInvoke(new Action(c.Invalidate)); }
                catch (Exception) { }
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

        // ---- talking to Wowhead
        static byte[] Get(string url)
        {
            try
            {
                if ((int)ServicePointManager.SecurityProtocol != 0) ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            }
            catch (Exception) { }
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AzerothSurvivorsLauncher";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            req.AllowAutoRedirect = true;
            req.Accept = "text/html,application/json,image/*,*/*";
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        static string GetText(string url)
        {
            byte[] b = Get(url);
            return b == null ? "" : Encoding.UTF8.GetString(b);
        }

        // the search's results, one object at a time: { "type":6, "id":116, "name":"Frostbolt", "typeName":"Spell", "icon":"..." }
        static IEnumerable<Dictionary<string, string>> Results(string json)
        {
            foreach (Match m in Regex.Matches(json, @"\{[^{}]*""typeName""[^{}]*\}"))
            {
                var d = new Dictionary<string, string>();
                foreach (Match f in Regex.Matches(m.Value, @"""(\w+)"":(?:""((?:[^""\\]|\\.)*)""|(-?\d+))"))
                    d[f.Groups[1].Value] = f.Groups[2].Success && f.Groups[2].Length > 0 ? Regex.Unescape(f.Groups[2].Value) : f.Groups[3].Value;
                yield return d;
            }
        }

        static string Search(string site, string q)
        {
            return GetText("https://www.wowhead.com" + site + "/search/suggestions-template?q=" + Uri.EscapeDataString(q));
        }

        static string FindSpellIcon(string name)
        {
            foreach (string site in new[] { "/classic", "" })
            {
                foreach (var d in Results(Search(site, name)))
                {
                    string t, n, icon;
                    if (d.TryGetValue("typeName", out t) && t == "Spell" && d.TryGetValue("name", out n) &&
                        Key(n) == Key(name) && d.TryGetValue("icon", out icon) &&
                        Regex.IsMatch(icon, "^[a-z0-9_-]+$"))
                        return icon;
                }
            }
            return "";                                       // asked, and Wowhead has no such spell
        }
    }
}
