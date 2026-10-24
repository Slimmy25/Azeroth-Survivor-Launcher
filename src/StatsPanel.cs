// The STATS section: celebrates the player's long-term numbers. The launcher's own play time is always there;
// the run records come from the optional Azeroth Stats addon (see Stats.cs). Turned off in Options, the section
// and its buttons disappear.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        public bool StatsEnabled() { return Cfg.Bool("stats_enabled", true); }

        // Options > "Show the Stats section"
        void SetStatsEnabled(bool on)
        {
            Cfg.Set("stats_enabled", on);
            Cfg.Save();
            LayoutNav();
            Invalidate();
            if (!on && current == "Stats") CloseSide();
            if (current == "Options" && side.Visible) BuildPanel("Options");
        }

        static string Big(double n)
        {
            return Math.Floor(Math.Max(0, n)).ToString("#,0", CultureInfo.InvariantCulture);
        }

        // 754 -> "12:34", 4000 -> "1:06:40"
        static string Clock(double seconds)
        {
            long s = (long)Math.Max(0, Math.Floor(seconds));
            long h = s / 3600, m = s % 3600 / 60, sec = s % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + sec.ToString("00") : m + ":" + sec.ToString("00");
        }

        static string ResultName(string kind, string result)
        {
            if (kind == "victory") return "Victory";
            if (kind == "defeat") return "Defeat";
            if (kind == "left") return "Left the run";
            return result.Length > 0 ? char.ToUpperInvariant(result[0]) + result.Substring(1) : "?";
        }

        // a card in the stats list: gold-framed box that its parts are stacked into
        class StatCard
        {
            public readonly Bordered Box = new Bordered(Theme.GoldDim, 2);
            readonly Panel inner = new Panel();
            public int Y = 10;
            readonly int cw;                                 // the width the content can use
            const int X = 14;

            public StatCard(int width, string title)
            {
                inner.BackColor = Theme.Ink;
                inner.Dock = DockStyle.Fill;
                Box.Controls.Add(inner);
                Box.Width = width;
                cw = width - 4 - 2 * X;
                if (title != null) Add(UI.Text(title, 13, FontStyle.Bold, Theme.Gold, Theme.Ink, cw), X, 0);
            }

            Label Add(Label l, int x, int gap)
            {
                l.Size = VStack.Measure(l);
                l.Location = new Point(x, Y + gap);
                inner.Controls.Add(l);
                Y += gap + l.Height;
                return l;
            }

            public void Text(string text, float size, FontStyle style, Color fg, int gap, bool translate = true)
            {
                Add(UI.Text(text, size, style, fg, Theme.Ink, cw, translate), X, gap);
            }

            // tiles of a big value over its name, `cols` in a row
            public void Tiles(List<string[]> items, int cols)
            {
                const int gap = 8, th = 64;
                int tw = (cw - (cols - 1) * gap) / cols;
                int top = Y + 8;
                for (int i = 0; i < items.Count; i++)
                {
                    int r = i / cols, c = i % cols;
                    var tile = new Bordered(Theme.Bronze, 1);
                    tile.Bounds = new Rectangle(X + c * (tw + gap), top + r * (th + gap), tw, th);
                    var t = new Panel();
                    t.BackColor = Theme.Panel;
                    t.Dock = DockStyle.Fill;
                    tile.Controls.Add(t);
                    var v = UI.Text(items[i][0], 15, FontStyle.Bold, Theme.Gold, Theme.Panel, tw - 8, false);
                    v.Size = VStack.Measure(v);
                    v.Location = new Point(Math.Max(2, (tw - 2 - v.Width) / 2), 7);
                    t.Controls.Add(v);
                    var n = UI.Text(items[i][1], 8.5f, FontStyle.Regular, Theme.Text, Theme.Panel, tw - 8);
                    n.Size = VStack.Measure(n);
                    n.Location = new Point(Math.Max(2, (tw - 2 - n.Width) / 2), v.Bottom + 1);
                    t.Controls.Add(n);
                    inner.Controls.Add(tile);
                }
                int rows = (items.Count + cols - 1) / cols;
                Y = top + rows * (th + gap) - gap;
            }

            // a line: name (and a small line under it) on the left, the value on the right
            public void Row(string name, string sub, string value, bool translateName, Color valueColor)
            {
                Y += 6;
                int top = Y;
                var v = UI.Text(value, 11, FontStyle.Bold, valueColor, Theme.Ink, 0, false);
                v.Size = VStack.Measure(v);
                v.Location = new Point(X + cw - v.Width, top);
                inner.Controls.Add(v);
                int lw = Math.Max(80, cw - v.Width - 12);
                var l = UI.Text(name, 10.5f, FontStyle.Bold, Theme.Text, Theme.Ink, lw, translateName);
                l.Size = VStack.Measure(l);
                l.Location = new Point(X, top);
                inner.Controls.Add(l);
                Y = Math.Max(l.Bottom, v.Bottom);
                if (!string.IsNullOrEmpty(sub))
                {
                    var s = UI.Text(sub, 9, FontStyle.Italic, Theme.GoldDim, Theme.Ink, lw, false);
                    s.Size = VStack.Measure(s);
                    s.Location = new Point(X, Y);
                    inner.Controls.Add(s);
                    Y = s.Bottom;
                }
                Y += 6;
                var line = UI.Line(Theme.Bronze, cw, 1);
                line.Location = new Point(X, Y);
                inner.Controls.Add(line);
                Y += 1;
            }

            // a milestone: the name, how far to the next step, and a bar
            public void Bar(string name, double value, double[] steps, bool hours)
            {
                double next = 0, prev = 0;
                foreach (double s in steps)
                {
                    if (value < s) { next = s; break; }
                    prev = s;
                }
                Y += 8;
                int top = Y;
                string right = next > 0 ? Big(value) + " of " + Big(next) : "Every milestone reached!";
                var r = UI.Text(right, 9, FontStyle.Italic, next > 0 ? Theme.GoldDim : Theme.Good, Theme.Ink, 0);
                r.Size = VStack.Measure(r);
                r.Location = new Point(X + cw - r.Width, top + 2);
                inner.Controls.Add(r);
                var l = UI.Text(name, 10.5f, FontStyle.Bold, Theme.Text, Theme.Ink, Math.Max(80, cw - r.Width - 12));
                l.Size = VStack.Measure(l);
                l.Location = new Point(X, top);
                inner.Controls.Add(l);
                Y = Math.Max(l.Bottom, r.Bottom) + 3;
                double frac = next > 0 ? (value - prev) / Math.Max(1, next - prev) : 1;
                var track = new Panel();
                track.BackColor = Theme.Bronze;
                track.Bounds = new Rectangle(X, Y, cw, 8);
                var fill = new Panel();
                fill.BackColor = next > 0 ? Theme.Gold : Theme.Good;
                fill.Bounds = new Rectangle(0, 0, (int)Math.Round(cw * Math.Max(0, Math.Min(1, frac))), 8);
                track.Controls.Add(fill);
                inner.Controls.Add(track);
                Y += 8 + 2;
            }

            public void Button(WowButton b)
            {
                Y += 10;
                b.Location = new Point(X, Y);
                inner.Controls.Add(b);
                Y += b.Height;
            }

            // puts the card in the list at `y`; returns where the next one goes
            public int Finish(Control list, int x, int y)
            {
                Y += 14;
                Box.Bounds = new Rectangle(x, y, Box.Width, Y + 4);
                list.Controls.Add(Box);
                return y + Box.Height + 10;
            }
        }

        static readonly double[] RunSteps = { 1, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        static readonly double[] WinSteps = { 1, 5, 10, 25, 50, 100, 250, 500, 1000 };
        static readonly double[] KillSteps = { 100, 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000, 10000000 };
        static readonly double[] BossSteps = { 1, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        static readonly double[] GoldSteps = { 100, 1000, 5000, 10000, 50000, 100000, 500000, 1000000, 5000000 };
        static readonly double[] HourSteps = { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500 };

        void PanelStats()
        {
            ClearPanel("Stats");
            string addonsDir = AddonsDir();
            string addonPath = Stats.AddonPath(addonsDir);
            bool have = addonPath != null, outdated = have && Stats.AddonOutdated(addonsDir);
            bool off = have && addonPath.EndsWith(Addons.DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            if (!have) Put(new WowButton("INSTALL STATS ADDON", InstallStatsAddon, 230, 32, 10), PW - 20 - 230, 12);
            else if (outdated) Put(new WowButton("UPDATE STATS ADDON", InstallStatsAddon, 230, 32, 10), PW - 20 - 230, 12);
            else Put(new WowButton("REFRESH", delegate { BuildPanel("Stats"); }, 120, 32, 10), PW - 20 - 120, 12);

            RunStats st = null;
            string err = null;
            try { st = Stats.Load(SyncSettings().SavedDir); }
            catch (Exception e) { err = e.Message; }

            var sp = MakeScroll(panel, new Rectangle(20, 56, PW - 40, PH - 12 - 56), Theme.Panel, Theme.Bronze);
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            int y = 4;

            // --- the headline
            if (st != null && st.Runs > 0)
            {
                var head = new StatCard(w, null);
                head.Text(st.Runs == 1 ? "Your first run is in the books!" :
                          Big(st.Kills) + " enemies slain in " + Big(st.Runs) + " runs!", 15, FontStyle.Bold, Theme.Gold, 0);
                if (st.Victories > 0)
                    head.Text(Big(st.Victories) + " victories, " + Big(st.Bosses) + " bosses defeated and " + Big(st.Gold) +
                              " gold collected.", 10, FontStyle.Italic, Theme.Text, 4);
                y = head.Finish(sp, 4, y);
            }

            // --- the launcher's play time
            PlayStats ps = ShownPlayStats();
            var play = new StatCard(w, "Play time");
            if (ps.Sessions == 0) play.Text("No games played with this launcher yet.", 10, FontStyle.Italic, Theme.GoldDim, 6);
            else
            {
                play.Tiles(new List<string[]> {
                    new[] { Duration(ps.Seconds), "Time played" },
                    new[] { Big(ps.Sessions), "Sessions" },
                    new[] { Duration(ps.Longest), "Longest session" },
                    new[] { Duration(ps.LastSeconds), "Last session" } }, 4);
                bool shared = CloudEnabled() && cloudPlay != null;
                string note = shared ? "Shared with your other PCs through the cloud sync folder." :
                              CloudEnabled() ? "Counted on this PC (the sync folder is not reachable right now)." :
                              "Counted on this PC. Turn on cloud saving to share it between your PCs.";
                play.Text(note, 9, FontStyle.Italic, Theme.GoldDim, 8);
            }
            y = play.Finish(sp, 4, y);

            // --- no run records (yet)
            if (st == null || st.Runs == 0)
            {
                var info = new StatCard(w, have ? "Your runs" : "Track every run");
                if (err != null) info.Text("The stats file could not be read (" + err + ").", 10, FontStyle.Regular, Theme.Bad, 6);
                else if (!have)
                {
                    info.Text("The Azeroth Stats addon keeps a record of every run you play: victories, kills, damage, gold " +
                              "and your personal bests. Install it, finish a run, and your stats appear here.",
                              10, FontStyle.Regular, Theme.Text, 6);
                    info.Text("It is optional: without it, this page only shows your play time.", 9, FontStyle.Italic, Theme.GoldDim, 6);
                    info.Button(new WowButton("INSTALL STATS ADDON", InstallStatsAddon, 230, 32, 10));
                }
                else if (off)
                    info.Text("The Azeroth Stats addon is turned off. Turn it on in Addons to keep recording your runs.",
                              10, FontStyle.Regular, Theme.Text, 6);
                else
                    info.Text("No runs recorded yet. Finish a run with the Azeroth Stats addon turned on. The game saves " +
                              "addon data when you leave it, so your stats show up here after you close the game.",
                              10, FontStyle.Regular, Theme.Text, 6);
                y = info.Finish(sp, 4, y);
                Pad(sp, y);
                return;
            }

            if (off)
            {
                var note = new StatCard(w, null);
                note.Text("The Azeroth Stats addon is turned off. Turn it on in Addons to keep recording your runs.",
                          10, FontStyle.Italic, Theme.Bad, 0);
                y = note.Finish(sp, 4, y);
            }

            // --- totals
            var runs = new StatCard(w, "Your runs");
            double rate = st.Runs > 0 ? Math.Round(100 * st.Victories / st.Runs) : 0;
            runs.Tiles(new List<string[]> {
                new[] { Big(st.Runs), "Runs" },
                new[] { Big(st.Victories), "Victories" },
                new[] { rate.ToString("0", CultureInfo.InvariantCulture) + "%", "Win rate" },
                new[] { Duration((long)st.Time), "Time in runs" },
                new[] { Big(st.Kills), "Enemies slain" },
                new[] { Big(st.Elites), "Elites" },
                new[] { Big(st.Guardians), "Guardians" },
                new[] { Big(st.Bosses), "Bosses" },
                new[] { Big(st.Damage), "Damage dealt" },
                new[] { Big(st.Taken), "Damage taken" },
                new[] { Big(st.Gold), "Gold collected" },
                new[] { Big(st.Chests), "Caches opened" } }, 4);
            y = runs.Finish(sp, 4, y);

            // --- personal bests
            var bests = new StatCard(w, "Personal bests");
            var order = new[] {
                new[] { "time", "Longest run", "clock" }, new[] { "fastestWin", "Fastest victory", "clock" },
                new[] { "lvl", "Highest level", "" }, new[] { "kills", "Most kills in a run", "" },
                new[] { "dmg", "Most damage in a run", "" }, new[] { "gold", "Most gold in a run", "" },
                new[] { "bosses", "Most bosses in a run", "" } };
            foreach (var o in order)
            {
                StatRecord b;
                if (!st.Best.TryGetValue(o[0], out b) || b.Value <= 0) continue;
                bests.Row(o[1], Dots(b.Hero, Pair(b.Class, b.Race.Length > 0 ? "(" + b.Race + ")" : ""), b.Zone, b.Date),
                          o[2] == "clock" ? Clock(b.Value) : Big(b.Value), true, Theme.Gold);
            }
            y = bests.Finish(sp, 4, y);

            // --- milestones
            var miles = new StatCard(w, "Milestones");
            miles.Bar("Runs", st.Runs, RunSteps, false);
            miles.Bar("Victories", st.Victories, WinSteps, false);
            miles.Bar("Enemies slain", st.Kills, KillSteps, false);
            miles.Bar("Bosses", st.Bosses, BossSteps, false);
            miles.Bar("Gold collected", st.Gold, GoldSteps, false);
            miles.Bar("Hours played", Math.Floor(ps.Seconds / 3600.0), HourSteps, true);
            y = miles.Finish(sp, 4, y);

            // --- classes, races, zones
            var groups = new[] { new object[] { "Classes", st.Classes }, new object[] { "Races", st.Races }, new object[] { "Zones", st.Zones } };
            foreach (var g in groups)
            {
                var list = (List<StatRecord>)g[1];
                if (list.Count == 0) continue;
                var card = new StatCard(w, (string)g[0]);
                foreach (var r in list)
                    card.Row(r.Name, Lang.T("Runs: " + Big(r.Runs) + " · Won: " + Big(r.Wins) + " · Longest: " + Clock(r.BestTime) +
                                          " · Best level: " + Big(r.BestLevel)), Lang.T(Big(r.Kills) + " kills"), false, Theme.Text);
                y = card.Finish(sp, 4, y);
            }

            // --- the last runs
            if (st.History.Count > 0)
            {
                var recent = new StatCard(w, "Recent runs");
                for (int i = 0; i < Math.Min(10, st.History.Count); i++)
                {
                    StatRun r = st.History[i];
                    string title = Lang.T(ResultName(r.Kind, r.Result)) + " · " +
                                   Pair(r.Class, r.Race.Length > 0 ? "(" + r.Race + ")" : "") + (r.Zone.Length > 0 ? " · " + r.Zone : "");
                    string sub = Lang.T("Survived " + Clock(r.Time) + " · Level " + Big(r.Level) + " · Kills " + Big(r.Kills) +
                                        " · Gold " + Big(r.Gold));
                    if (r.Date.Length > 0) sub = r.Date + "   " + sub;
                    recent.Row(title, sub, Clock(r.Time), false,
                               r.Kind == "victory" ? Theme.Good : r.Kind == "defeat" ? Theme.Bad : Theme.GoldDim);
                }
                y = recent.Finish(sp, 4, y);
            }

            var foot = UI.Text("In game, type /azstats to see your stats.\nSaved by the game " +
                               st.Written.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ".",
                               9, FontStyle.Italic, Theme.GoldDim, Theme.Panel, w);
            foot.Size = VStack.Measure(foot);
            foot.Location = new Point(8, y);
            sp.Controls.Add(foot);
            Pad(sp, foot.Bottom + 4);
        }

        // room under the last card
        static void Pad(ScrollPanel sp, int y)
        {
            var pad = new Panel();
            pad.Bounds = new Rectangle(4, y, 1, 8);
            pad.BackColor = sp.BackColor;
            sp.Controls.Add(pad);
        }

        // the parts that are not empty, between dots
        static string Dots(params string[] parts)
        {
            return JoinParts(parts, " · ");
        }

        static string Pair(string a, string b)
        {
            return JoinParts(new[] { a, b }, " ");
        }

        static string JoinParts(string[] parts, string sep)
        {
            var keep = new List<string>();
            foreach (string p in parts) if (!string.IsNullOrEmpty(p) && p.Trim().Length > 0) keep.Add(p.Trim());
            return string.Join(sep, keep.ToArray());
        }

        void InstallStatsAddon()
        {
            if (AddonsLocked()) return;
            string dir = AddonsDir();
            bool had = Stats.AddonInstalled(dir);
            try
            {
                Stats.InstallAddon(dir);
                SetStatus(had ? "Azeroth Stats updated." : "Azeroth Stats installed. It records your runs from now on.");
            }
            catch (Exception e)
            {
                Msg.Show(side.Visible ? (IWin32Window)side : this, "Could not install Azeroth Stats:\n\n" + e.Message, "Stats",
                         MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (current == "Stats") BuildPanel("Stats");
        }
    }
}
