using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteamManifestToggler
{
    public class GameEntry
    {
        public string Name { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;

        public bool IsReadOnly
        {
            get
            {
                try
                {
                    if (!File.Exists(ManifestPath)) return false;
                    var fi = new FileInfo(ManifestPath);
                    return fi.IsReadOnly;
                }
                catch { return false; }
            }
        }
    }

    public static class SteamScanner
    {
        public static string? GetDefaultSteamPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var sp = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrWhiteSpace(sp) && Directory.Exists(sp)) return sp;
                    var ip = key.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(ip) && Directory.Exists(ip)) return ip;
                }
            }
            catch { }
            var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (Directory.Exists(p)) return p;
            p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
            if (Directory.Exists(p)) return p;
            p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Steam");
            if (Directory.Exists(p)) return p;
            return null;
        }

        public static string? ResolveLibraryVdfPath(string root)
        {
            var candidates = new[] {
                Path.Combine(root, "steamapps", "libraryfolders.vdf"),
                Path.Combine(root, "config", "libraryfolders.vdf"),
                Path.Combine(root, "libraryfolders.vdf")
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return null;
        }

        public static List<GameEntry> ScanAllGamesFromRoot(string steamRoot)
        {
            var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vdf = ResolveLibraryVdfPath(steamRoot);
            if (vdf == null) throw new FileNotFoundException("libraryfolders.vdf not found under " + steamRoot);

            foreach (var lib in ParseLibraryFolders(vdf))
                if (Directory.Exists(lib)) libs.Add(lib);

            // ensure root itself is considered
            libs.Add(steamRoot);

            var games = new List<GameEntry>();
            foreach (var lib in libs)
            {
                var last = Path.GetFileName(lib.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var steamapps = string.Equals(last, "steamapps", StringComparison.OrdinalIgnoreCase) ? lib : Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamapps)) continue;
                foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                {
                    var ge = ParseAppManifest(manifest);
                    if (ge != null) games.Add(ge);
                }
            }
            // de-dup by ManifestPath and sort
            return games
                .GroupBy(g => g.ManifestPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void SetManifestReadOnly(string path, bool readOnly, bool backupIfMissing = true)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Manifest not found", path);

            var bak = path + ".bak";
            if (backupIfMissing && !File.Exists(bak))
                File.Copy(path, bak, overwrite: false);

            var fi = new FileInfo(path);
            fi.IsReadOnly = readOnly;
        }

        private static IEnumerable<string> ParseLibraryFolders(string vdfPath)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string text = File.ReadAllText(vdfPath);

            // pattern: "path"  "C:\SteamLibrary"
            foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
            {
                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(p)) results.Add(p);
            }
            // fallback: any quoted windows path "C:\..."
            foreach (Match m in Regex.Matches(text, "\"([A-Za-z]:\\\\[^\"\\r\\n]+)\""))
            {
                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(p)) results.Add(p);
            }
            return results;
        }

        private static GameEntry? ParseAppManifest(string manifestPath)
        {
            try
            {
                var text = File.ReadAllText(manifestPath);
                var app = Regex.Match(text, "\"appid\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
                var name = Regex.Match(text, "\"name\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                var appid = app.Success ? app.Groups[1].Value : string.Empty;
                var gname = name.Success ? name.Groups[1].Value : Path.GetFileNameWithoutExtension(manifestPath);
                return new GameEntry { AppId = appid, Name = gname, ManifestPath = manifestPath };
            }
            catch { return null; }
        }
    }
}
