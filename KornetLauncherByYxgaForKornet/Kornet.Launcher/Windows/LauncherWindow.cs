using Kornet.Launcher.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kornet.Launcher.Windows;

public class LauncherWindow : LauncherForm
{
    private readonly string _appData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kornet");
    private readonly string? _placeId;
    private readonly string? _ticket;
    private readonly string _year;
    private readonly InstallService _installService;
    private readonly GameService _gameService;

#if DEBUG
    private DebugWindow? _debugWindow;
#endif

    public LauncherWindow(string? placeId, string? ticket, string year)
    {
        _placeId = placeId;
        _ticket = ticket;
        _year = year;
        _installService = new InstallService(_appData);
        _gameService = new GameService(_appData, _year);

        _installService.OnStatusChanged += text => Invoke(() =>
        {
            StatusText.Text = text;
#if DEBUG
            _debugWindow?.Log($"[status] {text}");
#endif
        });
        _installService.OnProgressChanged += val => Invoke(() =>
        {
            ProgressBar.Style = ProgressBarStyle.Continuous;
            ProgressBar.Value = val;
#if DEBUG
            _debugWindow?.Log($"[progress] {val}%");
#endif
        });
        _installService.OnProgressIndeterminate += val => Invoke(() =>
        {
            ProgressBar.Style = val ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
#if DEBUG
            _debugWindow?.Log($"[progress] indeterminate={val}");
#endif
        });
        _installService.OnRequestLog += msg => Invoke(() =>
        {
#if DEBUG
            _debugWindow?.Log(msg);
#endif
        });
        _gameService.OnKillNotify += reason => Invoke(() =>
        {
            MessageBox.Show(
                $"Kornet The Hornet has detected a third party application.\nReason: {reason}\n\nYou will now be exited.",
                "Kornet - Kornet The Hornet", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            Application.Exit();
        });

        Load += (s, e) =>
        {
#if DEBUG
            _debugWindow = new DebugWindow();
            _debugWindow.Show();
#endif
            Task.Run(StartLauncher);
        };
    }

    private async Task StartLauncher()
    {
        await _installService.InstallAllClients(_year);

        var protocolService = new ProtocolService(
            Path.Combine(_installService.VersionedPath!, "Kornet.exe")
        );
        protocolService.Register();

        if (!string.IsNullOrEmpty(_placeId) && !string.IsNullOrEmpty(_ticket))
        {
            Invoke(() => StatusText.Text = $"Launching Kornet ({_year})...");
            var gameInfo = await _gameService.FetchGameInfo(_placeId);
            _gameService.Launch(_placeId, _ticket, gameInfo, _installService.VersionedPath!);
            await Task.Delay(3000);
            Invoke(Close);
        }
        else
        {
            Invoke(Close);
        }
    }
}