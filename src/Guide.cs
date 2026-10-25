// The New Player Guide (assets\guide.txt): topic cards (every topic open at any time, nothing tracked), topics made of pages, and
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

        int GuideTop { get { return (int)(12 + 32 * Theme.Zoom) + 10; } }   // under the back arrow row

        void SetGuideView(int topic, int page)
        {
            gVideo = null;
            gTopic = topic;
            gPage = page;
            if (current == "Guide") BuildPanel("Guide");
        }

        static int CountVideos(GuideTopic t)
        {
            int n = t.Videos.Count;
            foreach (var p in t.Pages) n += p.Videos.Count;
            return n;
        }

        void PanelGuide()
        {
            if (VideoCard.Embed == null) VideoCard.Embed = delegate(GuideVideo v) { gVideo = v; BuildPanel("Guide"); return true; };
            if (Cfg.Data.ContainsKey("guide_read")) { Cfg.Remove("guide_read"); if (!PreviewMode) Cfg.Save(); }   // no longer tracked
            if (gVideo != null) { GuideVideoPlayer(gVideo); return; }
            var topics = Data.Guide;
            if (gTopic >= topics.Count) { gTopic = -1; gPage = -1; }
            if (gTopic < 0) { GuideHome(topics); return; }
            GuideTopic topic = topics[gTopic];
            if (gPage >= topic.Pages.Count) gPage = -1;
            if (gPage < 0 && topic.Pages.Count > 0) { GuideTopicPages(topic); return; }
            GuideArticle(topics, topic);
        }

        // ---- a video streamed inside the section (the same address as the card: nothing is saved on the PC)
        void GuideVideoPlayer(GuideVideo v)
        {
            ClearPanel(v.Title.Length > 0 ? v.Title : "Video");
            int bw = (int)(190 * Theme.Zoom);
            string url = v.Url;
            Put(new WowButton("OPEN IN BROWSER ›", delegate { Util.OpenInShell(url); }, 190, 32, 9), PW - 20 - bw, 12);
            var frame = new Bordered(Theme.CardEdge, 1);
            frame.Bounds = new Rectangle(20, GuideTop, PW - 40, PH - 12 - GuideTop);
            Put(frame, 20, 52);
            var web = VideoView.Make(v);
            web.Dock = DockStyle.Fill;
            frame.Controls.Add(web);
        }

        // ---- first page: a card for every topic, all open at any time
        string guideQuery = "";

        void GuideHome(List<GuideTopic> topics)
        {
            ClearPanel("New Player Guide");
            // the search box, beside the back arrow: every topic and page, titles and text
            int sx = 20 + (int)(52 * Theme.Zoom) + 12, sh = (int)(32 * Theme.Zoom);
            var search = new GuideSearchBox(PW - 20 - sx, sh);
            search.Text = guideQuery;
            Put(search, sx, 12);
            var sp = MakeThinScroll(panel, new Rectangle(20, GuideTop, PW - 40, PH - 12 - GuideTop), Theme.Panel, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            Action fill = delegate
            {
                sp.SuspendLayout();
                while (sp.Controls.Count > 0) { Control c = sp.Controls[0]; sp.Controls.RemoveAt(0); c.Dispose(); }
                int w = sp.ClientSize.Width - 8, y = 4;
                if (guideQuery.Trim().Length == 0)
                    GuideGrid(sp, y, w, topics.Count, delegate(int i, GuideCard c)
                    {
                        c.Title = topics[i].Title;
                        c.Desc = topics[i].Desc;
                        c.Videos = CountVideos(topics[i]);
                        c.Pages = topics[i].Pages.Count;
                        c.Pressed += delegate { SetGuideView(i, -1); };
                    });
                else GuideResults(sp, w, topics, guideQuery.Trim());
                sp.ResumeLayout();
            };
            search.TextChanged += delegate { guideQuery = search.Text; fill(); };
            fill();
            if (guideQuery.Length > 0) { search.Focus(); search.SelectionStart = search.TextLength; }
        }

        // every topic and page whose title or text has all the words searched for; the line it was found in is shown
        void GuideResults(ScrollPanel sp, int w, List<GuideTopic> topics, string q)
        {
            string[] words = q.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var hits = new List<KeyValuePair<int, int>>();   // topic, page (-1: the topic itself)
            var lines = new List<string>();
            for (int t = 0; t < topics.Count; t++)
                for (int p = -1; p < topics[t].Pages.Count; p++)
                {
                    GuidePage pg = p < 0 ? (GuidePage)topics[t] : topics[t].Pages[p];
                    string all = (pg.Title + " " + pg.Desc + " " + pg.Body).ToLowerInvariant();
                    bool ok = true;
                    foreach (string wd in words) if (all.IndexOf(wd, StringComparison.Ordinal) < 0) { ok = false; break; }
                    if (!ok) continue;
                    string line = pg.Desc;
                    foreach (string raw in pg.Body.Split('\n'))
                        if (!raw.StartsWith("@") && raw.ToLowerInvariant().IndexOf(words[0], StringComparison.Ordinal) >= 0)
                        { line = raw.TrimStart('-', '#', ' ', '"').Trim(); break; }
                    hits.Add(new KeyValuePair<int, int>(t, p));
                    lines.Add(line);
                }
            if (hits.Count == 0)
            {
                var none = UI.Text(Lang.T("Nothing in the guide matches") + " \"" + q + "\".", 11, FontStyle.Italic, Theme.GoldDim, sp.BackColor, w - 8, false);
                none.Size = VStack.Measure(none);
                none.Location = new Point(6, 8);
                sp.Controls.Add(none);
                return;
            }
            GuideGrid(sp, 4, w, hits.Count, delegate(int i, GuideCard c)
            {
                var h = hits[i];
                GuideTopic topic = topics[h.Key];
                c.Title = h.Value < 0 ? topic.Title : topic.Pages[h.Value].Title;
                c.Desc = (h.Value < 0 ? "" : topic.Title + " · ") + lines[i];   // where it is, and the line it was found in
                c.Pressed += delegate { SetGuideView(h.Key, h.Value); };
            });
        }

        // from elsewhere in the launcher (a "?" on Stats): the guide page about a spell, or the topic naming a boss
        public void OpenGuideFor(string text)
        {
            var topics = Data.Guide;
            if (string.IsNullOrEmpty(text)) return;
            string low = text.ToLowerInvariant();
            for (int t = 0; t < topics.Count; t++)
                for (int p = 0; p < topics[t].Pages.Count; p++)
                    if (topics[t].Pages[p].Body.ToLowerInvariant().Contains(low)) { OpenGuideAt(t, p); return; }
            for (int t = 0; t < topics.Count; t++)
                if ((topics[t].Title + " " + topics[t].Body).ToLowerInvariant().Contains(low)) { OpenGuideAt(t, -1); return; }
            if (current != "Guide") ShowSection("Guide");   // nothing names it: search for it
            guideQuery = text;
            SetGuideView(-1, -1);
        }

        void OpenGuideAt(int topic, int page)
        {
            if (current != "Guide") ShowSection("Guide");
            SetGuideView(topic, page);
        }

        // ---- a topic made of pages (Classes): a card for each page
        void GuideTopicPages(GuideTopic topic)
        {
            ClearPanel(topic.Title);
            Put(new WowButton("‹ ALL TOPICS", delegate { guideQuery = ""; SetGuideView(-1, -1); }, 170, 32, 10), PW - 20 - (int)(170 * Theme.Zoom), 12);
            var sp = MakeThinScroll(panel, new Rectangle(20, GuideTop, PW - 40, PH - 12 - GuideTop), Theme.Panel, Theme.Bronze,
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

            var sp = MakeThinScroll(panel, new Rectangle(20, GuideTop, PW - 40, by - 10 - GuideTop), GuideCardBg, Theme.Bronze,
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
        public int Number, Videos, Pages;
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
            Color top = Theme.Hex("#1b1724"), bot = Theme.Hex("#110e17");
            if (hover) { top = Theme.Mix(top, Theme.Gold, 0.10); bot = Theme.Mix(bot, Theme.Gold, 0.05); }
            using (var path = Round(r, 7))
            {
                using (var lg = new LinearGradientBrush(r, top, bot, LinearGradientMode.Vertical)) g.FillPath(lg, path);
                using (var pen = new Pen(hover ? Theme.Gold : Theme.Hex("#3d3647"), hover ? 1.5f : 1f)) g.DrawPath(pen, path);
            }
            float x = 14;
            Font small = Theme.F(7.5f, FontStyle.Bold);
            if (Number > 0)
            {
                float d = 30 * zoom + 4, cy = 14;
                var c = new RectangleF(x, cy, d, d);
                using (var b = new SolidBrush(Theme.Hex("#2a2016"))) g.FillEllipse(b, c);
                using (var pen = new Pen(Theme.GoldDim, 1.5f)) g.DrawEllipse(pen, c);
                Theme.CenterText(g, Number.ToString(), Theme.F(11, FontStyle.Bold), Theme.Gold, c, null);
                x += d + 12;
            }
            float y = 12, tw = Width - x - 14;
            if (!string.IsNullOrEmpty(Label))
            {
                Pill(g, Lang.T(Label), x, y, GuideTheme.Red, Color.White, small);
                y += small.Height + 6;
            }
            Font tf = Theme.F(11.5f, FontStyle.Bold);
            using (var b = new SolidBrush(Theme.Gold)) g.DrawString(Title, tf, b, new RectangleF(x, y, tw, tf.Height + 2));
            y += tf.Height + 1;
            Font df = Theme.F(9);
            float badgesY = Height - small.Height - 12;
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(Theme.Text))
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                sf.FormatFlags = StringFormatFlags.LineLimit;   // only whole lines, the last one ending in "..."
                float dh = badgesY - y - 3;
                if (dh > df.Height * 0.8f) g.DrawString(Desc ?? "", df, b, new RectangleF(x, y, tw, dh), sf);
            }
            // badges along the bottom
            float bx = x, by = badgesY;
            var badges = new List<KeyValuePair<string, Color>>();
            if (Videos > 0) badges.Add(new KeyValuePair<string, Color>("▶ " + Lang.T(Videos == 1 ? "1 VIDEO" : Videos + " VIDEOS"), GuideTheme.Red));
            if (Pages > 0) badges.Add(new KeyValuePair<string, Color>(Lang.T(Pages + " PAGES"), Theme.Hex("#3d3647")));
            foreach (var kv in badges)
            {
                float bw = Pill(g, kv.Key, bx, by, kv.Value, Theme.Hex("#fff2d8"), small);
                bx += bw + 6;
            }
        }
    }

    // The video player: Windows' own web view (no extra files) showing a page with a <video> that streams the address.
    // The web view runs in Internet Explorer 7 mode unless told otherwise, and that cannot play video: the launcher
    // asks for the modern (IE 11) mode for itself in the current user's registry (FEATURE_BROWSER_EMULATION).
    public static class VideoView
    {
        static bool modeSet;

        static void UseModernMode()
        {
            if (modeSet) return;
            modeSet = true;
            try
            {
                string exe = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                    if (k != null) k.SetValue(exe, 11001, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (Exception) { }
        }

        static string Html(string s)
        {
            return System.Net.WebUtility.HtmlEncode(s ?? "");
        }

        // what the page tells the launcher (window.external): go fullscreen, or come back, at this point in the video
        [System.Runtime.InteropServices.ComVisible(true)]
        public class Bridge
        {
            public Action<double, double> OnToggle;          // time, volume
            public void Toggle(object time, object volume)
            {
                double t = 0, vol = 0.15;
                try { t = Convert.ToDouble(time, System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { }
                try { vol = Convert.ToDouble(volume, System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { }
                if (OnToggle != null) WowButton.Fire(delegate { OnToggle(t, vol); });
            }
        }

        // the guide's player; fullscreen opens the same video in a window over the whole screen, at the same point
        public static WebBrowser Make(GuideVideo v)
        {
            WebBrowser web = null;
            var bridge = new Bridge();
            bridge.OnToggle = delegate(double t, double vol)
            {
                Pause(web);
                var full = new Form();
                full.FormBorderStyle = FormBorderStyle.None;
                full.StartPosition = FormStartPosition.Manual;
                full.Bounds = Screen.FromControl(web).Bounds;
                full.BackColor = Color.Black;
                full.TopMost = true;
                full.ShowInTaskbar = false;
                full.KeyPreview = true;
                full.Icon = Theme.AppIcon;
                double backAt = t, backVol = vol;
                var fb = new Bridge();
                fb.OnToggle = delegate(double t2, double v2) { backAt = t2; backVol = v2; full.Close(); };
                var fweb = Build(v, t, vol, true, fb);
                fweb.Dock = DockStyle.Fill;
                full.Controls.Add(fweb);
                fweb.PreviewKeyDown += delegate(object s, PreviewKeyDownEventArgs e)
                {
                    if (e.KeyCode == Keys.Escape) { try { Read(fweb, ref backAt, ref backVol); } catch (Exception) { } full.Close(); }
                };
                full.FormClosed += delegate
                {
                    fweb.Dispose();
                    Resume(web, backAt, backVol);
                };
                full.Show();
                full.Activate();
            };
            web = Build(v, 0, 0.15, false, bridge);
            return web;
        }

        static void Pause(WebBrowser w)
        {
            try { if (w != null && w.Document != null) w.Document.InvokeScript("eval", new object[] { "document.getElementById('v').pause()" }); }
            catch (Exception) { }
        }

        static void Read(WebBrowser w, ref double t, ref double vol)
        {
            object r = w.Document.InvokeScript("eval", new object[] { "(function(){var v=document.getElementById('v');return v.currentTime+'|'+v.volume;})()" });
            string[] p = Convert.ToString(r).Split('|');
            t = double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
            vol = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
        }

        static void Resume(WebBrowser w, double t, double vol)
        {
            try
            {
                if (w == null || w.IsDisposed || w.Document == null) return;
                string js = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "(function(){{var v=document.getElementById('v');v.volume={1};v.currentTime={0};v.play();}})()", t, vol);
                w.Document.InvokeScript("eval", new object[] { js });
            }
            catch (Exception) { }
        }

        static WebBrowser Build(GuideVideo v, double start, double volume, bool fullscreen, Bridge bridge)
        {
            UseModernMode();
            var web = new WebBrowser();
            web.ScriptErrorsSuppressed = true;
            web.IsWebBrowserContextMenuEnabled = false;
            web.WebBrowserShortcutsEnabled = false;
            web.AllowWebBrowserDrop = false;
            web.ObjectForScripting = bridge;
            string poster = v.Url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? v.Url.Substring(0, v.Url.Length - 4) + ".jpg" : "";
            string label = fullscreen ? Lang.T("Exit fullscreen") : Lang.T("Fullscreen");
            string page =
                "<!DOCTYPE html>\r\n<!-- saved from url=(0014)about:internet -->\r\n<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\">" +
                "<style>html,body{margin:0;height:100%;background:" + (fullscreen ? "#000" : "#0e0d14") + ";overflow:hidden;font-family:Georgia,serif;color:#d7cea4}" +
                "video{width:100%;height:100%;background:#000;display:block}" +
                "#fs{position:absolute;top:10px;right:10px;padding:5px 12px;background:rgba(14,13,20,.75);color:#f2c14e;border:1px solid #b39b62;" +
                "font:bold 13px Georgia,serif;cursor:pointer;opacity:.35}#fs:hover{opacity:1}" +
                "#err{display:none;position:absolute;left:0;right:0;top:40%;text-align:center;font-size:15px}</style></head><body>" +
                "<video id=\"v\" controls autoplay preload=\"auto\"" + (poster.Length > 0 && start <= 0 ? " poster=\"" + Html(poster) + "\"" : "") +
                " src=\"" + Html(v.Url) + "\"></video>" +
                "<div id=\"fs\" title=\"" + Html(label) + "\">" + (fullscreen ? "&#x2715; " : "&#x26F6; ") + Html(label) + "</div>" +
                "<div id=\"err\">" + Html(Lang.T("This video can't play here. Use OPEN IN BROWSER.")) + "</div>" +
                "<script>var v=document.getElementById('v');" +
                "v.volume=" + volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ";" +       // 15% to start
                "var at=" + start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ";" +
                "v.addEventListener('loadedmetadata',function(){v.volume=" + volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ";if(at>0){try{v.currentTime=at;}catch(e){}}});" +
                "function toggle(){try{window.external.Toggle(v.currentTime,v.volume);}catch(e){}}" +
                "document.getElementById('fs').onclick=toggle;" +
                "v.ondblclick=function(e){toggle();if(e&&e.preventDefault)e.preventDefault();return false;};" +
                "document.onkeydown=function(e){e=e||window.event;if((e.keyCode==27&&" + (fullscreen ? "true" : "false") + ")||e.keyCode==70){toggle();return false;}};" +
                "v.onerror=function(){v.style.display='none';document.getElementById('fs').style.display='none';document.getElementById('err').style.display='block';};" +
                "</script></body></html>";
            // the page (not the video) goes in a small temp file marked as web content, so the web view treats it like a
            // web page and lets it stream from the internet; DocumentText pages count as about:blank and get blocked
            try
            {
                Directory.CreateDirectory(Util.TempDir);
                string path = Path.Combine(Util.TempDir, fullscreen ? "guide-video-full.html" : "guide-video.html");
                File.WriteAllText(path, page, new System.Text.UTF8Encoding(false));
                web.Navigate(new Uri(path));
            }
            catch (Exception) { web.DocumentText = page; }
            return web;
        }
    }

    // the guide's search box: a night-stone field with a gold edge and a hint while it is empty
    public class GuideSearchBox : TextBox
    {
        const int WM_PAINT = 0x000F;

        public GuideSearchBox(int w, int h)
        {
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Font = Theme.F(10.5f);
            Width = w;
            Height = Math.Max(h, Font.Height + 8);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && TextLength == 0)        // the hint while nothing is typed
                using (var g = CreateGraphics())
                    TextRenderer.DrawText(g, "\u2315  " + Lang.T("Search the guide: a spell, a boss, a key..."), Font,
                                          new Rectangle(2, 1, Width - 4, Height - 2), Theme.GoldDim,
                                          TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    static class GuideTheme
    {
        public static readonly Color Red = Theme.Hex("#c8442f");
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
        readonly string ytId, file, thumbKey, thumbUrl;
        readonly float zoom;
        bool hover;

        public static Func<GuideVideo, bool> Embed;          // set by the launcher: plays a web video inside the section
        public static Func<GuideVideo, bool> YouTube;        // set by the launcher: plays a YouTube video (Community.cs)

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
            if (ytId != null) { thumbKey = ytId; thumbUrl = "https://img.youtube.com/vi/" + ytId + "/hqdefault.jpg"; }
            else if (file == null && v.Url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {   // a streamed video: its picture is a .jpg of the same name next to it
                thumbUrl = v.Url.Substring(0, v.Url.Length - 4) + ".jpg";
                thumbKey = Regex.Replace(thumbUrl.Substring(thumbUrl.IndexOf("//") + 2), "[^A-Za-z0-9.-]", "_");
            }
            int th = Math.Min(150, (int)(w * 0.36 * 9 / 16));
            Size = new Size(w, th + 16);
            Cursor = Cursors.Hand;
            if (thumbKey != null) LoadThumb();
        }

        // a YouTube video's picture for others (the home page's Build collection): null until it has loaded, then
        // loaded() is called on a worker thread
        public static string YouTubeIdOf(string url)
        {
            Match m = YouTubeId.Match(url ?? "");
            return m.Success && (url ?? "").IndexOf("youtu", StringComparison.OrdinalIgnoreCase) >= 0 ? m.Groups[1].Value : null;
        }

        public static Image YouTubeThumb(string id, Action loaded)
        {
            lock (thumbs)
            {
                Image img;
                if (thumbs.TryGetValue(id, out img)) return img;
            }
            Fetch(id, "https://img.youtube.com/vi/" + id + "/hqdefault.jpg", loaded);
            return null;
        }

        void LoadThumb()
        {
            lock (thumbs) if (thumbs.ContainsKey(thumbKey)) return;
            Fetch(thumbKey, thumbUrl, delegate
            {
                try { if (IsHandleCreated) BeginInvoke(new Action(Invalidate)); }
                catch (Exception) { }
            });
        }

        static readonly HashSet<string> fetching = new HashSet<string>();

        static void Fetch(string thumbKey, string thumbUrl, Action loaded)
        {
            lock (thumbs) { if (fetching.Contains(thumbKey)) return; fetching.Add(thumbKey); }
            string id = thumbKey, url = thumbUrl;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Image img = null;
                try
                {
                    string cache = Path.Combine(Util.TempDir, "guide-thumbs");
                    Directory.CreateDirectory(cache);
                    string path = Path.Combine(cache, id + ".jpg");
                    if (!File.Exists(path) && !LauncherApp.PreviewMode)
                        using (var resp = Net.Open(url, 10000, true, "video picture"))
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
                lock (thumbs) { thumbs[id] = img; fetching.Remove(id); }
                try { loaded(); }
                catch (Exception) { }
            });
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !ClientRectangle.Contains(e.Location)) return;
            if (file == null && ytId == null && Embed != null && Embed(video)) return;   // streamed inside the launcher
            if (ytId != null && YouTube != null && YouTube(video)) return;              // its own window over the section
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
            if (thumbKey != null) lock (thumbs) thumbs.TryGetValue(thumbKey, out img);
            using (var clip = GuideCard.Round(pic, 5))
            {
                g.SetClip(clip);
                if (img != null)
                {
                    // YouTube's picture has black bars above and below: fill the box with its middle
                    float s = Math.Max((float)tw / img.Width, (float)th / (img.Height * (ytId != null ? 0.75f : 1f)));
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
                y += h + 4;
            }
            if (video.Author.Length > 0)                     // credit to the uploader
            {
                Font af = Theme.F(9.5f);
                using (var b = new SolidBrush(Theme.Text)) g.DrawString(Lang.T("by") + " " + video.Author, af, b, x, y);
                y += af.Height + 4;
            }
            string where = file == null ? (ytId != null ? "Click to watch ›" : "Play here ›")
                         : File.Exists(file) ? "Play on this PC ›" : "Video file not found - click to open its folder";
            using (var b = new SolidBrush(hover ? Theme.Hex("#fff2b0") : Theme.GoldDim))
                g.DrawString(Lang.T(where), Theme.F(9, FontStyle.Italic), b, new RectangleF(x, y, w, Height - y - 6));
        }
    }
}
