// First-time setup, inside the launcher's section window (no window of its own): the language first, then whether the
// player comes from the website (their save is imported), cloud saving and the folders, AZS Stats, the addons and, when
// the game is not here yet, its download. Setup cannot be closed before it is done: cancelling it shows a last page that
// says what the launcher is for, with START SETUP AGAIN. PLAY stays grayed out until setup is done.
// While it runs no Community video is shown; when it is done the home screen's Community comes back.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        // 0 language, 1 website save, 2 cloud and folders, 3 AZS Stats, 4 addons, 5 the game's download, 6 setup cancelled
        int setupPage;
        bool setupCloud;
        bool? setupStats;                                     // the answer to the AZS Stats question
        string setupAddons;                                   // "download" when the player wants addons, else null
        bool setupDownloading;                                // the game's download was started from setup
        bool setupFirstTime;                                  // setup was never done before (not RUN SETUP AGAIN)
        readonly List<string> setupCreated = new List<string>();   // the folders setup created, deepest last
        string setupImported;
        Dictionary<string, string> setupVals, setupDefaults;
        Dictionary<string, object> setupResult;

        // RUN SETUP AGAIN and the first start ask the same, from the start, as if nothing had been set up: every folder
        // at its usual place, cloud saving on with no folder chosen yet. What was set before stays until FINISH.
        void ShowSetupDialog()
        {
            string game = GameDir();
            string config = Path.Combine(game, "benilla-config");
            setupCloud = true;
            setupVals = new Dictionary<string, string>();
            setupDefaults = new Dictionary<string, string>();
            setupDefaults["addons_dir"] = Path.Combine(config, "AddOns");
            setupDefaults["saved_dir"] = Path.Combine(config, "saved");
            setupDefaults["save_dir"] = Util.DefaultSaveDir;
            setupVals["cloud_dir"] = "";
            foreach (var kv in setupDefaults) setupVals[kv.Key] = kv.Value;
            Config.Frozen = false;                            // (after a cancelled first setup: saving again)
            setupFirstTime = !Cfg.Bool("setup_done", false);
            setupImported = null;
            setupResult = null;
            setupStats = null;
            setupAddons = null;
            setupPage = 0;
            setupOpen = true;
            foreach (var b in nav.Values) b.Active = false;
            current = "Setup";
            RefreshStrip();
            ShowSide();
            BuildPanel("Setup");
        }

        void SetupGo(int page) { setupPage = page; BuildPanel("Setup"); }

        // the back arrow: the page before. Setup is never left this way (the first page and the last stay)
        void SetupBack()
        {
            if (setupPage > 0 && setupPage <= 5) SetupGo(setupPage - 1);
        }

        static readonly string[] SetupSteps = { "Language", "Your save", "Cloud & folders", "AZS Stats", "Addons", "Download", "Setup" };

        void PanelSetup()
        {
            ClearPanel("Setup");
            int step = Math.Min(setupPage, 3) + 1;               // the AZS Stats, addons and download pages are step 4
            SetCrumbs("Setup", null, SetupSteps[setupPage], null);
            // the page sits snug at the top, as every other section does; "Step X of 4" in the top right corner
            var sp = MakeScroll(panel, new Rectangle(8, 4, PW - 16, PH - 12), Theme.Panel, Theme.Panel);
            setupHeadW = 0;
            if (setupPage < 6)
            {
                var stepL = PutText(Lang.F("Step {0} of {1}", step, 4), 10, FontStyle.Italic, Theme.GoldDim, 0, 8, 0);
                stepL.Left = PW - 24 - stepL.Width;
                stepL.BringToFront();
                setupHeadW = stepL.Width + 16;                    // the first heading stays clear of it
            }
            putInto = sp;
            try
            {
                int px = 16, pw = sp.ClientSize.Width - 32;
                int end;
                if (setupPage == 0) end = SetupLanguage(px, 6, pw);
                else if (setupPage == 1) end = SetupSave(px, 6, pw);
                else if (setupPage == 2) end = SetupFolders(px, 6, pw);
                else if (setupPage == 3) end = SetupStats(px, 6, pw);
                else if (setupPage == 4) end = SetupAddons(px, 6, pw);
                else if (setupPage == 5) end = SetupDownload(px, 6, pw);
                else end = SetupCancelled(px, 6, pw);
                var pad = new Panel();
                pad.BackColor = Theme.Panel;
                pad.Bounds = new Rectangle(0, end, 1, 12);
                sp.Controls.Add(pad);
            }
            finally { putInto = null; }
        }

        int setupHeadW;                                       // room kept free for "Step X of 4" beside a page's heading

        // page 1: the language, every one the launcher speaks; the choice is shown at once
        int SetupLanguage(int x, int y, int w)
        {
            var q = PutText("Which language do you want the launcher in?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            y = BottomOf(q) + 10;
            int bw = 150, gap = 8, cw = (int)(bw * Theme.Zoom), cols = Math.Max(1, (w + gap) / (cw + gap)), col = 0;
            foreach (var kv in Lang.Available())
            {
                string code = kv.Key;
                var b = new WowButton(kv.Value, delegate { SetLanguage(code); }, bw, 28, 9);
                b.Selected = code == Lang.Code;
                Put(b, x + col * (cw + gap), y);
                if (++col >= cols) { col = 0; y += (int)(28 * Theme.Zoom) + 6; }
            }
            if (col > 0) y += (int)(28 * Theme.Zoom) + 6;
            var nx = Put(new WowButton("NEXT ›", delegate { SetupGo(1); }, 170, 38, 12), x + w - (int)(170 * Theme.Zoom), y + 10);
            return nx.Bottom;
        }

        // page 2: moving over from the website? The save is imported before anything else
        int SetupSave(int x, int y, int w)
        {
            var q = PutText("Moving over from the website?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText("Played Azeroth Survivors in your browser? Bring your progress with you: your heroes, the zones you opened, " +
                            "your gear, talents and codex.\n\nOn the website, open Options > Saves and press Export Save. Then choose " +
                            "that .sav file here. The game opens with your progress, ready to play.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            if (setupImported != null)
            {
                var ok = PutText("✓ Imported: " + setupImported, 11, FontStyle.Bold, Theme.Good, x, y, w, false);
                y = BottomOf(ok) + 12;
                var n = Put(new WowButton("NEXT ›", delegate { SetupGo(2); }, 170, 38, 12), x, y);
                Put(new WowButton("IMPORT ANOTHER", SetupImport, 190, 38, 10), n.Right + 12, y);
                return n.Bottom;
            }
            var im = Put(new WowButton("IMPORT MY SAVE", SetupImport, 220, 42, 12), x, y);
            Put(new WowButton("I'M NEW HERE", delegate { SetupGo(2); }, 190, 42, 11), im.Right + 12, y);
            return im.Bottom;
        }

        void SetupImport()
        {
            string saveDir = (setupVals["save_dir"] ?? "").Length > 0 ? setupVals["save_dir"] : Util.DefaultSaveDir;
            string name = ImportSave(side, saveDir);
            if (name == null) return;
            setupImported = name;
            BuildPanel("Setup");
        }

        // page 3: cloud saving, and where the files are
        int SetupFolders(int x, int y, int w)
        {
            var a = PutText("A.  Cloud saving", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            y = BottomOf(a) + 4;
            var cb = UI.Check("Keep my progress in the cloud (Google Drive, OneDrive, Dropbox ...)\nand share it between my PCs",
                              setupCloud, Theme.Text, Theme.Panel, 11);
            cb.Size = VStack.Measure(cb);
            cb.CheckedChanged += delegate { setupCloud = cb.Checked; BeginInvoke(new Action(delegate { BuildPanel("Setup"); })); };
            Put(cb, x - 4, y);
            y = BottomOf(cb) + 4;
            if (setupCloud) y = SetupFolderRow("Sync folder (inside your cloud drive)", "cloud_dir", delegate
            {
                string d = PickCloudDir(side, setupVals["cloud_dir"]);
                if (d != null) { setupVals["cloud_dir"] = d; BuildPanel("Setup"); }
            }, false, x, y, w);
            else
            {
                var off = PutText("Cloud saving stays off. You can turn it on later in Options > CLOUD SAVE.", 10, FontStyle.Italic, Theme.GoldDim, x, y, w);
                y = BottomOf(off) + 2;
            }
            Put(UI.Line(Theme.Bronze, w, 1), x, y + 6);
            var b = PutText("B.  Where are your files?", 13, FontStyle.Bold, Theme.Gold, x, y + 14, w);
            var n = PutText("Folders found at their usual place are used as they are. A folder that is not there yet is created when setup is done; you can choose another one instead.",
                            10, FontStyle.Regular, Theme.Text, x, BottomOf(b), w);
            y = BottomOf(n) + 4;
            y = SetupFolderRow("AddOns", "addons_dir", delegate { SetupPick("addons_dir", "Select your AddOns folder"); }, true, x, y, w);
            y = SetupFolderRow("Game saves", "save_dir", delegate
            {
                SetupPick("save_dir", "Select the folder that holds your save game (normally %APPDATA%\\wenilla-rogue)");
            }, true, x, y, w);
            y = SetupFolderRow("Addon settings", "saved_dir", delegate
            {
                SetupPick("saved_dir", "Select your addon settings folder (the 'saved' folder in benilla-config)");
            }, true, x, y, w);
            var fin = Put(new WowButton("NEXT ›", SetupFinishFolders, 170, 38, 12), x, y + 12);
            return fin.Bottom;
        }

        int SetupFolderRow(string label, string key, Action chooser, bool defaultOk, int x, int y, int w)
        {
            string path = setupVals.ContainsKey(key) ? setupVals[key] ?? "" : "";
            bool found = path.Length > 0 && Directory.Exists(path);
            var name = PutText(label, 11, FontStyle.Bold, Theme.Gold, x, y, 0);
            // a folder of the game's that is not there yet is simply created when setup is done
            string note = found ? (defaultOk ? "✓ found" : "✓ chosen") : (path.Length == 0 ? "not chosen yet" : defaultOk ? "Created if not found" : "not found - please choose it");
            PutText(note, 9, FontStyle.Italic, found ? Theme.Good : defaultOk && path.Length > 0 ? Theme.GoldDim : Theme.Bad, name.Right + 8, y + 3, 0);
            int btnW = (int)(100 * Theme.Zoom);
            var pathL = PutText(path.Length > 0 ? path : Lang.T("(not set)"), 9, FontStyle.Regular, Theme.LinkBlue, x, BottomOf(name), w - btnW - 12, false);
            int h = BottomOf(pathL) - y;
            Put(new WowButton(found ? "CHANGE" : "CHOOSE", chooser, 100, 28, 9), x + w - btnW, y + Math.Max(0, (h - (int)(28 * Theme.Zoom)) / 2));
            return Math.Max(BottomOf(pathL), y + (int)(28 * Theme.Zoom)) + 6;
        }

        void SetupPick(string key, string title)
        {
            string cur = setupVals.ContainsKey(key) ? setupVals[key] ?? "" : "";
            string d = AskFolder(side, title, Directory.Exists(cur) ? cur :
                (cur.Length > 0 && Directory.Exists(Path.GetDirectoryName(cur)) ? Path.GetDirectoryName(cur) : Util.Home));
            if (d == null) return;
            setupVals[key] = Path.GetFullPath(d);
            BuildPanel("Setup");
        }

        void SetupFinishFolders()
        {
            if (setupCloud && (setupVals["cloud_dir"] ?? "").Trim().Length == 0)
            {
                Msg.Show(side, "Choose a sync folder for cloud saving, or untick\n\"Keep my progress in the cloud\".", "Setup");
                return;
            }
            var res = new Dictionary<string, object>();
            res["cloud_enabled"] = setupCloud;
            if (setupCloud) res["cloud_dir"] = setupVals["cloud_dir"];
            foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                if (!Util.SamePath(setupVals[key], setupDefaults[key])) res[key] = Path.GetFullPath(setupVals[key]);   // only stored when not the default
            setupResult = res;
            SetupGo(3);
        }

        // page 4: AZS Stats, asked before the addons (it is one of them)
        int SetupStats(int x, int y, int w)
        {
            var q = PutText("Do you want to track your stats in and out of the game?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText("AZS Stats records your runs while you play (/azs in game) and fills the launcher's STATS page: records, " +
                            "abilities, classes, play time, your Nemesis and co-op with friends.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var yes = Put(new WowButton("YES, TRACK MY STATS", delegate { setupStats = true; SetupGo(4); }, 250, 42, 12), x, y);
            Put(new WowButton("NO", delegate { setupStats = false; SetupGo(4); }, 140, 42, 12), yes.Right + 12, y);
            return yes.Bottom;
        }

        // page 4, then: the other addons, all optional
        int SetupAddons(int x, int y, int w)
        {
            var q = PutText("Would you like to download addons?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText("They are all optional: you choose each one you want in Addons > DOWNLOAD MORE.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var yes = Put(new WowButton("YES, ADDONS", delegate { setupAddons = "download"; SetupNext(); }, 200, 42, 12), x, y);
            Put(new WowButton("NO THANKS", delegate { setupAddons = null; SetupNext(); }, 170, 42, 11), yes.Right + 12, y);
            return yes.Bottom;
        }

        // after the questions: the game's download when it is not here yet, else setup is done
        void SetupNext()
        {
            if (!ClientPresent()) SetupGo(5);
            else SetupLeave(setupAddons, setupStats);
        }

        // page 5: the game is downloaded now, after a click; cancelling it cancels setup
        int SetupDownload(int x, int y, int w)
        {
            var q = PutText("Ready to download the game", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText(Lang.F("Azeroth Survivors is not on this PC yet. The launcher will now download it into this folder:\n{0}\n\n" +
                            "The download shows in the bar at the bottom. You can stop it there at any time.", GameDir()),
                            11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var dl = Put(new WowButton("DOWNLOAD THE GAME", delegate
            {
                setupDownloading = true;
                SetupLeave(setupAddons, setupStats);
                InstallClient(false);
            }, 260, 42, 12), x, y);
            Put(new WowButton("CANCEL SETUP", SetupCancel, 190, 42, 11), dl.Right + 12, y);
            return dl.Bottom;
        }

        // setup (or the game's download started from it) was cancelled. RUN SETUP AGAIN: back to the home screen with
        // the settings as they were. A first setup: the launcher takes away what it made (CleanUpFirstSetup) and the
        // section window stays up with the last page
        void SetupCancel()
        {
            if (!setupFirstTime)
            {
                setupResult = null;
                SetupLeave(null, null);
                return;
            }
            CleanUpFirstSetup();
            setupStats = null;
            setupAddons = null;
            Cfg.Set("setup_done", false);
            setupOpen = true;
            current = "Setup";
            foreach (var b in nav.Values) b.Active = false;
            RefreshStrip();
            ShowSide();
            RefreshPlayLabel();
            SetupGo(6);
        }

        // A cancelled first setup leaves nothing of the launcher's behind but the exe: its own folder (settings, log,
        // backups, the unfinished download in temp) and the folders setup created while they are still empty. The game,
        // a save imported into it and the cloud folder are never touched. Nothing is saved again until setup restarts.
        void CleanUpFirstSetup()
        {
            Config.Frozen = true;
            try { Util.DeleteTreeQuiet(Path.Combine(Util.LauncherDir, "temp")); } catch (Exception) { }
            for (int i = setupCreated.Count - 1; i >= 0; i--)
                try
                {
                    string d = setupCreated[i];
                    if (Directory.Exists(d) && Directory.GetFileSystemEntries(d).Length == 0) Directory.Delete(d);
                }
                catch (Exception) { }
            setupCreated.Clear();
            try { Util.DeleteTreeQuiet(Util.DataDir); } catch (Exception) { }
        }

        // the game's download, started from setup, was stopped: setup is cancelled with it
        public void SetupDownloadCancelled()
        {
            if (!setupDownloading) return;
            setupDownloading = false;
            SetupCancel();
        }

        // page 6: what the launcher is for, and the way back in
        int SetupCancelled(int x, int y, int w)
        {
            var q = PutText("Setup was cancelled", 13, FontStyle.Bold, Theme.Gold, x, y, w);
            var p = PutText("I'm sorry the launcher could not help this time. Everything it set up has been taken away again; your " +
                            "game, your saves and your cloud files are untouched.\n\nIf you need more help, please contact Slimjim " +
                            "in the Discord. Best of luck!", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var again = Put(new WowButton("START SETUP AGAIN", ShowSetupDialog, 250, 42, 12), x, y);
            return again.Bottom;
        }

        // setup ends: what was chosen is saved (nothing when it was left before the folders page: it is asked again next
        // time), then the Addons section or the home screen's Community
        void SetupLeave(string go, bool? trackStats)
        {
            setupOpen = false;
            current = null;
            if (setupResult != null)
            {
                foreach (var kv in setupResult) Cfg.Set(kv.Key, kv.Value);
                foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                    if (!setupResult.ContainsKey(key)) Cfg.Remove(key);   // back to the default location
                // the folders that are not there yet are created now, empty
                foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                {
                    string d = setupResult.ContainsKey(key) ? (string)setupResult[key] : setupDefaults[key];
                    try
                    {
                        if (string.IsNullOrEmpty(d) || Directory.Exists(d)) continue;
                        // every folder on the way that is not there yet: taken away again if a first setup is cancelled
                        var made = new List<string>();
                        for (string up = Path.GetFullPath(d); !string.IsNullOrEmpty(up) && !Directory.Exists(up); up = Path.GetDirectoryName(up))
                            made.Insert(0, up);
                        Directory.CreateDirectory(d);
                        if (setupFirstTime) setupCreated.AddRange(made);
                    }
                    catch (Exception) { }
                }
                Cfg.Set("setup_done", true);
                Cfg.Save();
                RefreshPlayLabel();                           // PLAY is no longer grayed out
                SetStatus("Setup saved.");
                ApplyCloudVisibility();
                if (trackStats.HasValue)                      // AZS Stats only when the player wants their stats tracked
                {
                    SetStatsEnabled(trackStats.Value);
                    if (trackStats.Value && ClientPresent()) InstallStatsAddon();
                    else if (trackStats.Value) { Cfg.Set("stats_pending", true); Cfg.Save(); }   // once the game is installed
                }
                if (go != null)
                {
                    addonMode = go;
                    ShowSection("Addons");
                    if (current == "Addons") return;          // (Addons waits for the game: then the home screen)
                }
            }
            ShowSection("Community");
        }
    }
}
