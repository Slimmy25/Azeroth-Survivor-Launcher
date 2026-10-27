// The Gear Planner (Guide > Theorycrafting): pick a class, put gear and jewelry in the slots of a character sheet laid
// out like the game's own (C), and see what they add: the sheet's stats and the Run Stats panel beside it.
// The numbers are the bonuses added together (Items.cs); a piece whose bonuses are not known yet adds nothing.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        public const string GearPlanner = "Gear Planner";
        bool gPlanner;                                        // the Gear Planner is open
        string plannerClass = "Priest";
        readonly Dictionary<string, object> plannerSlots = new Dictionary<string, object>();   // slot -> GearPiece or Jewel

        void GuideGearPlanner()
        {
            GuideHeader(GearPlanner, Data.Guide);
            int top = GuideTop, w = PW - 40;
            // the classes, one row
            int n = Items.Classes.Length, gap = 4;
            int bw = (int)(((w - gap * (n - 1)) / (float)n) / Theme.Zoom);
            for (int i = 0; i < n; i++)
            {
                string cls = Items.Classes[i];
                var b = new WowButton(cls.ToUpperInvariant(), delegate
                {
                    if (plannerClass == cls) return;
                    plannerClass = cls;
                    foreach (string s in new List<string>(plannerSlots.Keys))
                        if (plannerSlots[s] is GearPiece) plannerSlots.Remove(s);   // the gear is the class's own; jewelry stays
                    BuildPanel("Guide");
                }, bw, 24, 7);
                b.Selected = cls == plannerClass;
                Put(b, 20 + i * (b.Width + gap), top);
            }
            top += (int)(24 * Theme.Zoom) + 8;
            // the sheet keeps its size and scrolls in a small window
            var sp = MakeThinScroll(panel, new Rectangle(20, top, w, PH - 12 - top), Theme.Panel, Theme.Bronze, Theme.GoldDim, Theme.Gold);
            var sheet = new GearSheet(plannerClass, plannerSlots);
            sheet.Bounds = new Rectangle(0, 0, sp.ClientSize.Width - 4, sheet.NeededHeight);
            sp.Controls.Add(sheet);
        }
    }

    public class GearSheet : Control
    {
        static readonly string[] Left = { "Head", "Neck", "Shoulder", "Back", "Chest", "Shirt", "Tabard", "Wrist" };
        static readonly string[] Right = { "Hands", "Waist", "Legs", "Feet", "Finger 1", "Finger 2", "Trinket 1", "Trinket 2" };
        static readonly string[] Bottom = { "Main Hand", "Off Hand", "Ranged" };
        static readonly HashSet<string> Unused = new HashSet<string> { "Back", "Shirt", "Tabard", "Off Hand", "Ranged" };

        readonly string cls;
        readonly Dictionary<string, object> slots;
        readonly Dictionary<string, Rectangle> boxes = new Dictionary<string, Rectangle>();
        readonly Font fTitle = Theme.F(13, FontStyle.Bold), fText = Theme.F(9.5f), fBold = Theme.F(9.5f, FontStyle.Bold),
                      fSmall = Theme.F(8.25f), fHead = Theme.F(11, FontStyle.Bold);   // made with the section's zoom; cached
        readonly ToolTip tip = new ToolTip();
        string hover;

        static readonly Color Page = Theme.Hex("#0e0d14"), Edge = Theme.Hex("#4a3a22"), SlotBg = Theme.Hex("#1b1a24"),
            SlotEdge = Theme.Hex("#45414f"), Gold = Theme.Hex("#f0c050"), Bright = Theme.Hex("#f5e9c8"), Dim = Theme.Hex("#9c8a64");

        readonly int slotSize = Math.Max(26, (int)(44 * Theme.Zoom)), slotGap = Math.Max(4, (int)(7 * Theme.Zoom));

        // the height that shows every slot (the title, eight rows of slots and the weapons)
        public int NeededHeight { get { return 40 + (int)(fTitle.Height + fSmall.Height) + 9 * (slotSize + slotGap) + 14; } }

        public GearSheet(string cls, Dictionary<string, object> slots)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            this.cls = cls;
            this.slots = slots;
            tip.InitialDelay = 150;
            tip.AutoPopDelay = 30000;
        }

        public static Color RarityColor(string r)
        {
            switch (r)
            {
                case "Uncommon": return Theme.Hex("#1eff00");
                case "Rare": return Theme.Hex("#3b8ff0");
                case "Epic": case "PvP": return Theme.Hex("#b35cf5");
                case "Legendary": return Theme.Hex("#ff8000");
                case "Ring": case "Trinket": case "Amulet": return Theme.Hex("#3b8ff0");
            }
            return Color.White;
        }

        static string RarityOf(object o)
        {
            var p = o as GearPiece;
            if (p != null) return p.Rarity;
            var j = o as Jewel;
            return j != null ? j.Kind : "";
        }

        static string NameOf(object o)
        {
            var p = o as GearPiece;
            if (p != null) return p.Name;
            var j = o as Jewel;
            return j != null ? j.Name : "";
        }

        // ---- the totals: every bonus of every piece in a slot, added together
        Dictionary<string, double> Totals(out int health, out int unknown)
        {
            var t = new Dictionary<string, double>();
            health = 0;
            unknown = 0;
            foreach (var kv in slots)
            {
                var p = kv.Value as GearPiece;
                List<ItemBonus> list = null;
                if (p != null)
                {
                    if (!p.Known) { unknown++; continue; }
                    health += p.Health;
                    list = p.Bonuses;
                }
                var j = kv.Value as Jewel;
                if (j != null) list = j.Bonuses;
                if (list == null) continue;
                foreach (var b in list)
                {
                    double v;
                    t.TryGetValue(b.Stat, out v);
                    t[b.Stat] = v + b.Value;
                }
            }
            return t;
        }

        static string Val(Dictionary<string, double> t, string stat, string unit)
        {
            double v;
            if (!t.TryGetValue(stat, out v) || v == 0) return unit == "%" ? "+0%" : "+0";
            return (v > 0 ? "+" : "") + Items.Pct(v) + unit;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            boxes.Clear();
            int runW = Math.Min(Width / 3, 220);
            var sheet = new Rectangle(0, 0, Width - runW - 10, Height - 1);
            var run = new Rectangle(sheet.Right + 10, 0, runW - 1, Math.Min(Height - 1, 300));
            Frame(g, sheet);
            Frame(g, run);
            int health, unknown;
            var t = Totals(out health, out unknown);

            // the title
            string title = Lang.T(cls);
            SizeF ts = g.MeasureString(title, fTitle);
            using (var b = new SolidBrush(Bright)) g.DrawString(title, fTitle, b, sheet.X + (sheet.Width - ts.Width) / 2, sheet.Y + 8);
            string sub = Lang.T("What your gear and jewelry add");
            SizeF ss = g.MeasureString(sub, fSmall);
            using (var b = new SolidBrush(Gold)) g.DrawString(sub, fSmall, b, sheet.X + (sheet.Width - ss.Width) / 2, sheet.Y + 10 + ts.Height);
            int top = sheet.Y + 16 + (int)(ts.Height + ss.Height);

            // the slots: two columns and the weapons under them
            int s = slotSize, gap = slotGap;
            int lx = sheet.X + 12, rx = sheet.Right - 12 - s;
            for (int i = 0; i < Left.Length; i++) Slot(g, Left[i], new Rectangle(lx, top + i * (s + gap), s, s));
            for (int i = 0; i < Right.Length; i++) Slot(g, Right[i], new Rectangle(rx, top + i * (s + gap), s, s));
            int by = top + 8 * (s + gap);
            int bx = sheet.X + (sheet.Width - (3 * s + 2 * gap)) / 2;
            for (int i = 0; i < Bottom.Length; i++) Slot(g, Bottom[i], new Rectangle(bx + i * (s + gap), by, s, s));

            // the middle: what is worn, then the two stat boxes like the game's
            var mid = new Rectangle(lx + s + 12, top, rx - lx - s - 24, by - top - 8);
            int boxH = (int)(6 * (fText.GetHeight(g) + 2)) + 12;
            var listR = new Rectangle(mid.X, mid.Y, mid.Width, Math.Max(0, mid.Height - boxH - 8));
            int y = listR.Y;
            float lh = fSmall.GetHeight(g) + 1;
            if (slots.Count == 0)
            {
                using (var b = new SolidBrush(Dim))
                    g.DrawString(Lang.T("Click a slot to choose a piece. Right-click it to empty it."), fSmall, b,
                                 new RectangleF(listR.X, y, listR.Width, listR.Height));
            }
            else
                foreach (string slot in Order())
                {
                    object o;
                    if (!slots.TryGetValue(slot, out o)) continue;
                    if (y + lh > listR.Bottom) break;
                    var p = o as GearPiece;
                    string line = NameOf(o) + (p != null && !p.Known ? " *" : "");
                    using (var b = new SolidBrush(RarityColor(RarityOf(o))))
                        g.DrawString(line, fSmall, b, new RectangleF(listR.X, y, listR.Width, lh), new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter });
                    y += (int)lh;
                }
            int half = (mid.Width - 6) / 2;
            double hp;
            t.TryGetValue("health%", out hp);
            string healthText = "+" + health + (hp != 0 ? " +" + Items.Pct(hp) + "%" : "");
            StatBox(g, new Rectangle(mid.X, mid.Bottom - boxH, half, boxH), new[]
            {
                new[] { "Health", healthText }, new[] { "Damage", Val(t, "damage", "%") }, new[] { "Haste", Val(t, "cooldown", "%") },
                new[] { "Crit", Val(t, "crit", "%") }, new[] { "Crit Dmg", Val(t, "critdmg", "%") }, new[] { "Armor", Val(t, "armor", "%") },
            });
            StatBox(g, new Rectangle(mid.X + half + 6, mid.Bottom - boxH, mid.Width - half - 6, boxH), new[]
            {
                new[] { "Area", Val(t, "area", "%") }, new[] { "Targets", Val(t, "targets", "") }, new[] { "Echo", Val(t, "echo", "%") },
                new[] { "Speed", Val(t, "speed", "%") }, new[] { "Regen", Val(t, "regen", "") }, new[] { "Pickup", Val(t, "pickup", "%") },
            });

            // Run Stats
            string rt = Lang.T("Run Stats");
            SizeF rs = g.MeasureString(rt, fHead);
            using (var b = new SolidBrush(Gold)) g.DrawString(rt, fHead, b, run.X + (run.Width - rs.Width) / 2, run.Y + 8);
            var rows = new[]
            {
                new[] { "Lifesteal", Val(t, "lifesteal", "%") }, new[] { "Kill Heal", Val(t, "killheal", "") },
                new[] { "Thorns", Val(t, "thorns", "%") }, new[] { "Lives", Val(t, "lives", "") },
                new[] { "Experience", Val(t, "xp", "%") }, new[] { "Gold", Val(t, "gold", "%") }, new[] { "Luck", Val(t, "luck", "%") },
                new[] { "Buff Power", Val(t, "buff", "%") }, new[] { "DoT Haste", Val(t, "dothaste", "%") },
            };
            Rows(g, new Rectangle(run.X + 12, run.Y + 14 + (int)rs.Height, run.Width - 24, run.Height), rows);

            // the notes under Run Stats
            var note = new RectangleF(run.X, run.Bottom + 10, run.Width, Height - run.Bottom - 10);
            string txt = Lang.T("The bonuses of everything chosen, added together. A piece marked * has bonuses not known yet: it adds nothing here.");
            if (unknown > 0) txt = Lang.T("Pieces with bonuses not known yet") + ": " + unknown + ".\n\n" + txt;
            using (var b = new SolidBrush(Dim)) g.DrawString(txt, fSmall, b, note);
        }

        IEnumerable<string> Order()
        {
            foreach (string s in Left) yield return s;
            foreach (string s in Right) yield return s;
            foreach (string s in Bottom) yield return s;
        }

        void Frame(Graphics g, Rectangle r)
        {
            using (var path = Armory.Round(r, 8))
            {
                using (var b = new SolidBrush(Page)) g.FillPath(b, path);
                using (var p = new Pen(Edge, 2f)) g.DrawPath(p, path);
            }
        }

        void StatBox(Graphics g, Rectangle r, string[][] rows)
        {
            using (var b = new SolidBrush(Color.FromArgb(18, 17, 24))) g.FillRectangle(b, r);
            using (var p = new Pen(SlotEdge)) g.DrawRectangle(p, r);
            Rows(g, new Rectangle(r.X + 8, r.Y + 6, r.Width - 16, r.Height - 12), rows);
        }

        void Rows(Graphics g, Rectangle r, string[][] rows)
        {
            float lh = fText.GetHeight(g) + 2, y = r.Y;
            foreach (var row in rows)
            {
                string label = Lang.T(row[0]) + ":";
                using (var b = new SolidBrush(Gold)) g.DrawString(label, fText, b, r.X, y);
                SizeF vs = g.MeasureString(row[1], fText);
                using (var b = new SolidBrush(Bright)) g.DrawString(row[1], fText, b, r.Right - vs.Width, y);
                y += lh;
            }
        }

        void Slot(Graphics g, string slot, Rectangle r)
        {
            boxes[slot] = r;
            object o;
            slots.TryGetValue(slot, out o);
            bool unused = Unused.Contains(slot);
            using (var b = new SolidBrush(unused ? Color.FromArgb(14, 13, 18) : (hover == slot ? Theme.Hex("#252331") : SlotBg))) g.FillRectangle(b, r);
            if (o != null)
            {
                var j = o as Jewel;
                Image img = j != null ? SpellIcons.Icon(j.Icon.ToLowerInvariant(), this) : null;
                if (img != null) g.DrawImage(img, Rectangle.Inflate(r, -2, -2));
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(70, RarityColor(RarityOf(o))))) g.FillRectangle(b, Rectangle.Inflate(r, -3, -3));
                    string ab = slot.Substring(0, Math.Min(2, slot.Length));
                    SizeF a = g.MeasureString(ab, fBold);
                    using (var b = new SolidBrush(Bright)) g.DrawString(ab, fBold, b, r.X + (r.Width - a.Width) / 2, r.Y + (r.Height - a.Height) / 2);
                }
                using (var p = new Pen(RarityColor(RarityOf(o)), 2f)) g.DrawRectangle(p, r);
            }
            else
            {
                using (var p = new Pen(unused ? Color.FromArgb(40, 38, 48) : SlotEdge)) g.DrawRectangle(p, r);
                if (!unused)
                {
                    string ab = Lang.T(slot);
                    using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var b = new SolidBrush(Color.FromArgb(110, 156, 138, 100)))
                        g.DrawString(ab, fSmall, b, r, f);
                }
            }
        }

        string SlotAt(Point p)
        {
            foreach (var kv in boxes) if (kv.Value.Contains(p) && !Unused.Contains(kv.Key)) return kv.Key;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            string s = SlotAt(e.Location);
            if (s != hover)
            {
                hover = s;
                Cursor = s != null ? Cursors.Hand : Cursors.Default;
                object o = null;
                if (s != null) slots.TryGetValue(s, out o);
                string text = o is GearPiece ? Items.Card((GearPiece)o) : o is Jewel ? Items.Card((Jewel)o) : s != null ? Lang.T(s) : null;
                tip.SetToolTip(this, text);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = null; tip.SetToolTip(this, null); Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            string s = SlotAt(e.Location);
            if (s == null) return;
            if (e.Button == MouseButtons.Right) { slots.Remove(s); Invalidate(); return; }
            Menu(s).Show(this, new Point(boxes[s].Right + 2, boxes[s].Top));
        }

        ContextMenuStrip Menu(string slot)
        {
            var m = new ContextMenuStrip();
            m.ShowImageMargin = false;
            m.BackColor = Theme.Hex("#16151e");
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            var empty = new ToolStripMenuItem(Lang.T("Empty"));
            empty.ForeColor = Dim;
            empty.Click += delegate { slots.Remove(slot); Invalidate(); };
            m.Items.Add(empty);
            if (slot == "Neck" || slot.StartsWith("Finger") || slot.StartsWith("Trinket"))
            {
                string kind = slot == "Neck" ? "Amulet" : slot.StartsWith("Finger") ? "Ring" : "Trinket";
                string other = slot.EndsWith("1") ? slot.Replace("1", "2") : slot.Replace("2", "1");
                foreach (var j in Items.JewelryOf(kind))
                {
                    var jw = j;
                    object o;
                    bool taken = slot != "Neck" && slots.TryGetValue(other, out o) && o == (object)jw;   // the same ring twice: no
                    var it = new ToolStripMenuItem(jw.Name + (taken ? "  (" + Lang.T("worn") + ")" : ""));
                    it.ForeColor = RarityColor(kind);
                    it.Enabled = !taken;
                    it.ToolTipText = Items.Card(jw);
                    it.Click += delegate { slots[slot] = jw; Invalidate(); };
                    m.Items.Add(it);
                }
            }
            else
                foreach (var p in Items.GearOf(cls, slot))
                {
                    var piece = p;
                    var it = new ToolStripMenuItem(piece.Name + "  ·  " + Lang.T(piece.Rarity) + (piece.Known ? "" : "  *"));
                    it.ForeColor = RarityColor(piece.Rarity);
                    it.ToolTipText = Items.Card(piece);
                    it.Click += delegate { slots[slot] = piece; Invalidate(); };
                    m.Items.Add(it);
                }
            m.ShowItemToolTips = true;
            return m;
        }

        class DarkColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Theme.Hex("#2a2636"); } }
            public override Color MenuItemBorder { get { return Theme.Hex("#4a3a22"); } }
            public override Color MenuBorder { get { return Theme.Hex("#4a3a22"); } }
            public override Color ToolStripDropDownBackground { get { return Theme.Hex("#16151e"); } }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
