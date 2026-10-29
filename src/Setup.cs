// First-time setup, inside the launcher's section window (no window of its own): a welcome, then six setup slides for
// language, save import, cloud saving and folders, AZS Stats, addons and the game location/install. Choices stay staged
// until the final slide. Setup cannot be closed before it is done: cancelling it shows a last page that
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
        // 0 welcome, 1 language, 2 website save, 3 cloud and folders, 4 AZS Stats, 5 addons, 6 game, 7 setup cancelled
        int setupPage;
        bool setupCloud, setupCloudFolderRequired;
        bool? setupStats;                                     // the answer to the AZS Stats question
        string setupAddons;                                   // "download" when the player wants addons, else null
        bool setupDownloading;                                // first-time game download is in progress
        bool setupFirstTime;                                  // setup was never done before (not RUN SETUP AGAIN)
        string setupImported;
        string setupImportFile;                               // selected save is validated now, imported only when setup finishes
        string setupGameDir;
        string setupLanguage;
        Dictionary<string, string> setupVals, setupDefaults;
        Dictionary<string, object> setupResult;

        // Setup keeps explicitly configured folders, but does not guess conventional save or addon locations.
        void ShowSetupDialog()
        {
            setupCloud = Cfg.Bool("cloud_enabled", false);
            setupCloudFolderRequired = false;
            setupVals = new Dictionary<string, string>();
            setupDefaults = new Dictionary<string, string>();
            setupDefaults["addons_dir"] = "";
            setupDefaults["saved_dir"] = "";
            setupDefaults["save_dir"] = "";
            setupVals["cloud_dir"] = Cfg.Str("cloud_dir");
            foreach (var kv in setupDefaults) setupVals[kv.Key] = Cfg.Str(kv.Key);
            setupGameDir = ClientPresent() ? GameDir() : Cfg.Str("game_dir");
            setupLanguage = Cfg.Str("language");
            if (string.IsNullOrWhiteSpace(setupLanguage)) setupLanguage = Lang.Code;
            Config.Frozen = false;                            // (after a cancelled first setup: saving again)
            // nothing else stays open while setup runs: the launcher's own message boxes and dialogs are closed
            try
            {
                var open = new List<Form>();
                foreach (Form f in Application.OpenForms) if (f is WowDialog) open.Add(f);
                foreach (Form f in open) f.Close();
            }
            catch (Exception) { }
            setupFirstTime = !Cfg.Bool("setup_done", false);
            setupImported = null;
            setupImportFile = null;
            setupResult = null;
            setupStats = null;
            setupAddons = null;
            setupPage = 0;
            setupOpen = true;
            current = "Setup";
            SetSetupNavigationEnabled(false);
            ApplyLanguage(setupLanguage, false);
            foreach (var b in nav.Values) b.Active = false;
            RefreshStrip();
            ShowSide();
            BuildPanel("Setup");
        }

        void SetupGo(int page) { setupPage = page; BuildPanel("Setup"); }

        // The setup back arrow always moves exactly one slide; the welcome has no back action.
        void SetupBack()
        {
            if (setupPage > 0 && setupPage <= 7) SetupGo(setupPage - 1);
        }

        void SetupRestart() { ShowSetupDialog(); }

        static readonly string[] SetupSteps = { "Welcome", "Language", "Your save", "Cloud & folders", "AZS Stats", "Addons", "Game" };

        void PanelSetup()
        {
            ClearPanel("Setup");
            // Keep the restart control fixed at the bottom while each slide's content can scroll.
            int setupFooter = 52;
            var sp = MakeScroll(panel, new Rectangle(8, 4, PW - 16, PH - 12 - setupFooter), Theme.Panel, Theme.Panel);
            setupHeadW = 0;
            if (setupPage > 0 && setupPage <= 6)
            {
                var stepL = PutText(Lang.F("Step {0} of {1}", setupPage, 6), 10, FontStyle.Italic, Theme.GoldDim, 0, 8, 0);
                stepL.Left = PW - 24 - stepL.Width;
                stepL.BringToFront();
                setupHeadW = stepL.Width + 16;                    // the first heading stays clear of it
            }
            putInto = sp;
            try
            {
                int px = 16, pw = sp.ClientSize.Width - 32;
                int end;
                if (setupPage == 0) end = SetupWelcome(px, 6, pw);
                else if (setupPage == 1) end = SetupLanguage(px, 6, pw);
                else if (setupPage == 2) end = SetupSave(px, 6, pw);
                else if (setupPage == 3) end = SetupFolders(px, 6, pw);
                else if (setupPage == 4) end = SetupStats(px, 6, pw);
                else if (setupPage == 5) end = SetupAddons(px, 6, pw);
                else if (setupPage == 6) end = SetupDownload(px, 6, pw);
                else end = SetupCancelled(px, 6, pw);
                var pad = new Panel();
                pad.BackColor = Theme.Panel;
                pad.Bounds = new Rectangle(0, end, 1, 12);
                sp.Controls.Add(pad);
            }
            finally { putInto = null; }
            var cancel = new WowButton("CANCEL SETUP", SetupCancel, 142, 34, 9);
            cancel.Location = new Point(16, PH - 44);
            panel.Controls.Add(cancel);
            cancel.BringToFront();
            var restart = new WowButton("START OVER", SetupRestart, 142, 34, 9);
            restart.Location = new Point(cancel.Right + 8, PH - 44);
            panel.Controls.Add(restart);
            restart.BringToFront();
            if (setupPage == 0 || setupPage == 1)
            {
                var next = new WowButton("NEXT ›", delegate { SetupGo(setupPage + 1); }, 170, 38, 12);
                next.Location = new Point(PW - 16 - next.Width, PH - 48);
                panel.Controls.Add(next);
                next.BringToFront();
            }
        }

        int setupHeadW;                                       // room kept free for the step count beside a page heading

        int SetupWelcome(int x, int y, int w)
        {
            var title = PutText("Welcome to Azeroth Survivors Launcher", 15, FontStyle.Bold, Theme.Gold, x, y, w);
            var p = PutText("Thank you for using my community-made launcher. This is designed to help people celebrate how cool this game is and make a unifying one-stop-shop for all Azeroth Survivors needs\n\n" +
                            "During your first time setup please go through this next line of slides that will help set the game up for an existing user, or even help out if you are new\n\n" +
                            "Please follow this step by step guide to help get ready to play", 12, FontStyle.Regular, Theme.Text, x, BottomOf(title) + 12, w);
            return BottomOf(p);
        }

        // page 2: the language, every one the launcher speaks; the choice is shown at once
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
            return y;
        }

        // page 3: moving over from the website? The save is imported before anything else
        int SetupSave(int x, int y, int w)
        {
            var q = PutText("Moving over from the website?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText("Played Azeroth Survivors in your browser? Bring your progress with you: your heroes, the zones you opened, " +
                            "your gear, talents and codex.\n\nOn the website, open Options > Saves and press Export Save. Then choose " +
                            "that .sav file here. The game opens with your progress, ready to play.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            if (setupImported != null)
            {
                var ok = PutText("✓ Save selected: " + setupImported + " (will import when setup finishes)", 11, FontStyle.Bold, Theme.Good, x, y, w, false);
                y = BottomOf(ok) + 12;
                var n = Put(new WowButton("NEXT ›", delegate { SetupGo(3); }, 170, 38, 12), x, y);
                Put(new WowButton("IMPORT ANOTHER", SetupImport, 190, 38, 10), n.Right + 12, y);
                Put(new WowButton("CLEAR", delegate { setupImported = setupImportFile = null; BuildPanel("Setup"); }, 92, 38, 9), n.Right + (int)(202 * Theme.Zoom), y);
                return n.Bottom;
            }
            var im = Put(new WowButton("IMPORT MY SAVE", SetupImport, 220, 42, 12), x, y);
            Put(new WowButton("I'M NEW HERE", delegate { SetupGo(3); }, 190, 42, 11), im.Right + 12, y);
            return im.Bottom;
        }

        void SetupImport()
        {
            string saveDir = setupVals["save_dir"] ?? "";
            if (saveDir.Length == 0 || !Directory.Exists(saveDir))
            {
                saveDir = AskFolder(side, "Choose where to save the imported game progress", Util.Home);
                if (saveDir == null) return;
                setupVals["save_dir"] = Path.GetFullPath(saveDir);
            }
            using (var d = new OpenFileDialog())
            {
                d.Title = Lang.T("Choose the save you exported from the website (.sav)");
                d.Filter = Lang.T("Azeroth Survivors save (*.sav)|*.sav|All files (*.*)|*.*");
                string downloads = Path.Combine(Util.Home, "Downloads");
                if (Directory.Exists(downloads)) d.InitialDirectory = downloads;
                if (d.ShowDialog(side) != DialogResult.OK) return;
                try { SaveImport.Read(d.FileName); }
                catch (Exception e) { Msg.Show(side, e.Message, "Import save", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                setupImportFile = d.FileName;
                setupImported = Path.GetFileName(d.FileName);
            }
            BuildPanel("Setup");
        }

        // page 4: cloud saving, and where the files are
        int SetupFolders(int x, int y, int w)
        {
            var a = PutText("A.  Cloud saving", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            y = BottomOf(a) + 4;
            var cb = UI.Check("Keep my progress in the cloud (Google Drive, OneDrive, Dropbox ...)\nand share it between my PCs",
                              setupCloud, Theme.Text, Theme.Panel, 11);
            cb.Size = VStack.Measure(cb);
            cb.CheckedChanged += delegate
            {
                setupCloud = cb.Checked;
                if (!setupCloud) setupCloudFolderRequired = false;
                BeginInvoke(new Action(delegate { BuildPanel("Setup"); }));
            };
            Put(cb, x - 4, y);
            y = BottomOf(cb) + 4;
            if (setupCloud) y = SetupFolderRow("Sync folder (inside your cloud drive)", "cloud_dir", delegate
            {
                string d = PickCloudDir(side, setupVals["cloud_dir"]);
                if (d != null) { setupVals["cloud_dir"] = d; setupCloudFolderRequired = false; BuildPanel("Setup"); }
            }, false, x, y, w);
            else
            {
                var off = PutText("Cloud saving stays off. You can turn it on later in Options > CLOUD SAVE.", 10, FontStyle.Italic, Theme.GoldDim, x, y, w);
                y = BottomOf(off) + 2;
            }
            if (setupCloud && setupCloudFolderRequired)
            {
                string message = (setupVals["cloud_dir"] ?? "").Trim().Length == 0
                    ? "Choose a sync folder, or turn off cloud saving to continue."
                    : "Choose your game saves folder, or turn off cloud saving to continue.";
                var required = PutText(message, 10, FontStyle.Bold, Theme.Bad, x, y + 2, w);
                y = BottomOf(required) + 4;
            }
            Put(UI.Line(Theme.Bronze, w, 1), x, y + 6);
            var b = PutText("B.  Where are your files?", 13, FontStyle.Bold, Theme.Gold, x, y + 14, w);
            var n = PutText("Choose the folders you want the launcher to use. Locations stay unset until you choose them.",
                            10, FontStyle.Regular, Theme.Text, x, BottomOf(b), w);
            y = BottomOf(n) + 4;
            y = SetupFolderRow("AddOns", "addons_dir", delegate { SetupPick("addons_dir", "Select your AddOns folder"); }, false, x, y, w);
            y = SetupFolderRow("Game saves", "save_dir", delegate
            {
                SetupPick("save_dir", "Select the folder that holds your game saves");
            }, false, x, y, w);
            y = SetupFolderRow("Addon settings", "saved_dir", delegate
            {
                SetupPick("saved_dir", "Select the folder that holds your addon settings");
            }, false, x, y, w);
            var fin = Put(new WowButton("NEXT ›", SetupFinishFolders, 170, 38, 12), x, y + 12);
            return fin.Bottom;
        }

        int SetupFolderRow(string label, string key, Action chooser, bool defaultOk, int x, int y, int w)
        {
            string path = setupVals.ContainsKey(key) ? setupVals[key] ?? "" : "";
            bool found = path.Length > 0 && Directory.Exists(path);
            var name = PutText(label, 11, FontStyle.Bold, Theme.Gold, x, y, 0);
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
                setupCloudFolderRequired = true;
                BuildPanel("Setup");
                return;
            }
            if (setupCloud && (setupVals["save_dir"] ?? "").Trim().Length == 0)
            {
                setupCloudFolderRequired = true;
                BuildPanel("Setup");
                return;
            }
            setupCloudFolderRequired = false;
            var res = new Dictionary<string, object>();
            res["cloud_enabled"] = setupCloud;
            if (setupCloud) res["cloud_dir"] = setupVals["cloud_dir"];
            foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                if (!string.IsNullOrWhiteSpace(setupVals[key]) && !Util.SamePath(setupVals[key], setupDefaults[key]))
                    res[key] = Path.GetFullPath(setupVals[key]);
            setupResult = res;
            SetupGo(4);
        }

        // page 5: AZS Stats, asked before the addons (it is one of them)
        int SetupStats(int x, int y, int w)
        {
            var q = PutText("Do you want to track your stats in and out of the game?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText("AZS Stats records your runs while you play (/azs in game) and fills the launcher's STATS page: records, " +
                            "abilities, classes, play time, your Nemesis and co-op with friends.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var yes = Put(new WowButton("YES, TRACK MY STATS", delegate { setupStats = true; SetupGo(5); }, 250, 42, 12), x, y);
            Put(new WowButton("NO", delegate { setupStats = false; SetupGo(5); }, 140, 42, 12), yes.Right + 12, y);
            return yes.Bottom;
        }

        // page 6: the other addons, all optional
        int SetupAddons(int x, int y, int w)
        {
            var q = PutText("Would you like to download addons?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            var p = PutText("They are all optional: you choose each one you want in Addons > DOWNLOAD MORE.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var yes = Put(new WowButton("YES, ADDONS", delegate { setupAddons = "download"; SetupNext(); }, 200, 42, 12), x, y);
            Put(new WowButton("NO THANKS", delegate { setupAddons = null; SetupNext(); }, 170, 42, 11), yes.Right + 12, y);
            return yes.Bottom;
        }

        // The game location is always the sixth slide, whether the game is already installed or not.
        void SetupNext()
        {
            SetupGo(6);
        }

        // slide 6: select the game folder, then explicitly start the download or finish with the existing game
        int SetupDownload(int x, int y, int w)
        {
            var q = PutText("Ready to download and play?", 13, FontStyle.Bold, Theme.Gold, x, y, w - setupHeadW);
            bool alreadyInstalled = setupGameDir.Length > 0 && File.Exists(Path.Combine(setupGameDir, Util.GameExe));
            var p = PutText(alreadyInstalled
                                ? "Your game is already in this folder. Finish setup to return to the launcher."
                                : "Choose where to install the game. The download starts when you press READY TO DOWNLOAD AND PLAY?",
                            11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 12;
            var path = PutText(setupGameDir.Length > 0 ? setupGameDir : "No game folder selected", 10, FontStyle.Italic,
                               setupGameDir.Length > 0 ? Theme.LinkBlue : Theme.GoldDim, x, y, w - 120);
            Put(new WowButton("CHOOSE FOLDER", delegate
            {
                string d = AskFolder(side, "Choose a folder for Azeroth Survivors", setupGameDir.Length > 0 && Directory.Exists(setupGameDir) ? setupGameDir : Util.Home);
                if (d == null) return;
                setupGameDir = Path.GetFullPath(d);
                BuildPanel("Setup");
            }, 110, 30, 9), x + w - (int)(110 * Theme.Zoom), y - 3);
            y = BottomOf(path) + 12;
            if (alreadyInstalled)
            {
                var existing = Put(new WowButton("FINISH SETUP", delegate
                {
                    if (setupResult == null) setupResult = new Dictionary<string, object>();
                    setupResult["game_dir"] = Path.GetFullPath(setupGameDir);
                    setupResult["installed_by_launcher"] = true;
                    SetupLeave(setupAddons, setupStats);
                }, 250, 40, 12), x, y);
                return existing.Bottom;
            }
            var dl = Put(new WowButton("DOWNLOAD THE GAME", delegate
            {
                if (setupGameDir.Length == 0)
                {
                    Warn("Game folder", "Choose a folder for the game first.");
                    return;
                }
                if (setupResult == null) setupResult = new Dictionary<string, object>();
                setupResult["game_dir"] = Path.GetFullPath(setupGameDir);
                if (!SetupLeave(setupAddons, setupStats)) return;
                setupDownloading = true;
                InstallClient(false, setupGameDir);
            }, 270, 42, 11), x, y);
            dl.Label = "READY TO DOWNLOAD AND PLAY?";
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
            SetupGo(7);
        }

        // A cancelled first setup does not remove user folders or game data.
        void CleanUpFirstSetup()
        {
            Config.Frozen = true;
            // Remove only launcher-owned settings/state and this launcher's uniquely named partial-download folder.
            // DataDir also holds user content (saved addon profiles, downloaded addons, guide videos, and backups),
            // so never recursively delete it. The game, its save directory, addon folders, and cloud data are separate.
            var playerFolders = new List<string>();
            try
            {
                SyncSettings ss = SyncSettings();
                playerFolders.Add(ss.SaveDir);
                playerFolders.Add(ss.AddonsDir);
                playerFolders.Add(ss.SavedDir);
                playerFolders.Add(ss.CloudDir);
            }
            catch (Exception) { }
            if (setupVals != null)
                foreach (string key in new[] { "save_dir", "addons_dir", "saved_dir", "cloud_dir" })
                    if (setupVals.ContainsKey(key)) playerFolders.Add(setupVals[key]);

            foreach (string name in new[] { "config.json", "state.json", "log.txt", "log.old.txt" })
                try
                {
                    string file = Path.Combine(Util.DataDir, name);
                    if (File.Exists(file) && !IsInsidePlayerFolder(file, playerFolders)) File.Delete(file);
                }
                catch (Exception) { }
            string downloadTemp = Path.Combine(Util.LauncherDir, Util.DownloadTempName);
            if (!OverlapsPlayerFolder(downloadTemp, playerFolders))
                try { Util.DeleteTreeQuiet(downloadTemp); } catch (Exception) { }
        }

        static bool IsInsidePlayerFolder(string path, List<string> folders)
        {
            if (folders == null) return false;
            string full;
            try { full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch (Exception) { return true; }
            foreach (string folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                string root;
                try { root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
                catch (Exception) { continue; }
                if (full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static bool OverlapsPlayerFolder(string path, List<string> folders)
        {
            if (folders == null) return false;
            foreach (string folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                if (IsInsidePlayerFolder(path, new List<string> { folder }) ||
                    IsInsidePlayerFolder(folder, new List<string> { path })) return true;
            }
            return false;
        }

        // the game's download, started from setup, was stopped: setup is cancelled with it
        public void SetupDownloadCancelled()
        {
            if (!setupDownloading) return;
            setupDownloading = false;
            RefreshPlayLabel();
            SetStatus("Game download cancelled. Choose INSTALL when you are ready to try again.");
        }

        // page 7: what the launcher is for, and the way back in
        int SetupCancelled(int x, int y, int w)
        {
            var q = PutText("Setup was cancelled", 13, FontStyle.Bold, Theme.Gold, x, y, w);
            var p = PutText("I'm sorry the launcher could not help this time. Everything it set up has been taken away again; your " +
                            "game, your saves and your cloud files are untouched.\n\nIf you need more help, please contact Slimjim " +
                            "in the Discord. Best of luck!", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            return BottomOf(p);
        }

        // setup ends: what was chosen is saved (nothing when it was left before the folders page: it is asked again next
        // time), then the Addons section or the home screen's Community
        bool SetupLeave(string go, bool? trackStats)
        {
            if (setupResult != null && !CommitSetupImport()) return false;
            setupOpen = false;
            SetSetupNavigationEnabled(true);
            current = null;
            if (setupResult != null)
            {
                if (!string.IsNullOrWhiteSpace(setupLanguage)) Cfg.Set("language", setupLanguage);
                foreach (var kv in setupResult) Cfg.Set(kv.Key, kv.Value);
                foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                    if (!setupResult.ContainsKey(key)) Cfg.Remove(key);   // leave unset until the player chooses a folder
                Cfg.Set("setup_done", true);
                UpdateNewPlayerFlag();
                Cfg.Set("setup_folder", Util.LauncherDir);       // the folder this setup was for (a copy elsewhere sets up again)
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
                    if (current == "Addons") return true;     // (Addons waits for the game: then the home screen)
                }
            }
            ShowSection("Community");
            return true;
        }

        bool CommitSetupImport()
        {
            if (string.IsNullOrWhiteSpace(setupImportFile)) return true;
            string saveDir = setupVals != null && setupVals.ContainsKey("save_dir") ? setupVals["save_dir"] : "";
            if (string.IsNullOrWhiteSpace(saveDir)) saveDir = Util.DefaultSaveDir;
            string existing = Path.Combine(saveDir, SaveImport.SaveFile);
            if (File.Exists(existing) && new FileInfo(existing).Length > 0)
            {
                if (Msg.Show(side, "This PC already has progress in the game.\n\nReplace it with " + Path.GetFileName(setupImportFile) +
                               "?\nWhat is there now is kept as a backup in the game's Saves folder.",
                               "Import save", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    setupImportFile = setupImported = null;
                    SetStatus("Existing save kept. Setup is still open; you can finish without importing it.");
                    return false;
                }
            }
            try
            {
                SaveImport.Import(setupImportFile, saveDir);
                Cfg.Set("save_imported", DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception e)
            {
                Msg.Show(side, "The save could not be imported:\n\n" + e.Message, "Import save", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
