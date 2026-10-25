// The New Player Guide's links and hover texts. Every name the guide knows (its topics and pages, the classes,
// every spell in a class's spell pool, the talents, the races and their racials, the zones) is found in the
// guide's text: click it to go to the page about it, point at it to read what it does.
//
// The hover texts come from the game itself: assets/game-text.txt holds what the game says about classes, races,
// racials, zones and talents (taken from the game at wow.aethus.pl). A spell's hover text is the guide's own line
// about it in its class's spell pool (the game builds a spell's tooltip with your numbers during a run).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public class GuideLink
    {
        public string Term = "", Tip = "";
        public int Topic = -1, Page = -1;                    // where a click goes (-1, -1: nowhere, a hover text only)
    }

    public static class GuideLinks
    {
        static Dictionary<string, GuideLink> links;
        static Regex finder;
        public static Action<int, int> Navigate;             // set by the launcher: open the Guide at that topic / page

        static Dictionary<string, string> GameText()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            bool on = false;
            foreach (string raw in Util.ReadResource("AS.game-text.txt").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line == "===") { on = true; continue; }
                if (!on || line.Length == 0 || line.StartsWith("#")) continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                d[line.Substring(0, bar).Trim()] = line.Substring(bar + 1).Trim();
            }
            return d;
        }

        static void Add(string term, int topic, int page, string tip)
        {
            term = (term ?? "").Trim();
            if (term.Length < 4 || links.ContainsKey(term)) return;
            var l = new GuideLink();
            l.Term = term;
            l.Topic = topic;
            l.Page = page;
            l.Tip = tip ?? "";
            links[term] = l;
        }

        static readonly Regex Bullet = new Regex(@"^- ([^:(]+?)(?: \([^)]*\))?: (.+)$");
        static readonly string[] TreeWords = { "Arms", "Fury", "Protection", "Holy", "Retribution", "Beast Mastery", "Marksmanship",
            "Survival", "Assassination", "Combat", "Subtlety", "Discipline", "Shadow", "Elemental", "Enhancement", "Restoration",
            "Arcane", "Fire", "Frost", "Affliction", "Demonology", "Destruction", "Balance", "Feral", "Feral Combat" };

        static void Build()
        {
            links = new Dictionary<string, GuideLink>(StringComparer.Ordinal);
            var text = GameText();
            List<GuideTopic> topics;
            try { topics = Data.Guide; }
            catch (Exception) { topics = new List<GuideTopic>(); }
            int racesTopic = -1, zonesTopic = -1;
            for (int t = 0; t < topics.Count; t++)
            {
                if (topics[t].Title.StartsWith("Races")) racesTopic = t;
                if (topics[t].Title.StartsWith("Zones")) zonesTopic = t;
            }
            // pages first (a class page wins over a mention elsewhere), then the spells and talents on them
            for (int t = 0; t < topics.Count; t++)
                for (int p = 0; p < topics[t].Pages.Count; p++)
                {
                    GuidePage pg = topics[t].Pages[p];
                    string tip;
                    Add(pg.Title, t, p, text.TryGetValue(pg.Title, out tip) ? tip : pg.Desc);
                }
            for (int t = 0; t < topics.Count; t++)
            {
                if (topics[t].Title.Length >= 6) Add(topics[t].Title, t, -1, topics[t].Desc);
                for (int p = -1; p < topics[t].Pages.Count; p++)
                {
                    GuidePage pg = p < 0 ? (GuidePage)topics[t] : topics[t].Pages[p];
                    string section = "";
                    foreach (string raw in pg.Body.Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("# ")) { section = line.Substring(2).Trim(); continue; }
                        if (!line.StartsWith("- ")) continue;
                        Match m = Bullet.Match(line);
                        if (section.StartsWith("Spell pool") && m.Success)
                        {
                            string name = m.Groups[1].Value.Trim();
                            string tip;
                            Add(name, t, p, text.TryGetValue(name, out tip) ? tip : Capital(m.Groups[2].Value.Trim()));
                        }
                        else if (section.StartsWith("Talent") && line.IndexOf(':') > 0)
                        {
                            foreach (string part in line.Substring(line.IndexOf(':') + 1).Split(','))
                                foreach (string n in part.Split('/'))
                                {
                                    string name = n.Trim().TrimEnd('.');
                                    int dot = name.IndexOf('.');
                                    if (dot > 0) name = name.Substring(0, dot).Trim();
                                    string tip;
                                    if (text.TryGetValue(name, out tip)) Add(name, t, p, tip);
                                }
                        }
                    }
                }
            }
            // races, racials and zones: the game's own words; a click opens their topic
            foreach (var kv in text)
            {
                if (Array.IndexOf(TreeWords, kv.Key) >= 0) continue;
                if (links.ContainsKey(kv.Key)) { if (links[kv.Key].Tip.Length == 0) links[kv.Key].Tip = kv.Value; continue; }
                bool zone = kv.Value.Length > 0 && Array.IndexOf(new[] { "Elwynn Forest", "Duskwood", "Warsong Gulch", "Arathi Basin",
                    "Feralas", "Gurubashi Arena", "Alterac Valley", "Blackrock Mountain", "Gates of Ahn'Qiraj", "Un'Goro Crater",
                    "Scarlet Monastery", "Naxxramas" }, kv.Key) >= 0;
                Add(kv.Key, zone ? zonesTopic : racesTopic, -1, kv.Value);
            }
            foreach (string w in TreeWords) links.Remove(w);
            var terms = new List<string>(links.Keys);
            terms.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });   // longest first
            var parts = new List<string>();
            foreach (string term in terms) parts.Add(Regex.Escape(term));
            finder = parts.Count == 0 ? null : new Regex(@"(?<![A-Za-z'])(" + string.Join("|", parts.ToArray()) + @")(?![A-Za-z])");
        }

        static string Capital(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // the text cut into plain runs and links: (text, link or null)
        public static List<KeyValuePair<string, GuideLink>> Split(string text)
        {
            if (links == null) Build();
            var outList = new List<KeyValuePair<string, GuideLink>>();
            int at = 0;
            if (finder != null)
                foreach (Match m in finder.Matches(text))
                {
                    if (m.Index > at) outList.Add(new KeyValuePair<string, GuideLink>(text.Substring(at, m.Index - at), null));
                    outList.Add(new KeyValuePair<string, GuideLink>(m.Value, links[m.Value]));
                    at = m.Index + m.Length;
                }
            if (at < text.Length) outList.Add(new KeyValuePair<string, GuideLink>(text.Substring(at), null));
            return outList;
        }
    }

    // A guide paragraph or bullet with its links: the linked names are drawn in gold and underlined; a click goes to
    // their page (not to the page it is already on), and pointing at one shows what it does.
    public class GuideText : Control
    {
        readonly List<KeyValuePair<string, GuideLink>> runs;
        readonly List<KeyValuePair<RectangleF, GuideLink>> hot = new List<KeyValuePair<RectangleF, GuideLink>>();
        readonly int here, herePage;                         // the page it is on
        readonly Font font, linkFont;
        readonly int wrap;
        GuideLink over;

        public GuideText(string text, float size, Color fg, Color bg, int wrapWidth, int topic, int page)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            runs = GuideLinks.Split(text);
            here = topic;
            herePage = page;
            font = Theme.F(size);
            linkFont = Theme.F(size, FontStyle.Underline);
            ForeColor = fg;
            BackColor = bg;
            wrap = wrapWidth;
            Flow(null);
        }

        bool Clickable(GuideLink l) { return l != null && l.Topic >= 0 && !(l.Topic == here && l.Page == herePage); }

        static readonly Bitmap measureBmp = new Bitmap(1, 1);

        // lays the words out (and draws them when g is given); returns the height. Measured and drawn the same way
        // (GDI+, typographic), so the spaces and the links sit exactly where the text says
        int Flow(Graphics draw)
        {
            hot.Clear();
            Graphics g = draw ?? Graphics.FromImage(measureBmp);
            try
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;   // as the guide's other text
                using (var sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
                    float x = 0, y = 0;
                    int lineH = font.Height + 1;
                    float spaceW = g.MeasureString(" ", font, PointF.Empty, sf).Width * 1.15f;
                    foreach (var run in runs)
                    {
                        GuideLink link = run.Value;
                        Font f = link != null ? linkFont : font;
                        Color c = link == null ? ForeColor : (link == over ? Theme.Hex("#fff2b0") : Theme.Hex("#e8b84a"));
                        // a link stays in one piece when it fits on a line; plain text wraps word by word
                        string[] words = link != null ? new[] { run.Key } : Regex.Split(run.Key, "( )");
                        foreach (string word in words)
                        {
                            if (word.Length == 0) continue;
                            if (word == " ") { if (x > 0) x += spaceW; continue; }
                            float w = g.MeasureString(word, f, PointF.Empty, sf).Width;
                            if (x > 0 && x + w > wrap) { x = 0; y += lineH; }
                            if (draw != null)
                                using (var b = new SolidBrush(c)) draw.DrawString(word, f, b, x, y, sf);
                            if (link != null) hot.Add(new KeyValuePair<RectangleF, GuideLink>(new RectangleF(x, y, w, lineH), link));
                            x += w;
                        }
                    }
                    int h = (int)Math.Ceiling(y) + lineH;
                    if (draw == null) Size = new Size(wrap, h);
                    return h;
                }
            }
            finally { if (draw == null) g.Dispose(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Flow(e.Graphics);
        }

        GuideLink At(Point p)
        {
            foreach (var h in hot) if (h.Key.Contains(p)) return h.Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            GuideLink l = At(e.Location);
            if (l == over) return;
            over = l;
            Cursor = Clickable(l) ? Cursors.Hand : (l != null ? Cursors.Help : Cursors.Default);
            Invalidate();
            if (l != null && l.Tip.Length > 0) GuideHover.Show(this, l.Term, l.Tip);
            else GuideHover.Hide();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (over != null) { over = null; Invalidate(); }
            Cursor = Cursors.Default;
            GuideHover.Hide();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            GuideLink l = At(e.Location);
            if (e.Button != MouseButtons.Left || !Clickable(l) || GuideLinks.Navigate == null) return;
            GuideHover.Hide();
            GuideLinks.Navigate(l.Topic, l.Page);
        }
    }

    // the hover box: the name in gold, then what it does, in the launcher's dark and gold
    public class GuideHover : Form
    {
        static GuideHover box;
        string title = "", body = "";
        const int W = 300;

        GuideHover()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Theme.Hex("#120f18");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080;       // no activation, no Alt+Tab entry
                return cp;
            }
        }

        public static void Show(Control owner, string title, string body)
        {
            if (box == null || box.IsDisposed) box = new GuideHover();
            box.title = title;
            box.body = body;
            Font tf = Theme.F(10.5f, FontStyle.Bold), bf = Theme.F(9.5f);
            int th = TextRenderer.MeasureText(title, tf, new Size(W - 20, 0), TextFormatFlags.WordBreak).Height;
            int bh = TextRenderer.MeasureText(body, bf, new Size(W - 20, 0), TextFormatFlags.WordBreak).Height;
            Point p = Cursor.Position;
            var size = new Size(W, th + bh + 22);
            Rectangle screen = Screen.FromPoint(p).WorkingArea;
            int x = Math.Min(p.X + 16, screen.Right - size.Width - 4), y = p.Y + 20;
            if (y + size.Height > screen.Bottom) y = p.Y - size.Height - 8;
            box.Bounds = new Rectangle(x, y, size.Width, size.Height);
            box.Invalidate();
            if (!box.Visible) box.Show();
        }

        public static new void Hide()
        {
            if (box != null && !box.IsDisposed && box.Visible) ((Form)box).Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            using (var p = new Pen(Theme.Hex("#c49a48"), 1)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            Font tf = Theme.F(10.5f, FontStyle.Bold), bf = Theme.F(9.5f);
            int th = TextRenderer.MeasureText(title, tf, new Size(W - 20, 0), TextFormatFlags.WordBreak).Height;
            TextRenderer.DrawText(g, title, tf, new Rectangle(10, 8, W - 20, th), Theme.Gold, BackColor, TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, body, bf, new Rectangle(10, 10 + th, W - 20, Height - th - 14), Theme.Text, BackColor,
                                  TextFormatFlags.WordBreak);
        }
    }
}
