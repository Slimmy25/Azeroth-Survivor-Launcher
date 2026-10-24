// Play time shared between PCs through the cloud sync folder.
// <sync folder>\playtime.json holds one entry per PC (each PC only ever writes its own entry, so two PCs never
// overwrite each other's time) and an "Epoch": RESET PLAY TIME writes a new epoch, and every PC that sees a new
// epoch sets its own counters back to zero. The file sits next to the synced areas, not inside them, so it does
// not change the cloud save fingerprints (AzerothSync compatible).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AzerothLauncher
{
    public class PlayStats
    {
        public long Seconds, Sessions, Longest, LastSeconds;
        public string LastDate = "";
        public int Devices = 1;
    }

    public static class PlayTime
    {
        const string FileName = "playtime.json";
        static readonly object gate = new object();

        static string FilePath(string cloudDir) { return Path.Combine(cloudDir, FileName); }

        // the sync folder's parent must exist (the cloud drive is running); the folder itself is created if needed
        public static bool Reachable(string cloudDir)
        {
            try
            {
                if (string.IsNullOrEmpty(cloudDir)) return false;
                string parent = Path.GetDirectoryName(Path.GetFullPath(cloudDir).TrimEnd('\\'));
                return !string.IsNullOrEmpty(parent) && Directory.Exists(parent);
            }
            catch (Exception) { return false; }
        }

        static Dictionary<string, object> Read(string cloudDir)
        {
            try
            {
                string f = FilePath(cloudDir);
                if (File.Exists(f))
                {
                    var d = Json.AsDict(Json.Parse(File.ReadAllText(f, Encoding.UTF8)));
                    if (d != null) return d;
                }
            }
            catch (Exception) { }
            return new Dictionary<string, object>();
        }

        static long L(Dictionary<string, object> d, string key)
        {
            object v = Json.Get(d, key);
            return v is long ? (long)v : v is double ? (long)(double)v : 0;
        }

        public static string Epoch(string cloudDir)
        {
            lock (gate) return Json.Str(Read(cloudDir), "Epoch");
        }

        // Puts this PC's numbers in the file (and the epoch they belong to) and returns the totals of all PCs.
        public static PlayStats Publish(string cloudDir, string device, string deviceName, PlayStats mine, string epoch)
        {
            lock (gate)
            {
                var root = Read(cloudDir);
                var devices = Json.AsDict(Json.Get(root, "Devices")) ?? new Dictionary<string, object>();
                var e = new Dictionary<string, object>();
                e["Name"] = deviceName;
                e["Seconds"] = mine.Seconds;
                e["Sessions"] = mine.Sessions;
                e["Longest"] = mine.Longest;
                e["LastSeconds"] = mine.LastSeconds;
                e["LastDate"] = mine.LastDate;
                e["Updated"] = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
                devices[device] = e;
                root["Format"] = 1L;
                root["Epoch"] = epoch ?? "";
                root["Devices"] = devices;
                Directory.CreateDirectory(cloudDir);
                Util.WriteAtomic(FilePath(cloudDir), Json.Write(root, true));
                return Total(devices);
            }
        }

        // RESET PLAY TIME on any PC: a new epoch and no entries; every PC zeroes itself when it sees the new epoch
        public static void Reset(string cloudDir, string epoch)
        {
            lock (gate)
            {
                var root = new Dictionary<string, object>();
                root["Format"] = 1L;
                root["Epoch"] = epoch;
                root["Devices"] = new Dictionary<string, object>();
                Directory.CreateDirectory(cloudDir);
                Util.WriteAtomic(FilePath(cloudDir), Json.Write(root, true));
            }
        }

        static PlayStats Total(Dictionary<string, object> devices)
        {
            var t = new PlayStats();
            t.Devices = 0;
            foreach (var kv in devices)
            {
                var e = Json.AsDict(kv.Value);
                if (e == null) continue;
                t.Devices++;
                t.Seconds += L(e, "Seconds");
                t.Sessions += L(e, "Sessions");
                t.Longest = Math.Max(t.Longest, L(e, "Longest"));
                string date = Json.Str(e, "LastDate");
                if (string.CompareOrdinal(date, t.LastDate) > 0)        // "yyyy-MM-dd HH:mm" sorts by time
                {
                    t.LastDate = date;
                    t.LastSeconds = L(e, "LastSeconds");
                }
            }
            return t;
        }
    }
}
