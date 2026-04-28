using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Game2014Testing : ControllerBase 
    {	
		private bool RequireAuthorizedIp()
		{
			var ip = GetRequesterIpRaw(HttpContext);
			if (!GameServer2014Comm.IsAuthorizedReportingIp(ip))
			{
				Response.StatusCode = 403;
				return false;
			}

			return true;
		}

		private async Task<string> ReadBodyAsync()
		{
			Request.EnableBuffering();
			using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
			var body = await reader.ReadToEndAsync();
			Request.Body.Position = 0;
			return body;
		}

		private static JObject? ParsePayloadRoot(string body)
		{
			if (string.IsNullOrWhiteSpace(body))
				return null;
			var tok = JToken.Parse(body);
			if (tok is JArray arr && arr.Count > 0)
				return arr[0] as JObject;
			return tok as JObject;
		}

		private static bool TryValidateHostBody(JObject? root, out GameServer2014Comm.HostSession? hostSession)
		{
			hostSession = null;
			if (root == null)
				return false;
			var jobId = root["JobId"]?.ToString();
			var auth = root["AuthToken"]?.ToString();
			if (!GameServer2014Comm.TryGetAuthorizedHostSession(jobId, auth, out var session) || session == null)
				return false;
			var bodyPlace = root["PlaceId"]?.Value<long?>();
			if (bodyPlace.HasValue && bodyPlace.Value != session.PlaceId)
				return false;
			hostSession = session;
			return true;
		}

		[HttpPostBypass("internal/gameserver/reportplayers")]
		public async Task<IActionResult> ReportPlayers2014()
		{
			if (!RequireAuthorizedIp())
				return Content("{}", "application/json");

			var body = await ReadBodyAsync();
			var root = ParsePayloadRoot(body);
			if (!TryValidateHostBody(root, out _))
				return Content("{\"bad\":[]}", "application/json");

			return Content("{\"bad\":[]}", "application/json");
		}

		[HttpPostBypass("internal/gameserver/reportstats")]
		public async Task<IActionResult> ReportStats2014()
		{
			if (!RequireAuthorizedIp())
				return Ok();

			var body = await ReadBodyAsync();
			var root = ParsePayloadRoot(body);
			if (!TryValidateHostBody(root, out _))
				return Ok();

			return Ok();
		}

		[HttpPostBypass("internal/gameserver/reportshutdown")]
		public async Task<IActionResult> ReportShutdown2014()
		{
			if (!RequireAuthorizedIp())
				return Ok();

			var body = await ReadBodyAsync();
			var root = ParsePayloadRoot(body);
			if (!TryValidateHostBody(root, out _))
				return Ok();

			return Ok();
		}

		[HttpPostBypass("internal/gameserver/verifyplayer")]
		public async Task<IActionResult> VerifyPlayer2014()
		{
			if (!RequireAuthorizedIp())
				return Content("{\"authenticated\":false}", "application/json");

			var body = await ReadBodyAsync();
			var root = ParsePayloadRoot(body);
			if (!TryValidateHostBody(root, out _))
				return Content("{\"authenticated\":false}", "application/json");

			var userId = root!["UserId"]?.Value<long?>() ?? 0;
			var ticket = root["VerificationTicket"]?.ToString();

			var ok = GameServer2014Comm.TryVerifyTicket(ticket, userId);
			return Content(ok ? "{\"authenticated\":true}" : "{\"authenticated\":false}", "application/json");
		}

		[HttpGetBypass("/game/host2014")]
		public async Task<MVC.IActionResult> Host2014()
		{
			try
			{
				var accessKey = Request.Headers.ContainsKey("accesskey") ? Request.Headers["accesskey"].ToString() : null;
				if (string.IsNullOrWhiteSpace(accessKey))
					accessKey = Request.Query["accesskey"].FirstOrDefault();
				if (accessKey != Configuration.RccAuthorization)
					return StatusCode(403, "Forbidden");

				var port = Request.Query["port"].FirstOrDefault();
				var PlaceID = Request.Query["placeId"].FirstOrDefault();
				
				if (string.IsNullOrEmpty(port))
				{
					return StatusCode(400, "Port is required");
				}
				
				if (string.IsNullOrEmpty(PlaceID) || !long.TryParse(PlaceID, out long placeId))
				{
					return StatusCode(400, "Valid placeId required");
				}

				var placeDetails = await services.assets.GetAssetCatalogInfo(placeId);
				if (placeDetails == null || placeDetails.assetType != Roblox.Models.Assets.Type.Place)
					return StatusCode(404, "Place not found");

				long universeId = 0;
				try
				{
					universeId = await services.games.GetUniverseId(placeId);
				}
				catch
				{
				}

				if (!int.TryParse(port, out var networkPort))
					return StatusCode(400, "Port must be a number");

				var session = GameServer2014Comm.CreateHostSession(
					placeId,
					universeId,
					placeDetails.creatorTargetId,
					(int)placeDetails.creatorType,
					networkPort,
					TimeSpan.FromHours(8),
					TimeSpan.FromMinutes(3));

				var luaPath = Path.Combine(AppContext.BaseDirectory, "Files", "2014Gameserver.lua");
				if (!System.IO.File.Exists(luaPath))
					luaPath = Path.Combine(Directory.GetCurrentDirectory(), "Files", "2014Gameserver.lua");
				if (!System.IO.File.Exists(luaPath))
					return StatusCode(500, "2014 server script template missing");

				var template = await System.IO.File.ReadAllTextAsync(luaPath);
				var Script = template
					.Replace("{PlaceId}", placeId.ToString())
					.Replace("{NetworkPort}", networkPort.ToString())
					.Replace("{CreatorId}", placeDetails.creatorTargetId.ToString())
					.Replace("{CreatorType}", ((int)placeDetails.creatorType).ToString())
					.Replace("{TempPlaceAccessKey}", session.TempPlaceAccessKey)
					.Replace("{AuthToken}", session.AuthToken)
					.Replace("{JobId}", session.JobId)
					.Replace("{UniverseId}", universeId.ToString());

				var RSA = services.rsaSign;
				var signature = RSA.SignScript(Script, false, true);
				var result = signature + Script;
				
				return Content(result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"2014 join error: {ex}");
				return StatusCode(500, "Failed to generate game script");
			}
		}
		
		[HttpGetBypass("/game/studio.ashx")]
		public async Task<MVC.IActionResult> StudioAshx()
		{
			try
			{
				var Script = @"print(""hello"")
repeat wait() until game:FindFirstChild(""NetworkServer"")
game:SetMessage(""Hosting!"")";

				var RSA = services.rsaSign;
				var signature = RSA.SignScript(Script, false);
				var result = $"{signature}\r\n{Script}";
				
				return Content(result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Studio.ashx error: {ex}");
				return StatusCode(500, "Failed to generate studio script");
			}
		}
		
		[HttpGetBypass("/game/join2014")]
		public async Task<MVC.IActionResult> Join2014()
		{
			try
			{
				var port = Request.Query["port"].FirstOrDefault();
				var PlaceID = Request.Query["placeId"].FirstOrDefault();
				var ip = Request.Query["ip"].FirstOrDefault();
				if (string.IsNullOrWhiteSpace(ip))
					ip = "127.0.0.1";
				ip = ip.Trim().Replace("\"", "");
				
				if (string.IsNullOrEmpty(port))
				{
					return StatusCode(400, "Port is required");
				}
				
				if (string.IsNullOrEmpty(PlaceID) || !long.TryParse(PlaceID, out long placeId))
				{
					return StatusCode(400, "Valid PlaceID required");
				}
				var ticket = Request.Query["ticket"].FirstOrDefault() ?? "";
                var ticketEncoded = Uri.EscapeDataString(ticket);

				var Script = $@"pcall(function() game:SetPlaceID(-1, false) end)

local startTime = tick()
local connectResolved = false
local loadResolved = false
local joinResolved = false
local playResolved = true
local playStartTime = 0
local player = nil
local BaseURL = ""http://kornet.lat""
local PlaceId = {placeId}

settings()[""Game Options""].CollisionSoundEnabled = true
pcall(function() settings().Rendering.EnableFRM = true end)
pcall(function() settings().Physics.Is30FpsThrottleEnabled = true end)
pcall(function() settings()[""Task Scheduler""].PriorityMethod = Enum.PriorityMethod.AccumulatedError end)
pcall(function() settings().Physics.PhysicsEnvironmentalThrottle = Enum.EnviromentalPhysicsThrottle.DefaultAuto end)


-- arguments ---------------------------------------
local threadSleepTime = 15
local test = false

local closeConnection = game.Close:connect(function() 
	if 0 then
		if not connectResolved then
			local duration = tick() - startTime;
		elseif (not loadResolved) or (not joinResolved) then
			local duration = tick() - startTime;
			if not loadResolved then
				loadResolved = true
			end
			if not joinResolved then
				joinResolved = true
			end
		elseif not playResolved then
			local duration = tick() - playStartTime;
			playResolved = true
		end
	end
end)

game:GetService(""ChangeHistoryService""):SetEnabled(false)
game:GetService(""ContentProvider""):SetThreadPool(16)
game:GetService(""InsertService""):SetBaseSetsUrl(BaseURL..""/Game/Tools/InsertAsset.ashx?nsets=10&type=base"")
game:GetService(""InsertService""):SetUserSetsUrl(BaseURL..""/Game/Tools/InsertAsset.ashx?nsets=20&type=user&userid=%d"")
game:GetService(""InsertService""):SetCollectionUrl(BaseURL..""/Game/Tools/InsertAsset.ashx?sid=%d"")
game:GetService(""InsertService""):SetAssetUrl(BaseURL..""/Asset/?id=%d"")
game:GetService(""InsertService""):SetAssetVersionUrl(BaseURL..""/Asset/?assetversionid=%d"")

pcall(function() game:GetService(""SocialService""):SetFriendUrl(BaseURL..""/Game/LuaWebService/HandleSocialRequest.ashx?method=IsFriendsWith&playerid=%d&userid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetBestFriendUrl(BaseURL..""/Game/LuaWebService/HandleSocialRequest.ashx?method=IsBestFriendsWith&playerid=%d&userid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetGroupUrl(BaseURL..""/Game/LuaWebService/HandleSocialRequest.ashx?method=IsInGroup&playerid=%d&groupid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetGroupRankUrl(BaseURL..""/Game/LuaWebService/HandleSocialRequest.ashx?method=GetGroupRank&playerid=%d&groupid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetGroupRoleUrl(BaseURL..""/Game/LuaWebService/HandleSocialRequest.ashx?method=GetGroupRole&playerid=%d&groupid=%d"") end)
pcall(function() game:GetService(""GamePassService""):SetPlayerHasPassUrl(BaseURL..""/Game/GamePass/GamePassHandler.ashx?Action=HasPass&UserID=%d&PassID=%d"") end)
pcall(function() game:GetService(""MarketplaceService""):SetProductInfoUrl(BaseURL..""/marketplace/productinfo?assetId=%d"") end)
pcall(function() game:GetService(""MarketplaceService""):SetPlayerOwnsAssetUrl(BaseURL..""/ownership/hasasset?userId=%d&assetId=%d"") end)
pcall(function() game:SetCreatorID(0, Enum.CreatorType.User) end)

pcall(function() game:GetService(""Players""):SetChatStyle(Enum.ChatStyle.Classic) end)
pcall( function() if settings().Network.MtuOverride == 0 then settings().Network.MtuOverride = 1400 end end)

local waitingForCharacter = false;
local waitingForCharacterGuid = ""26c3de03-3381-4ab6-8e60-e415fa757eba"";


-- globals -----------------------------------------

client = game:GetService(""NetworkClient"")
visit = game:GetService(""Visit"")

-- functions ---------------------------------------
function ifSeleniumThenSetCookie(key, value)
	game:GetService(""CookiesService""):SetCookieValue(key, value)
end

function setMessage(message)
	game:SetMessage(message)
end
setMessage(""Connecting to Kornet..."")
function showErrorWindow(message, errorType, errorCategory)
	if (not loadResolved) or (not joinResolved) then
		local duration = tick() - startTime;
		if not loadResolved then
			loadResolved = true
		end
		if not joinResolved then
			joinResolved = true
		end
	elseif not playResolved then
		local duration = tick() - playStartTime;
		playResolved = true
	end
	
	game:SetMessage(message)
end

function reportError(err, message)
	print(""***ERROR*** "" .. err)
	client:Disconnect()
	wait(1)
	showErrorWindow(""Error: "" .. err, message, ""Other"")
end

function onDisconnection(peer, lostConnection)
	if lostConnection then
		showErrorWindow(""You have lost the connection to the game"", ""LostConnection"", ""LostConnection"")
	else
		showErrorWindow(""This game has shut down"", ""Kick"", ""Kick"")
	end
end

function requestCharacter(replicator)
	
	local connection
	connection = player.Changed:connect(function (property)
		if property==""Character"" then
			game:ClearMessage()
			waitingForCharacter = false
			
			connection:disconnect()
		
			if 0 then
				if not joinResolved then
					local duration = tick() - startTime;
					joinResolved = true
					
					playStartTime = tick()
					playResolved = false
				end
			end
		end
	end)
	
	setMessage(""Requesting character"")
	
	if 0 and not loadResolved then
		local duration = tick() - startTime;
		loadResolved = true
	end

	local success, err = pcall(function()	
		replicator:RequestCharacter()
		setMessage(""Waiting for character"")
		waitingForCharacter = true
	end)
end

function onConnectionAccepted(url, replicator)
	connectResolved = true

	local waitingForMarker = true
	
	local success, err = pcall(function()	
		if not test then 
		    visit:SetPing("""", 300) 
		end
		game:SetMessageBrickCount()
		replicator.Disconnection:connect(onDisconnection)
		
		local marker = replicator:SendMarker()
		
		marker.Received:connect(function()
			waitingForMarker = false
			requestCharacter(replicator)
		end)
	end)
	
	if not success then
		reportError(err,""ConnectionAccepted"")
		return
	end
	
	while waitingForMarker do
		workspace:ZoomToExtents()
		wait(0.5)
	end
end

function onConnectionFailed(_, error)
	showErrorWindow(""Failed to connect to the Game. (ID="" .. error .. "")"", ""ID"" .. error, ""Other"")
end

function onConnectionRejected()
	connectionFailed:disconnect()
	showErrorWindow(""This game is not available. Please try another"", ""WrongVersion"", ""WrongVersion"")
end

idled = false
function onPlayerIdled(time)
	if time > 20*60 then
		showErrorWindow(string.format(""You were disconnected for being idle %d minutes"", time/60), ""Idle"", ""Idle"")
		client:Disconnect()	
		if not idled then
			idled = true
		end
	end
end

pcall(function() settings().Diagnostics:LegacyScriptMode() end)
coroutine.wrap(function()
	game:SetRemoteBuildMode(true)
	
	setMessage(""Fetching place info from Kornet"")
	local joinScriptUrl = nil
	local AttemptCount = 0
	local success, result = nil, nil
	while true do
		success, result = pcall(function()	
			return game:HttpPost( BaseURL..""/Game/join.ashx?placeId=""..tostring(PlaceId)..""&ticket={ticketEncoded}"", ""{{}}"", true, ""application/json"")
		end)

		if success then
			local JSONResponse = game:GetService(""HttpService""):JSONDecode(result)
			if JSONResponse[""status""] == 1 then
				setMessage(""Waiting for Server to start... ( This may take a while ) [ ""..tostring(AttemptCount).."" ]"")
			elseif JSONResponse[""status""] == 2 then
				setMessage(""Server Found! Connecting..."")
				joinScriptUrl = JSONResponse[""joinScriptUrl""]
				break
			else
				setMessage(""RequestFailed, message: ""..JSONResponse[""message""])
				error(""RequestFailed, message: ""..JSONResponse[""message""])
			end
			if AttemptCount > 15 then
				setMessage(""Placelauncher request timed out, please try again later"")
				error(""Placelauncher request timed out, please try again later"")
			end
			wait(3)
			AttemptCount = AttemptCount + 1
		else
			setMessage(""Failed to get place launcher info: ""..result)
			error(""Failed to get place launcher info: ""..result)
		end
	end

	if not joinScriptUrl then
		setMessage(""Failed to get join script, please try again later"")
		error(""Failed to get join script"")
	end

	local success, result = pcall(function()	
		return game:HttpGet(joinScriptUrl, true)
	end)
	if not success then
		setMessage(""Failed to get join script: ""..result)
		error(""Failed to get join script: ""..result)
	end
	
	local JSONResponse = game:GetService(""HttpService""):JSONDecode(result:sub(result:find(""\n"", 1, true)+1))

	local MachineAddress = JSONResponse[""MachineAddress""]
	local ServerPort = JSONResponse[""ServerPort""]
	local PlayerUsername = JSONResponse[""UserName""]
	local PlayerId = JSONResponse[""UserId""]
	local AccountAge = JSONResponse[""AccountAge""]
	local GameSessionId = JSONResponse[""SessionId""]
	local CharacterAppearance = JSONResponse[""CharacterAppearance""]

	setMessage(""Welcome, ""..PlayerUsername..""! Connecting to Kornet..."")
	wait(1.5)

	client.ConnectionAccepted:connect(onConnectionAccepted)
	client.ConnectionRejected:connect(onConnectionRejected)
	connectionFailed = client.ConnectionFailed:connect(onConnectionFailed)
	client.Ticket = """"	
	
	local ConnectionAttempt = 0
	while true do
		setMessage(""Connecting to Gameserver... [ ""..tostring(ConnectionAttempt).."" ]"")

		local isConnectionSuccessful, player = pcall(function() 
			playerConnectSucces, player = pcall(function() return client:PlayerConnect(PlayerId, MachineAddress, ServerPort, 0, threadSleepTime) end)
			if not playerConnectSucces then
				player = game:GetService(""Players""):CreateLocalPlayer(0)
				client:Connect(MachineAddress, ServerPort, 0, threadSleepTime)
			end
			return player
		end)
		if isConnectionSuccessful then
			break
		else
			if ConnectionAttempt > 5 then
				error(""Failed to connect to server: ""..player)
			end
			ConnectionAttempt = ConnectionAttempt + 1
			wait(2)
		end
	end

	player:SetSuperSafeChat(false)

	pcall(function() player:SetUnder13(false) end)
	pcall(function() player:SetMembershipType(Enum.MembershipType[JSONResponse[""MembershipType""]]) end)
	pcall(function() player:SetAccountAge(AccountAge) end)
	pcall(function() player.Name = PlayerUsername end)
	pcall(function() player.UserId = PlayerId end)
	pcall(function() client:SetGameSessionID(GameSessionId) end)
	pcall(function() game:SetPlaceID(PlaceId, false) end)
	pcall(function() player.ChatMode = Enum.ChatMode.TextAndMenu end)
	
	player.Idled:connect(onPlayerIdled)
	player.CharacterAppearance = CharacterAppearance
	game:GetService(""Players""):SetChatStyle(Enum.ChatStyle[JSONResponse[""ChatStyle""]])

	pcall(function() game:SetScreenshotInfo("""") end)
	pcall(function() game:SetVideoInfo('<?xml version=""1.0""?><entry xmlns=""http://www.w3.org/2005/Atom"" xmlns:media=""http://search.yahoo.com/mrss/"" xmlns:yt=""http://gdata.youtube.com/schemas/2007""><media:group><media:title type=""plain""><![CDATA[ROBLOX Place]]></media:title><media:description type=""plain""><![CDATA[ For more games visit https://www.kornet.lat]]></media:description><media:category scheme=""http://gdata.youtube.com/schemas/2007/categories.cat"">Games</media:category><media:keywords>ROBLOX, video, free game, online virtual world</media:keywords></media:group></entry>') end)
end)()";

				var RSA = services.rsaSign;
				var signature = RSA.SignScript(Script, false);
				var result = $"{signature}\r\n{Script}";
				
				return Content(result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"2014 join error: {ex}");
				return StatusCode(500, "Failed to generate game script");
			}
		}

		[HttpGetBypass("/game/player2014/join")]
		public Task<MVC.IActionResult> Player2014Join() => Join2014();

		[HttpGetBypass("/game/player2014/host")]
		public Task<MVC.IActionResult> Player2014Host() => Host2014();
	}
}