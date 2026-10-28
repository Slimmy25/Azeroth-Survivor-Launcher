// The Gear Planner (Guide > Theorycrafting): pick a class, put gear and jewelry in the slots of a character sheet laid
// out like the game's own (C), and see what they add: the sheet's stats and the Run Stats panel beside it.
// The numbers are the bonuses added together (Items.cs), with the 4-piece bonus of every set worn at 4 pieces of 1+ stars
// (two sets of four give both). A click on a piece adds a star (5 at most, then back to 0): stars decide the set bonuses
// reached; what a star adds to a piece is not known yet, so it is not counted. A piece whose bonuses are not
// known yet adds nothing.
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
            int top = GuideTop, w = PW - 40;                  // the trail above leads to the talents and the other tools
            // the sheet fits the box: its slots are sized to the room there is, so nothing scrolls
            int h = PH - 12 - top;
            var sheet = new GearSheet(plannerClass, plannerSlots, GearIcons(), h);
            sheet.Bounds = new Rectangle(20, top, w, h);
            panel.Controls.Add(sheet);
        }

        // a new class: its gear goes (each class has its own), the jewelry stays
        void PlannerClass(string cls)
        {
            if (plannerClass != cls)
                foreach (string s in new List<string>(plannerSlots.Keys))
                    if (plannerSlots[s] is GearPiece) { plannerSlots.Remove(s); GearSheet.Stars.Remove(s); }
            plannerClass = cls;
        }

        // the gear's real icons (assets\item-icons.txt); a piece that is not a real item shows "Image soon"
        Dictionary<string, string> GearIcons() { return Items.GearIcons; }

        // the gear in the planner, added up: the hover texts of spells and talents show their numbers with it
        public static Dictionary<string, double> PlannerTotals;
    }

    public class GearSheet : Control
    {
        static readonly string[] Left = { "Head", "Neck", "Shoulder", "Back", "Chest", "Shirt", "Tabard", "Wrist" };
        static readonly string[] Right = { "Hands", "Waist", "Legs", "Feet", "Finger 1", "Finger 2", "Trinket 1", "Trinket 2" };
        static readonly string[] Bottom = { "Main Hand", "Off Hand", "Ranged" };
        static readonly HashSet<string> UnusedSlots = new HashSet<string> { "Back", "Shirt", "Tabard", "Off Hand", "Ranged" };
        static readonly HashSet<string> UnusedHunter = new HashSet<string> { "Back", "Shirt", "Tabard", "Off Hand", "Main Hand" };
        static readonly HashSet<string> UnusedDual = new HashSet<string> { "Back", "Shirt", "Tabard", "Ranged" };
        // a hunter's weapon is Ranged; a rogue (two daggers) and a shaman (a shield) also fill the Off Hand
        HashSet<string> Unused { get { return cls == "Hunter" ? UnusedHunter : (cls == "Rogue" || cls == "Shaman") ? UnusedDual : UnusedSlots; } }

        readonly string cls;
        readonly Dictionary<string, object> slots;
        readonly Dictionary<string, string> gearIcons;       // piece name -> icon (the ones AZS Stats has seen)
        readonly Dictionary<string, Rectangle> boxes = new Dictionary<string, Rectangle>();
        readonly List<KeyValuePair<Rectangle, object>> rows = new List<KeyValuePair<Rectangle, object>>();   // the selector's lines
        readonly Font fTitle = Theme.F(13, FontStyle.Bold), fText = Theme.F(9.5f), fBold = Theme.F(9.5f, FontStyle.Bold),
                      fSmall = Theme.F(8.25f), fTier = Theme.F(7f, FontStyle.Bold), fHead = Theme.F(11, FontStyle.Bold);   // made with the section's zoom; cached
        readonly ToolTip tip = new ToolTip();
        int slotSize = Math.Max(26, (int)(44 * Theme.Zoom)), slotGap = Math.Max(4, (int)(7 * Theme.Zoom));
        static string chosenSlot = "Head";                   // the slot the selector shows (kept while the launcher runs)
        string hover;
        int hoverRow = -1;
        static readonly object EmptyRow = new object();
        // each slot's stars, 0 to 5 (kept while the launcher runs, like the slots): a click on the piece adds one
        public static readonly Dictionary<string, int> Stars = new Dictionary<string, int>();
        // what Totals found: every set worn (rarity -> pieces worn, at 1+, 3+, 5 stars)
        readonly Dictionary<string, int[]> setsWorn = new Dictionary<string, int[]>();
        static readonly Color Active = Theme.Hex("#40d040");

        static int StarsOf(string slot) { int n; return Stars.TryGetValue(slot, out n) ? n : 0; }

        // the list beside the sheet: Single Item (what the chosen slot can hold) or Tier Bonus (a whole set at once)
        static bool tierMode;
        readonly List<KeyValuePair<Rectangle, bool>> modeBoxes = new List<KeyValuePair<Rectangle, bool>>();
        readonly List<KeyValuePair<Rectangle, string[]>> tierBoxes = new List<KeyValuePair<Rectangle, string[]>>();   // { rarity, "4" or "8" }
        int hoverMode = -1, hoverTier = -1;

        static readonly string[] ArmorSlots = { "Head", "Shoulder", "Chest", "Waist", "Legs", "Feet", "Wrist", "Hands" };

        // a set put in the planner at once: 4 pieces at T1 (its 4-piece bonus) or all 8 at T3 (both bonuses). It is a
        // starting point, not a pile: the whole planner is emptied first (armor, weapons and jewelry, with their stars),
        // then filled with the set picked. Slimjim's call (2026-10-28): a Tier Bonus button replaces what was calculated.
        public static void FillSet(string cls, string rarity, int pieces, Dictionary<string, object> slots)
        {
            slots.Clear();
            Stars.Clear();
            int n = 0;
            foreach (string slot in ArmorSlots)
            {
                if (n >= pieces) break;
                foreach (var p in Items.GearOf(cls, slot))
                    if (p.Rarity == rarity && Items.IsArmor(p))
                    {
                        slots[slot] = p;
                        Stars[slot] = pieces >= 8 ? 3 : 1;
                        n++;
                        break;
                    }
            }
        }

        static readonly Color Page = Theme.Hex("#0e0d14"), Edge = Theme.Hex("#4a3a22"), SlotBg = Theme.Hex("#1b1a24"),
            SlotEdge = Theme.Hex("#45414f"), Gold = Theme.Hex("#f0c050"), Bright = Theme.Hex("#f5e9c8"), Dim = Theme.Hex("#9c8a64");

        // the height that shows every slot (the title, eight rows of slots and the weapons)
        public int NeededHeight { get { return 40 + (int)(fTitle.Height + fSmall.Height) + 9 * (slotSize + slotGap) + 14; } }

        // a sheet that fits a height: its slots (and the gaps between them) are sized so the whole sheet fits it
        public GearSheet(string cls, Dictionary<string, object> slots, Dictionary<string, string> gearIcons, int height)
            : this(cls, slots, gearIcons)
        {
            int fixedH = 40 + (int)(fTitle.Height + fSmall.Height) + 14;
            int row = Math.Max(24, (height - fixedH) / 9);    // a slot and its gap
            slotSize = Math.Max(20, Math.Min((int)(44 * Theme.Zoom), row * 44 / 51));
            slotGap = Math.Max(3, row - slotSize);
        }

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
            setsWorn.Clear();
            foreach (var kv in slots)
            {
                var p = kv.Value as GearPiece;
                List<ItemBonus> list = null;
                if (p != null)
                {
                    int st = StarsOf(kv.Key);
                    if (Items.IsArmor(p))                    // the set it counts for: worn, at 1+, 3+ and 5 stars
                    {
                        int[] c;
                        if (!setsWorn.TryGetValue(p.Rarity, out c)) setsWorn[p.Rarity] = c = new int[4];
                        c[0]++;
                        if (st >= 1) c[1]++;
                        if (st >= 3) c[2]++;
                        if (st >= 5) c[3]++;
                    }
                    if (!p.Known) { unknown++; continue; }
                    health += p.Health;
                    list = p.Bonuses;
                }
                var j = kv.Value as Jewel;
                if (j != null) list = j.Bonuses;
                if (list == null) continue;
                foreach (var b in list) Add(t, b.Stat, b.Value);
            }
            // the 4-piece bonus of every set with 4 armor pieces at 1+ stars: two sets of four give both
            foreach (var kv in setsWorn)
            {
                var four = Items.FourPiece(kv.Key);
                if (kv.Value[1] >= 4 && four != null) foreach (var b in four.Bonuses) Add(t, b.Stat, b.Value);
            }
            return t;
        }

        static void Add(Dictionary<string, double> t, string stat, double value)
        {
            double v;
            t.TryGetValue(stat, out v);
            t[stat] = v + value;
        }

        // the totals for the hover texts of spells and talents (GearEffect)
        void Publish()
        {
            int h, u;
            var t = Totals(out h, out u);
            LauncherApp.PlannerTotals = t.Count > 0 ? t : null;
        }

        string Val(Dictionary<string, double> t, string stat, string unit)
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
            int tierW = (int)Math.Ceiling(TextRenderer.MeasureText("T5", fTier).Width * 0.8) + 2;   // room for a piece's T1-T5 on its right
            int lx = sheet.X + 12, rx = sheet.Right - 8 - tierW - s;
            for (int i = 0; i < Left.Length; i++) Slot(g, Left[i], new Rectangle(lx, top + i * (s + gap), s, s));
            for (int i = 0; i < Right.Length; i++) Slot(g, Right[i], new Rectangle(rx, top + i * (s + gap), s, s));
            int by = top + 8 * (s + gap);
            int bgap = gap + tierW;                           // the weapons: room for their T1-T5 between them
            int bx = sheet.X + (sheet.Width - (3 * s + 2 * bgap)) / 2;
            for (int i = 0; i < Bottom.Length; i++) Slot(g, Bottom[i], new Rectangle(bx + i * (s + bgap), by, s, s));

            // the middle: the two stat boxes like the game's, then the note
            var mid = new Rectangle(lx + s + 6 + tierW, top, rx - lx - s - 18 - tierW, by - top - 8);
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
            string txt = Lang.T("The bonuses of everything chosen, added together, with the set bonuses reached. Click a piece to add a star (up to 5, then back to 0); right-click to take it off.");
            if (unknown > 0) txt = Lang.T("Pieces with bonuses not known yet") + ": " + unknown + ". " + txt;
            float noteH = Para(g, txt, fSmall, Dim, mid.X, mid.Y + boxH + 8, mid.Width, mid.Bottom);

            // under the totals, in the middle of the sheet: every set worn, its 4-piece and 8-piece bonuses (bright when
            // reached), so two sets of four show and count both, and an 8-piece shows its 4-piece too
            int setsTop = (int)(mid.Y + boxH + 8 + noteH + 8);
            DrawSets(g, new Rectangle(mid.X, setsTop, mid.Width, by - 8 - setsTop));   // above the weapons

            // the selector: what the chosen slot can hold, each with its picture
            Selector(g, pick);
        }

        void DrawSets(Graphics g, Rectangle r)
        {
            float y = r.Y;
            using (var b = new SolidBrush(Gold)) g.DrawString(Lang.T("Set bonuses"), fBold, b, r.X, y);
            y += fBold.GetHeight(g) + 2;
            if (setsWorn.Count == 0)
            {
                Para(g, Lang.T("Wear armor pieces of a set to see its bonuses here. Four pieces of two sets give both 4-piece bonuses."), fSmall, Dim, r.X, y, r.Width, r.Bottom);
                return;
            }
            // what is reached, in one line
            var reached = new List<string>();
            foreach (string rar in Items.Rarities)
            {
                int[] c;
                if (!setsWorn.TryGetValue(rar, out c)) continue;
                if (c[1] >= 4) reached.Add(Items.SetName(cls, rar) + " (4)");
                if (c[2] >= 8) reached.Add(Items.SetName(cls, rar) + " (8)" + (c[3] >= 8 ? " " + Lang.T("empowered") : ""));
            }
            y += Para(g, Lang.T("Reached") + ": " + (reached.Count > 0 ? string.Join(", ", reached.ToArray()) : Lang.T("none yet")),
                      fSmall, reached.Count > 0 ? Active : Dim, r.X, y, r.Width, r.Bottom) + 4;
            foreach (string rar in Items.Rarities)
            {
                int[] c;
                if (!setsWorn.TryGetValue(rar, out c) || y >= r.Bottom) continue;
                y += Para(g, Items.SetName(cls, rar) + "  " + c[0] + "/8", fBold, GearSheet.RarityColor(rar), r.X, y, r.Width, r.Bottom);
                var four = Items.FourPiece(rar);
                string eight = Items.EightPiece(cls, rar);
                bool on4 = c[1] >= 4, on8 = c[2] >= 8, emp = c[3] >= 8;
                y += Rich(g, "(4) " + (four != null ? four.Text : Lang.T("not entered yet")) + "  [" + c[1] + "/4, 1+ " + Lang.T("stars") + "]",
                          fSmall, on4 ? Active : Dim, on4, r.X + 10, y, r.Width - 10, r.Bottom);
                y += Rich(g, "(8) " + (eight ?? Lang.T("not entered yet")) + "  [" + c[2] + "/8, 3+ " + Lang.T("stars") + "]" +
                          (emp ? "  " + Lang.T("empowered") : ""), fSmall, on8 ? Active : Dim, on8, r.X + 10, y, r.Width - 10, r.Bottom) + 4;
            }
        }

        // a line wrapped word by word, the spells and talents in it in gold (as the Guide links them); its height
        static float Rich(Graphics g, string text, Font f, Color baseColor, bool bright, float x, float y, float w, float bottom)
        {
            float lh = f.GetHeight(g);
            if (y + lh > bottom) return 0;
            using (var fmt = StringFormat.GenericTypographic)
                return RichIn(g, text, f, baseColor, bright, x, y, w, bottom, fmt);
        }

        static float RichIn(Graphics g, string text, Font f, Color baseColor, bool bright, float x, float y, float w, float bottom, StringFormat fmt)
        {
            float lh = f.GetHeight(g);
            float space = g.MeasureString("a b", f, 1000, fmt).Width - g.MeasureString("ab", f, 1000, fmt).Width;
            if (space <= 0) space = f.Size * 0.3f;
            float cx = x, cy = y;
            Color link = bright ? Gold : Color.FromArgb(200, 184, 154, 80);
            foreach (var run in GuideLinks.Split(text))
            {
                string[] words = run.Key.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    if (i > 0) cx += space;
                    if (words[i].Length == 0) continue;
                    float ww = g.MeasureString(words[i], f, 1000, fmt).Width;
                    if (cx > x && cx + ww > x + w) { cx = x; cy += lh; }
                    if (cy + lh > bottom) return cy - y;
                    using (var b = new SolidBrush(run.Value != null ? link : baseColor)) g.DrawString(words[i], f, b, cx, cy, fmt);
                    cx += ww;
                }
            }
            return cy + lh - y;
        }

        // a paragraph wrapped to a width (cut at the bottom); its height
        static float Para(Graphics g, string text, Font f, Color c, float x, float y, float w, float bottom)
        {
            if (y >= bottom) return 0;
            SizeF sz = g.MeasureString(text, f, (int)Math.Max(10, w));
            float h = Math.Min(sz.Height, bottom - y);
            using (var b = new SolidBrush(c)) g.DrawString(text, f, b, new RectangleF(x, y, w, h));
            return h;
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
            modeBoxes.Clear();
            tierBoxes.Clear();
            float lh = Math.Max(fText.GetHeight(g) + 6, 20);
            // where the header was: Single Item / Tier Bonus
            int bh = (int)lh + 4, half = (r.Width - 24) / 2;
            using (var sfc = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < 2; i++)
                {
                    bool tier = i == 1, on = tier == tierMode;
                    var box = new Rectangle(r.X + 8 + i * (half + 8), r.Y + 8, half, bh);
                    using (var b = new SolidBrush(on ? Color.FromArgb(200, 240, 192, 80) : hoverMode == i ? Theme.Hex("#252331") : SlotBg)) g.FillRectangle(b, box);
                    using (var p = new Pen(on ? Gold : SlotEdge)) g.DrawRectangle(p, box);
                    using (var b = new SolidBrush(on ? Color.Black : Bright)) g.DrawString(Lang.T(tier ? "Tier Bonus" : "Single Item"), fBold, b, box, sfc);
                    modeBoxes.Add(new KeyValuePair<Rectangle, bool>(box, tier));
                }
            float y = r.Y + 8 + bh + 8;
            if (tierMode) { TierRows(g, r, y, lh); return; }
            // the slot and the name of what it holds, under those two
            object held;
            slots.TryGetValue(chosenSlot, out held);
            using (var b = new SolidBrush(Gold)) g.DrawString(Lang.T(chosenSlot), fHead, b, r.X + 10, y);
            y += fHead.GetHeight(g);
            using (var sfn = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter })
            using (var b = new SolidBrush(held != null ? RarityColor(RarityOf(held)) : Dim))
                g.DrawString(held != null ? NameOf(held) : Lang.T("Empty"), fText, b, new RectangleF(r.X + 10, y, r.Width - 20, lh), sfn);
            y += lh + 2;
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
                        else if (o is GearPiece) Theme.ImageSoon(g, icon, RarityColor(RarityOf(o)));
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

        // Tier Bonus: one line per rarity, its name on the left and 4 piece / 8 piece side by side
        void TierRows(Graphics g, Rectangle r, float y, float lh)
        {
            int bw = Math.Max(54, (int)(r.Width * 0.27f)), bh = (int)lh + 2, i = 0;
            using (var sfc = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var sfl = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
                foreach (string rar in Items.Rarities)
                {
                    if (y + bh > r.Bottom - 6) break;
                    int x8 = r.Right - 8 - bw, x4 = x8 - 6 - bw;
                    var label = new RectangleF(r.X + 10, y, x4 - r.X - 14, bh);
                    using (var b = new SolidBrush(RarityColor(rar))) g.DrawString(Lang.T(rar), fBold, b, label, sfl);
                    foreach (string n in new[] { "4", "8" })
                    {
                        var box = new Rectangle(n == "4" ? x4 : x8, (int)y, bw, bh);
                        using (var b = new SolidBrush(hoverTier == i ? Theme.Hex("#252331") : SlotBg)) g.FillRectangle(b, box);
                        using (var p = new Pen(RarityColor(rar))) g.DrawRectangle(p, box);
                        using (var b = new SolidBrush(Bright)) g.DrawString(Lang.T(n + " piece"), fSmall, b, box, sfc);
                        tierBoxes.Add(new KeyValuePair<Rectangle, string[]>(box, new[] { rar, n }));
                        i++;
                    }
                    y += bh + 6;
                }
            if (y + 2 * fSmall.GetHeight(g) < r.Bottom)
                Para(g, Lang.T("4 piece: four pieces at T1. 8 piece: all eight at T3. Everything else in the planner is emptied."), fSmall, Dim, r.X + 10, y + 2, r.Width - 20, r.Bottom - 4);
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
                if (img != null)                              // at a sharp size, centred in the slot
                {
                    int ic = Theme.CrispIcon(r.Width - 4);
                    Theme.Draw(g, img, new Rectangle(r.X + (r.Width - ic) / 2, r.Y + (r.Height - ic) / 2, ic, ic));
                }
                else if (o is GearPiece) Theme.ImageSoon(g, Rectangle.Inflate(r, -3, -3), RarityColor(RarityOf(o)));
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(70, RarityColor(RarityOf(o))))) g.FillRectangle(b, Rectangle.Inflate(r, -3, -3));
                    string ab = slot.Substring(0, Math.Min(2, slot.Length));
                    SizeF a = g.MeasureString(ab, fBold);
                    using (var b = new SolidBrush(Bright)) g.DrawString(ab, fBold, b, r.X + (r.Width - a.Width) / 2, r.Y + (r.Height - a.Height) / 2);
                }
                using (var p = new Pen(RarityColor(RarityOf(o)), 2f)) g.DrawRectangle(p, r);
                int st = o is GearPiece ? StarsOf(slot) : 0;
                if (st > 0)                                   // its stars: T1 to T5 just outside the box, on its inner side
                {                                             // (left column: by the top right corner; right column: top left),
                    string n = "T" + st;                      // so both columns lean in towards the stats in the middle
                    float x = r.X + r.Width / 2 < ClientSize.Width / 2 ? r.Right + 1 : r.X - 1 - g.MeasureString(n, fTier).Width;
                    using (var b = new SolidBrush(Gold)) g.DrawString(n, fTier, b, x, r.Y - 2);
                }
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
            int hm = -1, ht = -1;
            for (int i = 0; i < modeBoxes.Count; i++) if (modeBoxes[i].Key.Contains(e.Location)) hm = i;
            for (int i = 0; i < tierBoxes.Count; i++) if (tierBoxes[i].Key.Contains(e.Location)) ht = i;
            if (hm != hoverMode || ht != hoverTier) { hoverMode = hm; hoverTier = ht; Invalidate(); }
            Cursor = s != null || r >= 0 || hm >= 0 || ht >= 0 ? Cursors.Hand : Cursors.Default;
            if (s != hover || r != hoverRow)
            {
                hover = s;
                hoverRow = r;
                object o = null;
                if (s != null) slots.TryGetValue(s, out o);
                else if (r >= 0) o = rows[r].Value;
                int st = StarsOf(s ?? chosenSlot);
                string text = o is GearPiece ? Items.Card((GearPiece)o, st) : o is Jewel ? Items.Card((Jewel)o) : s != null ? Lang.T(s) : null;
                tip.SetToolTip(this, text);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = null; hoverRow = -1; tip.SetToolTip(this, null); Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                foreach (var m in modeBoxes)
                    if (m.Key.Contains(e.Location)) { tierMode = m.Value; Invalidate(); return; }
                foreach (var t in tierBoxes)
                    if (t.Key.Contains(e.Location)) { FillSet(cls, t.Value[0], t.Value[1] == "8" ? 8 : 4, slots); Publish(); Invalidate(); return; }
            }
            string s = SlotAt(e.Location);
            if (s != null)
            {
                object worn;
                slots.TryGetValue(s, out worn);
                if (e.Button == MouseButtons.Right) { slots.Remove(s); Stars.Remove(s); }
                else
                {
                    if (worn is GearPiece) Stars[s] = (StarsOf(s) + 1) % 6;   // a click on a piece: one more star, after 5 back to 0
                    chosenSlot = s;                           // the selector shows what this slot can hold
                    tierMode = false;
                }
                hover = null;                                 // the hover card shows the new stars at once
                Publish();
                Invalidate();
                return;
            }
            int r = RowAt(e.Location);
            if (r < 0 || e.Button != MouseButtons.Left) return;
            object o = rows[r].Value;
            if (o == EmptyRow) slots.Remove(chosenSlot);
            else slots[chosenSlot] = o;
            Stars.Remove(chosenSlot);                         // a new piece starts at 0 stars
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
