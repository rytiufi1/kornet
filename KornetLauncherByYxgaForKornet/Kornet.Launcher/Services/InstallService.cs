using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kornet.Launcher.Services;

public class InstallService
{
    private readonly string _appData;
    private readonly string _versionsPath;
    private readonly string _downloadsPath;

    private const string DeployHistoryUrl = "https://setup.kornet.lat/DeployHistory.txt";
    private const string LauncherExeUrl = "https://setup.kornet.lat/KornetPlayerLauncher.exe";

    private static readonly Dictionary<string, string> ClientUrls = new()
    {
        ["2014"] = "https://setup.kornet.lat/KClient14.zip",
        ["2015"] = "https://setup.kornet.lat/KClient15.zip",
        ["2016"] = "https://setup.kornet.lat/KClient16.zip",
        ["2017"] = "https://setup.kornet.lat/KClient17.zip",
        ["2018"] = "https://setup.kornet.lat/KClient18.zip",
        ["2020"] = "https://setup.kornet.lat/KClient20.zip",
        ["2021"] = "https://setup.kornet.lat/KClient21.zip",
    };

    private static readonly Dictionary<string, string> DeployHistoryNames = new()
    {
        ["2014"] = "KornetPlayer2014",
        ["2015"] = "KornetPlayer2015",
        ["2016"] = "KornetPlayer2016",
        ["2017"] = "KornetPlayer2017",
        ["2018"] = "KornetPlayer2018",
        ["2020"] = "KornetPlayer2020",
        ["2021"] = "KornetPlayer2021",
    };

    public Action<string>? OnStatusChanged;
    public Action<int>? OnProgressChanged;
    public Action<bool>? OnProgressIndeterminate;
    public Action<string>? OnRequestLog;

    public string? VersionedPath { get; private set; }

    public InstallService(string appData)
    {
        _appData = appData;
        _versionsPath = Path.Combine(appData, "Versions");
        _downloadsPath = Path.Combine(appData, "Downloads");
    }

    public async Task InstallAllClients(string year)
    {
        Directory.CreateDirectory(_versionsPath);
        Directory.CreateDirectory(_downloadsPath);

        OnStatusChanged?.Invoke("Checking for updates...");
        OnProgressIndeterminate?.Invoke(true);

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(60);

        OnRequestLog?.Invoke($"GET {DeployHistoryUrl}");
        var deployHistory = await http.GetStringAsync(DeployHistoryUrl);
        OnRequestLog?.Invoke($"200 OK {DeployHistoryUrl}");

        var launcherHash = ParseLatestHash(deployHistory, "KornetPlayerLauncher") ?? "version-current";
        var versionedPath = Path.Combine(_versionsPath, launcherHash);
        VersionedPath = versionedPath;
        Directory.CreateDirectory(versionedPath);

        OnRequestLog?.Invoke($"[version] launcher={launcherHash}");

        // clean up old version folders
        foreach (var dir in Directory.GetDirectories(_versionsPath))
        {
            if (!dir.Equals(versionedPath, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // launcher exe
        var launcherExeDest = Path.Combine(versionedPath, "Kornet.exe");
        if (!File.Exists(launcherExeDest))
            await DownloadFile(http, "Launcher", LauncherExeUrl, launcherExeDest);

        // rpc exe
        var rpcExeDest = Path.Combine(versionedPath, "KornetRPC.exe");
        if (!File.Exists(rpcExeDest))
        {
            OnRequestLog?.Invoke("[rpc] extracting KornetRPC.exe from embedded resources");
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("KornetRPC.exe");
            if (stream != null)
            {
                using var fs = File.Create(rpcExeDest);
                await stream.CopyToAsync(fs);
                OnRequestLog?.Invoke("[rpc] KornetRPC.exe extracted");
            }
        }

        // install all year clients
        foreach (var (clientYear, clientUrl) in ClientUrls)
        {
            if (!DeployHistoryNames.TryGetValue(clientYear, out var deployName))
                continue;

            var clientHash = ParseLatestHash(deployHistory, deployName);
            var clientFolder = Path.Combine(versionedPath, clientYear);
            var hashFile = Path.Combine(clientFolder, ".hash");


            if (clientHash == null)
            {
                OnRequestLog?.Invoke($"[skip] {clientYear} not in deployhistory");
                continue;
            }

            bool needsInstall = !Directory.Exists(clientFolder)
                || !File.Exists(hashFile)
                || File.ReadAllText(hashFile).Trim() != clientHash;

            if (!needsInstall)
            {
                OnRequestLog?.Invoke($"[skip] {clientYear} client up to date");
                continue;
            }

            OnRequestLog?.Invoke($"HEAD {clientUrl}");
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, clientUrl);
            using var headResponse = await http.SendAsync(headRequest);
            OnRequestLog?.Invoke($"{(int)headResponse.StatusCode} {headResponse.StatusCode} {clientUrl}");

            if (!headResponse.IsSuccessStatusCode)
            {
                OnRequestLog?.Invoke($"[skip] {clientYear} client not available on cdn");
                continue;
            }

            if (Directory.Exists(clientFolder))
                Directory.Delete(clientFolder, true);

            var zipHash = clientHash.Replace("version-", "");
            var cachedZip = Path.Combine(_downloadsPath, zipHash);

            if (!File.Exists(cachedZip))
                await DownloadFile(http, $"{clientYear} Client", clientUrl, cachedZip);

            OnStatusChanged?.Invoke($"Installing {clientYear} Client...");
            OnProgressIndeterminate?.Invoke(true);
            OnRequestLog?.Invoke($"[extract] extracting {clientYear} client to {clientFolder}");

            await Task.Run(() =>
            {
                Directory.CreateDirectory(clientFolder);
                ZipFile.ExtractToDirectory(cachedZip, clientFolder);
                
                var subDirs = Directory.GetDirectories(clientFolder);
                if (subDirs.Length == 1 && Directory.GetFiles(clientFolder).Length == 0)
                {
                    var subDir = subDirs[0];
                    var tempDir = clientFolder + "_temp";
                    Directory.Move(subDir, tempDir);
                    Directory.Delete(clientFolder);
                    Directory.Move(tempDir, clientFolder);
                }
            });

            OnRequestLog?.Invoke("[extract] done");

            if (clientHash != null)
                await File.WriteAllTextAsync(hashFile, clientHash);
        }

        // clean up downloads folder
        foreach (var file in Directory.GetFiles(_downloadsPath))
        {
            try { File.Delete(file); } catch { }
        }
    }

    private async Task DownloadFile(HttpClient http, string label, string url, string dest)
    {
        OnRequestLog?.Invoke($"GET {url}");
        OnStatusChanged?.Invoke($"Downloading {label}...");
        OnProgressIndeterminate?.Invoke(false);
        OnProgressChanged?.Invoke(0);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        OnRequestLog?.Invoke($"{(int)response.StatusCode} {response.StatusCode} {url}");
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        var read = 0L;

        await using var src = await response.Content.ReadAsStreamAsync();
        await using var dest_ = File.Create(dest);

        int bytesRead;
        while ((bytesRead = await src.ReadAsync(buffer)) > 0)
        {
            await dest_.WriteAsync(buffer.AsMemory(0, bytesRead));
            read += bytesRead;
            if (total > 0)
                OnProgressChanged?.Invoke((int)(read * 100 / total));
        }

        OnRequestLog?.Invoke($"[done] {label} saved to {dest}");
    }

    private static string? ParseLatestHash(string deployHistory, string entryName)
    {
        string? last = null;
        foreach (var line in deployHistory.Split('\n'))
        {
            var match = Regex.Match(line, $@"New {Regex.Escape(entryName)} (version-[0-9a-fA-F]+)");
            if (match.Success)
                last = match.Groups[1].Value;
        }
        return last;
    }
}