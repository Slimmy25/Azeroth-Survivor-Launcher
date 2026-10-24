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
    public class SideHead : Control
    {
        string title = "Addons";
        public Action CloseClicked;
        public Form Owner2;
        public Action<Point> Moved;
        Point? dragOffset;

        public SideHead()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 50;
            BackColor = Theme.Hex("#15100a");
        }

        public string Title { get { return title; } set { title = value; Invalidate(); } }

        Rectangle CloseBox { get { return new Rectangle(Width - 34, 13, 22, 22); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int w = Math.Max(Width, 200);
            for (int i = 0; i < 30; i++)                     // bronze bar with a gradient
            {
                float y = 11 + i * 0.9f;
                using (var p = new Pen(Theme.Mix(Theme.Hex("#6a5026"), Theme.Hex("#241908"), i / 29.0), 1.5f))
                    g.DrawLine(p, 30, y, w - 4, y);
            }
            using (var p = new Pen(Theme.Hex("#b8893a"), 2)) g.DrawRectangle(p, 30, 11, w - 34, 27);
            Theme.Smooth(g);
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(Theme.Hex("#fff0b0")))
            {
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Lang.T(title), Theme.F(13, FontStyle.Bold), b, new RectangleF(66, 11, w - 110, 28), sf);
            }
            if (Theme.Portrait != null) g.DrawImage(Theme.Portrait, 28 - 24, 25 - 24, 48, 48);
            Rectangle x = CloseBox;
            using (var b = new SolidBrush(Theme.Hex("#4a2a12"))) g.FillRectangle(b, x);
            using (var p = new Pen(Theme.Hex("#d6a640"), 2)) g.DrawRectangle(p, x);
            using (var p = new Pen(Theme.Hex("#e53a22"), 3))
            {
                g.DrawLine(p, x.X + 6, x.Y + 6, x.X + 16, x.Y + 16);
                g.DrawLine(p, x.X + 16, x.Y + 6, x.X + 6, x.Y + 16);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = CloseBox.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            if (dragOffset.HasValue && Owner2 != null)
            {
                Point s = PointToScreen(e.Location);
                Owner2.Location = new Point(s.X - dragOffset.Value.X, s.Y - dragOffset.Value.Y);
            }
            base.OnMouseMove(e);
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
        string addonFilter;
        int clView = ClNewest, clPage;
        bool clToEnd;
        DateTime clLastStep = DateTime.MinValue;
        Dictionary<string, CheckBox> addonRows;
        Label addonCount;

        static readonly string[] SectionNames = { "Guide", "Cloud Save", "Addons", "Changelog", "Stats", "Options", "About Launcher" };
        int gTopic = -1, gPage = -1;                         // the guide: -1 = the topic list / the topic itself

        int PW { get { return panel.ClientSize.Width; } }
        int PH { get { return panel.ClientSize.Height; } }

        // ------------------------------------------------------------------ the window itself
        void BuildSide()
        {
            Color bronzeD = Theme.Hex("#3a2a12"), dark = Theme.Hex("#15100a");
            side = new Form();
            side.FormBorderStyle = FormBorderStyle.None;
            side.ShowInTaskbar = true;                       // its own taskbar button and Alt+Tab entry
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
            var frame = new Bordered(Theme.Hex("#b8893a"), 2);
            frame.Dock = DockStyle.Fill;
            side.Controls.Add(frame);
            var inner = new Panel();
            inner.Dock = DockStyle.Fill;
            inner.BackColor = dark;
            frame.Controls.Add(inner);

            var body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = dark;
            body.Padding = new Padding(8, 6, 8, 0);
            inner.Controls.Add(body);
            sideHead = new SideHead();
            sideHead.Dock = DockStyle.Top;
            sideHead.Owner2 = side;
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
            strip.Width = 42;
            strip.BackColor = dark;
            body.Controls.Add(strip);
            homeBtn = new IconButton(Icons.Home, ShowWindowFront);    // bring the launcher back
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
            var chat = new Bordered(Theme.Hex("#6b4a1c"), 2);
            chat.Dock = DockStyle.Fill;
            body.Controls.Add(chat);
            chat.BringToFront();
            panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Theme.Panel;
            chat.Controls.Add(panel);
            LayoutStrip();
            side.PerformLayout();
        }

        void LayoutStrip()
        {
            homeBtn.Location = new Point(8, 0);
            int y = 34 + 14;
            foreach (string n in SectionNames)
            {
                var b = stripBtns[n];
                bool show = NavShown(n);
                b.Visible = show;
                if (!show) continue;
                b.Location = new Point(8, y);
                y += 34 + 6;
            }
        }

        void RefreshStrip()
        {
            foreach (var kv in stripBtns) kv.Value.Active = kv.Key == current;
        }

        void SetSideTitle(string text)
        {
            sideHead.Title = text;
            side.Text = Lang.T(Array.IndexOf(SectionNames, current) >= 0 ? current : text);    // the taskbar and Alt+Tab name
        }

        // opens at its remembered spot, or the first time to the right of the launcher
        void PlaceSide()
        {
            int h = Math.Min(SideH, Screen.PrimaryScreen.Bounds.Height - 60);
            var pos = Json.AsList(Json.Get(Cfg.Data, "side_pos"));
            int x, y;
            if (pos != null && pos.Count == 2)
            {
                x = Convert.ToInt32(pos[0]);
                y = Convert.ToInt32(pos[1]);
            }
            else
            {
                x = Left + Width + 6;
                y = Top;
            }
            Rectangle all = SystemInformation.VirtualScreen;
            x = Math.Max(all.Left - SideW + 120, Math.Min(x, all.Right - 120));     // keep a grabbable part on screen
            y = Math.Max(all.Top, Math.Min(y, all.Bottom - 80));
            side.Bounds = new Rectangle(x, y, SideW, h);
        }

        void ShowSide()
        {
            if (!side.Visible) PlaceSide();
            side.Show();
            side.WindowState = FormWindowState.Normal;
            side.Activate();
        }

        void HideSide()
        {
            if (side.Visible) side.Hide();
        }

        void CloseSide()
        {
            current = "Home";
            foreach (var b in nav.Values) b.Active = false;
            RefreshStrip();
            HideSide();
        }

        public void ShowSection(string name)
        {
            // Options, About, the Guide and Stats work without the game; the other sections need it
            if (name != "Home" && name != "Options" && name != "About Launcher" && name != "Guide" && name != "Stats" && !GateOpen())
            {
                SetStatus("Press INSTALL to download the game first.");
                ShowWindowFront();
                return;
            }
            if (name == current && name != "Home") name = "Home";     // clicking the active button again closes it
            current = name;
            foreach (var kv in nav) kv.Value.Active = kv.Key == name;
            RefreshStrip();
            if (name == "Changelog") clView = ClNewest;
            if (name == "Guide") { gTopic = -1; gPage = -1; }      // the guide always opens on its topic list
            if (name == "About Launcher") aboutLog = false;
            if (name == "Home")
            {
                HideSide();
                return;
            }
            ShowSide();
            BuildPanel(name);
        }

        void BuildPanel(string name)
        {
            panel.SuspendLayout();
            try
            {
                if (name == "Cloud Save") PanelCloud();
                else if (name == "Addons") PanelAddons();
                else if (name == "Options") PanelOptions();
                else if (name == "Changelog") PanelChangelog();
                else if (name == "Guide") PanelGuide();
                else if (name == "Stats") PanelStats();
                else if (name == "About Launcher") PanelAbout();
            }
            finally { panel.ResumeLayout(); }
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
            logBox = null;
            while (panel.Controls.Count > 0)
            {
                Control c = panel.Controls[0];
                panel.Controls.RemoveAt(0);
                c.Dispose();
            }
            SetSideTitle(title);
            Put(UI.BackArrow(GoBack), 20, 12);
        }

        // the back arrow: one step back inside the section, or out of the section when at its first page
        void GoBack()
        {
            if (current == "Addons" && addonMode == "download") { SetAddonMode("installed"); return; }
            if (current == "Changelog")
            {
                if (clView >= 0) { SetChangelogView(ClList); return; }
                if (clView == ClList) { SetChangelogView(ClNewest); return; }
            }
            if (current == "Guide")
            {
                if (gPage >= 0) { SetGuideView(gTopic, -1); return; }
                if (gTopic >= 0) { SetGuideView(-1, -1); return; }
            }
            CloseSide();
        }

        T Put<T>(T c, int x, int y) where T : Control
        {
            c.Location = new Point(x, y);
            panel.Controls.Add(c);
            c.BringToFront();
            return c;
        }

        Label PutText(string text, float size, FontStyle style, Color fg, int x, int y, int wrap, bool translate = true)
        {
            var l = UI.Text(text, size, style, fg, Theme.Panel, wrap, translate);
            l.Size = VStack.Measure(l);
            return Put(l, x, y);
        }

        // A scrollable area whose Windows scroll bar is hidden (clipped off to the right) and replaced by a slim
        // painted one in `thumb` colors. The content keeps the same width as with MakeScroll.
        ScrollPanel MakeThinScroll(Control parent, Rectangle r, Color bg, Color border, Color thumb, Color thumbHot)
        {
            var frame = new Bordered(border, 1);
            frame.Bounds = r;
            parent.Controls.Add(frame);
            frame.BringToFront();
            var clip = new Panel();                          // clips the native bar off
            clip.Dock = DockStyle.Fill;
            clip.BackColor = bg;
            frame.Controls.Add(clip);
            var sp = new ScrollPanel();
            sp.BackColor = bg;
            int cw = r.Width - 2, ch = r.Height - 2;
            sp.Bounds = new Rectangle(0, 0, cw + SystemInformation.VerticalScrollBarWidth, ch);
            clip.Controls.Add(sp);
            var bar = new ThinScrollBar(sp, Theme.Mix(bg, Color.Black, 0.3), thumb, thumbHot);
            bar.Bounds = new Rectangle(cw - 7, 2, 6, ch - 4);
            clip.Controls.Add(bar);
            bar.BringToFront();
            return sp;
        }

        // a scrollable area with a 1 px border; returns the scrolling panel
        ScrollPanel MakeScroll(Control parent, Rectangle r, Color bg, Color border)
        {
            var frame = new Bordered(border, 1);
            frame.Bounds = r;
            parent.Controls.Add(frame);
            frame.BringToFront();
            var sp = new ScrollPanel();
            sp.Dock = DockStyle.Fill;
            sp.BackColor = bg;
            frame.Controls.Add(sp);
            return sp;
        }

        static int BottomOf(Control c) { return c.Top + c.Height; }

        // ------------------------------------------------------------------ Changelog
        // ------------------------------------------------------------------ Guide
        // assets\guide.txt: topics on the first page; a topic with pages lists them, the others show their text.
        // The guide's own text is English; only the buttons around it are translated.
        void SetGuideView(int topic, int page)
        {
            gTopic = topic;
            gPage = page;
            if (current == "Guide") BuildPanel("Guide");
        }

        void PanelGuide()
        {
            var topics = Data.Guide;
            if (gTopic >= topics.Count) { gTopic = -1; gPage = -1; }
            if (gTopic < 0)
            {
                ClearPanel("Guide");
                var intro = PutText("New to Azeroth Survivors? Pick a topic.", 11, FontStyle.Italic, Theme.GoldDim, 60, 18, 0);
                var items = new List<GuidePage>();
                foreach (var t in topics) items.Add(t);
                GuideList(items, BottomOf(intro) + 12, delegate(int i) { SetGuideView(i, -1); });
                return;
            }
            GuideTopic topic = topics[gTopic];
            if (gPage >= topic.Pages.Count) gPage = -1;
            if (gPage < 0 && topic.Pages.Count > 0)            // a topic made of pages: list them
            {
                ClearPanel(topic.Title);
                Put(new WowButton("‹ ALL TOPICS", delegate { SetGuideView(-1, -1); }, 170, 32, 10), PW - 20 - 170, 12);
                int ti = gTopic;
                GuideList(topic.Pages, 56, delegate(int i) { SetGuideView(ti, i); });
                return;
            }
            GuidePage shown = gPage >= 0 ? topic.Pages[gPage] : topic;
            ClearPanel(shown.Title);
            bool inPage = gPage >= 0;
            if (inPage) Put(new WowButton("‹ BACK", delegate { SetGuideView(gTopic, -1); }, 120, 32, 10), PW - 20 - 120, 12);
            else Put(new WowButton("‹ ALL TOPICS", delegate { SetGuideView(-1, -1); }, 170, 32, 10), PW - 20 - 170, 12);
            // PREVIOUS / NEXT step through the pages of this topic, or through the topics
            int count = inPage ? topic.Pages.Count : topics.Count, at = inPage ? gPage : gTopic, by = PH - 12 - 34;
            int tIndex = gTopic;
            if (at > 0)
                Put(new WowButton("‹ PREVIOUS", delegate { if (inPage) SetGuideView(tIndex, at - 1); else SetGuideView(at - 1, -1); }, 150, 34, 10), 20, by);
            if (at + 1 < count)
                Put(new WowButton("NEXT ›", delegate { if (inPage) SetGuideView(tIndex, at + 1); else SetGuideView(at + 1, -1); }, 150, 34, 10), PW - 20 - 150, by);
            var sp = MakeScroll(panel, new Rectangle(20, 56, PW - 40, by - 14 - 56), Theme.Panel, Theme.Bronze);
            GuideBox(sp, shown);
        }

        // rows of title + description with an OPEN button, like the addon download list
        void GuideList(List<GuidePage> items, int top, Action<int> open)
        {
            Color listbg = Theme.Ink;
            var sp = MakeScroll(panel, new Rectangle(20, top, PW - 40, PH - 12 - top), listbg, Theme.Bronze);
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            int y = 4;
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var row = new Bordered(Theme.GoldDim, 1);
                var inner = new Panel();
                inner.BackColor = listbg;
                inner.Dock = DockStyle.Fill;
                row.Controls.Add(inner);
                var btn = new WowButton("OPEN ›", delegate { open(index); }, 100, 30, 10);
                int textW = w - 2 - 10 - 100 - 10 - 10;
                int ty = 6;
                var labels = new List<Label>();
                labels.Add(UI.Text(items[i].Title, 12, FontStyle.Bold, Theme.Gold, listbg, textW, false));
                if (items[i].Desc.Length > 0) labels.Add(UI.Text(items[i].Desc, 9, FontStyle.Regular, Theme.Text, listbg, textW, false));
                foreach (var l in labels)
                {
                    l.Size = VStack.Measure(l);
                    l.Location = new Point(10, ty);
                    l.Cursor = Cursors.Hand;
                    l.Click += delegate { open(index); };   // the whole row opens the topic, not only the button
                    inner.Controls.Add(l);
                    ty += l.Height;
                }
                int h = Math.Max(ty + 6, 42) + 2;
                inner.Cursor = Cursors.Hand;
                inner.Click += delegate { open(index); };
                btn.Location = new Point(w - 2 - 10 - 100, (h - 2 - 30) / 2);
                inner.Controls.Add(btn);
                row.Bounds = new Rectangle(4, y, w, h);
                sp.Controls.Add(row);
                y += h + 6;
            }
        }

        // the text of a guide page: "# heading", "- bullet", a line in quotes, paragraphs
        void GuideBox(ScrollPanel sp, GuidePage page)
        {
            Color boxbg = Theme.Ink;
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            int tw = w - 32;
            var box = new Bordered(Theme.GoldDim, 2);
            var inner = new Panel();
            inner.BackColor = boxbg;
            inner.Dock = DockStyle.Fill;
            box.Controls.Add(inner);
            int y = 10;
            Action<Label, int, int> add = delegate(Label l, int x, int gap)
            {
                l.Size = VStack.Measure(l);
                l.Location = new Point(x, y + gap);
                inner.Controls.Add(l);
                y += gap + l.Height;
            };
            add(UI.Text(page.Title, 16, FontStyle.Bold, Theme.Gold, boxbg, tw, false), 14, 0);
            if (page.Desc.Length > 0) add(UI.Text(page.Desc, 10, FontStyle.Italic, Theme.GoldDim, boxbg, tw, false), 14, 0);
            var para = new List<string>();
            Action flush = delegate
            {
                if (para.Count == 0) return;
                add(UI.Text(string.Join(" ", para.ToArray()), 10, FontStyle.Regular, Theme.Text, boxbg, tw, false), 14, 6);
                para.Clear();
            };
            foreach (string raw in page.Body.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) { flush(); continue; }
                if (line.StartsWith("# "))
                {
                    flush();
                    add(UI.Text(line.Substring(2).Trim(), 12, FontStyle.Bold, Theme.Gold, boxbg, tw, false), 14, 12);
                }
                else if (line.StartsWith("- "))
                {
                    flush();
                    var dot = UI.Text("•", 10, FontStyle.Bold, Theme.Gold, boxbg, 0, false);
                    dot.Size = VStack.Measure(dot);
                    dot.Location = new Point(18, y + 4);
                    inner.Controls.Add(dot);
                    add(UI.Text(line.Substring(2).Trim(), 10, FontStyle.Regular, Theme.Text, boxbg, tw - 18, false), 32, 4);
                }
                else if (line.StartsWith("\""))
                {
                    flush();
                    add(UI.Text(line, 10, FontStyle.Italic, Theme.GoldDim, boxbg, tw, false), 14, 6);
                }
                else para.Add(line);
            }
            flush();
            y += 14;
            box.Bounds = new Rectangle(4, 4, w, y + 4);
            sp.Controls.Add(box);
            var pad = new Panel();                           // a little room under the box
            pad.Bounds = new Rectangle(4, box.Bottom, 1, 8);
            pad.BackColor = sp.BackColor;
            sp.Controls.Add(pad);
        }

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
            Put(new WowButton(btn, delegate { SetChangelogView(back); }, 190, 32, 10), PW - 20 - 190, 12);
            int top = 52, bottom = PH;
            if (clView >= 0)                                 // PREVIOUS (older) / NEXT (newer) under the post
            {
                int v = clView, by = PH - 12 - 34;
                if (v + 1 < entries.Count) Put(new WowButton("‹ PREVIOUS", delegate { SetChangelogView(v + 1); }, 150, 34, 10), 20, by);
                if (v > 0) Put(new WowButton("NEXT ›", delegate { SetChangelogView(v - 1); }, 150, 34, 10), PW - 20 - 150, by);
                var lbl = UI.Text((entries.Count - v) + " of " + entries.Count, 10, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
                Size ls = VStack.Measure(lbl);
                lbl.Size = ls;
                Put(lbl, (PW - ls.Width) / 2, by + (34 - ls.Height) / 2);
                bottom = by;
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
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            var box = new Bordered(Theme.GoldDim, 2);
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

        void PatchNotesList(List<string> entries, int top)
        {
            int per = Data.ClRows * 2;
            int pages = Math.Max(1, (entries.Count + per - 1) / per);
            int page = Math.Min(Math.Max(clPage, 0), pages - 1);
            clPage = page;
            int areaBottom = PH - 14;
            if (pages > 1)                                   // numbered page buttons at the bottom
            {
                int perRow = pages <= 8 ? pages : (pages + 1) / 2;     // many pages: a second row right below
                int rows = (pages + perRow - 1) / perRow;
                int y = PH - 12 - rows * 32 + 2;
                areaBottom = y - 14;
                for (int r = 0; r < pages; r += perRow)
                {
                    var items = new List<Control>();
                    if (r == 0) items.Add(UI.Text("PAGE", 10, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0));
                    for (int pg = r; pg < Math.Min(pages, r + perRow); pg++)
                    {
                        int n = pg;
                        var pb = new WowButton((pg + 1).ToString(), delegate { SetClPage(n); }, 44, 30, pg != page ? 11 : 13);
                        pb.Selected = pg == page;            // you are here: filled gold
                        items.Add(pb);
                    }
                    var row = new HRow(6, items.ToArray());
                    Put(row, (PW - row.Width) / 2, y);
                    y += 32;
                }
            }
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
                int col = k / Data.ClRows, rowN = k % Data.ClRows;   // fill the left column first, then the right
                int n = i;
                string version = Data.PostTitle(entries[i]) + (i == 0 ? " (current)" : "");
                var b = new WowButton(version, delegate { SetChangelogView(n); }, 230, 40, 14);
                b.Location = new Point(col * colW + 4, rowN * 48 + 4);
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
            if (view >= 0) clPage = view / (Data.ClRows * 2);         // coming back to the list shows this post's page
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

        void PanelAbout()
        {
            ClearPanel("About Launcher");
            var about = Put(new WowButton("ABOUT", delegate { aboutLog = false; BuildPanel("About Launcher"); }, 130, 32, 10), 84, 12);
            about.Selected = !aboutLog;
            var log = Put(new WowButton("LAUNCHER CHANGELOG", delegate { aboutLog = true; BuildPanel("About Launcher"); }, 230, 32, 10),
                          84 + 130 + 10, 12);
            log.Selected = aboutLog;
            var credits = UI.Text("Vibe coded by Slimjim\nCloud save code established by Keijin", 13, FontStyle.Italic,
                                  Theme.Credit, Theme.Panel, 0);
            credits.TextAlign = ContentAlignment.TopRight;
            credits.Size = VStack.Measure(credits);
            Put(credits, PW - 20 - credits.Width, 56);
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

        // ------------------------------------------------------------------ Addons
        string AddonsDir() { return SyncSettings().AddonsDir; }

        void PanelAddons()
        {
            bool dl = addonMode == "download";
            ClearPanel(dl ? "Download More" : "Addons");
            if (dl)
            {
                // the back arrow returns to My Addons; the two category buttons share the rest of the row
                int x0 = 84, wCats = PW - 104, cw = wCats / Data.AddonCategories.Length;
                for (int i = 0; i < Data.AddonCategories.Length; i++)
                {
                    int n = i;
                    var b = new WowButton(Data.AddonCategories[i], delegate { addonCategory = n; BuildPanel("Addons"); }, 190, 32, 10);
                    b.Selected = i == addonCategory;          // the open category: filled gold
                    Put(b, x0 + i * cw + (cw - 190) / 2, 12);
                }
                // second row: filter by kind of addon (click the open one again to show everything)
                var labels = new List<string> { "ALL" };
                labels.AddRange(Data.AddonFilters);
                int fw = (PW - 40) / labels.Count;
                for (int i = 0; i < labels.Count; i++)
                {
                    string label = labels[i];
                    var b = new WowButton(label, delegate { SetAddonFilter(label); }, 130, 30, 9);
                    b.Selected = (label == "ALL" && addonFilter == null) || label == addonFilter;
                    Put(b, 20 + i * fw + (fw - 130) / 2, 53);
                }
                AddonsDownloadView(94);
            }
            else
            {
                Put(new WowButton("DOWNLOAD MORE ›", delegate { SetAddonMode("download"); }, 190, 32, 10), PW - 20 - 190, 12);
                AddonsInstalledView(52);
            }
        }

        void SetAddonMode(string mode)
        {
            addonMode = mode;
            BuildPanel("Addons");
        }

        void SetAddonFilter(string label)
        {
            addonFilter = label == "ALL" || label == addonFilter ? null : label;
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
            return Addons.FoldersOf(AddonsDir(), name, RecordedFolders(name));
        }

        void AddonsDownloadView(int top)
        {
            Color listbg = Theme.Ink;
            var sp = MakeScroll(panel, new Rectangle(20, top, PW - 40, PH - 12 - top), listbg, Theme.Bronze);
            int cat = Math.Min(Math.Max(addonCategory, 0), Data.AddonCategories.Length - 1);
            var items = new List<AddonEntry>();
            foreach (var a in Data.Addons)
                if (a.Category == Data.AddonCategories[cat] && (addonFilter == null || a.Tags.Contains(addonFilter))) items.Add(a);
            items.Sort(delegate(AddonEntry a, AddonEntry b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });   // A to Z
            if (items.Count == 0)
            {
                var l = UI.Text(addonFilter != null ? "No addons in this filter yet." : "No downloadable addons listed yet.",
                                11, FontStyle.Italic, Theme.GoldDim, listbg, 0);
                l.Location = new Point(10, 8);
                sp.Controls.Add(l);
                return;
            }
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8;
            int y = 4;
            foreach (var item in items)
            {
                var it = item;
                var row = new Bordered(Theme.GoldDim, 1);
                var inner = new Panel();
                inner.BackColor = listbg;
                inner.Dock = DockStyle.Fill;
                row.Controls.Add(inner);
                bool haveIt = AddonFoldersOf(it.Name).Count > 0;
                // the button keeps its room; the text wraps to what is left, so nothing runs past the window
                var btn = haveIt ? new WowButton("UNINSTALL", delegate { UninstallAddon(it.Name); }, 120, 30, 10)
                                 : new WowButton("INSTALL", delegate { DownloadAddon(it.Name, it.Url); }, 120, 30, 10);
                int textW = w - 2 - 10 - 120 - 10 - 10;
                int ty = 6;
                var labels = new List<Label>();
                labels.Add(UI.Text(it.Name, 12, FontStyle.Bold, Theme.Gold, listbg, textW, false));
                if (it.Description.Length > 0) labels.Add(UI.Text(it.Description, 9, FontStyle.Regular, Theme.Text, listbg, textW, false));
                foreach (var l in labels)
                {
                    l.Size = VStack.Measure(l);
                    l.Location = new Point(10, ty);
                    inner.Controls.Add(l);
                    ty += l.Height;
                }
                int h = Math.Max(ty + 6, 42) + 2;
                btn.Location = new Point(w - 2 - 10 - 120, (h - 2 - 30) / 2);
                inner.Controls.Add(btn);
                row.Bounds = new Rectangle(4, y, w, h);
                sp.Controls.Add(row);
                y += h + 6;
            }
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
                try { names = Archive.InstallAddonZip(url, dir); }
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
                        Cfg.Save();
                        if (after != null) try { after(); } catch (Exception) { }
                        if (current == "Addons" || current == "Stats") BuildPanel(current);
                    }
                    else Msg.Show(side.Visible ? (IWin32Window)side : this, "Could not install " + name + ":\n\n" + err, "Addons",
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
            Cfg.Save();
            SetStatus(removed.Count > 0 ? name + " uninstalled." : name + " was not installed.");
            if (current == "Addons") BuildPanel("Addons");
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
            try { addons = Addons.Scan(dir); }
            catch (Exception e)
            {
                PutText(e.Message, 11, FontStyle.Regular, Theme.Text, 20, top, PW - 40);
                return;
            }
            int bottom = PH - 12;
            // --- right column: REFRESH and FOLDER, toggle all, the counter, two profiles at the bottom ---
            int cx = PW - 20 - 190;
            Put(new WowButton("REFRESH", delegate { BuildPanel("Addons"); }, 92, 28, 9), cx, top);
            Put(new WowButton("FOLDER", delegate { Util.OpenInShell(dir); }, 92, 28, 9), cx + 98, top);
            var toggle = Put(new WowButton("TOGGLE ALL", ToggleAllAddons, 190, 34, 11), cx, top + 28 + 6);
            addonCount = UI.Text("", 10, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
            addonCount.AutoSize = false;
            addonCount.TextAlign = ContentAlignment.TopRight;
            addonCount.Bounds = new Rectangle(cx, BottomOf(toggle) + 2, 190, 20);
            Put(addonCount, cx, BottomOf(toggle) + 2);
            var profiles = Cfg.Section("addon_profiles");
            int py = bottom;
            foreach (string slot in new[] { "2", "1" })      // stacked upwards from the bottom of the column
            {
                string s = slot;
                var prof = Json.AsDict(Json.Get(profiles, s));
                py -= 28 + 2;
                Put(new WowButton("SAVE", delegate { SaveAddonProfile(s); }, 92, 28, 9), cx, py);
                Put(new WowButton("LOAD", delegate { LoadAddonProfile(s); }, 92, 28, 9), cx + 98, py);
                var info = UI.Text(prof == null ? "empty" : Addons.ProfileCount(prof, "enabled") + " on, " +
                                   Addons.ProfileCount(prof, "disabled") + " off\nsaved " + Json.Str(prof, "saved"),
                                   9, FontStyle.Regular, Theme.Text, Theme.Panel, 190);
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
            int w = sp.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12;
            int y = 2;
            foreach (var a in addons)
            {
                string name = a.Key;
                var cb = UI.Check(name, a.Value, a.Value ? Theme.Text : Theme.GoldDim, listbg, 11, false);
                cb.AutoSize = false;
                Size need = TextRenderer.MeasureText(name, cb.Font, new Size(w - 24, 0), TextFormatFlags.WordBreak);
                cb.Bounds = new Rectangle(6, y, w, Math.Max(22, need.Height + 4));     // long names wrap
                cb.CheckedChanged += delegate { OnAddonToggle(name); };
                sp.Controls.Add(cb);
                addonRows[name] = cb;
                y += cb.Height + 1;
            }
            UpdateAddonCount();
        }

        bool settingCheck;

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
            SetStatus(name + " " + (want ? "enabled" : "disabled") + ".");
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
            if (!CloudEnabled())
            {
                var t = PutText("Cloud saving is turned off.\nYour progress stays on this PC only.", 12, FontStyle.Regular,
                                Theme.Text, 20, 56, 0);
                Put(new WowButton("TURN ON CLOUD SAVING", delegate { SetCloudEnabled(true); }, 290, 38, 11), 20, BottomOf(t) + 12);
                return;
            }
            var intro = PutText("PLAY downloads the newest state, starts the game and uploads your progress after you close it. " +
                                "One complete state wins; the overwritten one is backed up as a ZIP first.", 10, FontStyle.Regular,
                                Theme.Text, 20, 52, Math.Min(570, PW - 40));
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
        void PanelOptions()
        {
            ClearPanel("Options");
            SyncSettings ss = SyncSettings();
            var rows = new List<object[]>();
            rows.Add(new object[] { "Game folder", GameDir(), new Action(ChooseGameFolder) });
            rows.Add(new object[] { "Save game folder", ss.SaveDir, new Action(ChooseSaveDir) });
            rows.Add(new object[] { "Game config folder (AddOns, addon settings)", ss.ConfigDir, new Action(ChooseConfigDir) });
            if (CloudEnabled()) rows.Add(new object[] { "Sync folder (inside your cloud drive)", Cfg.Str("cloud_dir"), new Action(ChooseCloudDir) });
            int y = 52;
            foreach (var r in rows)
            {
                var lab = PutText((string)r[0], 11, FontStyle.Bold, Theme.Gold, 20, y, 0);
                string v = (string)r[1];
                var val = PutText(string.IsNullOrEmpty(v) ? Lang.T("(not set)") : v, 9, FontStyle.Regular, Theme.LinkBlue, 20, BottomOf(lab), 400, false);
                int h = BottomOf(val) - y;
                Put(new WowButton("CHANGE", (Action)r[2], 100, 28, 9), PW - 20 - 100, y + Math.Max(0, (h - 28) / 2));
                y = Math.Max(BottomOf(val), y + 30) + 5;
            }
            Put(UI.Line(Theme.Bronze, PW - 40, 1), 20, y + 4);
            y += 15;
            var checks = new[] {
                new object[] { "cloud_enabled", CloudEnabled(), "Cloud saving" },
                new object[] { "close_to_tray", Cfg.Bool("close_to_tray", true), "Close button hides the launcher to the tray" },
                new object[] { "minimize_on_play", Cfg.Bool("minimize_on_play", true), "Minimize the launcher when the game starts" },
                new object[] { "stats_enabled", StatsEnabled(), "Show the Stats section (your play time and run records)" } };
            foreach (var c in checks)
            {
                string key = (string)c[0];
                var cb = UI.Check((string)c[2], (bool)c[1], Theme.Text, Theme.Panel, 11);
                cb.CheckedChanged += delegate
                {
                    if (key == "cloud_enabled")
                    {
                        bool on = cb.Checked;
                        BeginInvoke(new Action(delegate { SetCloudEnabled(on); }));
                        return;
                    }
                    if (key == "stats_enabled")
                    {
                        bool on = cb.Checked;
                        BeginInvoke(new Action(delegate { SetStatsEnabled(on); }));
                        return;
                    }
                    Cfg.Set(key, cb.Checked);
                    Cfg.Save();
                };
                cb.Size = VStack.Measure(cb);
                Put(cb, 18, y);
                y += cb.Height + 4;
            }
            // language: every language built into the launcher, in its own name
            var langs = Lang.Available();
            string langName = "English";
            foreach (var kv in langs) if (kv.Key == Lang.Code) langName = kv.Value;
            var langLab = PutText("Language", 11, FontStyle.Bold, Theme.Gold, 20, y + 9, 0);
            WowButton langBtn = null;
            langBtn = Put(new WowButton(langName, delegate
            {
                var menu = new ContextMenuStrip();
                foreach (var kv in langs)
                {
                    string code = kv.Key;
                    var item = new ToolStripMenuItem(kv.Value, null, delegate { SetLanguage(code); });
                    item.Checked = code == Lang.Code;
                    menu.Items.Add(item);
                }
                menu.Show(langBtn, new Point(0, langBtn.Height));
            }, 220, 30, 10), 20 + langLab.Width + 14, y + 4);
            y += 40;
            // play time
            Put(UI.Line(Theme.Bronze, PW - 40, 1), 20, y + 4);
            y += 15;
            var head = PutText("Play time", 11, FontStyle.Bold, Theme.Gold, 20, y, 0);
            PlayStats ps = ShownPlayStats();
            long sessions = ps.Sessions;
            bool shared = CloudEnabled() && cloudPlay != null;
            string stats = sessions == 0 ? "No games played with this launcher yet." :
                "Total: " + Duration(ps.Seconds) + " over " + Util.Plural((int)sessions, "session") +
                (shared && ps.Devices > 1 ? " on " + ps.Devices + " PCs" : "") + "\n" +
                "Last session: " + Duration(ps.LastSeconds) + " (" + ps.LastDate + ")\n" +
                "Longest session: " + Duration(ps.Longest);
            if (sessions > 0 && StatsEnabled())             // the details are on the STATS page
                stats = "Total: " + Duration(ps.Seconds) + " over " + Util.Plural((int)sessions, "session") +
                        (shared && ps.Devices > 1 ? " on " + ps.Devices + " PCs" : "") + "\n" +
                        "Your sessions and records are on the STATS page.";
            else
                stats += shared ? "\nShared with your other PCs through the cloud sync folder." :
                         CloudEnabled() ? "\nCounted on this PC (the sync folder is not reachable right now)." :
                         "\nCounted on this PC. Turn on cloud saving to share it between your PCs.";
            PutText(stats, 10, FontStyle.Regular, Theme.Text, 20, BottomOf(head) + 2, PW - 40);
            if (sessions > 0)
                Put(new WowButton("RESET PLAY TIME", delegate
                {
                    if (Msg.Show(side, shared ? "Set the play time back to zero on all your PCs?" : "Set the play time back to zero?",
                                        "Play time", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        ResetPlayTime();
                        BuildPanel("Options");
                    }
                }, 190, 30, 9), 20, PH - 10 - 30);
            Put(new WowButton("RUN SETUP AGAIN", delegate { RunSetup(); }, 190, 30, 9), PW - 20 - 190, PH - 10 - 30);
        }

        void ChooseGameFolder()
        {
            string d = AskFolder(side, "Select your Azeroth Survivors game folder", GameDir());
            if (d == null) return;
            Cfg.Set("game_dir", d);
            Cfg.Save();
            SetStatus("Game folder set: " + d);
            RefreshPlayLabel();                              // PLAY/INSTALL and the Cloud Save button follow the new folder
            Invalidate();
            RebuildCurrent();
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
