using System.Diagnostics;
using System.Net.Http;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Roblox.Logging;
using System.Net;
using System.Xml;
using Roblox;

namespace Roblox.Rendering
{
    public static class CommandHandler
    {
        private static Random random { get; } = new();
        private static object rccLock { get; } = new();
		private static SemaphoreSlim Rcc2020Lock { get; } = new(1, 1);
		private static Process? rccProcess;
		private static int? rccPort;

        public static void Configure(string baseUrl, string authorization)
        {
			_ = baseUrl;
			_ = authorization;
        }
		
		private static bool IsPortInUse(int port)
		{
			var TCP = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
			try
			{
				TCP.Start();
				return true;
			}
			catch (System.Net.Sockets.SocketException)
			{
				return false;
			}
			finally
			{
				try
				{
					TCP.Stop();
				}
				catch { }
			}
		}
		
		private static int GetRandomPortRCC2020()
		{
			lock (rccLock)
			{
				int port;
				bool available;

				do
				{
					port = random.Next(20000, 40000);
					available = IsPortInUse(port);
				} while (!available);

				return port;
			}
		}

		private static async Task SendCloseJobRequest(int port, string jobId)
		{
			var XML = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
		<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" 
						   xmlns:SOAP-ENC=""http://schemas.xmlsoap.org/soap/encoding/"" 
						   xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
						   xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
						   xmlns:ns2=""http://roblox.com/RCCServiceSoap"" 
						   xmlns:ns1=""http://roblox.com/"" 
						   xmlns:ns3=""http://roblox.com/RCCServiceSoap12"">
			<SOAP-ENV:Body>
				<ns1:CloseJob>
					<ns1:jobID>{jobId}</ns1:jobID>
				</ns1:CloseJob>
			</SOAP-ENV:Body>
		</SOAP-ENV:Envelope>";

			await SendSoapRequest(port, "http://roblox.com/CloseJob", XML);
		}  
		
		private static readonly HttpClient httpClient = new()
		{
			Timeout = TimeSpan.FromMinutes(2)
		};
		
		private static async Task<int> StartRccService()
		{
			lock (rccLock)
			{
				if (rccProcess != null && !rccProcess.HasExited && rccPort.HasValue)
					return rccPort.Value;

				rccPort = GetRandomPortRCC2020();

				var rccPath = Path.Combine(Roblox.Configuration.RccService2020Path, "RCCService.exe");
				if (string.IsNullOrEmpty(rccPath) || !File.Exists(rccPath))
					throw new Exception("RCC 2020 path not configured or RCC exe doesn't exist");

				var processStartInfo = new ProcessStartInfo
				{
					FileName = rccPath,
					Arguments = $"-console -verbose -port {rccPort.Value}",
					UseShellExecute = true,
					CreateNoWindow = false
				};

				rccProcess = new Process { StartInfo = processStartInfo };

				if (!rccProcess.Start())
					throw new Exception("Failed to start RCC 2020 process");

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
					var pingXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soap:Body>
					<HelloWorld xmlns=""http://roblox.com/"" />
				</soap:Body>
			</soap:Envelope>";

					await SendSoapRequest(port, "http://roblox.com/HelloWorld", pingXml);
					return;
				}
				catch
				{
					await Task.Delay(500);
				}
			}

			throw new Exception($"RCC2020 did not become ready on port {port} within 30 seconds");
		}
		
		private static async Task<string> SendSoapRequest(int port, string soapAction, string xmlBody)
		{
			var url = $"http://localhost:{port}";
			using var request = new HttpRequestMessage(HttpMethod.Post, url);
			request.Headers.Add("SOAPAction", soapAction);
			request.Content = new StringContent(xmlBody, Encoding.UTF8, "text/xml");

			var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync();
		}

		private static async Task<Stream> RenderRcc2020(long userId, string renderType, string format = "Png", CancellationToken? cancellationToken = null)
		{
			var port = await StartRccService();
			await WaitForRccReady(port);
			var jobId = Guid.NewGuid().ToString();
			var baseUrl = Roblox.Configuration.BaseUrl;
			var charApp = $"{baseUrl}/v1.1/avatar-fetch?placeId=0&userId={userId}";

			object[] arguments;
			if (renderType == "Closeup")
			{
				arguments = new object[]
				{
					Roblox.Configuration.BaseUrl,
					charApp,
					format,
					840,
					840,
					true,   // quadratic
					30.0,   // baseHatZoom
					130.0,  // maxHatZoom
					0.0,    // cameraOffsetX
					0.0,    // cameraOffsetY
				};
			}
			else if (renderType == "Avatar")
			{
				arguments = new object[]
				{
					charApp,
					Roblox.Configuration.BaseUrl,
					format,
					840,
					840,
				};
			}
			else
			{
				arguments = new object[]
				{
					Roblox.Configuration.BaseUrl,
					charApp,
					format,
					840,
					840,
				};
			}

			var Json = new
			{
				Mode = "Thumbnail",
				Settings = new
				{
					Type = renderType,
					Arguments = arguments,
				}
			};

			var finalJson = JsonSerializer.Serialize(Json);

			var XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
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
						<LuaValue>
							<type>LUA_TNIL</type>
						</LuaValue>
					</arguments>
				</OpenJob>
			</soap:Body>
		</soap:Envelope>";

			try
			{
				var res = await SendSoapRequest(port, "http://roblox.com/OpenJob", XML);
				
				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(res);
				
				var NSManager = new XmlNamespaceManager(xmlDoc.NameTable);
				NSManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
				NSManager.AddNamespace("ns1", "http://roblox.com/");
				
				var resNodes = xmlDoc.SelectNodes("//soap:Envelope/soap:Body/ns1:OpenJobResponse/ns1:OpenJobResult", NSManager);
				foreach (XmlNode resultNode in resNodes)
				{
					var typeNode = resultNode.SelectSingleNode("ns1:type", NSManager);
					var valueNode = resultNode.SelectSingleNode("ns1:value", NSManager);
					
					if (typeNode != null && valueNode != null &&
						// tstring contains the actual b64 render
						typeNode.InnerText == "LUA_TSTRING" && 
						!string.IsNullOrEmpty(valueNode.InnerText))
					{
						await SendCloseJobRequest(port, jobId);
						
						if (format == "Obj")
						{
							return new MemoryStream(Encoding.UTF8.GetBytes(valueNode.InnerText));
						}
						
						try
						{
							var imgBytes = Convert.FromBase64String(valueNode.InnerText);
							return new MemoryStream(imgBytes);				
						}
						catch (FormatException)
						{
							continue;
						}
					}
				}
				
				throw new Exception("no bullshit found in rcc response");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"rcc {renderType} render shit fail here msg {ex.Message}");
				throw;
			}
		}

		private static async Task<Stream> RenderRcc2020Game(long assetId, int x, int y, CancellationToken? cancellationToken = null)
		{
			var port = await StartRccService();
			await WaitForRccReady(port);
			var jobId = Guid.NewGuid().ToString();
			var baseUrl = Roblox.Configuration.BaseUrl;
			var assetUrl = $"{baseUrl}/Asset/?id={assetId}&apikey=rccservislwkeueueeuww39";

			var Json = new
			{
				Mode = "Thumbnail",
				Settings = new
				{
					Type = "Place",
					Arguments = new object[]
					{
						assetUrl,
						"Png",
						x,
						y,
						baseUrl,
					}
				}
			};

			var finalJson = JsonSerializer.Serialize(Json);

			var XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
		<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
		   xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
		   xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
			<soap:Body>
				<OpenJob xmlns=""http://roblox.com/"">
					<job>
						<id>{jobId}</id>
						<expirationInSeconds>120</expirationInSeconds>
						<category>0</category>
						<cores>1</cores>
					</job>
					<script>
						<name>GameServer</name>
						<script>{finalJson}</script>
					</script>
					<arguments>
						<LuaValue>
							<type>LUA_TNIL</type>
						</LuaValue>
					</arguments>
				</OpenJob>
			</soap:Body>
		</soap:Envelope>";

			try
			{
				var res = await SendSoapRequest(port, "http://roblox.com/OpenJob", XML);

				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(res);

				var NSManager = new XmlNamespaceManager(xmlDoc.NameTable);
				NSManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
				NSManager.AddNamespace("ns1", "http://roblox.com/");

				var resNodes = xmlDoc.SelectNodes("//soap:Envelope/soap:Body/ns1:OpenJobResponse/ns1:OpenJobResult", NSManager);
				foreach (XmlNode resultNode in resNodes)
				{
					var typeNode = resultNode.SelectSingleNode("ns1:type", NSManager);
					var valueNode = resultNode.SelectSingleNode("ns1:value", NSManager);

					if (typeNode != null && valueNode != null &&
						// tstring contains the actual b64 render
						typeNode.InnerText == "LUA_TSTRING" &&
						!string.IsNullOrEmpty(valueNode.InnerText))
					{
						try
						{
							var imgBytes = Convert.FromBase64String(valueNode.InnerText);
							await SendCloseJobRequest(port, jobId);
							return new MemoryStream(imgBytes);
						}
						catch (FormatException)
						{
							continue;
						}
					}
				}

				throw new Exception("bullshit 64 :heart:");
			}
			catch (Exception ex)
			{
				Console.WriteLine($" bullshit error {ex.Message}");
				throw;
			}
		}

		private static async Task<Stream> RenderRcc2020Asset(long assetId, string renderType, string format = "Png", CancellationToken? cancellationToken = null)
		{
			var port = await StartRccService();
			await WaitForRccReady(port);
			var jobId = Guid.NewGuid().ToString();
			var baseUrl = Roblox.Configuration.BaseUrl;
			var assetUrl = $"{baseUrl}/Asset/?id={assetId}&apikey=rccservislwkeueueeuww39";
			const long defaultMannequinId = 1785197;

			var Json = new
			{
				Mode = "Thumbnail",
				Settings = new
				{
					Type = renderType,
					Arguments = new object[]
					{
						assetUrl,
						format,
						840,
						840,
						baseUrl,
						defaultMannequinId,
					}
				}
			};

			var finalJson = JsonSerializer.Serialize(Json);

			var XML = $@"<?xml version=""1.0"" encoding=""utf-8""?>
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
						<LuaValue>
							<type>LUA_TNIL</type>
						</LuaValue>
					</arguments>
				</OpenJob>
			</soap:Body>
		</soap:Envelope>";

			try
			{
				var res = await SendSoapRequest(port, "http://roblox.com/OpenJob", XML);

				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(res);

				var NSManager = new XmlNamespaceManager(xmlDoc.NameTable);
				NSManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
				NSManager.AddNamespace("ns1", "http://roblox.com/");

				var resNodes = xmlDoc.SelectNodes("//soap:Envelope/soap:Body/ns1:OpenJobResponse/ns1:OpenJobResult", NSManager);
				foreach (XmlNode resultNode in resNodes)
				{
					var typeNode = resultNode.SelectSingleNode("ns1:type", NSManager);
					var valueNode = resultNode.SelectSingleNode("ns1:value", NSManager);

					if (typeNode != null && valueNode != null &&
						// tstring contains the actual b64 render
						typeNode.InnerText == "LUA_TSTRING" &&
						!string.IsNullOrEmpty(valueNode.InnerText))
					{
						await SendCloseJobRequest(port, jobId);

						if (format == "Obj")
						{
							return new MemoryStream(Encoding.UTF8.GetBytes(valueNode.InnerText));
						}

						try
						{
							var imgBytes = Convert.FromBase64String(valueNode.InnerText);
							return new MemoryStream(imgBytes);
						}
						catch (FormatException)
						{
							continue;
						}
					}
				}

				throw new Exception("no bullshit found in rcc response");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"rcc {renderType} render shit fail here msg {ex.Message}");
				throw;
			}
		}

		private static async Task<Stream> RenderRcc2020AssetWithFallback(long assetId, IEnumerable<string> renderTypes, string format = "Png", CancellationToken? cancellationToken = null)
		{
			Exception? lastException = null;
			foreach (var renderType in renderTypes)
			{
				try
				{
					return await RenderRcc2020Asset(assetId, renderType, format, cancellationToken);
				}
				catch (Exception ex)
				{
					lastException = ex;
				}
			}

			throw new Exception($"RCC 2020 failed to render asset {assetId} for all render types.", lastException);
		}

		public static async Task<Stream> RequestPlayerThumbnailR15(long userId, CancellationToken? cancellationToken = null)
		{
			await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);
			
			try
			{
				return await RenderRcc2020(userId, "Avatar_R15_Action", cancellationToken: cancellationToken);
			}
			finally
			{
				Rcc2020Lock.Release();
			}
		}

        public static async Task<Stream> RequestPlayerThumbnail(AvatarData data, CancellationToken? cancellationToken = null)
        {
            if (data.playerAvatarType != "R6")
                throw new Exception("Invalid PlayerAvatarType");

            // todo: do we need to get assetTypeId here, or can we just expect caller to get it for us?
            var w = new Stopwatch();
            w.Start();

            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

            try
            {
                var result = await RenderRcc2020(data.userId, "Avatar", data.format ?? "Png", cancellationToken);
                w.Stop();
                Metrics.RenderMetrics.ReportRenderAvatarThumbnailTime(data.userId, w.ElapsedMilliseconds);
                return result;
            }
            catch
            {
                Roblox.Metrics.RenderMetrics.ReportRenderAvatarThumbnailFailure(data.userId);
                throw;
            }
            finally
            {
                Rcc2020Lock.Release();
            }
        }

        public static async Task<Stream> RequestPlayerHeadshot(AvatarData data, CancellationToken? cancellationToken = null)
        {
            if (data.playerAvatarType != "R6")
                throw new Exception("Invalid PlayerAvatarType");

            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

            try
            {
                return await RenderRcc2020(data.userId, "Closeup", data.format ?? "Png", cancellationToken);
            }
            finally
            {
                Rcc2020Lock.Release();
            }
        }

        public static async Task<Stream> RequestPlayerThumbnail3D(long userId, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

            try
            {
                return await RenderRcc2020(userId, "Avatar_R15_Action", "Obj", cancellationToken);
            }
            finally
            {
                Rcc2020Lock.Release();
            }
        }

        public static async Task<Stream> RequestTextureThumbnail(long assetId, int assetTypeId, CancellationToken? cancellationToken = null)
        {
			await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

			try
			{
				_ = assetTypeId;
				return await RenderRcc2020AssetWithFallback(assetId, new[]
				{
					"Decal",
					"Image"
				}, "Png", cancellationToken);
			}
			finally
			{
				Rcc2020Lock.Release();
			}
        }
        
        public static async Task<Stream> RequestAssetThumbnail(long assetId, CancellationToken? cancellationToken = null)
        {
			await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

			try
			{
				return await RenderRcc2020AssetWithFallback(assetId, new[]
				{
					"Hat",
					"Shirt",
					"Pants",
					"Gear",
					"Model",
					"Image",
					"Decal",
					"MeshPart",
					"Mesh",
					"Head",
				}, "Png", cancellationToken);
			}
			finally
			{
				Rcc2020Lock.Release();
			}
        }

        public static async Task<Stream> RequestAssetThumbnail3D(long assetId, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

            try
            {
                return await RenderRcc2020Asset(assetId, "Hat", "Obj", cancellationToken);
            }
            finally
            {
                Rcc2020Lock.Release();
            }
        }

        public static async Task<Stream> RequestHeadThumbnail(long assetId, CancellationToken? cancellationToken = null)
        {
			await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

			try
			{
				return await RenderRcc2020Asset(assetId, "Head", "Png", cancellationToken);
			}
			finally
			{
				Rcc2020Lock.Release();
			}
        }
		
        public static async Task<Stream> RequestAssetMesh(long assetId, CancellationToken? cancellationToken = null)
        {
			await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

			try
			{
				return await RenderRcc2020AssetWithFallback(assetId, new[]
				{
					"Mesh",
					"MeshPart"
				}, "Png", cancellationToken);
			}
			finally
			{
				Rcc2020Lock.Release();
			}
        }

        public static async Task<Stream> RequestPlaceConversion(string base64EncodedPlace, CancellationToken? cancellationToken = null)
        {
			_ = base64EncodedPlace;
			_ = cancellationToken;
			throw new NotSupportedException("Place conversion is removed. Websocket renderer is disabled.");
        }

        public static async Task<Stream> RequestHatConversion(string base64EncodedHat,
            CancellationToken? cancellationToken = null)
        {
			_ = base64EncodedHat;
			_ = cancellationToken;
			throw new NotSupportedException("Hat conversion is removed. Websocket renderer is disabled.");
        }
        
        public static async Task<Stream> RequestAssetGame(long assetId, int x, int y, CancellationToken? cancellationToken = null)
        {
            await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

            try
            {
                return await RenderRcc2020Game(assetId, x, y, cancellationToken);
            }
            finally
            {
                Rcc2020Lock.Release();
            }
        }

        public static async Task<Stream> RequestAssetTeeShirt(long assetId, long contentId, CancellationToken? cancellationToken = null)
        {
			await Rcc2020Lock.WaitAsync(cancellationToken ?? CancellationToken.None);

			try
			{
				_ = contentId;
				return await RenderRcc2020AssetWithFallback(assetId, new[]
				{
					"Image",
					"Shirt",
					"Decal"
				}, "Png", cancellationToken);
			}
			finally
			{
				Rcc2020Lock.Release();
			}
        }
    }
}