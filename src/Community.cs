// The Community section: what opens on the left of the home screen whenever no other section is open. It holds
// the players' build videos (the Guide's Build Guides topic in assets/guide.txt), each with its title and credit
// to its uploader. Nothing plays by itself. A click plays the YouTube video in a window of its own laid exactly over
// this section (Microsoft Edge in app mode, which every Windows 10/11 has: Windows' old web view inside the launcher
// cannot play YouTube any more); without Edge it opens in the web browser.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        bool communityDismissed;                             // its X was pressed: the home screen stays plain until next time

        // every video of the Build Guides topic, newest first, and the topic's index
        static List<GuideVideo> BuildVideos(out int topicIndex)
        {
            topicIndex = -1;
            var list = new List<GuideVideo>();
            List<GuideTopic> topics;
            try { topics = Data.Guide; }
            catch (Exception) { return list; }
            for (int i = 0; i < topics.Count; i++)
            {
                if (!string.Equals(topics[i].Title, "Build Guides", StringComparison.OrdinalIgnoreCase)) continue;
                topicIndex = i;
                list.AddRange(topics[i].Videos);
                foreach (var p in topics[i].Pages) list.AddRange(p.Videos);
                break;
            }
            return list;
        }

        // the home screen: Community, unless its X was pressed
        void ShowHomeCommunity()
        {
            if (communityDismissed || side == null) { HideSide(); return; }
            ShowSection("Community");
        }

        void PanelCommunity()
        {
            ClearPanel("Community");
            if (VideoCard.YouTube == null) VideoCard.YouTube = PlayYouTube;
            int ti;
            var videos = BuildVideos(out ti);
            PutText("Build videos made by players. Click one to watch it; all credit goes to the people who made them.",
                    9.5f, FontStyle.Italic, Theme.GoldDim, 84, 16, PW - 104);
            int top = (int)(12 + 32 * Theme.Zoom) + 12;
            var sp = MakeScroll(panel, new Rectangle(20, top, PW - 40, PH - 12 - top), Theme.Panel, Theme.Bronze);
            int w = sp.ClientSize.Width - 12, y = 6;
            if (videos.Count == 0)
            {
                var l = UI.Text("No community videos yet.", 11, FontStyle.Italic, Theme.GoldDim, Theme.Panel, 0);
                l.Location = new Point(10, 10);
                sp.Controls.Add(l);
                return;
            }
            foreach (var v in videos)
            {
                var card = new VideoCard(v, w);
                card.Location = new Point(4, y);
                sp.Controls.Add(card);
                y += card.Height + 10;
            }
            if (ti >= 0)
            {
                int topic = ti;
                var more = new WowButton("BUILD GUIDES IN THE GUIDE ›", delegate { ShowSection("Guide"); SetGuideView(topic, -1); }, 260, 30, 9);
                more.Location = new Point(4, y + 2);
                sp.Controls.Add(more);
            }
        }

        static string EdgePath()
        {
            foreach (string k in new[] { "ProgramFiles(x86)", "ProgramFiles", "ProgramW6432", "LOCALAPPDATA" })
            {
                string root = Environment.GetEnvironmentVariable(k);
                if (string.IsNullOrEmpty(root)) continue;
                string p = Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        // a YouTube video: in an Edge app window over the section area (the launcher's left side), else the browser
        bool PlayYouTube(GuideVideo v)
        {
            string id = VideoCard.YouTubeIdOf(v.Url);
            string url = id != null ? "https://www.youtube.com/watch?v=" + id : v.Url;
            string edge = EdgePath();
            if (edge != null)
            {
                try
                {
                    Rectangle r = side != null && side.Visible ? side.RectangleToScreen(side.ClientRectangle)
                                                               : RectangleToScreen(new Rectangle(8, 200, 670, 378));
                    var psi = new ProcessStartInfo(edge, "--app=\"" + url + "\" --window-position=" + r.X + "," + r.Y +
                                                         " --window-size=" + r.Width + "," + r.Height);
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                    SetStatus("Playing: " + (v.Title.Length > 0 ? v.Title : "the video") + " (close its window to stop).");
                    return true;
                }
                catch (Exception) { }
            }
            Util.OpenInShell(url);
            SetStatus("The video opened in your web browser.");
            return true;
        }
    }
}
