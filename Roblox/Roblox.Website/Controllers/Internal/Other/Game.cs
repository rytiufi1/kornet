using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.Middleware;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Website.WebsiteModels.Games;
using Roblox.Services.Exceptions;
using Roblox.Dto.Games;
using Roblox.Models.GameServer;
using Roblox.Dto.Assets;
using Roblox.Models.Assets;
using Roblox.Services;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Game : ControllerBase 
    {	
		private sealed class Player2014HostProc
		{
			public long PlaceId { get; init; }
			public int Port { get; init; }
			public string JobId { get; init; } = "";
			public Process Process { get; init; } = null!;
			public DateTimeOffset StartedAt { get; init; }
		}

		private static readonly ConcurrentDictionary<long, Player2014HostProc> Player2014HostsByPlace = new();

		private static bool IsPortInUse(int port)
		{
			try
			{
				using var client = new TcpClient();
				var result = client.BeginConnect("127.0.0.1", port, null, null);
				var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(120));
				if (!success)
					return false;
				client.EndConnect(result);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private Player2014HostProc EnsurePlayer2014Hosting(long placeId, int port, string jobId)
		{
			if (Player2014HostsByPlace.TryGetValue(placeId, out var existing))
			{
				try
				{
					if (!existing.Process.HasExited && existing.Port == port)
						return existing;
				}
				catch
				{
				}
			}

			var exePath = Path.Combine(Directory.GetCurrentDirectory(), "Player2014", "RobloxPlayerBeta.exe");
			if (!System.IO.File.Exists(exePath))
				throw new RobloxException(500, 0, "Player2014 missing");

			var hostUrl = $"{Configuration.BaseUrl}/game/player2014/host?placeId={placeId}&port={port}&accesskey={Uri.EscapeDataString(Configuration.RccAuthorization ?? "")}";
			var authUrl = $"{Configuration.BaseUrl}/Login/Negotiate.ashx";

			var psi = new ProcessStartInfo
			{
				FileName = exePath,
				Arguments = $"-t None -j \"{hostUrl}\" -a \"{authUrl}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory(),
			};

			var proc = Process.Start(psi);
			if (proc == null)
				throw new RobloxException(500, 0, "Failed to start Player2014");

			var info = new Player2014HostProc
			{
				PlaceId = placeId,
				Port = port,
				JobId = jobId,
				Process = proc,
				StartedAt = DateTimeOffset.UtcNow,
			};
			Player2014HostsByPlace[placeId] = info;
			return info;
		}

		private bool IsRcc()
        {
            var rccAccessKey = Request.Headers.ContainsKey("accesskey") ? Request.Headers["accesskey"].ToString() : null;
            var isRcc = rccAccessKey == Configuration.RccAuthorization;
            return isRcc;
        }	
		
		[HttpPostBypass("game/badge/award.ashx")]
		[HttpPostBypass("assets/award-badge")]
		public async Task<string> AwardBadge(
			[Required] long userId,
			[Required] long badgeId,
			[Required] long placeId)
		{
			if (!IsRcc())
				throw new RobloxException(400, 0, "BadRequest");
					
			using var Assets = services.assets;
			if (!await Assets.IsBadgeAssociatedWithPlace(badgeId, placeId))
			{
				throw new RobloxException(400, 0, "Badge does not belong to this place");
			}
			
			var Badge = await Assets.GetAssetCatalogInfo(badgeId);
			if (Badge == null || Badge.assetType != Models.Assets.Type.Badge)
			{
				throw new RobloxException(400, 0, "BadRequest");
			}
			
			using var Users = services.users;
			var User = await Users.GetUserById(userId);
			if (User == null)
			{
				throw new RobloxException(400, 0, "BadRequest");
			}
			
			string Creator;
			if (Badge.creatorType == CreatorType.User)
			{
				var CreatorUsername = await Users.GetUserById(Badge.creatorTargetId);
				Creator = CreatorUsername?.username ?? "Unknown";
			}
			else
			{
				using var Groups = services.groups;
				var group = await Groups.GetGroupById(Badge.creatorTargetId);
				Creator = group?.name ?? "Unknown";
			}

			var hasBadge = await Assets.DoesUserOwnAsset(userId, badgeId);
			if (!hasBadge)
			{
				var Awarded = await Users.GiveUserGameBadge(userId, badgeId);
				if (Awarded)
				{
					await Assets.IncrementBadgeAwarded(badgeId);
				}
			}
			
			return $"{User.username} won {Creator}'s \"{Badge.name}\" award!";
		}

		[HttpPostBypass("/v1/users/{userId}/badges/{badgeId}/award-badge")]
		public async Task<MVC.IActionResult> AwardBadgeV1(
			[MVC.FromRoute] long userId,
			[MVC.FromRoute] long badgeId)
		{
			if (!IsRcc())
				throw new RobloxException(400, 0, "BadRequest");
			
			if (!HttpContext.Request.Headers.TryGetValue("Roblox-Place-Id", out var placeIDHeader))
			{
				throw new RobloxException(400, 0, "no Roblox-Place-Id header");
			}

			if (!long.TryParse(placeIDHeader, out var placeId))
			{
				throw new RobloxException(400, 0, "bad Roblox-Place-Id header");
			}
			
			using var Assets = services.assets;
			if (!await Assets.IsBadgeAssociatedWithPlace(badgeId, placeId))
			{
				throw new RobloxException(400, 0, "Badge does not belong to this place");
			}
			
			var Badge = await Assets.GetAssetCatalogInfo(badgeId);
			if (Badge == null || Badge.assetType != Models.Assets.Type.Badge)
			{
				throw new RobloxException(400, 0, "BadRequest");
			}
			
			using var Users = services.users;
			var User = await Users.GetUserById(userId);
			if (User == null)
			{
				throw new RobloxException(400, 0, "BadRequest");
			}
			
			string Creator;
			if (Badge.creatorType == CreatorType.User)
			{
				var CreatorUsername = await Users.GetUserById(Badge.creatorTargetId);
				Creator = CreatorUsername?.username ?? "Unknown";
			}
			else
			{
				using var Groups = services.groups;
				var group = await Groups.GetGroupById(Badge.creatorTargetId);
				Creator = group?.name ?? "Unknown";
			}

			var hasBadge = await Assets.DoesUserOwnAsset(userId, badgeId);
			if (!hasBadge)
			{
				var Awarded = await Users.GiveUserGameBadge(userId, badgeId);
				if (Awarded)
				{
					await Assets.IncrementBadgeAwarded(badgeId);
				}
			}
			
			return Ok(new 
			{
				creatorType = Badge.creatorType,
				creatorId = Badge.creatorTargetId,
				awardAssetIds = Array.Empty<dynamic>()
			});
		}

		[HttpGetBypass("game/badge/hasbadge.ashx")]
		[HttpPostBypass("game/badge/hasbadge.ashx")]
		public async Task<dynamic> HasBadge(
			[Required] long userId,
			[Required] long badgeId)
		{
			if (!IsRcc())
				throw new RobloxException(400, 0, "BadRequest");
			
			using var Assets = services.assets;
			var hasBadge = await Assets.DoesUserOwnAsset(userId, badgeId);
			
			return hasBadge ? "Success" : "Failure";
		}

		[HttpGetBypass("/game/PlaceLauncher.ashx")]
		[HttpPostBypass("/game/PlaceLauncher.ashx")]
		public async Task<dynamic> PlaceLauncher(long placeId, string ticket, string? gameId = null, int? year = null)
		{	
			var PlaceYear = await services.games.GetPlaceYear(placeId);
			string Year = (year ?? PlaceYear)?.ToString() ?? "2016";

			if (!await services.games.IsPlayable(placeId))
			{
				return BadRequest(new 
				{
					jobId = (string?)null,
					status = (int)JoinStatus.Error,
					message = "You can not access this place at this time."
				});
			}

			FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled, FeatureFlag.GameJoinEnabled);
			
			long userId;
			if (!string.IsNullOrEmpty(ticket))
			{
				try 
				{
					var JWT = SessionMiddleware.DecodeJwt<JwtEntry>(ticket);
					var Session = await services.users.GetSessionById(JWT.sessionId);
					
					if (Session != null && Session.userId > 0)
					{
						userId = Session.userId;
						
						var userInfo = await services.users.GetUserById(userId);
						if (userInfo == null)
						{
							return StatusCode(400, "Invalid user ID/ticket");
						}

						if (userInfo.accountStatus is AccountStatus.Suppressed or AccountStatus.Poisoned or AccountStatus.Deleted or AccountStatus.Forgotten)
						{
							return BadRequest(new 
							{
								jobId = (string?)null,
								status = (int)JoinStatus.Error,
								message = "ay gng nice try but u are banned."
							});
						}
					}
					else
					{
						return StatusCode(400, "Invalid session");
					}
				}
				catch (Exception ex)
				{
					return StatusCode(400, "Invalid ticket");
				}
			}
			else
			{
				userId = userSession?.userId ?? 1L;
				if (userId <= 0)
				{
					return StatusCode(400, "Ticket is required");
				}
			}
				
			GameServerJwt details = new GameServerJwt
			{
				userId = userId != 0 ? userId : 1,
				placeId = placeId,
				t = "GameJoinTicketV1.1",
				iat = DateTimeOffset.Now.ToUnixTimeSeconds(),
				ip = GetIP()
			};

			string targetJobId;
			JoinStatus targetStatus;
			int targetPort = 0;

			if (!string.IsNullOrEmpty(gameId))
			{
				var MaxPlayers = await services.games.GetMaxPlayerCount(placeId);
				var ServerPlayers = await services.gameServer.GetGameServerPlayers(gameId);
				if (ServerPlayers.Count() < MaxPlayers)
				{
					targetJobId = gameId;
					targetStatus = JoinStatus.Joining;
					targetPort = await services.gameServer.GetServerPortFromDatabase(targetJobId);
				}
				else
				{
					return new
					{
						jobId = (string?)null,
						status = (int)JoinStatus.Error,
						message = "That server is full."
					};
				}
			}
			else
			{
				if (Year == "2014")
				{
					targetJobId = Guid.NewGuid().ToString("N");
					targetStatus = JoinStatus.Joining;
					targetPort = 53640 + (int)(placeId % 1000);
					while (IsPortInUse(targetPort))
						targetPort++;
					_ = EnsurePlayer2014Hosting(placeId, targetPort, targetJobId);
				}
				else
				{
					var Result = await services.gameServer.GetServerForPlace(placeId, Year);
					targetJobId = Result.job;
					targetStatus = Result.status;
					targetPort = await services.gameServer.GetServerPortFromDatabase(targetJobId);
				}
			}
				
			if (targetStatus == JoinStatus.Joining)
			{
				await Roblox.Metrics.GameMetrics.ReportGameJoinPlaceLauncherReturned(details.placeId);

				var TicketQ = Request.Query["ticket"].FirstOrDefault();
				var Ticket = Uri.EscapeDataString(TicketQ);
				var joinScriptUrl = Year == "2014"
					? $"{Configuration.BaseUrl}/game/join2014?placeId={placeId}&port={targetPort}&ip={Uri.EscapeDataString(string.IsNullOrWhiteSpace(Configuration.GSIPAddress) ? "127.0.0.1" : Configuration.GSIPAddress)}"
					: $"{Configuration.BaseUrl}/game/join.ashx?placeid={placeId}&ticket={Ticket}&jobId={targetJobId}";
				
				return new
				{
					jobId = targetJobId,
					status = (int)targetStatus,
					joinScriptUrl = joinScriptUrl,
					authenticationUrl = Configuration.BaseUrl + "/Login/Negotiate.ashx",
					authenticationTicket = Ticket,
					message = (string?)null,
				};
			}
			else if (targetStatus == JoinStatus.Waiting)
			{
				return new
				{
					jobId = targetJobId,
					status = (int)JoinStatus.Waiting,
					message = "Waiting for server"
				};
			}
			return new
			{
				jobId = (string?)null,
				status = (int)targetStatus,
				message = "Waiting for server",
			};
		}

		[HttpGetBypass("/game/Join.ashx")]
		[HttpPostBypass("/game/Join.ashx")]
		public async Task<MVC.IActionResult> JoinGame()
		{
			try
			{
				var placeId = long.Parse(Request.Query["placeid"].FirstOrDefault() ?? Request.Query["placeId"].FirstOrDefault());
				var ticket = Request.Query["ticket"].FirstOrDefault();
				var JobId = Request.Query["jobId"].FirstOrDefault();
				var yearOverrideRaw = Request.Query["year"].FirstOrDefault();
				var PlaceYear = await services.games.GetPlaceYear(placeId);
				if (!int.TryParse(yearOverrideRaw, out var yearOverride))
					yearOverride = 0;
				string Year = (yearOverride > 0 ? yearOverride : PlaceYear)?.ToString() ?? "2016";
				bool is2020 = Year == "2020" || Year == "2021";

				long userId;
				if (!string.IsNullOrEmpty(ticket))
				{
					try
					{
						var JWT = SessionMiddleware.DecodeJwt<JwtEntry>(ticket);
						var Session = await services.users.GetSessionById(JWT.sessionId);
						if (Session != null && Session.userId > 0)
						{
							userId = Session.userId;
							var userInfo = await services.users.GetUserById(userId);
							if (userInfo == null)
							{
								return StatusCode(400, "Invalid user ID");
							}

							if (userInfo.accountStatus is AccountStatus.Suppressed or AccountStatus.Poisoned or AccountStatus.Deleted or AccountStatus.Forgotten)
							{
								return StatusCode(400, "banned");
							}
						}
						else
						{
							return StatusCode(400, "Bad session");
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Join.ashx ticket error: {ex}");
						return StatusCode(400, "Invalid ticket");
					}
				}
				else
				{
					return StatusCode(400, "Ticket is required");
				}
				
				var PlayerGS = await services.gameServer.GetPlayersCurrentServer(userId);
				if (PlayerGS != null && PlayerGS.assetId != placeId)
				{
					await services.gameServer.EvictPlayer(userId, PlayerGS.assetId, Year);
					await Task.Delay(300);
				}

				string ServerId;
				int ServerPort;

				if (!string.IsNullOrEmpty(JobId))
				{
					var MaxPlayers = await services.games.GetMaxPlayerCount(placeId);
					var ServerPlayers = await services.gameServer.GetGameServerPlayers(JobId);
					
					if (ServerPlayers.Count() < MaxPlayers)
					{
						ServerId = JobId;
						ServerPort = await services.gameServer.GetServerPortFromDatabase(JobId);
					}
					else
					{
						var Server = await services.gameServer.GetServerForPlace(placeId, Year);
						if (Server.status != JoinStatus.Joining)
						{
							return StatusCode(400, "No servers available");
						}
						ServerId = Server.job;
						ServerPort = await services.gameServer.GetServerPortFromDatabase(ServerId);
					}
				}
				else
				{
					var Server = await services.gameServer.GetServerForPlace(placeId, Year);
					if (Server.status != JoinStatus.Joining)
					{
						return StatusCode(400, "No servers available");
					}
					ServerId = Server.job;
					ServerPort = await services.gameServer.GetServerPortFromDatabase(ServerId);
				}

				if (ServerPort <= 0)
				{
					return StatusCode(400, "Server port unavailable");
				}

				var JoinScript = await services.gameJoin.GenerateJoinScript(userId, placeId, ServerId, ServerPort, ticket, Year);

				var JSONSettings = new JsonSerializerOptions 
				{ 
					PropertyNamingPolicy = null, 
					WriteIndented = false, 
					Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
				};
				
				var JSONScript = System.Text.Json.JsonSerializer.Serialize(JoinScript, JSONSettings);
				var RSA = services.rsaSign;
				var signature = RSA.SignScript(JSONScript, is2020);
				var Result = $"{signature}\r\n{JSONScript}";
				
				return Content(Result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"join.ashx error: {ex}");
				return StatusCode(500, "Failed to generate join script");
			}
		}
		
		private void CheckServerAuth(string auth)
		{
		    string expected = Roblox.Configuration.GameServerAuthorization;
			
			if (auth != expected)
			{
				string ip = GetRequesterIpRaw(HttpContext);

				Roblox.Metrics.GameMetrics.ReportRccAuthorizationFailure("http://kornet.lat/gs/hi", auth, ip);
				
				Console.WriteLine($"[INFO] auth failed");

				throw new BadRequestException();
			}
		}

        [HttpPostBypass("/gs/activity")]
        public async Task<dynamic> GetGsActivity([Required, MVC.FromBody] ReportActivity request)
        {
            CheckServerAuth(request.authorization);
            var Result = await services.gameServer.GetLastServerPing(request.serverId);
            return new
            {
                isAlive = Result >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1)),
                updatedAt = Result,
            };
        }

        [HttpPostBypass("/gs/ping")]
        public async Task ReportServerActivity([Required, MVC.FromBody] ReportActivity request)
        {
            CheckServerAuth(request.authorization);
            await services.gameServer.SetServerPing(request.serverId);
        }

		[HttpPostBypass("/gs/shutdown")]
		public async Task ShutDownServer([Required, MVC.FromBody] ReportActivity request)
		{
			CheckServerAuth(request.authorization);
			services.gameServer.ShutDownServer(request.serverId);
		}

		[HttpPostBypass("/gs/players/report")]
		public async Task ReportPlayerActivity([Required, MVC.FromBody] ReportPlayerActivity request)
		{
			CheckServerAuth(request.authorization);
			
			if (request.eventType == "Leave")
			{
				await services.gameServer.OnPlayerLeave(request.userId, request.placeId, request.serverId);
				
				try 
				{
					var wh = Roblox.Configuration.Webhook;
					if (!string.IsNullOrEmpty(wh))
					{
						var userInfo = await services.users.GetUserById(request.userId);
						var message = $"player {userInfo.username} (ID: {request.userId}) left place {request.placeId} on server {request.serverId}";
						
						using (var httpClient = new HttpClient())
						{
							var payload = new { content = message };
							var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
							await httpClient.PostAsync(wh, content);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"error sending leave to Discord: {ex.Message}");
				}
			}
			else if (request.eventType == "Join")
			{
				await Roblox.Metrics.GameMetrics.ReportGameJoinSuccess(request.placeId);
				await services.gameServer.OnPlayerJoin(request.userId, request.placeId, request.serverId);

				try 
				{
					var wh = Roblox.Configuration.Webhook;
					if (!string.IsNullOrEmpty(wh))
					{
						var User = await services.users.GetUserById(request.userId);
						var message = $"player {User.username} (ID: {request.userId}) joined place {request.placeId} on server {request.serverId}";
						
						using (var httpClient = new HttpClient())
						{
							var payload = new { content = message };
							var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
							await httpClient.PostAsync(wh, content);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"error sending join to Discord: {ex.Message}");
				}
			}
			else if (request.eventType == "Executor")
			{
				await Roblox.Metrics.GameMetrics.ReportGameJoinSuccess(request.placeId);
				await services.gameServer.OnPlayerJoin(request.userId, request.placeId, request.serverId);

				try 
				{
					var wh = Roblox.Configuration.Webhook;
					if (!string.IsNullOrEmpty(wh))
					{
						var User = await services.users.GetUserById(request.userId);
						var message = $"player {User.username} (ID: {request.userId}) given executor {request.placeId} on server {request.serverId}";
						
						using (var httpClient = new HttpClient())
						{
							var payload = new { content = message };
							var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
							await httpClient.PostAsync(wh, content);
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"error sending join to Discord: {ex.Message}");
				}
			}
			else
			{
				throw new Exception("unexpected type " + request.eventType);
			}
		}
		

				int accountAgeDays = (int)(DateTime.UtcNow - userInfo.created).TotalDays;

				var membership = await services.users.GetUserMembership(userId);
				string membershipType = membership?.membershipType switch
				{
					MembershipType.OutrageousBuildersClub => "OutrageousBuildersClub",
					MembershipType.TurboBuildersClub => "TurboBuildersClub",
					MembershipType.BuildersClub => "BuildersClub",
					_ => "None"
				};

				var placeDetails = await services.assets.GetAssetCatalogInfo(placeId);
				if (placeDetails == null || placeDetails.assetType != Roblox.Models.Assets.Type.Place)
				{
					throw new RobloxException(404, 0, "Place not found");
				}
				
				long universeId = await services.games.GetUniverseId(placeId);

				string creatorName;
				if (placeDetails.creatorType == CreatorType.User)
				{
					var creatorUser = await services.users.GetUserById(placeDetails.creatorTargetId);
					creatorName = creatorUser?.username ?? "Unknown";
				}
				else
				{
					creatorName = placeDetails.creatorName ?? "Unknown";
				}

				return new
				{
					success = true,
					user = new
					{
						userId = userId,
						username = userInfo.username,
						accountAgeDays = accountAgeDays,
						membershipType = membershipType
					},
					place = new
					{
						placeId = placeId,
						creatorId = placeDetails.creatorTargetId,
						creatorType = placeDetails.creatorType.ToString(),
						creatorName = creatorName,
						universeId = universeId
					}
				};
			}
			catch (Exception ex)
			{
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		
	}
}	