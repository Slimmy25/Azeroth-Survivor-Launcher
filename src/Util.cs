// Small building blocks: JSON, the settings file, folders, the built-in data (changelog, addon list, About).
// Written for the C# 5 compiler that comes with Windows (no newer syntax), so build.bat needs nothing installed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AzerothLauncher
{
    // ------------------------------------------------------------------ JSON
    // Objects become Dictionary<string, object>, arrays List<object>, numbers long or double.
    public static class Json
    {
        public static object Parse(string text)
        {
            var p = new JsonParser(text);
            p.SkipWs();
            object v = p.Value();
            p.SkipWs();
            if (p.Pos != text.Length) throw new FormatException("Unexpected text after the JSON value.");
            return v;
        }

        public static string Write(object value, bool indent)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, indent, 0);
            return sb.ToString();
        }

        static void NewLine(StringBuilder sb, bool indent, int depth)
        {
            if (!indent) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        static void WriteValue(StringBuilder sb, object v, bool indent, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong)
            {
                sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (v is double || v is float || v is decimal)
            {
                sb.Append(Convert.ToDouble(v).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(indent ? "," : ", ");
                    first = false;
                    NewLine(sb, indent, depth + 1);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    WriteValue(sb, kv.Value, indent, depth + 1);
                }
                NewLine(sb, indent, depth);
                sb.Append('}');
                return;
            }
            var list = v as System.Collections.IEnumerable;
            if (list != null)
            {
                var items = new List<object>();
                foreach (object o in list) items.Add(o);
                if (items.Count == 0) { sb.Append("[]"); return; }
                sb.Append('[');
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) sb.Append(indent ? "," : ", ");
                    NewLine(sb, indent, depth + 1);
                    WriteValue(sb, items[i], indent, depth + 1);
                }
                NewLine(sb, indent, depth);
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c > 0x7e) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        class JsonParser
        {
            readonly string s;
            public int Pos;
            public JsonParser(string text) { s = text; Pos = 0; if (s.Length > 0 && s[0] == '﻿') Pos = 1; }

            public void SkipWs()
            {
                while (Pos < s.Length && (s[Pos] == ' ' || s[Pos] == '\t' || s[Pos] == '\n' || s[Pos] == '\r')) Pos++;
            }

            Exception Bad(string what) { return new FormatException("JSON: " + what + " at position " + Pos); }

            public object Value()
            {
                if (Pos >= s.Length) throw Bad("value expected");
                char c = s[Pos];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == 't' && Lit("true")) return true;
                if (c == 'f' && Lit("false")) return false;
                if (c == 'n' && Lit("null")) return null;
                if (c == '-' || (c >= '0' && c <= '9')) return Num();
                throw Bad("unexpected character '" + c + "'");
            }

            bool Lit(string word)
            {
                if (string.CompareOrdinal(s, Pos, word, 0, word.Length) == 0) { Pos += word.Length; return true; }
                return false;
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                Pos++;
                SkipWs();
                if (Pos < s.Length && s[Pos] == '}') { Pos++; return d; }
                while (true)
                {
                    SkipWs();
                    if (Pos >= s.Length || s[Pos] != '"') throw Bad("name expected");
                    string k = Str();
                    SkipWs();
                    if (Pos >= s.Length || s[Pos] != ':') throw Bad("':' expected");
                    Pos++;
                    SkipWs();
                    d[k] = Value();
                    SkipWs();
                    if (Pos < s.Length && s[Pos] == ',') { Pos++; continue; }
                    if (Pos < s.Length && s[Pos] == '}') { Pos++; return d; }
                    throw Bad("',' or '}' expected");
                }
            }

            List<object> Arr()
            {
                var l = new List<object>();
                Pos++;
                SkipWs();
                if (Pos < s.Length && s[Pos] == ']') { Pos++; return l; }
                while (true)
                {
                    SkipWs();
                    l.Add(Value());
                    SkipWs();
                    if (Pos < s.Length && s[Pos] == ',') { Pos++; continue; }
                    if (Pos < s.Length && s[Pos] == ']') { Pos++; return l; }
                    throw Bad("',' or ']' expected");
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                Pos++;
                while (true)
                {
                    if (Pos >= s.Length) throw Bad("unterminated string");
                    char c = s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (Pos >= s.Length) throw Bad("bad escape");
                    char e = s[Pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (Pos + 4 > s.Length) throw Bad("bad \\u escape");
                            sb.Append((char)int.Parse(s.Substring(Pos, 4), NumberStyles.HexNumber));
                            Pos += 4;
                            break;
                        default: throw Bad("bad escape");
                    }
                }
            }

            object Num()
            {
                int start = Pos;
                if (s[Pos] == '-') Pos++;
                while (Pos < s.Length && "0123456789+-.eE".IndexOf(s[Pos]) >= 0) Pos++;
                string t = s.Substring(start, Pos - start);
                long l;
                if (long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l)) return l;
                double d;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                throw Bad("bad number");
            }
        }

        // ---- helpers for reading loosely typed JSON ----
        public static Dictionary<string, object> AsDict(object o)
        {
            return o as Dictionary<string, object>;
        }

        public static List<object> AsList(object o)
        {
            return o as List<object>;
        }

        public static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(Dictionary<string, object> d, string key)
        {
            object v = Get(d, key);
            if (v == null) return "";
            if (v is bool) return (bool)v ? "True" : "False";
            if (v is double) return ((double)v).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }
    }

    // ------------------------------------------------------------------ the settings file
    // <launcher folder>\Azeroth Survivor Launcher\config.json (same keys as the Python launcher used).
    public class Config
    {
        public static string AppDir { get { return Util.DataDir; } }
        public static string FilePath { get { return Path.Combine(AppDir, "config.json"); } }

        public Dictionary<string, object> Data;

        public static Config Load()
        {
            var c = new Config();
            try
            {
                c.Data = Json.AsDict(Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8)));
            }
            catch (Exception)
            {
                c.Data = null;
            }
            if (c.Data == null) c.Data = new Dictionary<string, object>();
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                Util.WriteAtomic(FilePath, Json.Write(Data, true));
            }
            catch (Exception) { }
        }

        public bool Has(string key) { return Data.ContainsKey(key); }

        public string Str(string key)
        {
            object v;
            if (!Data.TryGetValue(key, out v) || v == null) return "";
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public bool Bool(string key, bool def)
        {
            object v;
            if (!Data.TryGetValue(key, out v) || v == null) return def;
            if (v is bool) return (bool)v;
            if (v is long) return (long)v != 0;
            string s = v as string;
            return s != null ? s.Length > 0 : def;
        }

        public void Set(string key, object value) { Data[key] = value; }
        public void Remove(string key) { Data.Remove(key); }

        // a nested object, created when asked for
        public Dictionary<string, object> Section(string key)
        {
            var d = Json.AsDict(Json.Get(Data, key));
            if (d == null)
            {
                d = new Dictionary<string, object>();
                Data[key] = d;
            }
            return d;
        }
    }

    // ------------------------------------------------------------------ folders and files
    public static class Util
    {
        public const string GameExe = "AzerothSurvivors.exe";
        public const string SaveFolderName = "wenilla-rogue";

        public static string LauncherDir
        {
            get { return Path.GetDirectoryName(Path.GetFullPath(Application.ExecutablePath)); }
        }

        public static string AppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
        }

        public static string LocalAppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        }

        // Everything the launcher writes for itself (settings, sync memory, log, backups, kept downloads, temporary
        // files) goes into one folder next to the exe. Only when that folder can't be written (for example the exe
        // sits in Program Files) is %LOCALAPPDATA%\Azeroth Survivor Launcher used instead.
        public const string DataFolderName = "Azeroth Survivor Launcher";
        static string dataDir;
        static bool dataBesideExe;
        static readonly object dataLock = new object();

        public static string DataDir { get { InitData(); return dataDir; } }
        public static bool DataBesideExe { get { InitData(); return dataBesideExe; } }
        public static string StateDir { get { return DataDir; } }
        public static string TempDir { get { return Path.Combine(DataDir, "temp"); } }

        static void InitData()
        {
            lock (dataLock)
            {
                if (dataDir != null) return;
                string beside = Path.Combine(LauncherDir, DataFolderName);
                if (!InCloudDrive(LauncherDir) && Writable(beside))
                {
                    dataDir = beside;
                    dataBesideExe = true;
                }
                else
                {
                    dataDir = Path.Combine(LocalAppData, DataFolderName);
                    try { Directory.CreateDirectory(dataDir); } catch (Exception) { }
                }
                try { MoveOldData(dataDir); } catch (Exception) { }
                try { WriteCredits(dataDir); } catch (Exception) { }
                try { DeleteTreeQuiet(Path.Combine(dataDir, "temp")); } catch (Exception) { }
            }
        }

        // A launcher kept inside Google Drive, OneDrive, Dropbox or iCloud would have its sync memory copied to the
        // other PCs, which breaks the "what changed since the last sync on this PC" check. Its files go to
        // %LOCALAPPDATA% then. (A quick name check: looking for the cloud apps themselves can hang on network drives.)
        static bool InCloudDrive(string dir)
        {
            try
            {
                string full = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
                foreach (string v in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
                {
                    string root = Environment.GetEnvironmentVariable(v);
                    if (!string.IsNullOrEmpty(root) &&
                        full.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
                }
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "google drive", "my drive", "meine ablage",
                    "mi unidad", "mon drive", "il mio drive", "mijn drive", "meu drive", "mój dysk", "мой диск", "dropbox",
                    "onedrive", "icloud drive", "iclouddrive" };
                foreach (string part in full.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
                    if (names.Contains(part) || part.StartsWith("OneDrive - ", StringComparison.OrdinalIgnoreCase) ||
                        part.StartsWith("Dropbox (", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception) { }
            return false;
        }

        // Credits.txt in the launcher's folder: rewritten whenever it is missing or differs from this text.
        const string CreditsText =
            "Azeroth Survivors Launcher - Credits\r\n" +
            "====================================\r\n" +
            "\r\n" +
            "Made by Slimjim.\r\n" +
            "\r\n" +
            "This launcher was vibe coded: Slimjim led the way and shaped every feature, and the code was written\r\n" +
            "together with an AI assistant.\r\n" +
            "\r\n" +
            "Thank you, Aethus\r\n" +
            "-----------------\r\n" +
            "Azeroth Survivors is Aethus's game. Thank you for creating it, for keeping it going with update after\r\n" +
            "update, and for supporting the people who play it. None of this would exist without you.\r\n" +
            "\r\n" +
            "Thank you, community\r\n" +
            "--------------------\r\n" +
            "To everyone in the Azeroth Survivors community who makes addons, shares fixes and tips, tests\r\n" +
            "things, reports problems and simply plays: thank you for sharing something this fun alongside me.\r\n" +
            "This launcher is for you.\r\n" +
            "\r\n" +
            "Also thanks to\r\n" +
            "--------------\r\n" +
            "Keijin - the cloud save sync is based on AzerothSync by KeijinDE (MIT License).\r\n" +
            "The addon authors, whose work is offered for download in the launcher.\r\n" +
            "\r\n" +
            "Source code: https://github.com/Slimmy25/Azeroth-Survivor-Launcher\r\n";

        static void WriteCredits(string dir)
        {
            string path = Path.Combine(dir, "Credits.txt");
            try { if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == CreditsText) return; }
            catch (Exception) { }
            WriteAtomic(path, CreditsText);
        }

        static bool Writable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception) { return false; }
        }

        // Earlier versions kept their files in %APPDATA%\WoWLauncher (settings) and %LOCALAPPDATA%\WoWLauncher
        // (sync memory, log, backups). The first start of this version copies them over, so nothing is lost; the old
        // folders are left as they were, in case an older launcher is still used.
        static void MoveOldData(string dest)
        {
            if (File.Exists(Path.Combine(dest, "config.json")) || File.Exists(Path.Combine(dest, "state.json"))) return;
            string oldCfg = Path.Combine(Path.Combine(AppData, "WoWLauncher"), "config.json");
            string oldState = Path.Combine(LocalAppData, "WoWLauncher");
            if (!File.Exists(oldCfg) && !File.Exists(Path.Combine(oldState, "state.json"))) return;
            foreach (string name in new[] { "state.json", "log.txt", "log.old.txt" })
                CopyIfThere(Path.Combine(oldState, name), Path.Combine(dest, name));
            foreach (string sub in new[] { "backups", "downloads" })
            {
                string src = Path.Combine(oldState, sub);
                if (!Directory.Exists(src)) continue;
                foreach (string f in Directory.GetFiles(src, "*.zip"))
                    CopyIfThere(f, Path.Combine(Path.Combine(dest, sub), Path.GetFileName(f)));
            }
            CopyIfThere(oldCfg, Path.Combine(dest, "config.json"));      // last: it marks the copy as done
        }

        static void CopyIfThere(string src, string dst)
        {
            try
            {
                if (!File.Exists(src) || File.Exists(dst)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst + ".tmp", true);
                File.Move(dst + ".tmp", dst);
            }
            catch (Exception) { }
        }

        public static string DefaultSaveDir
        {
            get { return Path.Combine(AppData, SaveFolderName); }
        }

        public static string DownloadsDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                return Directory.Exists(d) ? d : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }

        public static string Home
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
        }

        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            Replace(tmp, path);
        }

        // a junction or symbolic link (a folder that really lives somewhere else)
        public static bool IsLink(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) return false;
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception) { return false; }
        }

        public static string Canon(string p)
        {
            p = Path.GetFullPath(p);
            string root = Path.GetPathRoot(p);
            if (string.Equals(p, root, StringComparison.OrdinalIgnoreCase)) return p;
            return p.TrimEnd('\\', '/');
        }

        public static bool SamePath(string a, string b)
        {
            try { return string.Equals(Canon(a), Canon(b), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
        }

        public static bool IsDriveRoot(string p)
        {
            string full = Canon(p);
            return string.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
            SetLastError = true)]
        static extern bool MoveFileEx(string existing, string target, int flags);

        // like Python's os.replace: moves a file over another one in one step (same drive)
        public static void Replace(string src, string dst)
        {
            if (!MoveFileEx(src, dst, 0x1 | 0x8))                    // REPLACE_EXISTING | WRITE_THROUGH
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (err == 5 || err == 32 || err == 33)
                    throw new UnauthorizedAccessException("The file is in use or read-only: " + dst);
                if (err == 112 || err == 39)
                    throw new DiskFullException();
                throw new IOException("Could not move " + src + " to " + dst + " (Windows error " + err + ").");
            }
        }

        public static bool Inside(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();
            return a == b || a.StartsWith(b.TrimEnd('\\', '/') + "\\", StringComparison.Ordinal);
        }

        public static bool Overlap(string a, string b) { return Inside(a, b) || Inside(b, a); }

        // removes a folder, read-only files included
        public static void DeleteTree(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (string f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); } catch (Exception) { }
            }
            Directory.Delete(path, true);
        }

        public static void DeleteTreeQuiet(string path)
        {
            try { DeleteTree(path); } catch (Exception) { }
        }

        public static string SizeText(long b)
        {
            if (b < 1048576) return ((b + 1023) / 1024).ToString(CultureInfo.InvariantCulture) + " KB";
            return (b / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        public static string FmtSize(long n)
        {
            double d = n;
            if (d >= 1L << 30) return (d / (1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
            if (d >= 1L << 20)
                return (d / (1L << 20)).ToString(d >= 100L << 20 ? "0" : "0.0", CultureInfo.InvariantCulture) + " MB";
            if (d >= 1024) return (d / 1024).ToString("0", CultureInfo.InvariantCulture) + " KB";
            return n.ToString(CultureInfo.InvariantCulture) + " B";
        }

        public static void OpenInShell(string target)
        {
            try { System.Diagnostics.Process.Start(target); }
            catch (Exception) { }
        }

        public static bool GameRunning()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameExe)).Length > 0;
            }
            catch (Exception) { return false; }
        }

        public static string Plural(int n, string word)
        {
            return n.ToString(CultureInfo.InvariantCulture) + " " + word + (n == 1 ? "" : "s");
        }

        // "0.113.3" -> {0, 113, 3}; null when the text does not start with a number
        public static int[] ParseVersion(string text)
        {
            var m = Regex.Match(text ?? "", @"^\s*v?(\d+(?:\.\d+)*)");
            if (!m.Success) return null;
            string[] parts = m.Groups[1].Value.Split('.');
            var r = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int n;
                r[i] = int.TryParse(parts[i], out n) ? n : int.MaxValue;
            }
            return r;
        }

        // -1, 0 or 1; null when either is not a version number
        public static int? CompareVersions(string a, string b)
        {
            int[] pa = ParseVersion(a), pb = ParseVersion(b);
            if (pa == null || pb == null) return null;
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length ? pa[i] : 0, y = i < pb.Length ? pb[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        public static string ReadResource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return "";
                using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
            }
        }

        public static Stream ResourceStream(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }
    }

    // ------------------------------------------------------------------ built-in data
    public class AddonEntry
    {
        public string Category, Name, Url, Description;
        public List<string> Tags = new List<string>();
    }

    public static class Data
    {
        public static readonly string[] AddonCategories = { "AS ADDONS", "WOW 1.12 ADDONS" };
        public static readonly string[] AddonFilters = { "UI OVERHAUL", "GAMEPLAY", "COSMETIC" };
        public const int ClRows = 8;                 // buttons per column in All Patch Notes (two columns per page)

        static List<string> changelog;
        static List<AddonEntry> addons;
        static string about;

        // the posts of changelog.txt, highest version first (a post whose title is not a number goes last)
        public static List<string> Changelog
        {
            get
            {
                if (changelog != null) return changelog;
                string text = Util.ReadResource("AS.changelog.txt").Replace("\r\n", "\n");
                var posts = new List<string>();
                string[] parts = Regex.Split(text, @"^===[ \t]*$", RegexOptions.Multiline);
                for (int i = 1; i < parts.Length; i++)            // part 0 is the explanation at the top
                {
                    string p = parts[i].Trim('\n');
                    if (p.Trim().Length > 0) posts.Add(p);
                }
                var order = new List<KeyValuePair<int, string>>();
                for (int i = 0; i < posts.Count; i++) order.Add(new KeyValuePair<int, string>(i, posts[i]));
                order.Sort(delegate(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
                {
                    int[] va = Util.ParseVersion(a.Value), vb = Util.ParseVersion(b.Value);
                    if (va == null && vb == null) return a.Key.CompareTo(b.Key);
                    if (va == null) return 1;
                    if (vb == null) return -1;
                    int c = -(Util.CompareVersions(a.Value, b.Value) ?? 0);
                    return c != 0 ? c : a.Key.CompareTo(b.Key);
                });
                changelog = new List<string>();
                foreach (var kv in order) changelog.Add(kv.Value);
                return changelog;
            }
        }

        // the game's current version: the title of the newest patch note that is a version number ("" if none)
        public static string GameVersion
        {
            get
            {
                foreach (string post in Changelog)
                {
                    string t = PostTitle(post);
                    var m = Regex.Match(t, @"^v?(\d+(?:\.\d+)*)");
                    if (m.Success) return m.Groups[1].Value;
                }
                return "";
            }
        }

        public static string PostTitle(string post)
        {
            return post.Trim('\n').Split('\n')[0].Trim();
        }

        // addons.txt: CATEGORY | Name | url | FILTERS | description
        public static List<AddonEntry> Addons
        {
            get
            {
                if (addons != null) return addons;
                addons = new List<AddonEntry>();
                foreach (string raw in Util.ReadResource("AS.addons.txt").Replace("\r\n", "\n").Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    string[] f = line.Split('|');
                    if (f.Length < 3) continue;
                    var e = new AddonEntry();
                    e.Category = f[0].Trim().ToUpperInvariant();
                    e.Name = f[1].Trim();
                    e.Url = f[2].Trim();
                    e.Description = f.Length > 4 ? string.Join("|", f, 4, f.Length - 4).Trim() : "";
                    if (f.Length > 3)
                        foreach (string t in f[3].Split(','))
                            if (t.Trim().Length > 0) e.Tags.Add(t.Trim().ToUpperInvariant());
                    if (e.Name.Length > 0 && e.Url.Length > 0) addons.Add(e);
                }
                return addons;
            }
        }

        public static string About
        {
            get
            {
                if (about == null) about = Util.ReadResource("AS.about.txt").Replace("\r\n", "\n");
                return about;
            }
        }
    }
}
