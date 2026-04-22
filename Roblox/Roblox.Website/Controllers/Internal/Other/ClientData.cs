using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Dto.Users;
using Roblox.Services.Exceptions;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using Roblox.Exceptions;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class ClientData : ControllerBase 
    {
		private class OAuthAuthorizationCode
		{
			public long userId { get; set; }
			public string? redirectUri { get; set; }
			public string? nonce { get; set; }
			public string? state { get; set; }
			public DateTimeOffset expiresAt { get; set; }
		}

		private class StudioLoginRequest
		{
			public string? username { get; set; }
			public string? cvalue { get; set; }
			public string password { get; set; } = "";
		}

		private static readonly ConcurrentDictionary<string, OAuthAuthorizationCode> OAuthCodes = new();
		private static readonly TimeSpan OAuthCodeLifetime = TimeSpan.FromMinutes(5);

		private async Task<string> GetRequestBody()
		{
			HttpContext.Request.EnableBuffering();
			using var reader = new StreamReader(
				HttpContext.Request.Body,
				Encoding.UTF8,
				detectEncodingFromByteOrderMarks: false,
				bufferSize: 1024,
				leaveOpen: true
			);

			var body = await reader.ReadToEndAsync();
			HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
			return body;
		}

		private async Task RateLimitCheck()
		{
			var loginKey = "LoginAttemptCountV1:" + GetIP();
			var attemptCount = (await services.cooldown.GetBucketDataForKey(loginKey, TimeSpan.FromMinutes(10))).ToArray();
			if (!await services.cooldown.TryIncrementBucketCooldown(loginKey, 15, TimeSpan.FromMinutes(10), attemptCount, true))
				throw new ForbiddenException(15, "Too many attempts, please wait about 10 minutes before retrying!");
		}

		private static (string username, string? twoFactorCode) ParseUsernameAndTwoFactor(string rawUsername)
		{
			if (string.IsNullOrWhiteSpace(rawUsername))
				return ("", null);

			var semicolonSplit = rawUsername.Split(';');
			if (semicolonSplit.Length == 2)
				return (semicolonSplit[0], semicolonSplit[1]);

			var pipeSplit = rawUsername.Split('|');
			if (pipeSplit.Length == 2)
				return (pipeSplit[0], pipeSplit[1]);

			return (rawUsername, null);
		}

		private async Task<UserInfo> ValidateStudioLogin(string username, string password, string? twoFactorCode)
		{
			try
			{
				FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
			}
			catch (RobloxException)
			{
				throw new RobloxException(503, 0, "Login is currently disabled. Please try again later.");
			}

			await RateLimitCheck();

			UserInfo userInfo;
			try
			{
				userInfo = await services.users.GetUserByName(username);
			}
			catch (RecordNotFoundException)
			{
				throw new ForbiddenException(1, "Incorrect username or password. Please try again.");
			}

			try
			{
				if (!await services.users.VerifyPassword(userInfo.userId, password))
					throw new ForbiddenException(1, "Incorrect username or password. Please try again.");
			}
			catch (RecordNotFoundException)
			{
				throw new ForbiddenException(4, "Your account has been locked. Please reset your password to unlock your account.");
			}

			if (await services.twoFactor.IsEnabled(userInfo.userId))
			{
				if (string.IsNullOrWhiteSpace(twoFactorCode))
					throw new ForbiddenException(2, "2FA is enabled. Please login with username;2FACode.");

				if (!await services.twoFactor.VerifyCode(userInfo.userId, twoFactorCode))
					throw new ForbiddenException(1, "Incorrect 2FA code. Please try again.");
			}

			return userInfo;
		}

		private async Task<string> CreateSessionAndSetCookie(long userId)
		{
			var sessionCookie = SessionMiddleware.CreateJwt(new JwtEntry
			{
				sessionId = await services.users.CreateSession(userId),
				createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
			});

			HttpContext.Response.Cookies.Append(SessionMiddleware.CookieName, sessionCookie, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				Expires = DateTimeOffset.Now.AddDays(364),
				IsEssential = true,
				Path = "/",
				SameSite = SameSiteMode.Lax,
			});

			return sessionCookie;
		}

		private static string RandomToken(int byteLength = 32)
		{
			return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');
		}

		private static string BuildUnsignedJwt(object payload)
		{
			var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
				.TrimEnd('=').Replace('+', '-').Replace('/', '_');
			var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)))
				.TrimEnd('=').Replace('+', '-').Replace('/', '_');
			return $"{header}.{body}.";
		}

		private static void PruneExpiredOAuthCodes()
		{
			var now = DateTimeOffset.UtcNow;
			foreach (var kvp in OAuthCodes)
			{
				if (kvp.Value.expiresAt <= now)
					OAuthCodes.TryRemove(kvp.Key, out _);
			}
		}
		[HttpGet("login/negotiate.ashx"), HttpGet("login/negotiateasync.ashx")]
		public object Negotiate(string suggest)
		{
			HttpContext.Response.Cookies.Append(".ROBLOSECURITY", suggest, new CookieOptions
			{
				Domain = null,
				HttpOnly = true,
				Secure = true,
				Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(364)),
				IsEssential = true,
				Path = "/",
				SameSite = SameSiteMode.Lax,
			});

			return suggest;
		}	
		
		// TODO: Before source release, make these actually fucking work
		// It's so incredibly insecure having these patched in RCC, but i cannot find a fix for some reason cause RCC SUCKS. 
		// (i know why now and i'm just too lazy to fix it, but please do this in the future)
 		[HttpGetBypass("GetAllowedSecurityKeys")]
        public MVC.ActionResult<dynamic> AllowedSecurity()
        {
            return true;
        }
		
		[HttpGetBypass("GetAllowedMD5Hashes")]
        public MVC.ActionResult<dynamic> AllowedMD5Hashes()
        {
            List<string> allowedList = new List<string>()
            {
				"97e93df61c3357531585cebb22d2edff",
				"053974fb1131dda3fc75534b08576b67",
				"2e71951cc3566e2ab558b9f8a8f1c510",
				"0f84ee329a636e6c23829514d1adc89c",
            };

            return new { data = allowedList };
        }

        [HttpGetBypass("GetAllowedSecurityVersions")]
        public MVC.ActionResult<dynamic> AllowedSecurityVersions()
        {
            List<string> allowedList = new List<string>()
            {  	
				"0.463.0pcplayer",
				"0.450.0pcplayer",
				"0.395.0pcplayer",
				"0.376.0pcplayer",
				"0.355.0pcplayer",
				"2.355.0iosapp",
				"0.314.0pcplayer",
				"0.300.1pcplayer",
				"0.300.0pcplayer",
				"0.285.0pcplayer",
				"0.283.0pcplayer",
				"0.275.0pcplayer",
				"0.235.0pcplayer",
				"0.206.0pcplayer",
				"0.201.0pcplayer",
				"INTERNALandroidapp",
				"INTERNALiosapp",
				"INTERNALpcplayer"
            };
			
            return new { data = allowedList };
        }
		
		[HttpGetBypass("Setting/QuietGet/{type}")]
		public MVC.ActionResult<dynamic> GetAppSettings(string type, [MVC.FromQuery] string? apiKey = "")
		{
			try
			{
				if (!Configuration.AllowedQuietGetJson.Any(x => x.Equals(type, StringComparison.OrdinalIgnoreCase)))
				{
					Console.WriteLine($"[RetrieveClientFFlags] disallowed JSON trying to be requested!");
					return "Go away";
				}

				bool use2015 = apiKey.Equals("2015MRCC-2015-2015-2015-2015MidRCC15", StringComparison.OrdinalIgnoreCase);
				
				string fileName = type;
				if (use2015)
				{
					fileName = type + "2015";
					Console.WriteLine($"[RetrieveClientFFlags] Using 2015 flags for: {type}");
				}

				string jsonFilePath = Path.Combine(Configuration.JsonDataDirectory, fileName + ".json");
				
				if (use2015 && !System.IO.File.Exists(jsonFilePath))
				{
					Console.WriteLine($"[RetrieveClientFFlags] 2015 flasg not found, falling back");
					jsonFilePath = Path.Combine(Configuration.JsonDataDirectory, type + ".json");
				}

				string jsonContent = System.IO.File.ReadAllText(jsonFilePath);
				dynamic? clientAppSettingsData = JsonConvert.DeserializeObject<ExpandoObject>(jsonContent);

				return clientAppSettingsData ?? "";
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[RetrieveClientFFlags] could not get FFlags: {ex.Message}");
				return new {};
			}
		}

		[HttpGetBypass("studio-open-place/v1/openplace")]
public IActionResult OpenPlace()
{
    var response = new
    {
        universe = new
        {
            Id = 28220420,
            RootPlaceId = 95206881,
            Name = "Baseplate",
            IsArchived = false,
            CreatorType = "User",
            CreatorTargetId = 998796,
            PrivacyType = "Public",
            Created = "2013-11-01T08:47:14.07+00:00",
            Updated = "2023-05-02T22:03:01.107+00:00"
        },
        teamCreateEnabled = false,
        place = new
        {
            Creator = new
            {
                CreatorType = "User",
                CreatorTargetId = 998796
            }
        }
    };

    return new JsonResult(response);
}
		[HttpGetBypass("universal-app-configuration/v1/behaviors/studio/content")]
public async Task<IActionResult> GetStudioBehaviorConfig()
{
    var path = Path.Combine(Configuration.JsonDataDirectory, "studio.json");

    if (!System.IO.File.Exists(path))
        return NotFound("{}");

    var content = await System.IO.File.ReadAllTextAsync(path);
    return Content(content, "application/json");
}
[HttpGetBypass("studio-login/v1/login")]
[HttpPostBypass("studio-login/v1/login")]
public async Task<IActionResult> StudioLogin()
{
	UserInfo userInfo;
	if (userSession != null)
	{
		userInfo = await services.users.GetUserById(userSession.userId);
	}
	else
	{
		var requestBody = await GetRequestBody();
		if (string.IsNullOrWhiteSpace(requestBody))
			throw new BadRequestException(3, "Username and Password are required. Please try again.");

		StudioLoginRequest? loginRequest;
		try
		{
			loginRequest = JsonConvert.DeserializeObject<StudioLoginRequest>(requestBody);
		}
		catch (Exception)
		{
			throw new BadRequestException(3, "Username and Password are required. Please try again.");
		}

		var usernameRaw = loginRequest?.username ?? loginRequest?.cvalue;
		var password = loginRequest?.password ?? "";
		if (string.IsNullOrWhiteSpace(usernameRaw) || string.IsNullOrWhiteSpace(password))
			throw new BadRequestException(3, "Username and Password are required. Please try again.");

		var parsed = ParseUsernameAndTwoFactor(usernameRaw);
		if (string.IsNullOrWhiteSpace(parsed.username))
			throw new BadRequestException(3, "Username and Password are required. Please try again.");

		userInfo = await ValidateStudioLogin(parsed.username, password, parsed.twoFactorCode);
		await CreateSessionAndSetCookie(userInfo.userId);
	}

	var response = new
	{
		user = new
		{
			UserId = userInfo.userId,
			Username = userInfo.username,
			AgeBracket = 0,
			Roles = Array.Empty<string>(),
			Email = new
			{
				value = "",
				isVerified = false
			},
			IsBanned = false,
			DisplayName = userInfo.username
		},
		userAgreements = Array.Empty<object>()
	};

	return new JsonResult(response);
}
		[HttpGetBypass("Setting/24")]
		public async Task<MVC.ActionResult> GetAppSettings2014()
		{
			string json = "2014LFFlags";
			json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "2014LFFlags.json");
			string content = await System.IO.File.ReadAllTextAsync(json);
			return Content(content, "text/plain");
		}
		

[HttpGetBypass("v1/settings/application/{applicationName?}")]
[HttpGetBypass("v2/settings/application/{applicationName?}")]
[HttpPostBypass("v1/settings/application/{applicationName?}")]
[HttpPostBypass("v2/settings/application/{applicationName?}")]
public async Task<IActionResult> RCCNewApplication(
    [FromRoute] string? applicationName,
    [FromQuery(Name = "applicationName")] string? applicationNameQuery)
{
    string? resolvedName = applicationName ?? applicationNameQuery;
    
    string json = "PCDesktopClient";
    if (!string.IsNullOrEmpty(resolvedName))
    {
        switch (resolvedName)
        {
            case "PCDesktopClient":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "PCDesktopClient.json");
                break;
            case "StudioApp":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "StudioApp.json");
                break;
            case "PCStudioApp":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "PCStudioApp.json");
                break;
            case "AndroidApp":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "AndroidApp.json");
                break;
            case "6sxp8X2Y02lwkeueueeuww39":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "RCCService.json");
                break;
            case "RCCServicelwkeueueeuww39":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "RCCService.json");
                break;
             case "iOSApp":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "iOSApp.json");
                break;
             case "iOSAppSettings":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "iOSAppSettings.json");
                break;
            case "GD5Z5gO1n0gYX1P":
                json = System.IO.Path.Combine(Configuration.JsonDataDirectory, "PCDesktopClient.json");
                break;
        }
    }

    if (!System.IO.File.Exists(json))
        return NotFound("{}");

    string content = await System.IO.File.ReadAllTextAsync(json);
    return Content(content, "text/plain");
}
[HttpGetBypass("oauth/v1/authorize")]
public IActionResult OAuthAuthorize([FromQuery] string? state = "", [FromQuery(Name = "redirect_uri")] string? redirectUri = "", [FromQuery] string? nonce = "")
{
    if (userSession == null)
    {
		var qs = HttpContext.Request.QueryString.HasValue ? HttpContext.Request.QueryString.Value : "";
		var returnUrl = Uri.EscapeDataString($"/oauth/v1/authorize{qs}");
		return Redirect($"/UnsecuredContent/index.html?returnUrl={returnUrl}");
    }

	PruneExpiredOAuthCodes();
	var code = RandomToken(24);
	OAuthCodes[code] = new OAuthAuthorizationCode
	{
		userId = userSession.userId,
		redirectUri = redirectUri,
		nonce = nonce,
		state = state,
		expiresAt = DateTimeOffset.UtcNow.Add(OAuthCodeLifetime),
	};

	var appRedirect = !string.IsNullOrWhiteSpace(redirectUri)
		? $"{redirectUri}{(redirectUri.Contains('?') ? "&" : "?")}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state ?? "")}"
		: $"roblox-studio-auth-kornet:/?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state ?? "")}";

    var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta http-equiv=""refresh"" content=""0;url={appRedirect}"">
    <title>Studio OAuth Login</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #212529;
            color: #fff;
            margin: 0;
            padding: 0;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
        }}
        .container {{
            text-align: center;
            max-width: 400px;
            padding: 20px;
            background-color: #30363b;
            border-radius: 10px;
            box-shadow: 0 0 10px rgba(0, 0, 0, 0.2);
        }}
        h1 {{ color: inherit; }}
        a.btn {{
            display: inline-block;
            background-color: #4CAF50;
            color: white;
            padding: 10px 20px;
            border-radius: 5px;
            text-decoration: none;
            transition: background-color 0.3s ease;
        }}
        a.btn:hover {{ background-color: #45a049; }}
        footer {{
            margin-top: 20px;
            font-size: 12px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Studio OAuth Login</h1>
        <p>Authenticated as user ID {userSession.userId}. Redirecting to Studio OAuth...</p>
        <a class=""btn"" href=""{appRedirect}"">Continue</a>
    </div>
    <footer>
        <p>OAuth code expires in {OAuthCodeLifetime.TotalMinutes:0} minutes.</p>
    </footer>
</body>
</html>";

    return Content(html, "text/html");
}
[HttpGetBypass("timespent/pbe")]
[HttpPostBypass("timespent/pbe")]
public IActionResult TimeSpentPbe2023Lol()
{
    return Ok();
}
[HttpGetBypass("oauth/v1/token")]
[HttpPostBypass("oauth/v1/token")]
public async Task<IActionResult> OAuthToken([FromQuery] string? code = null, [FromQuery] string? grant_type = null, [FromQuery(Name = "redirect_uri")] string? redirectUri = null)
{
    string? postedCode = null;
	string? postedGrantType = null;
	string? postedRedirect = null;

	if (Request.HasFormContentType)
	{
		var form = await Request.ReadFormAsync();
		postedCode = form["code"].ToString();
		postedGrantType = form["grant_type"].ToString();
		postedRedirect = form["redirect_uri"].ToString();
	}

	var resolvedCode = string.IsNullOrWhiteSpace(code) ? postedCode : code;
	var resolvedGrantType = string.IsNullOrWhiteSpace(grant_type) ? postedGrantType : grant_type;
	var resolvedRedirectUri = string.IsNullOrWhiteSpace(redirectUri) ? postedRedirect : redirectUri;

	if (!string.Equals(resolvedGrantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
		return BadRequest(new { error = "unsupported_grant_type" });

	if (string.IsNullOrWhiteSpace(resolvedCode) || !OAuthCodes.TryRemove(resolvedCode, out var authCode))
		return BadRequest(new { error = "invalid_grant" });

	if (authCode.expiresAt <= DateTimeOffset.UtcNow)
		return BadRequest(new { error = "invalid_grant", error_description = "authorization code expired" });

	if (!string.IsNullOrWhiteSpace(authCode.redirectUri) &&
		!string.IsNullOrWhiteSpace(resolvedRedirectUri) &&
		!string.Equals(authCode.redirectUri, resolvedRedirectUri, StringComparison.Ordinal))
	{
		return BadRequest(new { error = "invalid_grant", error_description = "redirect_uri mismatch" });
	}

	var userInfo = await services.users.GetUserById(authCode.userId);
	var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();

	var accessToken = BuildUnsignedJwt(new
	{
		sub = userInfo.userId.ToString(),
		preferred_username = userInfo.username,
		scope = "openid profile",
		iat = now,
		exp = expiresAt,
		jti = RandomToken(18),
	});

	var idToken = BuildUnsignedJwt(new
	{
		sub = userInfo.userId.ToString(),
		name = userInfo.username,
		nickname = userInfo.username,
		preferred_username = userInfo.username,
		profile = $"{Request.Scheme}://{Request.Host}/users/{userInfo.userId}/profile",
		nonce = authCode.nonce ?? "",
		iat = now,
		exp = expiresAt,
	});

	return new JsonResult(new
	{
		access_token = accessToken,
		refresh_token = RandomToken(48),
		token_type = "Bearer",
		expires_in = 900,
		id_token = idToken,
		scope = "openid profile"
	});
}
[HttpGetBypass("oauth/.well-known/openid-configuration")]
public async Task<IActionResult> OpenIdConfiguration()
{
    var path = Path.Combine(Configuration.JsonDataDirectory, "oauth2thing.json");

    if (!System.IO.File.Exists(path))
        return NotFound("{}");

    var content = await System.IO.File.ReadAllTextAsync(path);
    return Content(content, "application/json");
}
[HttpGet("v1/player-policies-client")]
public IActionResult PlayerPoliciesClient()
{
    return new JsonResult(new
    {
        allowedExternalLinkReferences = new[] { "Discord", "YouTube", "Twitch", "Facebook" },
        arePaidRandomItemsRestricted = false,
        isPaidItemTradingAllowed = true,
        isSubjectToChinaPolicies = false
    });
}
		[HttpPostBypass("Game/ChatFilter.ashx")]
		public dynamic ChatFilter()
		{
			try
			{
				var text = HttpContext.Request.Form["text"].ToString();
				var userId = HttpContext.Request.Form["userId"].ToString();
				var placeId = HttpContext.Request.Headers["placeId"].ToString();
				var gameInstanceId = HttpContext.Request.Headers["gameInstanceID"].ToString();

				var filteredText = services.filter.FilterText(text);
				
				return new
				{
					data = new 
					{
						white = filteredText,
						black = filteredText
					}
				};
			}
			catch (Exception ex)
			{
				return new
				{
					data = new 
					{
						white = "#",
						black = "#"
					}
				};
			}
		}
		
		[HttpPostBypass("moderation/v2/filtertext")]
        [HttpPostBypass("moderation/filtertext")]
        public dynamic GetModerationText()
        {
            var text = services.filter.FilterText(HttpContext.Request.Form["text"].ToString());
            return new
            {
                success = true,
                data = new
                {
                    AgeUnder13 = text,
                    Age13OrOver = text,
                    white = text,
                    black = text
                }
            };
        }

		// // Make an actual filter function later
		// [HttpPostBypass("moderation/filtertext")]
        // public dynamic GetModerationText()
        // {
        //     //var text = FilterFunction(HttpContext.Request.Form["text"].ToString());
		// 	var text = HttpContext.Request.Form["text"].ToString();
        //     return new
        //     {
        //         success = true,
        //         data = new 
        //         {
        //             white = text,
        //             black = text
        //         }
        //     };
        // }
		
        // [HttpPostBypass("moderation/v2/filtertext")]
        // public dynamic GetModerationTextV2()
        // {
        //     //var text = FilterFunction(HttpContext.Request.Form["text"].ToString());
		// 	var text = HttpContext.Request.Form["text"].ToString();
        //     var json = new
        //     {
        //         success = true,
        //         data = new
        //         {
        //             AgeUnder13 = text,
        //             Age13OrOver = text,
        //         }
        //     };
        //     string jsonString = JsonConvert.SerializeObject(json);
        //     return Content(jsonString, "application/json");
        // }
	}
}	