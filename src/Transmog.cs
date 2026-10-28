// Transmog: the looks that add nothing (Appearances, Tabards, weapon glows), from assets/transmog.txt, written out as the
// Guide's "Transmog" topic. Their icons: the game's own icon names when known. On mouseover, a look that is a real item shows its 3D model
// picture (assets/item-models.txt, GuideHover).
using System;
using System.Collections.Generic;

namespace AzerothLauncher
{
    public class Look
    {
        public string Kind = "", Slot = "", Name = "", How = "", Icon = "";
    }

    public static class Transmog
    {
        static List<Look> looks;

        public static List<Look> All
        {
            get
            {
                if (looks != null) return looks;
                var l = new List<Look>();
                bool on = false;
                try
                {
                    foreach (string raw in Util.ReadResource("AS.transmog.txt").Replace("\r\n", "\n").Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line == "===") { on = true; continue; }
                        if (!on || line.Length == 0 || line.StartsWith("#")) continue;
                        string[] f = line.Split('|');
                        if (f.Length < 4) continue;
                        l.Add(new Look { Kind = f[0].Trim(), Slot = f[1].Trim(), Name = f[2].Trim(), How = f[3].Trim(), Icon = f.Length > 4 ? f[4].Trim() : "" });
                    }
                }
                catch (Exception) { }
                return looks = l;
            }
        }

        // the Guide's "Transmog" topic (guide.txt's format)
        public static IEnumerable<string> GuideLines()
        {
            var l = new List<string>();
            if (All.Count == 0) return l;
            l.Add("@topic Transmog | Appearances, tabards and weapon glows: looks that add nothing, and how to earn them");
            l.Add("Every look here only changes how your hero looks; none of them adds to your numbers. Choose them in Armory > Character.");
            l.Add("@page Appearances | Head and back looks any class can show, each with an achievement");
            foreach (string slot in new[] { "Head", "Back" })
            {
                l.Add("# " + slot);
                foreach (var x in All) if (x.Kind == "appearance" && x.Slot == slot) l.Add("- " + x.Name + ": " + x.How);
            }
            l.Add("@page Tabards | A look for the tabard slot: battleground tabards come with their zone's achievement, the others are bought");
            l.Add("# Earned");
            foreach (var x in All) if (x.Kind == "tabard" && !x.How.StartsWith("Buy")) l.Add("- " + x.Name + ": " + x.How);
            l.Add("# Bought");
            foreach (var x in All) if (x.Kind == "tabard" && x.How.StartsWith("Buy")) l.Add("- " + x.Name + ": " + x.How);
            l.Add("@page Weapon Glows | A glow on any weapon, picked in the armory");
            l.Add("Pick your class's glow, or none, on any weapon in the armory. A picked glow also replaces a legendary weapon's own.");
            foreach (var x in All) if (x.Kind == "glow") l.Add("- " + x.Name + ": " + x.How);
            // Wowhead's still pictures don't show enchant glows; its live 3D dressing room does (opened in Edge, Community.cs)
            l.Add("See them in 3D: Wowhead's live dressing room opens in a window of its own. Put a weapon in a hand, then pick its illusion (Icy Chill, Crusader, Fiery Weapon or Unholy Weapon).");
            l.Add("@video Weapon glows in 3D (Wowhead) | https://www.wowhead.com/dressing-room");
            return l;
        }
    }
}
