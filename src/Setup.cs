// First-time setup, inside the launcher's section window (no window of its own): the language first, then whether the
// player comes from the website (their save is imported), cloud saving and the folders, and the addons.
// While it runs no Community video is shown; when it is done (or left for later) the home screen's Community comes back.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        int setupPage;                                        // 0 language, 1 website save, 2 cloud and folders, 3 addons, 4 AZS Stats
        bool setupCloud;
        string setupImported;
        Dictionary<string, string> setupVals, setupDefaults;
        Dictionary<string, object> setupResult;

        void ShowSetupDialog()
        {
            SyncSettings ss = SyncSettings();
            setupCloud = Cfg.Bool("setup_done", false) ? CloudEnabled() : Cfg.Bool("cloud_enabled", true);
            setupVals = new Dictionary<string, string>();
            setupDefaults = new Dictionary<string, string>();
            setupVals["cloud_dir"] = Cfg.Str("cloud_dir");
            setupVals["addons_dir"] = ss.AddonsDir;
            setupVals["save_dir"] = ss.SaveDir;
            setupVals["saved_dir"] = ss.SavedDir;
            setupDefaults["addons_dir"] = Path.Combine(ss.ConfigDir, "AddOns");
            setupDefaults["saved_dir"] = Path.Combine(ss.ConfigDir, "saved");
            setupDefaults["save_dir"] = Util.DefaultSaveDir;
            setupImported = null;
            setupResult = null;
            setupPage = 0;
            setupOpen = true;
            foreach (var b in nav.Values) b.Active = false;
            current = "Setup";
            RefreshStrip();
            ShowSide();
            BuildPanel("Setup");
        }

        void SetupGo(int page) { setupPage = page; BuildPanel("Setup"); }

        // the back arrow: the page before; on the first page it leaves setup for later
        void SetupBack()
        {
            if (setupPage == 4) SetupGo(3);
            else if (setupPage > 0 && setupPage <= 3) SetupGo(setupPage - 1);
            else SetupLeave(null, null);
        }

        static readonly string[] SetupSteps = { "Language", "Your save", "Cloud & folders", "Addons", "AZS Stats" };

        void PanelSetup()
        {
            ClearPanel("Setup");
            int step = Math.Min(setupPage, 3) + 1;
            SetCrumbs("Setup", null, SetupSteps[setupPage], null);
            int x = 24, w = PW - 48, y = 10;
            var head = PutText("Welcome, Adventurer", 17, FontStyle.Bold, Theme.Gold, x, y, w);
            PutText(Lang.F("Step {0} of {1}", step, 4), 10, FontStyle.Italic, Theme.GoldDim, PW - 24 - 120, y + 8, 120);
            y = BottomOf(head) + 4;
            Put(UI.Line(Theme.GoldDim, w, 2), x, y);
            y += 10;
            if (setupPage == 0) SetupLanguage(x, y, w);
            else if (setupPage == 1) SetupSave(x, y, w);
            else if (setupPage == 2) SetupFolders(x, y, w);
            else if (setupPage == 3) SetupAddons(x, y, w);
            else SetupStats(x, y, w);
        }

        // page 1: the language, every one the launcher speaks; the choice is shown at once
        void SetupLanguage(int x, int y, int w)
        {
            var q = PutText("Which language do you want the launcher in?", 13, FontStyle.Bold, Theme.Gold, x, y, w);
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
            Put(new WowButton("NEXT ›", delegate { SetupGo(1); }, 170, 38, 12), PW - 24 - (int)(170 * Theme.Zoom), y + 10);
        }

        // page 2: moving over from the website? The save is imported before anything else
        void SetupSave(int x, int y, int w)
        {
            var q = PutText("Moving over from the website?", 13, FontStyle.Bold, Theme.Gold, x, y, w);
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
            }
            else
            {
                var im = Put(new WowButton("IMPORT MY SAVE", SetupImport, 220, 42, 12), x, y);
                Put(new WowButton("I'M NEW HERE", delegate { SetupGo(2); }, 190, 42, 11), im.Right + 12, y);
            }
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
        void SetupFolders(int x, int y, int w)
        {
            var a = PutText("A.  Cloud saving", 13, FontStyle.Bold, Theme.Gold, x, y, w);
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
            var n = PutText("Folders found at their usual place are used as they are. Choose the ones that are missing.",
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
            var fin = Put(new WowButton("FINISH", SetupFinishFolders, 170, 38, 12), x, y + 12);
            Put(new WowButton("LATER", delegate { SetupLeave(null, null); }, 120, 38, 11), fin.Right + 12, y + 12);
        }

        int SetupFolderRow(string label, string key, Action chooser, bool defaultOk, int x, int y, int w)
        {
            string path = setupVals.ContainsKey(key) ? setupVals[key] ?? "" : "";
            bool found = path.Length > 0 && Directory.Exists(path);
            var name = PutText(label, 11, FontStyle.Bold, Theme.Gold, x, y, 0);
            string note = found ? (defaultOk ? "✓ found" : "✓ chosen") : (path.Length == 0 ? "not chosen yet" : "not found - please choose it");
            PutText(note, 9, FontStyle.Italic, found ? Theme.Good : Theme.Bad, name.Right + 8, y + 3, 0);
            var pathL = PutText(path.Length > 0 ? path : Lang.T("(not set)"), 9, FontStyle.Regular, Theme.LinkBlue, x, BottomOf(name), w - 130, false);
            int h = BottomOf(pathL) - y;
            Put(new WowButton(found ? "CHANGE" : "CHOOSE", chooser, 100, 28, 9), x + w - (int)(100 * Theme.Zoom), y + Math.Max(0, (h - (int)(28 * Theme.Zoom)) / 2));
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
            var missing = new List<string>();
            foreach (var kv in new[] { new[] { "AddOns", "addons_dir" }, new[] { "Game saves", "save_dir" }, new[] { "Addon settings", "saved_dir" } })
                if (!Directory.Exists(setupVals[kv[1]] ?? "")) missing.Add(kv[0]);
            if (missing.Count > 0 && Msg.Show(side, "These folders were not found:\n\n  " + string.Join("\n  ", missing.ToArray()) +
                    "\n\nFinish anyway? You can change them later in Options.", "Setup", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            var res = new Dictionary<string, object>();
            res["cloud_enabled"] = setupCloud;
            if (setupCloud) res["cloud_dir"] = setupVals["cloud_dir"];
            foreach (string key in new[] { "addons_dir", "saved_dir", "save_dir" })
                if (!Util.SamePath(setupVals[key], setupDefaults[key])) res[key] = Path.GetFullPath(setupVals[key]);   // only stored when not the default
            setupResult = res;
            SetupGo(3);
        }

        // page 4: addons, all optional; AZS Stats is asked about on its own
        void SetupAddons(int x, int y, int w)
        {
            var q = PutText("Your settings are saved.\n\nWould you like to download addons? They are all optional: " +
                            "you choose each one you want.", 12, FontStyle.Regular, Theme.Text, x, y, w);
            y = BottomOf(q) + 14;
            var yes = Put(new WowButton("YES, ADDONS", delegate { SetupGo(4); }, 200, 42, 12), x, y);
            Put(new WowButton("NO THANKS", delegate { SetupLeave(null, null); }, 170, 42, 11), yes.Right + 12, y);
            Put(new WowButton("ENABLE / DISABLE MY ADDONS", delegate { SetupLeave("installed", null); }, 300, 34, 10), x, BottomOf(yes) + 10);
        }

        void SetupStats(int x, int y, int w)
        {
            var q = PutText("Do you want to track your stats in and out of the game?", 13, FontStyle.Bold, Theme.Gold, x, y, w);
            var p = PutText("AZS Stats records your runs while you play (/azs in game) and fills the launcher's STATS page: records, " +
                            "abilities, classes, play time, your Nemesis and co-op with friends.", 11, FontStyle.Regular, Theme.Text, x, BottomOf(q) + 4, w);
            y = BottomOf(p) + 14;
            var yes = Put(new WowButton("YES, TRACK MY STATS", delegate { SetupLeave("download", true); }, 250, 42, 12), x, y);
            Put(new WowButton("NO", delegate { SetupLeave("download", false); }, 140, 42, 12), yes.Right + 12, y);
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
                Cfg.Set("setup_done", true);
                Cfg.Save();
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
