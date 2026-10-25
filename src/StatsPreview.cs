// A developer tool, not used by players: "--preview-stats <saved folder> <out folder>" renders the STATS page for
// the AZS Stats file in <saved folder> into PNG pictures (every tab, overall and for the most played class),
// so the layout can be checked without the game. Settings are only changed in memory, nothing is saved.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        public static void RenderStatsPreview(string savedDir, string outDir)
        {
            Directory.CreateDirectory(outDir);
            PreviewMode = true;
            var app = new LauncherApp(true);
            app.Cfg.Set("saved_dir", Path.GetFullPath(savedDir));
            app.Cfg.Set("addons_dir", Path.Combine(Path.GetFullPath(outDir), "AddOns"));
            app.Cfg.Set("cloud_enabled", false);
            app.StartPosition = FormStartPosition.Manual;
            app.Location = new Point(0, 0);
            app.Show();
            app.ShowSide();
            RunStats st = Stats.Load(app.SyncSettings().SavedDir);
            string topClass = st != null && st.Classes.Count > 0 ? st.Classes[0].Key : null;
            if (st != null)                                  // ask for every spell icon now, so the pictures show them
            {
                foreach (var ab in st.Abilities) SpellIcons.Spell(ab.Name, null);
                foreach (var c in st.Classes) SpellIcons.Class(c.Key, null);
                foreach (var n in st.Nemesis) SpellIcons.Npc(n.Name, null);
                SpellIcons.WaitForAll(30000);
                foreach (var n in st.Nemesis) SpellIcons.Npc(n.Name, null);   // looked up: now the pictures
                if (st.Nemesis.Count > 0)
                    using (var eb = new Bitmap(1200, 80))
                    {
                        using (var g = Graphics.FromImage(eb)) { g.Clear(Color.White); g.DrawString(SpellIcons.Explain(st.Nemesis[0].Name), new Font("Arial", 8), Brushes.Black, new RectangleF(2, 2, 1196, 76)); }
                        eb.Save(Path.Combine(outDir, "npc-lookup-check.png"), ImageFormat.Png);
                    }
                SpellIcons.WaitForAll(30000);
            }
            foreach (string cls in new[] { null, topClass })
            {
                for (int tab = 0; tab < StatTabs.Length; tab++)
                {
                    app.current = "Stats";
                    app.statsClass = cls;
                    app.statsTab = tab;
                    app.BuildPanel("Stats");
                    Application.DoEvents();
                    for (int k = 0; k < 3; k++)                  // spell icons arrive from Wowhead while the page is shown
                    {
                        SpellIcons.WaitForAll(15000);
                        app.panel.Refresh();
                        Application.DoEvents();
                    }
                    string name = (cls ?? "all") + "-" + tab + "-" + StatTabs[tab].ToLowerInvariant();
                    using (var win = new Bitmap(app.side.Width, app.side.Height))
                    {
                        app.side.DrawToBitmap(win, new Rectangle(0, 0, win.Width, win.Height));
                        win.Save(Path.Combine(outDir, name + "-window.png"), ImageFormat.Png);
                    }
                    ScrollPanel sp = FindScroll(app.panel);
                    if (sp != null)
                    {
                        int h = 10, wdt = sp.ClientSize.Width;
                        foreach (Control c in sp.Controls) h = Math.Max(h, c.Bottom + 10);
                        using (var page = new Bitmap(wdt, h))
                        {
                            using (var g = Graphics.FromImage(page)) g.Clear(sp.BackColor);
                            foreach (Control c in sp.Controls)
                                if (c.Width > 0 && c.Height > 0) c.DrawToBitmap(page, new Rectangle(c.Left, c.Top, c.Width, c.Height));
                            page.Save(Path.Combine(outDir, name + "-page.png"), ImageFormat.Png);
                        }
                    }
                }
                if (topClass == null) break;
            }
            // the New Player Guide: topic cards, a topic made of pages, and pages with video cards
            {
                var topics = Data.Guide;
                int classes = 0;
                for (int i = 0; i < topics.Count; i++) if (topics[i].Pages.Count > 0) { classes = i; break; }
                foreach (int[] view in new[] { new[] { -1, -1 }, new[] { classes, -1 }, new[] { 0, -1 }, new[] { 1, -1 }, new[] { classes, 0 }, new[] { classes + 1, -1 } })
                {
                    app.current = "Guide";
                    app.ShowSide();
                    app.gTopic = view[0];
                    app.gPage = view[1];
                    app.BuildPanel("Guide");
                    Application.DoEvents();
                    using (var win = new Bitmap(app.side.Width, app.side.Height))
                    {
                        app.side.DrawToBitmap(win, new Rectangle(0, 0, win.Width, win.Height));
                        win.Save(Path.Combine(outDir, "guide-" + view[0] + "-" + view[1] + ".png"), ImageFormat.Png);
                    }
                    ScrollPanel gsp = FindScroll(app.panel);
                    if (gsp != null)
                    {
                        int h = 10;
                        foreach (Control c in gsp.Controls) h = Math.Max(h, c.Bottom + 10);
                        using (var page = new Bitmap(gsp.ClientSize.Width, h))
                        {
                            using (var g = Graphics.FromImage(page)) g.Clear(gsp.BackColor);
                            foreach (Control c in gsp.Controls)
                                if (c.Width > 0 && c.Height > 0) c.DrawToBitmap(page, new Rectangle(c.Left, c.Top, c.Width, c.Height));
                            page.Save(Path.Combine(outDir, "guide-" + view[0] + "-" + view[1] + "-page.png"), ImageFormat.Png);
                        }
                    }
                }
                app.current = "Stats";
                app.BuildPanel("Stats");
            }
            // the guide's video player: does the first video stream inside the launcher?
            {
                GuideVideo vid = null;
                foreach (var t in Data.Guide) { if (t.Videos.Count > 0) { vid = t.Videos[0]; break; } foreach (var pg in t.Pages) if (vid == null && pg.Videos.Count > 0) vid = pg.Videos[0]; if (vid != null) break; }
                var tests = new System.Collections.Generic.List<GuideVideo>();
                if (vid != null) tests.Add(vid);
                string localMp4 = Path.GetFullPath(Path.Combine("guide-videos", "races", "can-i-change-my-race.mp4"));
                if (File.Exists(localMp4)) { var lv = new GuideVideo(); lv.Title = "local"; lv.Url = new Uri(localMp4).AbsoluteUri; tests.Add(lv); }
                var report = new System.Text.StringBuilder();
                foreach (GuideVideo tv in tests)
                {
                    vid = tv;
                    app.Show();
                    app.ShowSide();
                    app.current = "Guide";
                    app.gVideo = vid;
                    app.BuildPanel("Guide");
                    string state = "no web view";
                    WebBrowser web = FindWeb(app.panel);
                    var until = DateTime.Now.AddSeconds(25);
                    bool probed = false;
                    while (web != null && DateTime.Now < until)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(100);
                        try
                        {
                            if (web.Document == null) continue;
                            if (!probed && web.ReadyState == WebBrowserReadyState.Complete)
                            {   // can the page reach the address at all?
                                probed = true;
                                web.Document.InvokeScript("eval", new object[] { "window.__x='?';try{var x=new XMLHttpRequest();x.open('GET','" + vid.Url + "',true);x.setRequestHeader('Range','bytes=0-99');x.onreadystatechange=function(){if(x.readyState==4)window.__x=x.status+' '+x.getResponseHeader('Content-Type');};x.send();}catch(e){window.__x='err '+e.message;}" });
                            }
                            object r = web.Document.InvokeScript("eval", new object[] {
                                "(function(){var v=document.getElementById('v');return 'mode='+document.documentMode+' ready='+(v?v.readyState:-1)+' duration='+(v?v.duration:-1)+' time='+(v?v.currentTime:-1)+' error='+(v&&v.error?v.error.code:0)+' h264='+(v?v.canPlayType('video/mp4'):'')+' xhr='+window.__x;})()" });
                            state = Convert.ToString(r);
                            if (state.Contains("ready=4") && !state.Contains("time=0 ")) break;
                        }
                        catch (Exception ex) { state = "script: " + ex.Message; }
                    }
                    report.Append(vid.Url + "\n" + state + "\n\n");
                }
                using (var eb = new Bitmap(900, 140))
                {
                    using (var g = Graphics.FromImage(eb)) { g.Clear(Color.White); g.DrawString(report.ToString(), new Font("Arial", 9), Brushes.Black, 4, 4); }
                    eb.Save(Path.Combine(outDir, "video-player-check.png"), ImageFormat.Png);
                }
                app.gVideo = null;
                app.current = "Stats";
                app.BuildPanel("Stats");
            }
            // every other section, to check they all use the launcher's colors
            foreach (string sec in new[] { "Cloud Save", "Addons", "Changelog", "Options", "About Launcher" })
            {
                app.Show();                                  // the preview starts as if in the tray
                app.ShowSide();
                app.current = sec;
                try { app.BuildPanel(sec); }
                catch (Exception ex)
                {
                    using (var eb = new Bitmap(900, 300))
                    {
                        using (var g = Graphics.FromImage(eb)) { g.Clear(Color.White); g.DrawString(ex.ToString(), new Font("Arial", 8), Brushes.Black, new RectangleF(4, 4, 892, 292)); }
                        eb.Save(Path.Combine(outDir, "section-error-" + sec.Replace(" ", "-") + ".png"), ImageFormat.Png);
                    }
                    continue;
                }
                Application.DoEvents();
                app.panel.Refresh();
                string nm = "section-" + sec.Replace(" ", "-").ToLowerInvariant();
                using (var win = new Bitmap(app.panel.Width, app.panel.Height))
                {   // control by control, back to front (DrawToBitmap of the whole window skips some of them)
                    using (var g = Graphics.FromImage(win)) g.Clear(app.panel.BackColor);
                    for (int i = app.panel.Controls.Count - 1; i >= 0; i--)
                    {
                        Control c = app.panel.Controls[i];
                        if (c.Visible && c.Width > 0 && c.Height > 0) c.DrawToBitmap(win, c.Bounds);
                    }
                    win.Save(Path.Combine(outDir, nm + ".png"), ImageFormat.Png);
                }
                ScrollPanel ssp = FindScroll(app.panel);
                if (ssp != null)
                {
                    int h = 10;
                    foreach (Control c in ssp.Controls) h = Math.Max(h, c.Bottom + 10);
                    using (var page = new Bitmap(ssp.ClientSize.Width, Math.Min(h, 3000)))
                    {
                        using (var g = Graphics.FromImage(page)) g.Clear(ssp.BackColor);
                        foreach (Control c in ssp.Controls)
                            if (c.Width > 0 && c.Height > 0) c.DrawToBitmap(page, new Rectangle(c.Left, c.Top, c.Width, c.Height));
                        page.Save(Path.Combine(outDir, nm + "-page.png"), ImageFormat.Png);
                    }
                }
            }
            app.current = "Stats";
            app.BuildPanel("Stats");
            // the main window with a section open, then with none
            foreach (string shot in new[] { "home-section", "home" })
            {
                if (shot == "home") app.side.Hide();
                app.Show();                                  // the preview starts as if in the tray
                Application.DoEvents();
                // painted by hand: the build machine's screen is smaller than the window, so it would be cut off
                using (var home = new Bitmap(W, H))
                {
                    using (var g = Graphics.FromImage(home))
                        app.OnPaint(new PaintEventArgs(g, new Rectangle(0, 0, W, H)));
                    for (int i = app.Controls.Count - 1; i >= 0; i--)
                    {
                        Control c = app.Controls[i];
                        if (c.Visible && c.Width > 0 && c.Height > 0) c.DrawToBitmap(home, c.Bounds);
                    }
                    home.Save(Path.Combine(outDir, shot + ".png"), ImageFormat.Png);
                }
            }
            app.tray.Visible = false;
        }

        static WebBrowser FindWeb(Control c)
        {
            foreach (Control k in c.Controls)
            {
                var w = k as WebBrowser;
                if (w != null) return w;
                w = FindWeb(k);
                if (w != null) return w;
            }
            return null;
        }

        static ScrollPanel FindScroll(Control c)
        {
            foreach (Control k in c.Controls)
            {
                var s = k as ScrollPanel;
                if (s != null) return s;
                s = FindScroll(k);
                if (s != null) return s;
            }
            return null;
        }
    }
}
