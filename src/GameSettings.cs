// The game's own settings, changed from the launcher: the [cvars] of benilla-config\config.toml, the file the game
// keeps the settings you changed in (it reads it when it starts, and keeps hand edits). The names, values and
// defaults below are the game's own (from its options window): gxWindow 0 is "Windowed (Fullscreen)", the
// borderless window over the whole screen (there is no exclusive fullscreen), 1 an ordinary window of gxResolution.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AzerothLauncher
{
    public class GameSetting
    {
        public string Cvar, Label, Note, Default;
        public string[] Values, Names;                        // the choices: what is written, and what is shown
        public bool Check;                                    // on ("1") or off ("0")
    }

    public static class GameSettings
    {
        public static readonly List<GameSetting> All = new List<GameSetting>();

        static void Add(string cvar, string label, string def, string note, string values, string names)
        {
            var s = new GameSetting();
            s.Cvar = cvar; s.Label = label; s.Default = def; s.Note = note;
            s.Values = values.Split('|');
            s.Names = names.Split('|');
            All.Add(s);
        }

        static void Check(string cvar, string label, string def, string note)
        {
            var s = new GameSetting();
            s.Cvar = cvar; s.Label = label; s.Default = def; s.Note = note; s.Check = true;
            All.Add(s);
        }

        static GameSettings()
        {
            Add("gxWindow", "Display mode", null, "Windowed (Fullscreen) fills the screen with a borderless window; Windowed runs in an ordinary one.",
                "0|1", "WINDOWED (FULLSCREEN)|WINDOWED");
            Add("gxResolution", "Window size (Windowed)", "1600x900", null,
                "1280x720|1366x768|1600x900|1920x1080|2560x1440|3840x2160", "1280×720|1366×768|1600×900|1920×1080|2560×1440|3840×2160");
            Check("gxMaximize", "Maximize the game window", null, null);
            Check("gxVSync", "Vertical sync", "1", null);
            Add("renderScale", "Render scale", "1", "Below 100% trades sharpness for frame rate; above 100% smooths edges at the cost of frame rate.",
                "0.5|0.75|1|1.5|2", "50%|75%|100%|150%|200%");
            Add("gxMultisample", "Anti-aliasing", null, "Takes effect the next time the game starts.", "1|2|4|8", "OFF|2x|4x|8x");
            Add("uiScale", "UI scale", "1.0", null, "0.64|0.75|0.85|1.0", "64%|75%|85%|100%");
            Add("farclip", "Terrain distance", "350", null, "177|350|577|777", "177|350|577|777");
            Add("WorldDetail", "Environment detail", null, null, "0|1|2", "LOW|MEDIUM|HIGH");
            Add("MasterVolume", "Master volume", "1.0", null, "0|0.1|0.25|0.5|0.75|1.0", "0%|10%|25%|50%|75%|100%");
            Check("EnableMusic", "Music", "1", null);
            Check("Sound_EnableSoundWhenGameIsInBG", "Sound when the game is in the background", "0", null);
        }

        // where the game keeps it: the game config folder (benilla-config)
        public static string FileIn(string configDir)
        {
            return string.IsNullOrEmpty(configDir) ? null : Path.Combine(configDir, "config.toml");
        }

        static readonly Regex Line = new Regex("^\\s*([A-Za-z0-9_]+)\\s*=\\s*\"([^\"]*)\"\\s*$");

        public static Dictionary<string, string> Read(string file)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (file == null || !File.Exists(file)) return d;
            bool cvars = false;
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.StartsWith("[")) { cvars = line == "[cvars]"; continue; }
                Match m = Line.Match(line);
                if (cvars && m.Success) d[m.Groups[1].Value] = m.Groups[2].Value;
            }
            return d;
        }

        // the value now: the file's, else the game's default (null: the game decides)
        public static string Value(Dictionary<string, string> d, GameSetting s)
        {
            string v;
            return d.TryGetValue(s.Cvar, out v) ? v : s.Default;
        }

        // one setting written into the file, everything else in it kept as it is
        public static void Set(string file, string cvar, string value)
        {
            if (file == null) throw new UserError("The game config folder is not set. Set it in Options > FOLDERS.");
            var lines = File.Exists(file) ? new List<string>(File.ReadAllLines(file)) : new List<string>();
            string entry = cvar + " = \"" + value.Replace("\"", "") + "\"";
            int section = -1, end = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("["))
                {
                    if (section >= 0) { end = i; break; }
                    if (t == "[cvars]") section = i;
                    continue;
                }
                Match m = Line.Match(t);
                if (section >= 0 && m.Success && string.Equals(m.Groups[1].Value, cvar, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = entry;
                    Write(file, lines);
                    return;
                }
            }
            if (section < 0)
            {
                if (lines.Count == 0) lines.Add("# benilla local config — CVar values that moved off their defaults.");
                lines.Add("[cvars]");
                lines.Add(entry);
            }
            else
            {
                while (end > section + 1 && lines[end - 1].Trim().Length == 0) end--;   // before the blank lines at its end
                lines.Insert(end, entry);
            }
            Write(file, lines);
        }

        static void Write(string file, List<string> lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, string.Join("\n", lines.ToArray()) + "\n", new UTF8Encoding(false));
            Util.Replace(tmp, file);
        }
    }
}
