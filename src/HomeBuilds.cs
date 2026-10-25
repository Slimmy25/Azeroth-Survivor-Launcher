// The home page's "Build collection": the newest video of the guide's Build Guides topic (assets/guide.txt), as a
// card in the empty space under the logo: its picture with a play button, the video's title as the header, and the
// uploader's name as credit. Nothing plays by itself; a click opens the video in the web browser (YouTube does not
// play inside the launcher's web view). "More build guides" opens the topic in the GUIDE section.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        static readonly RectangleF BuildBox = new RectangleF(30, 222, 628, 250);
        RectangleF buildPic, buildTitle, buildAuthor, buildMore;
        string buildHover;                                   // "video", "author", "more" or null

        // the Build Guides topic's index and its newest video (the first one), or null
        static GuideVideo FeaturedBuild(out int topicIndex)
        {
            topicIndex = -1;
            List<GuideTopic> topics;
            try { topics = Data.Guide; }
            catch (Exception) { return null; }
            for (int i = 0; i < topics.Count; i++)
            {
                if (!string.Equals(topics[i].Title, "Build Guides", StringComparison.OrdinalIgnoreCase)) continue;
                topicIndex = i;
                if (topics[i].Videos.Count > 0) return topics[i].Videos[0];
                foreach (var p in topics[i].Pages) if (p.Videos.Count > 0) return p.Videos[0];
                return null;
            }
            return null;
        }

        void BuildCollection(Graphics g)
        {
            int ti;
            GuideVideo v = FeaturedBuild(out ti);
            buildPic = buildTitle = buildAuthor = buildMore = RectangleF.Empty;
            if (v == null) return;
            RectangleF box = BuildBox;
            using (var path = Armory.Round(box, 8))
            {
                using (var b = new SolidBrush(Color.FromArgb(200, 14, 13, 20))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(160, Theme.GoldDim), 1)) g.DrawPath(p, path);
            }
            Theme.Smooth(g);
            Font head = Theme.F(11, FontStyle.Bold), link = Theme.F(9, FontStyle.Italic);
            using (var gold = new SolidBrush(Retro.Gold))
                g.DrawString(Lang.T("Build collection"), head, gold, box.X + 14, box.Y + 8);
            string more = Lang.T("More build guides ›");
            SizeF ms = g.MeasureString(more, link);
            buildMore = new RectangleF(box.Right - 14 - ms.Width, box.Y + 11, ms.Width, ms.Height);
            using (var b = new SolidBrush(buildHover == "more" ? Theme.Hex("#fff2b0") : Theme.GoldDim))
                g.DrawString(more, link, b, buildMore.X, buildMore.Y);

            // the picture (16:9) with a play button; YouTube's picture has black bars, so its middle fills the box
            float top = box.Y + 36, ph = box.Bottom - 14 - top, pw = ph * 16 / 9;
            buildPic = new RectangleF(box.X + 14, top, pw, ph);
            string id = VideoCard.YouTubeIdOf(v.Url);
            Image img = id != null ? VideoCard.YouTubeThumb(id, delegate { Ui(delegate { Invalidate(Rectangle.Ceiling(BuildBox)); }); }) : null;
            bool hot = buildHover == "video";
            using (var clip = Armory.Round(buildPic, 6))
            {
                g.SetClip(clip);
                if (img != null)
                {
                    float s = Math.Max(pw / img.Width, ph / (img.Height * 0.75f));
                    float iw = img.Width * s, ih = img.Height * s;
                    g.DrawImage(img, buildPic.X + (pw - iw) / 2, buildPic.Y + (ph - ih) / 2, iw, ih);
                    if (!hot) using (var dim = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillRectangle(dim, buildPic);
                }
                else
                    using (var lg = new LinearGradientBrush(buildPic, Theme.Hex("#3b1f4a"), Theme.Hex("#120a18"), 60f)) g.FillRectangle(lg, buildPic);
                g.ResetClip();
            }
            using (var pen = new Pen(hot ? Theme.Gold : Theme.Hex("#4a3a22"), hot ? 1.5f : 1f))
            using (var rp = Armory.Round(buildPic, 6)) g.DrawPath(pen, rp);
            float pd = 54, cx = buildPic.X + pw / 2, cy = buildPic.Y + ph / 2;
            using (var b = new SolidBrush(hot ? Theme.Hex("#c8442f") : Color.FromArgb(220, 30, 22, 18))) g.FillEllipse(b, cx - pd / 2, cy - pd / 2, pd, pd);
            using (var pen = new Pen(Theme.Gold, 1.5f)) g.DrawEllipse(pen, cx - pd / 2, cy - pd / 2, pd, pd);
            float t = pd * 0.32f;
            using (var b = new SolidBrush(Color.White))
                g.FillPolygon(b, new[] { new PointF(cx - t * 0.7f, cy - t), new PointF(cx - t * 0.7f, cy + t), new PointF(cx + t, cy) });

            // the header (the video's title), the credit, where it plays
            float x = buildPic.Right + 14, y = top, w = box.Right - 14 - x;
            Font tf = Theme.F(11.5f, FontStyle.Bold);
            string title = v.Title.Length > 0 ? v.Title : Lang.T("Watch the video");
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(hot ? Theme.Hex("#fff2b0") : Theme.Gold))
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                SizeF ts = g.MeasureString(title, tf, (int)w);
                float h = Math.Min(ts.Height, tf.Height * 4.2f);
                buildTitle = new RectangleF(x, y, w, h);
                g.DrawString(title, tf, b, buildTitle, sf);
                y += h + 8;
            }
            if (v.Author.Length > 0)
            {
                Font af = Theme.F(10);
                string by = Lang.T("by") + " " + v.Author;
                SizeF bs = g.MeasureString(by, af);
                buildAuthor = new RectangleF(x, y, Math.Min(w, bs.Width), bs.Height);
                bool ah = buildHover == "author" && v.AuthorUrl.Length > 0;
                using (var b = new SolidBrush(ah ? Theme.Hex("#fff2b0") : Theme.Text)) g.DrawString(by, af, b, x, y);
                if (ah) using (var p = new Pen(Theme.Hex("#fff2b0"))) g.DrawLine(p, x, y + bs.Height - 2, x + bs.Width - 4, y + bs.Height - 2);
                y += bs.Height + 2;
                using (var b = new SolidBrush(Theme.GoldDim))
                    g.DrawString(Lang.T("All credit for the build and the video goes to the uploader."), Theme.F(8, FontStyle.Italic), b,
                                 new RectangleF(x, y, w, 30));
                y += 30;
            }
            using (var b = new SolidBrush(hot ? Theme.Hex("#fff2b0") : Theme.GoldDim))
                g.DrawString(Lang.T(id != null ? "Click to watch on YouTube in your browser ›" : "Click to watch it in your browser ›"),
                             Theme.F(9, FontStyle.Italic), b, new RectangleF(x, Math.Max(y, buildPic.Bottom - 34), w, 34));
        }

        // what the mouse is over in the Build collection card (null when the card is not there or a section covers it)
        string BuildHit(Point p)
        {
            if (side != null && side.Visible) return null;
            if (buildMore.Contains(p)) return "more";
            if (buildAuthor.Contains(p)) return "author";
            if (buildPic.Contains(p) || buildTitle.Contains(p)) return "video";
            return null;
        }

        void BuildMouseMove(Point p)
        {
            string h = BuildHit(p);
            int ti;
            GuideVideo v = FeaturedBuild(out ti);
            if (h == "author" && (v == null || v.AuthorUrl.Length == 0)) h = null;
            if (h == buildHover) return;
            buildHover = h;
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
            Invalidate(Rectangle.Ceiling(BuildBox));
        }

        bool BuildClick(Point p)
        {
            string h = BuildHit(p);
            if (h == null) return false;
            int ti;
            GuideVideo v = FeaturedBuild(out ti);
            if (v == null) return false;
            if (h == "video") { Util.OpenInShell(v.Url); SetStatus("The video opened in your web browser."); }
            else if (h == "author") { if (v.AuthorUrl.Length > 0) Util.OpenInShell(v.AuthorUrl); }
            else if (h == "more" && ti >= 0)
            {
                ShowSection("Guide");
                SetGuideView(ti, -1);
            }
            return true;
        }
    }
}
