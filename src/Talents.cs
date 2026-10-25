// The talent trees of every class, as the game has them (assets/talents.txt, read from the game at wow.aethus.pl by
// tools/game_talents.py), and the pieces the New Player Guide's class pages draw them with:
//   * TalentTreeView: one spec's tree, its talents' icons on the game's grid (7 rows, 4 columns) with their ranks,
//     the 10, 20 and 30 point milestones marked; pointing at a talent shows its text and the spells it changes
//   * SpellRow: one spell of the class, its icon, the guide's line about it, and the talents that change it
// The icons come from the same place as the STATS page's (Wowhead's image host, cached; SpellIcons.cs).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public class Talent
    {
        public string Key = "", Name = "", Icon = "", Text = "", Spec = "", Class = "";
        public int Row, Col, Ranks;
        public List<string> Changes = new List<string>();     // the class's spells its text names

        // the text as the game shows it, with the number that grows with each rank as "X"
        public string Shown { get { return Text.Replace("{v}%", "X%").Replace("{v2}%", "Y%").Replace("{v}", "X").Replace("{v2}", "Y"); } }
        public int Points { get { return Row * 5; } }          // points in the tree it needs (the milestones: 10, 20, 30)
        public string Id { get { return Spec + "/" + Key; } }  // unique within its class
        public bool Milestone { get { return Text.StartsWith("Milestone") || Text.StartsWith("Capstone"); } }
    }

    public class TalentTree
    {
        public string Class = "", Spec = "";
        public List<Talent> Talents = new List<Talent>();
    }

    public static class Talents
    {
        static List<TalentTree> trees;
        static Dictionary<string, string> abilities;           // ability key -> name, as the game names them

        static void Load()
        {
            if (trees != null) return;
            trees = new List<TalentTree>();
            abilities = new Dictionary<string, string>(StringComparer.Ordinal);
            TalentTree cur = null;
            bool on = false, abil = false;
            foreach (string raw in Util.ReadResource("AS.talents.txt").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line == "===") { on = true; continue; }
                if (!on || line.Length == 0) continue;
                if (line.StartsWith("@tree "))
                {
                    string[] h = line.Substring(6).Split('|');
                    cur = new TalentTree();
                    cur.Class = h[0].Trim();
                    cur.Spec = h.Length > 1 ? h[1].Trim() : "";
                    trees.Add(cur);
                    abil = false;
                    continue;
                }
                if (line == "@abilities") { abil = true; continue; }
                string[] f = line.Split(new[] { " | " }, StringSplitOptions.None);
                if (abil)
                {
                    if (f.Length >= 2) abilities[f[0].Trim()] = f[1].Trim();
                    continue;
                }
                if (cur == null || f.Length < 7) continue;
                var t = new Talent();
                t.Key = f[0].Trim(); t.Name = f[1].Trim(); t.Icon = f[2].Trim().ToLowerInvariant();
                int.TryParse(f[3], out t.Row); int.TryParse(f[4], out t.Col); int.TryParse(f[5], out t.Ranks);
                t.Text = string.Join(" | ", f, 6, f.Length - 6).Trim().Replace("\\n", "\n");
                t.Spec = cur.Spec; t.Class = cur.Class;
                cur.Talents.Add(t);
            }
            // which spells each talent changes: the abilities its text names
            var names = new List<string>(abilities.Values);
            names.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
            foreach (var tr in trees)
                foreach (var t in tr.Talents)
                {
                    string text = t.Text;
                    foreach (string n in names)
                    {
                        if (n.Length < 4) continue;
                        if (!Regex.IsMatch(text, @"(?<![A-Za-z])" + Regex.Escape(n) + @"(?:'?s)?(?![A-Za-z])")) continue;
                        if (!t.Changes.Contains(n)) t.Changes.Add(n);
                        text = Regex.Replace(text, Regex.Escape(n), "");   // "Shadow Bolt" is not also "Shadow ..."
                    }
                }
        }

        public static List<TalentTree> Of(string className)
        {
            Load();
            return trees.FindAll(delegate(TalentTree t) { return string.Equals(t.Class, className, StringComparison.OrdinalIgnoreCase); });
        }

        public static IEnumerable<Talent> All()
        {
            Load();
            foreach (var tr in trees) foreach (var t in tr.Talents) yield return t;
        }

        // the talents of a class that change a spell
        public static List<Talent> Changing(string className, string spell)
        {
            var list = new List<Talent>();
            foreach (var tr in Of(className))
                foreach (var t in tr.Talents)
                    foreach (string c in t.Changes)
                        if (string.Equals(c, spell, StringComparison.OrdinalIgnoreCase)) { list.Add(t); break; }
            return list;
        }

        public static string HoverText(Talent t)
        {
            string head = t.Spec + "  ·  " + (t.Ranks > 1 ? Lang.T("Ranks") + " " + t.Ranks : "1 " + Lang.T("rank")) +
                          (t.Row > 0 ? "  ·  " + Lang.T("needs") + " " + t.Points + " " + Lang.T("points") : "");
            string s = head + "\n\n" + t.Shown;
            if (t.Changes.Count > 0) s += "\n\n" + Lang.T("Changes") + ": " + string.Join(", ", t.Changes.ToArray());
            if (t.Text.Contains("{v}")) s += "\n" + Lang.T("X grows with every rank.");
            return s;
        }
    }

    // a test build in the Guide's talent trees: the points put in each talent of one class. Up to 51 points, and no
    // more than a talent's ranks; the game's other rules (a row opens after 5 points in its tree) are not enforced,
    // so any build can be tried out
    public class TalentBuild
    {
        public const int MaxPoints = 51;
        readonly Dictionary<string, int> points = new Dictionary<string, int>(StringComparer.Ordinal);
        public Action Changed;

        public int Get(Talent t) { int n; return points.TryGetValue(t.Id, out n) ? n : 0; }

        public int Spent { get { int s = 0; foreach (int v in points.Values) s += v; return s; } }

        public int SpentIn(TalentTree tree) { int s = 0; foreach (var t in tree.Talents) s += Get(t); return s; }

        public bool Add(Talent t)
        {
            if (Get(t) >= t.Ranks || Spent >= MaxPoints) return false;
            points[t.Id] = Get(t) + 1;
            if (Changed != null) Changed();
            return true;
        }

        public bool Remove(Talent t)
        {
            int n = Get(t);
            if (n <= 0) return false;
            if (n == 1) points.Remove(t.Id); else points[t.Id] = n - 1;
            if (Changed != null) Changed();
            return true;
        }

        public void Reset() { points.Clear(); if (Changed != null) Changed(); }

        // "Arms/deep_wounds=1;Fury/cruelty=5": kept in the launcher's settings, one build per class
        public string Save()
        {
            var parts = new List<string>();
            foreach (var kv in points) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(";", parts.ToArray());
        }

        public static TalentBuild Parse(string text, string className)
        {
            var b = new TalentBuild();
            var byId = new Dictionary<string, Talent>(StringComparer.Ordinal);
            foreach (var tr in Talents.Of(className)) foreach (var t in tr.Talents) byId[t.Id] = t;
            foreach (string part in (text ?? "").Split(';'))
            {
                int eq = part.LastIndexOf('='), n;
                Talent t;
                if (eq <= 0 || !int.TryParse(part.Substring(eq + 1), out n) || !byId.TryGetValue(part.Substring(0, eq), out t)) continue;
                n = Math.Min(n, Math.Min(t.Ranks, MaxPoints - b.Spent));
                if (n > 0) b.points[t.Id] = n;
            }
            return b;
        }
    }

    // one spec's tree, like the game's talent window: the spec's name over a grid of the talents' icons
    public class TalentTreeView : Control
    {
        readonly TalentTree tree;
        readonly List<KeyValuePair<Rectangle, Talent>> cells = new List<KeyValuePair<Rectangle, Talent>>();
        Talent over;
        const int Cols = 4, Rows = 7;
        public TalentBuild Build;                            // set: a test build, click to add a point, right-click to take one back

        public TalentTreeView(TalentTree tree, int width)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            this.tree = tree;
            int cell = Math.Max(30, (width - 16) / Cols);
            Size = new Size(width, 30 + Rows * cell + 8);
        }

        int Cell { get { return Math.Max(30, (Width - 16) / Cols); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            var box = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (var path = Armory.Round(box, 6))
            {
                using (var b = new LinearGradientBrush(box, Theme.Hex("#1c1610"), Theme.Hex("#0e0c12"), 90f)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Hex("#5a4426"), 1)) g.DrawPath(p, path);
            }
            using (var b = new SolidBrush(Theme.Gold))
                g.DrawString(tree.Spec, Theme.F(11, FontStyle.Bold), b, 10, 6);
            if (Build != null)
            {
                string pts = Build.SpentIn(tree).ToString();
                Font pf = Theme.F(11, FontStyle.Bold);
                SizeF ps = g.MeasureString(pts, pf);
                using (var b = new SolidBrush(Build.SpentIn(tree) > 0 ? Theme.Hex("#7ee05a") : Theme.GoldDim))
                    g.DrawString(pts, pf, b, Width - 10 - ps.Width, 6);
            }
            int cell = Cell, ic = Math.Min(36, cell - 10), x0 = 8 + (Width - 16 - cell * Cols) / 2, y0 = 30;
            cells.Clear();
            // the line that joins the milestones, as in the game's window
            var ms = tree.Talents.FindAll(delegate(Talent t) { return t.Milestone; });
            ms.Sort(delegate(Talent a, Talent b) { return a.Row.CompareTo(b.Row); });
            using (var p = new Pen(Color.FromArgb(120, Theme.GoldDim), 2))
                for (int i = 1; i < ms.Count; i++)
                    g.DrawLine(p, x0 + ms[i - 1].Col * cell + cell / 2, y0 + ms[i - 1].Row * cell + cell / 2,
                               x0 + ms[i].Col * cell + cell / 2, y0 + ms[i].Row * cell + cell / 2);
            Font rf = Theme.F(7.5f, FontStyle.Bold);
            foreach (var t in tree.Talents)
            {
                var r = new Rectangle(x0 + t.Col * cell + (cell - ic) / 2, y0 + t.Row * cell + (cell - ic) / 2 - 3, ic, ic);
                cells.Add(new KeyValuePair<Rectangle, Talent>(r, t));
                Image img = SpellIcons.Icon(t.Icon, this);
                if (img != null) g.DrawImage(img, r);
                else using (var b = new SolidBrush(Theme.Hex("#2a2218"))) g.FillRectangle(b, r);
                int have = Build != null ? Build.Get(t) : 0;
                if (Build != null && have == 0)                // not taken: dimmed, as in the game
                    using (var b = new SolidBrush(Color.FromArgb(150, 8, 6, 10))) g.FillRectangle(b, r);
                bool milestone = t.Milestone;
                using (var p = new Pen(t == over ? Theme.Hex("#fff2b0") : milestone ? Theme.Hex("#b27cff") : Theme.Hex("#8a7040"), t == over ? 2 : 1.5f))
                    g.DrawRectangle(p, r);
                string rk = have + "/" + t.Ranks;
                SizeF rs = g.MeasureString(rk, rf);
                var rb = new RectangleF(r.Right - rs.Width + 4, r.Bottom - rs.Height + 4, rs.Width, rs.Height - 1);
                using (var b = new SolidBrush(Color.FromArgb(220, 10, 8, 6))) g.FillRectangle(b, rb);
                Color rc = have >= t.Ranks ? Theme.Hex("#ffd100") : have > 0 ? Theme.Hex("#7ee05a") : Theme.Text;
                using (var b = new SolidBrush(rc)) g.DrawString(rk, rf, b, rb.X, rb.Y - 1);
            }
        }

        Talent At(Point p)
        {
            foreach (var c in cells) if (Rectangle.Inflate(c.Key, 3, 3).Contains(p)) return c.Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Talent t = At(e.Location);
            if (t != over) { over = t; Invalidate(); }
            Cursor = t != null ? (Build != null ? Cursors.Hand : Cursors.Help) : Cursors.Default;
            if (t != null) ShowTip(t);
            else GuideHover.Hide();
        }

        void ShowTip(Talent t)
        {
            string body = Talents.HoverText(t);
            if (Build != null)
                body = Lang.T("Rank") + " " + Build.Get(t) + "/" + t.Ranks + "\n" + body + "\n\n" +
                       (Build.Get(t) < t.Ranks ? (Build.Spent < TalentBuild.MaxPoints ? Lang.T("Click to add a point.") : Lang.T("All 51 points are spent.")) + " " : "") +
                       (Build.Get(t) > 0 ? Lang.T("Right-click to take one back.") : "");
            GuideHover.Show(this, t.Name, body.TrimEnd(), SpellIcons.Icon(t.Icon, null));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (Build == null) return;
            Talent t = At(e.Location);
            if (t == null) return;
            if (e.Button == MouseButtons.Left) Build.Add(t);
            else if (e.Button == MouseButtons.Right) Build.Remove(t);
            ShowTip(t);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (over != null) { over = null; Invalidate(); }
            GuideHover.Hide();
        }
    }

    // one of a class's spells: its icon, its name, the guide's line about it and the talents that change it
    public class SpellRow : Control
    {
        readonly string name, line, className;
        readonly List<Talent> changers;
        readonly GuideText text;
        readonly List<KeyValuePair<Rectangle, Talent>> chips = new List<KeyValuePair<Rectangle, Talent>>();
        Talent over;
        const int Ic = 34;

        public SpellRow(string name, string line, string className, int width, int topic, int page)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            this.name = name;
            this.line = line;
            this.className = className;
            changers = Talents.Changing(className, Regex.Replace(name, @"\s*\(.*\)$", ""));
            BackColor = Theme.Hex("#15121c");
            text = new GuideText(line, 9.5f, Theme.Text, BackColor, width - Ic - 26, topic, page);
            text.Location = new Point(Ic + 18, 24);
            Controls.Add(text);
            int h = text.Bottom + 6;
            if (changers.Count > 0) h += 26;
            Size = new Size(width, Math.Max(h, Ic + 14));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            var box = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (var path = Armory.Round(box, 6))
            {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Hex("#3a2e1e"), 1)) g.DrawPath(p, path);
            }
            var ir = new Rectangle(8, 8, Ic, Ic);
            Image img = SpellIcons.Spell(Regex.Replace(name, @"\s*\(.*\)$", ""), this);
            if (img != null) g.DrawImage(img, ir);
            else using (var b = new SolidBrush(Theme.Hex("#2a2218"))) g.FillRectangle(b, ir);
            using (var p = new Pen(Theme.Hex("#8a7040"), 1)) g.DrawRectangle(p, ir);
            using (var b = new SolidBrush(Theme.Gold)) g.DrawString(name, Theme.F(10.5f, FontStyle.Bold), b, Ic + 16, 5);
            chips.Clear();
            if (changers.Count == 0) return;
            float x = Ic + 18, y = Height - 24;
            Font lf = Theme.F(8, FontStyle.Italic), cf = Theme.F(8, FontStyle.Bold);
            string label = Lang.T("Talents that change it:");
            using (var b = new SolidBrush(Theme.GoldDim)) g.DrawString(label, lf, b, x, y + 3);
            x += g.MeasureString(label, lf).Width + 4;
            foreach (var t in changers)
            {
                Image ti = SpellIcons.Icon(t.Icon, this);
                var r = new Rectangle((int)x, (int)y, 18, 18);
                if (x + 18 > Width - 6) break;
                if (ti != null) g.DrawImage(ti, r);
                else using (var b = new SolidBrush(Theme.Hex("#2a2218"))) g.FillRectangle(b, r);
                using (var p = new Pen(t == over ? Theme.Hex("#fff2b0") : Theme.Hex("#8a7040"), 1)) g.DrawRectangle(p, r);
                chips.Add(new KeyValuePair<Rectangle, Talent>(r, t));
                x += 22;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Talent t = null;
            foreach (var c in chips) if (Rectangle.Inflate(c.Key, 2, 2).Contains(e.Location)) t = c.Value;
            if (t != over) { over = t; Invalidate(); }
            Cursor = t != null ? Cursors.Help : Cursors.Default;
            if (t != null) GuideHover.Show(this, t.Name, Talents.HoverText(t), SpellIcons.Icon(t.Icon, null));
            else if (new Rectangle(8, 8, Ic, Ic).Contains(e.Location) || e.Y < 24)
                GuideHover.Show(this, name, SpellHover(), SpellIcons.Spell(Regex.Replace(name, @"\s*\(.*\)$", ""), null));
            else GuideHover.Hide();
        }

        string SpellHover()
        {
            string s = line;
            string game = GuideLinks.GameTip(Regex.Replace(name, @"\s*\(.*\)$", ""));
            if (game != null) s = game;
            if (changers.Count > 0)
            {
                var n = new List<string>();
                foreach (var t in changers) n.Add(t.Name + " (" + t.Spec + ")");
                s += "\n\n" + Lang.T("Talents that change it:") + " " + string.Join(", ", n.ToArray());
            }
            return s;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (over != null) { over = null; Invalidate(); }
            GuideHover.Hide();
        }
    }
}
