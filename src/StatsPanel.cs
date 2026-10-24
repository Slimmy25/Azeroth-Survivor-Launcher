// The STATS section: celebrates the player's long-term numbers. The launcher's own play time is always there;
// the run records come from the optional AS Time Tracker addon (see Stats.cs). Turned off in Options, the section
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

            public int ContentWidth { get { return cw; } }

            // any control, full width
            public void Add(Control c, int gap)
            {
                Y += gap;
                c.Location = new Point(X, Y);
                inner.Controls.Add(c);
                Y += c.Height;
            }

            // a share of the whole: the name, a small line, the value on the right and a bar in `color`
            public void Share(string name, string sub, double frac, string value, Color color)
            {
                Y += 7;
                int top = Y;
                var v = UI.Text(value, 10.5f, FontStyle.Bold, Theme.Text, Theme.Ink, 0, false);
                v.Size = VStack.Measure(v);
                v.Location = new Point(X + cw - v.Width, top);
                inner.Controls.Add(v);
                var l = UI.Text(name, 10.5f, FontStyle.Bold, color, Theme.Ink, Math.Max(80, cw - v.Width - 12), false);
                l.Size = VStack.Measure(l);
                l.Location = new Point(X, top);
                inner.Controls.Add(l);
                Y = Math.Max(l.Bottom, v.Bottom) + 2;
                var track = new Panel();
                track.BackColor = Theme.Bronze;
                track.Bounds = new Rectangle(X, Y, cw, 7);
                var fill = new Panel();
                fill.BackColor = color;
                fill.Bounds = new Rectangle(0, 0, (int)Math.Round(cw * Math.Max(0, Math.Min(1, frac))), 7);
                track.Controls.Add(fill);
                inner.Controls.Add(track);
                Y += 7;
                if (!string.IsNullOrEmpty(sub))
                {
                    var s = UI.Text(sub, 9, FontStyle.Italic, Theme.GoldDim, Theme.Ink, cw, false);
                    s.Size = VStack.Measure(s);
                    s.Location = new Point(X, Y + 2);
                    inner.Controls.Add(s);
                    Y = s.Bottom;
                }
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

        string statsClass;                                   // the STATS page's view: null = every class, else a class key

        // WoW's class colors, by the class's key; others get gold
        public static Color ClassColorOf(string key) { return ClassColor(key); }

        static Color ClassColor(string key)
        {
            string k = (key ?? "").ToLowerInvariant();
            if (k.Contains("death")) return Theme.Hex("#c41f3b");
            if (k.Contains("warrior")) return Theme.Hex("#c79c6e");
            if (k.Contains("paladin")) return Theme.Hex("#f58cba");
            if (k.Contains("hunter")) return Theme.Hex("#abd473");
            if (k.Contains("rogue")) return Theme.Hex("#fff569");
            if (k.Contains("priest")) return Theme.Hex("#f0f0f0");
            if (k.Contains("shaman")) return Theme.Hex("#2f8fff");
            if (k.Contains("mage")) return Theme.Hex("#69ccf0");
            if (k.Contains("warlock")) return Theme.Hex("#9482c9");
            if (k.Contains("druid")) return Theme.Hex("#ff7d0a");
            return Theme.Gold;
        }

        static string Pct(double part, double whole)
        {
            return (whole > 0 ? Math.Round(100 * part / whole) : 0).ToString("0", CultureInfo.InvariantCulture);
        }

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

            // the view: every class, or one class on its own
            StatBlock cls = st != null && statsClass != null ? st.Class(statsClass) : null;
            if (cls == null) statsClass = null;
            if (st != null && st.Classes.Count > 0)
            {
                WowButton view = null;
                view = Put(new WowButton(cls == null ? "ALL CLASSES" : cls.Name.ToUpperInvariant(), delegate
                {
                    var menu = new ContextMenuStrip();
                    var all = new ToolStripMenuItem(Lang.T("ALL CLASSES"), null, delegate { statsClass = null; BuildPanel("Stats"); });
                    all.Checked = statsClass == null;
                    menu.Items.Add(all);
                    menu.Items.Add(new ToolStripSeparator());
                    foreach (var c in st.Classes)
                    {
                        string key = c.Key;
                        var item = new ToolStripMenuItem(c.Name + "  (" + Big(c.Runs) + ")", null, delegate { statsClass = key; BuildPanel("Stats"); });
                        item.Checked = key == statsClass;
                        item.ForeColor = Theme.Mix(ClassColor(key), Color.Black, 0.45);
                        menu.Items.Add(item);
                    }
                    menu.Show(view, new Point(0, view.Height));
                }, 200, 32, 10), 84, 12);
                if (cls != null) view.Selected = true;
            }

            var sp = MakeScroll(panel, new Rectangle(20, 56, PW - 40, PH - 12 - 56), Theme.Panel, Theme.Bronze);
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            int y = 4;

            if (cls != null)
            {
                y = ClassView(sp, w, y, st, cls);
                StatsFoot(sp, w, y, st);
                return;
            }

            // --- the headline
            if (st != null && st.Total.Runs > 0)
            {
                var head = new StatCard(w, null);
                head.Text(st.Total.Runs == 1 ? "Your first run is in the books!" :
                          Big(st.Total.Kills) + " enemies slain in " + Big(st.Total.Runs) + " runs!", 15, FontStyle.Bold, Theme.Gold, 0);
                if (st.Total.Victories > 0)
                    head.Text(Big(st.Total.Victories) + " victories, " + Big(st.Total.Bosses) + " bosses defeated and " +
                              Big(st.Total.Gold) + " gold collected.", 10, FontStyle.Italic, Theme.Text, 4);
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
            if (st == null || st.Total.Runs == 0)
            {
                var info = new StatCard(w, have ? "Your runs" : "Track every run");
                if (err != null) info.Text("The stats file could not be read (" + err + ").", 10, FontStyle.Regular, Theme.Bad, 6);
                else if (!have)
                {
                    info.Text("The AS Time Tracker addon keeps a record of every run you play: victories, kills, damage, gold " +
                              "and your personal bests. Install it, finish a run, and your stats appear here.",
                              10, FontStyle.Regular, Theme.Text, 6);
                    info.Text("It is optional: without it, this page only shows your play time.", 9, FontStyle.Italic, Theme.GoldDim, 6);
                    info.Button(new WowButton("INSTALL STATS ADDON", InstallStatsAddon, 230, 32, 10));
                }
                else if (off)
                    info.Text("The AS Time Tracker addon is turned off. Turn it on in Addons to keep recording your runs.",
                              10, FontStyle.Regular, Theme.Text, 6);
                else
                    info.Text("No runs recorded yet. Finish a run with the AS Time Tracker addon turned on. The game saves " +
                              "addon data when you leave it, so your stats show up here after you close the game.",
                              10, FontStyle.Regular, Theme.Text, 6);
                y = info.Finish(sp, 4, y);
                Pad(sp, y);
                return;
            }

            if (off)
            {
                var note = new StatCard(w, null);
                note.Text("The AS Time Tracker addon is turned off. Turn it on in Addons to keep recording your runs.",
                          10, FontStyle.Italic, Theme.Bad, 0);
                y = note.Finish(sp, 4, y);
            }

            y = TotalsCard(sp, w, y, st.Total);
            y = BestsCard(sp, w, y, st.Total.Best);

            // --- milestones
            var miles = new StatCard(w, "Milestones");
            miles.Bar("Runs", st.Total.Runs, RunSteps, false);
            miles.Bar("Victories", st.Total.Victories, WinSteps, false);
            miles.Bar("Enemies slain", st.Total.Kills, KillSteps, false);
            miles.Bar("Bosses", st.Total.Bosses, BossSteps, false);
            miles.Bar("Gold collected", st.Total.Gold, GoldSteps, false);
            miles.Bar("Hours played", Math.Floor(ps.Seconds / 3600.0), HourSteps, true);
            y = miles.Finish(sp, 4, y);

            // --- how often each class is picked, overall and month by month
            if (st.Classes.Count > 0)
            {
                var pick = new StatCard(w, "Class pick rate");
                foreach (var c in st.Classes)
                    pick.Share(c.Name, Lang.T("Runs: " + Big(c.Runs) + " · Won: " + Big(c.Victories) + " · Longest: " +
                                              Clock(c.BestTime) + " · Best level: " + Big(c.BestLevel)),
                               st.Total.Runs > 0 ? c.Runs / st.Total.Runs : 0, Pct(c.Runs, st.Total.Runs) + "%", ClassColor(c.Key));
                pick.Text("Choose a class at the top to see its own stats.", 9, FontStyle.Italic, Theme.GoldDim, 8);
                y = pick.Finish(sp, 4, y);
            }
            if (st.Months.Count > 0)
            {
                var names = new Dictionary<string, string>();
                foreach (var c in st.Classes) names[c.Key] = c.Name;
                var over = new StatCard(w, "Pick rate over time");
                over.Text("Share of your runs each month", 9, FontStyle.Italic, Theme.GoldDim, 2);
                over.Add(new MonthChart(over.ContentWidth, st.Months, names), 8);
                y = over.Finish(sp, 4, y);
            }

            y = AbilitiesCard(sp, w, y, st.Abilities, st.Total.Runs);

            // --- races and zones
            var groups = new[] { new object[] { "Races", st.Races }, new object[] { "Zones", st.Zones } };
            foreach (var g in groups)
            {
                var list = (List<StatBlock>)g[1];
                if (list.Count == 0) continue;
                var card = new StatCard(w, (string)g[0]);
                foreach (var r in list)
                    card.Row(r.Name, Lang.T("Runs: " + Big(r.Runs) + " · Won: " + Big(r.Victories) + " · Longest: " + Clock(r.BestTime) +
                                          " · Best level: " + Big(r.BestLevel)), Lang.T(Big(r.Kills) + " kills"), false, Theme.Text);
                y = card.Finish(sp, 4, y);
            }

            y = RecentCard(sp, w, y, st.History);
            StatsFoot(sp, w, y, st);
        }

        // one class on its own: the same totals, records and abilities, its picks per month and its runs
        int ClassView(ScrollPanel sp, int w, int y, RunStats st, StatBlock cls)
        {
            var head = new StatCard(w, null);
            head.Text(cls.Name, 16, FontStyle.Bold, ClassColor(cls.Key), 0, false);
            head.Text(cls.Runs == 1 ? "Your first run is in the books!" :
                      Big(cls.Kills) + " enemies slain in " + Big(cls.Runs) + " runs!", 12, FontStyle.Bold, Theme.Gold, 2);
            if (cls.Victories > 0)
                head.Text(Big(cls.Victories) + " victories, " + Big(cls.Bosses) + " bosses defeated and " + Big(cls.Gold) +
                          " gold collected.", 10, FontStyle.Italic, Theme.Text, 4);
            y = head.Finish(sp, 4, y);
            y = TotalsCard(sp, w, y, cls);
            y = BestsCard(sp, w, y, cls.Best);
            y = AbilitiesCard(sp, w, y, cls.Abilities, cls.Runs);
            if (st.Months.Count > 0)
            {
                var months = new StatCard(w, "Picks per month");
                for (int i = st.Months.Count - 1; i >= 0 && i >= st.Months.Count - 12; i--)
                {
                    MonthStat m = st.Months[i];
                    double n;
                    m.Classes.TryGetValue(cls.Key, out n);
                    months.Share(m.Month == "unknown" ? "?" : m.Month, null, m.Runs > 0 ? n / m.Runs : 0,
                                 Big(n) + " / " + Big(m.Runs) + "  ·  " + Pct(n, m.Runs) + "%", ClassColor(cls.Key));
                }
                y = months.Finish(sp, 4, y);
            }
            var mine = st.History.FindAll(delegate(StatRun r) { return r.ClassKey == cls.Key; });
            return RecentCard(sp, w, y, mine);
        }

        int TotalsCard(ScrollPanel sp, int w, int y, StatBlock b)
        {
            var runs = new StatCard(w, "Your runs");
            runs.Tiles(new List<string[]> {
                new[] { Big(b.Runs), "Runs" },
                new[] { Big(b.Victories), "Victories" },
                new[] { Pct(b.Victories, b.Runs) + "%", "Win rate" },
                new[] { Duration((long)b.Time), "Time in runs" },
                new[] { Big(b.Kills), "Enemies slain" },
                new[] { Big(b.Elites), "Elites" },
                new[] { Big(b.Guardians), "Guardians" },
                new[] { Big(b.Bosses), "Bosses" },
                new[] { Big(b.Damage), "Damage dealt" },
                new[] { Big(b.Taken), "Damage taken" },
                new[] { Big(b.Gold), "Gold collected" },
                new[] { Big(b.Chests), "Caches opened" } }, 4);
            return runs.Finish(sp, 4, y);
        }

        int BestsCard(ScrollPanel sp, int w, int y, Dictionary<string, StatRecord> best)
        {
            var bests = new StatCard(w, "Personal bests");
            var order = new[] {
                new[] { "time", "Longest run", "clock" }, new[] { "fastestWin", "Fastest victory", "clock" },
                new[] { "lvl", "Highest level", "" }, new[] { "kills", "Most kills in a run", "" },
                new[] { "dmg", "Most damage in a run", "" }, new[] { "gold", "Most gold in a run", "" },
                new[] { "bosses", "Most bosses in a run", "" } };
            foreach (var o in order)
            {
                StatRecord b;
                if (!best.TryGetValue(o[0], out b) || b.Value <= 0) continue;
                bests.Row(o[1], Dots(b.Hero, Pair(b.Class, b.Race.Length > 0 ? "(" + b.Race + ")" : ""), b.Zone, b.Date),
                          o[2] == "clock" ? Clock(b.Value) : Big(b.Value), true, Theme.Gold);
            }
            return bests.Finish(sp, 4, y);
        }

        // the abilities taken: how often, their win rate and best rank, and the damage they dealt
        int AbilitiesCard(ScrollPanel sp, int w, int y, List<AbilityStat> list, double runs)
        {
            var card = new StatCard(w, "Abilities");
            if (list.Count == 0)
                card.Text("Abilities are recorded for runs played with the newest AS Time Tracker.", 10, FontStyle.Italic, Theme.GoldDim, 6);
            for (int i = 0; i < Math.Min(15, list.Count); i++)
            {
                AbilityStat a = list[i];
                string sub = Lang.T("Picked in " + Big(a.Picks) + " runs (" + Pct(a.Picks, runs) + "%) · Win rate " +
                                    Pct(a.Wins, a.Picks) + "%");
                if (a.BestRank > 0) sub += " · " + Lang.T("Best rank " + Big(a.BestRank));
                card.Row(a.Name, sub, Lang.T(Big(a.Damage) + " damage"), false, Theme.Text);
            }
            return card.Finish(sp, 4, y);
        }

        int RecentCard(ScrollPanel sp, int w, int y, List<StatRun> runs)
        {
            if (runs.Count == 0) return y;
            var recent = new StatCard(w, "Recent runs");
            for (int i = 0; i < Math.Min(10, runs.Count); i++)
            {
                StatRun r = runs[i];
                string title = Lang.T(ResultName(r.Kind, r.Result)) + " · " +
                               Pair(r.Class, r.Race.Length > 0 ? "(" + r.Race + ")" : "") + (r.Zone.Length > 0 ? " · " + r.Zone : "");
                string sub = Lang.T("Survived " + Clock(r.Time) + " · Level " + Big(r.Level) + " · Kills " + Big(r.Kills) +
                                    " · Gold " + Big(r.Gold));
                if (r.Date.Length > 0) sub = r.Date + "   " + sub;
                if (r.Abilities.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var a in r.Abilities) parts.Add(a.Picks > 0 ? a.Name + " " + Big(a.Picks) : a.Name);
                    sub += "\n" + string.Join(", ", parts.ToArray());
                }
                recent.Row(title, sub, Clock(r.Time), false,
                           r.Kind == "victory" ? Theme.Good : r.Kind == "defeat" ? Theme.Bad : Theme.GoldDim);
            }
            return recent.Finish(sp, 4, y);
        }

        void StatsFoot(ScrollPanel sp, int w, int y, RunStats st)
        {
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

        // installs (or updates) the AS Time Tracker built into the launcher
        void InstallStatsAddon()
        {
            if (AddonsLocked()) return;
            string dir = AddonsDir();
            bool had = Stats.AddonInstalled(dir);
            try
            {
                Stats.InstallAddon(dir, SyncSettings().SavedDir);
                SetStatus(had ? "AS Time Tracker updated." : "AS Time Tracker installed. It records your runs from now on.");
            }
            catch (Exception e)
            {
                Msg.Show(side.Visible ? (IWin32Window)side : this, "Could not install AS Time Tracker:\n\n" + e.Message, "Stats",
                         MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (current == "Stats") BuildPanel("Stats");
        }
    }

    // Pick rate over time: one bar a month (the last 12), split into the classes played that month, and the
    // classes' colors underneath
    public class MonthChart : Control
    {
        readonly List<MonthStat> months;
        readonly List<string> order = new List<string>();
        readonly Dictionary<string, string> names;
        const int RowH = 22, LabelW = 70, LegendH = 20;
        int legendRows = 1;

        public MonthChart(int width, List<MonthStat> all, Dictionary<string, string> names)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Ink;
            this.names = names;
            months = all.Count > 12 ? all.GetRange(all.Count - 12, 12) : new List<MonthStat>(all);
            var totals = new Dictionary<string, double>();
            foreach (var m in months)
                foreach (var kv in m.Classes)
                {
                    double t;
                    totals.TryGetValue(kv.Key, out t);
                    totals[kv.Key] = t + kv.Value;
                }
            order.AddRange(totals.Keys);
            order.Sort(delegate(string a, string b) { return totals[b].CompareTo(totals[a]); });
            // legend: as many names in a row as fit
            int x = 0;
            Font f = Theme.F(9);
            foreach (string k in order)
            {
                int iw = 16 + TextRenderer.MeasureText(Name(k), f).Width + 12;
                if (x > 0 && x + iw > width) { legendRows++; x = 0; }
                x += iw;
            }
            Size = new Size(width, months.Count * RowH + 6 + legendRows * LegendH);
        }

        string Name(string key)
        {
            string n;
            return names != null && names.TryGetValue(key, out n) && n.Length > 0 ? n : Stats.Pretty(key);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Font f = Theme.F(9);
            int barW = Width - LabelW - 4;
            for (int i = 0; i < months.Count; i++)
            {
                MonthStat m = months[i];
                int top = i * RowH + 3;
                TextRenderer.DrawText(g, m.Month == "unknown" ? "?" : m.Month, f, new Point(0, top + 1), Theme.Text);
                using (var b = new SolidBrush(Theme.Bronze)) g.FillRectangle(b, LabelW, top + 3, barW, RowH - 8);
                float x = LabelW;
                foreach (string k in order)
                {
                    double n;
                    if (!m.Classes.TryGetValue(k, out n) || n <= 0 || m.Runs <= 0) continue;
                    float segW = (float)(barW * n / m.Runs);
                    using (var b = new SolidBrush(LauncherApp.ClassColorOf(k))) g.FillRectangle(b, x, top + 3, segW, RowH - 8);
                    x += segW;
                }
            }
            int lx = 0, ly = months.Count * RowH + 6;
            foreach (string k in order)
            {
                string n = Name(k);
                int iw = 16 + TextRenderer.MeasureText(n, f).Width + 12;
                if (lx > 0 && lx + iw > Width) { lx = 0; ly += LegendH; }
                using (var b = new SolidBrush(LauncherApp.ClassColorOf(k))) g.FillRectangle(b, lx, ly + 4, 11, 11);
                TextRenderer.DrawText(g, n, f, new Point(lx + 16, ly + 1), Theme.Text);
                lx += iw;
            }
        }
    }
}
