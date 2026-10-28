// The back arrow of the section window goes back along the way you came (Guide > a class > Options > back lands on
// that class again), not just one level up. Every view shown is remembered (the last 50); a view that only redraws
// itself (a toggle, a language change, typing in the search) is not a new step. The home screen starts the way anew.
using System;
using System.Collections.Generic;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        class ViewState
        {
            public string Section, GTalents, GuideQuery, AddonMode;
            public int GTopic, GPage, GuideGroupPage, GuideGroup, GuideLastTopic, ClView, OptionsTab, StatsTab;
            public bool GBuilder, GPlanner, GuideMain, OptionsLang, OptionsAbout, CommunityMore;

            // the same place (the search text aside: typing is not a new step)
            public bool SamePlace(ViewState o)
            {
                return o != null && Section == o.Section && GTalents == o.GTalents && AddonMode == o.AddonMode &&
                       GTopic == o.GTopic && GPage == o.GPage && GuideGroupPage == o.GuideGroupPage && GuideGroup == o.GuideGroup &&
                       ClView == o.ClView && OptionsTab == o.OptionsTab && StatsTab == o.StatsTab && GBuilder == o.GBuilder &&
                       GPlanner == o.GPlanner && GuideMain == o.GuideMain && OptionsLang == o.OptionsLang && OptionsAbout == o.OptionsAbout &&
                       CommunityMore == o.CommunityMore && (GuideQuery.Length > 0) == (o.GuideQuery.Length > 0);
            }
        }

        readonly List<ViewState> history = new List<ViewState>();
        bool restoringView;

        ViewState CurrentView(string section)
        {
            return new ViewState
            {
                Section = section, GTalents = gTalents, GuideQuery = guideQuery ?? "", AddonMode = addonMode,
                GTopic = gTopic, GPage = gPage, GuideGroupPage = guideGroupPage, GuideGroup = guideGroup, GuideLastTopic = guideLastTopic,
                ClView = clView, OptionsTab = optionsTab, StatsTab = statsTab,
                GBuilder = gBuilder, GPlanner = gPlanner, GuideMain = guideMain, OptionsLang = optionsLang, OptionsAbout = optionsAbout, CommunityMore = communityMore
            };
        }

        // called each time a section is built
        void RememberView(string section)
        {
            if (restoringView || section == "Setup") return;
            if (section == "Community" && !communityMore) { history.Clear(); return; }   // the home screen: a new way starts
            var v = CurrentView(section);
            if (history.Count > 0 && history[history.Count - 1].SamePlace(v)) { history[history.Count - 1] = v; return; }
            history.Add(v);
            if (history.Count > 50) history.RemoveAt(0);
        }

        // the back arrow: the view before this one, as it was; false when there is none (then one level up, as before)
        bool BackInHistory()
        {
            if (history.Count < 2) return false;
            history.RemoveAt(history.Count - 1);
            ViewState v = history[history.Count - 1];
            if (v.Section == "Stats" && !StatsAddonActive()) { history.RemoveAt(history.Count - 1); return BackInHistory(); }
            bool leavingPlanner = gPlanner && !v.GPlanner;
            gTalents = v.GTalents; guideQuery = v.GuideQuery; addonMode = v.AddonMode;
            gTopic = v.GTopic; gPage = v.GPage; guideGroupPage = v.GuideGroupPage; guideGroup = v.GuideGroup; guideLastTopic = v.GuideLastTopic;
            clView = v.ClView; optionsTab = v.OptionsTab; statsTab = v.StatsTab;
            gBuilder = v.GBuilder; gPlanner = v.GPlanner; guideMain = v.GuideMain; optionsLang = v.OptionsLang; optionsAbout = v.OptionsAbout; communityMore = v.CommunityMore;
            if (leavingPlanner) ResetPlannerState();
            gVideo = null;
            current = v.Section;
            foreach (var kv in nav) kv.Value.Active = kv.Key == v.Section;
            RefreshStrip();
            ShowSide();
            restoringView = true;
            try { BuildPanel(v.Section); }
            finally { restoringView = false; }
            return true;
        }
    }
}
