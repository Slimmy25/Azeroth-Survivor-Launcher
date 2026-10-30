using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AzerothLauncher
{
    // Experimental independent build record. Not wired into the launcher yet.
    public sealed class ProjectLaunchBuild
    {
        public string Id = "";
        public string Name = "";
        public string Class = "";
        public string TalentCode = "";
        public Dictionary<string, string> Equipment = new Dictionary<string, string>();
        public Dictionary<string, int> Stars = new Dictionary<string, int>();
    }

    public static class ProjectLaunchBuildStorage
    {
        public static string ToJson(ProjectLaunchBuild b)
        {
            var gear = new Dictionary<string, object>();
            foreach (var p in b.Equipment) gear[p.Key] = p.Value;
            var stars = new Dictionary<string, object>();
            foreach (var p in b.Stars) stars[p.Key] = p.Value;
            return Json.Write(new Dictionary<string, object> {
                { "schema", 1 }, { "id", b.Id }, { "name", b.Name },
                { "class", b.Class }, { "talents", b.TalentCode },
                { "equipment", gear }, { "stars", stars }
            }, true);
        }

        public static ProjectLaunchBuild FromJson(string content)
        {
            var obj = Json.AsDict(Json.Parse(content));
            if (obj == null || Convert.ToInt32(Json.Get(obj, "schema")) != 1)
                throw new FormatException("Unsupported build version.");
            var b = new ProjectLaunchBuild();
            b.Id = Json.Str(obj, "id");
            b.Name = Json.Str(obj, "name");
            b.Class = Json.Str(obj, "class");
            b.TalentCode = Json.Str(obj, "talents");
            var gear = Json.AsDict(Json.Get(obj, "equipment"));
            if (gear != null)
                foreach (var p in gear) if (p.Value is string) b.Equipment[p.Key] = (string)p.Value;
            var stars = Json.AsDict(Json.Get(obj, "stars"));
            if (stars != null)
                foreach (var p in stars) {
                    int n = Convert.ToInt32(p.Value);
                    if (n < 0 || n > 5) throw new FormatException("Invalid stars.");
                    b.Stars[p.Key] = n;
                }
            return b;
        }

        // Stable IDs, not user-visible build names, are filenames.
        public static void Save(string directory, ProjectLaunchBuild b)
        {
            if (String.IsNullOrEmpty(b.Id) || b.Id.Length > 64)
                throw new ArgumentException("Invalid build ID.");
            foreach (char c in b.Id)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '-'))
                    throw new ArgumentException("Invalid build ID.");
            Directory.CreateDirectory(directory);
            string dest = Path.Combine(directory, b.Id + ".json");
            string temp = dest + ".tmp";
            File.WriteAllText(temp, ToJson(b), new UTF8Encoding(false));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(temp, dest);
        }
    }
}
