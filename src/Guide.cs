// The Guide (assets\guide.txt), in two groups (New Player and Gameplay), with Theorycrafting as a submenu in Gameplay: topic cards (every topic open at any time, nothing tracked), topics made of pages, and
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

        // under the back arrow row, or under the topic tabs when they are shown
        int guideTabsBottom;
        int GuideTop { get { return guideTabsBottom > 0 ? guideTabsBottom : 8; } }
        int guideLastTopic;                                  // the tab to go back to when the search is emptied
        bool guideFocusSearch;

        // The guide's groups, each a row of topic tabs (by the topics' titles, in this order). A topic not named here goes
        // to the first group. "Talent Tree Builder" is not a topic of guide.txt: it is the class picker for the talent
        // trees and their test builds.
        public const string TalentBuilder = "Talent Tree Builder";
        static readonly string[][] GuideGroups =
        {
            new[] { "NEW PLAYER", "Getting Started", "Controls", "Your First Run", "Tips for New Players", "Pickups, Caches & Guardians",
                    "Zones & Bosses", "Talents", "Gear & the Armory", "Addons", "Achievements & Rewards", "Level-Ups & Cards", "Co-op" },
            new[] { "GAMEPLAY", "Heroic & Endless", "Classes", "Races", "Items", "Transmog", "Build Guides" },
            new[] { "THEORYCRAFTING", TalentBuilder, GearPlanner },
        };
        int guideGroup;                                       // the group whose tabs are shown
        bool guideMain = true;                                // the main page: choose New Player or Gameplay
        bool gBuilder;                                        // the Talent Tree Builder is open
        int guideGroupPage = -1;                              // -1 chooser; otherwise the selected category (or Theorycrafting submenu)

        static int GroupOf(string title)
        {
            for (int g = 0; g < GuideGroups.Length; g++)
                for (int i = 1; i < GuideGroups[g].Length; i++)
                    if (string.Equals(GuideGroups[g][i], title, StringComparison.OrdinalIgnoreCase)) return g;
            return 0;
        }

        void LoadGuideFavorites()
        {
            var saved = Json.AsList(Json.Get(Cfg.Data, "guide_favorites"));
            if (saved == null) return;
            for (int i = 0; i < Math.Min(saved.Count, GuideFavoriteCount); i++)
            {
                var row = Json.AsDict(saved[i]);
                if (row == null) continue;
                string topic = Json.Str(row, "topic"), page = Json.Str(row, "page");
                string kind = Json.Str(row, "kind");
                string classKey = Json.Str(row, "class"), url = Json.Str(row, "url");
                if (kind.Length == 0) kind = "guide";          // earlier local favorites stored only the topic and page
                if (classKey.Length == 0) classKey = GuideClassKey(topic, page);  // migrate saved class pages to their class icon
                if (kind != "guide" || topic.Length > 0) guideFavorites[i] = new GuideFavoriteRoute(kind, topic, page, classKey, url);
            }
            CompactGuideFavorites();
        }

        void SaveGuideFavorites()
        {
            var saved = new List<object>();
            for (int i = 0; i < guideFavorites.Length; i++)
            {
                var row = new Dictionary<string, object>();
                if (guideFavorites[i] != null)
                {
                    row.Add("kind", guideFavorites[i].Kind);
                    row.Add("topic", guideFavorites[i].Topic);
                    row.Add("page", guideFavorites[i].Page);
                    row.Add("class", guideFavorites[i].ClassKey);
                    row.Add("url", guideFavorites[i].Url);
                }
                saved.Add(row);
            }
            Cfg.Set("guide_favorites", saved);
            Cfg.Save();
        }

        void OpenGuideFavorite(int slot)
        {
            GuideFavoriteRoute favorite = guideFavorites[slot];
            if (favorite == null)
            {
                SetStatus("Hover a path name and click the heart to save it to Quick Access.");
                return;
            }
            if (favorite.Kind == "section")
            {
                if (favorite.Topic == "Setup")
                {
                    if (setupOpen) SetupGo(0);
                    else SetStatus("Setup is only available while the setup guide is open.");
                }
                else OpenMain(favorite.Topic);
                return;
            }
            if (favorite.Kind == "option")
            {
                if (current != "Options" || !side.Visible) ShowSection("Options");
                if (current != "Options") return;
                optionsAbout = favorite.Page == "about";
                optionsLang = favorite.Page == "language";
                if (favorite.Page == "tab") int.TryParse(favorite.ClassKey, out optionsTab);
                else if (optionsAbout || optionsLang) optionsTab = 4;
                BuildPanel("Options");
                return;
            }
            if (favorite.Kind == "addon")
            {
                if (current != "Addons" || !side.Visible) ShowSection("Addons");
                if (current != "Addons") return;
                addonMode = "download";
                int category;
                if (int.TryParse(favorite.ClassKey, out category) && category >= 0 && category < Data.AddonCategories.Length)
                    addonCategory = category;
                BuildPanel("Addons");
                return;
            }
            if (favorite.Kind == "stats")
            {
                if (current != "Stats" || !side.Visible) ShowSection("Stats");
                if (current != "Stats") return;
                if (favorite.Page == "class") SetStatsView(favorite.ClassKey, 0);
                else
                {
                    int tab;
                    if (int.TryParse(favorite.Url, out tab)) SetStatsView(favorite.ClassKey.Length == 0 ? null : favorite.ClassKey, tab);
                }
                return;
            }
            if (favorite.Kind == "setup")
            {
                if (!setupOpen) { SetStatus("Setup is only available while the setup guide is open."); return; }
                int step;
                if (int.TryParse(favorite.ClassKey, out step)) SetupGo(step);
                return;
            }
            if (favorite.Kind == "builder" || favorite.Kind == "planner")
            {
                if (current != "Guide" || !side.Visible) ShowSection("Guide");
                OpenGuideTab(favorite.Kind == "builder" ? -2 : -3);
                RefreshStrip();
                return;
            }
            if (favorite.Kind == "group")
            {
                int group = -1;
                for (int i = 0; i < 3; i++) if (GroupName(i) == favorite.Topic) group = i;
                if (group < 0) { SetStatus("This Guide location no longer exists."); return; }
                if (current != "Guide" || !side.Visible) ShowSection("Guide");
                GuideGroupPage(group);
                return;
            }
            if (favorite.Kind == "builderClass" || favorite.Kind == "plannerClass")
            {
                if (current != "Guide" || !side.Visible) ShowSection("Guide");
                if (favorite.Kind == "builderClass")
                {
                    OpenGuideTab(-2);
                    PlannerClass(favorite.ClassKey);
                    gTalents = favorite.ClassKey;
                }
                else { OpenGuideTab(-3); PlannerClass(favorite.ClassKey); }
                BuildPanel("Guide");
                RefreshStrip();
                return;
            }
            if (favorite.Kind == "guideTalents")
            {
                int talentTopic = Data.Guide.FindIndex(delegate(GuideTopic t) { return t.Title == favorite.Topic; });
                int talentPage = talentTopic < 0 ? -1 : Data.Guide[talentTopic].Pages.FindIndex(delegate(GuidePage p) { return p.Title == favorite.Page; });
                if (talentTopic < 0 || talentPage < 0) { SetStatus("This class talent page is no longer available."); return; }
                if (current != "Guide" || !side.Visible) ShowSection("Guide");
                SetGuideView(talentTopic, talentPage);
                gTalents = favorite.ClassKey;
                BuildPanel("Guide");
                return;
            }
            if (favorite.Kind == "video")
            {
                int videoTopic = Data.Guide.FindIndex(delegate(GuideTopic t) { return t.Title == favorite.Topic; });
                if (videoTopic < 0) { SetStatus("This Guide video is no longer available."); return; }
                GuideTopic parent = Data.Guide[videoTopic];
                GuideVideo video = null;
                if (favorite.Page.Length == 0)
                    video = parent.Videos.Find(delegate(GuideVideo v) { return v.Url == favorite.Url; });
                else
                {
                    GuidePage videoSourcePage = parent.Pages.Find(delegate(GuidePage p) { return p.Title == favorite.Page; });
                    if (videoSourcePage != null) video = videoSourcePage.Videos.Find(delegate(GuideVideo v) { return v.Url == favorite.Url; });
                }
                if (video == null) { SetStatus("This Guide video is no longer available."); return; }
                if (current != "Guide" || !side.Visible) ShowSection("Guide");
                int videoPage = favorite.Page.Length == 0 ? -1 : parent.Pages.FindIndex(delegate(GuidePage p) { return p.Title == favorite.Page; });
                SetGuideView(videoTopic, videoPage);
                gVideo = video;
                BuildPanel("Guide");
                return;
            }
            var topics = Data.Guide;
            int topic = topics.FindIndex(delegate(GuideTopic t) { return string.Equals(t.Title, favorite.Topic, StringComparison.Ordinal); });
            int page = -1;
            if (topic >= 0 && favorite.Page.Length > 0)
                page = topics[topic].Pages.FindIndex(delegate(GuidePage p) { return string.Equals(p.Title, favorite.Page, StringComparison.Ordinal); });
            if (topic < 0 || (favorite.Page.Length > 0 && page < 0))
            {
                SetStatus("This favorite no longer matches a Guide page. Bind the page again to update it.");
                return;
            }
            if (current != "Guide" || !side.Visible) ShowSection("Guide");
            SetGuideView(topic, page);
        }

        void CompactGuideFavorites()
        {
            int next = 0;
            for (int i = 0; i < guideFavorites.Length; i++)
                if (guideFavorites[i] != null)
                {
                    if (i != next) { guideFavorites[next] = guideFavorites[i]; guideFavorites[i] = null; }
                    next++;
                }
        }

        void AddGuideFavorite(GuideFavoriteRoute favorite)
        {
            for (int i = 0; i < guideFavorites.Length; i++)
                if (guideFavorites[i] != null && guideFavorites[i].Kind == favorite.Kind &&
                    guideFavorites[i].Topic == favorite.Topic && guideFavorites[i].Page == favorite.Page &&
                    guideFavorites[i].ClassKey == favorite.ClassKey && guideFavorites[i].Url == favorite.Url)
                {
                    SetStatus(Lang.T(favorite.PageLabel) + " is already in Quick Access " + (i + 1) + ".");
                    return;
                }
            CompactGuideFavorites();
            int slot = Array.FindIndex(guideFavorites, delegate(GuideFavoriteRoute r) { return r == null; });
            if (slot < 0) { SetStatus("All ten Quick Access favorites are full. Remove one before adding another."); return; }
            guideFavorites[slot] = favorite;
            SaveGuideFavorites();
            RefreshStrip();
            SetStatus(Lang.T(favorite.PageLabel) + " added to the bottom of Quick Access.");
        }

        void EnableGuideCardFavorite(GuideCard card, GuideFavoriteRoute favorite)
        {
            card.CanFavorite = true;
            card.FavoriteClicked += delegate { AddGuideFavorite(favorite); };
        }

        bool MoveGuideFavoriteTo(int source, int target)
        {
            CompactGuideFavorites();
            int count = 0;
            while (count < guideFavorites.Length && guideFavorites[count] != null) count++;
            if (source < 0 || source >= count || count < 2) return false;
            if (target >= count) target = count - 1;
            if (target < 0) target = 0;
            if (source == target) return false;
            GuideFavoriteRoute route = guideFavorites[source];
            if (source < target)
                for (int i = source; i < target; i++) guideFavorites[i] = guideFavorites[i + 1];
            else
                for (int i = source; i > target; i--) guideFavorites[i] = guideFavorites[i - 1];
            guideFavorites[target] = route;
            RefreshStrip();
            return true;
        }

        void RemoveGuideFavorite(int slot)
        {
            if (slot < 0 || slot >= guideFavorites.Length || guideFavorites[slot] == null) return;
            string removed = Lang.T(guideFavorites[slot].PageLabel);
            guideFavorites[slot] = null;
            CompactGuideFavorites();
            SaveGuideFavorites();
            RefreshStrip();
            SetStatus("Removed " + removed + " from Quick Access. Hover its path and click the heart to add it again.");
        }

        // the tabs of a group: the topics' indexes in Data.Guide (-2 for the Talent Tree Builder), in the group's order
        static List<int> GroupTabs(int group, List<GuideTopic> topics)
        {
            var list = new List<int>();
            for (int i = 1; i < GuideGroups[group].Length; i++)
            {
                if (GuideGroups[group][i] == TalentBuilder) { list.Add(-2); continue; }
                if (GuideGroups[group][i] == GearPlanner) { list.Add(-3); continue; }
                for (int t = 0; t < topics.Count; t++)
                    if (string.Equals(topics[t].Title, GuideGroups[group][i], StringComparison.OrdinalIgnoreCase)) { list.Add(t); break; }
            }
            if (group == 1) list.Add(-4);                     // Gameplay › Theorycrafting: the theorycrafting tools live in Gameplay
            if (group == 0)                                   // topics no group names
                for (int t = 0; t < topics.Count; t++)
                {
                    bool named = false;
                    foreach (var grp in GuideGroups)
                        for (int i = 1; i < grp.Length; i++) if (string.Equals(grp[i], topics[t].Title, StringComparison.OrdinalIgnoreCase)) named = true;
                    if (!named) list.Add(t);
                }
            return list;
        }

        void OpenGuideTab(int tab)
        {
            if (tab == -4) { GuideGroupPage(2); return; }    // Theorycrafting (inside Gameplay)
            if (gPlanner && tab != -3) ResetPlannerState();
            guideQuery = "";
            guideGroupPage = -1;
            if (tab == -2) { gVideo = null; gTalents = null; gBuilder = true; gPlanner = false; guideMain = false; guideGroup = 2; if (current == "Guide") BuildPanel("Guide"); }
            else if (tab == -3) { gVideo = null; gTalents = null; gBuilder = false; gPlanner = true; guideMain = false; guideGroup = 2; if (current == "Guide") BuildPanel("Guide"); }
            else SetGuideView(tab, -1);
        }

        // Keep the search box available after following a result so another search can start from that page.
        void GuideHeader(string title, List<GuideTopic> topics)
        {
            ClearPanel(title);
            guideTabsBottom = 8;
            int sh = (int)(32 * Theme.Zoom);
            var search = new GuideSearchBox(PW - 40, sh);
            search.Text = guideQuery;
            Put(search, 20, 8);
            // A click on a suggestion goes there; Enter (or the last line) shows every page the words are found in.
            SearchSuggestions(search, 20, 8 + sh);
            if (guideFocusSearch) { guideFocusSearch = false; search.Focus(); search.SelectionStart = search.TextLength; }
            guideSearch = search;
            guideTabsBottom = 8 + sh + 8;
        }

        // one line of the search box's suggestions
        class GuideHint
        {
            public string Text;
            public Action Go;
            public bool SearchAll;
            public override string ToString() { return Text; }
        }

        void SearchSuggestions(GuideSearchBox search, int x, int y)
        {
            var drop = new ListBox();
            drop.BorderStyle = BorderStyle.FixedSingle;
            drop.BackColor = Theme.Hex("#16151e");
            drop.ForeColor = Theme.Hex("#f5e9c8");
            drop.Font = Theme.F(10);
            drop.IntegralHeight = false;
            drop.Visible = false;
            drop.Width = search.Width;
            Put(drop, x, y);
            drop.BringToFront();
            Action showAll = delegate
            {
                guideQuery = search.Text;
                if (guideQuery.Trim().Length == 0) return;
                guideFocusSearch = true;
                gBuilder = false; gPlanner = false;
                SetGuideView(-1, -1);                         // every page the words are found in
            };
            Action pick = delegate
            {
                var h = drop.SelectedItem as GuideHint;
                if (h != null)
                {
                    if (!h.SearchAll) guideQuery = "";
                    WowButton.Fire(h.Go);
                }
            };
            drop.MouseClick += delegate { pick(); };
            drop.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; pick(); }
                else if (e.KeyCode == Keys.Escape) { drop.Visible = false; search.Focus(); }
            };
            search.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down && drop.Visible && drop.Items.Count > 0) { e.SuppressKeyPress = true; drop.Focus(); drop.SelectedIndex = 0; }
                else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; WowButton.Fire(showAll); }
                else if (e.KeyCode == Keys.Escape) drop.Visible = false;
            };
            search.TextChanged += delegate
            {
                string q = search.Text.Trim();
                if (q.Length == 0 && guideQuery.Trim().Length > 0 && !guideMain)
                {
                    guideQuery = "";
                    guideFocusSearch = true;
                    SetGuideView(guideLastTopic, -1);
                    return;
                }
                drop.BeginUpdate();
                drop.Items.Clear();
                if (q.Length > 0)
                {
                    var topics = Data.Guide;
                    string[] words = q.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Func<string, bool> has = delegate(string title)
                    {
                        string hay = Lang.T(title).ToLowerInvariant() + " " + title.ToLowerInvariant();
                        foreach (string w in words) if (!hay.Contains(w)) return false;
                        return true;
                    };
                    for (int t = 0; t < topics.Count && drop.Items.Count < 6; t++)
                    {
                        int ti = t;
                        if (has(topics[t].Title)) drop.Items.Add(new GuideHint { Text = Lang.T(topics[t].Title), Go = delegate { OpenGuideTab(ti); } });
                        for (int p = 0; p < topics[t].Pages.Count && drop.Items.Count < 6; p++)
                        {
                            int pi = p;
                            if (has(topics[t].Pages[p].Title))
                                drop.Items.Add(new GuideHint { Text = Lang.T(topics[t].Pages[p].Title) + "   ·   " + Lang.T(topics[t].Title), Go = delegate { SetGuideView(ti, pi); } });
                        }
                    }
                    foreach (var l in GuideLinks.Matching(q, 12 - drop.Items.Count))
                    {
                        GuideLink link = l;
                        string where = l.Topic >= 0 && l.Topic < topics.Count ? Lang.T(topics[l.Topic].Title) : Lang.T("Talents");
                        drop.Items.Add(new GuideHint { Text = l.Term + "   ·   " + where, Go = delegate { if (GuideLinks.Navigate != null) GuideLinks.Navigate(link); } });
                    }
                    drop.Items.Add(new GuideHint { Text = Lang.F("Search every page for \"{0}\"", q), Go = showAll, SearchAll = true });
                }
                drop.EndUpdate();
                int rows = Math.Min(drop.Items.Count, 10);
                drop.Height = rows * drop.ItemHeight + 4;
                drop.Visible = rows > 0;
                if (drop.Visible) drop.BringToFront();
            };
        }

        static string GroupName(int g)
        {
            string n = GuideGroups[g][0].ToLowerInvariant();
            return char.ToUpperInvariant(n[0]) + n.Substring(1).Replace("player", "Player");
        }

        void GuideMainPage() { GuideGroupPage(-1); }

        // Bind each Guide destination to the matching saved route. Section and group entry points
        // are also made available by the shared breadcrumb binder.
        void SetGuideTrail(List<object> trail, params GuideFavoriteRoute[] routes)
        {
            SetCrumbs(trail.ToArray());
            int count = trail.Count / 2;
            for (int i = 1; i < count && i < routes.Length; i++)
            {
                GuideFavoriteRoute route = routes[i];
                if (route != null)
                {
                    int step = i;
                    sideHead.SetCrumbFavorite(step, delegate { AddGuideFavorite(route); });
                }
            }
        }

        static string GuideClassKey(string topic, string page)
        {
            if (!string.Equals(topic, "Classes", StringComparison.OrdinalIgnoreCase)) return "";
            foreach (string cls in BuilderClasses)
                if (string.Equals(cls, page, StringComparison.OrdinalIgnoreCase)) return cls;
            return "";
        }

        // the main page, or one group's page (Guide › Gameplay › Theorycrafting): its topics and tools as cards
        public void GuideGroupPage(int group)
        {
            if (gPlanner) ResetPlannerState();
            gTopic = -1; gPage = -1; guideQuery = ""; gBuilder = false; gPlanner = false; gTalents = null; gVideo = null; guideMain = true;
            guideGroupPage = group;
            if (current != "Guide" || !side.Visible) ShowSection("Guide");
            BuildPanel("Guide");
        }

        // the trail in the title bar: Guide › Gameplay › Classes › Priest, each step a way back. Theorycrafting lives in
        // Gameplay: Guide › Gameplay › Theorycrafting › Talent Tree Builder › Priest
        void GuideTrail()
        {
            var topics = Data.Guide;
            Action home = GuideMainPage;
            Action builder = delegate { OpenGuideTab(-2); };    // the class question again
            Action theory = delegate { GuideGroupPage(2); };
            Action gameplay = delegate { GuideGroupPage(1); };
            string gp = GroupName(1), th = GroupName(2);
            if (gVideo != null)
            {
                var videoTrail = new List<object> { "Guide", home, gVideo.Title.Length > 0 ? gVideo.Title : "Video", null };
                GuideFavoriteRoute video = null;
                if (gTopic >= 0 && gTopic < topics.Count)
                {
                    string page = gPage >= 0 && gPage < topics[gTopic].Pages.Count ? topics[gTopic].Pages[gPage].Title : "";
                    video = new GuideFavoriteRoute("video", topics[gTopic].Title, page, "", gVideo.Url);
                }
                SetGuideTrail(videoTrail, null, video);
                return;
            }
            if (gTalents != null && gBuilder)
            {
                var builderClassTrail = new List<object> { "Guide", home, gp, gameplay, th, theory, TalentBuilder, builder, gTalents, null };
                SetGuideTrail(builderClassTrail, null, null, new GuideFavoriteRoute("group", th, ""), new GuideFavoriteRoute("builder", "", ""),
                    new GuideFavoriteRoute("builderClass", "", "", gTalents, ""));
                return;
            }
            if (gBuilder)
            {
                var builderTrail = new List<object> { "Guide", home, gp, gameplay, th, theory, TalentBuilder, null };
                SetGuideTrail(builderTrail, null, null, new GuideFavoriteRoute("group", th, ""), new GuideFavoriteRoute("builder", "", ""));
                return;
            }
            Action planner = delegate { ResetPlannerState(); plannerClass = null; OpenGuideTab(-3); };
            if (gPlanner && plannerClass != null)
            {
                var plannerClassTrail = new List<object> { "Guide", home, gp, gameplay, th, theory, GearPlanner, planner, plannerClass, null };
                SetGuideTrail(plannerClassTrail, null, null, new GuideFavoriteRoute("group", th, ""), new GuideFavoriteRoute("planner", "", ""),
                    new GuideFavoriteRoute("plannerClass", "", "", plannerClass, ""));
                return;
            }
            if (gPlanner)
            {
                var plannerTrail = new List<object> { "Guide", home, gp, gameplay, th, theory, GearPlanner, null };
                SetGuideTrail(plannerTrail, null, null, new GuideFavoriteRoute("group", th, ""), new GuideFavoriteRoute("planner", "", ""));
                return;
            }
            if (guideQuery.Trim().Length > 0) { SetCrumbs("Guide", home, "Search", null); return; }
            if ((gTopic < 0 || gTopic >= topics.Count) && guideGroupPage == 2)
            {
                SetGuideTrail(new List<object> { "Guide", home, gp, gameplay, th, null }, null, null, new GuideFavoriteRoute("group", th, ""));
                return;
            }
            if ((gTopic < 0 || gTopic >= topics.Count) && guideGroupPage >= 0) { SetCrumbs("Guide", home, GroupName(guideGroupPage), null); return; }
            if (gTopic < 0 || gTopic >= topics.Count) { SetCrumbs("Guide", null); return; }
            int t = gTopic, pg = gPage;
            GuideTopic topic = topics[t];
            int gi = GroupOf(topic.Title);
            var trail = new List<object> { "Guide", (Action)GuideMainPage };
            if (gi == 2) { trail.Add(gp); trail.Add(gameplay); }      // a Theorycrafting topic: inside Gameplay
            trail.Add(GroupName(gi)); trail.Add((Action)delegate { GuideGroupPage(gi); });   // the group's step opens its page
            Action toTopic = delegate { SetGuideView(t, -1); };
            bool onPage = pg >= 0 && pg < topic.Pages.Count;
            if (gTalents != null)
            {
                trail.Add(topic.Title); trail.Add(toTopic);
                if (onPage) { trail.Add(topic.Pages[pg].Title); trail.Add((Action)delegate { SetGuideView(t, pg); }); }
                trail.Add("Talents"); trail.Add(null);
            }
            else if (onPage) { trail.Add(topic.Title); trail.Add(toTopic); trail.Add(topic.Pages[pg].Title); trail.Add(null); }
            else { trail.Add(topic.Title); trail.Add(null); }
            var routes = new List<GuideFavoriteRoute>();
            for (int i = 0; i < trail.Count / 2; i++) routes.Add(null);
            int topicStep = gi == 2 ? 3 : 2;
            if (gi == 2) routes[2] = new GuideFavoriteRoute("group", th, "");
            routes[topicStep] = new GuideFavoriteRoute("guide", topic.Title, "");
            if (onPage)
            {
                string page = topic.Pages[pg].Title;
                string classKey = GuideClassKey(topic.Title, page);
                routes[topicStep + 1] = new GuideFavoriteRoute("guide", topic.Title, page, classKey, "");
            }
            if (gTalents != null && onPage)
                routes[topicStep + 2] = new GuideFavoriteRoute("guideTalents", topic.Title, topic.Pages[pg].Title, GuideClassKey(topic.Title, topic.Pages[pg].Title), "");
            SetGuideTrail(trail, routes.ToArray());
        }

        static readonly string[] BuilderClasses = { "Warrior", "Paladin", "Hunter", "Rogue", "Priest", "Shaman", "Mage", "Warlock", "Druid" };

        // ---- the Talent Tree Builder: pick a class, then try out a build on its three trees (empty again each time it opens)
        void GuideBuilderHome()
        {
            GuideHeader(TalentBuilder, Data.Guide);
            ClassPrompt(delegate(string cls) { PlannerClass(cls); gTalents = cls; BuildPanel("Guide"); });
        }

        // the question both theorycrafting tools open with, in the middle of the window: which class to build for
        void ClassPrompt(Action<string> pick)
        {
            const int cols = 3;
            int gap = (int)(8 * Theme.Zoom), bw = 150, bh = 36;
            int cw = (int)(bw * Theme.Zoom), ch = (int)(bh * Theme.Zoom);
            var q = UI.Text("What class do you want to build for?", 13, FontStyle.Bold, Theme.Gold, Theme.Panel, 0);
            q.Size = VStack.Measure(q);
            int boxW = Math.Max(q.Width, cols * cw + (cols - 1) * gap) + 40;
            int rows = (BuilderClasses.Length + cols - 1) / cols;
            int boxH = 20 + q.Height + 14 + rows * ch + (rows - 1) * gap + 20;
            int top = GuideTop, avail = PH - 8 - top;
            var box = new Bordered(Theme.Hex("#c49a48"), 2);
            box.Bounds = new Rectangle((PW - boxW) / 2, top + Math.Max(0, (avail - boxH) / 2), boxW, boxH);
            var inner = new Panel();
            inner.Dock = DockStyle.Fill;
            inner.BackColor = Theme.Panel;
            box.Controls.Add(inner);
            q.Location = new Point((boxW - 4 - q.Width) / 2, 18);
            inner.Controls.Add(q);
            int x0 = (boxW - 4 - (cols * cw + (cols - 1) * gap)) / 2, y0 = q.Bottom + 14;
            for (int i = 0; i < BuilderClasses.Length; i++)
            {
                string cls = BuilderClasses[i];
                var b = new WowButton(cls.ToUpperInvariant(), delegate { pick(cls); }, bw, bh, 10);
                b.Location = new Point(x0 + (i % cols) * (cw + gap), y0 + (i / cols) * (ch + gap));
                inner.Controls.Add(b);
            }
            // behind it, the launcher's own picture (its starry mountains), not a black page
            var back = new Panel();
            back.Bounds = new Rectangle(20, top, PW - 40, avail);
            back.BackgroundImage = PromptBackdrop();
            back.BackgroundImageLayout = ImageLayout.Stretch;
            box.Location = new Point(box.Left - back.Left, box.Top - back.Top);
            back.Controls.Add(box);
            panel.Controls.Add(back);
            back.BringToFront();
        }

        static Image promptBackdrop;

        // the part of the launcher's background that lies behind the section window
        static Image PromptBackdrop()
        {
            if (promptBackdrop != null) return promptBackdrop;
            Image bg = Retro.Get("background");
            if (bg == null) return null;
            var area = new Rectangle(6, 198, 674, 376);     // the section window (SideRect), in the launcher's 1100 x 690
            var bmp = new Bitmap(area.Width, area.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float sx = bg.Width / 1100f, sy = bg.Height / 690f;
                g.DrawImage(bg, new Rectangle(0, 0, area.Width, area.Height),
                            new RectangleF(area.X * sx, area.Y * sy, area.Width * sx, area.Height * sy), GraphicsUnit.Pixel);
            }
            promptBackdrop = bmp;
            return bmp;
        }

        GuideSearchBox guideSearch;

        void SetGuideView(int topic, int page)
        {
            if (topic <= -2) { if (current != "Guide" || !side.Visible) ShowSection("Guide"); OpenGuideTab(topic); return; }   // a tool's link
            if (gPlanner) ResetPlannerState();
            guideGroupPage = -1;
            gVideo = null;
            gTalents = null;
            gBuilder = false; gPlanner = false;
            guideMain = false;
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
            PanelGuideView();
            GuideTrail();
            GuideTrailMenus();
            RefreshStrip();
        }

        // a link to a spell: its row on the page, scrolled to near the top and lit up
        void ShowSpellRow(string term)
        {
            if (current != "Guide") return;
            string want = Regex.Replace(term ?? "", @"\s*\(.*\)$", "");
            SpellRow row = null;
            var stack = new Stack<Control>();
            stack.Push(panel);
            while (stack.Count > 0 && row == null)
            {
                Control c = stack.Pop();
                var r = c as SpellRow;
                if (r != null && string.Equals(r.Spell, want, StringComparison.OrdinalIgnoreCase)) { row = r; break; }
                foreach (Control k in c.Controls) stack.Push(k);
            }
            if (row == null) return;
            var sp = row.Parent as ScrollPanel;
            if (sp != null) sp.ScrollTo(row.Top - sp.AutoScrollPosition.Y - 12);
            row.Flash();
        }

        // the drop-downs of the trail: the groups, a group's topics and tools, a topic's pages, the classes
        void GuideTrailMenus()
        {
            var topics = Data.Guide;
            Func<List<KeyValuePair<string, Action>>> groups = delegate
            {
                var l = new List<KeyValuePair<string, Action>>();
                for (int g = 0; g < 2; g++) { int gg = g; l.Add(new KeyValuePair<string, Action>(GroupName(g), delegate { GuideGroupPage(gg); })); }   // (Theorycrafting is in Gameplay)
                return l;
            };
            Func<int, List<KeyValuePair<string, Action>>> tabsOf = delegate(int g)
            {
                var l = new List<KeyValuePair<string, Action>>();
                foreach (int tab in GroupTabs(g, topics))
                {
                    int tt = tab;
                    string name = tab == -2 ? TalentBuilder : tab == -3 ? GearPlanner : tab == -4 ? GroupName(2) : topics[tab].Title;
                    l.Add(new KeyValuePair<string, Action>(name, delegate { if (tt < 0) OpenGuideTab(tt); else SetGuideView(tt, -1); }));
                }
                return l;
            };
            Func<Action<string>, List<KeyValuePair<string, Action>>> classes = delegate(Action<string> pick)
            {
                var l = new List<KeyValuePair<string, Action>>();
                foreach (string c in BuilderClasses) { string cc = c; l.Add(new KeyValuePair<string, Action>(c, delegate { pick(cc); })); }
                return l;
            };
            if (gVideo != null || guideQuery.Trim().Length > 0) return;
            if (gBuilder || gPlanner)                         // Guide › Gameplay › Theorycrafting › tool › class
            {
                SetCrumbMenu(1, groups());
                SetCrumbMenu(2, tabsOf(1));
                SetCrumbMenu(3, tabsOf(2));
                if (gBuilder && gTalents != null)
                    SetCrumbMenu(4, classes(delegate(string c) { PlannerClass(c); gTalents = c; BuildPanel("Guide"); }));
                if (gPlanner && plannerClass != null)
                    SetCrumbMenu(4, classes(delegate(string c) { PlannerClass(c); BuildPanel("Guide"); }));
                return;
            }
            if (gTopic < 0 || gTopic >= topics.Count)
            {
                if (guideGroupPage >= 0) SetCrumbMenu(1, groups());
                if (guideGroupPage == 2) SetCrumbMenu(2, tabsOf(1));
                return;
            }
            int t = gTopic;
            int gt = GroupOf(topics[t].Title), at = 2;         // at: the step of the topic's group's list
            SetCrumbMenu(1, groups());
            if (gt == 2) { SetCrumbMenu(2, tabsOf(1)); at = 3; }   // a Theorycrafting topic: Gameplay's list, then Theorycrafting's
            SetCrumbMenu(at, tabsOf(gt));
            if (gPage >= 0 && topics[t].Pages.Count > 0)
            {
                var l = new List<KeyValuePair<string, Action>>();
                for (int p = 0; p < topics[t].Pages.Count; p++) { int pp = p; l.Add(new KeyValuePair<string, Action>(topics[t].Pages[p].Title, delegate { SetGuideView(t, pp); })); }
                SetCrumbMenu(at + 1, l);
            }
            if (gTalents != null)                            // › Talents ▾ : another class's talent trees
            {
                var l = new List<KeyValuePair<string, Action>>();
                for (int p = 0; p < topics[t].Pages.Count; p++)
                {
                    int pp = p;
                    string cls = topics[t].Pages[p].Title;
                    if (Talents.Of(cls).Count == 0) continue;
                    l.Add(new KeyValuePair<string, Action>(cls, delegate { SetGuideView(t, pp); gTalents = cls; BuildPanel("Guide"); }));
                }
                SetCrumbMenu(gPage >= 0 ? at + 2 : at + 1, l);
            }
        }

        void PanelGuideView()
        {
            if (VideoCard.Embed == null) VideoCard.Embed = delegate(GuideVideo v) { gVideo = v; BuildPanel("Guide"); return true; };
            if (Cfg.Data.ContainsKey("guide_read")) { Cfg.Remove("guide_read"); if (!PreviewMode) Cfg.Save(); }   // no longer tracked
            if (gVideo != null) { GuideVideoPlayer(gVideo); return; }
            if (gTalents != null && Talents.Of(gTalents).Count > 0) { GuideTalents(gTalents); return; }
            if (gBuilder) { GuideBuilderHome(); return; }
            if (gPlanner) { GuideGearPlanner(); return; }
            GuideLinks.Reset();                              // the game's tooltips AZS Stats has seen since the last time
            var topics = Data.Guide;
            guideTabsBottom = 0;
            if (gTopic >= topics.Count) { gTopic = -1; gPage = -1; }
            if (gTopic < 0 && guideQuery.Trim().Length == 0 && topics.Count > 0 && !guideMain) gTopic = Math.Min(guideLastTopic, topics.Count - 1);
            if (gTopic < 0) { GuideHome(topics); return; }
            guideLastTopic = gTopic;
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
            WebBrowser web = null;
            // FULLSCREEN: the launcher's own button (it does not rely on the page's), the video over the whole screen at
            // the same point. The guide's own videos (hosted with the launcher's releases) have no OPEN IN BROWSER.
            // no FULLSCREEN button of the launcher's own (Slimjim, 2026-10-28): the player's own fullscreen button does it
            if (!VideoView.OwnHosted(url))
                Put(new WowButton("OPEN IN BROWSER ›", delegate { Util.OpenInShell(url); }, 190, 32, 9), PW - 20 - bw, 8);
            guideTabsBottom = 8 + (int)(32 * Theme.Zoom) + 8;
            var frame = new Bordered(Theme.CardEdge, 1);
            frame.Bounds = new Rectangle(20, GuideTop, PW - 40, PH - 12 - GuideTop);
            panel.Controls.Add(frame);
            web = VideoView.Make(v);
            web.Dock = DockStyle.Fill;
            frame.Controls.Add(web);
        }

        sealed class TalentBuildImportDialog : WowDialog
        {
            readonly TextBox input;
            public string Code { get { return input.Text.Trim(); } }

            public TalentBuildImportDialog(string initial) : base("Talent Tree Builder")
            {
                BackAction = delegate { Done(DialogResult.Cancel); };
                Begin();
                Heading("IMPORT BUILD", null, 24);
                Para("Paste a talent build code below. If one is on your clipboard, it is filled in for you.",
                     10, FontStyle.Regular, Theme.Text, 24, 0, 8, 460);
                input = new TextBox();
                input.Multiline = true;
                input.AcceptsReturn = true;
                input.ScrollBars = ScrollBars.Vertical;
                input.BorderStyle = BorderStyle.FixedSingle;
                input.BackColor = Theme.Hex("#211b2a");
                input.ForeColor = Theme.Gold;
                input.Font = Theme.F(9);
                input.AccessibleName = Lang.T("Talent build code");
                input.Text = initial ?? "";
                input.Size = new Size(430, 88);
                Body.MinWidth = 478;
                Body.Add(input, Align.Fill, 24, 0, 24, 8);
                var buttons = new HRow(10,
                    B("CANCEL", delegate { Done(DialogResult.Cancel); }, 126, 34, 9),
                    B("IMPORT", delegate { Done(DialogResult.OK); }, 126, 34, 9));
                Body.Add(buttons, Align.Center, 12, 2, 12, 14);
                input.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.Control && e.KeyCode == Keys.Enter)
                    {
                        e.Handled = e.SuppressKeyPress = true;
                        Done(DialogResult.OK);
                    }
                };
                Shown += delegate { input.Focus(); };
                Finish();
            }

            void Done(DialogResult result)
            {
                DialogResult = result;
                Close();
            }
        }

        // ---- a class's three talent trees, as in the game, with a test build of up to 51 points (cleared on leaving)
        void GuideTalents(string cls)
        {
            GuideHeader(cls + " Talents", Data.Guide);
            if (liveBuild == null || liveBuildCls != cls) { liveBuild = TalentBuild.Parse("", cls); liveBuildCls = cls; }
            var build = liveBuild;
            var sp = MakeThinScroll(panel, new Rectangle(20, GuideTop, PW - 40, PH - 12 - GuideTop), GuideCardBg, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            Color bg = sp.BackColor;
            int full = sp.ClientSize.Width - 8, x = 4, y = 4;
            // Keep the tree editor at roughly 60% and its build controls at 40%, like the Gear Planner.
            int boxW = Math.Max(220, full * 2 / 5);
            boxW = Math.Min(boxW, Math.Max(220, full - 360));
            int w = full - boxW - 10;
            var left = UI.Text("", 11, FontStyle.Bold, Theme.Gold, bg, w - 140, false);
            left.Bounds = new Rectangle(x, y + 6, w - 200, Theme.F(11, FontStyle.Bold).Height + 4);   // (no Gear Planner button: the path leads there)
            sp.Controls.Add(left);
            var reset = new WowButton("RESET", delegate { build.Reset(); }, 120, 30, 9);
            reset.Location = new Point(x + w - reset.Width, y);
            sp.Controls.Add(reset);
            // Hover-only help uses the same gold-bevel question mark and popup as the global help.
            var help = new HoverHelpButton("Talent Tree Builder",
                "Click a talent to add a point, right-click to take one back. A talent takes no more points than its ranks, and a build no more than 51; the purple ones are the milestones at 10, 20 and 30 points.");
            help.Location = new Point(reset.Left - help.Width - 8, y + (reset.Height - help.Height) / 2);
            sp.Controls.Add(help);
            // Named builds are kept in the launcher's own config, separately for each class.
            var tools = new Bordered(Theme.GoldDim, 1);
            tools.BackColor = bg;
            tools.Bounds = new Rectangle(x + w + 10, y, boxW, sp.ClientSize.Height - y - 8);
            var toolTitle = UI.Text(Lang.T("SAVED BUILDS"), 10, FontStyle.Bold, Theme.Gold, bg, boxW - 20);
            toolTitle.TextAlign = ContentAlignment.MiddleCenter;
            toolTitle.Location = new Point(10, 12);
            tools.Controls.Add(toolTitle);
            var fieldW = tools.ClientSize.Width - 32;
            var fieldX = (tools.ClientSize.Width - fieldW) / 2;
            var buildName = new TextBox();
            buildName.Font = Theme.F(9, FontStyle.Regular);
            buildName.BackColor = Theme.Hex("#211b2a");
            buildName.ForeColor = Theme.Gold;
            buildName.BorderStyle = BorderStyle.FixedSingle;
            buildName.Location = new Point(fieldX, 42);
            buildName.Width = fieldW;
            tools.Controls.Add(buildName);
            var saved = new ComboBox();
            saved.DropDownStyle = ComboBoxStyle.DropDownList;
            saved.Font = Theme.F(9, FontStyle.Regular);
            saved.BackColor = Theme.Hex("#211b2a");
            saved.ForeColor = Theme.Gold;
            saved.Location = new Point(fieldX, 76);
            saved.Width = fieldW;
            tools.Controls.Add(saved);
            // Keep the four actions centred and distribute their gaps through the remaining height,
            // so the last button sits close to the bottom edge like the Gear Planner's controls.
            int actionW = Math.Min(boxW - 32, Math.Max(150, (int)(boxW * 0.62)));
            int actionX = (tools.ClientSize.Width - actionW) / 2;
            int actionH = 28;
            int actionY = saved.Bottom + 10;
            int actionGap = Math.Max(4, (tools.ClientSize.Height - actionY - actionH * 4) / 4);
            Func<Dictionary<string, object>> classBuilds = delegate
            {
                var cfg = Config.Load();
                var all = cfg.Section("talentTreeBuilds");
                var byClass = Json.AsDict(Json.Get(all, cls));
                if (byClass == null) { byClass = new Dictionary<string, object>(); all[cls] = byClass; }
                return byClass;
            };
            Action refreshSaved = delegate
            {
                saved.Items.Clear();
                foreach (var kv in classBuilds())
                    if (kv.Value is string) saved.Items.Add(kv.Key);
                saved.SelectedIndex = -1;
            };
            var saveBuild = new WowButton(Lang.T("SAVE"), delegate
            {
                string name = buildName.Text.Trim();
                if (name.Length == 0) { Info("Talent Tree Builder", "Enter a name for this build."); buildName.Focus(); return; }
                var cfg = Config.Load();
                var all = cfg.Section("talentTreeBuilds");
                var byClass = Json.AsDict(Json.Get(all, cls));
                if (byClass == null) { byClass = new Dictionary<string, object>(); all[cls] = byClass; }
                byClass[name] = build.Save();
                cfg.Save();
                refreshSaved();
                saved.SelectedItem = name;
            }, actionW, actionH, 9);
            saveBuild.Location = new Point(actionX, actionY);
            tools.Controls.Add(saveBuild);
            Action<TalentBuild> applyBuild = delegate(TalentBuild loaded)
            {
                build.Reset();
                foreach (var tr in Talents.Of(cls))
                {
                    var ordered = new List<Talent>(tr.Talents);
                    ordered.Sort(delegate(Talent a, Talent b)
                    {
                        int row = a.Row.CompareTo(b.Row);
                        return row != 0 ? row : a.Col.CompareTo(b.Col);
                    });
                    foreach (var t in ordered)
                        for (int i = 0; i < loaded.Get(t); i++) build.Add(t);
                }
            };
            Action loadSelected = delegate
            {
                if (saved.SelectedItem == null) return;
                object code;
                if (!classBuilds().TryGetValue(Convert.ToString(saved.SelectedItem), out code)) return;
                applyBuild(TalentBuild.Parse(Convert.ToString(code), cls));
            };
            saved.SelectionChangeCommitted += delegate { loadSelected(); };
            var loadBuild = new WowButton(Lang.T("LOAD"), loadSelected, actionW, actionH, 9);
            loadBuild.Location = new Point(actionX, actionY + actionH + actionGap);
            tools.Controls.Add(loadBuild);
            var exportBuild = new WowButton(Lang.T("EXPORT"), delegate
            {
                try { Clipboard.SetText(build.ShareCode(cls)); Info("Talent Tree Builder", "Build code copied to the clipboard."); }
                catch (Exception) { Warn("Talent Tree Builder", "Could not copy the build code."); }
            }, actionW, actionH, 9);
            exportBuild.Location = new Point(actionX, actionY + (actionH + actionGap) * 2);
            tools.Controls.Add(exportBuild);
            var importBuild = new WowButton(Lang.T("IMPORT"), delegate
            {
                string code = "";
                string initial = "";
                try { if (Clipboard.ContainsText()) initial = Clipboard.GetText(); } catch (Exception) { }
                using (var prompt = new TalentBuildImportDialog(initial))
                {
                    if (prompt.ShowModal(panel.FindForm()) != DialogResult.OK) return;
                    code = prompt.Code;
                }
                if (code.Length == 0) return;
                TalentBuild imported;
                string error;
                if (!TalentBuild.TryParseShareCode(code, cls, out imported, out error))
                {
                    Warn("IMPORT BUILD", error);
                    return;
                }
                applyBuild(imported);
            }, actionW, actionH, 9);
            importBuild.Location = new Point(actionX, actionY + (actionH + actionGap) * 3);
            tools.Controls.Add(importBuild);
            Action layoutActions = delegate
            {
                int actionSpacing = Math.Max(4, (tools.ClientSize.Height - actionY - actionH * 4) / 4);
                saveBuild.Location = new Point((tools.ClientSize.Width - saveBuild.Width) / 2, actionY);
                loadBuild.Location = new Point((tools.ClientSize.Width - loadBuild.Width) / 2, actionY + actionH + actionSpacing);
                exportBuild.Location = new Point((tools.ClientSize.Width - exportBuild.Width) / 2, actionY + (actionH + actionSpacing) * 2);
                importBuild.Location = new Point((tools.ClientSize.Width - importBuild.Width) / 2, actionY + (actionH + actionSpacing) * 3);
            };
            tools.Resize += delegate { layoutActions(); };
            layoutActions();
            sp.Controls.Add(tools);
            refreshSaved();
            y += Math.Max(reset.Height, left.Height) + 6;
            var trees = Talents.Of(cls);
            var views = new List<TalentTreeView>();
            // the three trees fit the box: their cells as big as its width and height allow, the trees centred
            int gap = 8, tw3 = (w - gap * (trees.Count - 1)) / Math.Max(1, trees.Count), th = 0;
            int roomH = sp.ClientSize.Height - y - 8;
            int cellFit = Math.Max(22, Math.Min(Math.Max(30, (tw3 - 16) / 4), (roomH - 38) / 7));   // as TalentTreeView works it out
            int realW = Math.Min(tw3, cellFit * 4 + 16);
            int x0 = x;                                     // the trees start at the left (they were centred)
            for (int i = 0; i < trees.Count; i++)
            {
                var tv = new TalentTreeView(trees[i], tw3, roomH);
                tv.Build = build;
                tv.Location = new Point(x0 + i * (realW + gap), y);
                sp.Controls.Add(tv);
                views.Add(tv);
                th = Math.Max(th, tv.Height);
            }
            Action show = delegate
            {
                var spent = new List<string>();
                foreach (var tr in trees) spent.Add(build.SpentIn(tr).ToString());
                left.Text = Lang.T("Points left") + ": " + (TalentBuild.MaxPoints - build.Spent) + " / " + TalentBuild.MaxPoints +
                            "     (" + string.Join(" / ", spent.ToArray()) + ")";
            };
            build.Changed = delegate
            {
                show();
                foreach (var v in views) v.Invalidate();
            };
            show();
        }

        // ---- first page: a card for every topic, all open at any time
        string guideQuery = "";

        void GuideHome(List<GuideTopic> topics)
        {
            // the search results, under the tabs (none selected); emptying the search goes back to the last topic
            GuideHeader("Guide", topics);
            var search = guideSearch;
            var sp = MakeThinScroll(panel, new Rectangle(20, GuideTop, PW - 40, PH - 12 - GuideTop), Theme.Panel, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            Action fill = delegate
            {
                sp.SuspendLayout();
                while (sp.Controls.Count > 0) { Control c = sp.Controls[0]; sp.Controls.RemoveAt(0); c.Dispose(); }
                int w = sp.ClientSize.Width - 8, y = 4;
                if (guideQuery.Trim().Length == 0 && guideGroupPage < 0)
                {
                    // The Guide button opens a choice of its two major categories.
                    int gap = (int)(16 * Theme.Zoom), cardH = (int)(230 * Theme.Zoom);
                    int cardW = (w - gap) / 2;
                    string[] descriptions =
                    {
                        Lang.T("Start here: learn the controls, your first run, and the systems that help you get going."),
                        Lang.T("Explore classes, zones, builds, and the systems that shape harder runs.")
                    };
                    for (int g = 0; g < 2; g++)
                    {
                        int group = g;
                        var card = new GuideCard("", GroupName(g), descriptions[g], 0, cardW, cardH);
                        card.Location = new Point(4 + g * (cardW + gap), 18);
                        card.Pressed += delegate { GuideGroupPage(group); };
                        sp.Controls.Add(card);
                    }
                }
                else if (guideQuery.Trim().Length == 0)
                {
                    // A category page lists its topics; Theorycrafting remains a submenu within Gameplay.
                    const int gap = 10;
                    int ch = (int)(108 * Theme.Zoom);
                    for (int g = 0; g < GuideGroups.Length; g++)
                    {
                        if (guideGroupPage >= 0 && g != guideGroupPage) continue;
                        if (guideGroupPage < 0 && g == 2) continue;    // Theorycrafting is a card in Gameplay
                        var tabs = GroupTabs(g, topics);
                        if (tabs.Count == 0) continue;
                        var head = UI.Text(GuideGroups[g][0], 11, FontStyle.Bold, Theme.Gold, sp.BackColor, 0);
                        head.Size = VStack.Measure(head);
                        head.Location = new Point(4, y + (y > 4 ? 6 : 0));
                        sp.Controls.Add(head);
                        y = head.Bottom + 4;
                        GuideGrid(sp, y, w, tabs.Count, delegate(int i, GuideCard c)
                        {
                            int t = tabs[i];
                            if (t == -2)
                            {
                                c.Title = TalentBuilder;
                                c.Desc = Lang.T("Pick a class and try out a build of up to 51 points on its three talent trees.");
                                EnableGuideCardFavorite(c, new GuideFavoriteRoute("builder", "", ""));
                            }
                            else if (t == -3)
                            {
                                c.Title = GearPlanner;
                                c.Desc = Lang.T("Put gear and jewelry on a character sheet like the game's and see what they add.");
                                EnableGuideCardFavorite(c, new GuideFavoriteRoute("planner", "", ""));
                            }
                            else if (t == -4)
                            {
                                c.Title = GroupName(2);
                                c.Desc = Lang.T("The Talent Tree Builder, the Gear Planner and the build guides.");
                                EnableGuideCardFavorite(c, new GuideFavoriteRoute("group", GroupName(2), ""));
                            }
                            else
                            {
                                c.Title = topics[t].Title;
                                c.Desc = topics[t].Desc;
                                c.Videos = CountVideos(topics[t]);
                                c.Pages = topics[t].Pages.Count;
                                // Every non-root topic card can be saved, including categories that open another page list.
                                EnableGuideCardFavorite(c, new GuideFavoriteRoute("guide", topics[t].Title, ""));
                            }
                            c.Pressed += delegate { OpenGuideTab(t); };
                        });
                        y += ((tabs.Count + 1) / 2) * (ch + gap);
                    }
                }
                else GuideResults(sp, w, topics, guideQuery.Trim());
                sp.ResumeLayout();
            };
            search.TextChanged += delegate
            {
                guideQuery = search.Text;
                if (guideQuery.Trim().Length == 0) { guideQuery = ""; guideFocusSearch = true; SetGuideView(guideLastTopic, -1); return; }
                fill();
            };
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
            // the tools too (Talent Tree Builder, Gear Planner): first
            var tools = new[] { new[] { "-2", TalentBuilder, "Pick a class and try out a build of up to 51 points on its three talent trees." },
                                new[] { "-3", GearPlanner, "Put gear and jewelry on a character sheet like the game's and see what they add." } };
            for (int k = tools.Length - 1; k >= 0; k--)
            {
                string all = (tools[k][1] + " " + tools[k][2] + " theorycrafting").ToLowerInvariant();
                bool ok = true;
                foreach (string wd in words) if (all.IndexOf(wd, StringComparison.Ordinal) < 0) { ok = false; break; }
                if (!ok) continue;
                hits.Insert(0, new KeyValuePair<int, int>(int.Parse(tools[k][0]), -1));
                lines.Insert(0, Lang.T(tools[k][2]));
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
                if (h.Key < 0)
                {
                    c.Title = h.Key == -2 ? TalentBuilder : GearPlanner;
                    c.Desc = GroupName(2) + " · " + lines[i];
                    EnableGuideCardFavorite(c, new GuideFavoriteRoute(h.Key == -2 ? "builder" : "planner", "", ""));
                    c.Pressed += delegate { OpenGuideTab(h.Key); };
                    return;
                }
                GuideTopic topic = topics[h.Key];
                c.Title = h.Value < 0 ? topic.Title : topic.Pages[h.Value].Title;
                c.Desc = (h.Value < 0 ? "" : topic.Title + " · ") + lines[i];   // where it is, and the line it was found in
                string page = h.Value < 0 ? "" : topic.Pages[h.Value].Title;
                EnableGuideCardFavorite(c, new GuideFavoriteRoute("guide", topic.Title, page, GuideClassKey(topic.Title, page), ""));
                c.Pressed += delegate { guideQuery = ""; SetGuideView(h.Key, h.Value); };
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
            GuideHeader(topic.Title, Data.Guide);
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
                EnableGuideCardFavorite(c, new GuideFavoriteRoute("guide", topic.Title, topic.Pages[i].Title,
                    GuideClassKey(topic.Title, topic.Pages[i].Title), ""));
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
            GuideHeader(shown.Title, topics);

            int by = PH - 2;                                 // no PREVIOUS / NEXT row: the article has the whole window
            var sp = MakeThinScroll(panel, new Rectangle(20, GuideTop, PW - 40, by - 10 - GuideTop), GuideCardBg, Theme.Bronze,
                                    Theme.GoldDim, Theme.Gold);
            GuideBody(sp, inPage ? topic.Title : "Guide", shown);
            // no NEXT at the end of a page: the path above (hover a step) leads to every other page or topic
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
            // a class's page: its spells with their icons and the talents that change them, its gear, and its talent
            // trees one click away (TALENT TREES at the right of the "CLASSES" line)
            string className = null;
            try
            {
                var tops = Data.Guide;
                if (gTopic >= 0 && gTopic < tops.Count && tops[gTopic].Title == "Classes" && Talents.Of(page.Title).Count > 0)
                    className = page.Title;
            }
            catch (Exception) { }
            if (className != null)
            {
                string tcls = className;
                var tb = new WowButton("TALENT TREES ›", delegate { gTalents = tcls; BuildPanel("Guide"); }, 170, 28, 9);
                tb.Location = new Point(x + tw - tb.Width, y - 4);
                sp.Controls.Add(tb);
                // beside it: the class's gear page (every piece, its bonuses, the bosses and each set's bonuses)
                int it = -1, ip = -1;
                var tops2 = Data.Guide;
                for (int t = 0; t < tops2.Count && ip < 0; t++)
                    if (tops2[t].Title == "Items")
                        for (int p = 0; p < tops2[t].Pages.Count; p++)
                            if (tops2[t].Pages[p].Title == className + " Gear") { it = t; ip = p; break; }
                if (ip >= 0 && Items.Gear.Count > 0)
                {
                    int gt = it, gp = ip;
                    var gb = new WowButton(Lang.F("{0} GEAR", Lang.T(className).ToUpperInvariant()) + " ›", delegate { SetGuideView(gt, gp); }, 170, 28, 9);
                    gb.Location = new Point(tb.Left - gb.Width - 6, y - 4);
                    sp.Controls.Add(gb);
                }
            }
            add(UI.Text(crumb.ToUpperInvariant(), 8.5f, FontStyle.Bold, GuideRed, bg, tw - (className != null ? 260 : 0), crumb == "Guide"), x, 0);
            add(UI.Text(page.Title, 18, FontStyle.Bold, Theme.Gold, bg, tw, false), x, 2);
            if (page.Desc.Length > 0) add(UI.Text(page.Desc, 10.5f, FontStyle.Italic, Theme.GoldDim, bg, tw, false), x, 2);
            add(new GuideRule(tw, Theme.GoldDim), x, 10);

            var para = new List<string>();
            Action flush = delegate
            {
                if (para.Count == 0) return;
                add(new GuideText(string.Join(" ", para.ToArray()), 10, Theme.Text, bg, tw, gTopic, gPage), x, 8);
                para.Clear();
            };
            string section = "";
            // the class's spells and what they say (the game's own text when AZS Stats saw it): for "Spells that affect this"
            var pool = new List<KeyValuePair<string, string>>();
            if (className != null)
            {
                string sec = "";
                foreach (string raw in page.Body.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("# ")) { sec = line.Substring(2).Trim(); continue; }
                    if (!sec.StartsWith("Spell pool") || !line.StartsWith("- ")) continue;
                    Match m = Regex.Match(line.Substring(2).Trim(), @"^(.+?(?: \([^)]*\))?): (?=[a-z0-9])(.+)$");
                    if (!m.Success) continue;
                    string nm = m.Groups[1].Value;
                    string game = GuideLinks.GameTip(Regex.Replace(nm, @"\s*\(.*\)$", ""));
                    pool.Add(new KeyValuePair<string, string>(nm, game ?? m.Groups[2].Value));
                }
            }
            foreach (string raw in page.Body.Split('\n'))
            {
                string line = raw.Trim();
                int vi;
                if (line.Length == 0) { flush(); continue; }
                if (className != null && line.StartsWith("# ")) section = line.Substring(2).Trim();
                if (className != null && section.StartsWith("Talent")) continue;   // the trees: TALENT TREES at the top
                if (className != null && section.StartsWith("Spell pool") && line.StartsWith("- "))
                {
                    flush();
                    Match m = Regex.Match(line.Substring(2).Trim(), @"^(.+?(?: \([^)]*\))?): (?=[a-z0-9])(.+)$");
                    if (m.Success)
                    {
                        string body = m.Groups[2].Value;
                        add(new SpellRow(m.Groups[1].Value, char.ToUpperInvariant(body[0]) + body.Substring(1), className, tw, gTopic, gPage, pool), x, 6);
                        continue;
                    }
                }
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
                    var l = new GuideText(line.Substring(2).Trim(), 10, Theme.Text, bg, tw - 20, gTopic, gPage);
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
            // the class's gear: its button sits beside TALENT TREES at the top of the page
            var pad = new Panel();
            pad.BackColor = bg;
            pad.Bounds = new Rectangle(4, y, 1, 18);
            sp.Controls.Add(pad);
        }
    }

    // one rarity of a class's gear on its class page: the rarity's name, then each piece as a small box (its icon when
    // AZS Stats has seen it, else its slot) in the rarity's colour; the hover shows the item card
    public class GearStrip : Control
    {
        readonly string rarity;
        readonly List<GearPiece> pieces;
        readonly Dictionary<string, string> icons;
        readonly Action<GearPiece> pick;
        readonly List<KeyValuePair<Rectangle, GearPiece>> boxes = new List<KeyValuePair<Rectangle, GearPiece>>();
        GearPiece over;
        readonly int box, labelW;

        public GearStrip(string rarity, List<GearPiece> pieces, int width, Dictionary<string, string> icons, Action<GearPiece> pick)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            this.rarity = rarity;
            this.pieces = pieces;
            this.icons = icons ?? new Dictionary<string, string>();
            this.pick = pick;
            box = Math.Max(26, (int)(40 * Theme.Zoom));
            labelW = (int)(110 * Theme.Zoom);
            int perRow = Math.Max(1, (width - labelW) / (box + 6));
            int rows = (pieces.Count + perRow - 1) / perRow;
            Size = new Size(width, rows * (box + 6));
            for (int i = 0; i < pieces.Count; i++)
                boxes.Add(new KeyValuePair<Rectangle, GearPiece>(
                    new Rectangle(labelW + (i % perRow) * (box + 6), (i / perRow) * (box + 6), box, box), pieces[i]));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Theme.Smooth(g);
            Color rc = GearSheet.RarityColor(rarity);
            using (var b = new SolidBrush(rc)) g.DrawString(Lang.T(rarity), Theme.F(10, FontStyle.Bold), b, 0, (box - Theme.F(10).Height) / 2f);
            foreach (var kv in boxes)
            {
                Rectangle r = kv.Key;
                GearPiece p = kv.Value;
                using (var b = new SolidBrush(Theme.Hex("#1b1a24"))) g.FillRectangle(b, r);
                string ic;
                Image img = icons.TryGetValue(p.Name, out ic) ? SpellIcons.Icon(ic, this) : null;
                if (img != null) Theme.Draw(g, img, Rectangle.Inflate(r, -2, -2));
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(60, rc))) g.FillRectangle(b, Rectangle.Inflate(r, -3, -3));
                    string ab = p.Slot == "Main Hand" ? "MH" : p.Slot == "Off Hand" ? "OH" : p.Slot.Substring(0, Math.Min(2, p.Slot.Length));
                    Font f = Theme.F(8.5f, FontStyle.Bold);
                    SizeF a = g.MeasureString(ab, f);
                    using (var b = new SolidBrush(Theme.Hex("#f5e9c8"))) g.DrawString(ab, f, b, r.X + (r.Width - a.Width) / 2, r.Y + (r.Height - a.Height) / 2);
                }
                using (var pen = new Pen(p == over ? Theme.Hex("#fff2b0") : rc, p == over ? 2f : 1.5f)) g.DrawRectangle(pen, r);
                if (!p.Known)
                    using (var b = new SolidBrush(Theme.Hex("#f5e9c8"))) g.DrawString("*", Theme.F(9, FontStyle.Bold), b, r.Right - 9, r.Y - 1);
            }
        }

        GearPiece At(Point pt)
        {
            foreach (var kv in boxes) if (kv.Key.Contains(pt)) return kv.Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            GearPiece p = At(e.Location);
            if (p == over) return;
            over = p;
            Cursor = p != null ? Cursors.Hand : Cursors.Default;
            Invalidate();
            if (p != null)
            {
                string card = Items.Card(p);
                int nl = card.IndexOf('\n');
                string ic;
                GuideHover.Show(this, p.Name, nl >= 0 ? card.Substring(nl + 1) : card,
                                icons.TryGetValue(p.Name, out ic) ? SpellIcons.Icon(ic, null) : null,
                                SpellIcons.HasItemModel(p.Name) ? p.Name : null);
            }
            else GuideHover.Hide();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (over != null) { over = null; Invalidate(); }
            GuideHover.Hide();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            GearPiece p = At(e.Location);
            if (e.Button == MouseButtons.Left && p != null && pick != null) { GuideHover.Hide(); pick(p); }
        }
    }

    // ------------------------------------------------------------------ the guide's painted pieces
    // a clickable card: number (or a label for the big one), title, description and small badges
    public class GuideCard : Control
    {
        public string Title, Desc, Label;
        public int Number, Videos, Pages;
        public bool CanFavorite;
        public event Action Pressed;
        public event Action FavoriteClicked;
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
            if (e.Button != MouseButtons.Left || !ClientRectangle.Contains(e.Location)) return;
            if (CanFavorite && hover && new Rectangle(Width - 30, 2, 26, 27).Contains(e.Location))
            { WowButton.Fire(FavoriteClicked); return; }
            WowButton.Fire(Pressed);
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
            float y = 12, tw = Width - x - 14 - (CanFavorite ? 28 : 0);
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
            if (CanFavorite && hover)
            {
                var heartBox = new RectangleF(Width - 30, 2, 26, 27);
                using (var path = Round(heartBox, 5))
                using (var b = new SolidBrush(Theme.Hex("#30251a"))) g.FillPath(b, path);
                using (var path = Round(heartBox, 5))
                using (var p = new Pen(Theme.Gold, 1)) g.DrawPath(p, path);
                using (var heartFont = new Font("Segoe UI Symbol", 16 * zoom, FontStyle.Regular, GraphicsUnit.Point))
                    Theme.CenterText(g, "♥", heartFont, Theme.Gold, heartBox, null);
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

        // the guide's own videos: hosted with the launcher's releases (through jsDelivr or GitHub)
        public static bool OwnHosted(string url)
        {
            string u = (url ?? "").ToLowerInvariant();
            return u.Contains("slimmy25/azeroth-survivor") || u.Contains("github.com") || u.Contains("githubusercontent.com");
        }

        // the launcher's FULLSCREEN button: the video over the whole screen, from where it is now
        public static void Fullscreen(WebBrowser web, GuideVideo v)
        {
            double t = 0, vol = 0.15;
            try { if (web != null && web.Document != null) Read(web, ref t, ref vol); } catch (Exception) { }
            Full(web, v, t, vol);
        }

        // the guide's player; fullscreen opens the same video in a window over the whole screen, at the same point
        public static WebBrowser Make(GuideVideo v)
        {
            WebBrowser web = null;
            var bridge = new Bridge();
            bridge.OnToggle = delegate(double t, double vol) { Full(web, v, t, vol); };
            web = Build(v, 0, 0.15, false, bridge);
            return web;
        }

        static void Full(WebBrowser web, GuideVideo v, double t, double vol)
        {
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
                // the way back: the player's own fullscreen button or Esc (no extra button of the launcher's own)
                full.FormClosed += delegate
                {
                    fweb.Dispose();
                    Resume(web, backAt, backVol);
                };
                full.Show();
                full.Activate();
            }
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
            // the player's buttons: the launcher's own control bar, laid out like YouTube's (red progress bar with a dot,
            // play, volume, time, fullscreen on the right, hidden after a moment of no mouse movement), so both video
            // players look alike. The shapes are simple ones drawn here, not YouTube's own artwork. Keys: Space or K play,
            // arrows skip 5 s, M mute, F fullscreen (Esc leaves it). Fullscreen is the launcher's window (window.external).
            string fsPath = fullscreen ? "M14 14h-4v2h6v-6h-2zM22 14v-4h-2v6h6v-2zM20 26h2v-4h4v-2h-6zM10 22h4v4h2v-6h-6z"
                                       : "M10 16h2v-4h4v-2h-6zM24 10h-4v2h4v4h2v-6zM24 24h-4v2h6v-6h-2zM12 20h-2v6h6v-2h-4z";
            string page = (@"<!DOCTYPE html>
<!-- saved from url=(0014)about:internet -->
<html><head><meta http-equiv=""X-UA-Compatible"" content=""IE=edge""><meta charset=""utf-8""><style>
html,body{margin:0;height:100%;background:__BG__;overflow:hidden;font-family:Roboto,Arial,sans-serif;color:#fff}
#w{position:absolute;left:0;top:0;right:0;bottom:0;background:#000;cursor:default}
video{width:100%;height:100%;background:#000;display:block}
#bar{position:absolute;left:0;right:0;bottom:0;height:64px;background:linear-gradient(to top,rgba(0,0,0,.75),rgba(0,0,0,0));transition:opacity .25s;opacity:1}
#w.idle #bar{opacity:0}#w.idle{cursor:none}
#prog{position:absolute;left:12px;right:12px;bottom:44px;height:14px;cursor:pointer}
#track{position:absolute;left:0;right:0;top:5px;height:3px;background:rgba(255,255,255,.2);transition:height .1s,top .1s}
#prog:hover #track{top:4px;height:5px}
#buf{position:absolute;left:0;top:0;bottom:0;width:0;background:rgba(255,255,255,.4)}
#played{position:absolute;left:0;top:0;bottom:0;width:0;background:#f00}
#dot{position:absolute;top:1px;width:13px;height:13px;margin-left:-6px;border-radius:50%;background:#f00;transform:scale(0);transition:transform .1s;left:0}
#prog:hover #dot{transform:scale(1)}
#ctl{position:absolute;left:4px;right:4px;bottom:0;height:40px}
.b{position:absolute;top:0;width:40px;height:40px;cursor:pointer;opacity:.9}.b:hover{opacity:1}
.b svg{width:40px;height:40px;fill:#fff}
#play{left:0}#mute{left:40px}
#vol{position:absolute;left:80px;top:18px;width:0;height:4px;overflow:hidden;transition:width .2s;cursor:pointer}
#volarea:hover #vol,#vol.on{width:56px}
#volarea{position:absolute;left:40px;top:0;height:40px;width:40px}#volarea:hover{width:100px}
#volbg{position:absolute;left:0;top:0;width:52px;height:4px;background:rgba(255,255,255,.2)}
#vollv{position:absolute;left:0;top:0;height:4px;background:#fff}
#time{position:absolute;left:96px;top:0;line-height:40px;font-size:13px;color:#ddd;white-space:nowrap;transition:left .2s}
#volarea:hover~#time{left:150px}
#fs{right:0}
#err{display:none;position:absolute;left:0;right:0;top:40%;text-align:center;font-size:15px;color:#d7cea4;font-family:Georgia,serif}
</style></head><body><div id=""w"">
<video id=""v"" autoplay preload=""auto""__POSTER__ src=""__SRC__""></video>
<div id=""bar""><div id=""prog""><div id=""track""><div id=""buf""></div><div id=""played""></div></div><div id=""dot""></div></div>
<div id=""ctl""><div class=""b"" id=""play"" title=""__T_PLAY__""><svg viewBox=""0 0 36 36""><path id=""pp"" d=""M12 9v18l14-9z""/></svg></div>
<div id=""volarea""><div class=""b"" id=""mute"" style=""left:0"" title=""__T_MUTE__""><svg viewBox=""0 0 36 36""><path d=""M8 15v6h5l6 5V10l-6 5z""/><path id=""wave"" d=""M22 13.5a6 6 0 0 1 0 9v-2.3a3.8 3.8 0 0 0 0-4.4z M22 9.5a10 10 0 0 1 0 17v-2.2a8 8 0 0 0 0-12.6z""/><path id=""cross"" style=""display:none"" d=""M21 14.5l1.4-1.4 2.6 2.6 2.6-2.6 1.4 1.4-2.6 2.6 2.6 2.6-1.4 1.4-2.6-2.6-2.6 2.6-1.4-1.4 2.6-2.6z""/></svg></div>
<div id=""vol"" style=""left:40px""><div id=""volbg""></div><div id=""vollv""></div></div></div>
<div id=""time"">0:00 / 0:00</div>
<div class=""b"" id=""fs"" title=""__FSLABEL__""><svg viewBox=""0 0 36 36""><path d=""__FSPATH__""/></svg></div></div></div>
<div id=""err"">__ERR__</div></div>
<script>
var v=document.getElementById('v'),w=document.getElementById('w'),at=__AT__,full=__FULL__;
function $(i){return document.getElementById(i);}
v.volume=__VOL__;
function fmt(s){if(!isFinite(s)||s<0)s=0;s=Math.floor(s);var h=Math.floor(s/3600),m=Math.floor(s%3600/60),x=s%60;return (h>0?h+':'+(m<10?'0':''):'')+m+':'+(x<10?'0':'')+x;}
function draw(){var d=v.duration||0,p=d>0?v.currentTime/d:0;$('played').style.width=(p*100)+'%';$('dot').style.left=(p*100)+'%';
 try{if(v.buffered.length&&d>0)$('buf').style.width=(v.buffered.end(v.buffered.length-1)/d*100)+'%';}catch(e){}
 $('time').innerHTML=fmt(v.currentTime)+' / '+fmt(d);
 $('pp').setAttribute('d',v.paused?'M12 9v18l14-9z':'M11 9h5v18h-5z M20 9h5v18h-5z');
 var mu=v.muted||v.volume==0;$('wave').style.display=mu?'none':'';$('cross').style.display=mu?'':'none';
 $('vollv').style.width=(mu?0:v.volume*52)+'px';}
function play(){if(v.paused)v.play();else v.pause();draw();}
function toggle(){try{window.external.Toggle(v.currentTime,v.volume);}catch(e){}}
v.addEventListener('loadedmetadata',function(){v.volume=__VOL__;if(at>0){try{v.currentTime=at;}catch(e){}}draw();});
v.addEventListener('timeupdate',draw);v.addEventListener('play',draw);v.addEventListener('pause',function(){draw();wake();});
v.addEventListener('volumechange',draw);v.addEventListener('progress',draw);
v.addEventListener('click',play);
$('play').onclick=play;
$('mute').onclick=function(){v.muted=!v.muted;if(!v.muted&&v.volume==0)v.volume=0.15;draw();};
function seekAt(e,el,f){var r=el.getBoundingClientRect();var p=Math.max(0,Math.min(1,(e.clientX-r.left)/r.width));f(p);}
var dragging=null;
$('prog').onmousedown=function(e){dragging='prog';seekAt(e,$('prog'),function(p){if(v.duration)v.currentTime=p*v.duration;});draw();return false;};
$('vol').onmousedown=function(e){dragging='vol';seekAt(e,$('volbg'),function(p){v.muted=false;v.volume=p;});draw();return false;};
document.onmousemove=function(e){e=e||window.event;wake();if(dragging=='prog')seekAt(e,$('prog'),function(p){if(v.duration)v.currentTime=p*v.duration;});
 else if(dragging=='vol')seekAt(e,$('volbg'),function(p){v.muted=false;v.volume=p;});};
document.onmouseup=function(){dragging=null;};
$('fs').onclick=toggle;
v.ondblclick=function(e){toggle();if(e&&e.preventDefault)e.preventDefault();return false;};
var idle=null;function wake(){w.className='';if(idle)clearTimeout(idle);idle=setTimeout(function(){if(!v.paused&&!dragging)w.className='idle';},2500);}
w.onmouseleave=function(){if(!v.paused)w.className='idle';};
document.onkeydown=function(e){e=e||window.event;var k=e.keyCode;
 if((k==27&&full)||k==70){toggle();return false;}
 if(k==32||k==75){play();return false;}
 if(k==37){v.currentTime=Math.max(0,v.currentTime-5);return false;}
 if(k==39){v.currentTime=Math.min(v.duration||0,v.currentTime+5);return false;}
 if(k==77){v.muted=!v.muted;return false;}};
document.addEventListener('MSFullscreenChange',function(){if(document.msFullscreenElement){try{document.msExitFullscreen();}catch(x){}toggle();}});
v.onerror=function(){v.style.display='none';$('bar').style.display='none';$('err').style.display='block';};
draw();wake();
</script></body></html>")
                .Replace("__BG__", fullscreen ? "#000" : "#0e0d14")
                .Replace("__POSTER__", poster.Length > 0 && start <= 0 ? " poster=\"" + Html(poster) + "\"" : "")
                .Replace("__SRC__", Html(v.Url))
                .Replace("__T_PLAY__", Html(Lang.T("Play")))
                .Replace("__T_MUTE__", Html(Lang.T("Mute")))
                .Replace("__FSLABEL__", Html(label))
                .Replace("__FSPATH__", fsPath)
                .Replace("__ERR__", Html(Lang.T(OwnHosted(v.Url) ? "This video can't play here right now." : "This video can't play here. Use OPEN IN BROWSER.")))
                .Replace("__AT__", start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("__FULL__", fullscreen ? "true" : "false")
                .Replace("__VOL__", volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
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
        public static Func<GuideVideo, bool> Live3D;         // set by the launcher: a Wowhead page in an Edge window (Community.cs)

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
            if (file == null && video.Url.IndexOf("wowhead.com", StringComparison.OrdinalIgnoreCase) >= 0 && Live3D != null && Live3D(video))
                return;                                      // Wowhead's live 3D viewer needs a modern browser, not the built-in one
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
                    Theme.Draw(g, img, pic.X + (tw - iw) / 2, pic.Y + (th - ih) / 2, iw, ih);
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
