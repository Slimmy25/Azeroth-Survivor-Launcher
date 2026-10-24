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
            foreach (string cls in new[] { null, topClass })
            {
                for (int tab = 0; tab < StatTabs.Length; tab++)
                {
                    app.current = "Stats";
                    app.statsClass = cls;
                    app.statsTab = tab;
                    app.BuildPanel("Stats");
                    Application.DoEvents();
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
