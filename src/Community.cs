// The Community section: what the home screen shows whenever no other section is open. It features ONE of the
// players' build videos (the Guide's Build Guides topic in assets/guide.txt), picked at random and changed every
// 10 minutes, with its title and credit to the person who made it. Nothing plays by itself: a click plays it right
// here, inside the section's borders, like the New Player Guide's videos.
//
// Windows' old web view (which the Guide's own videos use) cannot play YouTube any more, so a YouTube video plays in
// Microsoft Edge (part of every Windows 10/11): Edge is started in app mode with a profile of its own (in the
// launcher's data folder), and its window is taken into the section as a child window, without its frame, sized
// to the section. Leaving the video (the back arrow, another section, closing the launcher) closes that Edge.
// Without Edge, the video opens in the web browser instead.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        static int communityPick = -1;                       // the featured video (index in BuildVideos)
        static DateTime communityPicked = DateTime.MinValue;
        GuideVideo communityPlaying;                         // the video playing in the section, or null
        Process edgeVideo;                                   // the Edge that plays it
        Panel edgeHost;                                      // where its window sits
        IntPtr edgeWnd = IntPtr.Zero;
        System.Windows.Forms.Timer communityTimer;
        const int RotateMinutes = 10;

        // every video of the Build Guides topic, newest first, and the topic's index
        static List<GuideVideo> BuildVideos(out int topicIndex)
        {
            topicIndex = -1;
            var list = new List<GuideVideo>();
            List<GuideTopic> topics;
            try { topics = Data.Guide; }
            catch (Exception) { return list; }
            for (int i = 0; i < topics.Count; i++)
            {
                if (!string.Equals(topics[i].Title, "Build Guides", StringComparison.OrdinalIgnoreCase)) continue;
                topicIndex = i;
                list.AddRange(topics[i].Videos);
                foreach (var p in topics[i].Pages) list.AddRange(p.Videos);
                break;
            }
            return list;
        }

        // the featured video: a random one, a different one every 10 minutes (while nothing is playing)
        static GuideVideo Featured(List<GuideVideo> list)
        {
            if (list.Count == 0) return null;
            if (communityPick < 0 || communityPick >= list.Count || (DateTime.Now - communityPicked).TotalMinutes >= RotateMinutes)
            {
                var rnd = new Random();
                int pick = rnd.Next(list.Count);
                if (list.Count > 1 && pick == communityPick) pick = (pick + 1 + rnd.Next(list.Count - 1)) % list.Count;
                communityPick = pick;
                communityPicked = DateTime.Now;
            }
            return list[communityPick];
        }

        // the home screen: Community, unless its X was pressed
        void ShowHomeCommunity()
        {
            if (side == null) return;
            ShowSection("Community");
        }

        void PanelCommunity()
        {
            if (communityTimer == null)                       // every minute: is it time for the next video?
            {
                communityTimer = new System.Windows.Forms.Timer();
                communityTimer.Interval = 60 * 1000;
                communityTimer.Tick += delegate
                {
                    if (current == "Community" && side.Visible && communityPlaying == null &&
                        (DateTime.Now - communityPicked).TotalMinutes >= RotateMinutes) BuildPanel("Community");
                };
                communityTimer.Start();
            }
            if (communityPlaying != null) { CommunityPlayer(communityPlaying); return; }
            ClearPanel("Community");
            int ti;
            GuideVideo v = Featured(BuildVideos(out ti));
            var intro = PutText("A build video made by a player. All credit goes to the people who made it.",
                                9.5f, FontStyle.Italic, Theme.GoldDim, 24, 18, PW - 24 - (int)(200 * Theme.Zoom) - 34);
            int top = Math.Max((int)(12 + 32 * Theme.Zoom) + 12, BottomOf(intro) + 6);
            if (v == null)
            {
                PutText("No community videos yet.", 11, FontStyle.Italic, Theme.GoldDim, 24, top + 8, 0);
                return;
            }
            // the video's title as the heading, the credit under it, then its picture with a play button
            var title = PutText(v.Title.Length > 0 ? v.Title : "Build video", 13, FontStyle.Bold, Theme.Gold, 24, top, PW - 48, false);
            int y = BottomOf(title) + 2;
            if (v.Author.Length > 0)
            {
                var by = PutText(Lang.T("by") + " " + v.Author, 10.5f, FontStyle.Regular, Theme.Text, 24, y, 0, false);
                if (v.AuthorUrl.Length > 0)
                {
                    string au = v.AuthorUrl;
                    by.Cursor = Cursors.Hand;
                    by.Click += delegate { Util.OpenInShell(au); };
                }
                y = BottomOf(by) + 6;
            }
            int h = PH - 12 - y, w = Math.Min(PW - 48, h * 16 / 9);
            var pic = new VideoPoster(v, new Size(w, h));
            pic.Location = new Point((PW - w) / 2, y);
            pic.Click += delegate { PlayYouTube(v); };
            panel.Controls.Add(pic);
            pic.BringToFront();
            if (ti >= 0)
            {
                int topic = ti;
                Put(new WowButton("MORE BUILD GUIDES ›", delegate { ShowSection("Guide"); SetGuideView(topic, -1); }, 200, 30, 9),
                    PW - 20 - (int)(200 * Theme.Zoom), 12);
            }
        }

        // the video playing inside the section's borders
        void CommunityPlayer(GuideVideo v)
        {
            ClearPanel(v.Title.Length > 0 ? v.Title : "Video");
            var frame = new Bordered(Theme.CardEdge, 1);
            int top = (int)(12 + 32 * Theme.Zoom) + 10;
            frame.Bounds = new Rectangle(20, top, PW - 40, PH - 12 - top);
            Put(frame, 20, top);
            edgeHost = new Panel();
            edgeHost.Dock = DockStyle.Fill;
            edgeHost.BackColor = Color.Black;
            frame.Controls.Add(edgeHost);
            var wait = new Label();
            wait.Text = Lang.T("Loading the video...");
            wait.ForeColor = Theme.GoldDim;
            wait.BackColor = Color.Black;
            wait.Font = Theme.F(10, FontStyle.Italic);
            wait.AutoSize = true;
            wait.Location = new Point(12, 10);
            edgeHost.Controls.Add(wait);
            edgeHost.Resize += delegate { FitEdge(); };
            if (v.Author.Length > 0)
                PutText(Lang.T("by") + " " + v.Author, 9.5f, FontStyle.Italic, Theme.GoldDim, 84, 16, PW - 104, false);
            StartEdge(v);
        }

        // ---- playing: Edge's window as a child of the section
        [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
        const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_POPUP = unchecked((int)0x80000000);
        const int WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_SYSMENU = 0x00080000;
        const int WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000, WS_EX_APPWINDOW = 0x00040000;

        static string EdgePath()
        {
            foreach (string k in new[] { "ProgramFiles(x86)", "ProgramFiles", "ProgramW6432", "LOCALAPPDATA" })
            {
                string root = Environment.GetEnvironmentVariable(k);
                if (string.IsNullOrEmpty(root)) continue;
                string p = Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static string WatchUrl(GuideVideo v)
        {
            string id = VideoCard.YouTubeIdOf(v.Url);
            return id != null ? "https://www.youtube.com/watch?v=" + id : v.Url;
        }

        // a click on a YouTube video (here or in the Guide): it plays in the Community section
        bool PlayYouTube(GuideVideo v)
        {
            if (EdgePath() == null) { Util.OpenInShell(WatchUrl(v)); return true; }
            communityPlaying = v;
            if (current == "Community" && side.Visible) BuildPanel("Community");
            else { current = null; ShowSection("Community"); }
            return true;
        }

        void StartEdge(GuideVideo v)
        {
            StopEdge();
            string edge = EdgePath();
            if (edge == null) return;
            string profile = Path.Combine(Util.DataDir, "video-player");
            Process p;
            try
            {
                Directory.CreateDirectory(profile);
                var psi = new ProcessStartInfo(edge,
                    "--user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-features=msEdgeSidebarV2" +
                    " --window-position=-32000,-32000 --window-size=640,360 --app=\"" + WatchUrl(v) + "\"");
                psi.UseShellExecute = false;
                p = Process.Start(psi);
            }
            catch (Exception) { Util.OpenInShell(WatchUrl(v)); communityPlaying = null; return; }
            edgeVideo = p;
            // its window appears after a moment: then it is moved into the section
            ThreadPool.QueueUserWorkItem(delegate
            {
                IntPtr hwnd = IntPtr.Zero;
                for (int i = 0; i < 100 && hwnd == IntPtr.Zero; i++)
                {
                    Thread.Sleep(150);
                    try { if (p.HasExited) return; p.Refresh(); hwnd = p.MainWindowHandle; }
                    catch (Exception) { return; }
                }
                if (hwnd == IntPtr.Zero) return;
                Ui(delegate { Adopt(p, hwnd); });
            });
        }

        void Adopt(Process p, IntPtr hwnd)
        {
            if (p != edgeVideo || edgeHost == null || edgeHost.IsDisposed) { try { if (!p.HasExited) p.Kill(); } catch (Exception) { } return; }
            int style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            style |= WS_CHILD | WS_VISIBLE;
            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) & ~WS_EX_APPWINDOW);   // no taskbar button
            SetParent(hwnd, edgeHost.Handle);
            edgeWnd = hwnd;
            foreach (Control c in edgeHost.Controls) c.Visible = false;
            FitEdge();
            ShowWindow(hwnd, 5);
        }

        void FitEdge()
        {
            if (edgeWnd == IntPtr.Zero || edgeHost == null || edgeHost.IsDisposed || !IsWindow(edgeWnd)) return;
            MoveWindow(edgeWnd, 0, 0, edgeHost.ClientSize.Width, edgeHost.ClientSize.Height, true);
        }

        // closes the playing video's Edge (leaving the video, another section, the launcher closing)
        void StopEdge()
        {
            Process p = edgeVideo;
            edgeVideo = null;
            edgeWnd = IntPtr.Zero;
            if (p == null) return;
            try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
            try { p.Dispose(); } catch (Exception) { }
        }

        // the back arrow while a video plays: back to the featured video's page
        bool LeaveCommunityVideo()
        {
            if (current != "Community" || communityPlaying == null) return false;
            communityPlaying = null;
            StopEdge();
            BuildPanel("Community");
            return true;
        }
    }

    // the featured video's picture with a play button (YouTube's picture, filling the box)
    public class VideoPoster : Control
    {
        readonly GuideVideo video;
        readonly string id;
        bool hover;

        public VideoPoster(GuideVideo v, Size size)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            video = v;
            id = VideoCard.YouTubeIdOf(v.Url);
            Size = size;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Image img = id != null ? VideoCard.YouTubeThumb(id, delegate
            {
                try { if (IsHandleCreated) BeginInvoke(new Action(Invalidate)); } catch (Exception) { }
            }) : null;
            using (var clip = Armory.Round(r, 8))
            {
                g.SetClip(clip);
                if (img != null)
                {
                    float s = Math.Max((float)Width / img.Width, Height / (img.Height * 0.75f));
                    float iw = img.Width * s, ih = img.Height * s;
                    g.DrawImage(img, (Width - iw) / 2, (Height - ih) / 2, iw, ih);
                    if (!hover) using (var dim = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillRectangle(dim, r);
                }
                else
                    using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(r, Theme.Hex("#3b1f4a"), Theme.Hex("#120a18"), 60f))
                        g.FillRectangle(lg, r);
                g.ResetClip();
            }
            using (var pen = new Pen(hover ? Theme.Gold : Theme.Hex("#4a3a22"), hover ? 2f : 1f))
            using (var path = Armory.Round(r, 8)) g.DrawPath(pen, path);
            float pd = Math.Min(70, Height * 0.3f), cx = Width / 2f, cy = Height / 2f;
            using (var b = new SolidBrush(hover ? Theme.Hex("#c8442f") : Color.FromArgb(220, 30, 22, 18))) g.FillEllipse(b, cx - pd / 2, cy - pd / 2, pd, pd);
            using (var pen = new Pen(Theme.Gold, 2f)) g.DrawEllipse(pen, cx - pd / 2, cy - pd / 2, pd, pd);
            float t = pd * 0.32f;
            using (var b = new SolidBrush(Color.White))
                g.FillPolygon(b, new[] { new PointF(cx - t * 0.7f, cy - t), new PointF(cx - t * 0.7f, cy + t), new PointF(cx + t, cy) });
        }
    }
}
