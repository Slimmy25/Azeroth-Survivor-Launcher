// Long-term stats: the AS Time Tracker addon (built into the launcher, addons\ASTimeTracker) records every run in
// its saved-variable file, ASTimeTracker.lua in the game's addon settings folder (benilla-config\saved). This file
// reads that Lua table and installs the addon; the STATS section (Sections.cs) shows what it found, together with
// the launcher's own play time. Everything here is optional: the section can be turned off in Options and
// the addon is only installed when the player asks for it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AzerothLauncher
{
    // A small reader for the Lua that the game writes for saved variables: "Name = value" lines whose values
    // are tables, strings, numbers, true / false and nil. Tables become dictionaries; keys are strings (numbers
    // as "1", "2" ...), and entries without a key are numbered 1, 2 ... as in Lua.
    public class LuaReader
    {
        readonly string s;
        int pos;

        LuaReader(string text) { s = text ?? ""; }

        public static Dictionary<string, object> ParseFile(string text)
        {
            var r = new LuaReader(text);
            var top = new Dictionary<string, object>();
            while (true)
            {
                r.Ws();
                if (r.pos >= r.s.Length) break;
                string name = r.Name();
                if (name == null) throw new FormatException("Unexpected text at " + r.pos + ".");
                r.Ws();
                if (r.Peek() != '=') throw new FormatException("Expected = after " + name + ".");
                r.pos++;
                top[name] = r.Value();
                r.Ws();
                if (r.Peek() == ';') r.pos++;
            }
            return top;
        }

        char Peek() { return pos < s.Length ? s[pos] : '\0'; }

        void Ws()
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (char.IsWhiteSpace(c) || c == '﻿') { pos++; continue; }
                if (c == '-' && pos + 1 < s.Length && s[pos + 1] == '-')
                {
                    if (pos + 3 < s.Length && s[pos + 2] == '[' && s[pos + 3] == '[')      // --[[ block ]]
                    {
                        int end = s.IndexOf("]]", pos + 4, StringComparison.Ordinal);
                        pos = end < 0 ? s.Length : end + 2;
                        continue;
                    }
                    while (pos < s.Length && s[pos] != '\n') pos++;                     // -- to the end of the line
                    continue;
                }
                break;
            }
        }

        string Name()
        {
            int start = pos;
            while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '_')) pos++;
            if (pos == start || char.IsDigit(s[start])) { pos = start; return null; }
            return s.Substring(start, pos - start);
        }

        object Value()
        {
            Ws();
            char c = Peek();
            if (c == '{') return Table();
            if (c == '"' || c == '\'') return Str();
            if (c == '[' && pos + 1 < s.Length && (s[pos + 1] == '[' || s[pos + 1] == '=')) return LongStr();
            if (c == '-' || c == '.' || char.IsDigit(c)) return Number();
            string word = Name();
            if (word == "true") return true;
            if (word == "false") return false;
            if (word == "nil") return null;
            throw new FormatException("Unexpected value at " + pos + ".");
        }

        object Number()
        {
            int start = pos;
            if (Peek() == '-') pos++;
            if (pos + 1 < s.Length && s[pos] == '0' && (s[pos + 1] == 'x' || s[pos + 1] == 'X'))
            {
                pos += 2;
                int hs = pos;
                while (pos < s.Length && Uri.IsHexDigit(s[pos])) pos++;
                double hv = (double)long.Parse(s.Substring(hs, pos - hs), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return s[start] == '-' ? -hv : hv;
            }
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E' ||
                                      ((s[pos] == '+' || s[pos] == '-') && (s[pos - 1] == 'e' || s[pos - 1] == 'E')))) pos++;
            string t = s.Substring(start, pos - start);
            if (t == "-" ) throw new FormatException("A lone - at " + start + ".");
            string low = s.Substring(pos, Math.Min(4, s.Length - pos)).ToLowerInvariant();     // 1.#INF, -nan, inf
            if (low.StartsWith("#") || low.StartsWith("inf") || low.StartsWith("nan"))
            {
                while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '#')) pos++;
                return 0.0;
            }
            double v;
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = 0;
            return v;
        }

        string Str()
        {
            char q = s[pos++];
            var sb = new StringBuilder();
            while (pos < s.Length && s[pos] != q)
            {
                char c = s[pos++];
                if (c != '\\' || pos >= s.Length) { sb.Append(c); continue; }
                char e = s[pos++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'a': sb.Append('\a'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'v': sb.Append('\v'); break;
                    case '\n': sb.Append('\n'); break;
                    default:
                        if (char.IsDigit(e))                         // \ddd: a byte
                        {
                            int n = e - '0', k = 1;
                            while (k < 3 && pos < s.Length && char.IsDigit(s[pos])) { n = n * 10 + (s[pos++] - '0'); k++; }
                            sb.Append((char)Math.Min(n, 255));
                        }
                        else sb.Append(e);
                        break;
                }
            }
            pos++;                                               // the closing quote
            return Bytes(sb.ToString());
        }

        // the file is read as UTF-8; text written with \ddd escapes is turned back from bytes into UTF-8 too
        static string Bytes(string t)
        {
            bool high = false;
            foreach (char c in t) if (c > 127 && c < 256) { high = true; break; }
            if (!high) return t;
            foreach (char c in t) if (c > 255) return t;
            try
            {
                var b = new byte[t.Length];
                for (int i = 0; i < t.Length; i++) b[i] = (byte)t[i];
                return new UTF8Encoding(false, true).GetString(b);
            }
            catch (Exception) { return t; }
        }

        string LongStr()
        {
            int start = pos + 1, eq = 0;
            while (start < s.Length && s[start] == '=') { eq++; start++; }
            string close = "]" + new string('=', eq) + "]";
            start++;
            int end = s.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0) end = s.Length;
            pos = Math.Min(s.Length, end + close.Length);
            string t = s.Substring(start, end - start);
            return t.StartsWith("\n") ? t.Substring(1) : t;
        }

        Dictionary<string, object> Table()
        {
            pos++;                                               // {
            var t = new Dictionary<string, object>();
            int next = 1;
            while (true)
            {
                Ws();
                if (pos >= s.Length) throw new FormatException("A table is not closed.");
                if (Peek() == '}') { pos++; return t; }
                string key = null;
                if (Peek() == '[' && pos + 1 < s.Length && s[pos + 1] != '[' && s[pos + 1] != '=')
                {
                    pos++;
                    object k = Value();
                    Ws();
                    if (Peek() != ']') throw new FormatException("Expected ] at " + pos + ".");
                    pos++;
                    Ws();
                    if (Peek() != '=') throw new FormatException("Expected = at " + pos + ".");
                    pos++;
                    key = KeyText(k);
                }
                else
                {
                    int save = pos;
                    string name = Name();
                    Ws();
                    if (name != null && name != "true" && name != "false" && name != "nil" && Peek() == '=' &&
                        !(pos + 1 < s.Length && s[pos + 1] == '='))
                    {
                        pos++;
                        key = name;
                    }
                    else pos = save;
                }
                object v = Value();
                if (key == null) key = (next++).ToString(CultureInfo.InvariantCulture);
                if (v != null) t[key] = v;
                Ws();
                if (Peek() == ',' || Peek() == ';') pos++;
            }
        }

        static string KeyText(object k)
        {
            if (k is double)
            {
                double d = (double)k;
                if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(CultureInfo.InvariantCulture);
                return d.ToString("R", CultureInfo.InvariantCulture);
            }
            if (k is bool) return (bool)k ? "true" : "false";
            return Convert.ToString(k, CultureInfo.InvariantCulture) ?? "";
        }

        // ---- reading the parsed values
        public static Dictionary<string, object> Tab(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        public static double Num(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return 0;
            if (v is double) return (double)v;
            double r;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : 0;
        }

        public static string Text(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return "";
            if (v is double) return KeyText(v);
            return v as string ?? "";
        }

        // the entries 1, 2, 3 ... of a list table, in order
        public static List<Dictionary<string, object>> List(Dictionary<string, object> d)
        {
            var list = new List<Dictionary<string, object>>();
            if (d == null) return list;
            for (int i = 1; ; i++)
            {
                object v;
                if (!d.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out v)) break;
                var t = v as Dictionary<string, object>;
                if (t != null) list.Add(t);
            }
            return list;
        }
    }

    // one personal best or one class / race / zone line
    public class StatRecord
    {
        public string Key = "", Name = "", Hero = "", Class = "", Race = "", Zone = "", Date = "";
        public double Value, Runs, Wins, Time, Kills, BestTime, BestLevel;
    }

    public class StatRun
    {
        public string Result = "", Kind = "", Date = "", Hero = "", Class = "", Race = "", Zone = "";
        public double Time, Level, Kills, Damage, Gold, Bosses;
    }

    // what the AS Time Tracker addon has recorded
    public class RunStats
    {
        public string FilePath = "", First = "", Last = "";
        public DateTime Written;
        public double Runs, Victories, Defeats, Left, Time, Kills, Elites, Guardians, Bosses, Damage, Taken, Gold, Chests,
                      Magnets, Levels;
        public readonly Dictionary<string, StatRecord> Best = new Dictionary<string, StatRecord>();
        public readonly List<StatRecord> Classes = new List<StatRecord>(), Races = new List<StatRecord>(), Zones = new List<StatRecord>();
        public readonly List<StatRun> History = new List<StatRun>();     // the newest first
    }

    public static class Stats
    {
        public const string AddonFolder = "ASTimeTracker", DbName = "ASTimeTrackerDB";
        public const string FileName = AddonFolder + ".lua";
        static readonly string[] AddonFiles = { AddonFolder + ".toc", AddonFolder + ".lua" };
        // its first name, "Azeroth Stats": replaced (and its records carried over) when the new one is installed
        const string OldFolder = "AzerothStats", OldDbName = "AzerothStatsDB";

        // the newest ASTimeTracker.lua (else the first version's AzerothStats.lua) in the addon settings folder
        public static string FindFile(string savedDir)
        {
            return FindNamed(savedDir, FileName) ?? FindNamed(savedDir, OldFolder + ".lua");
        }

        // the newest file with this name anywhere in the folder (account or character sub-folders too)
        static string FindNamed(string savedDir, string fileName)
        {
            if (string.IsNullOrEmpty(savedDir) || !Directory.Exists(savedDir)) return null;
            string best = null;
            DateTime when = DateTime.MinValue;
            var stack = new Stack<KeyValuePair<string, int>>();
            stack.Push(new KeyValuePair<string, int>(savedDir, 0));
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                try
                {
                    foreach (string f in Directory.GetFiles(cur.Key))
                        if (string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            DateTime t = File.GetLastWriteTimeUtc(f);
                            if (best == null || t > when) { best = f; when = t; }
                        }
                    if (cur.Value < 6)
                        foreach (string d in Directory.GetDirectories(cur.Key))
                            if (!Util.IsLink(d)) stack.Push(new KeyValuePair<string, int>(d, cur.Value + 1));
                }
                catch (Exception) { }
            }
            return best;
        }

        // null when the addon has not saved anything yet; throws when the file cannot be read
        public static RunStats Load(string savedDir)
        {
            string path = FindFile(savedDir);
            if (path == null) return null;
            string text;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(fs, Encoding.UTF8)) text = r.ReadToEnd();
            var vars = LuaReader.ParseFile(text);
            var db = LuaReader.Tab(vars, DbName) ?? LuaReader.Tab(vars, OldDbName);
            if (db == null) return null;
            var st = new RunStats();
            st.FilePath = path;
            st.Written = File.GetLastWriteTime(path);
            st.First = LuaReader.Text(db, "first");
            st.Last = LuaReader.Text(db, "last");
            var t = LuaReader.Tab(db, "totals");
            st.Runs = LuaReader.Num(t, "runs");
            st.Victories = LuaReader.Num(t, "victory");
            st.Defeats = LuaReader.Num(t, "defeat");
            st.Left = LuaReader.Num(t, "left");
            st.Time = LuaReader.Num(t, "time");
            st.Kills = LuaReader.Num(t, "kills");
            st.Elites = LuaReader.Num(t, "elites");
            st.Guardians = LuaReader.Num(t, "guardians");
            st.Bosses = LuaReader.Num(t, "bosses");
            st.Damage = LuaReader.Num(t, "dmg");
            st.Taken = LuaReader.Num(t, "taken");
            st.Gold = LuaReader.Num(t, "gold");
            st.Chests = LuaReader.Num(t, "chests");
            st.Magnets = LuaReader.Num(t, "magnets");
            st.Levels = LuaReader.Num(t, "levels");
            var best = LuaReader.Tab(db, "best");
            if (best != null)
                foreach (var kv in best)
                {
                    var b = kv.Value as Dictionary<string, object>;
                    if (b == null) continue;
                    var rec = new StatRecord();
                    rec.Key = kv.Key;
                    rec.Value = LuaReader.Num(b, "v");
                    rec.Hero = LuaReader.Text(b, "hero");
                    rec.Class = LuaReader.Text(b, "cname");
                    rec.Race = LuaReader.Text(b, "rname");
                    rec.Zone = LuaReader.Text(b, "zname");
                    rec.Date = LuaReader.Text(b, "date");
                    st.Best[kv.Key] = rec;
                }
            Groups(LuaReader.Tab(db, "classes"), st.Classes);
            Groups(LuaReader.Tab(db, "races"), st.Races);
            Groups(LuaReader.Tab(db, "zones"), st.Zones);
            var hist = LuaReader.List(LuaReader.Tab(db, "history"));
            for (int i = hist.Count - 1; i >= 0; i--)
            {
                var h = hist[i];
                var run = new StatRun();
                run.Result = LuaReader.Text(h, "result");
                run.Kind = LuaReader.Text(h, "kind");
                run.Date = LuaReader.Text(h, "date");
                run.Hero = LuaReader.Text(h, "hero");
                run.Class = LuaReader.Text(h, "cname");
                run.Race = LuaReader.Text(h, "rname");
                run.Zone = LuaReader.Text(h, "zname");
                run.Time = LuaReader.Num(h, "t");
                run.Level = LuaReader.Num(h, "lvl");
                run.Kills = LuaReader.Num(h, "kills");
                run.Damage = LuaReader.Num(h, "dmg");
                run.Gold = LuaReader.Num(h, "gold");
                run.Bosses = LuaReader.Num(h, "bosses");
                st.History.Add(run);
            }
            return st;
        }

        static void Groups(Dictionary<string, object> d, List<StatRecord> into)
        {
            if (d == null) return;
            foreach (var kv in d)
            {
                var g = kv.Value as Dictionary<string, object>;
                if (g == null) continue;
                var rec = new StatRecord();
                rec.Key = kv.Key;
                rec.Name = LuaReader.Text(g, "name");
                if (rec.Name.Length == 0) rec.Name = Pretty(kv.Key);
                rec.Runs = LuaReader.Num(g, "runs");
                rec.Wins = LuaReader.Num(g, "wins");
                rec.Time = LuaReader.Num(g, "time");
                rec.Kills = LuaReader.Num(g, "kills");
                rec.BestTime = LuaReader.Num(g, "bestTime");
                rec.BestLevel = LuaReader.Num(g, "bestLvl");
                into.Add(rec);
            }
            into.Sort(delegate(StatRecord a, StatRecord b)
            {
                int c = b.Runs.CompareTo(a.Runs);
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        // "death_knight" -> "Death Knight"
        static string Pretty(string key)
        {
            var parts = key.Replace('_', ' ').Split(' ');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        // ---- the addon, built into the launcher
        static string Embedded(string file) { return Util.ReadResource("AS.addon." + file); }

        // the addon's folder in AddOns (also when it is turned off; else the first version's folder), or null
        public static string AddonPath(string addonsDir)
        {
            return Folder(addonsDir, AddonFolder) ?? Folder(addonsDir, OldFolder);
        }

        static string Folder(string addonsDir, string name)
        {
            if (string.IsNullOrEmpty(addonsDir)) return null;
            string on = Path.Combine(addonsDir, name), off = on + Addons.DisabledSuffix;
            if (Directory.Exists(on)) return on;
            if (Directory.Exists(off)) return off;
            return null;
        }

        public static bool AddonInstalled(string addonsDir) { return AddonPath(addonsDir) != null; }

        // installed, but not the version built into this launcher
        public static bool AddonOutdated(string addonsDir)
        {
            string dir = AddonPath(addonsDir);
            if (dir == null) return false;
            if (Folder(addonsDir, AddonFolder) == null) return true;     // still the old "Azeroth Stats"
            foreach (string f in AddonFiles)
            {
                string p = Path.Combine(dir, f);
                try
                {
                    if (!File.Exists(p) || Norm(File.ReadAllText(p, Encoding.UTF8)) != Norm(Embedded(f))) return true;
                }
                catch (Exception) { return true; }
            }
            return false;
        }

        static string Norm(string t) { return (t ?? "").Replace("\r\n", "\n").TrimEnd(); }

        // writes the addon into AddOns (or updates it where it is, turned off or not); its saved stats are kept.
        // The first version ("Azeroth Stats") is replaced: its folder goes, its records move to the new name.
        public static void InstallAddon(string addonsDir, string savedDir)
        {
            if (string.IsNullOrEmpty(addonsDir)) throw new UserError("The AddOns folder is not set. Set the game config folder in Options.");
            string old = Folder(addonsDir, OldFolder);
            string dir = Folder(addonsDir, AddonFolder);
            if (dir == null)                                 // keep it turned off if the old one was
                dir = Path.Combine(addonsDir, AddonFolder) + (old != null && old.EndsWith(Addons.DisabledSuffix) ? Addons.DisabledSuffix : "");
            Directory.CreateDirectory(dir);
            foreach (string f in AddonFiles)
            {
                string text = Embedded(f);
                if (text.Length == 0) throw new UserError("This launcher was built without the AS Time Tracker addon.");
                File.WriteAllText(Path.Combine(dir, f), text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
            }
            MoveOldRecords(savedDir);
            if (old != null) Util.DeleteTreeQuiet(old);
        }

        // AzerothStats.lua -> ASTimeTracker.lua next to it (with the variable renamed), where there is none yet
        static void MoveOldRecords(string savedDir)
        {
            if (string.IsNullOrEmpty(savedDir) || !Directory.Exists(savedDir)) return;
            string[] olds;
            try { olds = Directory.GetFiles(savedDir, OldFolder + ".lua", SearchOption.AllDirectories); }
            catch (Exception) { return; }
            foreach (string f in olds)
                try
                {
                    string target = Path.Combine(Path.GetDirectoryName(f), FileName);
                    if (File.Exists(target)) continue;
                    string text = File.ReadAllText(f, Encoding.UTF8);
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"^(\s*)" + OldDbName + @"\b", "$1" + DbName,
                                                                         System.Text.RegularExpressions.RegexOptions.Multiline);
                    File.WriteAllText(target, text, new UTF8Encoding(false));
                    File.Delete(f);
                }
                catch (Exception) { }
        }
    }
}
