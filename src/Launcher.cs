// The main launcher window: the tall narrow window with PLAY and the section buttons, the tray icon, playing
// (cloud download -> game -> cloud upload), installing the game, cloud sync runs and game updates.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp : Form, IWheel
    {
        // TEMPORARY: while true, nothing but INSTALL works until the game has been downloaded by this launcher
        // (the flag is stored in the config when a download finishes). Set to false to switch it off.
        public const bool RequireLauncherInstall = true;

        // Where the game is downloaded from, and the download server's login (shared with every player, so it is fine
        // in the open). The launcher downloads it itself (BeginClientDownload), into a "temp" folder beside the launcher.
        public const string ClientUrl = "https://budapestmix.hu:5000/download";
        public const string ClientUser = "AzerothSurvivors_uSZfhjS", ClientPassword = "AzerothSurvivors_uSZfhjS";
        // The developers' backup host: a page with a link to the newest archive and its SHA-256 (no login).
        public const string ClientMirrorUrl = "https://azerothsurvivors-mirror.sandervspl.dev/";
        // A third host, on Google Drive (the same game in its own archive, so its own MD5).
        public const string ClientDriveId = "12EHYJrgdANtgZvND49L_cxQwoObTbkWq";
        // The last resort: the game's own download page. Never one of the three picked at random; only tried after
        // all three have failed. Its archive may be newer than the checksums below, so it is accepted when it is a
        // real .rar/.zip/.7z archive from the game's own site.
        public const string ClientLastResortUrl = "https://wow.aethus.pl/download";
        // Each install picks one of the hosts at random; if that one fails, the others are tried in turn. Every download
        // is checked against the game's known checksums below (update them with a new game version).
        public const string ClientSha256 = "971db7968dc37ec50b18d64a1de7f693e137f6793a7368e4d5c27bd9522eccd8";   // AzerothSurvivorsWindowsNew.rar
        public const string ClientMd5 = "7748bfdffaccd2f46d868cf69ad3e72c";                                       //   (760497456 bytes)
        public const string ClientDriveMd5 = "81887cd5d56dcca3b7dd086b0b498cf6";                                  // the Google Drive archive
        public const string DiscordUrl = "https://discord.gg/azerothsurvivors";

        const int W = 1100, H = 690;                         // retro launcher window: sections open on the left, News on the right

        public Config Cfg;
        public bool GameRunningFlag, Installing, SyncBusy, Updating, UpdateChecking, UpdateAvailable;
        UpdateInfo updateInfo;
        UpdateDialog updateDlg;
        bool updateOfferPending, setupOpen, setupAsked, trayNotified, quitting;
        string updateRecovery;
        readonly bool startInTray;
        readonly List<KeyValuePair<string, Level>> cloudLog = new List<KeyValuePair<string, Level>>();
        RichTextBox logBox;
        string status = "Ready.", cloudNote = "";
        bool addonDlBusy;

        readonly WowButton playBtn, updateBtn, aboutBtn;
        readonly BrandIcon discordIcon;
        readonly Dictionary<string, WowButton> nav = new Dictionary<string, WowButton>();
        readonly string[] navNames = { "Guide", "Addons", "Cloud Save", "Changelog", "Stats", "Options" };   // Changelog: on the News line   // order under "Launcher" (Discord before Options)
        NotifyIcon tray;
        ToolStripMenuItem trayShow, trayPlay, trayUpload, trayCloseToTray, trayQuit;

        internal void TrayTexts()
        {
            trayShow.Text = Lang.T("Show Launcher");
            trayPlay.Text = Lang.T("Play");
            trayUpload.Text = Lang.T("Upload Save Now");
            trayCloseToTray.Text = Lang.T("Close Button Hides to Tray");
            trayQuit.Text = Lang.T("Quit");
        }

        // Options > Language
        public void SetLanguage(string code)
        {
            Cfg.Set("language", code);
            Cfg.Save();
            Lang.Load(code);
            Theme.PickFont();
            TrayTexts();
            foreach (Control c in Controls) c.Invalidate();
            Invalidate();
            sideHead.Invalidate();
            SetSideTitle(sideHead.Title);
            if (current != null && current != "Home" && side.Visible) BuildPanel(current);
        }

        public static bool PreviewMode;                     // rendering preview pictures: no update checks, no restarts

        public static LauncherApp App;                       // the running launcher (for the Stats page's static builders)

        public LauncherApp(bool startInTray)
        {
            App = this;
            WowDialog.Host = this;                           // questions and messages open inside the launcher
            this.startInTray = startInTray;
            Theme.PickFont();
            Cfg = Config.Load();
            string game = GameDir();                         // an update cut off by a crash or power cut is undone first
            if (Directory.Exists(game)) updateRecovery = Updater.RecoverInterrupted(game);

            Text = "Azeroth Survivors Launcher";
            Icon = Theme.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(W, H);
            BackColor = Theme.BgBottom;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;

            // layout: PLAY in the box at the bottom right, the sections under the news, the rest in the bottom bar
            playBtn = Place(new WowButton("PLAY", Play, 88, 49, 12), 977, 578 + (112 - 49) / 2);   // centred in the bottom bar         // smaller: status above, time played below
            playBtn.Skin = Retro.Play;
            foreach (string name in navNames)
            {
                string n = name;
                if (n == "Changelog") continue;              // on the News line (below)
                nav[n] = Place(new WowButton(n.ToUpperInvariant(), delegate { ShowSection(n); }, 128, 27, 8.5f), 693, 480);
                nav[n].Skin = Retro.Button;
            }
            // UPDATE LAUNCHER and ABOUT LAUNCHER live in Options > Launcher now; these two stay hidden and carry their state
            aboutBtn = Place(new WowButton("ABOUT LAUNCHER", delegate { ShowSection("About Launcher"); }, 159, 27, 8), W - 8 - 159 - 6, 98);
            aboutBtn.Visible = false;
            nav["About Launcher"] = aboutBtn;
            updateBtn = Place(new WowButton("UPDATE LAUNCHER", delegate { CheckLauncherUpdate(true); }, 159, 27, 8), W - 8 - 159 - 6, 64);
            updateBtn.Visible = false;
            // the News line: Discord's own symbol (Blurple, from discord.com/branding, unaltered) left of "News", and
            // CHANGELOG at the very end of the line
            discordIcon = new BrandIcon(Theme.LoadImage("AS.discord-blurple.png"), Theme.LoadImage("AS.discord-white.png"), DiscordUrl);   // the hover shows where it goes
            discordIcon.Bounds = new Rectangle(697, 203, 26, 20);
            nav["Changelog"] = Place(new WowButton("CHANGELOG", delegate { ShowSection("Changelog"); }, 110, 22, 8), 1085 - 110, 201);
            nav["Changelog"].Skin = Retro.Button;
            discordIcon.Click += delegate { Util.OpenInShell(DiscordUrl); };
            Controls.Add(discordIcon);
            LayoutNav();

            BuildSide();
            SetupTray();
            RefreshPlayLabel();

            FormClosing += OnClosingWindow;
            FormClosed += delegate { StopEdge(); };              // a Community video never outlives the launcher
            VideoCard.YouTube = PlayYouTube;                     // YouTube videos play inside Community (Community.cs)
            GuideLinks.CapturedTips = delegate                  // the abilities' tooltips AZS Stats wrote down in game
            {
                RunStats st = AddonStats();
                return st != null ? st.Tooltips : null;
            };
            GuideLinks.Navigate = delegate(int t, int pg)      // a link in the Guide's text (GuideLinks.cs)
            {
                if (current != "Guide" || !side.Visible) ShowSection("Guide");
                SetGuideView(t, pg);
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) After(1, HideToTray); };
            Shown += delegate
            {
                if (startInTray) Hide();
                if (!PreviewMode) After(300, StartupUpdateCheck);   // a newer launcher on GitHub is installed right away
                After(500, MaybeFirstRun);
                After(700, delegate { if ((current == null || current == "Home") && !setupOpen) ShowHomeCommunity(); });   // home: Community
                After(900, TellUpdateRecovery);
            };
            VisibleChanged += delegate
            {
                if (Visible && updateOfferPending)           // an update was found while the launcher was hidden
                {
                    updateOfferPending = false;
                    After(600, OfferUpdate);
                }
                if (Visible && !setupAsked) After(500, MaybeFirstRun);   // started hidden: setup when first opened
            };
        }

        WowButton Place(WowButton b, int x, int y)
        {
            b.Location = new Point(x, y);
            Controls.Add(b);
            return b;
        }

        // ------------------------------------------------------------------ small tools
        public void Ui(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }
            catch (Exception) { }
        }

        // runs fn on the UI thread from a worker thread and returns its result
        T UiCall<T>(Func<T> fn)
        {
            return (T)Invoke(fn);
        }

        public static void After(int ms, Action a)
        {
            var t = new System.Windows.Forms.Timer();
            t.Interval = Math.Max(1, ms);
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                a();
            };
            t.Start();
        }

        static void Bg(ThreadStart work)
        {
            var t = new Thread(work);
            t.IsBackground = true;
            t.Start();
        }

        public static string AskFolder(IWin32Window owner, string title, string start)
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = Lang.T(title);
                d.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) d.SelectedPath = start;
                return d.ShowDialog(owner) == DialogResult.OK && d.SelectedPath.Length > 0 ? d.SelectedPath : null;
            }
        }

        public bool Ask(string title, string msg)
        {
            return Msg.Show(this, msg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public void Info(string title, string msg) { Msg.Show(this, msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        public void Warn(string title, string msg) { Msg.Show(this, msg, title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        public void Error(string title, string msg) { Msg.Show(this, msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }

        // ------------------------------------------------------------------ settings
        public string GameDir()
        {
            string d = Cfg.Str("game_dir");
            return d.Length > 0 ? d : Util.LauncherDir;      // by default the folder the launcher sits in
        }

        public bool ClientPresent()
        {
            if (!File.Exists(Path.Combine(GameDir(), Util.GameExe))) return false;
            return !RequireLauncherInstall || Cfg.Bool("installed_by_launcher", false);
        }

        // false while the temporary "install the game with this launcher first" rule is blocking
        public bool GateOpen() { return !RequireLauncherInstall || ClientPresent(); }

        public bool CloudEnabled() { return Cfg.Bool("cloud_enabled", Cfg.Str("cloud_dir").Trim().Length > 0); }

        public SyncSettings SyncSettings()
        {
            string game = GameDir();
            string config = Cfg.Str("config_dir");
            if (config.Length == 0 || !Directory.Exists(config)) config = Path.Combine(game, "benilla-config");
            string save = Cfg.Str("save_dir");
            return new SyncSettings(Path.Combine(game, Util.GameExe), Cfg.Str("cloud_dir").Trim(),
                                    save.Length > 0 ? save : Util.DefaultSaveDir, config, Util.StateDir,
                                    Cfg.Str("addons_dir"), Cfg.Str("saved_dir"));
        }

        // ------------------------------------------------------------------ drawing the launcher
        // the launcher art (layout after the WoW Retro Launcher); the newest patch notes in the News panel
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Image bg = Retro.Get("background");
            if (bg != null) g.DrawImage(bg, 0, 0, W, H);
            else g.Clear(Theme.BgBottom);
            Theme.Smooth(g);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // title: "Azeroth Survivors" in big gold letters over the section area, the emblem to its right
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat())
            {
                path.AddString("Azeroth Survivors", Theme.TitleFamily(), (int)FontStyle.Bold, 58f, new PointF(0, 0), sf);   // the decorative title font
                RectangleF b = path.GetBounds();
                Image em = Theme.Logo;
                int emW = em != null ? 128 : 0, emH = em != null ? em.Height * 128 / em.Width : 0;
                float total = b.Width + (em != null ? 16 + emW : 0);
                float x0 = 8 + (670 - total) / 2, y0 = 70 + (120 - b.Height) / 2;
                using (var m = new Matrix())
                {
                    m.Translate(x0 - b.X, y0 - b.Y);
                    path.Transform(m);
                }
                using (var sh = new GraphicsPath())
                {
                    sh.AddPath(path, false);
                    using (var m = new Matrix()) { m.Translate(3, 4); sh.Transform(m); }
                    using (var sb = new SolidBrush(Color.FromArgb(200, 0, 0, 0))) g.FillPath(sb, sh);
                }
                RectangleF pb = path.GetBounds();
                using (var lg = new LinearGradientBrush(pb, Theme.Hex("#fff1b8"), Theme.Hex("#c9821a"), LinearGradientMode.Vertical))
                    g.FillPath(lg, path);
                using (var pen = new Pen(Theme.Hex("#4a2a08"), 2f)) g.DrawPath(pen, path);
                if (em != null) g.DrawImage(em, pb.Right + 16, 70 + (120 - emH) / 2, emW, emH);
                // under the title: who made the game
                string by = Lang.T("A Game Made By Aethus");
                Font byf = Theme.F(13, FontStyle.Italic);
                float bw = g.MeasureString(by, byf).Width, bx = pb.Left + (pb.Width - bw) / 2, byy = pb.Bottom + 12;
                using (var bsh = new SolidBrush(Color.Black)) g.DrawString(by, byf, bsh, bx + 1, byy + 1);
                using (var bb = new SolidBrush(Theme.Hex("#e8d9a8"))) g.DrawString(by, byf, bb, bx, byy);
            }
            // News: the newest patch note
            var posts = Data.Changelog;
            if (posts.Count > 0)
            {
                string[] lines = posts[0].Trim('\n').Split('\n');
                using (var title = new SolidBrush(Retro.Gold))
                using (var body = new SolidBrush(Theme.Hex("#e8dcc0")))
                using (var shadow = new SolidBrush(Color.Black))
                {
                    Font tf = Theme.F(11, FontStyle.Bold), bf = Theme.F(9);
                    // the whole post, scrolled by newsScroll inside NewsArea (mouse wheel, or drag the slim bar)
                    Region oldClip = g.Clip;
                    g.SetClip(NewsArea);
                    float y = NewsArea.Y - newsScroll;
                    g.DrawString(Lang.T("Patch") + " " + lines[0].Trim(), tf, shadow, 698, y + 1);
                    g.DrawString(Lang.T("Patch") + " " + lines[0].Trim(), tf, title, 697, y);
                    y += 22;
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string l = lines[i].Trim();
                        if (l.Length == 0 || l.Trim('-', '=', '_', '\u2014', '\u2013').Length == 0) continue;   // blank or a rule
                        if (l.StartsWith("- ") || l.StartsWith("* ")) l = "\u2022 " + l.Substring(2);
                        SizeF sz = g.MeasureString(l, bf, NewsTextW);
                        if (y + sz.Height > NewsArea.Y && y < NewsArea.Bottom)
                        {
                            var box = new RectangleF(700, y, NewsTextW, sz.Height + 2);
                            g.DrawString(l, bf, shadow, new RectangleF(box.X + 1, box.Y + 1, box.Width, box.Height));
                            g.DrawString(l, bf, body, box);
                        }
                        y += sz.Height + 1;
                    }
                    newsHeight = (int)(y + newsScroll - NewsArea.Y) + 4;
                    g.Clip = oldClip;
                    Rectangle thumb = NewsThumb();
                    if (!thumb.IsEmpty)
                    {
                        using (var tb = new SolidBrush(Color.FromArgb(120, Theme.CardEdge))) g.FillRectangle(tb, NewsBar.X + 2, NewsBar.Y, 2, NewsBar.Height);
                        using (var tb = new SolidBrush(newsDrag || newsBarHover ? Theme.Gold : Theme.GoldDim)) g.FillRectangle(tb, thumb);
                    }
                }
            }
            using (var gold = new SolidBrush(Retro.Gold))
            using (var shadow = new SolidBrush(Color.Black))
            {
                Font hf = Theme.F(11);
                g.DrawString(Lang.T("News"), hf, shadow, 729, 205);    // after the Discord symbol
                g.DrawString(Lang.T("News"), hf, gold, 728, 204);
                g.DrawString(Lang.T("Launcher"), hf, shadow, 698, 447);
                g.DrawString(Lang.T("Launcher"), hf, gold, 697, 446);
            }
            WeeklyRecap(g);
            // bottom bar: the status above PLAY says everything
            using (var gb = new SolidBrush(Retro.Gold))
            using (var sf = new StringFormat())
            {
                // bottom bar, right: status above PLAY, time played below it, the game version in the corner
                sf.Alignment = StringAlignment.Center;
                var rc = new RectangleF(800, 0, 292, 16);
                using (var sb = new SolidBrush(Theme.Hex("#fff2b0")))
                {
                    // centred over PLAY; a long status that would run off the window ends at the right edge instead
                    string st = Lang.T(status == "Ready." && !ClientPresent()
                        ? (RequireLauncherInstall ? "Game client not found. Press INSTALL to download it first."
                                                  : "Game client not found. Press INSTALL, or set the game folder in Options.")
                        : status);
                    Font stf = Theme.F(9, FontStyle.Italic);
                    float cxp = playBtn.Left + playBtn.Width / 2f;
                    if (dlActive)                            // the game download: its bar takes the status line's place
                    {
                        int pct = dlTotal > 0 ? (int)Math.Min(100, dlDone * 100 / dlTotal) : -1;
                        st = Lang.T(pct >= 0 ? "Downloading the game: " + pct + "%  (" + Util.FmtSize(dlDone) + " of " + Util.FmtSize(dlTotal) + ")"
                                             : "Downloading the game: " + Util.FmtSize(dlDone));
                        Font df = Theme.F(8, FontStyle.Italic);
                        float dw = g.MeasureString(st, df).Width;
                        const int bw = 250, bh = 8;
                        float bx = Math.Min(cxp - bw / 2f, W - 10 - bw);
                        float by = playBtn.Top - 12;
                        g.DrawString(st, df, sb, Math.Min(cxp - dw / 2, W - 10 - dw), by - 15);
                        using (var back = new SolidBrush(Theme.Hex("#1a1410"))) g.FillRectangle(back, bx, by, bw, bh);
                        if (pct > 0)
                            using (var fill = new LinearGradientBrush(new RectangleF(bx, by, bw, bh), Theme.Hex("#f4d27a"), Theme.Hex("#a8761e"), 90f))
                                g.FillRectangle(fill, bx, by, bw * pct / 100f, bh);
                        using (var edge = new Pen(Theme.Hex("#c49a48"))) g.DrawRectangle(edge, bx, by, bw, bh);
                    }
                    else
                    {
                        float sw = g.MeasureString(st, stf).Width;
                        float sx = Math.Min(cxp - sw / 2, W - 10 - sw);
                        g.DrawString(st, stf, sb, sx, playBtn.Top - 26);
                    }
                }
                string version = Data.GameVersion;
                using (var vb = new SolidBrush(Theme.Hex("#b8a27a")))            // bottom-left corner
                    g.DrawString(Lang.T("Game version") + (version.Length > 0 ? ": " + version : ""), Theme.F(9.5f), vb, 12, 670);   // a little bigger
            }
        }

        // ---- "This week": a card in the empty lower left of the home screen (AZS Stats' runs of the last 7 days)
        void WeeklyRecap(Graphics g)
        {
            // under the Launcher buttons: "This week" (AZS Stats' runs of the last 7 days) and the play time AZS Stats
            // counts in game, together in one small card
            var lines = new List<string[]>();                // { heading, text }
            RunStats st = null;
            try { st = StatsEnabled() ? AddonStats() : null; } catch (Exception) { }
            if (st != null && st.History.Count > 0) lines.Add(new[] { Lang.T("This week"), WeekText(st) });
            PlayStats ps = st != null ? st.Play : new PlayStats();
            if (ps.Sessions > 0 && StatsEnabled() && Cfg.Bool("home_playtime", true))
                lines.Add(new[] { Lang.T("Time played"), Duration(ps.Seconds) + "  ·  " + Lang.T("Last session") + " " + Duration(ps.LastSeconds) });
            if (lines.Count == 0) return;
            var box = new RectangleF(694, 546, 394, 30);
            using (var path = Armory.Round(box, 5))
            {
                using (var b = new SolidBrush(Color.FromArgb(190, 14, 13, 20))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(150, Theme.GoldDim), 1)) g.DrawPath(p, path);
            }
            Font hf = Theme.F(8, FontStyle.Bold), bf = Theme.F(8);
            float lh = lines.Count == 1 ? box.Height : box.Height / 2;
            using (var gold = new SolidBrush(Retro.Gold))
            using (var text = new SolidBrush(Theme.Text))
            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            {
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < lines.Count; i++)
                {
                    float y = box.Y + i * lh + (lines.Count == 1 ? 0 : (i == 0 ? 1 : -1));
                    string head = lines[i][0] + ":";
                    float hw = g.MeasureString(head, hf).Width;
                    g.DrawString(head, hf, gold, new RectangleF(box.X + 8, y, hw + 2, lh), sf);
                    g.DrawString(lines[i][1], bf, text, new RectangleF(box.X + 8 + hw + 2, y, box.Width - 16 - hw - 2, lh), sf);
                }
            }
        }

        // "3 runs, 1 won · best DPS 205 (Mage) · most played with Brisa", or when the week was quiet, when you last played
        string WeekText(RunStats st)
        {
            DateTime since = DateTime.Now.AddDays(-7);
            int runs = 0, wins = 0;
            double bestDps = 0;
            StatRun bestRun = null;
            var partners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in st.History)
            {
                DateTime d;
                if (!DateTime.TryParse(r.Date, System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None, out d) || d < since) continue;
                runs++;
                if (r.Kind == "victory") wins++;
                if (r.Dps > bestDps) { bestDps = r.Dps; bestRun = r; }
                foreach (string p in r.Party.Split(','))
                {
                    string n = p.Trim();
                    if (n.Length == 0) continue;
                    int c;
                    partners.TryGetValue(n, out c);
                    partners[n] = c + 1;
                }
            }
            var parts = new List<string>();
            if (runs == 0)
            {
                DateTime last;
                string ago = DateTime.TryParse(st.History[0].Date, System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.None, out last)
                             ? Lang.T(((int)(DateTime.Now - last).TotalDays) + " days ago") : "";
                parts.Add(Lang.T("No runs yet this week.") + (ago.Length > 0 ? " " + Lang.T("Your last run was " + ago + ".") : ""));
            }
            else
            {
                parts.Add(Lang.T(runs + " runs, " + wins + " won"));
                if (bestRun != null)
                {
                    StatBlock cb = st.Class(bestRun.ClassKey);
                    StatRecord rec;
                    bool high = cb != null && cb.Best.TryGetValue("dps", out rec) && rec.Value <= bestRun.Dps + 0.5;
                    parts.Add(high ? Lang.T("new " + bestRun.Class + " DPS high: " + Big(bestRun.Dps))
                                   : Lang.T("best DPS " + Big(bestRun.Dps) + " (" + bestRun.Class + ")"));
                }
                string top = null;
                int most = 0;
                foreach (var kv in partners) if (kv.Value > most) { most = kv.Value; top = kv.Key; }
                if (top != null) parts.Add(Lang.T("most played with " + top));
            }
            return string.Join(" · ", parts.ToArray());
        }

        // ---- the News panel scrolls when the patch note is longer than its space
        static readonly Rectangle NewsArea = new Rectangle(694, 236, 386, 442 - 236);
        static readonly Rectangle NewsBar = new Rectangle(1080, 238, 6, 442 - 240);
        const int NewsTextW = 372;
        int newsScroll, newsHeight;
        bool newsDrag, newsBarHover;
        int newsDragOffset;

        Rectangle NewsThumb()
        {
            if (newsHeight <= NewsArea.Height) return Rectangle.Empty;
            int h = Math.Max(24, NewsBar.Height * NewsArea.Height / newsHeight);
            int y = NewsBar.Y + (NewsBar.Height - h) * newsScroll / Math.Max(1, newsHeight - NewsArea.Height);
            return new Rectangle(NewsBar.X, y, NewsBar.Width, h);
        }

        void ScrollNews(int to)
        {
            int max = Math.Max(0, newsHeight - NewsArea.Height);
            to = Math.Max(0, Math.Min(max, to));
            if (to == newsScroll) return;
            newsScroll = to;
            Invalidate(new Rectangle(NewsArea.X - 4, NewsArea.Y - 2, NewsBar.Right - NewsArea.X + 8, NewsArea.Height + 4));
        }

        public void Wheel(int delta)
        {
            Point p = PointToClient(Cursor.Position);
            Rectangle hot = Rectangle.Union(NewsArea, NewsBar);
            if (hot.Contains(p)) ScrollNews(newsScroll - delta / 120 * 40);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Rectangle t = NewsThumb();
            Rectangle bar = new Rectangle(NewsBar.X - 4, NewsBar.Y, NewsBar.Width + 8, NewsBar.Height);
            if (e.Button == MouseButtons.Left && !t.IsEmpty && bar.Contains(e.Location))
            {
                newsDrag = true;
                newsDragOffset = t.Contains(e.Location) ? e.Y - t.Y : t.Height / 2;
                DragNews(e.Y);
            }
            base.OnMouseDown(e);
        }

        void DragNews(int mouseY)
        {
            Rectangle t = NewsThumb();
            if (t.IsEmpty) return;
            int range = NewsBar.Height - t.Height;
            int top = Math.Max(0, Math.Min(range, mouseY - newsDragOffset - NewsBar.Y));
            ScrollNews(range <= 0 ? 0 : top * (newsHeight - NewsArea.Height) / range);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (newsDrag) DragNews(e.Y);
            bool over = new Rectangle(NewsBar.X - 4, NewsBar.Y, NewsBar.Width + 8, NewsBar.Height).Contains(e.Location) && !NewsThumb().IsEmpty;
            if (over != newsBarHover)
            {
                newsBarHover = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate(new Rectangle(NewsBar.X - 2, NewsBar.Y, NewsBar.Width + 4, NewsBar.Height));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (newsDrag) { newsDrag = false; Invalidate(new Rectangle(NewsBar.X - 2, NewsBar.Y, NewsBar.Width + 4, NewsBar.Height)); }
            base.OnMouseUp(e);
        }

        public void SetStatus(string msg)
        {
            status = msg ?? "";
            Invalidate(new Rectangle(0, 578, W, H - 578));
        }

        // every message goes to the one status line above PLAY (there is no note in the bottom left any more)
        public void SetCloudNote(string text)
        {
            cloudNote = text ?? "";
            if (cloudNote.Length > 0) SetStatus(cloudNote);
        }

        // a section button is hidden while its feature is off: Cloud Save (cloud saving) and Stats (Options)
        bool NavShown(string name)
        {
            if (name == "Cloud Save") return CloudEnabled();
            if (name == "Stats") return StatsEnabled();
            return true;
        }

        // stack the section buttons
        void LayoutNav()
        {
            int k = 0;                                       // three per row under "Launcher"
            foreach (string name in navNames)
            {
                if (name == "Changelog") continue;           // its place is on the News line
                if (!NavShown(name)) { nav[name].Visible = false; continue; }
                nav[name].Visible = true;
                nav[name].Location = new Point(693 + (k % 3) * 134, 480 + (k / 3) * 34);
                k++;
            }
            if (strip != null) LayoutStrip();
        }

        public void RefreshPlayLabel()
        {
            if (GameRunningFlag) { playBtn.Label = "PLAYING..."; playBtn.FontSize = 24; }
            else if (Installing) { playBtn.Label = "INSTALLING"; playBtn.FontSize = 18; }
            else if (Updating) { playBtn.Label = "UPDATING"; playBtn.FontSize = 20; }
            else { playBtn.Label = ClientPresent() ? "PLAY" : "INSTALL"; playBtn.FontSize = 24; }
            Invalidate(new Rectangle(0, 578, W, H - 578));
            RefreshGates();
        }

        // Cloud Save and Addons are grayed out (and say why on hover) until the game is installed; every other button
        // (Guide, Changelog, Stats, Options, About, Discord, Update) works the same with or without the game
        const string CloudNeedsGame = "Cloud Save needs the game. Press INSTALL to download it first.";
        const string AddonsNeedGame = "Addons need the game. Press INSTALL to download it first.";

        void RefreshGates()
        {
            bool off = !GateOpen();
            foreach (string name in new[] { "Cloud Save", "Addons" })
            {
                string tip = name == "Addons" ? AddonsNeedGame : CloudNeedsGame;
                WowButton b;
                if (nav.TryGetValue(name, out b))
                {
                    b.GrayedTip = tip;
                    b.Grayed = off;
                }
                IconButton s;
                if (stripBtns != null && stripBtns.TryGetValue(name, out s))
                {
                    s.GrayedTip = tip;
                    s.Grayed = off;
                }
            }
        }

        // call after cloud saving is turned on or off: show/hide every cloud reference
        public void ApplyCloudVisibility()
        {
            LayoutNav();
            if (!CloudEnabled()) SetCloudNote("");
            Invalidate();
            if (current == "Cloud Save" && !CloudEnabled())
            {
                current = null;
                ShowSection("Home");
            }
            else RebuildCurrent();
        }

        public void SetCloudEnabled(bool on)
        {
            Cfg.Set("cloud_enabled", on);
            Cfg.Save();
            ApplyCloudVisibility();
            if (on && Cfg.Str("cloud_dir").Trim().Length == 0) ChooseCloudDir();      // cloud on needs a sync folder
        }

        // ------------------------------------------------------------------ window, tray
        void SetupTray()
        {
            tray = new NotifyIcon();
            tray.Icon = Theme.AppIcon;
            tray.Text = "Azeroth Survivors Launcher";
            var menu = new ContextMenuStrip();
            var show = trayShow = new ToolStripMenuItem("Show Launcher", null, delegate { ShowWindowFront(); });
            show.Font = new Font(show.Font, FontStyle.Bold);
            trayPlay = new ToolStripMenuItem("Play", null, delegate { Play(); });
            trayUpload = new ToolStripMenuItem("Upload Save Now", null, delegate { RunSync("push", false, true); });
            trayCloseToTray = new ToolStripMenuItem("Close Button Hides to Tray", null, delegate
            {
                Cfg.Set("close_to_tray", !Cfg.Bool("close_to_tray", true));
                Cfg.Save();
            });
            menu.Items.Add(show);
            menu.Items.Add(trayPlay);
            menu.Items.Add(trayUpload);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(trayCloseToTray);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(trayQuit = new ToolStripMenuItem("Quit", null, delegate { QuitApp(); }));
            TrayTexts();
            menu.Opening += delegate
            {
                trayPlay.Enabled = !GameRunningFlag;
                trayUpload.Enabled = !GameRunningFlag;
                trayUpload.Visible = CloudEnabled();
                trayCloseToTray.Checked = Cfg.Bool("close_to_tray", true);
            };
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowWindowFront(); };
            tray.Visible = true;
        }

        public void Notify(string msg)
        {
            try { if (tray != null) tray.ShowBalloonTip(5000, "Azeroth Survivors Launcher", Lang.T(msg), ToolTipIcon.Info); }
            catch (Exception) { }
        }

        void SetTrayTitle(string text)
        {
            if (tray == null) return;
            string t = "Azeroth Survivors Launcher - " + Lang.T(text);
            tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
        }

        void HideToTray()
        {
            Hide();
            WindowState = FormWindowState.Normal;
        }

        public void ShowWindowFront()
        {
            Show();
            WindowState = FormWindowState.Normal;
            TopMost = true;
            Activate();
            BringToFront();
            After(200, delegate { TopMost = false; });
            if (current != null && current != "Home") ShowSide();
        }

        void OnClosingWindow(object sender, FormClosingEventArgs e)
        {
            if (quitting || e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            if (Cfg.Bool("close_to_tray", true))
            {
                HideToTray();
                if (!trayNotified)
                {
                    trayNotified = true;
                    Notify("Still running in the tray. Right-click the icon to quit.");
                }
            }
            else QuitApp();
        }

        public void QuitApp()
        {
            StopEdge();
            if (SyncBusy)
            {
                ShowWindowFront();
                if (!Ask("Cloud sync running", "A cloud sync is still running. If you quit now it is cut short; the next " +
                         "sync repairs any half-finished copy.\n\nQuit anyway?")) return;
            }
            if (Installing)
            {
                ShowWindowFront();
                if (!Ask("Installing", "The game client is still being installed. If you quit now, it is cut short.\n\nQuit anyway?")) return;
            }
            if (Updating)
            {
                ShowWindowFront();
                if (!Ask("Updating", "The game is still being updated. If you quit now, the update is cut short. Nothing is " +
                         "lost: a half-finished change is undone the next time the launcher starts.\n\nQuit anyway?")) return;
            }
            if (GameRunningFlag)
            {
                ShowWindowFront();
                if (!Ask("Game running", "The game is still running. If you quit the launcher now, your saves will NOT be " +
                         "uploaded automatically.\n\nQuit anyway?")) return;
            }
            quitting = true;
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
            Application.Exit();
        }

        // the launcher and the section window both out of the way (the game started)
        void HideAll()
        {
            HideSide();
            HideToTray();
        }

        // ------------------------------------------------------------------ first-time setup
        void MaybeFirstRun()
        {
            if (setupAsked || setupOpen || Cfg.Bool("setup_done", false) || PreviewMode) return;
            if (!Visible) return;                            // started hidden: asked when the window is first opened
            if (startupLauncherCheck || Updating)           // a launcher update may restart us first
            {
                After(500, MaybeFirstRun);
                return;
            }
            setupAsked = true;
            RunSetup();
        }

        // First-time setup, and RUN SETUP AGAIN: the save, folders, cloud saving and addons questions. Nothing is
        // downloaded here: the game is only downloaded when the player presses INSTALL (and says they don't have it).
        public void RunSetup()
        {
            if (GameRunningFlag) { Info("Setup", "Close the game first, then run setup again."); return; }
            if (Updating || Installing) { SetStatus("Please wait until the game update or install has finished."); return; }
            ShowSetupDialog();
        }

        // INSTALL (no game yet): first asks whether the game is already on this PC. Yes: its folder is chosen and the
        // setup goes on as usual; no: the launcher downloads it.
        void AskHaveGame()
        {
            DialogResult r = Msg.Show(this, "Do you already have Azeroth Survivors downloaded on this PC?\n\n" +
                                            "YES: choose the folder it is in (the one with AzerothSurvivors.exe).\n" +
                                            "NO: the launcher downloads and installs it for you.",
                                      "Install", MessageBoxButtons.YesNoCancel);
            if (r == DialogResult.Yes)
            {
                if (!ChooseGameFolder()) return;
                if (!Cfg.Bool("setup_done", false)) After(200, ShowSetupDialog);
            }
            else if (r == DialogResult.No) InstallClient(!Cfg.Bool("setup_done", false));
        }

        void ShowSetupDialog()
        {
            // close the section window so only the setup dialog is in focus
            current = "Home";
            foreach (var b in nav.Values) b.Active = false;
            RefreshStrip();
            HideSide();
            var dlg = new SetupDialog(this);
            dlg.PlaceOver(this);
            setupOpen = true;
            try { dlg.ShowModal(Visible ? this : null); }
            finally { setupOpen = false; }
            if (dlg.Result != null)
            {
                foreach (var kv in dlg.Result) Cfg.Set(kv.Key, kv.Value);
                foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                    if (!dlg.Result.ContainsKey(key)) Cfg.Remove(key);   // back to the default location
                Cfg.Set("setup_done", true);
                Cfg.Save();
                SetStatus("Setup saved.");
                ApplyCloudVisibility();
                if (dlg.TrackStats.HasValue)                 // AZS Stats only when the player wants their stats tracked
                {
                    SetStatsEnabled(dlg.TrackStats.Value);
                    if (dlg.TrackStats.Value && ClientPresent()) InstallStatsAddon();
                    else if (dlg.TrackStats.Value) { Cfg.Set("stats_pending", true); Cfg.Save(); }   // once the game is installed
                }
                if (dlg.Goto != null)                        // page 2: straight to the Addons section
                {
                    addonMode = dlg.Goto;
                    current = null;
                    ShowSection("Addons");
                }
            }
            dlg.Dispose();
        }

        // ------------------------------------------------------------------ cloud folder choices
        public string PickCloudDir(IWin32Window owner, string currentDir)
        {
            var clouds = SyncEngine.FindClouds();
            string start = !string.IsNullOrEmpty(currentDir) ? currentDir :
                           Cfg.Str("cloud_dir").Length > 0 ? Cfg.Str("cloud_dir") : (clouds.Count > 0 ? clouds[0].Value : Util.Home);
            string d = AskFolder(owner, "Select a folder inside your cloud drive (Google Drive, OneDrive, Dropbox ...)",
                                 Directory.Exists(start) ? start : Util.Home);
            if (d == null) return null;
            d = Path.GetFullPath(d);
            bool topLevel = Util.IsDriveRoot(d);
            foreach (var c in clouds) if (Util.SamePath(d, c.Value)) topLevel = true;
            if (topLevel && Msg.Show(owner, "That is the top level of your cloud drive.\n\nCreate and use a subfolder named " +
                                            "Azeroth Survivor Launcher inside it?", "Sync folder", MessageBoxButtons.YesNo) == DialogResult.Yes)
                d = Path.Combine(d, "Azeroth Survivor Launcher");
            return d;
        }

        public void ChooseCloudDir()
        {
            string d = PickCloudDir(this, null);
            if (d != null)
            {
                Cfg.Set("cloud_dir", d);
                Cfg.Save();
                RebuildCurrent();
            }
        }

        // ------------------------------------------------------------------ cloud sync runs
        SyncEngine MakeEngine()
        {
            var eng = new SyncEngine(SyncSettings());
            eng.Log = SyncLog;
            eng.Confirm = delegate(string msg)
            {
                return UiCall<bool>(delegate
                {
                    ShowWindowFront();
                    return Ask("Cloud Save", msg);
                });
            };
            eng.AskConflict = delegate(List<SyncSet> sets, bool downloading)
            {
                return UiCall<Choice>(delegate
                {
                    ShowWindowFront();
                    using (var dlg = new ConflictDialog(sets, downloading))
                    {
                        dlg.PlaceOver(this);
                        dlg.ShowModal(this);
                        return dlg.Result;
                    }
                });
            };
            return eng;
        }

        void SyncLog(string msg, Level level)
        {
            lock (cloudLog)
            {
                cloudLog.Add(new KeyValuePair<string, Level>(msg, level));
                if (cloudLog.Count > 400) cloudLog.RemoveRange(0, cloudLog.Count - 400);
            }
            try
            {
                Directory.CreateDirectory(Util.StateDir);
                string logf = Path.Combine(Util.StateDir, "log.txt");
                if (File.Exists(logf) && new FileInfo(logf).Length > 1000000)
                    Util.Replace(logf, Path.Combine(Util.StateDir, "log.old.txt"));
                File.AppendAllText(logf, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ", CultureInfo.InvariantCulture) + msg + "\r\n",
                                   Encoding.UTF8);
            }
            catch (Exception) { }
            Ui(delegate
            {
                if (logBox != null && !logBox.IsDisposed)
                {
                    UI.Append(logBox, Lang.T(msg) + "\n", UI.LevelColor(level), null);
                    logBox.ScrollToCaret();
                }
            });
        }

        public void RunSync(string kind, bool force, bool quiet)
        {
            RunSync(kind, force, quiet, null);
        }

        // kind: status, pull, push, or restore (stamp = which kept download)
        public void RunSync(string kind, bool force, bool quiet, string stamp)
        {
            if (SyncBusy || GameRunningFlag || Installing)
            {
                SetStatus("Another task is still running - wait for it to finish.");
                return;
            }
            if (!CloudEnabled()) return;
            if (!GateOpen())                                 // no cloud sync before the game is installed
            {
                if (quiet) Notify(CloudNeedsGame); else SetStatus(CloudNeedsGame);
                return;
            }
            if (Cfg.Str("cloud_dir").Trim().Length == 0)
            {
                string msg = "Choose a sync folder inside your cloud drive first (Cloud Save or Options).";
                if (quiet) Notify(msg); else Warn("Cloud Save", msg);
                return;
            }
            SyncBusy = true;
            SyncEngine eng = MakeEngine();
            Bg(delegate
            {
                bool ok = false;
                string err = null;
                try
                {
                    ok = kind == "status" ? eng.Status() : kind == "pull" ? eng.Pull(force) :
                         kind == "restore" ? eng.RestoreDownload(stamp) : eng.Push(force);
                }
                catch (Exception e)
                {
                    err = e.Message;
                    SyncLog("  " + err, Level.Error);
                }
                finally { eng.EndProgress(); }
                Ui(delegate { SyncDone(kind, ok, err, quiet); });
            });
        }

        void SyncDone(string kind, bool ok, string err, bool quiet)
        {
            SyncBusy = false;
            string stamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string text = err != null ? "Cloud sync failed." : kind == "status" ? "Status checked at " + stamp :
                          kind == "restore" ? (ok ? "Earlier download restored." : "Cancelled, nothing changed.") :
                          ok ? (kind == "pull" ? "Downloaded from the cloud" : "Saves uploaded") + " at " + stamp
                             : "Not everything was synced - see the Cloud Save log.";
            SetStatus("Ready.");                             // cloud results stay in the Cloud Save log
            if (quiet) Notify(err ?? text);
            else if (err != null)
            {
                ShowWindowFront();
                Error("Cloud Save", err);
            }
            if (current == "Cloud Save") RebuildCurrent();
            // settings downloaded: the addons they belong to are installed if they are missing
            if (ok && err == null && (kind == "pull" || kind == "restore"))
                Bg(delegate
                {
                    if (InstallAddonsForSettings().Count > 0)
                        Ui(delegate { if (current == "Addons") BuildPanel("Addons"); });
                });
        }

        // Cloud Save syncs only the addons' settings: a Download More addon whose settings are here but whose folders
        // are not is installed (on a background thread). Returns the names installed.
        List<string> InstallAddonsForSettings()
        {
            var done = new List<string>();
            SyncSettings ss = SyncSettings();
            List<AddonEntry> missing;
            try { missing = Addons.MissingForSettings(ss.AddonsDir, ss.SavedDir, Data.Addons); }
            catch (Exception e) { SyncLog("  Addons for the synced settings not checked: " + e.Message, Level.Warn); return done; }
            foreach (var entry in missing)
            {
                try
                {
                    Directory.CreateDirectory(ss.AddonsDir);
                    List<string> names;
                    BuiltinAddon builtin = BuiltinAddons.Find(entry.Url);
                    if (builtin != null) { BuiltinAddons.Install(ss.AddonsDir, builtin); names = new List<string> { builtin.Folder }; }
                    else names = Archive.InstallAddonZip(entry.Url, ss.AddonsDir);
                    string name = entry.Name;
                    var rec = new List<object>();
                    foreach (string n in names) rec.Add(n);
                    Ui(delegate
                    {
                        AddonInstalls()[name] = rec;
                        AddonInstalledAt()[name] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                        Cfg.Save();
                    });
                    SyncLog(name + " installed: its settings came from the cloud.", Level.Good);
                    done.Add(name);
                }
                catch (Exception e) { SyncLog("  " + entry.Name + " not installed: " + e.Message, Level.Warn); }
            }
            return done;
        }

        // ------------------------------------------------------------------ play: download newest -> run game -> upload
        void Play()
        {
            if (GameRunningFlag) return;
            if (Installing)
            {
                InstallClient();                             // asks whether to stop the download
                return;
            }
            if (Updating)
            {
                SetStatus("The game is being updated. Please wait until that has finished.");
                return;
            }
            if (!ClientPresent())
            {
                AskHaveGame();
                return;
            }
            // PLAY first makes sure this is the newest launcher: a newer one is installed (and the launcher restarts)
            // instead of starting the game; when GitHub can't be reached, the game starts as it is
            if (!playChecking && !StartedAfterUpdate && !PreviewMode)
            {
                playChecking = true;
                SetStatus("Checking for a launcher update...");
                Bg(delegate
                {
                    LauncherRelease rel = null;
                    bool differs = false;
                    try { rel = SelfUpdate.FetchLatest(); differs = SelfUpdate.Differs(rel); }
                    catch (Exception) { }
                    Ui(delegate
                    {
                        playChecking = false;
                        if (differs && rel != null && !Updating)
                        {
                            launcherRelease = rel;
                            RefreshUpdateBtn();
                            SetStatus("A new launcher (" + rel.Name + ") is installed first; press PLAY again after it restarts.");
                            ForceLauncherUpdate();
                            return;
                        }
                        StartGame();
                    });
                });
                return;
            }
            if (playChecking) return;
            StartGame();
        }

        bool playChecking;                                   // PLAY is checking for a launcher update

        void StartGame()
        {
            if (GameRunningFlag || Installing || Updating || !ClientPresent()) return;
            string exe = Path.Combine(GameDir(), Util.GameExe);
            GameRunningFlag = true;
            RefreshPlayLabel();
            SyncEngine eng = (CloudEnabled() && Cfg.Str("cloud_dir").Trim().Length > 0) ? MakeEngine() : null;
            bool cloudOnButNoFolder = CloudEnabled() && eng == null;
            bool minimize = Cfg.Bool("minimize_on_play", true);
            Bg(delegate { Session(exe, eng, cloudOnButNoFolder, minimize); });
        }

        // A copy of your AZS Stats records before every game start, the last 10 kept, in the launcher's
        // backups\AZS Stats folder: if the game ever loses the file, an older copy is there to put back.
        static void BackupStats(string savedDir)
        {
            try
            {
                string src = Path.Combine(savedDir ?? "", Stats.FileName);
                if (!File.Exists(src) || new FileInfo(src).Length < 200) return;
                string dir = Path.Combine(Util.DataDir, "backups", "AZS Stats");
                Directory.CreateDirectory(dir);
                File.Copy(src, Path.Combine(dir, "AZSStats " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".lua"), true);
                var files = new List<string>(Directory.GetFiles(dir, "AZSStats *.lua"));
                files.Sort(StringComparer.Ordinal);
                for (int i = 0; i < files.Count - 10; i++) { try { File.Delete(files[i]); } catch (Exception) { } }
            }
            catch (Exception) { }
        }

        void Session(string exe, SyncEngine eng, bool cloudOnButNoFolder, bool minimize)
        {
            var problems = new List<string>();
            bool launched = false;
            Ui(delegate { SetCloudNote(""); });
            try
            {
                if (eng != null)
                {
                    bool complete;
                    try
                    {
                        complete = eng.Pull(false);          // asks only on a real conflict
                        eng.EndProgress();
                    }
                    catch (Exception e)
                    {
                        SyncLog("  " + e.Message, Level.Error);
                        problems.Add("The cloud save could NOT be loaded, so the game was not launched.\n\n" + e.Message);
                        Ui(delegate { SetCloudNote("Cloud sync failed - game not launched"); SetStatus("Ready."); });
                        return;
                    }
                    if (!complete)                           // cloud save not loaded -> never start the game
                    {
                        SyncLog("Cloud save was not loaded - game not launched.", Level.Error);
                        problems.Add("The cloud save could NOT be loaded, so the game was not launched.\n\nSee the log in " +
                                     "Cloud Save. If your cloud drive is still syncing, wait for it to finish and press PLAY again.");
                        Ui(delegate { SetCloudNote("Cloud sync failed - game not launched"); SetStatus("Ready."); });
                        return;
                    }
                    Ui(delegate { SetCloudNote(""); });
                    InstallAddonsForSettings();              // the addons whose settings came from the cloud
                }
                else if (cloudOnButNoFolder) problems.Add("No sync folder set - launched without cloud sync.");
                // the AZS Stats addon this launcher carries goes in right before the game starts: after the cloud
                // download, which could otherwise bring back an older copy from another PC
                try
                {
                    SyncSettings ss = SyncSettings();
                    BackupStats(ss.SavedDir);
                    if (Stats.AddonInstalled(ss.AddonsDir) && Stats.AddonOutdated(ss.AddonsDir))
                    {
                        Stats.InstallAddon(ss.AddonsDir, ss.SavedDir);
                        SyncLog("AZS Stats addon updated to the launcher's version.", Level.Info);
                    }
                }
                catch (Exception e) { SyncLog("  AZS Stats addon not updated: " + e.Message, Level.Warn); }
                // the other AZS addons the launcher carries (AZS Handbook, AZS Minor Mods): the installed ones brought up
                // to the launcher's version too, and the retired ones (AZS Transparency) taken out
                try
                {
                    foreach (string f in BuiltinAddons.RemoveRetired(SyncSettings().AddonsDir, SyncSettings().SavedDir))
                        SyncLog(f + " addon removed (it is no longer part of the launcher).", Level.Info);
                    foreach (string f in BuiltinAddons.UpdateInstalled(SyncSettings().AddonsDir))
                        SyncLog(f + " addon updated to the launcher's version.", Level.Info);
                    foreach (string f in Addons.EnforceConflicts(SyncSettings().AddonsDir))
                        SyncLog(f + " addon switched off: it conflicts with another addon that is on.", Level.Info);
                }
                catch (Exception e) { SyncLog("  AZS addons not updated: " + e.Message, Level.Warn); }
                SyncLog("Starting Azeroth Survivors ...", Level.Info);
                Ui(delegate
                {
                    SetStatus("Game running.");
                    if (minimize) HideAll();                 // Options: minimize the launcher when the game starts
                    SetTrayTitle("Game running");
                });
                try
                {
                    var psi = new ProcessStartInfo(exe);
                    psi.WorkingDirectory = Path.GetDirectoryName(exe);
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi))
                    {
                        launched = true;
                        p.WaitForExit();                     // returns on normal exit or crash
                    }
                    while (Util.GameRunning()) Thread.Sleep(2000);   // the game may hand over to another process
                }
                catch (Exception e)
                {
                    problems.Add("Could not start the game: " + e.Message);
                }
                if (launched)
                {
                    SyncLog("Game closed.", Level.Info);
                    Thread.Sleep(2000);                      // short pause, then pop back up in front
                    Ui(ShowWindowFront);
                    if (eng == null) Ui(delegate { SetCloudNote("Game has closed."); });
                }
                if (launched && eng != null)
                {
                    Thread.Sleep(1000);                      // the upload starts 3 s after the game exits
                    try
                    {
                        if (eng.Push(false))
                            Ui(delegate { SetStatus("Ready."); SetCloudNote(""); });
                        else
                        {
                            problems.Add("Some of your progress was NOT uploaded. See the log in Cloud Save.");
                            Ui(delegate { SetCloudNote("Cloud sync NOT completed - see the Cloud Save log"); });
                        }
                    }
                    catch (Exception e)
                    {
                        SyncLog("  " + e.Message, Level.Error);
                        problems.Add("Cloud upload failed: " + e.Message);
                        Ui(delegate { SetCloudNote("Cloud sync failed - see the Cloud Save log"); });
                    }
                    eng.EndProgress();
                }
            }
            finally
            {
                Ui(delegate { SessionDone(problems); });
            }
        }

        // ------------------------------------------------------------------ play time
        // Counted in game by the AZS Stats addon (the time the game runs) and read from its saved file, which
        // cloud saving carries between PCs with the other addon settings. The launcher no longer counts it.
        public static string Duration(long seconds)
        {
            long h = seconds / 3600, m = seconds % 3600 / 60;
            return h > 0 ? h + "h " + m.ToString("00") + "m" : m + "m";
        }

        RunStats statsCache;
        string statsCacheKey = "";

        // the addon's stats, read again only when its file changed; null without the addon's file
        public RunStats AddonStats()
        {
            try
            {
                string f = Stats.FindFile(SyncSettings().SavedDir);
                if (f == null) { statsCache = null; statsCacheKey = ""; return null; }
                string key = f + "|" + File.GetLastWriteTimeUtc(f).Ticks;
                if (key != statsCacheKey)
                {
                    statsCache = Stats.Load(SyncSettings().SavedDir);
                    statsCacheKey = key;
                }
                return statsCache;
            }
            catch (Exception) { return null; }
        }

        // what the Home screen, Options and Stats show
        public PlayStats ShownPlayStats()
        {
            RunStats st = AddonStats();
            return st != null ? st.Play : new PlayStats();
        }

        void SessionDone(List<string> problems)
        {
            GameRunningFlag = false;
            Invalidate();                                    // the play time the game just saved
            RefreshPlayLabel();
            SetTrayTitle("Idle");
            ShowWindowFront();                               // the launcher stays up front after the session
            if (problems.Count > 0) Warn("Launcher", string.Join("\n\n", problems));
        }

        // ------------------------------------------------------------------ installing the game client
        void InstallClient() { InstallClient(false); }

        bool setupAfterInstall;
        bool dlActive;                                       // the game download is running: the status line shows its bar
        long dlDone, dlTotal;

        void SetDownloadBar(bool on, long done, long total)
        {
            dlActive = on;
            dlDone = done;
            dlTotal = total;
            Invalidate(new Rectangle(0, 578, W, H - 578));
        }

        ManualResetEvent installCancel;
        bool downloadInstall;

        // INSTALL: no window of its own. The download and the install run in the launcher's status line (a bar with the
        // percentage above INSTALLING); pressing INSTALLING asks whether to stop the download.
        void InstallClient(bool forSetup)
        {
            if (Updating)
            {
                SetStatus("The game is being updated. Please wait until that has finished.");
                return;
            }
            if (Installing)
            {
                if (installCancel != null && dlActive && Ask("Install", "Stop downloading the game?"))
                    installCancel.Set();
                return;
            }
            if (GameRunningFlag)
            {
                Info("Install", "Close the game first, then install it again.");
                return;
            }
            setupAfterInstall = forSetup;
            BeginClientDownload();
        }

        // The game download, done by the launcher: into <launcher folder>\temp, with the download server's login,
        // then unpacked next to the launcher; the temp folder is deleted afterwards, whatever happened.
        void BeginClientDownload()
        {
            Installing = true;
            RefreshPlayLabel();
            SetStatus("Downloading the game client...");
            SetDownloadBar(true, 0, 0);
            string temp = Path.Combine(Util.LauncherDir, "temp");
            string dest = Util.LauncherDir;
            installCancel = new ManualResetEvent(false);
            ManualResetEvent cancel = installCancel;
            Bg(delegate
            {
                string error = null;
                bool cancelled = false;
                try
                {
                    Util.DeleteTreeQuiet(temp);
                    Directory.CreateDirectory(temp);
                    string file = Path.Combine(temp, "AzerothSurvivors-client.download");
                    long last = -1;
                    int lastTick = 0;
                    Action<long, long> progress = delegate(long done, long total)
                    {
                        int pct = total > 0 ? (int)(done * 1000 / total) : (int)(done >> 20);
                        if (pct == last && Environment.TickCount - lastTick < 1000) return;
                        last = pct;
                        lastTick = Environment.TickCount;
                        Ui(delegate { SetDownloadBar(true, done, total); });
                    };
                    DownloadClient(file, progress, cancel, delegate(string text) { });
                    Ui(delegate { SetDownloadBar(false, 0, 0); SetStatus("Unpacking the game client..."); });
                    if (Archive.Kind(file) == null)
                        throw new UserError("The download server sent something that is not the game archive. Please try again later.");
                    Archive.InstallGame(file, dest);
                }
                catch (CancelledError) { cancelled = true; }
                catch (Exception e) { error = e.Message; }
                finally { Util.DeleteTreeQuiet(temp); }        // the download is never kept
                Ui(delegate
                {
                    SetDownloadBar(false, 0, 0);
                    installCancel = null;
                    if (cancelled)
                    {
                        Installing = false;
                        setupAfterInstall = false;
                        RefreshPlayLabel();
                        SetStatus("Download cancelled.");
                        return;
                    }
                    if (error != null)
                    {
                        Installing = false;
                        setupAfterInstall = false;
                        RefreshPlayLabel();
                        SetStatus("Install failed.");
                        ShowWindowFront();
                        Error("Install", error);
                        return;
                    }
                    bool setup = setupAfterInstall || !Cfg.Bool("setup_done", false);
                    setupAfterInstall = false;
                    downloadInstall = true;
                    try { InstallDone(null, dest); }
                    finally { downloadInstall = false; }
                    if (setup) After(300, ShowSetupDialog);  // first-time setup / RUN SETUP AGAIN: on to the folders page
                });
            });
        }

        static readonly Random hostDice = new Random();

        // One of the hosts at random; when it fails (not when you cancel), the next one.
        static void DownloadClient(string file, Action<long, long> progress, ManualResetEvent cancel, Action<string> say)
        {
            var hosts = new List<int> { 0, 1, 2 };            // 0 main, 1 backup, 2 Google Drive
            lock (hostDice)
                for (int i = hosts.Count - 1; i > 0; i--) { int j = hostDice.Next(i + 1); int t = hosts[i]; hosts[i] = hosts[j]; hosts[j] = t; }
            string[] names = { "main host", "backup host", "Google Drive host" };
            var errors = new List<string>();
            for (int i = 0; i < hosts.Count; i++)
            {
                int h = hosts[i];
                say(i == 0 ? "Downloading from the " + names[h] + "..."
                           : "The " + names[hosts[i - 1]] + " did not work. Trying the " + names[h] + "...");
                try
                {
                    if (h == 0) DownloadFromMain(file, progress, cancel);
                    else if (h == 1) DownloadFromMirror(file, progress, cancel);
                    else DownloadFromDrive(file, progress, cancel);
                    return;
                }
                catch (CancelledError) { throw; }
                catch (UserError e) { errors.Add(char.ToUpperInvariant(names[h][0]) + names[h].Substring(1) + ": " + e.Message); }
            }
            say("The other hosts did not work. Trying the game's own download page...");
            try { DownloadFromGameSite(file, progress, cancel); return; }
            catch (CancelledError) { throw; }
            catch (UserError e) { errors.Add("Game's download page: " + e.Message); }
            throw new UserError("No download host worked.\n\n" + string.Join("\n\n", errors.ToArray()));
        }

        // the downloaded game against its known checksums: a damaged or different file is deleted, never installed
        static void CheckClient(string file, string sha, string md5, bool drive, string where)
        {
            bool ok = drive ? string.Equals(md5, ClientDriveMd5, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(sha, ClientSha256, StringComparison.OrdinalIgnoreCase)
                            : string.Equals(sha, ClientSha256, StringComparison.OrdinalIgnoreCase)
                              && (md5 == null || string.Equals(md5, ClientMd5, StringComparison.OrdinalIgnoreCase));
            if (ok) return;
            try { File.Delete(file); } catch (Exception) { }
            throw new UserError("The download from the " + where + " is damaged or not the right file (its checksum does not match). Please try again.");
        }

        // the main host, with the download login: sent the plain HTTP way (Basic); a server that wants it in the
        // address instead gets that next
        static void DownloadFromMain(string file, Action<long, long> progress, ManualResetEvent cancel)
        {
            string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(ClientUser + ":" + ClientPassword));
            Action<HttpWebRequest> login = delegate(HttpWebRequest r) { r.Headers["Authorization"] = "Basic " + basic; };
            string q = ClientUrl.Contains("?") ? "&" : "?";
            string u = Uri.EscapeDataString(ClientUser), pw = Uri.EscapeDataString(ClientPassword);
            string[] urls = { ClientUrl, ClientUrl + q + "username=" + u + "&password=" + pw, ClientUrl + q + "user=" + u + "&pass=" + pw };
            UserError lastError = null;
            foreach (string url in urls)
            {
                try
                {
                    string sha = Net.Download(url, file, true, "download", progress, cancel, 0, login);
                    CheckClient(file, sha, Net.LastMd5, false, "main host");
                    return;
                }
                catch (CancelledError) { throw; }
                catch (UserError e)
                {
                    lastError = e;
                    if (!e.Message.Contains("error 401") && !e.Message.Contains("error 403")) throw;   // only a refused login tries the next way
                }
            }
            throw new UserError("The download server did not accept the launcher's login." +
                                (lastError != null ? " (" + lastError.Message + ")" : ""));
        }

        // the last resort: the game's own download page (https://wow.aethus.pl/download)
        static void DownloadFromGameSite(string file, Action<long, long> progress, ManualResetEvent cancel)
        {
            string sha = Net.Download(ClientLastResortUrl, file, true, "game's download page", progress, cancel, 0);
            if (string.Equals(sha, ClientSha256, StringComparison.OrdinalIgnoreCase)) return;
            if (Archive.Kind(file) != null) return;
            try { File.Delete(file); } catch (Exception) { }
            throw new UserError("The game's download page did not give a game archive.");
        }

        // the backup host: its page names the newest archive (the link changes with every upload) and its SHA-256;
        // the download must match the game's known checksums (and the page's, when it shows one)
        static void DownloadFromMirror(string file, Action<long, long> progress, ManualResetEvent cancel)
        {
            string html;
            using (HttpWebResponse resp = Net.Open(ClientMirrorUrl, 30000, true, "backup download", "text/html"))
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                html = rd.ReadToEnd();
            var link = System.Text.RegularExpressions.Regex.Match(html,
                "href\\s*=\\s*[\"']([^\"']*/downloads/[^\"']+\\.(?:rar|zip|7z))[\"']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!link.Success) throw new UserError("The backup host's page has no download link right now.");
            string url = new Uri(new Uri(ClientMirrorUrl), System.Net.WebUtility.HtmlDecode(link.Groups[1].Value)).AbsoluteUri;
            var sum = System.Text.RegularExpressions.Regex.Match(html, "SHA-?256.{0,300}?\\b([0-9a-fA-F]{64})\\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            string sha = Net.Download(url, file, true, "backup download", progress, cancel, 0);
            if (sum.Success && !string.Equals(sum.Groups[1].Value, sha, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(file); } catch (Exception) { }
                throw new UserError("The download from the backup host is damaged (its checksum does not match). Please try again.");
            }
            CheckClient(file, sha, Net.LastMd5, false, "backup host");
        }

        // Google Drive: a large file first gives a "can't scan this file for viruses" page whose form holds the real
        // download address; that form is followed, then the file is checked against its MD5
        static void DownloadFromDrive(string file, Action<long, long> progress, ManualResetEvent cancel)
        {
            string url = "https://drive.usercontent.google.com/download?id=" + ClientDriveId + "&export=download&confirm=t";
            // only the first byte is asked for: the file itself is then fetched by the download below
            using (HttpWebResponse resp = Net.Open(url, 30000, true, "Google Drive download", null,
                                                   delegate(HttpWebRequest r) { r.AddRange(0L, 0L); }))
            {
                if ((resp.ContentType ?? "").ToLowerInvariant().Contains("text/html"))
                {
                    string html;
                    using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) html = rd.ReadToEnd();
                    var form = System.Text.RegularExpressions.Regex.Match(html, "<form[^>]*action=\"([^\"]+)\"[^>]*>(.*?)</form>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (!form.Success) throw new UserError("Google Drive did not offer the file right now (it may have hit its download limit).");
                    var q = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(form.Groups[2].Value,
                        "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        q.Add(Uri.EscapeDataString(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)) + "=" +
                              Uri.EscapeDataString(System.Net.WebUtility.HtmlDecode(m.Groups[2].Value)));
                    url = new Uri(new Uri(url), System.Net.WebUtility.HtmlDecode(form.Groups[1].Value)).AbsoluteUri;
                    url += (url.Contains("?") ? "&" : "?") + string.Join("&", q.ToArray());
                }
            }
            string sha = Net.Download(url, file, true, "Google Drive download", progress, cancel, 0);
            CheckClient(file, sha, Net.LastMd5, true, "Google Drive host");
        }

        bool setupAfterFileInstall;

        public void InstallFromDownloads() { InstallFromDownloads(false); }

        public void InstallFromDownloads(bool forSetup)
        {
            if (Updating || Installing) return;
            setupAfterFileInstall = forSetup;
            string archive = Archive.FindDownloaded();
            if (archive != null)
            {
                var fi = new FileInfo(archive);
                if (!Ask("Install", "Install the game from this file?\n\n" + fi.Name + "  (" + (fi.Length >> 20) + " MB)\nin " + fi.DirectoryName))
                    archive = null;
            }
            if (archive == null)
            {
                using (var d = new OpenFileDialog())
                {
                    d.Title = Lang.T("Choose the game file you downloaded (.rar)");
                    d.InitialDirectory = Util.DownloadsDir;
                    d.Filter = Lang.T("Game archive (*.rar;*.zip;*.7z)|*.rar;*.zip;*.7z|All files (*.*)|*.*");
                    if (d.ShowDialog(this) != DialogResult.OK)
                    {
                        SetStatus("Install cancelled.");
                        return;
                    }
                    archive = d.FileName;
                }
            }
            string dest = Util.LauncherDir;                  // installs next to the launcher, no folder question
            Installing = true;
            RefreshPlayLabel();
            SetStatus("Unpacking the game client...");
            Bg(delegate
            {
                string error = null;
                try
                {
                    Archive.InstallGame(archive, dest);
                    try { File.Delete(archive); } catch (Exception) { }     // the download is no longer needed
                }
                catch (Exception e) { error = e.Message; }
                Ui(delegate { InstallDone(error, dest); });
            });
        }

        void InstallDone(string error, string gameDir)
        {
            Installing = false;
            if (error == null)
            {
                Cfg.Set("game_dir", gameDir);
                Cfg.Set("installed_by_launcher", true);
                Cfg.Save();
            }
            Invalidate();
            RebuildCurrent();
            if (error != null)
            {
                SetStatus("Install failed.");
                ShowWindowFront();
                Error("Install", error);
            }
            else
            {
                SetStatus("Install complete - press PLAY.");
                if (Cfg.Bool("stats_pending", false))        // setup said yes to AZS Stats before the game was here
                {
                    Cfg.Remove("stats_pending");
                    Cfg.Save();
                    InstallStatsAddon();
                }
                Notify("Install complete. Press PLAY to enter Azeroth.");
                if (!downloadInstall && (setupAfterFileInstall || !Cfg.Bool("setup_done", false)))
                    After(300, ShowSetupDialog);             // installed from a file during setup: on to the folders page
                setupAfterFileInstall = false;
                if (Updater.Enabled) After(800, delegate { CheckForUpdate(false, true); });   // is it already the newest?
            }
        }

        // ------------------------------------------------------------------ game updates (checked at start and with UPDATE)
        public string InstalledVersion() { return Cfg.Str("client_version").Trim(); }

        void RefreshUpdateBtn()
        {
            updateBtn.Label = UpdateChecking ? "CHECKING" : Updating ? "UPDATING" : "UPDATE LAUNCHER";
            updateBtn.Selected = (UpdateAvailable || launcherRelease != null) && !Updating;   // gold: an update is waiting
        }

        void TellUpdateRecovery()
        {
            string r = updateRecovery;
            updateRecovery = null;
            if (r == null) return;
            ShowWindowFront();
            if (r == "restored")
                Info("Game update", "The last game update was cut off before it finished. Your game files were put back " +
                     "exactly as they were.\n\nYou can run the update again with the UPDATE button.");
            else
                Warn("Game update", "The last game update was cut off, and some game files could not be put back " +
                     "automatically.\n\nThe old versions are kept in the folder \"" + Updater.WorkDir + "\\backup\" inside the " +
                     "game folder. Copy them back over the game files, or reinstall the game.");
        }

        void StartupUpdateCheck()
        {
            startupLauncherCheck = true;
            CheckLauncherUpdate(false);
            if (!UpdateChecking) startupLauncherCheck = false;   // the check could not start (another one is running)
        }

        // ---- the launcher itself: compared with the exe on the latest GitHub release ----
        LauncherRelease launcherRelease;                     // a different (newer) launcher on GitHub, when found
        bool startupLauncherCheck;                           // the start-up check is still waiting for GitHub

        // Started by a launcher update? Then a launcher that still differs is offered, not forced,
        // so a release whose exe never matches can't restart the launcher over and over.
        static readonly bool StartedAfterUpdate = Array.IndexOf(Environment.GetCommandLineArgs(), "--after-update") >= 0;

        // The UPDATE button and the start-up check: the launcher first, then (when switched on) the game.
        // At start-up it stays quiet unless there is something to offer; the button always answers.
        void CheckLauncherUpdate(bool manual)
        {
            if (Updating)
            {
                if (updateDlg != null && !updateDlg.IsDisposed) ShowUpdateDialog();
                return;
            }
            if (UpdateChecking) return;
            UpdateChecking = true;
            RefreshUpdateBtn();
            if (manual) SetStatus("Checking for updates...");
            Bg(delegate
            {
                LauncherRelease rel = null;
                bool differs = false;
                string err = null;
                try
                {
                    rel = SelfUpdate.FetchLatest();
                    differs = SelfUpdate.Differs(rel);
                }
                catch (UserError e) { err = e.Message; }
                catch (Exception e) { err = "The update check failed (" + e.Message + ")."; }
                Ui(delegate { LauncherCheckDone(rel, differs, err, manual); });
            });
        }

        void LauncherCheckDone(LauncherRelease rel, bool differs, string err, bool manual)
        {
            UpdateChecking = false;
            startupLauncherCheck = false;
            launcherRelease = err == null && differs ? rel : null;
            RefreshUpdateBtn();
            if (!manual && launcherRelease != null && !StartedAfterUpdate)
            {
                ForceLauncherUpdate();                       // at start-up a newer launcher is installed without asking
                return;
            }
            if (err != null)
            {
                if (manual)
                {
                    SetStatus("Could not check for launcher updates.");
                    Warn("Launcher update", err);
                }
            }
            else if (launcherRelease != null)
            {
                SetStatus("Launcher " + rel.Name + " is available.");
                OfferLauncherUpdate(manual);
                return;
            }
            else if (manual && !Updater.Enabled)
            {
                SetStatus("The launcher is up to date (" + rel.Name + ").");
                Info("Launcher update", "You have the newest launcher (" + rel.Name + ").");
            }
            NextGameCheck(manual);
        }

        void NextGameCheck(bool manual)
        {
            if (!Updater.Enabled) return;                    // game updates are switched on in src/Update.cs
            if (manual) CheckForUpdate(true, false);
            else if (ClientPresent()) CheckForUpdate(false, false);
        }

        void OfferLauncherUpdate(bool manual)
        {
            LauncherRelease rel = launcherRelease;
            if (rel == null) return;
            if (!manual && setupOpen)
            {
                After(3000, delegate { OfferLauncherUpdate(false); });
                return;
            }
            if (!manual && !Visible)                         // hidden in the tray: the gold UPDATE button waits
            {
                Notify("A new launcher (" + rel.Name + ") is available. Open the launcher and press UPDATE.");
                return;
            }
            if (GameRunningFlag || Installing || SyncBusy || Updating)
            {
                if (manual) Info("Launcher update", "Finish what is running first (the game, an install or a cloud sync), then press UPDATE again.");
                return;
            }
            if (!Ask("Launcher update", "A new version of the launcher is on GitHub: " + rel.Name + ".\n\nUpdate now? The new launcher" +
                     (rel.Size > 0 ? " (" + Util.FmtSize(rel.Size) + ")" : "") + " is downloaded, checked and started in place of this one. " +
                     "Your settings stay as they are."))
            {
                SetStatus("Launcher update postponed. Press UPDATE when you want it.");
                NextGameCheck(manual);
                return;
            }
            DownloadLauncher(rel, false);
        }

        // The start-up update: no question. It waits while the game, an install or a cloud sync runs
        // (so nothing is cut off), then downloads, checks and restarts into the new launcher.
        void ForceLauncherUpdate()
        {
            LauncherRelease rel = launcherRelease;
            if (rel == null || Updating) return;
            if (GameRunningFlag || Installing || SyncBusy || UpdateChecking || setupOpen)
            {
                SetStatus("A launcher update (" + rel.Name + ") will be installed as soon as the game, an install or a cloud sync has finished.");
                After(5000, ForceLauncherUpdate);
                return;
            }
            Notify("Updating the launcher to " + rel.Name + "...");
            DownloadLauncher(rel, true);
        }

        void DownloadLauncher(LauncherRelease rel, bool forced)
        {
            Updating = true;
            RefreshPlayLabel();
            RefreshUpdateBtn();
            SetStatus(forced ? "Updating the launcher to " + rel.Name + "..." : "Downloading the new launcher...");
            Bg(delegate
            {
                string err = null, path = null;
                DateTime last = DateTime.MinValue;
                try
                {
                    path = SelfUpdate.Download(rel, delegate(long done, long total)
                    {
                        if ((DateTime.Now - last).TotalSeconds < 0.2) return;
                        last = DateTime.Now;
                        string t = "Downloading the new launcher... " + (total > 0 ? (done * 100 / total) + "%" : Util.FmtSize(done));
                        Ui(delegate { SetStatus(t); });
                    }, null);
                }
                catch (Exception e) { err = e.Message; }
                Ui(delegate { LauncherDownloaded(path, err, forced); });
            });
        }

        void LauncherDownloaded(string path, string err, bool forced)
        {
            if (err == null)
            {
                try { SelfUpdate.InstallAndRestart(path); }        // the new one opens its window once this one is gone
                catch (Exception e) { err = e.Message; }
            }
            if (err != null)
            {
                Updating = false;
                RefreshPlayLabel();
                RefreshUpdateBtn();
                SetStatus("Launcher update failed.");
                if (forced)
                {
                    // no internet or GitHub down: don't lock the player out; the next start tries again
                    Error("Launcher update", err + "\n\nYou can keep using this version for now. The launcher tries again the next time it starts.");
                    NextGameCheck(false);
                }
                else Error("Launcher update", err);
                return;
            }
            quitting = true;                                 // the new launcher is starting: close this one
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
            Application.Exit();
            // shut down completely even if a window or background thread holds on: the new launcher is waiting
            var kill = new System.Threading.Thread(delegate() { System.Threading.Thread.Sleep(3000); Environment.Exit(0); });
            kill.IsBackground = true;
            kill.Start();
        }

        // Reads update.json in the background. At start-up it stays quiet unless there is an update to offer;
        // the UPDATE button (manual) always answers.
        void CheckForUpdate(bool manual, bool afterInstall)
        {
            if (Updating) { ShowUpdateDialog(); return; }
            if (UpdateChecking) return;
            if (!Updater.Enabled)
            {
                if (manual) Info("Game update", "Update checking is not switched on in this launcher yet (it has no update address).");
                return;
            }
            if (!ClientPresent())
            {
                if (manual) Info("Game update", "The game is not installed yet. Press INSTALL first.");
                return;
            }
            UpdateChecking = true;
            RefreshUpdateBtn();
            if (manual) SetStatus("Checking for updates...");
            Bg(delegate
            {
                UpdateInfo info = null;
                string err = null;
                try { info = Updater.FetchManifest(); }
                catch (UserError e) { err = e.Message; }
                catch (Exception e) { err = "The update check failed (" + e.Message + ")."; }
                Ui(delegate { CheckDone(info, err, manual, afterInstall); });
            });
        }

        void CheckDone(UpdateInfo info, string err, bool manual, bool afterInstall)
        {
            UpdateChecking = false;
            if (err != null)
            {
                RefreshUpdateBtn();
                if (manual)
                {
                    SetStatus("Could not check for updates.");
                    Warn("Game update", err);
                }
                return;
            }
            updateInfo = info;
            if (afterInstall && info.InstallerVersion.Length > 0)    // the file just installed has a known version
            {
                Cfg.Set("client_version", info.InstallerVersion);
                Cfg.Save();
            }
            UpdateAvailable = Updater.Needed(InstalledVersion(), info.Version);
            RefreshUpdateBtn();
            if (!UpdateAvailable)
            {
                if (manual)
                {
                    SetStatus("You have the newest version (" + info.Version + ").");
                    Info("Game update", "You have the newest version (" + info.Version + ").");
                }
                return;
            }
            SetStatus("Update " + info.Version + " is available.");
            if (manual || Cfg.Str("update_skipped") != info.Version) OfferUpdate();
        }

        // opens the update window, unless the player is busy with something else (then it waits a moment)
        void OfferUpdate()
        {
            if (updateInfo == null || Updating || Installing || GameRunningFlag) return;
            if (setupOpen || SyncBusy)
            {
                After(2000, OfferUpdate);
                return;
            }
            if (!Visible || WindowState == FormWindowState.Minimized)   // hidden in the tray: offered when opened
            {
                updateOfferPending = true;
                Notify("A game update (" + updateInfo.Version + ") is available.");
                return;
            }
            ShowUpdateDialog();
        }

        void ShowUpdateDialog()
        {
            if (updateInfo == null) return;
            if (updateDlg != null && !updateDlg.IsDisposed)
            {
                updateDlg.Show();
                updateDlg.WindowState = FormWindowState.Normal;
                updateDlg.Activate();
                return;
            }
            updateDlg = new UpdateDialog(this, updateInfo);
            updateDlg.PlaceOver(this);
            updateDlg.ShowInside();
        }

        UpdateDialog OpenUpdateDialog()
        {
            return updateDlg != null && !updateDlg.IsDisposed ? updateDlg : null;
        }

        public void SkipUpdate(string version)
        {
            Cfg.Set("update_skipped", version);
            Cfg.Save();
            SetStatus("Version " + version + " skipped. The UPDATE button still offers it.");
        }

        public void MarkUpToDate(string version)
        {
            Cfg.Set("client_version", version);
            Cfg.Remove("update_skipped");
            Cfg.Save();
            UpdateAvailable = false;
            RefreshUpdateBtn();
            SetStatus("Noted: you have version " + version + ".");
        }

        // the update the player downloaded in the browser: the newest recent archive (after asking), or one they pick
        public string PickUpdateFile(IWin32Window owner)
        {
            string archive = Archive.FindDownloaded();
            if (archive != null)
            {
                var fi = new FileInfo(archive);
                if (Msg.Show(owner, "Install the update from this file?\n\n" + fi.Name + "  (" + (fi.Length >> 20) + " MB)\nin " +
                                    fi.DirectoryName, "Game update", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    archive = null;
            }
            if (archive == null)
                using (var d = new OpenFileDialog())
                {
                    d.Title = Lang.T("Choose the update file you downloaded");
                    d.InitialDirectory = Util.DownloadsDir;
                    d.Filter = Lang.T("Update archive (*.rar;*.zip;*.7z)|*.rar;*.zip;*.7z|All files (*.*)|*.*");
                    archive = d.ShowDialog(owner) == DialogResult.OK ? d.FileName : null;
                }
            return archive;
        }

        // UPDATE NOW. True when the work has started. archive = a file the player downloaded in the browser.
        public bool BeginUpdate(UpdateDialog dlg, UpdateInfo info, string archive)
        {
            if (Updating || Installing) return false;
            string game = GameDir();
            if (GameRunningFlag || Util.GameRunning())
            {
                Msg.Show(dlg, "Close the game first, then try again.", "Game running");
                return false;
            }
            try
            {
                string kept = Path.Combine(game, Updater.DownloadName);   // a verified download kept from a failed install
                string have = archive ?? (File.Exists(kept) ? kept : null);
                if (have != null) Updater.EnsureFreeSpace(game, new FileInfo(have).Length, 1.5);
                else Updater.EnsureFreeSpace(game, info.Size, 2.5);
            }
            catch (UserError e)
            {
                Msg.Show(dlg, e.Message, "Game update");
                return false;
            }
            catch (Exception) { }
            Updating = true;
            RefreshPlayLabel();
            RefreshUpdateBtn();
            SetStatus("Updating to " + info.Version + "...");
            dlg.CancelEvent = new ManualResetEvent(false);
            ManualResetEvent cancel = dlg.CancelEvent;
            Bg(delegate { UpdateWorker(info, game, archive, cancel); });
            return true;
        }

        void UpdateWorker(UpdateInfo info, string game, string archive, ManualResetEvent cancel)
        {
            bool mine = archive == null;
            string path = mine ? Path.Combine(game, Updater.DownloadName) : archive;
            Updater.Result result = null;
            string error = null;
            bool cancelled = false, keep = false;
            try
            {
                if (mine)
                {
                    if (File.Exists(path))                   // kept from an earlier try: is it this update?
                    {
                        Ui(delegate { UpdatePhase("install", "Checking the download..."); });
                        if (Net.FileSha256(path) != info.Sha256) File.Delete(path);
                    }
                    if (!File.Exists(path))
                    {
                        Ui(delegate { UpdatePhase("download", "Downloading the update..."); });
                        DateTime last = DateTime.MinValue;
                        string digest = Net.Download(info.Url, path, true, "update", delegate(long done, long total)
                        {
                            DateTime now = DateTime.Now;
                            if ((now - last).TotalSeconds >= 0.12 || (total > 0 && done >= total))
                            {
                                last = now;
                                Ui(delegate { var d = OpenUpdateDialog(); if (d != null) d.SetProgress(done, total); });
                            }
                        }, cancel, info.Size);
                        Ui(delegate { UpdatePhase("install", "Checking the download..."); });
                        if (digest != info.Sha256)
                            throw new UserError("The downloaded file does not match its checksum, so nothing was installed. " +
                                                "Please try again later.");
                    }
                    keep = true;                             // verified: if the install fails, no new download is needed
                }
                else if (info.Sha256.Length > 0 && Net.FileSha256(path) != info.Sha256)
                    throw new UserError("That file is not this update (its checksum does not match), so nothing was installed. " +
                                        "Download it again from the update page.");
                result = Updater.Apply(path, game, info.Delete, delegate(string text) { Ui(delegate { UpdatePhase("install", text); }); });
            }
            catch (CancelledError) { cancelled = true; }
            catch (UserError e) { error = e.Message; }
            catch (Exception e) { error = "The update failed (" + e.Message + ")."; }
            finally
            {
                if (result != null || (mine && !keep))       // installed, or a download that is no use
                    try { File.Delete(path); } catch (Exception) { }
            }
            Ui(delegate { UpdateDone(info, result, error, cancelled); });
        }

        void UpdatePhase(string phase, string text)
        {
            var d = OpenUpdateDialog();
            if (d != null) d.SetPhase(phase, text);
        }

        void UpdateDone(UpdateInfo info, Updater.Result result, string error, bool cancelled)
        {
            Updating = false;
            var dlg = OpenUpdateDialog();
            if (result != null)
            {
                Cfg.Set("client_version", info.Version);
                Cfg.Remove("update_skipped");
                Cfg.Save();
                UpdateAvailable = false;
                SetStatus("Updated to " + info.Version + " - press PLAY.");
                Notify("Game updated to " + info.Version + ". Press PLAY to enter Azeroth.");
                int n = result.Added + result.Replaced;
                if (dlg != null)
                    dlg.ShowResult(true, "Version " + info.Version + " is installed.\nPress PLAY to enter Azeroth.",
                                   Util.Plural(n, "file") + " updated" + (result.Removed > 0 ? ", " + result.Removed + " removed" : "") + ".");
            }
            else if (cancelled)
            {
                SetStatus("Update cancelled.");
                if (dlg != null) dlg.ShowOffer();
            }
            else
            {
                SetStatus("Update failed.");
                if (dlg != null) dlg.ShowResult(false, error, null);
                else
                {
                    ShowWindowFront();
                    Error("Game update", error);
                }
            }
            RefreshPlayLabel();
            RefreshUpdateBtn();
        }
    }
}
