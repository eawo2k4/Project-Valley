using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    public enum UpdateChannel
    {
        Release,
        Experimental
    }

    public enum UpdateRecompiler
    {
        NDR,
        ODR
    }

    /// <summary>
    /// Schema for the active and packaged engine manifest.
    /// </summary>
    public class CoreManifest
    {
        [JsonPropertyName("activeDirectory")]
        public string ActiveDirectory { get; set; } = string.Empty;

        [JsonPropertyName("channel")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UpdateChannel Channel { get; set; } = UpdateChannel.Release;

        [JsonPropertyName("recompiler")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UpdateRecompiler Recompiler { get; set; } = UpdateRecompiler.NDR;

        [JsonPropertyName("buildNumber")]
        public string BuildNumber { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("sha256Checksum")]
        public string Sha256Checksum { get; set; } = string.Empty;

        [JsonPropertyName("installedAt")]
        public DateTime? InstalledAt { get; set; }
    }

    public class UpdateAvailableEventArgs : EventArgs
    {
        public CoreManifest Update { get; }
        public bool IsManualCheck { get; }

        public UpdateAvailableEventArgs(CoreManifest update, bool isManualCheck)
        {
            Update = update;
            IsManualCheck = isManualCheck;
        }
    }

    public class UpdateUpToDateEventArgs : EventArgs
    {
        public bool IsManualCheck { get; }

        public UpdateUpToDateEventArgs(bool isManualCheck)
        {
            IsManualCheck = isManualCheck;
        }
    }

    public class UpdateCheckFailedEventArgs : EventArgs
    {
        public string Message { get; }
        public bool IsManualCheck { get; }

        public UpdateCheckFailedEventArgs(string message, bool isManualCheck)
        {
            Message = message;
            IsManualCheck = isManualCheck;
        }
    }

    /// <summary>
    /// Implements the manifest-driven update pipeline for 86Box core engines.
    /// Handles querying GitHub Releases API, stream-downloading to %TEMP% with IProgress,
    /// SHA-256 verification, isolated version extraction, and active manifest.json pointer management.
    /// </summary>
    public sealed class CoreUpdateService
    {
        private static readonly Lazy<CoreUpdateService> _instance = new(() => new CoreUpdateService());
        public static CoreUpdateService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private readonly string _managerConfigDir;
        private readonly string _managerConfigPath;
        private readonly string _manifestPath;
        private readonly string _enginesDir;

        public event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;
        public event EventHandler<UpdateUpToDateEventArgs>? UpToDate;
        public event EventHandler<UpdateCheckFailedEventArgs>? UpdateCheckFailed;
        public event EventHandler<double>? DownloadProgressChanged;
        public event EventHandler<CoreManifest>? UpdateInstalled;

        public UpdateChannel Channel { get; set; } = UpdateChannel.Release;
        public UpdateRecompiler Recompiler { get; set; } = UpdateRecompiler.NDR;

        /// <summary>
        /// Placeholder: no release pipeline exists for this fork yet. Left blank rather than
        /// defaulting to the upstream "86Box/86Box" repo - that repo doesn't publish a bare
        /// 86box_core.dll (only Qt/SDL builds this WinUI shell can't use), and nothing here
        /// applies a downloaded update to the running app yet, so pointing at it would just let
        /// "Check for updates" silently fetch the wrong binary. Set via the "[Updates] github_repo"
        /// ini key once a repo/CI that actually publishes 86box_core.dll release assets exists.
        /// </summary>
        public string GitHubRepo { get; set; } = "";
        public DateTime? LastCheckedTime { get; private set; }
        public CoreManifest? AvailableUpdate { get; private set; }
        public bool IsChecking { get; private set; }
        public bool IsDownloading { get; private set; }
        public double DownloadProgress { get; private set; }
        public bool IsUpdateReadyToRestart { get; private set; }

        public CoreUpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("86Box-WinUI-Updater", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            _managerConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "86Box");
            _managerConfigPath = Path.Combine(_managerConfigDir, "86box.manager.ini");
            _enginesDir = Path.Combine(_managerConfigDir, "engines");
            _manifestPath = Path.Combine(_managerConfigDir, "manifest.json");

            LoadSettings();

            // A download that completed but was never applied (the app was closed before "Restart
            // to apply" was clicked) would otherwise be silently forgotten - IsUpdateReadyToRestart
            // was in-memory only. TryApplyPendingUpdate() deletes the manifest once it actually hands
            // off to the apply script, so finding one here means a real, not-yet-applied download.
            var pending = GetActiveManifest();
            if (pending != null && !string.IsNullOrWhiteSpace(pending.ActiveDirectory) && Directory.Exists(pending.ActiveDirectory))
            {
                AvailableUpdate = pending;
                IsUpdateReadyToRestart = true;
            }
        }

        public string GetEnginesDirectory() => _enginesDir;
        public string GetManifestPath() => _manifestPath;

        public CoreManifest? GetActiveManifest()
        {
            try
            {
                if (File.Exists(_manifestPath))
                {
                    string json = File.ReadAllText(_manifestPath);
                    return JsonSerializer.Deserialize<CoreManifest>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateService] Failed to read active manifest: {ex.Message}");
            }
            return null;
        }

        public void SaveActiveManifest(CoreManifest manifest)
        {
            try
            {
                if (!Directory.Exists(_managerConfigDir))
                    Directory.CreateDirectory(_managerConfigDir);

                string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_manifestPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateService] Failed to write active manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies the downloaded update's DLLs over this app's own copy and relaunches it. A DLL the
        /// current process has loaded (86box_core.dll included) can't just be overwritten while still
        /// running - Windows locks it, the same "file in use" sharing violation as deleting a VM whose
        /// engine hasn't fully released its files - so this hands off to a tiny external script that
        /// waits for THIS process to actually exit before touching anything, then relaunches the app.
        /// Returns true if the hand-off script was launched (the caller should exit immediately after);
        /// false if there was nothing to apply or the hand-off couldn't be started, in which case the
        /// caller should fall back to a plain restart.
        /// </summary>
        public bool TryApplyPendingUpdate()
        {
            var manifest = GetActiveManifest();
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.ActiveDirectory) || !Directory.Exists(manifest.ActiveDirectory))
                return false;

            try
            {
                string appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
                string exePath = Path.Combine(appDir, "Box86.WinUI.exe");
                if (!File.Exists(exePath))
                    return false;

                string scriptPath = Path.Combine(Path.GetTempPath(), $"86box_apply_update_{Guid.NewGuid():N}.ps1");
                static string Ps(string s) => s.Replace("'", "''");

                string script =
                    "$ErrorActionPreference = 'SilentlyContinue'\n" +
                    $"$deadline = (Get-Date).AddSeconds(20)\n" +
                    $"while ((Get-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {{ Start-Sleep -Milliseconds 200 }}\n" +
                    $"Copy-Item -Path '{Ps(manifest.ActiveDirectory)}\\*.dll' -Destination '{Ps(appDir)}' -Force\n" +
                    $"Start-Process -FilePath '{Ps(exePath)}'\n" +
                    $"Remove-Item -Path '{Ps(scriptPath)}' -Force\n";
                File.WriteAllText(scriptPath, script);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                // Once the hand-off script has been launched, this update is being applied - clear
                // the manifest so the next launch's constructor check doesn't offer it forever.
                try { File.Delete(_manifestPath); } catch { }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CoreUpdateService] Failed to launch update hand-off: {ex.Message}");
                return false;
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_managerConfigPath))
                {
                    var config = MachineConfig.Load(_managerConfigPath);
                    string channelStr = config.GetString("Updates", "channel", "Release");
                    if (Enum.TryParse<UpdateChannel>(channelStr, true, out var ch))
                        Channel = ch;

                    string recompStr = config.GetString("Updates", "recompiler", "NDR");
                    if (Enum.TryParse<UpdateRecompiler>(recompStr, true, out var rc))
                        Recompiler = rc;

                    string lastChecked = config.GetString("Updates", "last_checked", "");
                    if (DateTime.TryParse(lastChecked, out var dt))
                        LastCheckedTime = dt;

                    string customRepo = config.GetString("Updates", "github_repo", "");
                    if (!string.IsNullOrWhiteSpace(customRepo))
                        GitHubRepo = customRepo.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateService] Failed to load update settings: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(_managerConfigDir))
                    Directory.CreateDirectory(_managerConfigDir);

                var config = File.Exists(_managerConfigPath)
                    ? MachineConfig.Load(_managerConfigPath)
                    : new MachineConfig();

                config.SetString("Updates", "channel", Channel.ToString());
                config.SetString("Updates", "recompiler", Recompiler.ToString());
                if (LastCheckedTime.HasValue)
                    config.SetString("Updates", "last_checked", LastCheckedTime.Value.ToString("o"));
                if (!string.IsNullOrWhiteSpace(GitHubRepo))
                    config.SetString("Updates", "github_repo", GitHubRepo);

                config.Save(_managerConfigPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateService] Failed to save update settings: {ex.Message}");
            }
        }

        public async Task<CoreManifest?> CheckForUpdatesAsync(bool manualCheck = true, CancellationToken cancellationToken = default)
        {
            if (IsChecking)
                return AvailableUpdate;

            if (string.IsNullOrWhiteSpace(GitHubRepo))
            {
                UpdateCheckFailed?.Invoke(this, new UpdateCheckFailedEventArgs("Update publishing isn't set up for this build yet - no release repo is configured.", manualCheck));
                return null;
            }

            IsChecking = true;
            try
            {
                SaveSettings();
                string url = $"https://api.github.com/repos/{GitHubRepo}/releases?per_page=15";
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    UpdateCheckFailed?.Invoke(this, new UpdateCheckFailedEventArgs("Invalid response from GitHub Releases API.", manualCheck));
                    return null;
                }

                CoreManifest? matchingManifest = null;
                var activeManifest = GetActiveManifest();

                foreach (var release in doc.RootElement.EnumerateArray())
                {
                    bool isPrerelease = release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
                    bool isDraft = release.TryGetProperty("draft", out var draft) && draft.GetBoolean();
                    if (isDraft)
                        continue;

                    // Channel filtering:
                    // Release: must not be a prerelease
                    // Experimental: accepts prereleases or latest release
                    if (Channel == UpdateChannel.Release && isPrerelease)
                        continue;

                    string tagName = release.TryGetProperty("tag_name", out var tag) ? tag.GetString() ?? "" : "";
                    string releaseName = release.TryGetProperty("name", out var rname) ? rname.GetString() ?? "" : tagName;
                    string releaseBody = release.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                    string version = ExtractVersion(tagName, releaseName);
                    string buildId = ExtractBuildNumber(tagName, releaseName, version);

                    // Check if release contains assets
                    if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                        continue;

                    string downloadUrl = string.Empty;
                    string sha256Checksum = string.Empty;

                    // 1. First check if a direct manifest asset exists (e.g. manifest.json or manifest-{recompiler}.json)
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                        if (assetName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                            assetName.Equals($"manifest-{Recompiler}.json", StringComparison.OrdinalIgnoreCase))
                        {
                            if (asset.TryGetProperty("browser_download_url", out var assetUrl))
                            {
                                try
                                {
                                    string manifestJson = await _httpClient.GetStringAsync(assetUrl.GetString()!, cancellationToken);
                                    var customManifest = JsonSerializer.Deserialize<CoreManifest>(manifestJson);
                                    if (customManifest != null)
                                    {
                                        customManifest.Channel = Channel;
                                        matchingManifest = customManifest;
                                        break;
                                    }
                                }
                                catch
                                {
                                    // Fallback to standard asset resolution
                                }
                            }
                        }
                    }

                    if (matchingManifest != null)
                        break;

                    // 2. Otherwise match binary asset by platform (windows/x64) and recompiler
                    string targetRecompilerTag = Recompiler.ToString().ToLowerInvariant();
                    string fallbackUrl = string.Empty;

                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                        string lowerName = assetName.ToLowerInvariant();

                        // Look for checksum file if present
                        if (lowerName.EndsWith(".sha256") || lowerName.EndsWith(".sha256sum"))
                        {
                            if (asset.TryGetProperty("browser_download_url", out var chkUrl))
                            {
                                try
                                {
                                    string chkContent = await _httpClient.GetStringAsync(chkUrl.GetString()!, cancellationToken);
                                    sha256Checksum = ExtractSha256(chkContent);
                                }
                                catch { }
                            }
                        }

                        // Match zip assets for Windows 64-bit
                        if (lowerName.EndsWith(".zip") &&
                            (lowerName.Contains("win") || lowerName.Contains("windows")) &&
                            (lowerName.Contains("x64") || lowerName.Contains("64") || lowerName.Contains("x86_64")))
                        {
                            if (asset.TryGetProperty("browser_download_url", out var dl))
                            {
                                string candidateUrl = dl.GetString() ?? "";
                                if (lowerName.Contains(targetRecompilerTag))
                                {
                                    downloadUrl = candidateUrl;
                                }
                                else if (string.IsNullOrEmpty(fallbackUrl))
                                {
                                    fallbackUrl = candidateUrl;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        downloadUrl = fallbackUrl;
                    }

                    if (string.IsNullOrEmpty(sha256Checksum))
                    {
                        sha256Checksum = ExtractSha256(releaseBody);
                    }

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        matchingManifest = new CoreManifest
                        {
                            Channel = Channel,
                            Recompiler = Recompiler,
                            BuildNumber = buildId,
                            Version = version,
                            DownloadUrl = downloadUrl,
                            Sha256Checksum = sha256Checksum
                        };
                        break;
                    }
                }

                LastCheckedTime = DateTime.Now;
                SaveSettings();

                if (matchingManifest != null && IsNewerThanActive(matchingManifest, activeManifest))
                {
                    AvailableUpdate = matchingManifest;
                    UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(matchingManifest, manualCheck));
                    return matchingManifest;
                }
                else
                {
                    AvailableUpdate = null;
                    UpToDate?.Invoke(this, new UpdateUpToDateEventArgs(manualCheck));
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateService] Update check failed: {ex.Message}");
                UpdateCheckFailed?.Invoke(this, new UpdateCheckFailedEventArgs(ex.Message, manualCheck));
                return null;
            }
            finally
            {
                IsChecking = false;
            }
        }

        public async Task<bool> DownloadAndApplyUpdateAsync(
            CoreManifest manifest,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDownloading)
                return false;

            IsDownloading = true;
            DownloadProgress = 0;
            string tempZipPath = Path.Combine(Path.GetTempPath(), $"86Box_update_{Guid.NewGuid():N}.zip");

            try
            {
                // 1. Stream download to %TEMP% with IProgress<double> reporting
                using (var response = await _httpClient.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            double percentage = Math.Min(100.0, (double)totalRead / totalBytes * 100.0);
                            DownloadProgress = percentage;
                            progress?.Report(percentage);
                            DownloadProgressChanged?.Invoke(this, percentage);
                        }
                    }
                }

                // 2. Verify SHA-256 before extraction
                string computedSha256 = ComputeSha256(tempZipPath);
                if (!string.IsNullOrWhiteSpace(manifest.Sha256Checksum))
                {
                    if (!computedSha256.Equals(manifest.Sha256Checksum.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"SHA-256 verification failed. Expected: {manifest.Sha256Checksum}, Actual: {computedSha256}");
                    }
                }
                else
                {
                    manifest.Sha256Checksum = computedSha256;
                }

                // 3. Extract into isolated versioned directory (e.g. engines/{channel}-{recompiler}_{buildId})
                string folderName = $"{manifest.Channel}-{manifest.Recompiler}_{manifest.BuildNumber}";
                string targetDir = Path.Combine(_enginesDir, folderName);

                if (!Directory.Exists(_enginesDir))
                    Directory.CreateDirectory(_enginesDir);

                if (Directory.Exists(targetDir))
                {
                    try { Directory.Delete(targetDir, true); } catch { }
                }
                Directory.CreateDirectory(targetDir);

                ZipFile.ExtractToDirectory(tempZipPath, targetDir, overwriteFiles: true);

                // 4. Update manifest.json active directory pointer
                manifest.ActiveDirectory = targetDir;
                manifest.InstalledAt = DateTime.UtcNow;
                SaveActiveManifest(manifest);

                IsUpdateReadyToRestart = true;
                UpdateInstalled?.Invoke(this, manifest);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateService] Download & Apply update failed: {ex.Message}");
                throw;
            }
            finally
            {
                IsDownloading = false;
                try
                {
                    if (File.Exists(tempZipPath))
                        File.Delete(tempZipPath);
                }
                catch { }
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string ExtractSha256(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var match = Regex.Match(text, @"\b([a-fA-F0-9]{64})\b");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string ExtractVersion(string tagName, string releaseName)
        {
            var match = Regex.Match(tagName, @"v?(\d+\.\d+(\.\d+)?)");
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(releaseName, @"v?(\d+\.\d+(\.\d+)?)");
            if (match.Success)
                return match.Groups[1].Value;

            return tagName;
        }

        private static string ExtractBuildNumber(string tagName, string releaseName, string fallbackVersion)
        {
            var match = Regex.Match(tagName, @"b(\d+)");
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(releaseName, @"b(\d+)");
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(releaseName, @"Build\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            return fallbackVersion.Replace(".", "");
        }

        private static bool IsNewerThanActive(CoreManifest candidate, CoreManifest? active)
        {
            if (active == null)
                return true;

            if (candidate.Channel != active.Channel || candidate.Recompiler != active.Recompiler)
                return true;

            if (long.TryParse(candidate.BuildNumber, out var candBuild) &&
                long.TryParse(active.BuildNumber, out var actBuild))
            {
                return candBuild > actBuild;
            }

            if (Version.TryParse(candidate.Version, out var candVer) &&
                Version.TryParse(active.Version, out var actVer))
            {
                return candVer > actVer;
            }

            return !string.Equals(candidate.BuildNumber, active.BuildNumber, StringComparison.OrdinalIgnoreCase);
        }
    }
}
