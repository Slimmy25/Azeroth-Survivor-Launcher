// Moving over from the website: a save exported from Azeroth Survivors in the browser (wow.aethus.pl, Options > Saves >
// Export Save, a .sav file) put in as this PC's progress, so the game opens with it.
// The game keeps its progress in characters.txt in its save folder (%APPDATA%\wenilla-rogue): one line, "RSE1:" and the
// sealed data. Its own backups and exports (the Saves folder there) are the same thing in a .sav file, so an imported
// save is written in as characters.txt as it is. What was there first is kept in Saves as "Before import <date>.sav",
// where the game's own Options > Saves can restore it.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AzerothLauncher
{
    public static class SaveImport
    {
        public const string SaveFile = "characters.txt";

        // the save's text, or a UserError saying why it is not one
        public static string Read(string file)
        {
            var fi = new FileInfo(file);
            if (!fi.Exists) throw new UserError("That file does not exist.");
            if (fi.Length < 16 || fi.Length > 50L * 1024 * 1024) throw new UserError("That is not an Azeroth Survivors save file.");
            string text = File.ReadAllText(file, Encoding.UTF8).Trim().TrimStart('﻿');
            bool sealedSave = Regex.IsMatch(text, @"^RSE\d+:[A-Za-z0-9+/=\s]+$");
            bool plain = text.StartsWith("{") && text.EndsWith("}") && text.Contains("\"");   // a save the game wrote unsealed
            if (!sealedSave && !plain) throw new UserError("That is not an Azeroth Survivors save file.\n\n" +
                "Export it in the game on the website (Options > Saves > Export Save) and choose that .sav file.");
            return sealedSave ? Regex.Replace(text, @"\s+", "") : text;
        }

        // writes it in as the game's progress; returns the backup of what was there (or null)
        public static string Import(string file, string saveDir)
        {
            if (Util.GameRunning()) throw new UserError("Close the game first, then import the save.");
            string text = Read(file);
            if (string.IsNullOrEmpty(saveDir)) saveDir = Util.DefaultSaveDir;
            Directory.CreateDirectory(saveDir);
            string target = Path.Combine(saveDir, SaveFile), backup = null;
            if (File.Exists(target) && new FileInfo(target).Length > 0)
            {
                string saves = Path.Combine(saveDir, "Saves");
                Directory.CreateDirectory(saves);
                backup = Path.Combine(saves, "Before import " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".sav");
                File.Copy(target, backup, true);
            }
            string tmp = target + ".import";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            Util.Replace(tmp, target);
            // the file itself also goes in the game's Saves folder, where the game's own Options > Saves > Import Save
            // finds it too
            try
            {
                string saves = Path.Combine(saveDir, "Saves");
                Directory.CreateDirectory(saves);
                string copy = Path.Combine(saves, Path.GetFileName(file));
                if (!string.Equals(Path.GetFullPath(copy), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, copy, true);
            }
            catch (Exception) { }
            return backup;
        }
    }
}
