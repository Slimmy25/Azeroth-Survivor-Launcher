// "Share a run": a picture of one run (the History on the STATS page: click a run) for Discord and the like. It
// is copied to the clipboard and saved in Pictures\Azeroth Survivors. The class, zone and spell pictures come from
// the same place as the STATS page's (Wowhead, cached); a picture not there yet is drawn as its badge.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace AzerothLauncher
{
    public partial class LauncherApp
    {
        public void ShareRun(StatRun r)
        {
            // ask for the pictures first, then draw when they have had a moment to arrive
            SpellIcons.Class(r.ClassKey, null);
            SpellIcons.Zone(r.Zone, null);
            foreach (var a in r.Abilities) SpellIcons.Spell(a.Name, null);
            SetStatus("Making the run's picture...");
            After(1200, delegate
            {
                try
                {
                    using (Bitmap card = RunCard(r))
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Azeroth Survivors");
                        Directory.CreateDirectory(dir);
                        string stamp = r.Date.Length > 0 ? r.Date.Replace(":", "").Replace(" ", "-") : DateTime.Now.ToString("yyyy-MM-dd-HHmm");
                        string file = Path.Combine(dir, "Run " + stamp + " " + r.Class + ".png");
                        card.Save(file, ImageFormat.Png);
                        Clipboard.SetImage(card);
                        SetStatus("The run's picture is copied: paste it into Discord. Also saved in Pictures.");
                    }
                }
                catch (Exception e) { SetStatus("Could not make the run's picture: " + e.Message); }
            });
        }

        static Bitmap RunCard(StatRun r)
        {
            const int W = 640, H = 300;
            var bmp = new Bitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.Smooth(g);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;   // a picture: no ClearType colors
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Theme.Hex("#1d1730"), Theme.Hex("#0a0910"), 90f))
                    g.FillRectangle(bg, 0, 0, W, H);
                // the zone's art, faded, on the right
                Image zone = SpellIcons.Zone(r.Zone, null);
                if (zone != null)
                {
                    var za = new ColorMatrix();
                    za.Matrix33 = 0.28f;
                    using (var ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(za);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(zone, new Rectangle(W - 230, 10, 220, 220), 0, 0, zone.Width, zone.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                using (var p = new Pen(Theme.Gold, 2)) g.DrawRectangle(p, 1, 1, W - 3, H - 3);
                using (var p = new Pen(Theme.GoldDim, 1)) g.DrawRectangle(p, 6, 6, W - 13, H - 13);
                Color cc = ClassColorOf(r.ClassKey);
                // class icon and the headline
                var ci = new Rectangle(22, 22, 64, 64);
                Image cp = SpellIcons.Class(r.ClassKey, null);
                if (cp != null) Armory.Picture(g, ci, cp, false, true, cc); else Armory.Icon(g, ci, cc, Armory.Initials(r.Class), true);
                Color rc = r.Kind == "victory" ? Theme.Hex("#5fd35f") : r.Kind == "defeat" ? Theme.Bad : Theme.GoldDim;
                string result = r.Kind == "victory" ? "Victory" : r.Kind == "defeat" ? "Defeat" : (r.Result.Length > 0 ? char.ToUpper(r.Result[0]) + r.Result.Substring(1) : "Run");
                using (var b = new SolidBrush(rc)) g.DrawString(result, Theme.F(22, FontStyle.Bold), b, 98, 18);
                using (var b = new SolidBrush(cc))
                    g.DrawString(r.Class + (r.Race.Length > 0 ? " (" + r.Race + ")" : "") + (r.Zone.Length > 0 ? " · " + r.Zone : ""),
                                 Theme.F(12, FontStyle.Bold), b, 100, 56);
                // the numbers
                string[][] nums = {
                    new[] { Big(r.Dps), "DPS" }, new[] { Clock(r.Time), "Survived" }, new[] { Big(r.Level), "Level" }, new[] { Big(r.Kills), "Kills" } };
                for (int i = 0; i < nums.Length; i++)
                {
                    var box = new Rectangle(22 + i * 150, 104, 140, 64);
                    using (var path = Armory.Round(box, 8))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(200, 22, 20, 32))) g.FillPath(b, path);
                        using (var p = new Pen(i == 0 ? Armory.Legendary : Theme.CardEdge, 1)) g.DrawPath(p, path);
                    }
                    using (var b = new SolidBrush(i == 0 ? Armory.Legendary : Armory.Bright))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                    {
                        g.DrawString(nums[i][0], Theme.F(17, FontStyle.Bold), b, new RectangleF(box.X, box.Y + 8, box.Width, 30), sf);
                        using (var d = new SolidBrush(Armory.Dim)) g.DrawString(nums[i][1], Theme.F(9), d, new RectangleF(box.X, box.Y + 40, box.Width, 18), sf);
                    }
                }
                // the build
                int x = 22;
                foreach (var a in r.Abilities)
                {
                    if (x + 40 > W - 20) break;
                    var ir = new Rectangle(x, 186, 40, 40);
                    Image pic = SpellIcons.Spell(a.Name, null);
                    if (pic != null) Armory.Picture(g, ir, pic, false, false, Theme.GoldDim);
                    else Armory.Icon(g, ir, Theme.GoldDim, Armory.Initials(a.Name), false);
                    if (a.Picks > 0)
                        using (var b = new SolidBrush(Armory.Bright))
                            g.DrawString(Big(a.Picks), Theme.F(9, FontStyle.Bold), b, ir.Right - 14, ir.Bottom - 15);
                    x += 46;
                }
                using (var b = new SolidBrush(Theme.GoldDim))
                    g.DrawString((r.Date.Length > 0 ? r.Date + "   ·   " : "") + (r.Party.Length > 0 ? "with " + r.Party + "   ·   " : "") +
                                 "Azeroth Survivors · AZS Stats", Theme.F(9, FontStyle.Italic), b, 22, H - 34);
            }
            return bmp;
        }
    }
}
