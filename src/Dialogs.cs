// The pop-up windows: first-time setup, game install, cloud conflict and game update.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AzerothLauncher
{
    // First-time setup: (A) cloud saving yes/no, (B) where AddOns, game saves and addon settings are.
    // Page 2 asks whether to go to the Addons section. Result = settings to store, or null when closed early.
    public class SetupDialog : WowDialog
    {
        readonly LauncherApp app;
        public Dictionary<string, object> Result;
        public string Goto;                                  // "download" / "installed" / null
        bool cloudOn;
        readonly Dictionary<string, string> vals = new Dictionary<string, string>();
        readonly Dictionary<string, string> defaults = new Dictionary<string, string>();

        public SetupDialog(LauncherApp app) : base("First-time setup")
        {
            this.app = app;
            TopMost = true;
            SyncSettings ss = app.SyncSettings();
            cloudOn = app.Cfg.Bool("setup_done", false) ? app.CloudEnabled() : app.Cfg.Bool("cloud_enabled", true);
            vals["cloud_dir"] = app.Cfg.Str("cloud_dir");
            vals["addons_dir"] = ss.AddonsDir;
            vals["save_dir"] = ss.SaveDir;
            vals["saved_dir"] = ss.SavedDir;
            defaults["addons_dir"] = Path.Combine(ss.ConfigDir, "AddOns");
            defaults["saved_dir"] = Path.Combine(ss.ConfigDir, "saved");
            defaults["save_dir"] = Util.DefaultSaveDir;
            Page1();
        }

        void Page1()
        {
            BackAction = Close;                              // page 1: the back arrow leaves setup (same as LATER)
            Begin();
            Heading("Welcome, Adventurer", "Step 1 of 2", 26);
            Para("A.  Cloud saving", 13, FontStyle.Bold, Theme.Gold, 26, 0, 0, 0);
            var cb = UI.Check("Keep my progress in the cloud (Google Drive, OneDrive, Dropbox ...)\nand share it between my PCs",
                              cloudOn, Theme.Text, Theme.Panel, 11);
            cb.CheckedChanged += delegate { cloudOn = cb.Checked; BeginInvoke(new Action(Page1)); };
            Body.Add(cb, Align.Left, 22, 2, 26, 0);
            if (cloudOn) FolderRow("Sync folder (inside your cloud drive)", "cloud_dir", PickCloud, false);
            else Para("Cloud saving stays off. You can turn it on later in Cloud Save.", 10, FontStyle.Italic, Theme.GoldDim, 26, 2, 2, 0);
            Body.Add(UI.Line(Theme.Bronze, 10, 1), Align.Fill, 26, 10, 26, 6);
            Para("B.  Where are your files?", 13, FontStyle.Bold, Theme.Gold, 26, 0, 0, 0);
            Para("Folders found at their usual place are used as they are. Choose the ones that are missing.",
                 10, FontStyle.Regular, Theme.Text, 26, 0, 2, 0);
            FolderRow("AddOns", "addons_dir", delegate { Pick("addons_dir", "Select your AddOns folder"); }, true);
            FolderRow("Game saves", "save_dir", delegate
            {
                Pick("save_dir", "Select the folder that holds your save game (normally %APPDATA%\\wenilla-rogue)");
            }, true);
            FolderRow("Addon settings", "saved_dir", delegate
            {
                Pick("saved_dir", "Select your addon settings folder (the 'saved' folder in benilla-config)");
            }, true);
            Body.Add(new HRow(12, B("FINISH", FinishPage1, 170, 38, 12), B("LATER", Close, 120, 38, 11)),
                     Align.Center, 26, 14, 26, 16);
            Finish();
        }

        void FolderRow(string label, string key, Action chooser, bool defaultOk)
        {
            string path = vals.ContainsKey(key) ? vals[key] ?? "" : "";
            bool found = path.Length > 0 && Directory.Exists(path);
            var row = new Panel();
            row.BackColor = Theme.Panel;
            var name = UI.Text(label, 11, FontStyle.Bold, Theme.Gold, Theme.Panel, 0);
            string note = found ? (defaultOk ? "✓ found" : "✓ chosen") : (path.Length == 0 ? "not chosen yet" : "not found - please choose it");
            var noteL = UI.Text(note, 9, FontStyle.Italic, found ? Theme.Good : Theme.Bad, Theme.Panel, 0);
            var pathL = UI.Text(path.Length > 0 ? path : "(not set)", 9, FontStyle.Regular, Theme.LinkBlue, Theme.Panel, 470);
            var btn = B(found ? "CHANGE" : "CHOOSE", chooser, 100, 28, 9);
            Size ns = VStack.Measure(name), ps = VStack.Measure(pathL), os = VStack.Measure(noteL);
            int w = 600;
            name.Location = new Point(0, 0);
            noteL.Location = new Point(ns.Width + 10, ns.Height - os.Height - 1);
            pathL.Location = new Point(0, ns.Height);
            int h = Math.Max(ns.Height + ps.Height, 30);
            btn.Location = new Point(w - btn.Width, (h - btn.Height) / 2);
            row.Controls.Add(name);
            row.Controls.Add(noteL);
            row.Controls.Add(pathL);
            row.Controls.Add(btn);
            row.Size = new Size(w, h);
            Body.Add(row, Align.Left, 26, 4, 26, 0);
        }

        void Pick(string key, string title)
        {
            string cur = vals.ContainsKey(key) ? vals[key] ?? "" : "";
            string d = LauncherApp.AskFolder(this, title, Directory.Exists(cur) ? cur :
                (cur.Length > 0 && Directory.Exists(Path.GetDirectoryName(cur)) ? Path.GetDirectoryName(cur) : Util.Home));
            if (d != null)
            {
                vals[key] = Path.GetFullPath(d);
                Page1();
            }
        }

        void PickCloud()
        {
            string d = app.PickCloudDir(this, vals["cloud_dir"]);
            if (d != null)
            {
                vals["cloud_dir"] = d;
                Page1();
            }
        }

        void FinishPage1()
        {
            if (cloudOn && (vals["cloud_dir"] ?? "").Trim().Length == 0)
            {
                MessageBox.Show(this, "Choose a sync folder for cloud saving, or untick\n\"Keep my progress in the cloud\".", "Setup");
                return;
            }
            var missing = new List<string>();
            foreach (var kv in new[] { new[] { "AddOns", "addons_dir" }, new[] { "Game saves", "save_dir" }, new[] { "Addon settings", "saved_dir" } })
                if (!Directory.Exists(vals[kv[1]] ?? "")) missing.Add(kv[0]);
            if (missing.Count > 0 && MessageBox.Show(this, "These folders were not found:\n\n  " + string.Join("\n  ", missing) +
                    "\n\nFinish anyway? You can change them later in Options.", "Setup", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            var res = new Dictionary<string, object>();
            res["cloud_enabled"] = cloudOn;
            if (cloudOn) res["cloud_dir"] = vals["cloud_dir"];
            foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                if (!Util.SamePath(vals[key], defaults[key])) res[key] = Path.GetFullPath(vals[key]);   // only stored when not the default
            Result = res;                                     // saved when this window closes
            Page2();
        }

        void Page2()
        {
            BackAction = Page1;                               // page 2: back to the folders page
            Begin();
            Heading("Addons", "Step 2 of 2", 26);
            Para("Your settings are saved.\n\nWould you like to go to the Addons section now\n" +
                 "to download some addons, or to enable or disable the ones you have?", 12, FontStyle.Regular, Theme.Text, 26, 0, 14, 0);
            Body.Add(B("DOWNLOAD ADDONS", delegate { Leave("download"); }, 320, 42, 12), Align.Center, 26, 4, 26, 4);
            Body.Add(B("ENABLE / DISABLE ADDONS", delegate { Leave("installed"); }, 320, 42, 12), Align.Center, 26, 4, 26, 4);
            Body.Add(B("NO THANKS", delegate { Leave(null); }, 200, 34, 10), Align.Center, 26, 10, 26, 18);
            Finish();
            BackAction = Page1;
        }

        void Leave(string go)
        {
            Goto = go;
            Close();
        }
    }

    // Two steps: (1) open the download in the web browser, (2) tell the launcher it is in Downloads.
    // Not modal and not always-on-top, so the browser can be used in between.
    public class InstallDialog : WowDialog
    {
        public InstallDialog(LauncherApp app) : base("Install the game")
        {
            Begin();
            Body.Add(UI.Text("Install Azeroth Survivors", 18, FontStyle.Bold, Theme.Gold, Theme.Panel, 0), Align.Center, 30, 4, 30, 4);
            Body.Add(UI.Line(Theme.GoldDim, 10, 2), Align.Fill, 30, 0, 30, 10);
            Para("Step 1", 12, FontStyle.Bold, Theme.Gold, 30, 0, 0, 0);
            Para("Download the game in your web browser.", 11, FontStyle.Regular, Theme.Text, 30, 0, 0, 0);
            Body.Add(B("OPEN DOWNLOAD", delegate
            {
                Util.OpenInShell(LauncherApp.ClientUrl);
                app.SetStatus("Download opened in your browser.");
            }, 260, 40, 12), Align.Center, 30, 6, 30, 14);
            Para("Step 2", 12, FontStyle.Bold, Theme.Gold, 30, 0, 0, 0);
            Para("When the download has finished, come back here.", 11, FontStyle.Regular, Theme.Text, 30, 0, 0, 300);
            Body.Add(B("IT'S IN MY DOWNLOADS", delegate { Close(); app.InstallFromDownloads(); }, 260, 40, 12), Align.Center, 30, 6, 30, 6);
            Para("The launcher finds the file (in its own folder or Downloads), unpacks the game here and deletes the download.",
                 9, FontStyle.Italic, Theme.GoldDim, 30, 0, 0, 300);
            Body.Add(B("CANCEL", Close, 120, 30, 10), Align.Center, 30, 10, 30, 16);
            Finish();
        }
    }

    // One question for all areas in conflict: Use cloud / Keep local / Cancel.
    public class ConflictDialog : WowDialog
    {
        public Choice Result = Choice.Abort;

        public ConflictDialog(List<SyncSet> sets, bool downloading) : base("Which progress do you want to keep?")
        {
            TopMost = true;
            BackAction = delegate { Done(Choice.Abort); };
            Begin();
            Body.Add(UI.Text("Both sides changed", 18, FontStyle.Bold, Theme.Gold, Theme.Panel, 0), Align.Center, 26, 4, 26, 4);
            Body.Add(UI.Line(Theme.GoldDim, 10, 2), Align.Fill, 26, 0, 26, 10);
            Para("This PC and the cloud both changed since the last sync:", 11, FontStyle.Regular, Theme.Text, 26, 0, 0, 0);
            foreach (var st in sets)
                Body.Add(UI.Text("• " + st.Label + ":  this PC " + SyncEngine.NewestTime(st.Dir, st.Exclude) +
                                 "   |   cloud " + SyncEngine.NewestTime(st.Cloud, st.Exclude), 10, FontStyle.Regular,
                                 Theme.LinkBlue, Theme.Panel, 0), Align.Left, 40, 1, 26, 1);
            Para("One complete state wins, the other is backed up first.\nFirst time on this PC? Choose USE CLOUD.\n" +
                 (downloading ? "USE CLOUD replaces your local data. KEEP LOCAL keeps it and uploads it later."
                              : "KEEP LOCAL replaces the cloud. USE CLOUD leaves the cloud and skips your upload."),
                 10, FontStyle.Italic, Theme.Text, 26, 10, 12, 0);
            Body.Add(new HRow(12, B("USE CLOUD", delegate { Done(Choice.Cloud); }, 150, 38, 11),
                                  B("KEEP LOCAL", delegate { Done(Choice.Local); }, 150, 38, 11),
                                  B("CANCEL", delegate { Done(Choice.Abort); }, 120, 38, 11)), Align.Center, 26, 0, 26, 16);
            Finish();
        }

        void Done(Choice c)
        {
            Result = c;
            Close();
        }
    }

    // Offers a game update (never forced) and then shows how it is going.
    // The back arrow is NOT NOW; while the file downloads it cancels, while it installs it asks to wait.
    public class UpdateDialog : WowDialog
    {
        const int BarW = 380;
        readonly LauncherApp app;
        public readonly UpdateInfo Info;
        public string Phase = "offer";                       // offer / page / download / install / done
        public ManualResetEvent CancelEvent = new ManualResetEvent(false);
        Label statusLbl, detail;
        ProgressBarGold bar;
        WowButton cancelBtn;

        public UpdateDialog(LauncherApp app, UpdateInfo info) : base("Game update")
        {
            this.app = app;
            Info = info;
            BackAction = Back;
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing && (Phase == "install" || Phase == "download"))
                {
                    e.Cancel = true;
                    Back();
                }
            };
            ShowOffer();
        }

        void Reset(string title)
        {
            if (bar != null) bar.Stop();
            Begin();
            Body.Add(UI.Text(title, 18, FontStyle.Bold, Theme.Gold, Theme.Panel, 0), Align.Center, 30, 4, 30, 4);
            Body.Add(UI.Line(Theme.GoldDim, BarW, 2), Align.Fill, 30, 0, 30, 10);
        }

        Label Line(string text, Color fg, float size, bool italic, int top, int bottom)
        {
            return Body.Add(UI.Text(text, size, italic ? FontStyle.Italic : FontStyle.Regular, fg, Theme.Panel, BarW),
                            Align.Left, 30, top, 30, bottom);
        }

        public void ShowOffer()
        {
            Phase = "offer";
            string mine = app.InstalledVersion();
            Reset("Update available");
            var rows = new List<string[]>();
            rows.Add(new[] { "New version", Info.Version });
            rows.Add(new[] { "Your version", mine.Length > 0 ? mine : "not recorded yet" });
            if (Info.Size > 0) rows.Add(new[] { "Download size", Util.FmtSize(Info.Size) });
            foreach (var r in rows)
                Body.Add(new HRow(12, UI.Text(r[0] + ":", 11, FontStyle.Bold, Theme.Gold, Theme.Panel, 0),
                                      UI.Text(r[1], 11, FontStyle.Regular, Theme.Text, Theme.Panel, 0)), Align.Left, 30, 0, 30, 0);
            if (Info.Notes.Length > 0)
            {
                Body.Add(UI.Text("What is new", 11, FontStyle.Bold, Theme.Gold, Theme.Panel, 0), Align.Left, 30, 10, 30, 2);
                var box = UI.Log(Theme.Ink, 10);
                box.Text = Info.Notes.Replace("\n", "\r\n");
                int lines = Info.Notes.Split('\n').Length + Info.Notes.Length / 48 + 1;
                box.Size = new Size(BarW, 18 * Math.Min(9, Math.Max(3, lines)));
                Body.Add(box, Align.Left, 30, 0, 30, 0);
            }
            Line("Your addons, settings and saves are left alone. If anything goes wrong, the old game files are put back.",
                 Theme.GoldDim, 9, true, 10, 0);
            WowButton first = Info.Url.Length > 0 ? B("UPDATE NOW", StartUpdate, 170, 40, 12) : B("OPEN DOWNLOAD", OpenPage, 190, 40, 12);
            Body.Add(new HRow(12, first, B("NOT NOW", Close, 120, 40, 12)), Align.Center, 30, 14, 30, 6);
            if (mine.Length > 0)
                Body.Add(B("SKIP THIS VERSION", delegate { app.SkipUpdate(Info.Version); Close(); }, 190, 26, 9), Align.Center, 30, 0, 30, 14);
            else
                Body.Add(B("I ALREADY HAVE THIS VERSION", delegate { app.MarkUpToDate(Info.Version); Close(); }, 250, 26, 9),
                         Align.Center, 30, 0, 30, 14);
            Finish();
        }

        void Back()
        {
            if (Phase == "install")
                MessageBox.Show(this, "The update is being installed. Please wait until it has finished.", "Updating");
            else if (Phase == "download") Cancel();
            else Close();
        }

        void StartUpdate()
        {
            ShowProgress("download", "Downloading the update...");    // before the work can report back
            if (!app.BeginUpdate(this, Info, null)) ShowOffer();
        }

        void OpenPage()
        {
            Util.OpenInShell(Info.Page);
            ShowPageWait();
        }

        void ShowPageWait()
        {
            Phase = "page";
            Reset("Download the update");
            Line("Step 1: download the update in your web browser.", Theme.Gold, 12, false, 0, 2);
            Body.Add(B("OPEN DOWNLOAD AGAIN", OpenPage, 260, 34, 11), Align.Center, 30, 2, 30, 12);
            Line("Step 2: when the download has finished, come back here.", Theme.Gold, 12, false, 0, 2);
            Body.Add(B("IT'S IN MY DOWNLOADS", HaveDownload, 260, 40, 12), Align.Center, 30, 2, 30, 6);
            Line("The launcher finds the file (in its own folder or Downloads), installs it and deletes it.", Theme.GoldDim, 9, true, 4, 0);
            Body.Add(B("CANCEL", Close, 120, 30, 10), Align.Center, 30, 10, 30, 16);
            Finish();
        }

        void HaveDownload()
        {
            string archive = app.PickUpdateFile(this);
            if (archive == null) return;
            ShowProgress("install", "Checking the file...");
            if (!app.BeginUpdate(this, Info, archive)) ShowPageWait();
        }

        void Cancel()
        {
            CancelEvent.Set();
            if (cancelBtn != null) cancelBtn.Label = "CANCELLING...";
        }

        public void ShowProgress(string phase, string text)
        {
            Phase = phase;
            Reset("Updating to " + Info.Version);
            statusLbl = Line(text, Theme.Text, 11, false, 0, 8);
            bar = Body.Add(new ProgressBarGold(BarW, 18), Align.Left, 30, 0, 30, 0);
            detail = Line(" ", Theme.GoldDim, 10, false, 6, 0);
            cancelBtn = null;
            if (phase == "download")
            {
                cancelBtn = Body.Add(B("CANCEL", Cancel, 130, 34, 11), Align.Center, 30, 12, 30, 16);
            }
            else
            {
                Line("Please keep the launcher open until this has finished.", Theme.GoldDim, 9, true, 2, 16);
                bar.Pulse();
            }
            Finish();
        }

        // the work moved on (download -> install) or has a new line to show
        public void SetPhase(string phase, string text)
        {
            if (Phase == "done" || Phase == "offer" || Phase == "page") return;
            if (phase != Phase) ShowProgress(phase, text);
            else if (statusLbl != null) statusLbl.Text = text;
        }

        public void SetProgress(long done, long total)
        {
            if (Phase != "download" || bar == null) return;
            if (total > 0)
            {
                double frac = Math.Min(1.0, (double)done / total);
                bar.Set(frac);
                detail.Text = Util.FmtSize(done) + " of " + Util.FmtSize(total) + "  (" + (int)(frac * 100) + "%)";
            }
            else
            {
                detail.Text = Util.FmtSize(done) + " downloaded";
                bar.Pulse();
            }
        }

        public void ShowResult(bool ok, string text, string small)
        {
            Phase = "done";
            Reset(ok ? "Update installed" : "Update failed");
            Line(text, ok ? Theme.Good : Theme.Bad, 11, false, 0, 6);
            if (!string.IsNullOrEmpty(small)) Line(small, Theme.GoldDim, 9, true, 0, 6);
            if (ok) Body.Add(B("CLOSE", Close, 130, 40, 12), Align.Center, 30, 10, 30, 16);
            else Body.Add(new HRow(12, B("TRY AGAIN", ShowOffer, 150, 40, 12), B("CLOSE", Close, 130, 40, 12)), Align.Center, 30, 10, 30, 16);
            Finish();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (bar != null) bar.Stop();
            base.OnFormClosed(e);
        }
    }

    // gold progress bar; Pulse() = a block sweeping back and forth while the length of the work is not known
    public class ProgressBarGold : Control
    {
        double frac;
        int pulseX;
        System.Windows.Forms.Timer timer;

        public ProgressBarGold(int w, int h)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(w, h);
        }

        public void Set(double f)
        {
            Stop();
            frac = f;
            Invalidate();
        }

        public void Pulse()
        {
            if (timer != null) return;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 45;
            timer.Tick += delegate { pulseX = (pulseX + 9) % (Width + 100); Invalidate(); };
            timer.Start();
        }

        public void Stop()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Stop();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Ink);
            using (var b = new SolidBrush(Theme.Gold))
            {
                if (timer != null)
                {
                    int x0 = Math.Max(0, pulseX - 100), x1 = Math.Min(Width, pulseX);
                    if (x1 > x0) g.FillRectangle(b, x0, 0, x1 - x0, Height);
                }
                else g.FillRectangle(b, 0, 0, (int)(Width * frac), Height);
            }
            using (var p = new Pen(Theme.GoldDim, 1)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }
}
