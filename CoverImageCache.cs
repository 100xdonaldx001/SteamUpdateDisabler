using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SteamManifestToggler
{
    public static class CoverImageCache
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        private static readonly ConcurrentDictionary<string, Task> InFlightDownloads = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string CacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamUpdateDisabler",
            "covers");

        public static string? GetCoverImagePath(string? appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return null;

            try
            {
                var targetPath = Path.Combine(CacheFolder, appId + ".jpg");
                if (File.Exists(targetPath)) return targetPath;

                QueueDownload(appId, targetPath);
            }
            catch
            {
                // ignore network/cache errors and fall back to online URL
            }

            return null;
        }

        private static void QueueDownload(string appId, string targetPath)
        {
            _ = InFlightDownloads.GetOrAdd(appId, _ => DownloadAndPersistAsync(appId, targetPath));
        }

        private static async Task DownloadAndPersistAsync(string appId, string targetPath)
        {
            var url = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
            var tmpPath = targetPath + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await DownloadFile(url, tmpPath).ConfigureAwait(false);

                if (!File.Exists(tmpPath)) return;

                if (new FileInfo(tmpPath).Length == 0)
                {
                    File.Delete(tmpPath);
                    return;
                }

                File.Move(tmpPath, targetPath, overwrite: true);
            }
            catch
            {
                // ignore download errors
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpPath))
                    {
                        File.Delete(tmpPath);
                    }
                }
                catch
                {
                    // ignore cleanup errors
                }

                InFlightDownloads.TryRemove(appId, out Task _);
            }
        }

        private static async Task DownloadFile(string url, string destination)
        {
            try
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return;

                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var fs = File.Create(destination);
                await stream.CopyToAsync(fs).ConfigureAwait(false);
            }
            catch
            {
                // ignore download errors
            }
        }
    }
}