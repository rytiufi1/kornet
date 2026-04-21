using System.Diagnostics;
using System.Net.Http;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Roblox;
using System.IO;
 
namespace Roblox.Rendering
{
    public static class CommandHandler
    {
public static async Task<Stream> RequestTextureThumbnail(long assetId, CancellationToken? ct = null) 
    => await RenderWithArgs("Texture", new object[] { MakeAssetUrl(assetId), "Png", 420, 420 }, "Png", ct);

public static async Task<Stream> RequestAssetThumbnail(long assetId, CancellationToken? ct = null) 
    => await RenderWithArgs("Asset", new object[] { MakeAssetUrl(assetId), "Png", 420, 420 }, "Png", ct);

public static async Task<Stream> RequestAssetMesh(long assetId, CancellationToken? ct = null) 
    => await RenderWithArgs("Mesh", new object[] { MakeAssetUrl(assetId) }, "Obj", ct);

public static async Task<Stream> RequestHeadThumbnail(long assetId, CancellationToken? ct = null) 
    => await RenderWithArgs("Head", new object[] { MakeAssetUrl(assetId), "Png", 420, 420 }, "Png", ct);

public static async Task<Stream> RequestAssetTeeShirt(long assetId, long contentId, CancellationToken? ct = null) 
    => await RenderWithArgs("TeeShirt", new object[] { MakeAssetUrl(contentId), "Png", 420, 420 }, "Png", ct);

        private static SemaphoreSlim Rcc2020Lock { get; } = new(1, 1);
        private static Process? rccProcess;
        private static int? rccPort;
        private static object rccLock { get; } = new();
        private static Random random { get; } = new();

        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        private static bool IsPortInUse(int port)
        {
            var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            try
            {
                tcp.Start();
                return true;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
            finally
            {
                try { tcp.Stop(); } catch { }
            }
        }

        private static int GetRandomPort()
        {
            lock (rccLock)
            {
                int port;
                do { port = random.Next(20000, 40000); }
                while (!IsPortInUse(port));
                return port;
            }
        }

        private static async Task<int> StartRccService()
        {
            lock (rccLock)
            {
                if (rccProcess != null && !rccProcess.HasExited && rccPort.HasValue)
                    return rccPort.Value;

                rccPort = GetRandomPort();
                var rccPath = Path.Combine(Roblox.Configuration.RccService2020Path, "RCCService.exe");
                if (!File.Exists(rccPath))
                    throw new Exception("RCCService.exe not found at: " + rccPath);

                rccProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = rccPath,
                        Arguments = $"-console -verbose -port {rccPort.Value}",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                    }
                };

                if (!rccProcess.Start())
                    throw new Exception("Failed to start RCC process");

                return rccPort.Value;
            }
        }

        private static async Task WaitForRccReady(int port)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await SendSoapRequest(port, "http://roblox.com/HelloWorld", $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body><HelloWorld xmlns=""http://roblox.com/"" /></soap:Body>
</soap:Envelope>");
                    return;
                }
                catch { await Task.Delay(500); }
            }
            throw new Exception($"RCC did not become ready on port {port} within 30 seconds");
        }

        private static async Task<string> SendSoapRequest(int port, string soapAction, string xmlBody)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{port}");
            request.Headers.Add("SOAPAction", soapAction);
            request.Content = new StringContent(xmlBody, Encoding.UTF8, "text/xml");
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task SendCloseJobRequest(int port, string jobId)
        {
            await SendSoapRequest(port, "http://roblox.com/CloseJob", $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/""
                   xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
                   xmlns:ns1=""http://roblox.com/"">
    <SOAP-ENV:Body>
        <ns1:CloseJob><ns1:jobID>{jobId}</ns1:jobID></ns1:CloseJob>
    </SOAP-ENV:Body>
</SOAP-ENV:Envelope>");
        }

        private static async Task<Stream> SendAndParse(int port, string jobId, object jsonSettings, string format, CancellationToken? cancellationToken)
        {
            var finalJson = JsonSerializer.Serialize(jsonSettings);
            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <OpenJob xmlns=""http://roblox.com/"">
            <job>
                <id>{jobId}</id>
                <expirationInSeconds>60</expirationInSeconds>
                <category>0</category>
                <cores>1</cores>
            </job>
            <script>
                <name>GameServer</name>
                <script>{finalJson}</script>
            </script>
            <arguments>
                <LuaValue><type>LUA_TNIL</type></LuaValue>
            </arguments>
        </OpenJob>
    </soap:Body>
</soap:Envelope>";

            var res = await SendSoapRequest(port, "http://roblox.com/OpenJob", xml);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(res);

            var ns = new XmlNamespaceManager(xmlDoc.NameTable);
            ns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            ns.AddNamespace("ns1", "http://roblox.com/");

            var nodes = xmlDoc.SelectNodes("//soap:Envelope/soap:Body/ns1:OpenJobResponse/ns1:OpenJobResult", ns);
            foreach (XmlNode node in nodes)
            {
                var typeNode = node.SelectSingleNode("ns1:type", ns);
                var valueNode = node.SelectSingleNode("ns1:value", ns);

                if (typeNode?.InnerText == "LUA_TSTRING" && !string.IsNullOrEmpty(valueNode?.InnerText))
                {
                    await SendCloseJobRequest(port, jobId);

                    if (format == "Obj")
                        return new MemoryStream(Encoding.UTF8.GetBytes(valueNode.InnerText));

                    try
                    {
                        return new MemoryStream(Convert.FromBase64String(valueNode.InnerText));
                    }
                    catch (FormatException) { continue; }
                }
            }

            throw new Exception("No result found in RCC response");
        }

        private static object BuildSettings(string type, object[] arguments) => new
        {
            Mode = "Thumbnail",
            Settings = new { Type = type, Arguments = arguments }
        };

        private static string MakeAssetUrl(long assetId) =>
            $"{Roblox.Configuration.BaseUrl}/Asset/?id={assetId}&apikey=rccservislwkeueueeuww39";

        private static async Task<Stream> RenderWithArgs(string type, object[] arguments, string format, CancellationToken? cancellationToken)
        {
            var port = await StartRccService();
            await WaitForRccReady(port);
            return await SendAndParse(port, Guid.NewGuid().ToString(), BuildSettings(type, arguments), format, cancellationToken);
        }

        public static async Task<Stream> RequestAssetThumbnailByType(long assetId, Roblox.Models.Assets.Type assetType, long? teeShirtContentId = null, IEnumerable<long>? packageAssetIds = null, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                var baseUrl = Roblox.Configuration.BaseUrl;
                var assetUrl = MakeAssetUrl(assetId);

                switch (assetType)
                {
                    case Roblox.Models.Assets.Type.Hat:
                    case Roblox.Models.Assets.Type.HairAccessory:
                    case Roblox.Models.Assets.Type.FaceAccessory:
                    case Roblox.Models.Assets.Type.NeckAccessory:
                    case Roblox.Models.Assets.Type.ShoulderAccessory:
                    case Roblox.Models.Assets.Type.FrontAccessory:
                    case Roblox.Models.Assets.Type.BackAccessory:
                    case Roblox.Models.Assets.Type.WaistAccessory:
                        return await RenderWithArgs("Hat", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Gear:
                        return await RenderWithArgs("Gear", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Face:
                        return await RenderWithArgs("Decal", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Mesh:
                        return await RenderWithArgs("Mesh", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Model:
                        return await RenderWithArgs("Model", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Head:
                        return await RenderWithArgs("Head", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl, 0,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Shirt:
                        return await RenderWithArgs("Shirt", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl, 0,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Pants:
                        return await RenderWithArgs("Pants", new object[]
                        {
                            assetUrl, "Png", 840, 840, baseUrl, 0,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.TeeShirt:
                        if (teeShirtContentId == null)
                            throw new Exception("teeShirtContentId required for TeeShirt");
                        return await RenderWithArgs("Image", new object[]
                        {
                            teeShirtContentId.Value, baseUrl, "Png", 840, 840,
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.LeftArm:
                    case Roblox.Models.Assets.Type.RightArm:
                    case Roblox.Models.Assets.Type.LeftLeg:
                    case Roblox.Models.Assets.Type.RightLeg:
                    case Roblox.Models.Assets.Type.Torso:
                        return await RenderWithArgs("BodyPart", new object[]
                        {
                            assetUrl,
                            baseUrl,
                            "Png",
                            840,
                            840,
                            $"{baseUrl}/Asset/?id=1785197&apikey=rccservislwkeueueeuww39",
                            "",
                        }, "Png", cancellationToken);

                    case Roblox.Models.Assets.Type.Package:
                        if (packageAssetIds == null)
                            throw new Exception("packageAssetIds required for Package");
                        var allIds = new List<long> { assetId };
                        allIds.AddRange(packageAssetIds);
                        return await RenderWithArgs("Avatar_R15_Action_Package", new object[]
                        {
                            baseUrl,
                            $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId=0",
                            "Png",
                            840,
                            840,
                            allIds.ToArray(),
                        }, "Png", cancellationToken);

                    default:
                        throw new Exception("No render path for asset type: " + assetType);
                }
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestPlayerThumbnail(AvatarData data, CancellationToken? cancellationToken = null)
        {
            if (data.playerAvatarType != "R6")
                throw new Exception("Invalid PlayerAvatarType");

            var w = new Stopwatch();
            w.Start();

            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                var baseUrl = Roblox.Configuration.BaseUrl;
                var result = await RenderWithArgs("Avatar", new object[]
                {
                    $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId={data.userId}",
                    baseUrl,
                    data.format ?? "Png",
                    840,
                    840,
                }, data.format ?? "Png", cancellationToken);
                w.Stop();
                Metrics.RenderMetrics.ReportRenderAvatarThumbnailTime(data.userId, w.ElapsedMilliseconds);
                return result;
            }
            catch
            {
                Roblox.Metrics.RenderMetrics.ReportRenderAvatarThumbnailFailure(data.userId);
                throw;
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestPlayerThumbnailR15(long userId, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                var baseUrl = Roblox.Configuration.BaseUrl;
                return await RenderWithArgs("Avatar_R15_Action", new object[]
                {
                    baseUrl,
                    $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId={userId}",
                    "Png",
                    840,
                    840,
                }, "Png", cancellationToken);
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestPlayerHeadshot(AvatarData data, CancellationToken? cancellationToken = null)
        {
            if (data.playerAvatarType != "R6")
                throw new Exception("Invalid PlayerAvatarType");

            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                var baseUrl = Roblox.Configuration.BaseUrl;
                return await RenderWithArgs("Closeup", new object[]
                {
                    baseUrl,
                    $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId={data.userId}",
                    data.format ?? "Png",
                    840,
                    840,
                    true,
                    30.0,
                    130.0,
                    0.0,
                    0.0,
                }, data.format ?? "Png", cancellationToken);
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestPlayerThumbnail3D(long userId, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                var baseUrl = Roblox.Configuration.BaseUrl;
                return await RenderWithArgs("Avatar_R15_Action", new object[]
                {
                    baseUrl,
                    $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId={userId}",
                    "Obj",
                    840,
                    840,
                }, "Obj", cancellationToken);
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestAssetThumbnail3D(long assetId, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                return await RenderWithArgs("Hat", new object[]
                {
                    MakeAssetUrl(assetId),
                    "Obj",
                    840,
                    840,
                    Roblox.Configuration.BaseUrl,
                }, "Obj", cancellationToken);
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestAssetGame(long assetId, int x, int y, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
            try
            {
                return await RenderWithArgs("Place", new object[]
                {
                    MakeAssetUrl(assetId),
                    "Png",
                    x,
                    y,
                    Roblox.Configuration.BaseUrl,
                }, "Png", cancellationToken);
            }
            finally { Rcc2020Lock.Release(); }
        }

        public static async Task<Stream> RequestPlaceConversion(string base64EncodedPlace, CancellationToken? cancellationToken = null)
        {
            var tmpIn = Path.GetTempFileName();
            var tmpOut = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tmpIn, Convert.FromBase64String(base64EncodedPlace));
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(AppContext.BaseDirectory, "RobloxPlaceConverter.exe"),
                    Arguments = $"game \"{tmpOut}\" \"{tmpIn}\"",
                    UseShellExecute = false,
                }) ?? throw new Exception("Failed to start converter");
                await proc.WaitForExitAsync(cancellationToken ?? CancellationToken.None);
                if (proc.ExitCode != 0) throw new Exception($"Converter exited with code {proc.ExitCode}");
                return new MemoryStream(await File.ReadAllBytesAsync(tmpOut));
            }
            finally
            {
                try { File.Delete(tmpIn); } catch { }
                try { File.Delete(tmpOut); } catch { }
            }
        }

        public static async Task<Stream> RequestHatConversion(string base64EncodedHat, CancellationToken? cancellationToken = null)
        {
            var tmpIn = Path.GetTempFileName();
            var tmpOut = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tmpIn, Convert.FromBase64String(base64EncodedHat));
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(AppContext.BaseDirectory, "RobloxPlaceConverter.exe"),
                    Arguments = $"hat \"{tmpOut}\" \"{tmpIn}\"",
                    UseShellExecute = false,
                }) ?? throw new Exception("Failed to start converter");
                await proc.WaitForExitAsync(cancellationToken ?? CancellationToken.None);
                if (proc.ExitCode != 0) throw new Exception($"Converter exited with code {proc.ExitCode}");
                return new MemoryStream(await File.ReadAllBytesAsync(tmpOut));
            }
            finally
            {
                try { File.Delete(tmpIn); } catch { }
                try { File.Delete(tmpOut); } catch { }
            }
        }
    }
}