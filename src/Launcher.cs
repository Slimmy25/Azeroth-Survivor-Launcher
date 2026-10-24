// The main launcher window: the tall narrow window with PLAY and the section buttons, the tray icon, playing
// (cloud download -> game -> cloud upload), installing the game, cloud sync runs and game updates.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp : Form
    {
        // TEMPORARY: while true, nothing but INSTALL works until the game has been downloaded by this launcher
        // (the flag is stored in the config when a download finishes). Set to false to switch it off.
        public const bool RequireLauncherInstall = true;

        // Where the game is downloaded from: the launcher opens this in the default web browser.
        public const string ClientUrl = "https://budapestmix.hu:5000/download";
        public const string DiscordUrl = "https://discord.com/invite/UzxaZuSAk";

        const int W = 330, H = 710;                          // the main launcher is a tall, narrow window

        public Config Cfg;
        public bool GameRunningFlag, Installing, SyncBusy, Updating, UpdateChecking, UpdateAvailable;
        UpdateInfo updateInfo;
        UpdateDialog updateDlg;
        InstallDialog installDlg;
        bool updateOfferPending, setupOpen, setupAsked, trayNotified, quitting;
        string updateRecovery;
        readonly bool startInTray;
        readonly List<KeyValuePair<string, Level>> cloudLog = new List<KeyValuePair<string, Level>>();
        RichTextBox logBox;
        string status = "Ready.", cloudNote = "";
        bool addonDlBusy;

        readonly WowButton playBtn, updateBtn, aboutBtn, discordBtn;
        readonly Dictionary<string, WowButton> nav = new Dictionary<string, WowButton>();
        readonly string[] navNames = { "Cloud Save", "Addons", "Options", "Changelog" };
        NotifyIcon tray;
        ToolStripMenuItem trayShow, trayPlay, trayUpload, trayCloseToTray, trayQuit;

        void TrayTexts()
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

        public LauncherApp(bool startInTray)
        {
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

            int bw = 250, bx = (W - 250) / 2;
            playBtn = Place(new WowButton("PLAY", Play, bw, 64, 22), bx, 326);
            foreach (string name in navNames)
            {
                string n = name;
                nav[n] = Place(new WowButton(n.ToUpperInvariant(), delegate { ShowSection(n); }, bw, 44, 13), bx, 402);
            }
            aboutBtn = Place(new WowButton("ABOUT LAUNCHER", delegate { ShowSection("About Launcher"); }, 115, 26, 8), 45, 636 - 13);
            nav["About Launcher"] = aboutBtn;
            discordBtn = Place(new WowButton("DISCORD", delegate { Util.OpenInShell(DiscordUrl); }, 115, 26, 8), 170, 636 - 13);
            discordBtn.Icon = Icons.Chat;
            // top left corner: checks for a game update (turns gold while one is waiting)
            updateBtn = Place(new WowButton("UPDATE", delegate { CheckLauncherUpdate(true); }, 78, 24, 8), 24, 24);
            LayoutNav();

            BuildSide();
            SetupTray();
            RefreshPlayLabel();

            FormClosing += OnClosingWindow;
            Resize += delegate { if (WindowState == FormWindowState.Minimized) After(1, HideToTray); };
            Shown += delegate
            {
                if (startInTray) Hide();
                After(300, StartupUpdateCheck);              // a newer launcher on GitHub is installed right away
                After(500, MaybeFirstRun);
                After(900, TellUpdateRecovery);
                After(2000, SyncPlayTime);                   // play time of all PCs from the sync folder
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
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Theme.BgTop, Theme.BgBottom, LinearGradientMode.Vertical))
                g.FillRectangle(lg, 0, 0, W, H);
            Theme.Smooth(g);
            using (var p = new Pen(Theme.GoldDim, 3)) g.DrawRectangle(p, 8, 8, W - 16, H - 16);       // outer double gold frame
            using (var p = new Pen(Theme.Bronze, 1)) g.DrawRectangle(p, 16, 16, W - 32, H - 32);
            using (var b = new SolidBrush(Theme.Gold))
            using (var p = new Pen(Theme.GoldDim, 1))
                foreach (var pt in new[] { new Point(8, 8), new Point(W - 8, 8), new Point(8, H - 8), new Point(W - 8, H - 8) })
                {
                    g.FillEllipse(b, pt.X - 6, pt.Y - 6, 12, 12);
                    g.DrawEllipse(p, pt.X - 6, pt.Y - 6, 12, 12);
                }
            Image logo = Theme.Logo;                          // emblem at the top, the game's name beneath it
            if (logo != null) g.DrawImage(logo, (W - logo.Width) / 2, 20, logo.Width, logo.Height);
            Theme.CenterText(g, "AZEROTH SURVIVORS", Theme.F(20, FontStyle.Bold), Theme.Gold, new RectangleF(0, 175, W, 32), Color.Black);
            Theme.CenterText(g, Lang.T("A game made by Aethus"), Theme.F(11, FontStyle.Italic), Theme.GoldDim, new RectangleF(0, 205, W, 22), null);
            string version = Data.GameVersion;                // the newest patch note in the changelog
            if (version.Length > 0)
                Theme.CenterText(g, Lang.T("Version " + version), Theme.F(9, FontStyle.Italic), Theme.GoldDim, new RectangleF(0, 225, W, 18), null);
            // short welcome text between the logo and the buttons
            string body = ClientPresent() ? "Press PLAY to enter Azeroth." :
                (RequireLauncherInstall ? "Game client not found.\nPress INSTALL to download it first."
                                        : "Game client not found.\nPress INSTALL to download it, or set the game folder in Options.");
            if (CloudEnabled()) body += "\nYour cloud save syncs automatically.";
            body = Lang.T(body);
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                Font f = Theme.F(10);
                var box = new RectangleF(35, 246, W - 70, 75);
                using (var b = new SolidBrush(Color.Black)) g.DrawString(body, f, b, new RectangleF(box.X + 1, box.Y + 1, box.Width, box.Height), sf);
                using (var b = new SolidBrush(Theme.Text)) g.DrawString(body, f, b, box, sf);
            }
            // play time, at the bottom of the welcome area
            PlayStats ps = ShownPlayStats();
            if (ps.Sessions > 0)
                Theme.CenterText(g, Lang.T("Time played: " + Duration(ps.Seconds) + "   |   Last session: " + Duration(ps.LastSeconds)),
                                 Theme.F(9, FontStyle.Italic), Theme.GoldDim, new RectangleF(20, 300, W - 40, 18), null);
            // footer text: cloud result, then the status
            Theme.CenterText(g, Lang.T(cloudNote), Theme.F(10, FontStyle.Italic), Theme.Gold, new RectangleF(25, 652, W - 50, 24), null);
            Theme.CenterText(g, Lang.T(status), Theme.F(12, FontStyle.Italic), Theme.Text, new RectangleF(25, 674, W - 50, 26), null);
        }

        public void SetStatus(string msg)
        {
            status = msg ?? "";
            Invalidate(new Rectangle(17, 672, W - 34, 30));
        }

        public void SetCloudNote(string text)
        {
            cloudNote = text ?? "";
            Invalidate(new Rectangle(17, 650, W - 34, 26));
        }

        // stack the section buttons; the Cloud Save button is only there while cloud saving is on
        void LayoutNav()
        {
            int y = 402;
            foreach (string name in navNames)
            {
                if (name == "Cloud Save" && !CloudEnabled())
                {
                    nav[name].Visible = false;
                    continue;
                }
                nav[name].Visible = true;
                nav[name].Location = new Point((W - 250) / 2, y);
                y += 52;
            }
            if (strip != null) LayoutStrip();
        }

        public void RefreshPlayLabel()
        {
            if (GameRunningFlag) { playBtn.Label = "PLAYING..."; playBtn.FontSize = 24; }
            else if (Installing) { playBtn.Label = "INSTALLING"; playBtn.FontSize = 18; }
            else if (Updating) { playBtn.Label = "UPDATING"; playBtn.FontSize = 20; }
            else { playBtn.Label = ClientPresent() ? "PLAY" : "INSTALL"; playBtn.FontSize = 24; }
            Invalidate(new Rectangle(17, 240, W - 34, 84));
        }

        // call after cloud saving is turned on or off: show/hide every cloud reference
        public void ApplyCloudVisibility()
        {
            SyncPlayTime();
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
            if (side.Visible)                                // a section window is open: close only the launcher
            {
                Hide();
                if (!trayNotified)
                {
                    trayNotified = true;
                    Notify("The launcher is in the tray; the open window stays. Right-click the icon to quit.");
                }
                return;
            }
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
            if (setupAsked || setupOpen || Cfg.Bool("setup_done", false) || !GateOpen()) return;
            if (!Visible) return;                            // started hidden: asked when the window is first opened
            if (startupLauncherCheck || Updating)           // a launcher update may restart us first
            {
                After(500, MaybeFirstRun);
                return;
            }
            setupAsked = true;
            RunSetup();
        }

        public void RunSetup()
        {
            // close the section window so only the setup dialog is in focus
            current = "Home";
            foreach (var b in nav.Values) b.Active = false;
            RefreshStrip();
            HideSide();
            var dlg = new SetupDialog(this);
            dlg.PlaceOver(this);
            setupOpen = true;
            try { dlg.ShowDialog(Visible ? this : null); }
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
                                            "AzerothSync inside it?", "Sync folder", MessageBoxButtons.YesNo) == DialogResult.Yes)
                d = Path.Combine(d, "AzerothSync");
            return d;
        }

        public void ChooseCloudDir()
        {
            string d = PickCloudDir(side.Visible ? (IWin32Window)side : this, null);
            if (d != null)
            {
                Cfg.Set("cloud_dir", d);
                Cfg.Save();
                SyncPlayTime();
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
                        dlg.ShowDialog(this);
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
            if (SyncBusy || GameRunningFlag || Installing)
            {
                SetStatus("Another task is still running - wait for it to finish.");
                return;
            }
            if (!CloudEnabled()) return;
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
                    ok = kind == "status" ? eng.Status() : kind == "pull" ? eng.Pull(force) : eng.Push(force);
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
        }

        // ------------------------------------------------------------------ play: download newest -> run game -> upload
        void Play()
        {
            if (GameRunningFlag || Installing) return;
            if (Updating)
            {
                SetStatus("The game is being updated. Please wait until that has finished.");
                return;
            }
            if (!ClientPresent())
            {
                InstallClient();
                return;
            }
            string exe = Path.Combine(GameDir(), Util.GameExe);
            GameRunningFlag = true;
            RefreshPlayLabel();
            SyncEngine eng = (CloudEnabled() && Cfg.Str("cloud_dir").Trim().Length > 0) ? MakeEngine() : null;
            bool cloudOnButNoFolder = CloudEnabled() && eng == null;
            bool minimize = Cfg.Bool("minimize_on_play", true);
            Bg(delegate { Session(exe, eng, cloudOnButNoFolder, minimize); });
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
                    Ui(delegate { SetCloudNote("Cloud sync successfully downloaded"); });
                }
                else if (cloudOnButNoFolder) problems.Add("No sync folder set - launched without cloud sync.");
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
                    DateTime started = DateTime.UtcNow, lastTick = started;
                    long counted = 0;
                    Action tick = delegate                   // play time so far, saved every minute
                    {
                        long elapsed = (long)(DateTime.UtcNow - started).TotalSeconds;
                        long delta = elapsed - counted;
                        counted = elapsed;
                        lastTick = DateTime.UtcNow;
                        Ui(delegate { AddPlayTime(delta, elapsed); });
                    };
                    using (Process p = Process.Start(psi))
                    {
                        launched = true;
                        Ui(StartPlaySession);
                        while (!p.WaitForExit(60000)) tick();        // returns on normal exit or crash
                    }
                    while (Util.GameRunning())               // the game may hand over to another process
                    {
                        Thread.Sleep(2000);
                        if ((DateTime.UtcNow - lastTick).TotalSeconds >= 60) tick();
                    }
                    tick();
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
                            Ui(delegate { SetStatus("Ready."); SetCloudNote("Cloud sync successfully uploaded"); });
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
        // Counted while a game started with PLAY runs (also if it hands over to another process) and saved every
        // minute, so a crash of the game or the launcher still counts. Stored in config.json.
        public long PlayLong(string key)
        {
            object v = Json.Get(Cfg.Data, key);
            return v is long ? (long)v : v is double ? (long)(double)v : 0;
        }

        public static string Duration(long seconds)
        {
            long h = seconds / 3600, m = seconds % 3600 / 60;
            return h > 0 ? h + "h " + m.ToString("00") + "m" : m + "m";
        }

        PlayStats cloudPlay;                                 // totals of all PCs from the sync folder, when known

        PlayStats LocalPlayStats()
        {
            var p = new PlayStats();
            p.Seconds = PlayLong("play_seconds");
            p.Sessions = PlayLong("play_sessions");
            p.Longest = PlayLong("play_longest_seconds");
            p.LastSeconds = PlayLong("play_last_seconds");
            p.LastDate = Cfg.Str("play_last_date");
            return p;
        }

        // what the Home screen and Options show: all PCs while cloud saving is on, else this PC
        public PlayStats ShownPlayStats()
        {
            return CloudEnabled() && cloudPlay != null ? cloudPlay : LocalPlayStats();
        }

        bool PlayCloudOn() { return CloudEnabled() && PlayTime.Reachable(Cfg.Str("cloud_dir").Trim()); }

        string PlayDevice()
        {
            string id = Cfg.Str("play_device");
            if (id.Length == 0)
            {
                id = SyncEngine.Machine + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                Cfg.Set("play_device", id);
                Cfg.Save();
            }
            return id;
        }

        // Shares this PC's play time through the sync folder and fetches the totals of all PCs (in the background).
        void SyncPlayTime()
        {
            if (!PlayCloudOn()) { cloudPlay = null; return; }
            string cloud = Cfg.Str("cloud_dir").Trim(), device = PlayDevice(), myEpoch = Cfg.Str("play_epoch");
            PlayStats mine = LocalPlayStats();
            Bg(delegate
            {
                try
                {
                    string epoch = PlayTime.Epoch(cloud);
                    bool adopt = epoch.Length > 0 && epoch != myEpoch;   // play time was reset on another PC
                    if (adopt) mine = new PlayStats();
                    PlayStats total = PlayTime.Publish(cloud, device, SyncEngine.Machine, mine, adopt ? epoch : myEpoch);
                    Ui(delegate
                    {
                        if (adopt)
                        {
                            foreach (string k in new[] { "play_seconds", "play_sessions", "play_last_seconds", "play_longest_seconds", "play_last_date" })
                                Cfg.Remove(k);
                            Cfg.Set("play_epoch", epoch);
                            Cfg.Save();
                        }
                        cloudPlay = total;
                        Invalidate(new Rectangle(17, 296, W - 34, 26));
                        if (current == "Options" && side.Visible && !GameRunningFlag) BuildPanel("Options");
                    });
                }
                catch (Exception) { }                        // the cloud drive is busy or offline: try again later
            });
        }

        void StartPlaySession()
        {
            Cfg.Set("play_sessions", PlayLong("play_sessions") + 1);
            Cfg.Set("play_last_seconds", 0L);
            Cfg.Set("play_last_date", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Cfg.Save();
            Invalidate(new Rectangle(17, 296, W - 34, 26));
        }

        void AddPlayTime(long delta, long sessionSeconds)
        {
            if (delta <= 0) return;
            Cfg.Set("play_seconds", PlayLong("play_seconds") + delta);
            Cfg.Set("play_last_seconds", sessionSeconds);
            if (sessionSeconds > PlayLong("play_longest_seconds")) Cfg.Set("play_longest_seconds", sessionSeconds);
            Cfg.Save();
            Invalidate(new Rectangle(17, 296, W - 34, 26));
            SetTrayTitle("Game running - " + Duration(sessionSeconds));
            SyncPlayTime();
        }

        // back to zero; with cloud saving on, on every PC (each one zeroes itself when it sees the new epoch)
        public void ResetPlayTime()
        {
            foreach (string k in new[] { "play_seconds", "play_sessions", "play_last_seconds", "play_longest_seconds", "play_last_date" })
                Cfg.Remove(k);
            cloudPlay = null;
            if (PlayCloudOn())
            {
                string epoch = Guid.NewGuid().ToString("N"), cloud = Cfg.Str("cloud_dir").Trim();
                Cfg.Set("play_epoch", epoch);
                Bg(delegate
                {
                    try { PlayTime.Reset(cloud, epoch); } catch (Exception) { }
                    Ui(SyncPlayTime);
                });
            }
            Cfg.Save();
            Invalidate();
            SetStatus("Play time reset.");
        }

        void SessionDone(List<string> problems)
        {
            GameRunningFlag = false;
            SyncPlayTime();
            RefreshPlayLabel();
            SetTrayTitle("Idle");
            ShowWindowFront();                               // the launcher stays up front after the session
            if (problems.Count > 0) Warn("Launcher", string.Join("\n\n", problems));
        }

        // ------------------------------------------------------------------ installing the game client
        void InstallClient()
        {
            if (Updating)
            {
                SetStatus("The game is being updated. Please wait until that has finished.");
                return;
            }
            if (installDlg != null && !installDlg.IsDisposed)
            {
                installDlg.Show();
                installDlg.WindowState = FormWindowState.Normal;
                installDlg.Activate();
                return;
            }
            installDlg = new InstallDialog(this);
            installDlg.PlaceOver(this);
            installDlg.Show();
        }

        public void InstallFromDownloads()
        {
            if (Updating || Installing) return;
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
                Notify("Install complete. Press PLAY to enter Azeroth.");
                After(300, MaybeFirstRun);                   // first-time setup comes after the install
                if (Updater.Enabled) After(800, delegate { CheckForUpdate(false, true); });   // is it already the newest?
            }
        }

        // ------------------------------------------------------------------ game updates (checked at start and with UPDATE)
        public string InstalledVersion() { return Cfg.Str("client_version").Trim(); }

        void RefreshUpdateBtn()
        {
            updateBtn.Label = UpdateChecking ? "CHECKING" : Updating ? "UPDATING" : "UPDATE";
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
                try { SelfUpdate.InstallAndRestart(path, !Visible); }   // hidden in the tray: the new one starts there too
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
            updateDlg.Show();
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
