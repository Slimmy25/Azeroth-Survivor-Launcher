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
        public static readonly Color BgTop = Hex("#16151d"), BgBottom = Hex("#07070b"), Gold = Hex("#f2c14e"),    // night stone and gold, like the launcher art
            GoldDim = Hex("#b39b62"), Bronze = Hex("#34323f"), Text = Hex("#d7cea4"), Panel = Hex("#0e0d14"),
            LinkBlue = Hex("#e6cf8c"), Ink = Hex("#15121c"), Good = Hex("#8bc34a"), Bad = Hex("#ff6b5a"),
            Credit = Hex("#b39a5c"), Card = Hex("#1b1724"), CardEdge = Hex("#3d3647");   // LinkBlue: paths and links (pale gold); Ink: card backgrounds

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

        // The launcher's text font: Source Serif 4 SmText (Adobe, SIL Open Font License), built into the exe and made
        // for reading small text on screens. It has Latin (with Vietnamese), Greek and Cyrillic; Japanese, Korean,
        // Chinese, Thai and Hindi use the Windows font their language file names (@font).
        // Only the big "Azeroth Survivors" title keeps a decorative font (TitleFontName).
        public static string TitleFontName = "Georgia";
        static FontFamily bundled;
        static PrivateFontCollection privateFonts;
        static readonly List<IntPtr> fontMemory = new List<IntPtr>();   // must stay alive while the fonts are used

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

        static void LoadBundledFonts()
        {
            if (privateFonts != null) return;
            privateFonts = new PrivateFontCollection();
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith("AS.font.", StringComparison.Ordinal)) continue;
                    byte[] data;
                    using (Stream s = asm.GetManifestResourceStream(res))
                    using (var ms = new MemoryStream()) { s.CopyTo(ms); data = ms.ToArray(); }
                    IntPtr mem = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(data.Length);
                    System.Runtime.InteropServices.Marshal.Copy(data, 0, mem, data.Length);
                    fontMemory.Add(mem);
                    privateFonts.AddMemoryFont(mem, data.Length);        // for text drawn by the launcher (GDI+)
                    uint n = 0;
                    AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref n);   // and for labels and boxes (GDI)
                }
                if (privateFonts.Families.Length > 0) bundled = privateFonts.Families[0];
            }
            catch (Exception) { bundled = null; }
        }

        public static void PickFont()
        {
            LoadBundledFonts();
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var fc = new InstalledFontCollection())
                foreach (FontFamily f in fc.Families) have.Add(f.Name);
            fonts.Clear();
            TitleFontName = "Georgia";
            foreach (string f in new[] { "Morpheus", "Cinzel", "Friz Quadrata TT", "Friz Quadrata Std", "Palatino Linotype", "Georgia" })
                if (have.Contains(f)) { TitleFontName = f; break; }
            // scripts the bundled font does not have: the language's own Windows font
            string c = Lang.Code ?? "en";
            bool ownScript = c == "ja" || c == "ko" || c.StartsWith("zh") || c == "th" || c == "hi";
            if (ownScript)
                foreach (string f in Lang.FontOverride.Split(','))
                    if (f.Trim().Length > 0 && have.Contains(f.Trim())) { FontName = f.Trim(); return; }
            if (bundled != null) { FontName = bundled.Name; return; }
            FontName = have.Contains("Georgia") ? "Georgia" : "Times New Roman";
        }

        // the title's decorative font, big sizes only
        public static FontFamily TitleFamily()
        {
            try { return new FontFamily(TitleFontName); }
            catch (Exception) { return FontFamily.GenericSerif; }
        }

        public static Font F(float size) { return F(size, FontStyle.Regular); }

        // the sections are drawn a little smaller than the launcher itself: fonts made while Zoom < 1 shrink with it
        public static float Zoom = 1f;
        public const float SectionZoom = 0.85f;

        public static Font F(float size, FontStyle style)
        {
            size = Math.Max(8.75f, (float)Math.Round(size * Zoom * 4) / 4f);   // never smaller than this: readable everywhere
            string key = size + "|" + (int)style;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                try
                {
                    if (bundled != null && FontName == bundled.Name)
                    {
                        FontStyle st = style;
                        if (!bundled.IsStyleAvailable(st)) st = style & (FontStyle.Bold | FontStyle.Italic);
                        if (!bundled.IsStyleAvailable(st)) st = FontStyle.Regular;
                        f = new Font(bundled, size, st | (style & (FontStyle.Underline | FontStyle.Strikeout)), GraphicsUnit.Point);
                    }
                    else f = new Font(FontName, size, style, GraphicsUnit.Point);
                }
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

        public static Image LoadImage(string name)
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
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // the sharpest text Windows can draw
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
        bool hover, down, active, selected, grayed;
        public IconPainter Icon;
        public Image[] Skin;
        readonly float zoom;                                  // launcher art {normal, hover, down, grayed}
        public event Action Pressed;
        public string GrayedTip;                              // shown on hover, only while the button is grayed out

        public WowButton(string text, Action command, int width, int height, float size)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            this.text = text;
            this.size = size;
            zoom = Theme.Zoom;                                 // made inside a section: smaller
            Size = new Size((int)(width * zoom), (int)(height * zoom));
            Cursor = Cursors.Hand;
            if (command != null) Pressed += command;
        }

        public string Label { get { return text; } set { text = value; Invalidate(); } }
        public float FontSize { get { return size; } set { size = value; Invalidate(); } }
        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }   // filled gold: the current choice

        // Grayed: drawn dimmed, does nothing when clicked, and explains itself with GrayedTip on hover.
        // (Not Enabled = false: a disabled control gets no mouse events, so it could not show its tip.)
        public bool Grayed
        {
            get { return grayed; }
            set
            {
                if (grayed == value) return;
                grayed = value;
                down = false;
                Cursor = grayed ? Cursors.Default : Cursors.Hand;
                if (!grayed) Tips.Hide(this);
                Invalidate();
            }
        }

        internal static readonly ToolTip Tips = new ToolTip();

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            Tips.SetToolTip(this, grayed && !string.IsNullOrEmpty(GrayedTip) ? Lang.T(GrayedTip) : null);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !grayed) { down = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool was = down;
            down = false;
            Invalidate();
            base.OnMouseUp(e);
            if (was && !grayed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) Fire(Pressed);
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
            float old = Theme.Zoom;
            Theme.Zoom = zoom;
            try { PaintButton(e.Graphics); }
            finally { Theme.Zoom = old; }
        }

        void PaintButton(Graphics g)
        {
            int w = Width, h = Height;
            if (Skin != null) { PaintSkin(g, w, h); return; }
            if (Retro.Get("btn") != null)                     // every other button: the launcher's blue stone too
            {
                Skin = w > 120 ? Retro.Long : Retro.Button;
                PaintSkin(g, w, h);
                Skin = null;
                return;
            }
            string state = grayed ? "grayed" : down ? "down" : hover ? "hover" : active ? "active" : "normal";
            Color top, bot, border, fg, shadow = Color.Black;
            if (state == "grayed") { top = Theme.Hex("#2a2620"); bot = Theme.Hex("#12100d"); border = Theme.Hex("#5a5246"); fg = Theme.Hex("#7d7466"); }
            else if (state == "hover") { top = Theme.Hex("#5a4118"); bot = Theme.Hex("#241808"); border = Theme.Gold; fg = Theme.Hex("#fff2b0"); }
            else if (state == "down") { top = Theme.Hex("#120b04"); bot = Theme.Hex("#2e200c"); border = Theme.Gold; fg = Theme.Gold; }
            else if (state == "active") { top = Theme.Hex("#4a3414"); bot = Theme.Hex("#1c1208"); border = Theme.Gold; fg = Theme.Hex("#fff2b0"); }
            else { top = Theme.Hex("#3a2a12"); bot = Theme.Hex("#150e06"); border = Theme.GoldDim; fg = Theme.Gold; }
            if (selected && !grayed)
            {
                top = Theme.Hex("#ffd966"); bot = Theme.Hex("#d48f16"); border = Theme.Hex("#fff6c8");
                fg = Theme.Hex("#2a1706"); shadow = Theme.Hex("#ffe9a0");
            }
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), top, bot, LinearGradientMode.Vertical))
                g.FillRectangle(lg, 0, 0, w, h);
            Theme.Smooth(g);
            using (var p = new Pen(border, 2)) g.DrawRectangle(p, 1, 1, w - 3, h - 3);
            g.SmoothingMode = SmoothingMode.None;
            using (var p = new Pen(grayed ? Theme.Hex("#3a352d") : Theme.Bronze, 1)) g.DrawRectangle(p, 5, 5, w - 11, h - 11);
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
            string label = Lang.T(text);
            float fs = size;                                  // a longer translation gets a smaller font so it fits
            while (fs > 6 && g.MeasureString(label, Theme.F(fs, FontStyle.Bold)).Width > tw - 10) fs -= 0.5f;
            Theme.CenterText(g, label, Theme.F(fs, FontStyle.Bold), fg, new RectangleF(tx, 0, tw, h), shadow);
        }

        // the button is a picture from the launcher art, the label drawn on it
        void PaintSkin(Graphics g, int w, int h)
        {
            int i = grayed ? 3 : down ? 2 : (hover || active || selected) ? 1 : 0;
            Image img = Skin.Length > i && Skin[i] != null ? Skin[i] : Skin[0];
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, new Rectangle(0, 0, w, h));
            Theme.Smooth(g);
            Color fg = grayed ? Theme.Hex("#9a948a") : (hover || selected) ? Theme.Hex("#fff2b0") : Theme.Hex("#f2c14e");
            float tx = 0, tw = w;
            if (Icon != null)
            {
                float isz = h - 12;
                Icon(g, 10 + isz / 2, h / 2f, isz, fg);
                tx = 10 + isz + 4;
                tw = w - 8 - tx;
            }
            string label = Lang.T(text);
            float fs = size;
            while (fs > 6 && g.MeasureString(label, Theme.F(fs, FontStyle.Bold)).Width > tw - 10) fs -= 0.5f;
            Theme.CenterText(g, label, Theme.F(fs, FontStyle.Bold), fg, new RectangleF(tx, down ? 1 : 0, tw, h), Color.Black);
            if (selected && !grayed)                           // the current choice: a gold frame around the stone
            {
                using (var p = new Pen(Color.FromArgb(90, Theme.Gold), 4)) g.DrawRectangle(p, 2, 2, w - 5, h - 5);
                using (var p = new Pen(Theme.Gold, 1.6f)) g.DrawRectangle(p, 1, 1, w - 3, h - 3);
            }
        }
    }

    // a check box in the launcher's colors: a night-stone box with a gold tick
    public class NightCheck : CheckBox
    {
        bool hover;

        public NightCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        public override Size GetPreferredSize(Size proposed)
        {
            int box = Font.Height;
            Size t = TextRenderer.MeasureText(Text, Font);
            return new Size(box + 8 + t.Width, Math.Max(box + 2, t.Height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            int box = Math.Min(Font.Height - 2, Height - 2);
            int top = Math.Max(1, Math.Min((Height - box) / 2, (Font.Height - box) / 2 + 2));
            var r = new RectangleF(1, top, box, box);
            using (var path = new GraphicsPath())
            {
                float d = 6;
                path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (var b = new SolidBrush(Checked ? Theme.Hex("#2a2236") : Theme.Card)) g.FillPath(b, path);
                using (var p = new Pen(hover || Checked ? Theme.Gold : Theme.GoldDim, 1.3f)) g.DrawPath(p, path);
            }
            if (Checked)
                using (var p = new Pen(Theme.Gold, 2f))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    g.DrawLines(p, new[] { new PointF(r.X + box * 0.22f, r.Y + box * 0.52f), new PointF(r.X + box * 0.42f, r.Y + box * 0.72f),
                                           new PointF(r.X + box * 0.78f, r.Y + box * 0.28f) });
                }
            var tr = new Rectangle(box + 8, 0, Width - box - 8, Height);
            TextRenderer.DrawText(g, Text, Font, tr, hover ? Theme.Hex("#fff2b0") : ForeColor,
                                  TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    // the slim gold scroll bar for a read-only text box (UI.Log), in place of Windows' own wide white one
    public class LogBar : Control
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct POINT { public int X, Y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref POINT p);
        const int EM_GETSCROLLPOS = 0x04DD, EM_SETSCROLLPOS = 0x04DE, Wide = 8;

        readonly RichTextBox box;
        bool hover, dragging;
        int dragOffset;

        public static void Attach(RichTextBox r)
        {
            var bar = new LogBar(r);
            r.ParentChanged += delegate
            {
                if (r.Parent == null) return;
                if (r.Dock == DockStyle.Fill)
                {   // the box fills its parent: keep a gutter on the right for the bar
                    r.Parent.Padding = new Padding(r.Parent.Padding.Left, r.Parent.Padding.Top, Wide + 2, r.Parent.Padding.Bottom);
                }
                else if (!bar.shrunk) { bar.shrunk = true; r.Width -= Wide + 2; }
                r.Parent.Controls.Add(bar);
                bar.BackColor = r.BackColor;
                bar.Place();
                bar.BringToFront();
            };
            r.Resize += delegate { bar.Place(); bar.Invalidate(); };
            r.LocationChanged += delegate { bar.Place(); };
            r.VScroll += delegate { bar.Invalidate(); };
            r.TextChanged += delegate { bar.Invalidate(); };
            r.MouseWheel += delegate { bar.Invalidate(); };
            r.VisibleChanged += delegate { bar.Visible = r.Visible; };
        }

        bool shrunk;

        LogBar(RichTextBox r)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            box = r;
            Width = Wide;
        }

        void Place()
        {
            if (Parent == null) return;
            if (box.Dock == DockStyle.Fill) Bounds = new Rectangle(Parent.ClientSize.Width - Wide - 1, 2, Wide, Parent.ClientSize.Height - 4);
            else Bounds = new Rectangle(box.Right + 2, box.Top + 2, Wide, Math.Max(10, box.Height - 4));
            Anchor = box.Dock == DockStyle.Fill ? AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right : box.Anchor;
        }

        int Pos { get { var p = new POINT(); try { SendMessage(box.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref p); } catch (Exception) { } return p.Y; } }
        int Content
        {
            get
            {
                if (box.TextLength == 0) return 1;
                try { return Math.Max(1, box.GetPositionFromCharIndex(box.TextLength - 1).Y + Pos + box.Font.Height + 6); }
                catch (Exception) { return 1; }
            }
        }

        Rectangle Thumb()
        {
            int content = Content, view = Math.Max(1, box.ClientSize.Height);
            if (content <= view) return Rectangle.Empty;
            int h = Math.Max(26, Height * view / content);
            int y = (int)((long)(Height - h) * Math.Min(Pos, content - view) / Math.Max(1, content - view));
            return new Rectangle(1, y, Width - 2, h);
        }

        void ScrollTo(int thumbTop)
        {
            Rectangle t = Thumb();
            if (t.IsEmpty) return;
            int content = Content, view = box.ClientSize.Height;
            var p = new POINT();
            p.Y = (int)((long)Math.Max(0, Math.Min(Height - t.Height, thumbTop)) * (content - view) / Math.Max(1, Height - t.Height));
            try { SendMessage(box.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref p); } catch (Exception) { }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Rectangle t = Thumb();
            if (t.IsEmpty) return;
            dragging = true;
            dragOffset = t.Contains(e.Location) ? e.Y - t.Y : t.Height / 2;
            ScrollTo(e.Y - dragOffset);
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) ScrollTo(e.Y - dragOffset); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            Rectangle t = Thumb();
            if (t.IsEmpty) return;
            using (var b = new SolidBrush(Theme.Mix(BackColor, Theme.CardEdge, 0.6))) g.FillRectangle(b, 2, 0, Width - 4, Height);
            using (var b = new SolidBrush(hover || dragging ? Theme.Gold : Theme.GoldDim))
                g.FillRectangle(b, t.X, t.Y, t.Width, t.Height);
        }
    }

    // the launcher art (assets/retro, drawn by tools/make_launcher_art.py); layout after the WoW Retro Launcher (Parquelle)
    public static class Retro
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();
        public static Image Get(string name)
        {
            Image img;
            if (!cache.TryGetValue(name, out img)) cache[name] = img = Theme.LoadImage("AS.retro." + name + ".png");
            return img;
        }
        public static Image[] Play { get { return new[] { Get("play"), Get("play_hover"), Get("play_down"), Get("play_off") }; } }
        public static Image[] Button { get { return new[] { Get("btn"), Get("btn_hover"), Get("btn_down"), Get("btn") }; } }
        public static Image[] Long { get { return new[] { Get("long"), Get("long_hover"), Get("long_down"), Get("long") }; } }
        public static readonly Color Gold = Color.FromArgb(238, 190, 83);
    }

    // Small square bronze button with a drawn icon (the section window's side strip).
    public class IconButton : Control
    {
        readonly Action<Graphics, float, float, Color> icon;
        bool hover, active, grayed;
        public event Action Pressed;
        public string GrayedTip;

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

        public bool Grayed
        {
            get { return grayed; }
            set
            {
                if (grayed == value) return;
                grayed = value;
                Cursor = grayed ? Cursors.Default : Cursors.Hand;
                if (!grayed) WowButton.Tips.Hide(this);
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            WowButton.Tips.SetToolTip(this, grayed && !string.IsNullOrEmpty(GrayedTip) ? Lang.T(GrayedTip) : null);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!grayed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) WowButton.Fire(Pressed);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int s = Width;
            bool lit = !grayed && (active || hover);
            g.Clear(Theme.Hex("#0e0d14"));
            Theme.Smooth(g);
            // modern: a soft rounded tile, a gold bar on the left for the open section, a gentle lift on hover
            var tile = new RectangleF(2, 2, s - 4, s - 4);
            Color fill = grayed ? Theme.Hex("#121118") : active ? Theme.Hex("#2a2636") : hover ? Theme.Hex("#221f2c") : Theme.Hex("#17161f");
            Armory.Box(g, tile, 7, fill, active ? Theme.Hex("#6b5a32") : hover ? Theme.Hex("#3d3a4a") : Theme.Hex("#26242f"));
            if (active)
                using (var b = new SolidBrush(Theme.Gold)) g.FillRectangle(b, 0, 9, 3, s - 18);
            icon(g, s / 2f, s / 2f, active ? Theme.Gold : hover ? Theme.Hex("#f5e9c8") : grayed ? Theme.Hex("#55524c") : Theme.Hex("#a99c80"));
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

        // an open book (the guide)
        public static void Book(Graphics g, float x, float y, Color c)
        {
            using (var p = new Pen(c, 2))
            {
                g.DrawLine(p, x, y - 7, x, y + 9);
                g.DrawLines(p, new[] { new PointF(x, y - 7), new PointF(x - 4, y - 9), new PointF(x - 11, y - 8),
                                       new PointF(x - 11, y + 7), new PointF(x - 4, y + 6), new PointF(x, y + 9) });
                g.DrawLines(p, new[] { new PointF(x, y - 7), new PointF(x + 4, y - 9), new PointF(x + 11, y - 8),
                                       new PointF(x + 11, y + 7), new PointF(x + 4, y + 6), new PointF(x, y + 9) });
            }
        }

        // a trophy cup, for the STATS section
        public static void Trophy(Graphics g, float x, float y, Color c)
        {
            using (var p = new Pen(c, 2))
            {
                g.DrawLines(p, new[] { new PointF(x - 7, y - 9), new PointF(x + 7, y - 9), new PointF(x + 6, y - 1),
                                       new PointF(x + 2, y + 2), new PointF(x - 2, y + 2), new PointF(x - 6, y - 1),
                                       new PointF(x - 7, y - 9) });
                g.DrawArc(p, x - 12, y - 8, 7, 7, 90, 180);      // the handles
                g.DrawArc(p, x + 5, y - 8, 7, 7, 270, 180);
                g.DrawLine(p, x, y + 2, x, y + 6);               // stem and base
                g.DrawLine(p, x - 6, y + 8, x + 6, y + 8);
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
        public static Label Text(string text, float size, FontStyle style, Color fg, Color bg, int wrapWidth, bool translate = true)
        {
            var l = new Label();
            l.Text = translate ? Lang.T(text) : text;
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

        public static CheckBox Check(string text, bool value, Color fg, Color bg, float size, bool translate = true)
        {
            var c = new NightCheck();
            c.Text = translate ? Lang.T(text) : text;
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
            r.ScrollBars = RichTextBoxScrollBars.None;       // the slim gold bar instead (LogBar), added with the box
            r.WordWrap = true;
            LogBar.Attach(r);
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
        public event Action Scrolled;                         // any change of the scroll position (wheel, bar, code)

        void Moved() { if (Scrolled != null) Scrolled(); }

        // HideBars: Windows' own scroll bars are never drawn (a ThinScrollBar shows the position instead); the
        // panel still scrolls the same way. The styles are taken off each time Windows sizes the window frame.
        public bool HideBars;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int index);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        protected override void WndProc(ref Message m)
        {
            if (HideBars && m.Msg == 0x83)                    // WM_NCCALCSIZE
            {
                const int GWL_STYLE = -16, WS_VSCROLL = 0x00200000, WS_HSCROLL = 0x00100000;
                int style = GetWindowLong(Handle, GWL_STYLE);
                if ((style & (WS_VSCROLL | WS_HSCROLL)) != 0) SetWindowLong(Handle, GWL_STYLE, style & ~(WS_VSCROLL | WS_HSCROLL));
            }
            base.WndProc(ref m);
        }

        protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Moved(); }
        protected override void OnLayout(LayoutEventArgs levent) { base.OnLayout(levent); Moved(); }

        // scrolls to y (0 = top), within the content
        public void ScrollTo(int y)
        {
            int max = Math.Max(0, DisplayRectangle.Height - ClientSize.Height);
            AutoScrollPosition = new Point(0, Math.Max(0, Math.Min(max, y)));
            Moved();
        }

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
            Moved();
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
            Moved();
        }
    }

    // A slim painted scroll bar for a ScrollPanel whose own (white Windows) bar is hidden: a dark track with a
    // rounded thumb; drag the thumb, click the track to page, the wheel works over it too.
    public class ThinScrollBar : Control, IWheel
    {
        readonly ScrollPanel target;
        readonly Color track, thumb, thumbHot;
        bool hover, dragging;
        int dragOffset;

        public ThinScrollBar(ScrollPanel target, Color track, Color thumb, Color thumbHot)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            this.target = target;
            this.track = track;
            this.thumb = thumb;
            this.thumbHot = thumbHot;
            BackColor = track;
            target.Scrolled += delegate { Invalidate(); };
        }

        int Content { get { return Math.Max(1, target.DisplayRectangle.Height); } }
        int View { get { return Math.Max(1, target.ClientSize.Height); } }
        int Pos { get { return -target.AutoScrollPosition.Y; } }

        Rectangle Thumb()
        {
            if (Content <= View) return Rectangle.Empty;
            int h = Math.Max(30, Height * View / Content);
            int y = (int)((long)(Height - h) * Pos / Math.Max(1, Content - View));
            return new Rectangle(1, y, Width - 2, h);
        }

        public void Wheel(int delta) { target.Wheel(delta); }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Rectangle t = Thumb();
            if (t.IsEmpty) return;
            if (t.Contains(e.Location)) { dragging = true; dragOffset = e.Y - t.Y; Capture = true; }
            else target.ScrollTo(Pos + (e.Y < t.Y ? -View : View) * 9 / 10);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            Rectangle t = Thumb();
            int room = Math.Max(1, Height - t.Height);
            target.ScrollTo((int)((long)(e.Y - dragOffset) * (Content - View) / room));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            target.Wheel(e.Delta);
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(track);
            Rectangle t = Thumb();
            if (t.IsEmpty) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = Math.Min(t.Width, 8) / 2f;
            using (var path = new GraphicsPath())
            {
                path.AddArc(t.X, t.Y, 2 * r, 2 * r, 180, 180);
                path.AddArc(t.X, t.Bottom - 2 * r, 2 * r, 2 * r, 0, 180);
                path.CloseFigure();
                using (var b = new SolidBrush(hover || dragging ? thumbHot : thumb)) g.FillPath(b, path);
            }
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
            Text = Lang.T(title);
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
