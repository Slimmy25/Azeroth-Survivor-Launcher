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
        string plannerClass;                                  // the class both tools build for (asked first; not saved)
        readonly Dictionary<string, object> plannerSlots = new Dictionary<string, object>();   // slot -> GearPiece or Jewel

        void GuideGearPlanner()
        {
            GuideHeader(GearPlanner, Data.Guide);
            if (plannerClass == null)
            {
                ClassPrompt(delegate(string cls) { PlannerClass(cls); BuildPanel("Guide"); });
                return;
            }
            int top = GuideTop, w = PW - 40;
            // the same class's talents, in the Talent Tree Builder
            var tal = Put(new WowButton("TALENTS ›", delegate
            {
                gVideo = null; gPlanner = false; gBuilder = true; guideMain = false; guideGroupPage = -1; gTalents = plannerClass;
                BuildPanel("Guide");
            },
                                        130, 26, 9), PW - 20 - (int)(130 * Theme.Zoom), top);
            top = BottomOf(tal) + 6;
            // the sheet keeps its size and scrolls in a small window
            var sp = MakeThinScroll(panel, new Rectangle(20, top, w, PH - 12 - top), Theme.Panel, Theme.Bronze, Theme.GoldDim, Theme.Gold);
            var sheet = new GearSheet(plannerClass, plannerSlots, GearIcons());
            sheet.Bounds = new Rectangle(0, 0, sp.ClientSize.Width - 4, sheet.NeededHeight);
            sp.Controls.Add(sheet);
        }

        // a new class: its gear goes (each class has its own), the jewelry stays
        void PlannerClass(string cls)
        {
            if (plannerClass != cls)
                foreach (string s in new List<string>(plannerSlots.Keys))
                    if (plannerSlots[s] is GearPiece) plannerSlots.Remove(s);
            plannerClass = cls;
        }

        // the icons of the gear AZS Stats has seen worn (the game's own icons, by the piece's name)
        Dictionary<string, string> GearIcons()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                RunStats st = AddonStats();
                if (st != null)
                    foreach (var a in st.Armory)
                        foreach (var it in a.Gear)
                        {
                            string ic = it.Icon ?? "";
                            int cut = ic.LastIndexOfAny(new[] { '\\', '/' });
                            if (cut >= 0) ic = ic.Substring(cut + 1);
                            if (ic.Length > 0 && it.Name.Length > 0 && !d.ContainsKey(it.Name)) d[it.Name] = ic.ToLowerInvariant();
                        }
            }
            catch (Exception) { }
            return d;
        }

        // the gear in the planner, added up: the hover texts of spells and talents show their numbers with it
        public static Dictionary<string, double> PlannerTotals;
    }

    public class GearSheet : Control
    {
        static readonly string[] Left = { "Head", "Neck", "Shoulder", "Back", "Chest", "Shirt", "Tabard", "Wrist" };
        static readonly string[] Right = { "Hands", "Waist", "Legs", "Feet", "Finger 1", "Finger 2", "Trinket 1", "Trinket 2" };
        static readonly string[] Bottom = { "Main Hand", "Off Hand", "Ranged" };
        static readonly HashSet<string> Unused = new HashSet<string> { "Back", "Shirt", "Tabard", "Off Hand", "Ranged" };

        readonly string cls;
        readonly Dictionary<string, object> slots;
        readonly Dictionary<string, string> gearIcons;       // piece name -> icon (the ones AZS Stats has seen)
        readonly Dictionary<string, Rectangle> boxes = new Dictionary<string, Rectangle>();
        readonly List<KeyValuePair<Rectangle, object>> rows = new List<KeyValuePair<Rectangle, object>>();   // the selector's lines
        readonly Font fTitle = Theme.F(13, FontStyle.Bold), fText = Theme.F(9.5f), fBold = Theme.F(9.5f, FontStyle.Bold),
                      fSmall = Theme.F(8.25f), fHead = Theme.F(11, FontStyle.Bold);   // made with the section's zoom; cached
        readonly ToolTip tip = new ToolTip();
        readonly int slotSize = Math.Max(26, (int)(44 * Theme.Zoom)), slotGap = Math.Max(4, (int)(7 * Theme.Zoom));
        static string chosenSlot = "Head";                   // the slot the selector shows (kept while the launcher runs)
        string hover;
        int hoverRow = -1;
        static readonly object EmptyRow = new object();

        static readonly Color Page = Theme.Hex("#0e0d14"), Edge = Theme.Hex("#4a3a22"), SlotBg = Theme.Hex("#1b1a24"),
            SlotEdge = Theme.Hex("#45414f"), Gold = Theme.Hex("#f0c050"), Bright = Theme.Hex("#f5e9c8"), Dim = Theme.Hex("#9c8a64");

        // the height that shows every slot (the title, eight rows of slots and the weapons)
        public int NeededHeight { get { return 40 + (int)(fTitle.Height + fSmall.Height) + 9 * (slotSize + slotGap) + 14; } }

        public GearSheet(string cls, Dictionary<string, object> slots, Dictionary<string, string> gearIcons)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            this.cls = cls;
            this.slots = slots;
            this.gearIcons = gearIcons ?? new Dictionary<string, string>();
            tip.InitialDelay = 150;
            tip.AutoPopDelay = 30000;
            if (Unused.Contains(chosenSlot)) chosenSlot = "Head";
            Publish();
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

        Image IconOf(object o)
        {
            var j = o as Jewel;
            if (j != null) return SpellIcons.Icon(j.Icon.ToLowerInvariant(), this);
            var p = o as GearPiece;
            string ic;
            if (p != null && gearIcons.TryGetValue(p.Name, out ic)) return SpellIcons.Icon(ic, this);
            return null;
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

        // the totals for the hover texts of spells and talents (GearEffect)
        void Publish()
        {
            int h, u;
            var t = Totals(out h, out u);
            LauncherApp.PlannerTotals = t.Count > 0 ? t : null;
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
            int pickW = Math.Min(Width * 2 / 5, 280);
            var sheet = new Rectangle(0, 0, Width - pickW - 10, Height - 1);
            var pick = new Rectangle(sheet.Right + 10, 0, pickW - 1, Height - 1);
            Frame(g, sheet);
            Frame(g, pick);
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

            // the middle: the two stat boxes like the game's, then the note
            var mid = new Rectangle(lx + s + 12, top, rx - lx - s - 24, by - top - 8);
            int boxH = (int)(6 * (fText.GetHeight(g) + 2)) + 12;
            int half = (mid.Width - 6) / 2;
            double hp;
            t.TryGetValue("health%", out hp);
            string healthText = "+" + health + (hp != 0 ? " +" + Items.Pct(hp) + "%" : "");
            StatBox(g, new Rectangle(mid.X, mid.Y, half, boxH), new[]
            {
                new[] { "Health", healthText }, new[] { "Damage", Val(t, "damage", "%") }, new[] { "Haste", Val(t, "cooldown", "%") },
                new[] { "Crit", Val(t, "crit", "%") }, new[] { "Crit Dmg", Val(t, "critdmg", "%") }, new[] { "Armor", Val(t, "armor", "%") },
            });
            StatBox(g, new Rectangle(mid.X + half + 6, mid.Y, mid.Width - half - 6, boxH), new[]
            {
                new[] { "Area", Val(t, "area", "%") }, new[] { "Targets", Val(t, "targets", "") }, new[] { "Echo", Val(t, "echo", "%") },
                new[] { "Speed", Val(t, "speed", "%") }, new[] { "Regen", Val(t, "regen", "") }, new[] { "Pickup", Val(t, "pickup", "%") },
            });
            string txt = Lang.T("The bonuses of everything chosen, added together. A piece marked * has bonuses not known yet: it adds nothing here.");
            if (unknown > 0) txt = Lang.T("Pieces with bonuses not known yet") + ": " + unknown + ". " + txt;
            using (var b = new SolidBrush(Dim)) g.DrawString(txt, fSmall, b, new RectangleF(mid.X, mid.Y + boxH + 8, mid.Width, mid.Bottom - mid.Y - boxH - 8));

            // the selector: what the chosen slot can hold, each with its picture
            Selector(g, pick);
        }

        List<object> Choices(string slot)
        {
            var l = new List<object> { EmptyRow };
            if (slot == "Neck") foreach (var j in Items.JewelryOf("Amulet")) l.Add(j);
            else if (slot.StartsWith("Finger")) foreach (var j in Items.JewelryOf("Ring")) l.Add(j);
            else if (slot.StartsWith("Trinket")) foreach (var j in Items.JewelryOf("Trinket")) l.Add(j);
            else foreach (var p in Items.GearOf(cls, slot)) l.Add(p);
            return l;
        }

        void Selector(Graphics g, Rectangle r)
        {
            rows.Clear();
            string head = Lang.T(chosenSlot);
            using (var b = new SolidBrush(Gold)) g.DrawString(head, fHead, b, r.X + 10, r.Y + 8);
            float y = r.Y + 12 + fHead.GetHeight(g);
            float lh = Math.Max(fText.GetHeight(g) + 6, 20);
            int ic = (int)(lh - 4);                          // the picture: as tall as the line
            object worn;
            slots.TryGetValue(chosenSlot, out worn);
            string other = chosenSlot.EndsWith("1") ? chosenSlot.Replace("1", "2") : chosenSlot.EndsWith("2") ? chosenSlot.Replace("2", "1") : null;
            object inOther = null;
            if (other != null) slots.TryGetValue(other, out inOther);
            var list = Choices(chosenSlot);
            using (var sf = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < list.Count; i++)
                {
                    if (y + lh > r.Bottom - 6) break;
                    object o = list[i];
                    var row = new Rectangle(r.X + 6, (int)y, r.Width - 12, (int)lh);
                    bool isWorn = o == worn || (o == EmptyRow && worn == null);
                    bool taken = o != EmptyRow && o == inOther;  // the same ring or trinket twice: no
                    if (isWorn || hoverRow == i)
                        using (var b = new SolidBrush(isWorn ? Color.FromArgb(60, 240, 192, 80) : Theme.Hex("#252331"))) g.FillRectangle(b, row);
                    var icon = new Rectangle(row.X + 2, row.Y + 2, ic, ic);
                    if (o == EmptyRow)
                    {
                        using (var p = new Pen(SlotEdge)) g.DrawRectangle(p, icon);
                        using (var b = new SolidBrush(Dim)) g.DrawString(Lang.T("Empty"), fText, b, new RectangleF(icon.Right + 6, row.Y, row.Width - ic - 10, row.Height), sf);
                    }
                    else
                    {
                        Image img = IconOf(o);
                        if (img != null) Theme.Draw(g, img, icon);
                        else
                        {
                            using (var b = new SolidBrush(Color.FromArgb(70, RarityColor(RarityOf(o))))) g.FillRectangle(b, icon);
                            string ab = chosenSlot.Substring(0, Math.Min(2, chosenSlot.Length));
                            using (var f2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            using (var b = new SolidBrush(Bright)) g.DrawString(ab, fSmall, b, icon, f2);
                        }
                        using (var p = new Pen(RarityColor(RarityOf(o)))) g.DrawRectangle(p, icon);
                        var piece = o as GearPiece;
                        string name = NameOf(o) + (piece != null && !piece.Known ? " *" : "") + (taken ? "  (" + Lang.T("worn") + ")" : "");
                        Color c = taken ? Dim : RarityColor(RarityOf(o));
                        using (var b = new SolidBrush(c)) g.DrawString(name, fText, b, new RectangleF(icon.Right + 6, row.Y, row.Width - ic - 10, row.Height), sf);
                    }
                    if (!taken) rows.Add(new KeyValuePair<Rectangle, object>(row, o));
                    else rows.Add(new KeyValuePair<Rectangle, object>(Rectangle.Empty, o));
                    y += lh + 1;
                }
        }

        void Frame(Graphics g, Rectangle r)
        {
            using (var path = Armory.Round(r, 8))
            {
                using (var b = new SolidBrush(Page)) g.FillPath(b, path);
                using (var p = new Pen(Edge, 2f)) g.DrawPath(p, path);
            }
        }

        void StatBox(Graphics g, Rectangle r, string[][] lines)
        {
            using (var b = new SolidBrush(Color.FromArgb(18, 17, 24))) g.FillRectangle(b, r);
            using (var p = new Pen(SlotEdge)) g.DrawRectangle(p, r);
            float lh = fText.GetHeight(g) + 2, y = r.Y + 6;
            foreach (var row in lines)
            {
                string label = Lang.T(row[0]) + ":";
                using (var b = new SolidBrush(Gold)) g.DrawString(label, fText, b, r.X + 8, y);
                SizeF vs = g.MeasureString(row[1], fText);
                using (var b = new SolidBrush(Bright)) g.DrawString(row[1], fText, b, r.Right - 8 - vs.Width, y);
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
                Image img = IconOf(o);
                if (img != null) Theme.Draw(g, img, Rectangle.Inflate(r, -2, -2));
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
                    using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var b = new SolidBrush(Color.FromArgb(110, 156, 138, 100)))
                        g.DrawString(Lang.T(slot), fSmall, b, r, f);
            }
            if (slot == chosenSlot && !unused)                // the slot the selector shows
                using (var p = new Pen(Gold, 2f)) g.DrawRectangle(p, Rectangle.Inflate(r, 2, 2));
        }

        string SlotAt(Point p)
        {
            foreach (var kv in boxes) if (kv.Value.Contains(p) && !Unused.Contains(kv.Key)) return kv.Key;
            return null;
        }

        int RowAt(Point p)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i].Key.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            string s = SlotAt(e.Location);
            int r = s == null ? RowAt(e.Location) : -1;
            if (s != hover || r != hoverRow)
            {
                hover = s;
                hoverRow = r;
                Cursor = s != null || r >= 0 ? Cursors.Hand : Cursors.Default;
                object o = null;
                if (s != null) slots.TryGetValue(s, out o);
                else if (r >= 0) o = rows[r].Value;
                string text = o is GearPiece ? Items.Card((GearPiece)o) : o is Jewel ? Items.Card((Jewel)o) : s != null ? Lang.T(s) : null;
                tip.SetToolTip(this, text);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = null; hoverRow = -1; tip.SetToolTip(this, null); Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            string s = SlotAt(e.Location);
            if (s != null)
            {
                if (e.Button == MouseButtons.Right) slots.Remove(s);
                else chosenSlot = s;                          // the selector shows what this slot can hold
                Publish();
                Invalidate();
                return;
            }
            int r = RowAt(e.Location);
            if (r < 0 || e.Button != MouseButtons.Left) return;
            object o = rows[r].Value;
            if (o == EmptyRow) slots.Remove(chosenSlot);
            else slots[chosenSlot] = o;
            Publish();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }

    // The Gear Planner's set in the hover texts of spells and talents: their damage, their cooldowns and their reach
    // shown with the set's damage, cooldown and area bonuses (a plain estimate: the game may combine them otherwise).
    // The set is not saved, so this ends when the launcher closes.
    public static class GearEffect
    {
        static readonly System.Text.RegularExpressions.Regex DamageNum = new System.Text.RegularExpressions.Regex(
            @"\b(\d+(?:\.\d+)?)(?=(?: [A-Z][a-z]+)? damage\b)");
        static readonly System.Text.RegularExpressions.Regex SecNum = new System.Text.RegularExpressions.Regex(
            @"(?<=\b(?:every|cooldown of|cooldown:|Cooldown:) )(\d+(?:\.\d+)?)(?= ?sec)");
        static readonly System.Text.RegularExpressions.Regex YdNum = new System.Text.RegularExpressions.Regex(
            @"(?<=\bwithin )(\d+(?:\.\d+)?)(?= ?yd)");

        public static string Apply(string title, string body)
        {
            var t = LauncherApp.PlannerTotals;
            if (t == null || string.IsNullOrEmpty(body)) return body;
            foreach (var p in Items.Gear) if (p.Name == title) return body;     // an item's own card stays as it is
            foreach (var j in Items.Jewelry) if (j.Name == title) return body;
            double dmg, cd, area;
            t.TryGetValue("damage", out dmg);
            t.TryGetValue("cooldown", out cd);
            t.TryGetValue("area", out area);
            if (dmg == 0 && cd == 0 && area == 0) return body;
            bool changed = false;
            string s = body;
            if (dmg != 0)
                s = DamageNum.Replace(s, delegate(System.Text.RegularExpressions.Match m) { changed = true; return Scale(m.Value, 1 + dmg / 100); });
            if (cd != 0)
                s = SecNum.Replace(s, delegate(System.Text.RegularExpressions.Match m) { changed = true; return Scale(m.Value, Math.Max(0.05, 1 - cd / 100)); });
            if (area != 0)
                s = YdNum.Replace(s, delegate(System.Text.RegularExpressions.Match m) { changed = true; return Scale(m.Value, 1 + area / 100); });
            var parts = new List<string>();
            if (dmg != 0) parts.Add("+" + Items.Pct(dmg) + "% " + Lang.T("damage"));
            if (cd != 0) parts.Add("-" + Items.Pct(cd) + "% " + Lang.T("cooldowns"));
            if (area != 0) parts.Add("+" + Items.Pct(area) + "% " + Lang.T("area"));
            return s + "\n\n" + Lang.T(changed ? "Numbers with your Gear Planner set:" : "Your Gear Planner set:") + " " + string.Join(", ", parts.ToArray());
        }

        static string Scale(string num, double f)
        {
            double v;
            if (!double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return num;
            double r = v * f;
            return r >= 10 ? Math.Round(r).ToString(System.Globalization.CultureInfo.InvariantCulture)
                           : r.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
