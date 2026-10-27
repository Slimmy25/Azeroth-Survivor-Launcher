// The section window: a WoW-whisper-style box that opens to the right of the launcher, with Cloud Save,
// Addons (and Download More), Options, Changelog, Guide, Stats (StatsPanel.cs) and About Launcher.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    // the bronze title bar: round emblem portrait, the section's name, a red X
    // The section window's title bar: the back arrow, then the trail of where you are ("Guide › Theorycrafting ›
    // Talent Tree Builder › Priest"): click a step to go back to it. The red X closes the section.
    public class SideHead : Control
    {
        public Action CloseClicked;
        public Action BackClicked;                           // null: no back arrow
        public Form Owner2;
        bool closable = true;
        public bool Closable { get { return closable; } set { closable = value; Invalidate(); } }   // no X on Community
        public Action<Point> Moved;
        Point? dragOffset;
        List<KeyValuePair<string, Action>> crumbs = new List<KeyValuePair<string, Action>>();
        readonly List<KeyValuePair<Rectangle, Action>> hot = new List<KeyValuePair<Rectangle, Action>>();
        // a step's drop-down (the other choices at that level), and where its arrow was drawn
        readonly Dictionary<int, List<KeyValuePair<string, Action>>> menus = new Dictionary<int, List<KeyValuePair<string, Action>>>();
        readonly List<KeyValuePair<Rectangle, int>> arrows = new List<KeyValuePair<Rectangle, int>>();
        int hoverArrow = -1;
        int hover = -1;                                      // the step under the pointer (-2: the back arrow)

        public SideHead()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 38;                                     // compact: the sections have little room
            BackColor = Theme.Hex("#0e0d14");
        }

        public string Title
        {
            get { return crumbs.Count > 0 ? crumbs[crumbs.Count - 1].Key : ""; }
            set { crumbs = new List<KeyValuePair<string, Action>> { new KeyValuePair<string, Action>(value, null) }; Invalidate(); }
        }

        // the trail; the last step is where you are (its action is not used)
        public void SetCrumbs(List<KeyValuePair<string, Action>> list)
        {
            crumbs = list ?? new List<KeyValuePair<string, Action>>();
            menus.Clear();
            hover = -1;
            Invalidate();
        }

        // the choices of step i (not the first one): a small arrow beside it opens them
        public void SetMenu(int i, List<KeyValuePair<string, Action>> items)
        {
            if (i <= 0 || items == null || items.Count == 0) return;
            menus[i] = items;
            Invalidate();
        }

        Rectangle CloseBox { get { return closable ? new Rectangle(Width - 27, 9, 18, 18) : Rectangle.Empty; } }
        Rectangle BackBox { get { return BackClicked != null ? new Rectangle(44, 8, 30, 20) : Rectangle.Empty; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int w = Math.Max(Width, 200);
            Image bar = Retro.Get("long");                  // the launcher's blue stone, framed in gold
            if (bar != null) Theme.Draw(g, bar, new Rectangle(22, 7, w - 26, 22));
            using (var p = new Pen(Theme.Hex("#c49a48"), 2)) g.DrawRectangle(p, 22, 7, w - 26, 21);
            Theme.Smooth(g);
            hot.Clear();
            arrows.Clear();
            menuText.Clear();
            stepRects.Clear();
            int x = 50;
            Rectangle bb = BackBox;
            if (!bb.IsEmpty)
            {
                using (var b = new SolidBrush(hover == -2 ? Theme.Hex("#3a2c18") : Theme.Hex("#1c160e"))) g.FillRectangle(b, bb);
                using (var p = new Pen(Theme.Hex("#d6a640"), 1.5f)) g.DrawRectangle(p, bb);
                using (var b = new SolidBrush(Theme.Hex("#fff0b0")))
                    g.FillPolygon(b, new[] { new PointF(bb.X + 11, bb.Y + 10), new PointF(bb.X + 19, bb.Y + 4), new PointF(bb.X + 19, bb.Y + 16) });
                x = bb.Right + 8;
            }
            int right = (closable ? Width - 32 : Width - 8);
            Font f = Theme.F(11, FontStyle.Bold), fs = Theme.F(10);
            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                // from the end: the steps that fit; the first ones go when the trail is too long
                var widths = new List<float>();
                float arrowW = 14;
                for (int i = 0; i < crumbs.Count; i++)
                    widths.Add(g.MeasureString(Lang.T(crumbs[i].Key), i == crumbs.Count - 1 ? f : fs).Width + (menus.ContainsKey(i) ? arrowW : 0));
                float sep = g.MeasureString(" › ", fs).Width;
                int first = 0;
                float total = 0;
                for (int i = 0; i < crumbs.Count; i++) total += widths[i] + (i > 0 ? sep : 0);
                while (first < crumbs.Count - 1 && x + total > right) { total -= widths[first] + sep; first++; }
                for (int i = first; i < crumbs.Count; i++)
                {
                    bool last = i == crumbs.Count - 1;
                    string t = Lang.T(crumbs[i].Key);
                    float tw = Math.Min(widths[i], right - x);
                    bool menu = menus.ContainsKey(i);
                    var r = new RectangleF(x, 7, Math.Max(1, tw - (menu ? arrowW : 0)), 22);
                    Color c = last ? Theme.Hex("#fff0b0") : (hover == i ? Color.White : Theme.Hex("#d9c48a"));
                    using (var b = new SolidBrush(c)) g.DrawString(t, last ? f : fs, b, r, sf);
                    if (menu)
                    {
                        // the drop-down arrow: the other choices at this step
                        var ar = new Rectangle((int)r.Right + 1, 11, (int)arrowW - 2, 14);
                        if (hoverArrow == i) using (var b = new SolidBrush(Theme.Hex("#3a2c18"))) g.FillRectangle(b, ar);
                        using (var b = new SolidBrush(hoverArrow == i ? Color.White : Theme.Hex("#d9c48a")))
                            g.FillPolygon(b, new[] { new PointF(ar.X + 2, ar.Y + 5), new PointF(ar.Right - 2, ar.Y + 5), new PointF(ar.X + ar.Width / 2f, ar.Y + 10) });
                        arrows.Add(new KeyValuePair<Rectangle, int>(ar, i));
                        if (last || crumbs[i].Value == null) menuText.Add(new KeyValuePair<Rectangle, int>(Rectangle.Round(r), i));
                        stepRects.Add(new KeyValuePair<Rectangle, int>(Rectangle.Union(Rectangle.Round(r), ar), i));
                    }
                    if (!last && crumbs[i].Value != null)
                    {
                        hot.Add(new KeyValuePair<Rectangle, Action>(Rectangle.Round(r), crumbs[i].Value));
                        if (hover == i)
                            using (var p = new Pen(Theme.Hex("#d9c48a"))) g.DrawLine(p, r.X, r.Bottom - 4, r.X + tw, r.Bottom - 4);
                    }
                    x += (int)tw;
                    if (!last)
                    {
                        using (var b = new SolidBrush(Theme.Hex("#a08850"))) g.DrawString(" › ", fs, b, new RectangleF(x, 7, sep + 2, 22), sf);
                        x += (int)sep;
                    }
                    if (x >= right) break;
                }
            }
            if (Theme.Portrait != null) Theme.Draw(g, Theme.Portrait, new Rectangle(2, 1, 36, 36));
            Rectangle xb = CloseBox;
            if (xb.IsEmpty) return;
            using (var b = new SolidBrush(Theme.Hex("#2a1c1c"))) g.FillRectangle(b, xb);
            using (var p = new Pen(Theme.Hex("#d6a640"), 2)) g.DrawRectangle(p, xb);
            using (var p = new Pen(Theme.Hex("#e53a22"), 3))
            {
                g.DrawLine(p, xb.X + 5, xb.Y + 5, xb.X + 13, xb.Y + 13);
                g.DrawLine(p, xb.X + 13, xb.Y + 5, xb.X + 5, xb.Y + 13);
            }
        }

        int HotAt(Point pt)
        {
            if (BackBox.Contains(pt)) return -2;
            for (int i = 0; i < crumbs.Count; i++)
                foreach (var h in hot)
                    if (h.Key.Contains(pt) && h.Value == crumbs[i].Value) return i;
            return -1;
        }

        // the name of a step with a drop-down and nothing else to do (the last step): a click opens its drop-down
        readonly List<KeyValuePair<Rectangle, int>> menuText = new List<KeyValuePair<Rectangle, int>>();
        int MenuTextAt(Point pt)
        {
            foreach (var a in menuText) if (a.Key.Contains(pt)) return a.Value;
            return -1;
        }

        // hovering a step that has a drop-down opens it (a click on its name still goes there); it closes when the
        // pointer leaves both the step and the list
        readonly List<KeyValuePair<Rectangle, int>> stepRects = new List<KeyValuePair<Rectangle, int>>();
        int hoverStep = -1;
        ContextMenuStrip openMenu;
        int openMenuStep = -1, quietStep = -1;              // quietStep: just closed, not again until the pointer leaves it
        Timer hoverTimer;

        int StepAt(Point pt)
        {
            foreach (var a in stepRects) if (a.Key.Contains(pt)) return a.Value;
            return -1;
        }

        Rectangle StepRect(int i)
        {
            foreach (var a in stepRects) if (a.Value == i) return a.Key;
            return Rectangle.Empty;
        }

        void HoverTick()
        {
            Point pt = PointToClient(Cursor.Position);
            int s = ClientRectangle.Contains(pt) ? StepAt(pt) : -1;
            if (openMenu != null && !openMenu.IsDisposed)
            {
                bool inMenu = openMenu.Visible && openMenu.Bounds.Contains(Cursor.Position);
                if (!inMenu && s != openMenuStep) openMenu.Close();
                return;
            }
            if (s >= 0 && s == hoverStep && s != quietStep) ShowMenu(s, StepRect(s));
        }

        int ArrowAt(Point pt)
        {
            foreach (var a in arrows) if (a.Key.Contains(pt)) return a.Value;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int st = StepAt(e.Location);
            if (st != quietStep) quietStep = -1;
            if (st != hoverStep)
            {
                hoverStep = st;
                if (hoverTimer == null)
                {
                    hoverTimer = new Timer();
                    hoverTimer.Interval = 250;
                    hoverTimer.Tick += delegate { HoverTick(); };
                }
                hoverTimer.Start();
            }
            int h = HotAt(e.Location), ha = ArrowAt(e.Location);
            if (ha < 0) ha = MenuTextAt(e.Location);          // the last step's name lights its arrow
            if (h != hover || ha != hoverArrow) { hover = h; hoverArrow = ha; Invalidate(); }
            Cursor = CloseBox.Contains(e.Location) || h != -1 || ha != -1 ? Cursors.Hand : Cursors.Default;
            if (dragOffset.HasValue && Owner2 != null)
            {
                Point s = PointToScreen(e.Location);
                Owner2.Location = new Point(s.X - dragOffset.Value.X, s.Y - dragOffset.Value.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { if (hover != -1 || hoverArrow != -1) { hover = -1; hoverArrow = -1; Invalidate(); } base.OnMouseLeave(e); }

        void ShowMenu(int i, Rectangle at)
        {
            List<KeyValuePair<string, Action>> items;
            if (!menus.TryGetValue(i, out items)) return;
            if (openMenu != null && !openMenu.IsDisposed) { if (openMenuStep == i) return; openMenu.Close(); }
            var m = new ContextMenuStrip();
            openMenu = m;
            openMenuStep = i;
            m.ShowImageMargin = false;
            m.BackColor = Theme.Hex("#16151e");
            m.ForeColor = Theme.Hex("#f5e9c8");
            m.Font = Theme.F(9.5f);
            foreach (var kv in items)
            {
                Action act = kv.Value;
                var it = new ToolStripMenuItem(Lang.T(kv.Key));
                it.ForeColor = kv.Key == crumbs[i].Key ? Theme.Hex("#f0c050") : Theme.Hex("#f5e9c8");   // where you are now
                it.Click += delegate { WowButton.Fire(act); };
                m.Items.Add(it);
            }
            m.Closed += delegate { if (openMenu == m) { quietStep = openMenuStep; openMenu = null; openMenuStep = -1; } BeginInvoke(new Action(m.Dispose)); };
            m.Show(this, new Point(at.X - 6, Height - 4));
        }

        // the list of a step, opened by a click: at the same place as when hovered
        void ClickMenu(int i)
        {
            Rectangle at = StepRect(i);
            if (at.IsEmpty) foreach (var a in arrows) if (a.Value == i) at = a.Key;
            if (!at.IsEmpty) ShowMenu(i, at);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !CloseBox.Contains(e.Location) && Owner2 != null)
            {
                Point s = PointToScreen(e.Location);
                dragOffset = new Point(s.X - Owner2.Left, s.Y - Owner2.Top);   // drag the title bar to move the window
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragOffset.HasValue)
            {
                dragOffset = null;
                Capture = false;
                if (Moved != null && Owner2 != null) Moved(Owner2.Location);
            }
            else if (e.Button == MouseButtons.Left && CloseBox.Contains(e.Location)) WowButton.Fire(CloseClicked);
            else if (e.Button == MouseButtons.Left && ArrowAt(e.Location) >= 0) ClickMenu(ArrowAt(e.Location));
            else if (e.Button == MouseButtons.Left)
            {
                // a click anywhere on a step's word opens its list where the hover list opens (a step without a list
                // goes to its page)
                int h = HotAt(e.Location);
                if (h == -2) WowButton.Fire(BackClicked);
                else if (h >= 0 && menus.ContainsKey(h)) ClickMenu(h);
                else if (h >= 0) WowButton.Fire(crumbs[h].Value);
                else
                {
                    int m = MenuTextAt(e.Location);
                    if (m >= 0) ClickMenu(m);
                }
            }
            base.OnMouseUp(e);
        }
    }

    // a panel with a thin coloured border around its content
    public class Bordered : Panel
    {
        public Bordered(Color border, int width)
        {
            BackColor = border;
            Padding = new Padding(width);
        }
    }

    // the All Patch Notes list: the mouse wheel flips the page
    public class WheelPanel : Panel, IWheel
    {
        public Action<int> OnWheel;
        public void Wheel(int delta) { if (OnWheel != null) OnWheel(delta); }
    }

    public partial class LauncherApp
    {
        const int SideW = 680, SideH = 600;
        const int ClNewest = -1, ClList = -2;                    // changelog views; 0.. = one older post

        Form side;
        SideHead sideHead;
        Panel panel;
        Panel strip;
        readonly Dictionary<string, IconButton> stripBtns = new Dictionary<string, IconButton>();
        IconButton homeBtn;
        string current;
        string addonMode = "installed";
        int addonCategory;
        int clView = ClNewest, clPage;
        bool clToEnd;
        DateTime clLastStep = DateTime.MinValue;
        Dictionary<string, CheckBox> addonRows;
        Label addonCount;

        static readonly string[] SectionNames = { "Guide", "Cloud Save", "Addons", "Changelog", "Stats", "Options", "About Launcher" };
        int gTopic = -1, gPage = -1;
        string gTalents;                                     // the Guide shows this class's talent trees (a test build)
        TalentBuild liveBuild;                               // the test build on those trees: kept only while they show
        string liveBuildCls;
        GuideVideo gVideo;                                   // the guide: a video playing inside the section (Guide.cs)                         // the guide: -1 = the topic list / the topic itself

        int PW { get { return panel.ClientSize.Width; } }
        int PH { get { return panel.ClientSize.Height; } }

        // ------------------------------------------------------------------ the window itself
        void BuildSide()
        {
            Color bronzeD = Theme.Hex("#2a2620"), dark = Theme.Hex("#0e0d14");   // night stone, like the launcher
            side = new Form();
            side.FormBorderStyle = FormBorderStyle.None;
            side.ShowInTaskbar = false;                      // the sections open inside the launcher, on its left side
            side.TopLevel = false;
            side.StartPosition = FormStartPosition.Manual;
            side.BackColor = bronzeD;
            side.Icon = Theme.AppIcon;
            side.Text = "Addons";
            side.Padding = new Padding(3);
            side.Size = new Size(SideW, SideH);
            side.KeyPreview = true;
            side.FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (!quitting && e.CloseReason == CloseReason.UserClosing)     // Alt+F4 / taskbar close = the red X
                {
                    e.Cancel = true;
                    CloseSide();
                }
            };
            var frame = new Bordered(Theme.Hex("#c49a48"), 2);
            frame.Dock = DockStyle.Fill;
            side.Controls.Add(frame);
            var inner = new Panel();
            inner.Dock = DockStyle.Fill;
            inner.BackColor = dark;
            frame.Controls.Add(inner);

            var body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = dark;
            body.Padding = new Padding(4, 2, 4, 4);
            inner.Controls.Add(body);
            sideHead = new SideHead();
            sideHead.Dock = DockStyle.Top;
            // (no Owner2: the section area does not move)
            sideHead.CloseClicked = CloseSide;
            sideHead.Moved = delegate(Point p)
            {
                Cfg.Set("side_pos", new List<object> { (long)p.X, (long)p.Y });
                Cfg.Save();
            };
            inner.Controls.Add(sideHead);

            // right: strip of small icon buttons (jump to another section)
            strip = new Panel();
            strip.Dock = DockStyle.Right;
            strip.Width = 32;
            strip.BackColor = dark;
            body.Controls.Add(strip);
            homeBtn = new IconButton(Icons.Home, delegate { current = null; ShowSection("Community"); });   // home: Community
            strip.Controls.Add(homeBtn);
            var icons = new Action<Graphics, float, float, Color>[] { Icons.Book, Icons.Cloud, Icons.AddonsIcon, Icons.Scroll, Icons.Trophy, Icons.Gear, Icons.Info };
            for (int i = 0; i < SectionNames.Length; i++)
            {
                string n = SectionNames[i];
                var b = new IconButton(icons[i], delegate { if (n != current) ShowSection(n); });
                stripBtns[n] = b;
                strip.Controls.Add(b);
            }
            // left: the chat-style area that holds the section
            var chat = new Bordered(Theme.Hex("#8a7040"), 2);
            chat.Dock = DockStyle.Fill;
            body.Controls.Add(chat);
            chat.BringToFront();
            panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Theme.Panel;
            chat.Controls.Add(panel);
            LayoutStrip();
            Controls.Add(side);
            PlaceSide();
            side.PerformLayout();
        }

        void LayoutStrip()
        {
            homeBtn.Location = new Point(6, 0);
            int y = 26 + 10;
            foreach (string n in SectionNames)
            {
                var b = stripBtns[n];
                bool show = NavShown(n);
                b.Visible = show;
                if (!show) continue;
                b.Location = new Point(6, y);
                y += 26 + 5;
            }
        }

        void RefreshStrip()
        {
            foreach (var kv in stripBtns) kv.Value.Active = kv.Key == current;
        }

        void SetSideTitle(string text)
        {
            sideHead.Closable = current != "Community";
            sideHead.Title = text;
            side.Text = Lang.T(Array.IndexOf(SectionNames, current) >= 0 ? current : text);    // the taskbar and Alt+Tab name
        }

        // the open section fills the left side of the launcher window
        void PlaceSide()
        {
            side.Bounds = SideRect;                          // as tall as the News column: from "News" to the last Launcher button
            side.BringToFront();
        }

        // the section area: its top level with the top of the word "News", its bottom level with the Launcher buttons
        // (a little taller than the News column: from under the game's name down to the bottom bar, for more room)
        static readonly Rectangle SideRect = new Rectangle(6, 198, 674, 574 - 198);

        void ShowSide()
        {
            PlaceSide();
            side.Show();
            side.BringToFront();
        }

        void HideSide()
        {
            StopEdge();
            communityPlaying = null;
            if (side.Visible) side.Hide();
        }

        // closing a section goes back to the home screen, which shows Community; Community itself cannot be closed
        void CloseSide()
        {
            if (current == "Community") return;              // the home screen's Community can only be replaced, never closed
            if (current == "Setup") return;                  // setup cannot be closed before it is done (or cancelled)
            current = "Home";
            foreach (var b in nav.Values) b.Active = false;
            RefreshStrip();
            ShowHomeCommunity();
        }

        public void ShowSection(string name)
        {
            if (setupOpen && name != "Setup") { SetStatus("Finish the first-time setup first."); return; }   // setup stays up until done
            if (name == "Cloud Save") { optionsTab = 1; optionsLang = false; name = "Options"; }   // it lives in Options > CLOUD SAVE
            // everything works without the game except Cloud Save and Addons, which are grayed out until it is installed
            if ((name == "Cloud Save" || name == "Addons") && !GateOpen())
            {
                SetStatus(name == "Addons" ? AddonsNeedGame : CloudNeedsGame);
                ShowWindowFront();
                return;
            }
            if (name == "Stats" && !StatsAddonActive())     // the Stats page needs AZS Stats on
            {
                SetStatus(StatsNeedAddon);
                if (current == "Stats") { current = null; ShowSection("Community"); }
                return;
            }
            if (name == "Guide" && current == "Guide" && side.Visible)   // the Guide's button again: back to its main page
            {
                guideGroupPage = -1;
                gTopic = -1; gPage = -1; guideQuery = ""; gBuilder = false; gPlanner = false; gTalents = null; gVideo = null; guideMain = true;
                BuildPanel("Guide");
                return;
            }
            if (name == current && name != "Home" && side.Visible) return;   // already open: clicking its button again does nothing
            current = name;
            foreach (var kv in nav) kv.Value.Active = kv.Key == name;
            RefreshStrip();
            if (name == "Changelog") clView = Data.Changelog.Count > 1 ? 1 : ClNewest;   // News already shows the newest
            if (name == "Guide") { gTopic = -1; gPage = -1; guideQuery = ""; guideLastTopic = 0; gBuilder = false; gPlanner = false; gTalents = null; guideGroup = 0; guideMain = true; }   // the guide opens on its first topic
            if (name == "About Launcher") aboutLog = false;
            if (name == "Home")
            {
                current = null;
                ShowSection("Community");
                return;
            }
            ShowSide();
            BuildPanel(name);
        }

        // While a section is rebuilt, nothing in it is drawn: the old page stays on screen until the new one is complete,
        // then it is drawn at once (before, each new control showed up as an empty grey box for a moment)
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr region, uint flags);
        const int WM_SETREDRAW = 0x000B;
        int frozen;

        void Freeze(bool on)
        {
            if (panel == null || panel.IsDisposed || !panel.IsHandleCreated) return;
            if (on)
            {
                if (frozen++ == 0) SendMessage(panel.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            }
            else if (frozen > 0 && --frozen == 0)
            {
                SendMessage(panel.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                // erase, invalidate, every child, now
                RedrawWindow(panel.Handle, IntPtr.Zero, IntPtr.Zero, 0x0004 | 0x0001 | 0x0080 | 0x0100 | 0x0400);
            }
        }

        void BuildPanel(string name)
        {
            if (name != "Community") communityPlaying = null;   // another section: the Community video is left
            // the talent trees left (another page, class or section): their test build starts empty next time
            if (name != "Guide" || gTalents == null || gTalents != liveBuildCls) { liveBuild = null; liveBuildCls = null; }
            bool freeze = !PreviewMode && name != "Community";  // Community's player window is another program's
            if (freeze) Freeze(true);
            panel.SuspendLayout();
            Theme.Zoom = Theme.SectionZoom;                  // everything in a section is a little smaller
            try
            {
                if (name == "Cloud Save") PanelCloud();
                else if (name == "Addons") PanelAddons();
                else if (name == "Options") PanelOptions();
                else if (name == "Changelog") PanelChangelog();
                else if (name == "Guide") PanelGuide();
                else if (name == "Stats") PanelStats();
                else if (name == "About Launcher") PanelAbout();
                else if (name == "Community") PanelCommunity();
                else if (name == "Setup") PanelSetup();          // first-time setup (Setup.cs)
                RememberView(name);                              // the back arrow's way (History.cs)
            }
            finally
            {
                Theme.Zoom = 1f;
                panel.ResumeLayout();
                if (freeze) Freeze(false);
            }
            if (name == "Addons") RefreshGates();             // AZS Stats switched on or off: the Stats buttons follow
        }

        public void RebuildCurrent()
        {
            RefreshPlayLabel();
            Invalidate();
            if (current != null && current != "Home" && side.Visible) BuildPanel(current);
        }

        // ------------------------------------------------------------------ building blocks
        // Empties the section area; the title goes in the title bar. Every section gets the same back arrow.
        void ClearPanel(string title)
        {
            GuideHover.Hide();
            StopEdge();                                      // a video playing in Community (Community.cs) stops
            edgeHost = null;
            logBox = null;
            while (panel.Controls.Count > 0)
            {
                Control c = panel.Controls[0];
                panel.Controls.RemoveAt(0);
                c.Dispose();
            }
            SetSideTitle(title);
            // the back arrow and the trail live in the title bar: "Addons › Download More"
            bool back = current != "Community" || communityPlaying != null || communityMore;
            sideHead.BackClicked = back ? (Action)GoBack : null;
            var trail = new List<KeyValuePair<string, Action>>();
            string sec = current;
            if (sec != null && sec != "Home" && sec != title)
                trail.Add(new KeyValuePair<string, Action>(sec, delegate { SectionRoot(sec); }));
            trail.Add(new KeyValuePair<string, Action>(title, null));
            sideHead.SetCrumbs(trail);
        }

        // the drop-down of trail step i: name, action, name, action, ...
        void SetCrumbMenu(int i, List<KeyValuePair<string, Action>> items) { sideHead.SetMenu(i, items); }

        void SetCrumbs(params object[] steps)                // "Guide", action, "Theorycrafting", action, ..., "Priest", null
        {
            var trail = new List<KeyValuePair<string, Action>>();
            for (int i = 0; i + 1 < steps.Length; i += 2) trail.Add(new KeyValuePair<string, Action>((string)steps[i], steps[i + 1] as Action));
            sideHead.SetCrumbs(trail);
        }

        // a section's first page, from the trail in the title bar
        void SectionRoot(string name)
        {
            if (name == "Guide") { GuideMainPage(); return; }
            if (name == "Addons") addonMode = "installed";
            if (name == "Changelog") clView = ClNewest;
            if (name == "Community") { LeaveCommunityVideo(); communityMore = false; }
            BuildPanel(name);
        }

        // the back arrow: one step back inside the section, or out of the section when at its first page
        void GoBack()
        {
            if (current == "Setup") { SetupBack(); return; }
            if (LeaveCommunityVideo()) return;
            if (current == "Guide" && gVideo != null) { gVideo = null; BuildPanel("Guide"); return; }   // a video: back to its page
            if (BackInHistory()) return;                     // back along the way you came (History.cs)
            if (current == "Community" && communityMore) { communityMore = false; BuildPanel("Community"); return; }
            if (current == "Addons" && addonMode == "download") { SetAddonMode("installed"); return; }
            if (current == "Options" && optionsLang) { optionsLang = false; BuildPanel("Options"); return; }
            if (current == "Changelog")
            {
                if (clView >= 0) { SetChangelogView(ClList); return; }
                if (clView == ClList) { SetChangelogView(ClNewest); return; }
            }
            if (current == "Guide")
            {
                if (gVideo != null) { gVideo = null; BuildPanel("Guide"); return; }
                if (gTalents != null) { gTalents = null; BuildPanel("Guide"); return; }   // back to the builder's classes or the class page
                if (gPage >= 0) { SetGuideView(gTopic, -1); return; }
                if (!guideMain && guideQuery.Length == 0)          // a topic or the builder: back to the main page
                {
                    gTopic = -1; gTalents = null; gBuilder = false; gPlanner = false; guideMain = true;
                    BuildPanel("Guide");
                    return;
                }
                if (guideQuery.Length > 0) { guideQuery = ""; SetGuideView(guideLastTopic, -1); return; }
                if (guideGroupPage >= 0) { GuideMainPage(); return; }   // a group's page: back to every group
            }
            CloseSide();
        }

        Control putInto;                                     // where Put / PutText add (the section, or a scrolling page in it)

        T Put<T>(T c, int x, int y) where T : Control
        {
            c.Location = new Point(x, y);
            (putInto ?? panel).Controls.Add(c);
            c.BringToFront();
            return c;
        }

        Label PutText(string text, float size, FontStyle style, Color fg, int x, int y, int wrap, bool translate = true)
        {
            var l = UI.Text(text, size, style, fg, Theme.Panel, wrap, translate);
            l.Size = VStack.Measure(l);
            return Put(l, x, y);
        }

        // A scrollable area without Windows' own (white) scroll bar: a slim painted one in `thumb` colors runs in a
        // gutter on its right.
        ScrollPanel MakeThinScroll(Control parent, Rectangle r, Color bg, Color border, Color thumb, Color thumbHot)
        {
            var frame = new Bordered(border, 1);
            frame.Padding = new Padding(1, 1, 10, 1);        // a gutter on the right for the slim bar
            frame.Bounds = r;
            parent.Controls.Add(frame);
            frame.BringToFront();
            var gutter = new Panel();
            gutter.BackColor = bg;
            gutter.Bounds = new Rectangle(r.Width - 10, 1, 9, r.Height - 2);
            frame.Controls.Add(gutter);
            var sp = new ScrollPanel();
            sp.HideBars = true;
            sp.BackColor = bg;
            sp.Dock = DockStyle.Fill;
            frame.Controls.Add(sp);
            var bar = new ThinScrollBar(sp, Theme.Mix(bg, border, 0.3), thumb, thumbHot);
            bar.Bounds = new Rectangle(1, 3, 6, r.Height - 2 - 6);
            gutter.Controls.Add(bar);
            return sp;
        }

        // a scrollable area with a 1 px border; returns the scrolling panel
        ScrollPanel MakeScroll(Control parent, Rectangle r, Color bg, Color border)
        {   // every list and page uses the slim gold scroll bar, like the Guide and Stats
            return MakeThinScroll(parent, r, bg, Theme.CardEdge, Theme.GoldDim, Theme.Gold);
        }

        static int BottomOf(Control c) { return c.Top + c.Height; }

        // ------------------------------------------------------------------ Changelog
        // ------------------------------------------------------------------ Guide: see Guide.cs

        void PanelChangelog()
        {
            var entries = Data.Changelog;
            if (clView >= entries.Count) clView = ClList;
            string title, btn;
            int back;
            if (clView == ClNewest) { title = "Changelog"; btn = "ALL PATCH NOTES ›"; back = ClList; }
            else if (clView == ClList) { title = "All Patch Notes"; btn = "‹ LATEST"; back = ClNewest; }
            else { title = Data.PostTitle(entries[clView]); btn = "‹ ALL PATCH NOTES"; back = ClList; }
            ClearPanel(title);
            Put(new WowButton(btn, delegate { SetChangelogView(back); }, 190, 32, 10), PW - 20 - (int)(190 * Theme.Zoom), 8);
            int top = 8 + (int)(32 * Theme.Zoom) + 6, bottom = PH;
            if (clView >= 0 || clView == ClNewest)           // PREVIOUS (older) / NEXT (newer), always at the bottom
            {
                int v = clView == ClNewest ? 0 : clView;
                bottom = NavRow(entries.Count - v, entries.Count, v + 1 < entries.Count ? (Action)delegate { SetChangelogView(v + 1); } : null,
                                v > 0 ? (Action)delegate { SetChangelogView(v - 1); } : null);
            }
            if (clView == ClList)
            {
                PatchNotesList(entries, top);
                return;
            }
            var sp = MakeScroll(panel, new Rectangle(20, top, PW - 40, bottom - 14 - top), Theme.Panel, Theme.Bronze);
            sp.Overscroll = ChangelogStep;
            ChangelogBox(sp, entries[clView == ClNewest ? 0 : clView]);
            if (clToEnd)                                     // arrived by scrolling up: start at the bottom
            {
                clToEnd = false;
                sp.ScrollToEnd();
            }
        }

        void ChangelogBox(ScrollPanel sp, string text)
        {
            Color boxbg = Theme.Ink;
            string[] lines = text.Trim('\n').Split('\n');
            int w = sp.ClientSize.Width - 8;
            var box = new Bordered(Theme.CardEdge, 1);
            var inner = new Panel();
            inner.BackColor = boxbg;
            inner.Dock = DockStyle.Fill;
            box.Controls.Add(inner);
            int y = 10;
            var t = UI.Text(lines[0], 16, FontStyle.Bold, Theme.Gold, boxbg, w - 32, false);
            t.Size = VStack.Measure(t);
            t.Location = new Point(14, y);
            inner.Controls.Add(t);
            y += t.Height;
            int start = 1;
            if (lines.Length > 1 && System.Text.RegularExpressions.Regex.IsMatch(lines[1].Trim(), "^-{3,}$"))
            {
                var dash = UI.Text(lines[1].Trim(), 11, FontStyle.Regular, Theme.GoldDim, boxbg, w - 32, false);
                dash.Size = VStack.Measure(dash);
                dash.Location = new Point(14, y);
                inner.Controls.Add(dash);
                y += dash.Height;
                start = 2;
            }
            string body = string.Join("\n", lines, start, lines.Length - start).Trim('\n');
            if (body.Length > 0)
            {
                var b = UI.Text(body, 10, FontStyle.Regular, Theme.Text, boxbg, w - 32, false);
                b.Size = VStack.Measure(b);
                b.Location = new Point(14, y + 4);
                inner.Controls.Add(b);
                y += 4 + b.Height + 12;
            }
            else y += 10;
            box.Bounds = new Rectangle(4, 4, w, y + 4);
            sp.Controls.Add(box);
            var pad = new Panel();                           // a little room under the box
            pad.Bounds = new Rectangle(4, box.Bottom, 1, 8);
            pad.BackColor = sp.BackColor;
            sp.Controls.Add(pad);
        }

        // ‹ PREVIOUS · "3 of 40" · NEXT › pinned to the bottom of the section, whatever its size; returns its top
        int NavRow(int at, int count, Action previous, Action next)
        {
            int bh = (int)(34 * Theme.Zoom), by = PH - 10 - bh, bw = (int)(150 * Theme.Zoom);
            if (previous != null) Put(new WowButton("‹ PREVIOUS", previous, 150, 34, 10), 20, by);
            if (next != null) Put(new WowButton("NEXT ›", next, 150, 34, 10), PW - 20 - bw, by);
            var lbl = UI.Text(at + " of " + count, 10, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
            Size ls = VStack.Measure(lbl);
            lbl.Size = ls;
            Put(lbl, (PW - ls.Width) / 2, by + (bh - ls.Height) / 2);
            return by;
        }

        int clRows = Data.ClRows;                            // buttons per column that fit the section (set by the list)

        void PatchNotesList(List<string> entries, int top)
        {
            int rowH = (int)(48 * Theme.Zoom), bh = (int)(34 * Theme.Zoom);
            int areaBottom = PH - 10 - bh - 10;               // above the PREVIOUS / NEXT row
            clRows = Math.Max(1, (areaBottom - top - 4) / rowH);
            int per = clRows * 2;
            int pages = Math.Max(1, (entries.Count + per - 1) / per);
            int page = Math.Min(Math.Max(clPage, 0), pages - 1);
            clPage = page;
            NavRow(page + 1, pages, page > 0 ? (Action)delegate { SetClPage(page - 1); } : null,
                   page + 1 < pages ? (Action)delegate { SetClPage(page + 1); } : null);
            var area = new WheelPanel();
            area.BackColor = Theme.Panel;
            area.Bounds = new Rectangle(20, top, PW - 40, areaBottom - top);
            area.OnWheel = delegate(int delta) { ClPageWheel(delta, pages); };
            panel.Controls.Add(area);
            area.BringToFront();
            if (entries.Count == 0)
            {
                var l = UI.Text("No patch notes yet.", 11, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
                l.Location = new Point(6, 8);
                area.Controls.Add(l);
            }
            int colW = area.Width / 2;
            for (int k = 0, i = page * per; i < Math.Min(entries.Count, (page + 1) * per); k++, i++)
            {
                int col = k / clRows, rowN = k % clRows;     // fill the left column first, then the right
                int n = i;
                string version = Data.PostTitle(entries[i]) + (i == 0 ? " (current)" : "");
                var b = new WowButton(version, delegate { SetChangelogView(n); }, 230, 40, 14);
                b.Location = new Point(col * colW + 4, rowN * rowH + 4);
                area.Controls.Add(b);
            }
        }

        // wheel past the bottom (+1) opens the next older post, past the top (-1) the next newer one
        void ChangelogStep(int direction)
        {
            DateTime now = DateTime.Now;
            if ((now - clLastStep).TotalSeconds < 0.45) return;      // one post per flick of the wheel
            int cur = clView == ClNewest ? 0 : clView;
            int nw = cur + direction;
            if (nw < 0 || nw >= Data.Changelog.Count) return;
            clLastStep = now;
            clToEnd = direction < 0;
            SetChangelogView(nw == 0 ? ClNewest : nw);
        }

        void SetChangelogView(int view)
        {
            clView = view;
            if (view >= 0) clPage = view / (clRows * 2);         // coming back to the list shows this post's page
            BuildPanel("Changelog");
        }

        void SetClPage(int n)
        {
            clPage = n;
            BuildPanel("Changelog");
        }

        void ClPageWheel(int delta, int pages)
        {
            DateTime now = DateTime.Now;
            if ((now - clLastStep).TotalSeconds < 0.45) return;
            int nw = clPage + (delta < 0 ? 1 : -1);
            if (nw >= 0 && nw < pages)
            {
                clLastStep = now;
                SetClPage(nw);
            }
        }

        // ------------------------------------------------------------------ About Launcher
        // About Launcher: ABOUT shows the description, LAUNCHER CHANGELOG the launcher's own changelog
        bool aboutLog;
        // The launcher's own changelog is kept up to date in assets\launcher-changelog.txt but not shipped for now
        // (build.bat leaves it out). Set to true, and put its resource back in build.bat, to show it again.
        const bool ShowLauncherChangelog = false;

        void PanelAbout()
        {
            ClearPanel("About Launcher");
            if (ShowLauncherChangelog)
            {
                var about = Put(new WowButton("ABOUT", delegate { aboutLog = false; BuildPanel("About Launcher"); }, 130, 32, 10), 20, 8);
                about.Selected = !aboutLog;
                var log = Put(new WowButton("LAUNCHER CHANGELOG", delegate { aboutLog = true; BuildPanel("About Launcher"); }, 230, 32, 10),
                              20 + (int)(140 * Theme.Zoom), 8);
                log.Selected = aboutLog;
            }
            else aboutLog = false;
            var credits = UI.Text("Vibe coded by Slimjim\nCloud save code established by Keijin", 13, FontStyle.Italic,
                                  Theme.Credit, Theme.Panel, 0);
            credits.TextAlign = ContentAlignment.TopRight;
            credits.Size = VStack.Measure(credits);
            Put(credits, PW - 20 - credits.Width, ShowLauncherChangelog ? 8 + (int)(32 * Theme.Zoom) + 6 : 8);
            var line = Put(UI.Line(Theme.GoldDim, PW - 40, 2), 20, BottomOf(credits) + 4);
            var box = UI.Log(Theme.Panel, 11);
            box.Bounds = new Rectangle(20, BottomOf(line) + 10, PW - 34, PH - BottomOf(line) - 18);
            Put(box, box.Left, box.Top);
            // "# heading", "- bullet", anything else a paragraph (the texts are not translated)
            foreach (string raw in (aboutLog ? Data.LauncherChangelog : Data.About).Split('\n'))
            {
                string ln = raw.TrimEnd();
                box.SelectionStart = box.TextLength;
                if (ln.StartsWith("#"))
                {
                    box.SelectionIndent = 0;
                    box.SelectionHangingIndent = 0;
                    UI.Append(box, (box.TextLength > 0 ? "\n" : "") + ln.TrimStart('#', ' ').Trim() + "\n", Theme.Gold, Theme.F(14, FontStyle.Bold));
                }
                else if (ln.TrimStart().StartsWith("-") || ln.TrimStart().StartsWith("*"))
                {
                    box.SelectionIndent = 14;
                    box.SelectionHangingIndent = 14;
                    UI.Append(box, "• " + ln.TrimStart('-', '*', ' ').Trim() + "\n", Theme.Text, Theme.F(11));
                }
                else if (ln.Trim().Length > 0)
                {
                    box.SelectionIndent = 0;
                    box.SelectionHangingIndent = 0;
                    UI.Append(box, ln.Trim() + "\n\n", Theme.Text, Theme.F(11));
                }
            }
            box.SelectionStart = 0;
            box.ScrollToCaret();
        }

        // ------------------------------------------------------------------ importing a save (setup and Options)
        // asks for a .sav file and puts it in as the game's progress; returns its name, or null when nothing was done
        public string ImportSave(IWin32Window owner, string saveDir)
        {
            if (GameRunningFlag || Util.GameRunning()) { Msg.Show(owner, "Close the game first, then import the save.", "Import save"); return null; }
            string file;
            using (var d = new OpenFileDialog())
            {
                d.Title = Lang.T("Choose the save you exported from the website (.sav)");
                d.Filter = Lang.T("Azeroth Survivors save (*.sav)|*.sav|All files (*.*)|*.*");
                string dl = Path.Combine(Util.Home, "Downloads");
                if (Directory.Exists(dl)) d.InitialDirectory = dl;
                if (d.ShowDialog(owner) != DialogResult.OK) return null;
                file = d.FileName;
            }
            if (string.IsNullOrEmpty(saveDir)) saveDir = Util.DefaultSaveDir;
            try { SaveImport.Read(file); }
            catch (Exception e) { Msg.Show(owner, e.Message, "Import save", MessageBoxButtons.OK, MessageBoxIcon.Error); return null; }
            if (File.Exists(Path.Combine(saveDir, SaveImport.SaveFile)) &&
                Msg.Show(owner, "This PC already has progress in the game.\n\nReplace it with " + Path.GetFileName(file) +
                                "?\nWhat is there now is kept as a backup in the game's Saves folder.", "Import save",
                         MessageBoxButtons.YesNo) != DialogResult.Yes) return null;
            try { SaveImport.Import(file, saveDir); }
            catch (Exception e)
            {
                Msg.Show(owner, "The save could not be imported:\n\n" + e.Message, "Import save", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            Cfg.Set("save_imported", DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            Cfg.Save();
            return Path.GetFileName(file);
        }

        // ------------------------------------------------------------------ Addons
        string AddonsDir() { return SyncSettings().AddonsDir; }

        void PanelAddons()
        {
            bool dl = addonMode == "download";
            ClearPanel(dl ? "Download More" : "Addons");
            if (dl)                                          // Addons › Download More ▾ : its tabs
            {
                var l = new List<KeyValuePair<string, Action>>();
                for (int i = 0; i < Data.AddonCategories.Length; i++)
                {
                    int n = i;
                    l.Add(new KeyValuePair<string, Action>(Data.AddonCategories[i], delegate { addonCategory = n; BuildPanel("Addons"); }));
                }
                SetCrumbMenu(1, l);
            }
            if (dl)
            {
                // the categories, a row of tabs across the top (the back arrow in the title bar returns to My Addons)
                int x0 = 20, wAll = PW - 20 - x0, cw = wAll / Data.AddonCategories.Length;
                for (int i = 0; i < Data.AddonCategories.Length; i++)
                {
                    int n = i;
                    var b = new WowButton(Data.AddonCategories[i], delegate { addonCategory = n; BuildPanel("Addons"); },
                                          (int)((cw - 6) / Theme.Zoom), 32, 10);
                    b.Selected = i == addonCategory;          // the open category: filled gold
                    Put(b, x0 + i * cw, 8);
                }
                AddonsDownloadView(8 + (int)(32 * Theme.Zoom) + 6);
            }
            else AddonsInstalledView(8);
        }

        void SetAddonMode(string mode)
        {
            addonMode = mode;
            BuildPanel("Addons");
        }

        Dictionary<string, object> AddonInstalls() { return Cfg.Section("addon_installs"); }

        List<string> RecordedFolders(string name)
        {
            var l = Json.AsList(Json.Get(AddonInstalls(), name));
            if (l == null) return null;
            var r = new List<string>();
            foreach (object o in l) r.Add(Convert.ToString(o));
            return r;
        }

        List<string> AddonFoldersOf(string name)
        {
            foreach (var e in Data.Addons)
                if (e.Name == name && BuiltinAddons.Find(e.Url) != null)
                {
                    string at = BuiltinAddons.PathOf(AddonsDir(), BuiltinAddons.Find(e.Url));
                    return at == null ? new List<string>() : new List<string> { Path.GetFileName(at) };
                }
            return Addons.FoldersOf(AddonsDir(), name, RecordedFolders(name));
        }

        // ---- addon updates: when each Download More addon was installed (UTC), against when its copy on GitHub
        // was last uploaded (the release's asset dates, asked once a session). Newer on GitHub: UPDATE.
        Dictionary<string, object> AddonInstalledAt() { return Cfg.Section("addon_installed_at"); }
        static Dictionary<string, DateTime> mirrorDates;     // zip file name -> uploaded (UTC); null: not asked yet
        static bool mirrorAsking;

        void AskMirrorDates()
        {
            if (mirrorDates != null || mirrorAsking || PreviewMode) return;
            mirrorAsking = true;
            Bg(delegate
            {
                var dates = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string text;
                    using (var resp = Net.Open("https://api.github.com/repos/Slimmy25/Azeroth-Survivor-Launcher-Releases/releases/tags/addons",
                                               15000, true, "GitHub", "application/vnd.github+json"))
                    using (var rd = new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8))
                        text = rd.ReadToEnd();
                    foreach (object a in Json.AsList(Json.Get(Json.AsDict(Json.Parse(text)), "assets")) ?? new List<object>())
                    {
                        var d = Json.AsDict(a);
                        DateTime t;
                        if (DateTime.TryParse(Json.Str(d, "updated_at"), System.Globalization.CultureInfo.InvariantCulture,
                                              System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out t))
                            dates[Json.Str(d, "name")] = t;
                    }
                }
                catch (Exception) { }
                Ui(delegate
                {
                    mirrorDates = dates;
                    mirrorAsking = false;
                    if (current == "Addons" && addonMode == "download") BuildPanel("Addons");
                });
            });
        }

        // true when the copy on GitHub is newer than the one installed
        bool AddonHasUpdate(AddonEntry it)
        {
            if (mirrorDates == null || it.Category == Data.MyAddonsCategory || !Net.IsWebAddress(it.Url)) return false;
            string file = Uri.UnescapeDataString(it.Url.Substring(it.Url.LastIndexOf('/') + 1));
            DateTime up;
            if (!mirrorDates.TryGetValue(file, out up)) return false;
            DateTime installed = DateTime.MinValue;
            string rec = Json.Str(AddonInstalledAt(), it.Name);
            if (!DateTime.TryParse(rec, System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out installed))
            {
                // installed before the launcher kept the date: the newest file in its folders says when
                foreach (string f in AddonFoldersOf(it.Name))
                    try
                    {
                        string dir = Path.Combine(AddonsDir(), f);
                        foreach (string file2 in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        {
                            DateTime w = File.GetLastWriteTimeUtc(file2);
                            if (w > installed) installed = w;
                        }
                    }
                    catch (Exception) { }
                if (installed == DateTime.MinValue) return false;
            }
            return up > installed.AddMinutes(1);
        }

        void AddonsDownloadView(int top)
        {
            AskMirrorDates();                                // which addons have a newer copy on GitHub
            Color listbg = Theme.Ink;
            var sp = MakeScroll(panel, new Rectangle(20, top, PW - 40, PH - 12 - top), listbg, Theme.Bronze);
            int cat = Math.Min(Math.Max(addonCategory, 0), Data.AddonCategories.Length - 1);
            var items = new List<AddonEntry>();
            var all = new List<AddonEntry>(Data.Addons);
            foreach (var a in all)
                if (a.Category == Data.AddonCategories[cat]) items.Add(a);
            items.Sort(delegate(AddonEntry a, AddonEntry b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });   // A to Z
            int y0 = 4;
            if (Data.AddonCategories[cat] == Data.UiCategory)
            {
                var pick = UI.Text("Pick one: these interfaces can't be on together. Switching one on asks to switch the other off.",
                                   9, FontStyle.Italic, Theme.GoldDim, listbg, sp.ClientSize.Width - 24);
                pick.Size = VStack.Measure(pick);
                pick.Location = new Point(10, 6);
                sp.Controls.Add(pick);
                y0 = pick.Bottom + 6;
            }
            if (Data.AddonCategories[cat] == Data.MyAddonsCategory)
            {
                var hint = UI.Text("Put addon .zip files in the My Addons folder; they stay on this PC.", 9, FontStyle.Italic,
                                   Theme.GoldDim, listbg, sp.ClientSize.Width - 160);
                hint.Size = VStack.Measure(hint);
                hint.Location = new Point(10, 8);
                sp.Controls.Add(hint);
                var open = new WowButton("OPEN FOLDER", delegate { Directory.CreateDirectory(Data.MyAddonsDir); Util.OpenInShell(Data.MyAddonsDir); }, 130, 28, 9);
                open.Location = new Point(sp.ClientSize.Width - 4 - 140, 6);
                sp.Controls.Add(open);
                y0 = Math.Max(hint.Bottom, open.Bottom) + 8;
            }
            if (items.Count == 0)
            {
                var l = UI.Text("No downloadable addons listed yet.",
                                11, FontStyle.Italic, Theme.GoldDim, listbg, 0);
                l.Location = new Point(10, y0 + 4);
                sp.Controls.Add(l);
                return;
            }
            int w = sp.ClientSize.Width - 4 - 8;
            int y = y0;
            // every list is small blocks, four to a row
            AddonCards(sp, items, w, y, listbg);
        }

        // Download More (every category): a small block for each addon, four to a row: its name, what it does, and its button at the bottom
        readonly ToolTip addonTips = new ToolTip();

        void AddonCards(ScrollPanel sp, List<AddonEntry> items, int w, int y, Color listbg)
        {
            const int gap = 6, cols = 4;
            int cw = (w - gap * (cols - 1)) / cols, textW = cw - 2 - 14;
            int bw = (int)((cw - 2 - 16) / Theme.Zoom);       // the block's button runs along its bottom
            var heights = new List<int>();
            var built = new List<Control>();
            foreach (var item in items)
            {
                var it = item;
                var card = new Bordered(Theme.GoldDim, 1);
                var inner = new Panel();
                inner.BackColor = listbg;
                inner.Dock = DockStyle.Fill;
                card.Controls.Add(inner);
                bool haveIt = AddonFoldersOf(it.Name).Count > 0;
                bool update = haveIt && AddonHasUpdate(it);
                var btn = update ? new WowButton("UPDATE", delegate { DownloadAddon(it.Name, it.Url); }, bw, 30, 9)
                        : haveIt ? new WowButton("UNINSTALL", delegate { UninstallAddon(it.Name); }, bw, 30, 9)
                                 : new WowButton("INSTALL", delegate { DownloadAddon(it.Name, it.Url); }, bw, 30, 9);
                if (update) btn.Selected = true;
                if (btn.Width > cw - 14) btn.Width = cw - 14;  // never wider than its block
                int ty = 6;
                var name = UI.Text(it.Name, 9.5f, FontStyle.Bold, Theme.Gold, listbg, textW, false);
                name.Size = VStack.Measure(name);
                name.Location = new Point(7, ty);
                inner.Controls.Add(name);
                ty += name.Height + 1;
                if (it.Description.Length > 0)
                {
                    // a short line on the block; the whole description when the pointer rests on it
                    string shortDesc = Lang.T(it.Description);
                    int cut = shortDesc.IndexOfAny(new[] { ':', '.', '(' });
                    if (cut > 12) shortDesc = shortDesc.Substring(0, cut).Trim();
                    if (shortDesc.Length > 64) shortDesc = shortDesc.Substring(0, 62).TrimEnd() + "…";
                    var d = UI.Text(shortDesc, 7.5f, FontStyle.Regular, Theme.Text, listbg, textW, false);
                    addonTips.SetToolTip(d, Lang.T(it.Description));
                    addonTips.SetToolTip(inner, Lang.T(it.Description));
                    d.Size = VStack.Measure(d);
                    d.Location = new Point(7, ty);
                    inner.Controls.Add(d);
                    ty += d.Height;
                }
                if (haveIt)
                {
                    var on = UI.Text("Installed", 7.5f, FontStyle.Italic, Theme.GoldDim, listbg, 0);
                    on.Size = VStack.Measure(on);
                    inner.Controls.Add(on);
                    on.Tag = "installed";
                    ty += on.Height + 2;
                }
                inner.Controls.Add(btn);
                btn.Tag = "button";
                heights.Add(ty + 6 + btn.Height + 8);
                built.Add(card);
            }
            for (int r = 0; r * cols < built.Count; r++)
            {
                int h = 90;
                for (int i = r * cols; i < Math.Min(built.Count, r * cols + cols); i++) h = Math.Max(h, heights[i]);
                for (int i = r * cols; i < Math.Min(built.Count, r * cols + cols); i++)
                {
                    var card = built[i];
                    card.Bounds = new Rectangle(4 + (i % cols) * (cw + gap), y, cw, h);
                    var inner = card.Controls[0];
                    Control b = null;
                    foreach (Control c in inner.Controls)
                        if ((c.Tag as string) == "button") { c.Location = new Point((cw - 2 - c.Width) / 2, h - 2 - 8 - c.Height); b = c; }
                    foreach (Control c in inner.Controls)
                        if ((c.Tag as string) == "installed" && b != null) c.Location = new Point(7, b.Top - c.Height - 2);
                    sp.Controls.Add(card);
                }
                y += h + gap;
            }
            var pad = new Panel();
            pad.BackColor = listbg;
            pad.Bounds = new Rectangle(4, y, 1, 4);
            sp.Controls.Add(pad);
        }

        bool AddonsLocked()
        {
            if (GameRunningFlag || SyncBusy)
            {
                SetStatus("Addons can't be changed while the game runs or a sync is running.");
                return true;
            }
            return false;
        }

        void DownloadAddon(string name, string url, Action after = null)
        {
            if (AddonsLocked()) return;
            if (addonDlBusy)
            {
                SetStatus("Another addon is still downloading.");
                return;
            }
            addonDlBusy = true;
            SetStatus("Installing " + name + "...");
            string dir = AddonsDir();
            Bg(delegate
            {
                List<string> names = null;
                string err = null;
                BuiltinAddon builtin = BuiltinAddons.Find(url);
                try
                {
                    if (builtin != null) { BuiltinAddons.Install(dir, builtin); names = new List<string> { builtin.Folder }; }
                    else names = Archive.InstallAddonZip(url, dir);
                }
                catch (Exception e) { err = e.Message.Length > 0 ? e.Message : e.GetType().Name; }
                Ui(delegate
                {
                    addonDlBusy = false;
                    SetStatus(err == null ? name + " installed." : name + " failed.");
                    if (err == null)
                    {
                        var rec = new List<object>();
                        foreach (string n in names) rec.Add(n);
                        AddonInstalls()[name] = rec;
                        AddonInstalledAt()[name] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                        Cfg.Save();
                        if (after != null) try { after(); } catch (Exception) { }
                        if (current == "Addons" || current == "Stats") BuildPanel(current);
                    }
                    else Msg.Show(this, "Could not install " + name + ":\n\n" + err, "Addons",
                                         MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            });
        }

        void UninstallAddon(string name)
        {
            if (AddonsLocked()) return;
            var folders = AddonFoldersOf(name);
            if (folders.Count == 0)
            {
                SetStatus(name + " is not installed.");
                BuildPanel("Addons");
                return;
            }
            if (Msg.Show(side, "Remove " + name + " from your AddOns folder?\n\nFolders: " + string.Join(", ", folders) +
                                "\n\nThis deletes everything it added, including the settings it saved.", "Uninstall",
                                MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            List<string> removed;
            try { removed = Addons.Uninstall(AddonsDir(), folders, SyncSettings().SavedDir); }
            catch (Exception e)
            {
                Msg.Show(side, "Could not uninstall " + name + ":\n\n" + e.Message, "Addons", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            AddonInstalls().Remove(name);
            AddonInstalledAt().Remove(name);
            Cfg.Save();
            SetStatus(removed.Count > 0 ? name + " uninstalled." : name + " was not installed.");
            if (current == "Addons") BuildPanel("Addons");
        }

        // one folder of the AddOns folder deleted (the installed list's X), with the settings it saved
        void UninstallFolder(string folder)
        {
            if (AddonsLocked()) return;
            if (Msg.Show(side, "Uninstall " + folder + "?\n\nIts folder is deleted from your AddOns folder, with the settings it saved.",
                         "Uninstall", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var all = new List<string> { folder };
            all.AddRange(Addons.CompanionsOf(folder));       // the folders that came with it go too
            try { Addons.Uninstall(AddonsDir(), all, SyncSettings().SavedDir); }
            catch (Exception e)
            {
                Msg.Show(side, "Could not uninstall " + folder + ":\n\n" + e.Message, "Addons", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // a Download More addon made of this folder is no longer installed
            foreach (var kv in new List<KeyValuePair<string, object>>(AddonInstalls()))
            {
                var l = Json.AsList(kv.Value);
                if (l != null && (l.Contains(folder) || all.Exists(delegate(string x) { return l.Contains(x); }))) { AddonInstalls().Remove(kv.Key); AddonInstalledAt().Remove(kv.Key); }
            }
            Cfg.Save();
            SetStatus(folder + " uninstalled.");
            BuildPanel("Addons");
        }

        void AddonsInstalledView(int top)
        {
            string dir = AddonsDir();
            if (!Directory.Exists(dir))
            {
                PutText("No AddOns folder found.\nSet the game config folder in Options\n(the benilla-config folder).", 12,
                        FontStyle.Regular, Theme.Text, 20, top, 0);
                return;
            }
            List<KeyValuePair<string, bool>> addons;
            try
            {
                // addons that can't be on together and are both on are shown first, for the player to pick one (PLAY
                // keeps the preferred one if nobody picks)
                addons = Addons.Scan(dir);
            }
            catch (Exception e)
            {
                PutText(e.Message, 11, FontStyle.Regular, Theme.Text, 20, top, PW - 40);
                return;
            }
            int bottom = PH - 12;
            // --- right column: REFRESH and FOLDER, toggle all, the counter, two profiles at the bottom ---
            int cx = PW - 20 - (int)(190 * Theme.Zoom);
            var more = Put(new WowButton("DOWNLOAD MORE ›", delegate { SetAddonMode("download"); }, 190, 32, 10), cx, top);
            int rtop = BottomOf(more) + 6;
            var refresh = Put(new WowButton("REFRESH", delegate { BuildPanel("Addons"); }, 92, 28, 9), cx, rtop);
            Put(new WowButton("FOLDER", delegate { Util.OpenInShell(dir); }, 92, 28, 9), cx + (int)(98 * Theme.Zoom), rtop);
            var toggle = Put(new WowButton("TOGGLE ALL", ToggleAllAddons, 190, 34, 11), cx, BottomOf(refresh) + 6);
            addonCount = UI.Text("", 10, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
            addonCount.AutoSize = false;
            addonCount.TextAlign = ContentAlignment.TopCenter;   // centred under TOGGLE ALL
            addonCount.Bounds = new Rectangle(cx, BottomOf(toggle) + 2, toggle.Width, 20);
            Put(addonCount, cx, BottomOf(toggle) + 2);
            var profiles = Cfg.Section("addon_profiles");
            int py = bottom;
            foreach (string slot in new[] { "2", "1" })      // stacked upwards from the bottom of the column
            {
                string s = slot;
                var prof = Json.AsDict(Json.Get(profiles, s));
                py -= (int)(28 * Theme.Zoom) + 2;
                Put(new WowButton("SAVE", delegate { SaveAddonProfile(s); }, 92, 28, 9), cx, py);
                Put(new WowButton("LOAD", delegate { LoadAddonProfile(s); }, 92, 28, 9), cx + (int)(98 * Theme.Zoom), py);
                var info = UI.Text(prof == null ? "empty" : Addons.ProfileCount(prof, "enabled") + " on, " +
                                   Addons.ProfileCount(prof, "disabled") + " off\nsaved " + Json.Str(prof, "saved"),
                                   9, FontStyle.Regular, Theme.Text, Theme.Panel, (int)(190 * Theme.Zoom));
                info.Size = VStack.Measure(info);
                py -= info.Height + 2;
                Put(info, cx, py);
                var head = UI.Text("Profile " + s, 11, FontStyle.Bold, Theme.Gold, Theme.Panel, 0);
                head.Size = VStack.Measure(head);
                py -= head.Height;
                Put(head, cx, py);
                py -= 8;
            }
            // --- left: scrollable list with one switch per addon ---
            Color listbg = Theme.Ink;
            var sp = MakeScroll(panel, new Rectangle(20, top, cx - 14 - 20, bottom - top), listbg, Theme.Bronze);
            addonRows = new Dictionary<string, CheckBox>();
            if (addons.Count == 0)
            {
                var l = UI.Text("No addons installed.", 11, FontStyle.Italic, Theme.GoldDim, listbg, 0);
                l.Location = new Point(10, 8);
                sp.Controls.Add(l);
            }
            int w = sp.ClientSize.Width - 4 - 12;
            int y = 2;
            // grouped like Download More: AZS addons, WoW 1.12 addons, My Addons, then any other folder
            var cats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Data.Addons)
                foreach (string f in AddonFoldersOf(e.Name)) if (!cats.ContainsKey(f)) cats[f] = e.Category;
            // every addon of Download More that is not a WoW 1.12 one is an AZS addon here (UI, Gameplay and Misc together)
            Func<string, string> catOf = delegate(string folder)
            {
                string c;
                if (cats.TryGetValue(folder, out c)) return c == Data.Wow112Category || c == Data.MyAddonsCategory ? c : Data.AzsAddonsHeading;
                return Data.AzsAddonsHeading;                // any other folder too (for now: no OTHER ADDONS heading)
            };
            // the pairs that can't be on together and are both on: at the top, joined by a red bracket, until one is off
            var pairs = new List<KeyValuePair<string, string>>();
            var onNow = new Dictionary<string, bool>();
            foreach (var a in addons) onNow[a.Key] = a.Value;
            foreach (var a in addons)
                if (a.Value)
                    foreach (var pt in Addons.PartnersOf(dir, a.Key, addons))
                        if (Addons.IsPreferred(dir, a.Key, pt.Value) && onNow.ContainsKey(pt.Key) && onNow[pt.Key])
                            pairs.Add(new KeyValuePair<string, string>(a.Key, pt.Key));
            conflicted = new HashSet<string>();
            foreach (var pr in pairs) { conflicted.Add(pr.Key); conflicted.Add(pr.Value); }
            const string ConflictHeading = "CONFLICT: PICK ONE";
            Func<string, string> plainCat = catOf;
            catOf = delegate(string folder) { return conflicted.Contains(folder) ? ConflictHeading : plainCat(folder); };
            var order = new List<string> { ConflictHeading, Data.AzsAddonsHeading, Data.Wow112Category };
            order.Add(Data.MyAddonsCategory);
            order.Add("OTHER ADDONS");
            // one row per addon: a folder that comes with another (Survivor Plates, !BugGrabber) is part of its row;
            // the rows go by the addon's own name ("BugSack By ..."), not by folder
            addons.RemoveAll(delegate(KeyValuePair<string, bool> p) { return Addons.IsCompanion(dir, p.Key); });
            var shownName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in addons) shownName[a.Key] = Addons.DisplayName(dir, a.Key);
            Func<string, string> sortKey = delegate(string f) { return System.Text.RegularExpressions.Regex.Replace(shownName[f], "^[^A-Za-z0-9]+", ""); };
            addons.Sort(delegate(KeyValuePair<string, bool> p1, KeyValuePair<string, bool> p2)
            {
                int c1 = order.IndexOf(catOf(p1.Key)), c2 = order.IndexOf(catOf(p2.Key));
                if (c1 != c2) return c1.CompareTo(c2);
                return string.Compare(sortKey(p1.Key), sortKey(p2.Key), StringComparison.OrdinalIgnoreCase);
            });
            int linkW = pairs.Count > 0 ? 20 * pairs.Count : 0;
            var rowBox = new Dictionary<string, Rectangle>();
            string lastCat = null;
            foreach (var a in addons)
            {
                string name = a.Key;
                string cat = catOf(name);
                if (cat != lastCat)
                {
                    lastCat = cat;
                    var hd = UI.Text(cat, 9, FontStyle.Bold, cat == ConflictHeading ? Theme.Hex("#e0503a") : Theme.Gold, listbg, 0);
                    hd.Size = VStack.Measure(hd);
                    hd.Location = new Point(6, y + (y > 2 ? 8 : 2));
                    sp.Controls.Add(hd);
                    var rule = new Panel();                  // a thin gold line under the heading
                    rule.BackColor = Theme.GoldDim;
                    rule.Bounds = new Rectangle(6, hd.Bottom + 1, w - 4, 1);
                    sp.Controls.Add(rule);
                    y = rule.Bottom + 4;
                }
                string label = shownName[name];
                var cb = UI.Check(label, a.Value, a.Value ? Theme.Text : Theme.GoldDim, listbg, 11, false);
                cb.AutoSize = false;
                Size need = TextRenderer.MeasureText(label, cb.Font, new Size(w - 24 - linkW, 0), TextFormatFlags.WordBreak);
                cb.Bounds = new Rectangle(6, y, w - 34 - linkW, Math.Max(22, need.Height + 4));     // long names wrap
                rowBox[name] = cb.Bounds;
                cb.CheckedChanged += delegate { OnAddonToggle(name); };
                sp.Controls.Add(cb);
                // its folder out of the AddOns folder (with the settings it saved)
                var del = new WowButton("X", delegate { UninstallFolder(name); }, 26, 22, 9);
                del.Location = new Point(6 + w - 30, y + (cb.Height - del.Height) / 2);
                sp.Controls.Add(del);
                addonRows[name] = cb;
                y += cb.Height + 1;
            }
            for (int i = 0; i < pairs.Count; i++)
            {
                Rectangle r1 = rowBox[pairs[i].Key], r2 = rowBox[pairs[i].Value];
                var link = new ConflictLink();
                int y1 = Math.Min(r1.Top + r1.Height / 2, r2.Top + r2.Height / 2), y2 = Math.Max(r1.Top + r1.Height / 2, r2.Top + r2.Height / 2);
                link.BackColor = listbg;
                int need = link.WordHeight(), mid = (y1 + y2) / 2;
                int lt = Math.Max(0, Math.Min(y1 - 1, mid - need / 2)), lb = Math.Max(y2 + 2, mid + need / 2);
                link.Bounds = new Rectangle(6 + w - 34 - linkW + i * 20, lt, 20, lb - lt);
                link.Tick1 = y1 - lt;
                link.Tick2 = y2 - lt;
                WowButton.Tips.SetToolTip(link, pairs[i].Key + " / " + pairs[i].Value + ": " +
                                          Lang.T("these two can't be on together: untick the one you don't want."));
                sp.Controls.Add(link);
                link.BringToFront();
            }
            UpdateAddonCount();
        }

        bool settingCheck;
        HashSet<string> conflicted;                         // My Addons: the addons shown first, in a conflict

        void UpdateAddonCount()
        {
            if (addonRows == null || addonCount == null || addonCount.IsDisposed) return;
            int on = 0;
            foreach (var cb in addonRows.Values) if (cb.Checked) on++;
            addonCount.Text = Lang.T(on + " of " + addonRows.Count + " enabled");
        }

        void OnAddonToggle(string name)
        {
            if (settingCheck) return;
            CheckBox cb = addonRows[name];
            bool want = cb.Checked;
            if (AddonsLocked())
            {
                settingCheck = true;
                cb.Checked = !want;
                settingCheck = false;
                return;
            }
            if (want)
            {
                // an addon it can't be on with is on: ask before switching that one off
                var on = new List<string>();
                string why = "";
                try
                {
                    var installed = Addons.Scan(AddonsDir());
                    foreach (var pt in Addons.PartnersOf(AddonsDir(), name, installed))
                        foreach (var a in installed)
                            if (a.Key == pt.Key && a.Value && !on.Contains(a.Key)) { on.Add(a.Key); why = pt.Value.Why; }
                }
                catch (Exception) { }
                if (on.Count > 0)
                {
                    string others = string.Join(", ", on.ToArray());
                    if (!Ask("Addons", Lang.F("{0} can't be on together with {1}: {2}.", name, others, Lang.T(why)) + "\n\n" +
                                       Lang.F("Switch {0} off and turn {1} on?", others, name)))
                    {
                        settingCheck = true;
                        cb.Checked = false;
                        settingCheck = false;
                        return;
                    }
                }
            }
            try { Addons.SetEnabled(AddonsDir(), name, want); }
            catch (Exception e)
            {
                settingCheck = true;
                cb.Checked = !want;
                settingCheck = false;
                Msg.Show(side, "Could not " + (want ? "enable" : "disable") + " " + name + ":\n\n" + e.Message, "Addons",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            cb.ForeColor = want ? Theme.Text : Theme.GoldDim;
            UpdateAddonCount();
            RefreshGates();                                  // the Stats buttons follow AZS Stats
            if (!want && conflicted != null && conflicted.Contains(name))
            {
                BuildPanel("Addons");                        // the conflict is settled: the rows go back in A to Z order
                SetStatus(name + " disabled.");
                return;
            }
            SetStatus(name + " " + (want ? "enabled" : "disabled") + ".");
            if (want)
            {
                // the addons it conflicts with go off
                List<string> off;
                try { off = Addons.SwitchOffPartners(AddonsDir(), name); }
                catch (Exception e) { SetStatus(e.Message); return; }
                if (off.Count > 0)
                {
                    BuildPanel("Addons");
                    SetStatus(name + " enabled. " + string.Join(", ", off.ToArray()) + " " +
                              Lang.T("switched off: the two conflict."));
                }
            }
        }

        void ToggleAllAddons()
        {
            if (AddonsLocked()) return;
            string d = AddonsDir();
            List<KeyValuePair<string, bool>> addons;
            try { addons = Addons.Scan(d); } catch (Exception) { return; }
            if (addons.Count == 0) return;
            bool allOn = true;
            foreach (var a in addons) if (!a.Value) allOn = false;
            bool target = !allOn;                            // all on -> switch all off, otherwise all on
            var errors = new List<string>();
            foreach (var a in addons)
                if (a.Value != target)
                    try { Addons.SetEnabled(d, a.Key, target); }
                    catch (Exception e) { errors.Add(a.Key + ": " + e.Message); }
            SetStatus("All addons " + (target ? "enabled." : "disabled."));
            BuildPanel("Addons");
            if (errors.Count > 0)
                Msg.Show(side, "Some addons could not be changed:\n\n" + string.Join("\n", errors), "Addons",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void SaveAddonProfile(string slot)
        {
            var profiles = Cfg.Section("addon_profiles");
            if (profiles.ContainsKey(slot) && Msg.Show(side, "Replace the saved Profile " + slot + " with the current toggles?",
                                                              "Addons", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { profiles[slot] = Addons.MakeProfile(Addons.Scan(AddonsDir())); }
            catch (Exception e)
            {
                Msg.Show(side, e.Message, "Addons");
                return;
            }
            Cfg.Save();
            SetStatus("Profile " + slot + " saved.");
            BuildPanel("Addons");
        }

        void LoadAddonProfile(string slot)
        {
            var prof = Json.AsDict(Json.Get(Cfg.Section("addon_profiles"), slot));
            if (prof == null)
            {
                SetStatus("Profile " + slot + " is empty - press SAVE to store the current toggles.");
                return;
            }
            if (AddonsLocked()) return;
            var errors = new List<string>();
            var unknown = new List<string>();
            int changed;
            try { changed = Addons.ApplyProfile(AddonsDir(), prof, errors, unknown); }
            catch (Exception e)
            {
                Msg.Show(side, e.Message, "Addons");
                return;
            }
            string note = "Profile " + slot + " loaded: " + changed + " changed.";
            if (unknown.Count > 0) note += " " + unknown.Count + " newer addon(s) left as they were.";
            SetStatus(note);
            BuildPanel("Addons");
            if (errors.Count > 0)
                Msg.Show(side, "Some addons could not be changed:\n\n" + string.Join("\n", errors), "Addons",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ------------------------------------------------------------------ Cloud Save
        // The last 5 cloud downloads are kept as ZIPs on this PC. Nothing uses them on its own: this menu is the only
        // way back to one, for when the save is broken and the cloud copy cannot fix it.
        void ShowKeptDownloads(Control anchor)
        {
            List<SyncEngine.KeptDownload> kept;
            try { kept = new SyncEngine(SyncSettings()).KeptDownloads(); }
            catch (Exception) { kept = new List<SyncEngine.KeptDownload>(); }
            var menu = new ContextMenuStrip();
            menu.Font = Theme.F(9, FontStyle.Regular);
            var head = new ToolStripMenuItem(Lang.T("Restore a download kept on this PC (last resort):"));
            head.Enabled = false;
            menu.Items.Add(head);
            if (kept.Count == 0)
            {
                var none = new ToolStripMenuItem(Lang.T("No downloads kept yet."));
                none.Enabled = false;
                menu.Items.Add(none);
            }
            foreach (var k in kept)
            {
                var areas = new List<string>();
                foreach (var a in k.Areas) areas.Add(Lang.T(a.Key.Label));
                string stamp = k.Stamp;
                menu.Items.Add(new ToolStripMenuItem(k.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "   (" +
                                                     string.Join(", ", areas) + ")", null,
                                                     delegate { RunSync("restore", false, false, stamp); }));
            }
            if (anchor != null && !anchor.IsDisposed) menu.Show(anchor, new Point(0, anchor.Height));
            else menu.Show(Cursor.Position);
        }

        void PanelCloud()
        {
            ClearPanel("Cloud Save");
            SetCrumbs("Options", (Action)delegate { optionsTab = 1; optionsLang = false; ShowSection("Options"); }, "Cloud Save", null);
            if (!CloudEnabled())
            {
                var t = PutText("Cloud saving is turned off.\nYour progress stays on this PC only.", 12, FontStyle.Regular,
                                Theme.Text, 20, 8, 0);
                Put(new WowButton("TURN ON CLOUD SAVING", delegate { SetCloudEnabled(true); }, 290, 38, 11), 20, BottomOf(t) + 12);
                return;
            }
            var intro = PutText("PLAY downloads the newest state, starts the game and uploads your progress after you close it. " +
                                "One complete state wins; the overwritten one is backed up as a ZIP first.", 10, FontStyle.Regular,
                                Theme.Text, 20, 8, Math.Min(570, PW - 40));
            int y = BottomOf(intro) + 8;
            var lab = PutText("Sync folder", 11, FontStyle.Bold, Theme.Gold, 20, y, 0);
            string cloud = Cfg.Str("cloud_dir");
            var path = PutText(cloud.Length > 0 ? cloud : Lang.T("(not set - choose a folder inside Google Drive, OneDrive or Dropbox)"),
                               9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), 420, false);
            int rowH = BottomOf(path) - y;
            Put(new WowButton("CHOOSE", ChooseCloudDir, 100, 30, 10), PW - 20 - 100, y + Math.Max(0, (rowH - 30) / 2));
            string info = "Not synced yet.";
            try { if (cloud.Length > 0 && Directory.Exists(cloud)) info = "Cloud: " + new SyncEngine(SyncSettings()).CloudInfo(); }
            catch (Exception) { }
            var infoL = PutText(info, 9, FontStyle.Italic, Theme.GoldDim, 20, Math.Max(BottomOf(path), y + 30) + 2, PW - 20 - 170 - 30);
            // last resort: put back one of the last 5 downloads kept on this PC (small, out of the way)
            WowButton keptBtn = null;
            keptBtn = new WowButton("EARLIER DOWNLOADS", delegate { ShowKeptDownloads(keptBtn); }, 170, 24, 8);
            Put(keptBtn, PW - 20 - 170, infoL.Top - 3);
            y = Math.Max(BottomOf(infoL), infoL.Top + 21) + 4;
            Put(new WowButton("CHECK STATUS", delegate { RunSync("status", false, false); }, 170, 34, 10), 20, y);
            Put(new WowButton("DOWNLOAD", delegate { RunSync("pull", true, false); }, 170, 34, 10), 20 + 182, y);
            Put(new WowButton("UPLOAD", delegate { RunSync("push", true, false); }, 170, 34, 10), 20 + 364, y);
            y += 34 + 8;
            var frame = new Bordered(Theme.Bronze, 1);
            frame.Bounds = new Rectangle(20, y, PW - 40, PH - 12 - y);
            Put(frame, 20, y);
            var box = UI.Log(Theme.Ink, 9);
            box.Dock = DockStyle.Fill;
            frame.Controls.Add(box);
            lock (cloudLog)
            {
                int from = Math.Max(0, cloudLog.Count - 200);
                for (int i = from; i < cloudLog.Count; i++) UI.Append(box, cloudLog[i].Key + "\n", UI.LevelColor(cloudLog[i].Value), null);
            }
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            logBox = box;
        }

        // ------------------------------------------------------------------ Options
        // Options: a bar of tabs across the top (beside the back arrow) and one short page per tab
        // FOLDERS, CLOUD SAVE (its settings and its page), GRAPHICS and GAMEPLAY (the game's own settings), LAUNCHER (how
        // the launcher behaves, its language, About, setup)
        static readonly string[] OptionTabs = { "FOLDERS", "CLOUD SAVE", "GRAPHICS", "GAMEPLAY", "LAUNCHER" };
        int optionsTab;
        bool optionsLang;                                     // Options › Launcher › Language: the list of languages

        ScrollPanel optionsPage;                             // the page shown, so a rebuild of the same tab keeps its place
        int optionsPageTab = -1;

        void PanelOptions()
        {
            // a setting changed on this tab rebuilds it: it stays where it was scrolled to
            int keep = optionsPage != null && !optionsPage.IsDisposed && optionsPageTab == optionsTab ? -optionsPage.AutoScrollPosition.Y : 0;
            ClearPanel("Options");
            // no row of tabs: the tab is a step of the path at the top (Options › Folders ▾), its list the other tabs
            int top0 = 4;
            if (!optionsLang)
            {
                SetCrumbs("Options", (Action)delegate { optionsLang = false; optionsTab = 0; BuildPanel("Options"); },
                          OptionTabName(optionsTab), null);
                SetCrumbMenu(1, OptionTabMenu());
            }
            if (optionsLang)
                SetCrumbs("Options", (Action)delegate { optionsLang = false; optionsTab = 0; BuildPanel("Options"); },
                          "Launcher", (Action)delegate { optionsLang = false; BuildPanel("Options"); }, "Language", null);
            if (optionsLang)                                 // Options › Launcher ▾ : the other tabs
            {
                SetCrumbMenu(1, OptionTabMenu());
                var langs = new List<KeyValuePair<string, Action>>();   // › Language ▾ : the languages themselves
                foreach (var kv in Lang.Available()) { string code = kv.Key; langs.Add(new KeyValuePair<string, Action>(kv.Value, delegate { SetLanguage(code); })); }
                SetCrumbMenu(2, langs);
            }
            var sp = MakeScroll(panel, new Rectangle(8, top0, PW - 16, PH - 8 - top0), Theme.Panel, Theme.Panel);
            int pageW = sp.ClientSize.Width - 6;              // the page's width: what shows inside the scroll box, beside its bar
            putInto = sp;
            try
            {
                int end;
                if (optionsLang) end = OptionsLanguage(pageW);
                else if (optionsTab == 0) end = OptionsFolders(pageW);
                else if (optionsTab == 1) end = OptionsCloud(pageW);
                else if (optionsTab == 2) end = OptionsGame(pageW, new[] { "DISPLAY", "GRAPHICS" });
                else if (optionsTab == 3) end = OptionsGame(pageW, new[] { "GAMEPLAY", "SOUND" });
                else end = OptionsLauncher(pageW);
                var pad = new Panel();
                pad.BackColor = Theme.Panel;
                pad.Bounds = new Rectangle(0, end, 1, 12);
                sp.Controls.Add(pad);
            }
            finally { putInto = null; }
            optionsPage = sp;
            optionsPageTab = optionsTab;
            if (keep > 0) sp.AutoScrollPosition = new Point(0, keep);
        }

        static string OptionTabName(int i)                 // "CLOUD SAVE" -> "Cloud Save", for the trail
        {
            string[] names = { "Folders", "Cloud Save", "Graphics", "Gameplay", "Launcher" };
            return i >= 0 && i < names.Length ? names[i] : "Options";
        }

        List<KeyValuePair<string, Action>> OptionTabMenu()
        {
            var l = new List<KeyValuePair<string, Action>>();
            for (int i = 0; i < OptionTabs.Length; i++)
            {
                int n = i;
                l.Add(new KeyValuePair<string, Action>(OptionTabName(i), delegate { optionsTab = n; optionsLang = false; BuildPanel("Options"); }));
            }
            return l;
        }

        int OptionCheck(string text, bool value, int y, Action<bool> changed)
        {
            var cb = UI.Check(text, value, Theme.Text, Theme.Panel, 11);
            cb.CheckedChanged += delegate { bool on = cb.Checked; BeginInvoke(new Action(delegate { changed(on); })); };
            cb.Size = VStack.Measure(cb);
            Put(cb, 18, y);
            return y + cb.Height + 4;
        }

        // FOLDERS: where the game, its saves, its config and the cloud copy are, and cloud saving on or off
        int OptionsFolders(int PW)
        {
            SyncSettings ss = SyncSettings();
            var rows = new List<object[]>();
            rows.Add(new object[] { "Game folder", GameDir(), new Action(ChooseGameFolderButton) });
            rows.Add(new object[] { "Save game folder", ss.SaveDir, new Action(ChooseSaveDir) });
            rows.Add(new object[] { "Game config folder (AddOns, addon settings)", ss.ConfigDir, new Action(ChooseConfigDir) });
            int y = 6;
            foreach (var r in rows)
            {
                var lab = PutText((string)r[0], 11, FontStyle.Bold, Theme.Gold, 20, y, PW - 160);
                string v = (string)r[1];
                var val = PutText(string.IsNullOrEmpty(v) ? Lang.T("(not set)") : v, 9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), PW - 160, false);
                int h = BottomOf(val) - y;
                Put(new WowButton("CHANGE", (Action)r[2], 100, 28, 9), PW - 20 - (int)(100 * Theme.Zoom), y + Math.Max(0, (h - 28) / 2));
                y = Math.Max(BottomOf(val), y + 30) + 5;
            }
            // a save from the website: the game opens with it
            var il = PutText("Import a save", 11, FontStyle.Bold, Theme.Gold, 20, y, PW - 160);
            var inote = PutText("Puts in a save exported from the game (on the website: Options > Saves > Export Save), so the game opens with it.",
                                9, FontStyle.Italic, Theme.GoldDim, 20, BottomOf(il), PW - 160);
            int ih = BottomOf(inote) - y;
            Put(new WowButton("IMPORT", delegate { if (ImportSave(side, SyncSettings().SaveDir) != null) SetStatus("Save imported."); }, 100, 28, 9),
                PW - 20 - (int)(100 * Theme.Zoom), y + Math.Max(0, (ih - 28) / 2));
            y = Math.Max(BottomOf(inote), y + 30) + 5;
            return y;
        }

        // CLOUD SAVE: cloud saving on or off, its sync folder, and its page (sync now, the log, earlier downloads)
        int OptionsCloud(int PW)
        {
            int y = 6;
            y = OptionCheck("Cloud saving", CloudEnabled(), y, delegate(bool on) { SetCloudEnabled(on); });
            if (!CloudEnabled())
            {
                var off = PutText("Turn cloud saving on to keep your save and your addons' settings in Google Drive, OneDrive or Dropbox.",
                                  9.5f, FontStyle.Italic, Theme.GoldDim, 20, y + 2, PW - 40);
                return BottomOf(off);
            }
            y += 6;
            var lab = PutText("Sync folder (inside your cloud drive)", 11, FontStyle.Bold, Theme.Gold, 20, y, PW - 160);
            string v = Cfg.Str("cloud_dir");
            var val = PutText(string.IsNullOrEmpty(v) ? Lang.T("(not set)") : v, 9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), PW - 160, false);
            Put(new WowButton("CHANGE", ChooseCloudDir, 100, 28, 9), PW - 20 - (int)(100 * Theme.Zoom), y + Math.Max(0, (BottomOf(val) - y - 28) / 2));
            y = Math.Max(BottomOf(val), y + 30) + 6;
            if (!GateOpen())                                  // syncing needs the game installed
            {
                var need = PutText(CloudNeedsGame, 9.5f, FontStyle.Italic, Theme.GoldDim, 20, y, PW - 40);
                return BottomOf(need) + 6;
            }
            // what was the Cloud Save page: the state in the cloud, sync now, earlier downloads and the log
            var intro = PutText("PLAY downloads the newest state, starts the game and uploads your progress after you close it. " +
                                "One complete state wins; the overwritten one is backed up as a ZIP first.", 9.5f, FontStyle.Regular,
                                Theme.Text, 20, y, PW - 40);
            y = BottomOf(intro) + 6;
            string info = "Not synced yet.";
            try { if (v.Length > 0 && Directory.Exists(v)) info = "Cloud: " + new SyncEngine(SyncSettings()).CloudInfo(); }
            catch (Exception) { }
            var infoL = PutText(info, 9, FontStyle.Italic, Theme.GoldDim, 20, y + 4, PW - 20 - 170 - 30);
            WowButton keptBtn = null;
            keptBtn = new WowButton("EARLIER DOWNLOADS", delegate { ShowKeptDownloads(keptBtn); }, 170, 24, 8);
            Put(keptBtn, PW - 20 - (int)(170 * Theme.Zoom), y);
            y = Math.Max(BottomOf(infoL), y + (int)(24 * Theme.Zoom)) + 8;
            int bw = (int)(170 * Theme.Zoom) + 12;
            Put(new WowButton("CHECK STATUS", delegate { RunSync("status", false, false); }, 170, 34, 10), 20, y);
            Put(new WowButton("DOWNLOAD", delegate { RunSync("pull", true, false); }, 170, 34, 10), 20 + bw, y);
            Put(new WowButton("UPLOAD", delegate { RunSync("push", true, false); }, 170, 34, 10), 20 + 2 * bw, y);
            y += (int)(34 * Theme.Zoom) + 8;
            var frame = new Bordered(Theme.Bronze, 1);
            frame.Bounds = new Rectangle(20, y, PW - 40, 210);
            Put(frame, 20, y);
            var box = UI.Log(Theme.Ink, 9);
            box.Dock = DockStyle.Fill;
            frame.Controls.Add(box);
            lock (cloudLog)
            {
                int from = Math.Max(0, cloudLog.Count - 200);
                for (int i = from; i < cloudLog.Count; i++) UI.Append(box, cloudLog[i].Key + "\n", UI.LevelColor(cloudLog[i].Value), null);
            }
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
            logBox = box;
            return frame.Bottom + 6;
        }

        // GAME: the game's own settings (display mode, window size, graphics, sound), written into its config.toml;
        // the game reads them when it starts
        int OptionsGame(int PW, string[] groups)
        {
            string file = GameSettings.FileIn(SyncSettings().ConfigDir);
            var now = GameSettings.Read(file);
            bool running = GameRunningFlag || Util.GameRunning();
            int y = 6;
            if (running)                                     // only while the game runs: its settings can't be changed then
            {
                var intro = PutText("The game is running: close it first, it saves its own settings when it closes.",
                                    9.5f, FontStyle.Italic, Theme.Hex("#e07050"), 20, y, PW - 40);
                y = BottomOf(intro) + 8;
            }
            if (file == null)
            {
                var none = PutText("The game config folder is not set. Set it in Options > FOLDERS.", 10, FontStyle.Regular, Theme.Text, 20, y, PW - 40);
                return BottomOf(none);
            }
            Action<GameSetting, string> set = delegate(GameSetting gs, string v)
            {
                if (GameRunningFlag || Util.GameRunning()) { SetStatus("Close the game first: it saves its own settings when it closes."); return; }
                try
                {
                    GameSettings.Set(file, gs.Cvar, v);
                    SetStatus(Lang.T("Game setting saved:") + " " + Lang.T(gs.Label));
                }
                catch (UserError e) { SetStatus(e.Message); }
                catch (Exception e) { SetStatus(Lang.T("Could not save the game setting:") + " " + e.Message); }
                BuildPanel("Options");
            };
            string group = null;
            foreach (GameSetting gs in GameSettings.All)
            {
                if (Array.IndexOf(groups, gs.Group) < 0) continue;   // GRAPHICS: display and graphics; GAMEPLAY: gameplay and sound
                string cur = GameSettings.Value(now, gs);
                GameSetting g = gs;
                if (gs.Group != group)                       // a heading for each group: DISPLAY, GRAPHICS, SOUND, GAMEPLAY
                {
                    group = gs.Group;
                    y += 6;
                    var gh = PutText(group, 13, FontStyle.Bold, Theme.Gold, 20, y, 0);
                    Put(UI.Line(Theme.Bronze, PW - 40, 2), 20, BottomOf(gh) + 2);
                    y = BottomOf(gh) + 10;
                }
                if (gs.Check)
                {
                    y = OptionCheck(gs.Label, cur == "1", y, delegate(bool on) { set(g, on ? "1" : "0"); });
                    if (gs.Note != null)
                    {
                        var cn = PutText(gs.Note, 9.5f, FontStyle.Regular, Theme.Text, 44, y - 4, PW - 64);
                        y = BottomOf(cn) + 6;
                    }
                    continue;
                }
                var lab = PutText(gs.Label, 11.5f, FontStyle.Bold, Theme.Hex("#fff2b0"), 20, y, PW - 40);
                y = BottomOf(lab) + 1;
                if (gs.Note != null)
                {
                    var note = PutText(gs.Note, 9.5f, FontStyle.Regular, Theme.Text, 20, y, PW - 40);
                    y = BottomOf(note) + 4;
                }
                var values = new List<string>(gs.Values);
                var names = new List<string>(gs.Names);
                int at = values.FindIndex(delegate(string v) { return SameSetting(v, cur); });
                if (at < 0 && !string.IsNullOrEmpty(cur)) { values.Add(cur); names.Add(cur.Replace("x", "×")); at = values.Count - 1; }
                int n = values.Count, gap = 6;
                int bw = Math.Min(170, (int)(((PW - 40) - gap * (n - 1)) / (float)n / Theme.Zoom));
                int x = 20;
                for (int i = 0; i < n; i++)
                {
                    string v = values[i];
                    int need;                                // a long name gets the room it needs
                    using (var mg = CreateGraphics()) need = (int)(mg.MeasureString(Lang.T(names[i]), Theme.F(9)).Width / Theme.Zoom) + 20;
                    if (x > 20 && x + (int)(Math.Max(bw, need) * Theme.Zoom) > PW - 20) { x = 20; y += (int)(30 * Theme.Zoom) + 6; }
                    var b = new WowButton(names[i], delegate { set(g, v); }, Math.Max(bw, need), 30, 9);
                    b.Selected = i == at;
                    Put(b, x, y);
                    x += b.Width + gap;
                }
                y += (int)(30 * Theme.Zoom) + 14;
            }
            var where = PutText(Lang.T("Saved in") + " " + file, 8.5f, FontStyle.Regular, Theme.LinkBlue, 20, y, PW - 40, false);
            return BottomOf(where);
        }

        static bool SameSetting(string a, string b)
        {
            if (a == null || b == null) return false;
            double x, y;
            if (double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                return Math.Abs(x - y) < 0.001;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // the play time AZS Stats counts (at the end of LAUNCHER)
        int OptionsPlayTime(int PW, int y)
        {
            var head = PutText("Play time", 11, FontStyle.Bold, Theme.Gold, 20, y, 0);
            PlayStats ps = ShownPlayStats();
            long sessions = ps.Sessions;
            string stats = sessions == 0
                ? (Stats.AddonInstalled(AddonsDir()) ? "No play time counted yet. AZS Stats counts it while the game runs."
                                                      : "Play time is counted in game by the AZS Stats addon. Install it on the STATS page.")
                : "Total: " + Duration(ps.Seconds) + " over " + Util.Plural((int)sessions, "session") + "\n" +
                  "Counted in game by AZS Stats; cloud saving carries it to your other PCs.";
            var statsLabel = PutText(stats, 10, FontStyle.Regular, Theme.Text, 20, BottomOf(head) + 2, PW - 40);
            return BottomOf(statsLabel);
        }

        // LANGUAGE: every language built into the launcher, in its own name
        int OptionsLanguage(int PW)
        {
            var langs = Lang.Available();
            int y = 6, x = 20, col = 0, bw = 190, cols = Math.Max(1, (PW - 40) / (int)((bw + 10) * Theme.Zoom));
            foreach (var kv in langs)
            {
                string code = kv.Key;
                var b = new WowButton(kv.Value, delegate { SetLanguage(code); }, bw, 28, 9);
                b.Selected = code == Lang.Code;
                Put(b, x + col * (int)((bw + 10) * Theme.Zoom), y);
                if (++col >= cols) { col = 0; y += (int)(28 * Theme.Zoom) + 6; }
            }
            if (col > 0) y += (int)(28 * Theme.Zoom) + 6;
            return y;
        }

        // LAUNCHER: updating the launcher, About, setup again and installing the game from a file
        int OptionsLauncher(int PW)
        {
            int y = 6, bw = 200;
            Action<string, Action, string> row = delegate(string label, Action act, string note)
            {
                var b = Put(new WowButton(label, act, bw, 30, 9), 20, y);
                var t = PutText(note, 9.5f, FontStyle.Italic, Theme.GoldDim, 20 + b.Width + 12, y + 2, PW - 40 - b.Width - 12);
                y = Math.Max(BottomOf(b), BottomOf(t)) + 8;
            };
            // how the launcher behaves (was GENERAL)
            y = OptionCheck("Close button hides the launcher to the tray", Cfg.Bool("close_to_tray", true), y,
                            delegate(bool on) { Cfg.Set("close_to_tray", on); Cfg.Save(); });
            y = OptionCheck("Minimize the launcher when the game starts", Cfg.Bool("minimize_on_play", true), y,
                            delegate(bool on) { Cfg.Set("minimize_on_play", on); Cfg.Save(); });
            y = OptionCheck("Show the Stats section (your play time and run records)", StatsEnabled(), y,
                            delegate(bool on) { SetStatsEnabled(on); });
            y = OptionCheck("Show play time on the home screen", Cfg.Bool("home_playtime", true), y,
                            delegate(bool on) { Cfg.Set("home_playtime", on); Cfg.Save(); Invalidate(); });
            Put(UI.Line(Theme.Bronze, PW - 40, 1), 20, y + 4);
            y += 14;
            string langName = Lang.Code;
            foreach (var kv in Lang.Available()) if (kv.Key == Lang.Code) langName = kv.Value;
            row("LANGUAGE ›", delegate { optionsLang = true; BuildPanel("Options"); }, langName);
            row("ABOUT LAUNCHER", delegate { ShowSection("About Launcher"); }, "What the launcher does.");
            row("RUN SETUP AGAIN", delegate { RunSetup(); }, "Asks again about your save, folders, cloud saving and addons. Nothing is downloaded.");
            return y;                                        // play time: on the home screen, under the Launcher buttons
        }

        // the game folder: one that holds the game is the same as installing it (there is no "install from a file")
        void ChooseGameFolderButton() { ChooseGameFolder(); }

        bool ChooseGameFolder()
        {
            string d = AskFolder(this, "Select your Azeroth Survivors game folder (the one with " + Util.GameExe + ")", GameDir());
            if (d == null) return false;
            if (!File.Exists(Path.Combine(d, Util.GameExe)))
            {
                Warn("Game folder", Lang.T("That folder does not have the game in it") + " (" + Util.GameExe + ").");
                return false;
            }
            Cfg.Set("game_dir", d);
            Cfg.Set("installed_by_launcher", true);           // the player's own copy counts as installed
            Cfg.Save();
            SetStatus("Game folder set: " + d);
            RefreshPlayLabel();                              // PLAY/INSTALL and the Cloud Save button follow the new folder
            Invalidate();
            RebuildCurrent();
            return true;
        }

        void ChooseSaveDir()
        {
            string d = AskFolder(side, "Select the folder that holds your save game (normally %APPDATA%\\wenilla-rogue)", SyncSettings().SaveDir);
            if (d == null) return;
            Cfg.Set("save_dir", Path.GetFullPath(d));
            Cfg.Save();
            RebuildCurrent();
        }

        void ChooseConfigDir()
        {
            string d = AskFolder(side, "Select the game's benilla-config folder (contains AddOns and saved)", SyncSettings().ConfigDir);
            if (d == null) return;
            Cfg.Set("config_dir", Path.GetFullPath(d));
            Cfg.Remove("addons_dir");                        // AddOns and addon settings follow the config folder
            Cfg.Remove("saved_dir");
            Cfg.Save();
            RebuildCurrent();
        }
    }
}
