using DiscordRPC;
using DiscordRPC.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

int pid = 0;
string placeId = "";
string year = "2016";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--pid": pid = int.Parse(args[++i]); break;
        case "--placeid": placeId = args[++i]; break;
        case "--year": year = args[++i]; break;
    }
}

var (gameName, creatorName, iconUrl) = GetGameInfo(placeId);

var client = new DiscordRpcClient("1474893329513840780");
client.Logger = new NullLogger();
client.Initialize();

client.SetPresence(new RichPresence()
{
    Details = $"{gameName} ({year})",
    State = $"By: {creatorName}",
    Timestamps = Timestamps.Now,
    Assets = new Assets()
    {
        LargeImageKey = !string.IsNullOrEmpty(iconUrl) ? iconUrl : "kornet_logo",
        LargeImageText = gameName,
    }
});

var listenerThread = new Thread(() => StartHttpListener(client));
listenerThread.IsBackground = true;
listenerThread.Start();

try
{
    if (pid > 0)
    {
        var process = Process.GetProcessById(pid);
        process.WaitForExit();
    }
    else
    {
        Thread.Sleep(Timeout.Infinite);
    }
}
catch { }

client.ClearPresence();
client.Dispose();

static void StartHttpListener(DiscordRpcClient client)
{
    try
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:6464/rpc/");
        listener.Start();

        while (true)
        {
            try
            {
                var context = listener.GetContext();
                var request = context.Request;
                var response = context.Response;

                using var reader = new StreamReader(request.InputStream);
                var body = reader.ReadToEnd();

                if (request.Url?.AbsolutePath == "/rpc/setPresence")
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    var details = root.TryGetProperty("details", out var d) ? d.GetString() : null;
                    var state = root.TryGetProperty("state", out var s) ? s.GetString() : null;
                    var largeImage = root.TryGetProperty("largeImage", out var li) ? li.GetString() : null;
                    var largeText = root.TryGetProperty("largeText", out var lt) ? lt.GetString() : null;

                    client.SetPresence(new RichPresence()
                    {
                        Details = details,
                        State = state,
                        Timestamps = Timestamps.Now,
                        Assets = new Assets()
                        {
                            LargeImageKey = !string.IsNullOrEmpty(largeImage) ? largeImage : "kornet_logo",
                            LargeImageText = largeText ?? details,
                        }
                    });
                }
                else if (request.Url?.AbsolutePath == "/rpc/clearPresence")
                {
                    client.ClearPresence();
                }
                else if (request.Url?.AbsolutePath == "/rpc/ping")
                {
                    var responseBytes = System.Text.Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                    response.ContentType = "application/json";
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }

                response.StatusCode = 200;
                response.Close();
            }
            catch { }
        }
    }
    catch { }
}

static (string gameName, string creatorName, string iconUrl) GetGameInfo(string placeId)
{
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) KornetRPC/1.0");

        var detailsJson = http.GetStringAsync(
            $"https://kornet.lat/apisite/games/v1/games/multiget-place-details?placeIds={placeId}"
        ).Result;

        using var detailsDoc = JsonDocument.Parse(detailsJson);
        var root = detailsDoc.RootElement[0];
        var gameName = root.GetProperty("name").GetString() ?? "Kornet";
        var creatorName = root.GetProperty("builder").GetString() ?? "Unknown";
        var builderId = root.GetProperty("builderId").GetInt64();
        var universeId = root.GetProperty("universeId").GetInt64();

        var userJson = http.GetStringAsync(
            $"https://kornet.lat/apisite/users/v1/users/{builderId}"
        ).Result;

        using var userDoc = JsonDocument.Parse(userJson);
        var isVerified = userDoc.RootElement.GetProperty("isVerified").GetBoolean();
        if (isVerified)
            creatorName = creatorName + " ☑️";

        var iconJson = http.GetStringAsync(
            $"https://kornet.lat/apisite/thumbnails/v1/games/icons?universeIds={universeId}"
        ).Result;

        using var iconDoc = JsonDocument.Parse(iconJson);
        var imagePath = iconDoc.RootElement
            .GetProperty("data")[0]
            .GetProperty("imageUrl")
            .GetString();

        string finalIcon = "";
        if (!string.IsNullOrEmpty(imagePath) && !imagePath.Contains("placeholder"))
            finalIcon = "https://kornet.lat" + imagePath;

        return (gameName, creatorName, finalIcon);
    }
    catch { }

    return ("Kornet", "Unknown", "");
}