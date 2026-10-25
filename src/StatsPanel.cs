// The STATS section, laid out like a WoW armory page: a character header (every class together, or one class),
// tabs (Overview, Classes, Abilities, Records, History) and rounded cards (ArmoryUI.cs). The launcher's own play
// time is always there; the run records come from the optional AZS Stats addon (see Stats.cs). Turned off
// in Options, the section and its buttons disappear.
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

        static readonly double[] RunSteps = { 1, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        static readonly double[] WinSteps = { 1, 5, 10, 25, 50, 100, 250, 500, 1000 };
        static readonly double[] KillSteps = { 100, 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000, 10000000 };
        static readonly double[] BossSteps = { 1, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        static readonly double[] GoldSteps = { 100, 1000, 5000, 10000, 50000, 100000, 500000, 1000000, 5000000 };
        static readonly double[] HourSteps = { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500 };

        static readonly string[] StatTabs = { "Overview", "Classes", "Abilities", "Records", "Nemesis", "Co-op", "History" };
        string statsClass;                                   // null = every class, else a class key
        int statsTab;

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

        void SetStatsView(string cls, int tab)
        {
            statsClass = cls;
            statsTab = tab;
            if (current == "Stats") BuildPanel("Stats");
        }

        // adds a block to the page at y; returns where the next one goes
        static int Place(ScrollPanel sp, ArmoryBlock b, int w, int y)
        {
            b.Fit(w);
            b.Location = new Point(4, y);
            sp.Controls.Add(b);
            return y + b.Height + 10;
        }

        static TextBlock Note(string title, string text, Color color)
        {
            var t = new TextBlock();
            t.Title = title == null ? null : Lang.T(title);
            t.Lines.Add(new object[] { Lang.T(text), color, 10f, false });
            return t;
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
            bool data = st != null && st.Total.Runs > 0;
            StatBlock cls = data && statsClass != null ? st.Class(statsClass) : null;
            if (cls == null) statsClass = null;
            if (statsTab < 0 || statsTab >= StatTabs.Length) statsTab = 0;
            if (cls != null) Put(new WowButton("‹ ALL CLASSES", delegate { SetStatsView(null, 1); }, 170, 32, 10), 84, 12);

            var sp = MakeThinScroll(panel, new Rectangle(20, 56, PW - 40, PH - 12 - 56), Armory.Page, Theme.Bronze,
                                    Armory.SlotEdge, Theme.GoldDim);
            int w = sp.ClientSize.Width - 10;
            int y = 4;
            PlayStats ps = ShownPlayStats();

            // --- the header
            var hero = new HeroBlock();
            if (cls != null)
            {
                hero.Heading = cls.Name;
                hero.Accent = ClassColor(cls.Key);
                hero.Letters = Armory.Initials(cls.Name);
                hero.ClassPic = cls.Key;
                hero.Line = Lang.T("Runs " + Big(cls.Runs) + " · Victories " + Big(cls.Victories) + " · Best level " + Big(cls.BestLevel)) +
                            "   " + Lang.T("Picked in " + Pct(cls.Runs, st.Total.Runs) + "% of your runs");
                AddChips(hero, cls, null);
            }
            else
            {
                hero.Heading = Lang.T("Your Survivor");
                hero.Line = data ? Lang.T("Runs " + Big(st.Total.Runs) + " · Victories " + Big(st.Total.Victories) + " · Best level " +
                                          Big(st.Total.BestLevel))
                                 : Lang.T("No runs recorded yet.");
                AddChips(hero, data ? st.Total : null, ps);
            }
            if (st != null) hero.Note = Lang.T("Saved by the game " + st.Written.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ".");
            y = Place(sp, hero, w, y);

            // --- no run records (yet): play time and how to get more
            if (!data)
            {
                y = PlayTimeBlocks(sp, w, y, ps);
                TextBlock info;
                if (err != null) info = Note("Your runs", "The stats file could not be read (" + err + ").", Theme.Bad);
                else if (!have)
                {
                    info = Note("Track every run", "The AZS Stats addon keeps a record of every run you play: victories, kills, " +
                                "damage, gold and your personal bests. Install it, finish a run, and your stats appear here.", Armory.Bright);
                    info.Lines.Add(new object[] { Lang.T("It is optional: without it, this page only shows your play time."), Armory.Dim, 9f, true });
                    info.Button = new WowButton("INSTALL STATS ADDON", InstallStatsAddon, 230, 32, 10);
                }
                else if (off) info = Note("Your runs", "The AZS Stats addon is turned off. Turn it on in Addons to keep recording your runs.", Armory.Bright);
                else info = Note("Your runs", "No runs recorded yet. Finish a run with the AZS Stats addon turned on. The game saves " +
                                 "addon data when you leave it, so your stats show up here after you close the game.", Armory.Bright);
                y = Place(sp, info, w, y);
                Pad(sp, y);
                return;
            }
            if (off) y = Place(sp, Note(null, "The AZS Stats addon is turned off. Turn it on in Addons to keep recording your runs.", Theme.Bad), w, y);

            // --- the tabs
            var tabs = new TabsBlock();
            foreach (string t in StatTabs) tabs.Tabs.Add(Lang.T(t));
            tabs.Active = statsTab;
            string clsKey = cls != null ? cls.Key : null;
            tabs.Picked = delegate(int i) { SetStatsView(clsKey, i); };
            y = Place(sp, tabs, w, y);

            StatBlock b = cls ?? st.Total;
            List<StatRun> runs = cls == null ? st.History : st.History.FindAll(delegate(StatRun r) { return r.ClassKey == cls.Key; });
            var names = new Dictionary<string, string>();
            foreach (var c in st.Classes) names[c.Key] = c.Name;

            if (statsTab == 0)                               // Overview
            {
                y = Place(sp, Totals(b), w, y);
                if (cls == null)
                {
                    y = Place(sp, Milestones(st, ps), w, y);
                    y = PlayTimeBlocks(sp, w, y, ps);
                }
                else
                {
                    var top = AbilityRows(cls.Abilities, cls.Runs, 4);
                    top.Title = Lang.T("Abilities");
                    y = Place(sp, top, w, y);
                }
            }
            else if (statsTab == 1)                          // Classes
            {
                if (cls == null)
                {
                    var list = new RowsBlock();
                    list.Title = Lang.T("Class pick rate");
                    foreach (var c in st.Classes)
                    {
                        string key = c.Key;
                        var row = new ArmoryRow();
                        row.Icon = Armory.Initials(c.Name);
                        row.ClassPic = key;
                        row.RoundIcon = true;
                        row.IconColor = ClassColor(key);
                        row.Name = c.Name;
                        row.NameColor = ClassColor(key);
                        row.Sub = Lang.T("Runs: " + Big(c.Runs) + " · Won: " + Big(c.Victories) + " · Longest: " + Clock(c.BestTime) +
                                         " · Best level: " + Big(c.BestLevel));
                        row.Value = Pct(c.Runs, st.Total.Runs) + "%";
                        row.ValueSub = Lang.T("Win rate") + " " + Pct(c.Victories, c.Runs) + "%";
                        row.Bar = st.Total.Runs > 0 ? c.Runs / st.Total.Runs : 0;
                        row.Click = delegate { SetStatsView(key, 0); };
                        list.Rows.Add(row);
                    }
                    y = Place(sp, list, w, y);
                    var hint = Note(null, "Choose a class to see its own stats.", Armory.Dim);
                    hint.Lines[0][2] = 9f;
                    hint.Lines[0][3] = true;
                    y = Place(sp, hint, w, y);
                    if (st.Months.Count > 0)
                    {
                        var months = new MonthsBlock(st.Months, names);
                        months.Title = Lang.T("Pick rate over time");
                        y = Place(sp, months, w, y);
                    }
                    if (st.Races.Count > 0) y = Place(sp, GroupRows("Races", st.Races, Theme.Gold), w, y);
                    if (st.Zones.Count > 0) y = Place(sp, GroupRows("Zones", st.Zones, Theme.Hex("#5fb0d3")), w, y);
                }
                else if (st.Months.Count > 0)
                {
                    var months = new MonthsBlock(st.Months, names);
                    months.Only = cls.Key;
                    months.Title = Lang.T("Picks per month");
                    y = Place(sp, months, w, y);
                }
            }
            else if (statsTab == 2)                          // Abilities
            {
                var list = AbilityRows(cls != null ? cls.Abilities : st.Abilities, b.Runs, 30);
                list.Title = Lang.T("Abilities");
                y = Place(sp, list, w, y);
            }
            else if (statsTab == 3)                          // Records
                y = Place(sp, Records(b.Best), w, y);
            else if (statsTab == 4)                          // Nemesis
                y = NemesisTab(sp, w, y, st, cls);
            else if (statsTab == 5)                          // Co-op
                y = CoopTab(sp, w, y, st);
            else                                             // History
                y = Place(sp, History(runs), w, y);

            var foot = Note(null, "In game, type /azstats to see your stats.", Armory.Dim);
            foot.Lines[0][2] = 9f;
            foot.Lines[0][3] = true;
            y = Place(sp, foot, w, y);
            Pad(sp, y);
        }

        // ------------------------------------------------------------------ Nemesis: who kills you, and the death log
        int NemesisTab(ScrollPanel sp, int w, int y, RunStats st, StatBlock cls)
        {
            List<NemesisStat> killers = cls != null ? cls.Nemesis : st.Nemesis;
            List<DeathStat> deaths = cls == null ? st.Deaths : st.Deaths.FindAll(delegate(DeathStat d) { return d.ClassKey == cls.Key; });
            double total = 0;
            foreach (var k in killers) total += k.Kills;
            if (killers.Count > 0)
            {
                var top = killers[0];
                var hero = new TilesBlock();
                hero.Title = Lang.T("Your nemesis") + ": " + top.Name;
                hero.Items.Add(new[] { Big(top.Kills), Lang.T("Times it killed you") });
                hero.Items.Add(new[] { Pct(top.Kills, total) + "%", Lang.T("Of your deaths") });
                hero.Items.Add(new[] { Big(total), Lang.T("Deaths") });
                hero.Items.Add(new[] { top.Last.Length >= 10 ? top.Last.Substring(0, 10) : top.Last, Lang.T("Last time") });
                y = Place(sp, hero, w, y);
            }
            var rows = new RowsBlock();
            rows.Title = Lang.T("Who kills you");
            rows.Empty = Lang.T("No deaths recorded yet.");
            foreach (var k in killers)
            {
                var row = new ArmoryRow();
                row.Icon = Armory.Initials(k.Name);
                row.Npc = k.Name;                            // the creature's picture, from Wowhead
                row.IconColor = Theme.Hex("#c41f3b");
                row.Name = k.Name;
                row.NameColor = Theme.Hex("#ff5a4a");
                row.Sub = Lang.T("Killed you " + Big(k.Kills) + " times (" + Pct(k.Kills, total) + "%)");
                row.Value = Big(k.Kills);
                row.ValueSub = k.Last;
                row.Bar = total > 0 ? k.Kills / total : 0;
                rows.Rows.Add(row);
                if (rows.Rows.Count >= 15) break;
            }
            y = Place(sp, rows, w, y);
            var log = new RowsBlock();
            log.Title = Lang.T("Death log");
            log.Empty = Lang.T("No deaths recorded yet.");
            foreach (var d in deaths)
            {
                var row = new ArmoryRow();
                row.Icon = "X";
                row.Npc = d.Killer;
                row.IconColor = ClassColor(d.ClassKey);
                row.RoundIcon = true;
                row.Name = Lang.T("Slain by " + d.Killer) + (d.Spell.Length > 0 ? " · " + d.Spell : "") +
                           (d.Amount > 0 ? " " + Big(d.Amount) : "");
                row.NameColor = Theme.Hex("#ff5a4a");
                row.Sub = Dots(Pair(d.Class, d.Race.Length > 0 ? "(" + d.Race + ")" : ""), d.Zone,
                               Lang.T("Survived " + Clock(d.Time) + " · Level " + Big(d.Level)));
                row.Sub2 = d.LastHits.Length > 0 ? Lang.T("Last hits: " + d.LastHits)
                         : d.TopSources.Length > 0 ? Lang.T("Most damage from: " + d.TopSources) : "";
                row.Value = Clock(d.Time);
                row.ValueSub = d.Date;
                log.Rows.Add(row);
                if (log.Rows.Count >= 30) break;
            }
            return Place(sp, log, w, y);
        }

        // ------------------------------------------------------------------ Co-op: who you play with
        int CoopTab(ScrollPanel sp, int w, int y, RunStats st)
        {
            CoopStat co = st.Coop;
            var tiles = new TilesBlock();
            tiles.Title = Lang.T("Co-op");
            tiles.Items.Add(new[] { Big(co.Runs), Lang.T("Runs with friends") });
            tiles.Items.Add(new[] { Pct(co.Wins, co.Runs) + "%", Lang.T("Co-op win rate") });
            tiles.Items.Add(new[] { Pct(co.SoloWins, co.Solo) + "%", Lang.T("Solo win rate") });
            tiles.Items.Add(new[] { Duration((long)co.Time), Lang.T("Time together") });
            tiles.Items.Add(new[] { Big(co.Friends.Count), Lang.T("Friends") });
            tiles.Items.Add(new[] { Big(co.Wins), Lang.T("Victories together") });
            tiles.Items.Add(new[] { Big(co.Kills), Lang.T("Enemies slain together") });
            tiles.Items.Add(new[] { Big(co.Bosses), Lang.T("Bosses together") });
            y = Place(sp, tiles, w, y);

            // the friend you play with most, at a glance
            if (co.Friends.Count > 0)
            {
                FriendStat top = co.Friends[0];
                var best = new TilesBlock();
                best.Title = Lang.T("Your top partner") + ": " + top.Name + (top.Class.Length > 0 ? " (" + top.Class + ")" : "");
                best.Items.Add(new[] { Big(top.Runs), Lang.T("Runs together") });
                best.Items.Add(new[] { Big(top.Wins), Lang.T("Victories together") });
                best.Items.Add(new[] { Pct(top.Wins, top.Runs) + "%", Lang.T("Win rate together") });
                best.Items.Add(new[] { Duration((long)top.Time), Lang.T("Time together") });
                y = Place(sp, best, w, y);
            }

            // everyone, most runs together first: who, how it went, and when you last played
            var friends = new RowsBlock();
            friends.Title = Lang.T("Your party");
            friends.Empty = Lang.T("No co-op runs recorded yet. Play a run with friends and they show up here.");
            double most = co.Friends.Count > 0 ? co.Friends[0].Runs : 1;
            foreach (var f in co.Friends)
            {
                var row = new ArmoryRow();
                row.Icon = Armory.Initials(f.Name);
                row.ClassPic = f.ClassKey.Length > 0 ? f.ClassKey : null;     // their class's icon
                row.RoundIcon = true;
                row.IconColor = f.ClassKey.Length > 0 ? ClassColor(f.ClassKey) : Theme.Hex("#5fb0d3");
                row.Name = f.Name + (f.Class.Length > 0 ? " · " + f.Class : "");
                row.NameColor = row.IconColor;
                row.Sub = Lang.T(Big(f.Runs) + " runs together · " + Big(f.Wins) + " won (" + Pct(f.Wins, f.Runs) + "%)");
                row.SubColor = Armory.Bright;
                var more = new List<string>();
                if (f.BestTime > 0) more.Add(Lang.T("Best together: " + Clock(f.BestTime) + ", level " + Big(f.BestLevel)));
                if (f.FastestWin > 0) more.Add(Lang.T("fastest win " + Clock(f.FastestWin)));
                if (f.First.Length >= 10) more.Add(Lang.T("since " + f.First.Substring(0, 10)));
                row.Sub2 = string.Join(" · ", more.ToArray());
                row.Value = Ago(f.Last);
                row.ValueSub = Lang.T("last played");
                row.Bar = most > 0 ? f.Runs / most : 0;
                friends.Rows.Add(row);
                if (friends.Rows.Count >= 30) break;
            }
            y = Place(sp, friends, w, y);

            if (co.Best.Count > 0)
            {
                var best = new RowsBlock();
                best.Title = Lang.T("Accomplishments together");
                var order = new[] {
                    new[] { "time", "Longest run", "clock" }, new[] { "fastestWin", "Fastest victory", "clock" },
                    new[] { "lvl", "Highest level", "" }, new[] { "kills", "Most kills in a run", "" },
                    new[] { "bosses", "Most bosses in a run", "" }, new[] { "gold", "Most gold in a run", "" } };
                foreach (var o in order)
                {
                    StatRecord r;
                    if (!co.Best.TryGetValue(o[0], out r) || r.Value <= 0) continue;
                    var row = new ArmoryRow();
                    row.Icon = Armory.Initials(Lang.T(o[1]));
                    row.IconColor = Theme.Hex("#5fb0d3");
                    row.Name = Lang.T(o[1]);
                    row.NameColor = Theme.Hex("#8fd0ff");
                    row.Sub = Lang.T("With " + r.Hero);
                    row.Value = o[2] == "clock" ? Clock(r.Value) : Big(r.Value);
                    row.ValueSub = r.Date;
                    best.Rows.Add(row);
                }
                y = Place(sp, best, w, y);
            }
            return y;
        }

        // the header's chips: kills, bosses, gold, win rate and time (play time on the overall page)
        void AddChips(HeroBlock hero, StatBlock b, PlayStats ps)
        {
            if (b != null)
            {
                hero.Chips.Add(new[] { "kills", Big(b.Kills), Lang.T("Enemies slain") });
                hero.Chips.Add(new[] { "bosses", Big(b.Bosses), Lang.T("Bosses") });
                hero.Chips.Add(new[] { "gold", Big(b.Gold), Lang.T("Gold collected") });
                hero.Chips.Add(new[] { "win", Pct(b.Victories, b.Runs) + "%", Lang.T("Win rate") });
            }
            if (ps != null && ps.Sessions > 0) hero.Chips.Add(new[] { "time", Duration(ps.Seconds), Lang.T("Time played") });
            else if (b != null) hero.Chips.Add(new[] { "time", Duration((long)b.Time), Lang.T("Time in runs") });
        }

        int PlayTimeBlocks(ScrollPanel sp, int w, int y, PlayStats ps)
        {
            if (ps.Sessions == 0) return Place(sp, Note("Play time", "No play time counted yet. AZS Stats counts it while the game runs.", Armory.Dim), w, y);
            var tiles = new TilesBlock();
            tiles.Title = Lang.T("Play time");
            tiles.Items.Add(new[] { Duration(ps.Seconds), Lang.T("Time played") });
            tiles.Items.Add(new[] { Big(ps.Sessions), Lang.T("Sessions") });
            tiles.Items.Add(new[] { Duration(ps.Longest), Lang.T("Longest session") });
            tiles.Items.Add(new[] { Duration(ps.LastSeconds), Lang.T("Last session") });
            y = Place(sp, tiles, w, y);
            string note = CloudEnabled() ? "Counted in game by AZS Stats; cloud saving carries it to your other PCs."
                                         : "Counted in game by AZS Stats. Turn on cloud saving to take it to your other PCs.";
            var n = Note(null, note, Armory.Dim);
            n.Lines[0][2] = 9f;
            n.Lines[0][3] = true;
            return Place(sp, n, w, y);
        }

        static TilesBlock Totals(StatBlock b)
        {
            var t = new TilesBlock();
            t.Title = Lang.T("Your runs");
            string[][] items = {
                new[] { Big(b.Runs), "Runs" }, new[] { Big(b.Victories), "Victories" },
                new[] { Pct(b.Victories, b.Runs) + "%", "Win rate" }, new[] { Duration((long)b.Time), "Time in runs" },
                new[] { Big(b.Kills), "Enemies slain" }, new[] { Big(b.Elites), "Elites" },
                new[] { Big(b.Guardians), "Guardians" }, new[] { Big(b.Bosses), "Bosses" },
                new[] { Big(b.Damage), "Damage dealt" }, new[] { Big(b.Taken), "Damage taken" },
                new[] { Big(b.Gold), "Gold collected" }, new[] { Big(b.Chests), "Caches opened" } };
            foreach (var i in items) t.Items.Add(new[] { i[0], Lang.T(i[1]) });
            return t;
        }

        static BarsBlock Milestones(RunStats st, PlayStats ps)
        {
            var bars = new BarsBlock();
            bars.Title = Lang.T("Milestones");
            var list = new[] {
                new object[] { "Runs", st.Total.Runs, RunSteps }, new object[] { "Victories", st.Total.Victories, WinSteps },
                new object[] { "Enemies slain", st.Total.Kills, KillSteps }, new object[] { "Bosses", st.Total.Bosses, BossSteps },
                new object[] { "Gold collected", st.Total.Gold, GoldSteps },
                new object[] { "Hours played", Math.Floor(ps.Seconds / 3600.0), HourSteps } };
            foreach (var m in list)
            {
                double value = (double)m[1], next = 0, prev = 0;
                foreach (double s in (double[])m[2])
                {
                    if (value < s) { next = s; break; }
                    prev = s;
                }
                bars.Bars.Add(new object[] {
                    Lang.T((string)m[0]),
                    next > 0 ? Lang.T(Big(value) + " of " + Big(next)) : Lang.T("Every milestone reached!"),
                    next > 0 ? (value - prev) / Math.Max(1, next - prev) : 1.0,
                    next > 0 ? Theme.Gold : Armory.Green });
            }
            return bars;
        }

        // abilities as gear: the more often taken, the rarer the color
        static RowsBlock AbilityRows(List<AbilityStat> list, double runs, int max)
        {
            var rows = new RowsBlock();
            rows.Empty = Lang.T("Abilities are recorded for runs played with the newest AZS Stats.");
            for (int i = 0; i < Math.Min(max, list.Count); i++)
            {
                AbilityStat a = list[i];
                double share = runs > 0 ? a.Picks / runs : 0;
                var row = new ArmoryRow();
                row.Icon = Armory.Initials(a.Name);
                row.Spell = a.Name;                          // its icon, shown from Wowhead when there is one
                row.IconColor = Theme.GoldDim;               // one color for every ability
                row.Name = a.Name;
                row.NameColor = Armory.Bright;
                row.Sub = Lang.T("Picked in " + Big(a.Picks) + " runs (" + Pct(a.Picks, runs) + "%) · Win rate " + Pct(a.Wins, a.Picks) + "%");
                row.Value = Lang.T(Big(a.Damage) + " damage");
                if (a.BestRank > 0) row.ValueSub = Lang.T("Best rank " + Big(a.BestRank));
                row.Bar = share;
                rows.Rows.Add(row);
            }
            return rows;
        }

        static RowsBlock Records(Dictionary<string, StatRecord> best)
        {
            var rows = new RowsBlock();
            rows.Title = Lang.T("Personal bests");
            rows.Cols = 1;                                   // one a row: the names stay whole
            rows.Empty = Lang.T("No runs recorded yet.");
            var order = new[] {
                new[] { "time", "Longest run", "clock", "#a335ee" }, new[] { "fastestWin", "Fastest victory", "clock", "#ff8000" },
                new[] { "lvl", "Highest level", "", "#3b8ff0" }, new[] { "kills", "Most kills in a run", "", "#c41f3b" },
                new[] { "dmg", "Most damage in a run", "", "#ff8000" }, new[] { "gold", "Most gold in a run", "", "#ffd100" },
                new[] { "bosses", "Most bosses in a run", "", "#a335ee" }, new[] { "elites", "Elites", "", "#1eff00" },
                new[] { "guardians", "Guardians", "", "#69ccf0" } };
            foreach (var o in order)
            {
                StatRecord b;
                if (!best.TryGetValue(o[0], out b) || b.Value <= 0) continue;
                var row = new ArmoryRow();
                row.Name = Lang.T(o[1]);
                row.Icon = Armory.Initials(row.Name);
                row.IconColor = Theme.Hex(o[3]);
                row.NameColor = Theme.Hex(o[3]);
                row.Value = o[2] == "clock" ? Clock(b.Value) : Big(b.Value);
                row.Sub = Dots(b.Class, b.Zone);
                row.ValueSub = b.Date.Length >= 10 ? b.Date.Substring(0, 10) : b.Date;
                rows.Rows.Add(row);
            }
            return rows;
        }

        // "today", "yesterday", "3 days ago", "5 weeks ago" ... from a "yyyy-MM-dd HH:mm" date
        static string Ago(string when)
        {
            DateTime d;
            if (string.IsNullOrEmpty(when) || !DateTime.TryParse(when.Length >= 10 ? when.Substring(0, 10) : when,
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return when ?? "";
            int days = (int)(DateTime.Today - d.Date).TotalDays;
            if (days <= 0) return Lang.T("today");
            if (days == 1) return Lang.T("yesterday");
            if (days < 14) return Lang.T(days + " days ago");
            if (days < 60) return Lang.T((days / 7) + " weeks ago");
            if (days < 730) return Lang.T((days / 30) + " months ago");
            return Lang.T((days / 365) + " years ago");
        }

        static RowsBlock History(List<StatRun> runs)
        {
            var rows = new RowsBlock();
            rows.Title = Lang.T("Recent runs");
            rows.Empty = Lang.T("No runs recorded yet.");
            for (int i = 0; i < Math.Min(20, runs.Count); i++)
            {
                StatRun r = runs[i];
                var row = new ArmoryRow();
                Color rc = r.Kind == "victory" ? Armory.Green : r.Kind == "defeat" ? Theme.Bad : Armory.Dim;
                row.Icon = Armory.Initials(r.Class);
                row.ClassPic = r.ClassKey.Length > 0 ? r.ClassKey : r.Class;
                row.RoundIcon = true;
                row.IconColor = ClassColor(r.ClassKey.Length > 0 ? r.ClassKey : r.Class);
                row.Name = Lang.T(ResultName(r.Kind, r.Result)) + " · " + Pair(r.Class, r.Race.Length > 0 ? "(" + r.Race + ")" : "") +
                           (r.Zone.Length > 0 ? " · " + r.Zone : "");
                row.NameColor = rc;
                var parts = new List<string>();
                foreach (var a in r.Abilities) parts.Add(a.Picks > 0 ? a.Name + " " + Big(a.Picks) : a.Name);
                row.Sub = (r.Date.Length > 0 ? r.Date + "   " : "") +
                          (parts.Count > 0 ? string.Join(", ", parts.ToArray())
                                           : Lang.T("Survived " + Clock(r.Time) + " · Level " + Big(r.Level) + " · Kills " + Big(r.Kills) +
                                                    " · Gold " + Big(r.Gold)));
                row.Value = Clock(r.Time);
                row.ValueColor = rc;
                row.ValueSub = Lang.T("Level " + Big(r.Level)) + " · " + Lang.T(Big(r.Kills) + " kills");
                rows.Rows.Add(row);
            }
            return rows;
        }

        static RowsBlock GroupRows(string title, List<StatBlock> list, Color color)
        {
            var rows = new RowsBlock();
            rows.Title = Lang.T(title);
            rows.Cols = 2;
            foreach (var g in list)
            {
                var row = new ArmoryRow();
                row.Icon = Armory.Initials(g.Name);
                row.IconColor = color;
                row.Name = g.Name;
                row.Sub = Lang.T("Runs: " + Big(g.Runs) + " · Won: " + Big(g.Victories));
                row.Value = Clock(g.BestTime);
                row.ValueSub = Lang.T("Level " + Big(g.BestLevel));
                rows.Rows.Add(row);
            }
            return rows;
        }

        // room under the last block
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

        // installs (or updates) the AZS Stats built into the launcher
        void InstallStatsAddon()
        {
            if (AddonsLocked()) return;
            string dir = AddonsDir();
            bool had = Stats.AddonInstalled(dir);
            try
            {
                Stats.InstallAddon(dir, SyncSettings().SavedDir);
                SetStatus(had ? "AZS Stats updated." : "AZS Stats installed. It records your runs from now on.");
            }
            catch (Exception e)
            {
                Msg.Show(this, "Could not install AZS Stats:\n\n" + e.Message, "Stats",
                         MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (current == "Stats") BuildPanel("Stats");
        }
    }
}
