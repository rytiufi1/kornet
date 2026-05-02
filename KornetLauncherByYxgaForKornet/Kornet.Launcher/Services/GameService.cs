using Kornet.Shared.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Kornet.Launcher.Services;

public class GameService
{
    private readonly string _appData;
    private readonly string _year;

    public Process? GameProcess { get; private set; }
    public Thread? AntiCheatThread { get; private set; }
    public Action<string>? OnKillNotify;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isPresent);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    public GameService(string appData, string year)
    {
        _appData = appData;
        _year = year;
    }

    public async Task<GameInfo> FetchGameInfo(string placeId)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var response = await client.GetStringAsync(
                $"https://kornet.lat/apisite/games/v1/games/multiget-place-details?placeIds={placeId}"
            );
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement[0];
            return new GameInfo
            {
                Name = root.GetProperty("name").GetString() ?? "Kornet",
                Builder = root.GetProperty("builder").GetString() ?? "",
                PlaceId = placeId,
                Year = _year
            };
        }
        catch { }
        return new GameInfo { Name = "Kornet", Builder = "", PlaceId = placeId, Year = _year };
    }

    public void Launch(string placeId, string ticket, GameInfo gameInfo, string versionedPath)
    {
        var clientFolder = Path.Combine(versionedPath, _year);
        var clientExe = Path.Combine(clientFolder, "KornetPlayerBeta.exe");

        string authUrl = "https://kornet.lat/Login/Negotiate.ashx";
        string joinUrl = _year switch
        {
            "2020" => $"http://kornet.lat/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}&2020=true",
            "2018" => $"http://kornet.lat/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}&2018=true",
            _ => $"http://kornet.lat/game/PlaceLauncher.ashx?placeid={placeId}&ticket={ticket}"
        };

        GameProcess = Process.Start(new ProcessStartInfo()
        {
            FileName = clientExe,
            Arguments = $"-a \"{authUrl}\" -j \"{joinUrl}\" -t \"{ticket}\"",
            WorkingDirectory = clientFolder,
            UseShellExecute = true
        });

        var rpcExe = Path.Combine(versionedPath, "KornetRPC.exe");
        if (File.Exists(rpcExe) && GameProcess != null)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = rpcExe,
                Arguments = $"--pid {GameProcess.Id} --placeid {placeId} --year {_year}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        AntiCheatThread = new Thread(() => AntiCheatMonitor(GameProcess!))
        {
            IsBackground = true,
            Name = "kornetthehornet"
        };
        AntiCheatThread.Start();
    }

    private void AntiCheatMonitor(Process proc)
    {
        while (!proc.HasExited)
        {
            try
            {
                proc.Refresh();
                bool isPresent = false;
                if (CheckRemoteDebuggerPresent(proc.Handle, ref isPresent) & isPresent)
                {
                    OnKillNotify?.Invoke("Debugger detected attempting to attach to the client process.");
                    break;
                }
                if (IsProcessMemoryAccessed(proc.Id))
                {
                    OnKillNotify?.Invoke("External application detected accessing client memory.");
                    break;
                }
            }
            catch { }
            Thread.Sleep(500);
        }
    }

    private bool IsProcessMemoryAccessed(int processId)
    {
        IntPtr hObject = OpenProcess(56U, false, processId);
        if (hObject != IntPtr.Zero)
        {
            CloseHandle(hObject);
            return false;
        }
        return Marshal.GetLastWin32Error() == 5;
    }
}