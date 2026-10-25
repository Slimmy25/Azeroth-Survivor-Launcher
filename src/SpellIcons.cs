// Pictures for the STATS page, shown from Wowhead's image host (wow.zamimg.com) at runtime; nothing is built into
// the exe. Each picture is fetched only when a row that shows it is on screen, then kept in the launcher's temp
// folder so it is not downloaded again. Until it is there (or when it cannot be fetched) the row keeps its badge.
//   * spells: assets\spell-icons.txt names each spell's icon; a spell missing from it is looked up on Wowhead
//     (Classic first, then the current game) the first time it is shown, and the answer is remembered
//   * classes: Wowhead's class icons (classicon_mage ...)
//   * NPCs (Nemesis): the creature is looked up on Wowhead, and its model picture is shown
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
                    if (Regex.IsMatch(icon, "^[a-z0-9_-]+$")) names[line.Substring(0, bar).Trim()] = icon;
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

        // ---- what the rows ask for: the picture, or null for now (`repaint` is redrawn when it arrives)
        public static Image Spell(string spell, Control repaint)
        {
            Load();
            string name = Clean(spell);
            if (name.Length == 0) return null;
            string icon;
            if (names.TryGetValue(name, out icon)) return Icon(icon, repaint);
            return Lookup("spell:" + name, repaint, delegate { return FindSpellIcon(name); }, delegate(string ans) { return IconHost + ans + ".jpg"; });
        }

        public static Image Class(string classKey, Control repaint)
        {
            string k = Regex.Replace((classKey ?? "").ToLowerInvariant(), "[^a-z]", "");
            if (k == "deathknight") k = "deathknight";
            return k.Length == 0 ? null : Icon("classicon_" + k, repaint);
        }

        public static Image Icon(string icon, Control repaint)
        {
            if (string.IsNullOrEmpty(icon) || !Regex.IsMatch(icon, "^[a-z0-9_-]+$")) return null;
            return Picture("icon_" + icon, IconHost + icon + ".jpg", repaint);
        }

        // the model picture of a creature (Nemesis)
        public static Image Npc(string name, Control repaint)
        {
            Load();
            name = (name ?? "").Trim();
            if (name.Length == 0) return null;
            return Lookup("npc:" + name, repaint, delegate { return FindNpcDisplay(name); }, delegate(string d)
            {
                int id = int.Parse(d);
                return "https://wow.zamimg.com/modelviewer/classic/webthumbs/npc/" + (id & 255) + "/" + id + ".jpg";
            });
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

        static Image Picture(string key, string url, Control repaint)
        {
            lock (images)
            {
                Image img;
                if (images.TryGetValue(key, out img)) return img;
                if (repaint != null && !waiting.Contains(repaint)) waiting.Add(repaint);
                if (!loading.Add(key)) return null;
            }
            ThreadPool.QueueUserWorkItem(delegate { Fetch(key, url); });
            return null;
        }

        static void Fetch(string key, string url)
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
                        string.Equals(n, name, StringComparison.OrdinalIgnoreCase) && d.TryGetValue("icon", out icon) &&
                        Regex.IsMatch(icon, "^[a-z0-9_-]+$"))
                        return icon;
                }
            }
            return "";                                       // asked, and Wowhead has no such spell
        }

        // the screenshot tool: what the creature lookup sees
        public static string Explain(string name)
        {
            var sb = new StringBuilder();
            try
            {
                string json = Search("/classic", name);
                sb.Append("search " + json.Length + " chars; ");
                foreach (var d in Results(json))
                {
                    string t, n, id; d.TryGetValue("typeName", out t); d.TryGetValue("name", out n); d.TryGetValue("id", out id);
                    sb.Append("[" + t + " " + n + " " + id + "] ");
                }
                sb.Append(" => " + FindNpcDisplay(name));
            }
            catch (Exception ex) { sb.Append("error " + ex.Message); }
            return sb.ToString();
        }

        static string FindNpcDisplay(string name)
        {
            foreach (var d in Results(Search("/classic", name)))
            {
                string t, n, id;
                if (!d.TryGetValue("typeName", out t) || t != "NPC" || !d.TryGetValue("name", out n) ||
                    !string.Equals(n, name, StringComparison.OrdinalIgnoreCase) || !d.TryGetValue("id", out id)) continue;
                string page = GetText("https://www.wowhead.com/classic/npc=" + id);
                Match m = Regex.Match(page, @"data-mv-display-id=""(\d+)""");
                if (!m.Success) m = Regex.Match(page, @"displayId&quot;:(\d+)");
                if (m.Success) return m.Groups[1].Value;
            }
            return "";
        }
    }
}
