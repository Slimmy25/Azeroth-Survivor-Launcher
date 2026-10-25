// The New Player Guide (assets\guide.txt): topic cards with a "read" progress bar, topics made of pages, and
// article pages with headings, bullets, tip boxes and video cards.
//
// Videos: put a line "@video Title | address" anywhere in a topic or page of guide.txt; the video card shows up
// at that spot. The address can be a YouTube link (its picture is fetched from YouTube), any other web address,
// or a video file. A file name alone is looked for in the "Guide Videos" folder next to the launcher's data.
// Clicking the card opens the video in the browser or the PC's video player.
//
// The guide's own text is English; only the words around it are translated.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        static readonly Color GuideCardBg = Theme.Hex("#15121c"), GuideRed = Theme.Hex("#c8442f");

        void SetGuideView(int topic, int page)
        {
            gTopic = topic;
            gPage = page;
            if (current == "Guide") BuildPanel("Guide");
        }

        // ---- what has been read (kept in the config: "guide_read", a list of topic and topic/page titles)
        HashSet<string> GuideRead()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            List<object> l = Json.AsList(Json.Get(Cfg.Data, "guide_read"));
            if (l != null) foreach (object o in l) if (o is string) set.Add((string)o);
            return set;
        }

        void MarkGuideRead(string key)
        {
            var set = GuideRead();
            if (set.Contains(key)) return;
            set.Add(key);
            var l = new List<object>();
            foreach (string k in set) l.Add(k);
            Cfg.Set("guide_read", l);
            if (!PreviewMode) Cfg.Save();
        }

        bool TopicRead(GuideTopic t, HashSet<string> read)
        {
            if (t.Pages.Count == 0) return read.Contains(t.Title);
            foreach (var p in t.Pages) if (!read.Contains(t.Title + "/" + p.Title)) return false;
            return true;
        }

        static int CountVideos(GuideTopic t)
        {
            int n = t.Videos.Count;
            foreach (var p in t.Pages) n += p.Videos.Count;
            return n;
        }

        void PanelGuide()
        {
            var topics = Data.Guide;
            if (gTopic >= topics.Count) { gTopic = -1; gPage = -1; }
            var read = GuideRead();
            if (gTopic < 0) { GuideHome(topics, read); return; }
            GuideTopic topic = topics[gTopic];
            if (gPage >= topic.Pages.Count) gPage = -1;
            if (gPage < 0 && topic.Pages.Count > 0) { GuideTopicPages(topic, read); return; }
            GuideArticle(topics, topic);
        }

        // ---- first page: progress, "continue" banner and the topic cards
        void GuideHome(List<GuideTopic> topics, HashSet<string> read)
        {
            ClearPanel("New Player Guide");
            int done = 0, next = -1;
            for (int i = 0; i < topics.Count; i++)
            {
                if (TopicRead(topics[i], read)) done++;
                else if (next < 0) next = i;
            }
            int barW = Math.Min(260, PW / 3);
            var prog = new GuideProgress(Lang.T(done + " of " + topics.Count + " topics read"), done, topics.Count, barW, 30);
            Put(prog, PW - 20 - barW, 12);

            var sp = MakeThinScroll(panel, new Rectangle(20, 52, PW - 40, PH - 12 - 52), Theme.Panel, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            int w = sp.ClientSize.Width - 8, y = 4;
            if (next >= 0)
            {
                int ni = next;
                var hero = new GuideCard(next == 0 && done == 0 ? "START HERE" : "CONTINUE", topics[next].Title,
                                         topics[next].Desc, 0, w, 74);
                hero.Hero = true;
                hero.Videos = CountVideos(topics[next]);
                hero.Pressed += delegate { SetGuideView(ni, -1); };
                hero.Location = new Point(4, y);
                sp.Controls.Add(hero);
                y += hero.Height + 12;
            }
            GuideGrid(sp, y, w, topics.Count, delegate(int i, GuideCard c)
            {
                c.Title = topics[i].Title;
                c.Desc = topics[i].Desc;
                c.Videos = CountVideos(topics[i]);
                c.Read = TopicRead(topics[i], read);
                if (topics[i].Pages.Count > 0)
                {
                    int r = 0;
                    foreach (var p in topics[i].Pages) if (read.Contains(topics[i].Title + "/" + p.Title)) r++;
                    c.Pages = topics[i].Pages.Count;
                    c.PagesRead = r;
                }
                c.Pressed += delegate { SetGuideView(i, -1); };
            });
        }

        // ---- a topic made of pages (Classes): a card for each page
        void GuideTopicPages(GuideTopic topic, HashSet<string> read)
        {
            ClearPanel(topic.Title);
            Put(new WowButton("‹ ALL TOPICS", delegate { SetGuideView(-1, -1); }, 170, 32, 10), PW - 20 - (int)(170 * Theme.Zoom), 12);
            var sp = MakeThinScroll(panel, new Rectangle(20, 52, PW - 40, PH - 12 - 52), Theme.Panel, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            int w = sp.ClientSize.Width - 8, y = 4;
            if (topic.Desc.Length > 0)
            {
                var d = UI.Text(topic.Desc, 11, FontStyle.Italic, Theme.GoldDim, sp.BackColor, w - 8, false);
                d.Size = VStack.Measure(d);
                d.Location = new Point(6, y);
                sp.Controls.Add(d);
                y += d.Height + 10;
            }
            int ti = gTopic;
            GuideGrid(sp, y, w, topic.Pages.Count, delegate(int i, GuideCard c)
            {
                c.Title = topic.Pages[i].Title;
                c.Desc = topic.Pages[i].Desc;
                c.Videos = topic.Pages[i].Videos.Count;
                c.Read = read.Contains(topic.Title + "/" + topic.Pages[i].Title);
                c.Pressed += delegate { SetGuideView(ti, i); };
            });
        }

        // cards two to a row, numbered
        void GuideGrid(ScrollPanel sp, int y, int w, int count, Action<int, GuideCard> fill)
        {
            const int gap = 10;
            int cw = (w - gap) / 2, ch = (int)(108 * Theme.Zoom);
            for (int i = 0; i < count; i++)
            {
                var c = new GuideCard("", "", "", i + 1, cw, ch);
                fill(i, c);
                c.Location = new Point(4 + (i % 2) * (cw + gap), y + (i / 2) * (ch + gap));
                sp.Controls.Add(c);
            }
            var pad = new Panel();                           // a little room under the last row
            pad.BackColor = sp.BackColor;
            pad.Bounds = new Rectangle(4, y + ((count + 1) / 2) * (ch + gap), 1, 6);
            sp.Controls.Add(pad);
        }

        // ---- one topic or page as an article
        void GuideArticle(List<GuideTopic> topics, GuideTopic topic)
        {
            bool inPage = gPage >= 0;
            GuidePage shown = inPage ? topic.Pages[gPage] : topic;
            MarkGuideRead(inPage ? topic.Title + "/" + shown.Title : topic.Title);
            ClearPanel(shown.Title);
            int bw = (int)(170 * Theme.Zoom);
            if (inPage) Put(new WowButton("‹ " + topic.Title.ToUpperInvariant(), delegate { SetGuideView(gTopic, -1); }, 170, 32, 9), PW - 20 - bw, 12);
            else Put(new WowButton("‹ ALL TOPICS", delegate { SetGuideView(-1, -1); }, 170, 32, 10), PW - 20 - bw, 12);

            int count = inPage ? topic.Pages.Count : topics.Count, at = inPage ? gPage : gTopic;
            int bh = (int)(34 * Theme.Zoom), by = PH - 12 - bh, tIndex = gTopic;
            if (at > 0)
                Put(new WowButton("‹ PREVIOUS", delegate { if (inPage) SetGuideView(tIndex, at - 1); else SetGuideView(at - 1, -1); }, 150, 34, 10), 20, by);
            if (at + 1 < count)
                Put(new WowButton("NEXT ›", delegate { if (inPage) SetGuideView(tIndex, at + 1); else SetGuideView(at + 1, -1); }, 150, 34, 10), PW - 20 - (int)(150 * Theme.Zoom), by);
            var dots = new GuideDots(count, at, PW / 3, bh);
            Put(dots, (PW - dots.Width) / 2, by);

            var sp = MakeThinScroll(panel, new Rectangle(20, 52, PW - 40, by - 10 - 52), GuideCardBg, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            GuideBody(sp, inPage ? topic.Title : "Guide", shown);
        }

        void GuideBody(ScrollPanel sp, string crumb, GuidePage page)
        {
            Color bg = sp.BackColor;
            int w = sp.ClientSize.Width - 8, tw = w - 40, x = 18, y = 14;
            Action<Control, int, int> add = delegate(Control c, int cx, int gap)
            {
                if (c is Label) c.Size = VStack.Measure((Label)c);
                c.Location = new Point(cx, y + gap);
                sp.Controls.Add(c);
                y += gap + c.Height;
            };
            add(UI.Text(crumb.ToUpperInvariant(), 8.5f, FontStyle.Bold, GuideRed, bg, tw, crumb == "Guide"), x, 0);
            add(UI.Text(page.Title, 18, FontStyle.Bold, Theme.Gold, bg, tw, false), x, 2);
            if (page.Desc.Length > 0) add(UI.Text(page.Desc, 10.5f, FontStyle.Italic, Theme.GoldDim, bg, tw, false), x, 2);
            add(new GuideRule(tw, Theme.GoldDim), x, 10);

            var para = new List<string>();
            Action flush = delegate
            {
                if (para.Count == 0) return;
                add(UI.Text(string.Join(" ", para.ToArray()), 10, FontStyle.Regular, Theme.Text, bg, tw, false), x, 8);
                para.Clear();
            };
            foreach (string raw in page.Body.Split('\n'))
            {
                string line = raw.Trim();
                int vi;
                if (line.Length == 0) { flush(); continue; }
                if (line.StartsWith("@video ") && int.TryParse(line.Substring(7), out vi) && vi < page.Videos.Count)
                {
                    flush();
                    add(new VideoCard(page.Videos[vi], tw), x, 12);
                    y += 2;
                }
                else if (line.StartsWith("# "))
                {
                    flush();
                    add(UI.Text(line.Substring(2).Trim(), 12.5f, FontStyle.Bold, Theme.Gold, bg, tw, false), x, 16);
                    add(new GuideRule(Math.Min(60, tw), Theme.Gold), x, 3);
                }
                else if (line.StartsWith("- "))
                {
                    flush();
                    var dot = new GuideRule(6, Theme.Gold);
                    dot.Diamond = true;
                    dot.Height = 6;
                    var l = UI.Text(line.Substring(2).Trim(), 10, FontStyle.Regular, Theme.Text, bg, tw - 20, false);
                    l.Size = VStack.Measure(l);
                    int lineH = Theme.F(10).Height;
                    dot.Location = new Point(x + 3, y + 6 + (lineH - 6) / 2);
                    sp.Controls.Add(dot);
                    add(l, x + 20, 6);
                }
                else if (line.StartsWith("\""))
                {
                    flush();
                    add(new GuideTip(line.Trim('"'), tw), x, 10);
                }
                else para.Add(line);
            }
            flush();
            var pad = new Panel();
            pad.BackColor = bg;
            pad.Bounds = new Rectangle(4, y, 1, 18);
            sp.Controls.Add(pad);
        }
    }

    // ------------------------------------------------------------------ the guide's painted pieces
    // a clickable card: number (or a label for the big one), title, description and small badges
    public class GuideCard : Control
    {
        public string Title, Desc, Label;
        public int Number, Videos, Pages, PagesRead;
        public bool Read, Hero;
        public event Action Pressed;
        bool hover;
        readonly float zoom;

        public GuideCard(string label, string title, string desc, int number, int w, int h)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Label = label; Title = title; Desc = desc; Number = number;
            zoom = Theme.Zoom;
            Size = new Size(w, h);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) WowButton.Fire(Pressed);
        }

        internal static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // a small rounded label; returns its width
        internal static float Pill(Graphics g, string text, float x, float y, Color fill, Color fg, Font f)
        {
            SizeF s = g.MeasureString(text, f);
            var r = new RectangleF(x, y, s.Width + 8, s.Height + 2);
            using (var p = Round(r, r.Height / 2))
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
            using (var b = new SolidBrush(fg)) g.DrawString(text, f, b, x + 4, y + 1);
            return r.Width;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float old = Theme.Zoom;
            Theme.Zoom = zoom;
            try { Draw(e.Graphics); }
            finally { Theme.Zoom = old; }
        }

        void Draw(Graphics g)
        {
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            Color top = Hero ? Theme.Hex("#3a2a12") : Theme.Hex("#1b1724"), bot = Hero ? Theme.Hex("#160f07") : Theme.Hex("#110e17");
            if (hover) { top = Theme.Mix(top, Theme.Gold, 0.10); bot = Theme.Mix(bot, Theme.Gold, 0.05); }
            using (var path = Round(r, 7))
            {
                using (var lg = new LinearGradientBrush(r, top, bot, LinearGradientMode.Vertical)) g.FillPath(lg, path);
                using (var pen = new Pen(hover || Hero ? Theme.Gold : Theme.Hex("#3d3647"), hover ? 1.5f : 1f)) g.DrawPath(pen, path);
            }
            float x = 14;
            Font small = Theme.F(7.5f, FontStyle.Bold);
            if (Number > 0)
            {
                float d = 30 * zoom + 4, cy = 14;
                var c = new RectangleF(x, cy, d, d);
                using (var b = new SolidBrush(Read ? Theme.Hex("#2f4a1c") : Theme.Hex("#2a2016"))) g.FillEllipse(b, c);
                using (var pen = new Pen(Read ? Theme.Good : Theme.GoldDim, 1.5f)) g.DrawEllipse(pen, c);
                Theme.CenterText(g, Read ? "✓" : Number.ToString(), Theme.F(Read ? 12 : 11, FontStyle.Bold),
                                 Read ? Theme.Good : Theme.Gold, c, null);
                x += d + 12;
            }
            float y = 12, tw = Width - x - 14;
            if (!string.IsNullOrEmpty(Label))
            {
                Pill(g, Lang.T(Label), x, y, GuideTheme.Red, Color.White, small);
                y += small.Height + 6;
            }
            Font tf = Theme.F(Hero ? 14 : 11.5f, FontStyle.Bold);
            using (var b = new SolidBrush(Theme.Gold)) g.DrawString(Title, tf, b, new RectangleF(x, y, tw, tf.Height + 2));
            y += tf.Height + 1;
            Font df = Theme.F(9);
            float badgesY = Height - small.Height - 12;
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(Theme.Text))
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                sf.FormatFlags = StringFormatFlags.LineLimit;   // only whole lines, the last one ending in "..."
                float dh = Hero ? Height - y - 8 : badgesY - y - 3;
                if (dh > df.Height * 0.8f) g.DrawString(Desc ?? "", df, b, new RectangleF(x, y, tw, dh), sf);
            }
            // badges along the bottom (the big card has them at its right)
            float bx = Hero ? Width - 14 : x, by = Hero ? (Height - small.Height) / 2f : badgesY;
            var badges = new List<KeyValuePair<string, Color>>();
            if (Videos > 0) badges.Add(new KeyValuePair<string, Color>("▶ " + Lang.T(Videos == 1 ? "1 VIDEO" : Videos + " VIDEOS"), GuideTheme.Red));
            if (Pages > 0) badges.Add(new KeyValuePair<string, Color>(Lang.T(PagesRead + "/" + Pages + " PAGES"), Theme.Hex("#3d3647")));
            if (Read && !Hero) badges.Add(new KeyValuePair<string, Color>(Lang.T("READ"), Theme.Hex("#2f4a1c")));
            if (Hero) badges.Add(new KeyValuePair<string, Color>(Lang.T("OPEN") + " ›", Theme.Hex("#5a4318")));
            foreach (var kv in badges)
            {
                if (Hero) bx -= g.MeasureString(kv.Key, small).Width + 8;
                float bw = Pill(g, kv.Key, bx, by, kv.Value, Theme.Hex("#fff2d8"), small);
                if (Hero) bx -= 6; else bx += bw + 6;
            }
        }
    }

    static class GuideTheme
    {
        public static readonly Color Red = Theme.Hex("#c8442f");
    }

    // "3 of 13 topics read" over a thin gold bar
    public class GuideProgress : Control
    {
        readonly string text;
        readonly int done, total;
        readonly float zoom;

        public GuideProgress(string text, int done, int total, int w, int h)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.text = text; this.done = done; this.total = total;
            zoom = Theme.Zoom;
            Size = new Size(w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float old = Theme.Zoom;
            Theme.Zoom = zoom;
            try
            {
                Graphics g = e.Graphics;
                g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
                Theme.Smooth(g);
                Font f = Theme.F(9, FontStyle.Italic);
                using (var sf = new StringFormat())
                using (var b = new SolidBrush(Theme.GoldDim))
                {
                    sf.Alignment = StringAlignment.Far;
                    g.DrawString(text, f, b, new RectangleF(0, 0, Width, f.Height + 2), sf);
                }
                var r = new RectangleF(1, Height - 8, Width - 2, 6);
                using (var p = GuideCard.Round(r, 3))
                using (var b = new SolidBrush(Theme.Hex("#2a2533"))) g.FillPath(b, p);
                if (total > 0 && done > 0)
                {
                    var fr = new RectangleF(r.X, r.Y, Math.Max(6, r.Width * done / total), r.Height);
                    using (var p = GuideCard.Round(fr, 3))
                    using (var b = new LinearGradientBrush(fr, Theme.Hex("#c9821a"), Theme.Gold, LinearGradientMode.Horizontal))
                        g.FillPath(b, p);
                }
            }
            finally { Theme.Zoom = old; }
        }
    }

    // page dots between PREVIOUS and NEXT
    public class GuideDots : Control
    {
        readonly int count, at;

        public GuideDots(int count, int at, int maxW, int h)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.count = count; this.at = at;
            Size = new Size(Math.Min(maxW, count * 14 + 10), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            float step = Math.Min(14f, (Width - 10f) / Math.Max(1, count)), x0 = (Width - step * count) / 2f, cy = Height / 2f;
            for (int i = 0; i < count; i++)
            {
                float d = i == at ? 8 : 5, cx = x0 + step * i + step / 2;
                using (var b = new SolidBrush(i == at ? Theme.Gold : Theme.Hex("#4a4356")))
                    g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
            }
        }
    }

    // a thin line under a heading (or a small diamond for a bullet)
    public class GuideRule : Control
    {
        readonly Color color;
        public bool Diamond;

        public GuideRule(int w, Color color)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.color = color;
            Size = new Size(w, 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            using (var b = new SolidBrush(color))
            {
                if (Diamond)
                    g.FillPolygon(b, new[] { new PointF(Width / 2f, 0), new PointF(Width, Height / 2f),
                                             new PointF(Width / 2f, Height), new PointF(0, Height / 2f) });
                else
                    using (var lg = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), color,
                                                            Color.FromArgb(0, color), LinearGradientMode.Horizontal))
                        g.FillRectangle(lg, 0, 0, Width, Height);
            }
        }
    }

    // a line in quotes: a tip box with a gold bar on its left
    public class GuideTip : Panel
    {
        public GuideTip(string text, int w)
        {
            BackColor = Theme.Hex("#221a10");
            Padding = new Padding(0);
            var bar = new Panel();
            bar.BackColor = Theme.Gold;
            var head = UI.Text("TIP", 8, FontStyle.Bold, Theme.Gold, BackColor, 0);
            head.Size = VStack.Measure(head);
            head.Location = new Point(14, 8);
            var l = UI.Text(text, 10, FontStyle.Italic, Theme.Hex("#eadfb8"), BackColor, w - 28, false);
            l.Size = VStack.Measure(l);
            l.Location = new Point(14, head.Bottom + 2);
            Size = new Size(w, l.Bottom + 9);
            bar.Bounds = new Rectangle(0, 0, 3, Height);
            Controls.Add(bar);
            Controls.Add(head);
            Controls.Add(l);
        }
    }

    // a video: a picture (YouTube's own, when it is a YouTube link) with a play button, its title and where it plays
    public class VideoCard : Control
    {
        static readonly Dictionary<string, Image> thumbs = new Dictionary<string, Image>();
        static readonly Regex YouTubeId = new Regex(@"(?:youtu\.be/|[?&]v=|/embed/|/shorts/|/live/)([A-Za-z0-9_-]{11})");

        readonly GuideVideo video;
        readonly string ytId, file;
        readonly float zoom;
        bool hover;

        public static string VideoFolder { get { return Path.Combine(Util.DataDir, "Guide Videos"); } }

        public VideoCard(GuideVideo v, int w)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            video = v;
            zoom = Theme.Zoom;
            Match m = YouTubeId.Match(v.Url);
            ytId = m.Success && v.Url.IndexOf("youtu", StringComparison.OrdinalIgnoreCase) >= 0 ? m.Groups[1].Value : null;
            if (!Net.IsWebAddress(v.Url))
                file = Path.IsPathRooted(v.Url) ? v.Url : Path.Combine(VideoFolder, v.Url);
            int th = Math.Min(150, (int)(w * 0.36 * 9 / 16));
            Size = new Size(w, th + 16);
            Cursor = Cursors.Hand;
            if (ytId != null) LoadThumb();
        }

        void LoadThumb()
        {
            lock (thumbs) if (thumbs.ContainsKey(ytId)) return;
            string id = ytId;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Image img = null;
                try
                {
                    string cache = Path.Combine(Util.TempDir, "guide-thumbs");
                    Directory.CreateDirectory(cache);
                    string path = Path.Combine(cache, id + ".jpg");
                    if (!File.Exists(path) && !LauncherApp.PreviewMode)
                        using (var resp = Net.Open("https://img.youtube.com/vi/" + id + "/hqdefault.jpg", 10000, true, "video picture"))
                        using (Stream s = resp.GetResponseStream())
                        using (var fs = File.Create(path + ".part"))
                        {
                            s.CopyTo(fs);
                            fs.Close();
                            File.Move(path + ".part", path);
                        }
                    if (File.Exists(path))
                        using (var tmp = Image.FromFile(path)) img = new Bitmap(tmp);
                }
                catch (Exception) { }
                lock (thumbs) thumbs[id] = img;
                try { if (IsHandleCreated) BeginInvoke(new Action(Invalidate)); }
                catch (Exception) { }
            });
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !ClientRectangle.Contains(e.Location)) return;
            if (file == null) { Util.OpenInShell(video.Url); return; }
            if (File.Exists(file)) { Util.OpenInShell(file); return; }
            try { Directory.CreateDirectory(VideoFolder); } catch (Exception) { }
            Util.OpenInShell(VideoFolder);                   // the file is missing: show where it belongs
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float old = Theme.Zoom;
            Theme.Zoom = zoom;
            try { Draw(e.Graphics); }
            finally { Theme.Zoom = old; }
        }

        void Draw(Graphics g)
        {
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (var path = GuideCard.Round(r, 8))
            {
                using (var b = new SolidBrush(hover ? Theme.Hex("#2a2016") : Theme.Hex("#1d1812"))) g.FillPath(b, path);
                using (var pen = new Pen(hover ? Theme.Gold : Theme.Hex("#4a3a22"), hover ? 1.5f : 1f)) g.DrawPath(pen, path);
            }
            // the picture, 16:9
            int th = Height - 16, tw = th * 16 / 9;
            var pic = new Rectangle(8, 8, tw, th);
            Image img = null;
            if (ytId != null) lock (thumbs) thumbs.TryGetValue(ytId, out img);
            using (var clip = GuideCard.Round(pic, 5))
            {
                g.SetClip(clip);
                if (img != null)
                {
                    // YouTube's picture has black bars above and below: fill the box with its middle
                    float s = Math.Max((float)tw / img.Width, (float)th / (img.Height * 0.75f));
                    float iw = img.Width * s, ih = img.Height * s;
                    g.DrawImage(img, pic.X + (tw - iw) / 2, pic.Y + (th - ih) / 2, iw, ih);
                    if (!hover) using (var dim = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillRectangle(dim, pic);
                }
                else
                    using (var lg = new LinearGradientBrush(pic, Theme.Hex("#3b1f4a"), Theme.Hex("#120a18"), 60f)) g.FillRectangle(lg, pic);
                g.ResetClip();
            }
            // play button
            float pd = Math.Min(46, th * 0.42f), pcx = pic.X + tw / 2f, pcy = pic.Y + th / 2f;
            using (var b = new SolidBrush(hover ? GuideTheme.Red : Color.FromArgb(220, 30, 22, 18)))
                g.FillEllipse(b, pcx - pd / 2, pcy - pd / 2, pd, pd);
            using (var pen = new Pen(Theme.Gold, 1.5f)) g.DrawEllipse(pen, pcx - pd / 2, pcy - pd / 2, pd, pd);
            float t = pd * 0.32f;
            using (var b = new SolidBrush(Color.White))
                g.FillPolygon(b, new[] { new PointF(pcx - t * 0.7f, pcy - t), new PointF(pcx - t * 0.7f, pcy + t), new PointF(pcx + t, pcy) });
            // text on the right
            float x = pic.Right + 16, y = 14, w = Width - x - 14;
            Font small = Theme.F(7.5f, FontStyle.Bold);
            GuideCard.Pill(g, "▶ " + Lang.T("VIDEO"), x, y, GuideTheme.Red, Color.White, small);
            y += small.Height + 10;
            Font tf = Theme.F(12, FontStyle.Bold);
            string title = video.Title.Length > 0 ? video.Title : Lang.T("Watch the video");
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(Theme.Gold))
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                SizeF ts = g.MeasureString(title, tf, (int)w);
                float h = Math.Min(ts.Height, tf.Height * 2.2f);
                g.DrawString(title, tf, b, new RectangleF(x, y, w, h), sf);
                y += h + 6;
            }
            string where = file == null ? (ytId != null ? "Watch on YouTube ›" : "Open in your browser ›")
                         : File.Exists(file) ? "Play on this PC ›" : "Video file not found - click to open its folder";
            using (var b = new SolidBrush(hover ? Theme.Hex("#fff2b0") : Theme.GoldDim))
                g.DrawString(Lang.T(where), Theme.F(9, FontStyle.Italic), b, new RectangleF(x, y, w, Height - y - 6));
        }
    }
}
