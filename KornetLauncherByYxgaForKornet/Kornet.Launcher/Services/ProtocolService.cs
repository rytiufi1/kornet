using Microsoft.Win32;

namespace Kornet.Launcher.Services;

public class ProtocolService
{
    private readonly string _exePath;

    public ProtocolService(string exePath)
    {
        _exePath = exePath;
    }

    public void Register()
    {
        try
        {
            using var subKey1 = Registry.CurrentUser.CreateSubKey("Software\\Classes\\kornetclient");
            subKey1.SetValue("", "URL:Kornet Protocol");
            subKey1.SetValue("URL Protocol", "");
            using var subKey2 = subKey1.CreateSubKey("DefaultIcon");
            subKey2.SetValue("", $"\"{_exePath}\",1");
            using var subKey3 = subKey1.CreateSubKey("shell\\open\\command");
            subKey3.SetValue("", $"\"{_exePath}\" \"%1\"");
        }
        catch { }
    }
}