// The AZS addons the launcher carries inside itself (besides AZS Stats, Stats.cs): AZS Handbook and AZS Minor Mods.
// (AZS Transparency was a test and is gone: Retired lists it, and its folder is taken out of AddOns.) Download More lists them as "builtin:<folder>"; INSTALL writes their files from the launcher, and
// an installed one is brought up to the launcher's version right before the game starts (as AZS Stats is).
// Each one gets its own copy of AZSKit.lua, the look all AZS addons share (addons\AZSKit).
// AZS Handbook also gets AZSHandbookData.lua, made here from the launcher's own guide and talent trees, so the
// guide in game is always the one in the launcher.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AzerothLauncher
{
    public class BuiltinAddon
    {
        public string Folder;                                 // the folder in AddOns
        public string[] Files;                                // its own files (resources AS.addon.<Folder>.<file>)
        public string Generated;                              // a file the launcher makes (null: none)
    }

    public static class BuiltinAddons
    {
        public const string Prefix = "builtin:";
        public const string Kit = "AZSKit.lua";

        public static readonly BuiltinAddon[] All =
        {
            new BuiltinAddon { Folder = "AZSHandbook", Files = new[] { "AZSHandbook.toc", "AZSHandbook.lua" }, Generated = "AZSHandbookData.lua" },
            new BuiltinAddon { Folder = "AZSMinorMods", Files = new[] { "AZSMinorMods.toc", "AZSMinorMods.lua" } },
        };

        public static BuiltinAddon Find(string url)
        {
            if (url == null || !url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string f = url.Substring(Prefix.Length).Trim();
            foreach (var a in All) if (string.Equals(a.Folder, f, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        static IEnumerable<KeyValuePair<string, string>> Contents(BuiltinAddon a)
        {
            foreach (string f in a.Files) yield return new KeyValuePair<string, string>(f, Util.ReadResource("AS.addon." + a.Folder + "." + f));
            yield return new KeyValuePair<string, string>(Kit, Util.ReadResource("AS.addon.kit." + Kit));
            if (a.Generated != null)
                yield return new KeyValuePair<string, string>(a.Generated, HandbookData.Lua());
        }

        // its folder in AddOns, turned on or off, or null
        public static string PathOf(string addonsDir, BuiltinAddon a)
        {
            if (string.IsNullOrEmpty(addonsDir)) return null;
            string on = Path.Combine(addonsDir, a.Folder), off = on + Addons.DisabledSuffix;
            if (Directory.Exists(on)) return on;
            if (Directory.Exists(off)) return off;
            return null;
        }

        static string Norm(string t) { return (t ?? "").Replace("\r\n", "\n").TrimEnd(); }

        public static bool Outdated(string addonsDir, BuiltinAddon a)
        {
            string dir = PathOf(addonsDir, a);
            if (dir == null) return false;
            foreach (var kv in Contents(a))
            {
                string p = Path.Combine(dir, kv.Key);
                try { if (!File.Exists(p) || Norm(File.ReadAllText(p, Encoding.UTF8)) != Norm(kv.Value)) return true; }
                catch (Exception) { return true; }
            }
            return false;
        }

        // writes it into AddOns (where it is, turned off or not); its saved settings are kept
        public static void Install(string addonsDir, BuiltinAddon a)
        {
            if (string.IsNullOrEmpty(addonsDir)) throw new UserError("The AddOns folder is not set. Set the game config folder in Options.");
            string dir = PathOf(addonsDir, a) ?? Path.Combine(addonsDir, a.Folder);
            Directory.CreateDirectory(dir);
            foreach (var kv in Contents(a))
            {
                if (kv.Value.Length == 0) throw new UserError("This launcher was built without " + a.Folder + ".");
                File.WriteAllText(Path.Combine(dir, kv.Key), kv.Value.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
            }
        }

        // the installed ones that are not the launcher's version, brought up to it (before the game starts)
        // addons that are gone from the launcher: their folders (on or off) come out of AddOns
        public static readonly string[] Retired = { "AZSTransparency" };

        public static List<string> RemoveRetired(string addonsDir, string savedDir)
        {
            var gone = new List<string>();
            if (string.IsNullOrEmpty(addonsDir) || !Directory.Exists(addonsDir)) return gone;
            foreach (string r in Retired)
                if (Directory.Exists(Path.Combine(addonsDir, r)) || Directory.Exists(Path.Combine(addonsDir, r + Addons.DisabledSuffix)))
                    gone.AddRange(Addons.Uninstall(addonsDir, new List<string> { r }, savedDir));
            return gone;
        }

        public static List<string> UpdateInstalled(string addonsDir)
        {
            var done = new List<string>();
            foreach (var a in All)
                if (PathOf(addonsDir, a) != null && Outdated(addonsDir, a)) { Install(addonsDir, a); done.Add(a.Folder); }
            return done;
        }
    }

    // AZSHandbookData.lua: the launcher's guide (assets\guide.txt) and talent trees (assets\talents.txt) as Lua tables,
    // with the spells' icons and the talents that change each spell, for AZS Handbook in game
    public static class HandbookData
    {
        static string cached;

        static string Q(string s)
        {
            var b = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '\\') b.Append("\\\\");
                else if (c == '"') b.Append("\\\"");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') continue;
                else if (c == '·') b.Append("-");           // the game's fonts may not have it
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }

        static void Page(StringBuilder s, GuidePage p, string indent)
        {
            s.Append(indent).Append("title = ").Append(Q(p.Title)).Append(", desc = ").Append(Q(p.Desc)).Append(",\n");
            s.Append(indent).Append("body = {\n");
            foreach (string raw in p.Body.Split('\n')) s.Append(indent).Append("\t").Append(Q(raw.Trim())).Append(",\n");
            s.Append(indent).Append("},\n");
            s.Append(indent).Append("videos = {");
            foreach (var v in p.Videos) s.Append(" ").Append(Q(v.Title + (v.Author.Length > 0 ? " (video by " + v.Author + ")" : ""))).Append(",");
            s.Append(" },\n");
        }

        public static string Lua()
        {
            if (cached != null) return cached;
            var s = new StringBuilder();
            s.Append("-- AZS Handbook: the launcher's New Player Guide and talent trees, written by the launcher when it installs or\n");
            s.Append("-- updates the addon (so the guide in game is always the launcher's). Do not edit: the launcher writes it again.\n");
            s.Append("AZSHANDBOOK_DATA = {\n");
            s.Append("topics = {\n");
            List<GuideTopic> topics;
            try { topics = Data.Guide; } catch (Exception) { topics = new List<GuideTopic>(); }
            foreach (var t in topics)
            {
                s.Append("{\n");
                Page(s, t, "\t");
                s.Append("\tpages = {\n");
                foreach (var p in t.Pages) { s.Append("\t\t{\n"); Page(s, p, "\t\t\t"); s.Append("\t\t},\n"); }
                s.Append("\t},\n},\n");
            }
            s.Append("},\n");
            // the talent trees: per class, three trees of { name, icon, row, column, ranks, text, { spells it changes } }
            s.Append("talents = {\n");
            var classes = new List<string>();
            foreach (var tal in Talents.All()) if (!classes.Contains(tal.Class)) classes.Add(tal.Class);
            foreach (string c in classes)
            {
                s.Append("[").Append(Q(c)).Append("] = {\n");
                foreach (var tr in Talents.Of(c))
                {
                    s.Append("\t{ spec = ").Append(Q(tr.Spec)).Append(", t = {\n");
                    foreach (var tal in tr.Talents)
                    {
                        s.Append("\t\t{ ").Append(Q(tal.Name)).Append(", ").Append(Q(tal.Icon)).Append(", ").Append(tal.Row).Append(", ")
                         .Append(tal.Col).Append(", ").Append(tal.Ranks).Append(", ").Append(Q(tal.Shown)).Append(", {");
                        foreach (string ch in tal.Changes) s.Append(" ").Append(Q(ch)).Append(",");
                        s.Append(" }, ").Append(tal.Milestone ? "true" : "false").Append(" },\n");
                    }
                    s.Append("\t} },\n");
                }
                s.Append("},\n");
            }
            s.Append("},\n");
            // every spell's icon by its name without case, spaces or punctuation (as AZS Stats matches them)
            s.Append("icons = {\n");
            var seen = new HashSet<string>();
            foreach (string raw in Util.ReadResource("AS.spell-icons.txt").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                int bar = line.IndexOf('|');
                if (line.StartsWith("#") || bar <= 0) continue;
                string k = Key(line.Substring(0, bar));
                if (k.Length == 0 || !seen.Add(k)) continue;
                s.Append("\t[").Append(Q(k)).Append("] = ").Append(Q(line.Substring(bar + 1).Trim())).Append(",\n");
            }
            foreach (var tal in Talents.All())
            {
                string k = Key(tal.Name);
                if (k.Length > 0 && seen.Add(k)) s.Append("\t[").Append(Q(k)).Append("] = ").Append(Q(tal.Icon)).Append(",\n");
            }
            s.Append("},\n");
            // the names to highlight in the text: every spell and talent, the longest first
            var names = new List<string>();
            foreach (string raw in Util.ReadResource("AS.spell-icons.txt").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                int bar = line.IndexOf('|');
                if (line.StartsWith("#") || bar <= 0) continue;
                string n = line.Substring(0, bar).Trim();
                if (n.Length >= 4 && !names.Contains(n)) names.Add(n);
            }
            foreach (var tal in Talents.All()) if (tal.Name.Length >= 4 && !names.Contains(tal.Name)) names.Add(tal.Name);
            names.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            s.Append("names = {");
            foreach (string n in names) s.Append(" ").Append(Q(n)).Append(",");
            s.Append(" },\n");
            s.Append("};\n");
            cached = s.ToString();
            return cached;
        }

        public static string Key(string name)
        {
            var b = new StringBuilder();
            foreach (char c in (name ?? "").ToLowerInvariant()) if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) b.Append(c);
            return b.ToString();
        }
    }
}
