// The look: 2006 wow.com colours, the gold bevel buttons, small icon buttons and shared UI helpers.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public static class Theme
    {
        public static readonly Color BgTop = Hex("#1c1209"), BgBottom = Hex("#050302"), Gold = Hex("#ffb90f"),
            GoldDim = Hex("#8a6a1c"), Bronze = Hex("#4a3414"), Text = Hex("#d8c28a"), Panel = Hex("#120c06"),
            LinkBlue = Hex("#6aa9ff"), Ink = Hex("#0b0704"), Good = Hex("#8bc34a"), Bad = Hex("#ff6b5a"),
            Credit = Hex("#b39a5c");

        public static string FontName = "Times New Roman";
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static Image logo, portrait;
        static Icon icon;

        public static Color Hex(string h)
        {
            return Color.FromArgb(Convert.ToInt32(h.Substring(1, 2), 16), Convert.ToInt32(h.Substring(3, 2), 16),
                                  Convert.ToInt32(h.Substring(5, 2), 16));
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // Nearest to the "Azeroth Survivors" logo lettering: the game's own Morpheus if installed, then Cinzel,
        // then Papyrus (ships with Office), then the earlier choices.
        public static void PickFont()
        {
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var fc = new InstalledFontCollection())
                foreach (FontFamily f in fc.Families) have.Add(f.Name);
            foreach (string f in new[] { "Morpheus", "Cinzel", "Papyrus", "Friz Quadrata TT", "Friz Quadrata Std",
                                         "Palatino Linotype", "Book Antiqua", "Georgia" })
                if (have.Contains(f)) { FontName = f; return; }
        }

        public static Font F(float size) { return F(size, FontStyle.Regular); }

        public static Font F(float size, FontStyle style)
        {
            string key = size + "|" + (int)style;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                try { f = new Font(FontName, size, style, GraphicsUnit.Point); }
                catch (Exception) { f = new Font(FontFamily.GenericSerif, size, style, GraphicsUnit.Point); }
                fonts[key] = f;
            }
            return f;
        }

        public static Image Logo { get { if (logo == null) logo = LoadImage("AS.logo.png"); return logo; } }
        public static Image Portrait { get { if (portrait == null) portrait = LoadImage("AS.portrait.png"); return portrait; } }

        public static Icon AppIcon
        {
            get
            {
                if (icon == null)
                {
                    try
                    {
                        using (Stream s = Util.ResourceStream("AS.app.ico")) icon = new Icon(s);
                    }
                    catch (Exception) { icon = SystemIcons.Application; }
                }
                return icon;
            }
        }

        static Image LoadImage(string name)
        {
            try
            {
                using (Stream s = Util.ResourceStream(name))
                using (var tmp = Image.FromStream(s))
                    return new Bitmap(tmp);
            }
            catch (Exception) { return null; }
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        // text centred in a box, with an optional 1 px shadow like the Tk version
        public static void CenterText(Graphics g, string text, Font f, Color fg, RectangleF box, Color? shadow)
        {
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                if (shadow.HasValue)
                    using (var b = new SolidBrush(shadow.Value))
                        g.DrawString(text, f, b, new RectangleF(box.X + 1, box.Y + 1, box.Width, box.Height), sf);
                using (var b = new SolidBrush(fg)) g.DrawString(text, f, b, box, sf);
            }
        }
    }

    public delegate void IconPainter(Graphics g, float x, float y, float size, Color color);

    // ---------- Custom gold-bevel button ----------
    public class WowButton : Control
    {
        string text;
        float size;
        bool hover, down, active, selected;
        public IconPainter Icon;
        public event Action Pressed;

        public WowButton(string text, Action command, int width, int height, float size)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            this.text = text;
            this.size = size;
            Size = new Size(width, height);
            Cursor = Cursors.Hand;
            if (command != null) Pressed += command;
        }

        public string Label { get { return text; } set { text = value; Invalidate(); } }
        public float FontSize { get { return size; } set { size = value; Invalidate(); } }
        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }   // filled gold: the current choice

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { down = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool was = down;
            down = false;
            Invalidate();
            base.OnMouseUp(e);
            if (was && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) Fire(Pressed);
        }

        // runs the button's action after the click has been fully handled, so the action may rebuild the window
        public static void Fire(Action a)
        {
            if (a == null) return;
            var ctx = System.Threading.SynchronizationContext.Current;
            if (ctx != null) ctx.Post(delegate { a(); }, null);
            else a();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = Width, h = Height;
            string state = down ? "down" : hover ? "hover" : active ? "active" : "normal";
            Color top, bot, border, fg, shadow = Color.Black;
            if (state == "hover") { top = Theme.Hex("#5a4118"); bot = Theme.Hex("#241808"); border = Theme.Gold; fg = Theme.Hex("#fff2b0"); }
            else if (state == "down") { top = Theme.Hex("#120b04"); bot = Theme.Hex("#2e200c"); border = Theme.Gold; fg = Theme.Gold; }
            else if (state == "active") { top = Theme.Hex("#4a3414"); bot = Theme.Hex("#1c1208"); border = Theme.Gold; fg = Theme.Hex("#fff2b0"); }
            else { top = Theme.Hex("#3a2a12"); bot = Theme.Hex("#150e06"); border = Theme.GoldDim; fg = Theme.Gold; }
            if (selected)
            {
                top = Theme.Hex("#ffd966"); bot = Theme.Hex("#d48f16"); border = Theme.Hex("#fff6c8");
                fg = Theme.Hex("#2a1706"); shadow = Theme.Hex("#ffe9a0");
            }
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), top, bot, LinearGradientMode.Vertical))
                g.FillRectangle(lg, 0, 0, w, h);
            Theme.Smooth(g);
            using (var p = new Pen(border, 2)) g.DrawRectangle(p, 1, 1, w - 3, h - 3);
            g.SmoothingMode = SmoothingMode.None;
            using (var p = new Pen(Theme.Bronze, 1)) g.DrawRectangle(p, 5, 5, w - 11, h - 11);
            Theme.Smooth(g);
            using (var b = new SolidBrush(border))
                foreach (var pt in new[] { new PointF(3, 3), new PointF(w - 4, 3), new PointF(3, h - 4), new PointF(w - 4, h - 4) })
                    g.FillEllipse(b, pt.X - 2, pt.Y - 2, 4, 4);
            float tx = 0, tw = w;
            if (Icon != null)                                  // icon at the beginning, text shifted right
            {
                float isz = h - 12;
                Icon(g, 10 + isz / 2, h / 2f, isz, fg);
                tx = 10 + isz + 4;
                tw = w - 8 - tx;
            }
            Theme.CenterText(g, text, Theme.F(size, FontStyle.Bold), fg, new RectangleF(tx, 0, tw, h), shadow);
        }
    }

    // Small square bronze button with a drawn icon (the section window's side strip).
    public class IconButton : Control
    {
        readonly Action<Graphics, float, float, Color> icon;
        bool hover, active;
        public event Action Pressed;

        public IconButton(Action<Graphics, float, float, Color> icon, Action command)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            this.icon = icon;
            Size = new Size(34, 34);
            Cursor = Cursors.Hand;
            if (command != null) Pressed += command;
        }

        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) WowButton.Fire(Pressed);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int s = Width;
            bool lit = active || hover;
            g.Clear(Theme.Hex("#15100a"));
            using (var b = new SolidBrush(Theme.Hex("#1c130a"))) g.FillRectangle(b, 1, 1, s - 3, s - 3);
            using (var p = new Pen(lit ? Theme.Gold : Theme.Hex("#7a5a22"), 2)) g.DrawRectangle(p, 1, 1, s - 3, s - 3);
            using (var p = new Pen(Theme.Hex("#3a2a12"), 1)) g.DrawRectangle(p, 4, 4, s - 9, s - 9);
            Theme.Smooth(g);
            icon(g, s / 2f, s / 2f, lit ? Theme.Gold : Theme.Hex("#c9a24a"));
        }
    }

    public static class Icons
    {
        static readonly Color Hole = Theme.Hex("#1c130a");

        public static void Home(Graphics g, float x, float y, Color c)
        {
            using (var b = new SolidBrush(c))
            {
                g.FillPolygon(b, new[] { new PointF(x - 11, y), new PointF(x, y - 10), new PointF(x + 11, y) });
                g.FillRectangle(b, x - 7, y, 14, 10);
            }
            using (var b = new SolidBrush(Hole)) g.FillRectangle(b, x - 2, y + 4, 4, 6);
        }

        public static void Cloud(Graphics g, float x, float y, Color c)
        {
            using (var b = new SolidBrush(c))
            {
                g.FillEllipse(b, x - 10, y - 2, 9, 9);
                g.FillEllipse(b, x - 5, y - 9, 11, 11);
                g.FillEllipse(b, x + 1, y - 4, 9, 11);
                g.FillRectangle(b, x - 6, y, 12, 7);
            }
        }

        public static void AddonsIcon(Graphics g, float x, float y, Color c)
        {
            using (var b = new SolidBrush(c))
                foreach (int dx in new[] { -9, 1 })
                    foreach (int dy in new[] { -9, 1 })
                        g.FillRectangle(b, x + dx, y + dy, 8, 8);
        }

        public static void Gear(Graphics g, float x, float y, Color c)
        {
            using (var p = new Pen(c, 4))
                foreach (var d in new[] { new PointF(-11, 0), new PointF(11, 0), new PointF(0, -11), new PointF(0, 11),
                                          new PointF(-8, -8), new PointF(8, 8), new PointF(-8, 8), new PointF(8, -8) })
                    g.DrawLine(p, x, y, x + d.X, y + d.Y);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x - 7, y - 7, 14, 14);
            using (var b = new SolidBrush(Hole)) g.FillEllipse(b, x - 3, y - 3, 6, 6);
        }

        public static void Scroll(Graphics g, float x, float y, Color c)
        {
            using (var p = new Pen(c, 2))
            {
                g.DrawRectangle(p, x - 8, y - 10, 16, 20);
                foreach (int dy in new[] { -5, 0, 5 }) g.DrawLine(p, x - 4, y + dy, x + 4, y + dy);
            }
        }

        public static void Info(Graphics g, float x, float y, Color c)
        {
            using (var p = new Pen(c, 2)) g.DrawEllipse(p, x - 10, y - 10, 20, 20);
            using (var b = new SolidBrush(c))
            {
                g.FillEllipse(b, x - 1.5f, y - 6, 3, 3);
                g.FillRectangle(b, x - 1.5f, y - 1, 3, 8);
            }
        }

        // a small speech bubble (generic chat symbol) for the DISCORD button
        public static void Chat(Graphics g, float x, float y, float size, Color c)
        {
            float r = size / 2;
            using (var p = new Pen(c, 2)) g.DrawRectangle(p, x - r, y - r * 0.8f, 2 * r, r * 1.25f);
            using (var b = new SolidBrush(c))
            {
                g.FillPolygon(b, new[] { new PointF(x - r * 0.5f, y + r * 0.45f), new PointF(x - r * 0.5f, y + r * 0.95f),
                                         new PointF(x, y + r * 0.45f) });
                foreach (float dx in new[] { -r * 0.45f, 0f, r * 0.45f })
                    g.FillEllipse(b, x + dx - 1.5f, y - r * 0.2f - 1.5f, 3, 3);
            }
        }
    }

    // ---------- small helpers to build the sections and dialogs ----------
    public static class UI
    {
        public static Label Text(string text, float size, FontStyle style, Color fg, Color bg, int wrapWidth)
        {
            var l = new Label();
            l.Text = text;
            l.Font = Theme.F(size, style);
            l.ForeColor = fg;
            l.BackColor = bg;
            l.AutoSize = true;
            l.UseMnemonic = false;
            if (wrapWidth > 0) l.MaximumSize = new Size(wrapWidth, 0);
            return l;
        }

        public static Panel Line(Color color, int width, int height)
        {
            var p = new Panel();
            p.BackColor = color;
            p.Size = new Size(width, height);
            return p;
        }

        public static CheckBox Check(string text, bool value, Color fg, Color bg, float size)
        {
            var c = new CheckBox();
            c.Text = text;
            c.Checked = value;
            c.ForeColor = fg;
            c.BackColor = bg;
            c.Font = Theme.F(size);
            c.FlatStyle = FlatStyle.Flat;
            c.FlatAppearance.BorderColor = Theme.GoldDim;
            c.FlatAppearance.CheckedBackColor = Theme.Ink;
            c.FlatAppearance.MouseOverBackColor = bg;
            c.FlatAppearance.MouseDownBackColor = bg;
            c.AutoSize = true;
            c.UseMnemonic = false;
            c.Cursor = Cursors.Hand;
            return c;
        }

        public static RichTextBox Log(Color bg, float size)
        {
            var r = new RichTextBox();
            r.ReadOnly = true;
            r.BorderStyle = BorderStyle.None;
            r.BackColor = bg;
            r.ForeColor = Theme.Text;
            r.Font = Theme.F(size);
            r.DetectUrls = false;
            r.ScrollBars = RichTextBoxScrollBars.Vertical;
            r.WordWrap = true;
            return r;
        }

        public static void Append(RichTextBox box, string text, Color color, Font font)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = color;
            if (font != null) box.SelectionFont = font;
            box.AppendText(text);
        }

        // the back arrow, top left, same spot and look in every window
        public static WowButton BackArrow(Action action)
        {
            return new WowButton("‹", action, 52, 32, 18);
        }

        public static Color LevelColor(Level lv)
        {
            switch (lv)
            {
                case Level.Good: return Theme.Good;
                case Level.Warn: return Theme.Gold;
                case Level.Error: return Theme.Bad;
                case Level.Dim: return Theme.GoldDim;
                default: return Theme.Text;
            }
        }
    }

    // A panel that scrolls with the mouse wheel; Overscroll(-1 / +1) is called when the wheel turns further
    // while it is already at its top / bottom.
    public interface IWheel { void Wheel(int delta); }

    public class ScrollPanel : Panel, IWheel
    {
        public Action<int> Overscroll;

        public ScrollPanel()
        {
            AutoScroll = true;
            DoubleBuffered = true;
        }

        public void Wheel(int delta)
        {
            int d = delta > 0 ? -1 : 1;
            int max = Math.Max(0, DisplayRectangle.Height - ClientSize.Height);
            int pos = -AutoScrollPosition.Y;
            if (Overscroll != null && ((d < 0 && pos <= 0) || (d > 0 && pos >= max)))
            {
                Overscroll(d);
                return;
            }
            AutoScrollPosition = new Point(0, Math.Max(0, Math.Min(max, pos + d * 40)));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Wheel(e.Delta);
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        public void ScrollToEnd()
        {
            AutoScrollPosition = new Point(0, Math.Max(0, DisplayRectangle.Height - ClientSize.Height));
        }
    }

    public enum Align { Left, Center, Right, Fill }

    // Lays its children out top to bottom (like Tk's pack): each keeps its own size, placed left, centred or right.
    public class VStack : Panel
    {
        class Item { public Control C; public Align A; public Padding M; }
        readonly List<Item> items = new List<Item>();
        public int MinWidth;

        public VStack()
        {
            BackColor = Theme.Panel;
        }

        public T Add<T>(T c, Align a, int left, int top, int right, int bottom) where T : Control
        {
            var it = new Item();
            it.C = c;
            it.A = a;
            it.M = new Padding(left, top, right, bottom);
            items.Add(it);
            Controls.Add(c);
            return c;
        }

        public void ClearItems()
        {
            SuspendLayout();
            foreach (var it in items)
            {
                Controls.Remove(it.C);
                it.C.Dispose();
            }
            items.Clear();
            ResumeLayout();
        }

        public static Size Measure(Control c)
        {
            var l = c as Label;
            if (l != null && l.AutoSize)
            {
                Size p = l.MaximumSize.Width > 0 ? l.GetPreferredSize(new Size(l.MaximumSize.Width, 0)) : l.GetPreferredSize(Size.Empty);
                if (l.MaximumSize.Width > 0 && p.Width > l.MaximumSize.Width) p.Width = l.MaximumSize.Width;
                return p;
            }
            var cb = c as CheckBox;
            if (cb != null && cb.AutoSize) return cb.GetPreferredSize(Size.Empty);
            return c.Size;
        }

        // positions everything and takes the size it needs (at least width, when given)
        public void Arrange(int width)
        {
            int w = Math.Max(MinWidth, width);
            foreach (var it in items)
                if (it.A != Align.Fill) w = Math.Max(w, Measure(it.C).Width + it.M.Horizontal);
            int y = 0;
            foreach (var it in items)
            {
                Size s = Measure(it.C);
                if (it.A == Align.Fill) s.Width = w - it.M.Horizontal;
                int x = it.A == Align.Center ? it.M.Left + (w - it.M.Horizontal - s.Width) / 2
                      : it.A == Align.Right ? w - it.M.Right - s.Width : it.M.Left;
                it.C.Bounds = new Rectangle(x, y + it.M.Top, s.Width, s.Height);
                y += it.M.Top + s.Height + it.M.Bottom;
            }
            Size = new Size(w, y);
        }
    }

    // Children left to right, centred on one line.
    public class HRow : Panel
    {
        public HRow(int gap, params Control[] children)
        {
            BackColor = Theme.Panel;
            int x = 0, h = 0;
            foreach (Control c in children) h = Math.Max(h, VStack.Measure(c).Height);
            foreach (Control c in children)
            {
                Size s = VStack.Measure(c);
                c.Bounds = new Rectangle(x, (h - s.Height) / 2, s.Width, s.Height);
                Controls.Add(c);
                x += s.Width + gap;
            }
            Size = new Size(Math.Max(0, x - gap), h);
        }
    }

    // Base for the pop-up windows: dark frame with a gold border, the back arrow top left, content stacked below.
    public class WowDialog : Form
    {
        protected readonly VStack Body;
        readonly Panel frame;
        public Action BackAction;

        public WowDialog(string title)
        {
            Text = title;
            Icon = Theme.AppIcon;
            BackColor = Theme.BgBottom;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = true;          // its own taskbar button, so it does not depend on the launcher being open
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            frame = new Panel();
            frame.BackColor = Theme.Panel;
            frame.Location = new Point(10, 10);
            frame.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var p = new Pen(Theme.GoldDim, 2)) e.Graphics.DrawRectangle(p, 1, 1, frame.Width - 2, frame.Height - 2);
            };
            Controls.Add(frame);
            Body = new VStack();
            Body.Location = new Point(2, 2);
            frame.Controls.Add(Body);
            BackAction = Close;
        }

        // starts a page: empties the window and puts the back arrow in its corner
        protected void Begin()
        {
            Body.ClearItems();
            Body.Add(UI.BackArrow(delegate { if (BackAction != null) BackAction(); }), Align.Left, 20, 12, 0, 0);
        }

        // ends a page: sizes the window to its content
        protected void Finish()
        {
            Body.Arrange(0);
            frame.Size = new Size(Body.Width + 4, Body.Height + 4);
            ClientSize = new Size(frame.Width + 20, frame.Height + 20);
            frame.Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && BackAction != null) { e.Handled = true; BackAction(); }
            base.OnKeyDown(e);
        }

        protected void Heading(string title, string right, int pad)
        {
            var t = UI.Text(title, 18, FontStyle.Bold, Theme.Gold, Theme.Panel, 0);
            if (right == null)
                Body.Add(t, Align.Center, pad, 4, pad, 4);
            else
            {
                var row = new Panel();
                row.BackColor = Theme.Panel;
                var r = UI.Text(right, 10, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
                Size ts = VStack.Measure(t), rs = VStack.Measure(r);
                int w = Math.Max(420, ts.Width + rs.Width + 30);
                t.Location = new Point(0, 0);
                r.Location = new Point(w - rs.Width, ts.Height - rs.Height - 4);
                r.Anchor = AnchorStyles.Top | AnchorStyles.Right;     // stays at the right edge when the row widens
                row.Controls.Add(t);
                row.Controls.Add(r);
                row.Size = new Size(w, ts.Height);
                Body.Add(row, Align.Fill, pad, 4, pad, 4);
            }
            Body.Add(UI.Line(Theme.GoldDim, 10, 2), Align.Fill, pad, 0, pad, 10);
        }

        protected Label Para(string text, float size, FontStyle style, Color fg, int pad, int top, int bottom, int wrap)
        {
            return Body.Add(UI.Text(text, size, style, fg, Theme.Panel, wrap), Align.Left, pad, top, pad, bottom);
        }

        protected static WowButton B(string text, Action a, int w, int h, float size)
        {
            return new WowButton(text, a, w, h, size);
        }

        // centred over the launcher when that is on screen, otherwise centred on the screen
        public void PlaceOver(Form parent)
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x, y;
            if (parent != null && parent.Visible && parent.WindowState == FormWindowState.Normal)
            {
                area = Screen.FromControl(parent).WorkingArea;
                x = parent.Left + (parent.Width - Width) / 2;
                y = parent.Top + (parent.Height - Height) / 2;
            }
            else
            {
                x = area.Left + (area.Width - Width) / 2;
                y = area.Top + (area.Height - Height) / 3;
            }
            Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
        }
    }
}
