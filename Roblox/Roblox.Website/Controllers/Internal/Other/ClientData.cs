using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class ClientData : ControllerBase 
    {	
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
public IActionResult StudioLogin()
{
    var ROBLOSECURITY = "_|WARNING:-DO-NOT-SHARE-THIS.--Sharing-this-will-allow-someone-to-log-in-as-you-and-to-steal-your-ROBUX-and-items.|_DGJJD464646464dfgdgdgdCUdgjneth4iht4ih64uh4uihy4y4yuhi4yhuiyhui4yhui4uihy4huiyhu4iyhuihu4hhdghdgihdigdhuigdhuigidhugihugdgidojgijodijogdijogdjoigdjoidijogijodgijdgiojdgijodgijoF";

    var RBXID = "_|WARNING:-DO-NOT-SHARE-THIS.--Sharing-this-will-allow-someone-to-log-in-as-you-and-to-steal-your-ROBUX-and-items.|_eyJhbGciOiJIUzI1NiJ9.fakepayload";

    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        Expires = DateTimeOffset.Now.AddDays(14),
        Path = "/",
        SameSite = SameSiteMode.Lax
        // Domain = ".kornet.lat" 
    };

    HttpContext.Response.Cookies.Append(".ROBLOSECURITY", ROBLOSECURITY, cookieOptions);
    HttpContext.Response.Cookies.Append(".RBXID", RBXID, cookieOptions);

    var response = new
    {
        user = new
        {
            UserId = 1,
            Username = "Roblox",
            AgeBracket = 0,
            Roles = new string[] { },
            Email = new
            {
                value = "r*********@kornet.lat",
                isVerified = true
            },
            IsBanned = false,
            DisplayName = "Roblox"
        },
        userAgreements = new object[] { }
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
public IActionResult OAuthAuthorize([FromQuery] string? state = "")
{
    var loginUrl = $"roblox-studio-auth-kornet:/?code=a&state={state}";
    
    var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Studio Offline Login</title>
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
        <h1>Studio Offline Login</h1>
        <p>You are on the Studio Offline login page. To login, click the button below.</p>
        <a class=""btn"" href=""{loginUrl}"">Login</a>
    </div>
    <footer>
        <p>Made by Stan. Thanks to Chris for the hook help.</p>
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
public IActionResult OAuthToken()
{
    return new JsonResult(new
    {
        access_token = "eyJhbGciOiJFUzI1NiIsImtpZCI6IlBOeHhpb2JFNE8zbGhQUUlUZG9QQ3FCTE81amh3aXZFS1pHOWhfTGJNOWMiLCJ0eXAiOiJKV11234.eyJzdWIiOiIyMDY3MjQzOTU5IiwiYWlkIjoiM2Q2MWU3NDctM2ExNS00NTE4LWJiNDEtMWU3M2VhNDUyZWIwIiwic2NvcGUiOiJvcGVuaWQ6cmVhZCBwcm9maWxlOnJlYWQiLCJqdGkiOiJBVC5QbmFWVHpJU3k2YkI5TG5QYnZpTCIsIm5iZiI6MTY5MTYzOTY5OCwiZXhwIjoxNjkxNjQwNTk4LCJpYXQiOjE2OTE2Mzk2OTgsImlzcyI6Imh0dHBzOi8vYXBpcy5yb2Jsb3guY29tL29hdXRoLyIsImF1ZCI6IjcyOTA2MTAzOTc5ODc5MzQ5Nj1234.BjwMkC8Q5a_iP1Q5Th8FrS7ntioAollv_zW9mprF1ats9CD2axCvupZydVzYphzQ8TawunnYXp0Xe8k0t8ithg",
        refresh_token = "eyJhbGciOiJkaXIiLCJlbmMiOiJBMjU2Q0JDLUhTNTEyIiwia2lkIjoidGpHd1BHaURDWkprZEZkREg1dFZ5emVzRWQyQ0o1NDgtUi1Ya1J1TTBBRSIsInR5cCI6IkpXVCJ9..nKYZvjvXH6msDG8Udluuuw.PwP-_HJIjrgYdY-gMR0Q3cabNwIbmItcMEQHx5r7qStVVa5l4CbrKwJvjY-w9xZ9VFb6P70WmXndNifnio5BPZmivW5QkJgv5_sxLoCwsqB1bmEkz2nFF4ANLzQLCQMvQwgXHPMfCK-lclpVEwnHk4kemrCFOvfuH4qJ1V0Q0j0WjsSU026M67zMaFrrhSKwQh-SzhmXejhKJOjhNfY9hAmeS-LsLLdszAq_JyN7fIvZl1fWDnER_CeDAbQDj5K5ECNOHAQ3RemQ2dADVlc07VEt2KpSqUlHlq3rcaIcNRHCue4GfbCc1lZwQsALbM1aSIzF68klXs1Cj_ZmXxOSOyHxwmbQCHwY7aa16f3VEJzCYa6m0m5U_oHy84iQzsC-_JvBaeFCachrLWmFY818S-nH5fCIORdYgc4s7Fj5HdULnnVwiKeQLKSaYsfneHtqwOc_ux2QYv6Cv6Xn04tkB2TEsuZ7dFwPI-Hw2O30vCzLTcZ-Fl08ER0J0hhq4ep7B641IOnPpMZ1m0gpJJRPbHX_ooqHol9zHZ0gcLKMdYy1wUgsmn_nK_THK3m0RmENXNtepyLw_tSd5vqqIWZ5NFglKSqVnbomEkxneEJRgoFhBGMZiR-3FXMaVryUjq-N.Q_t4NGxTUSMsLVEppkTu0Q6rwt2rKJfFGuvy3s12345",
        token_type = "Bearer",
        expires_in = 899,
        id_token = "eyJhbGciOiJFUzI1NiIsImtpZCI6IkNWWDU1Mi1zeWh4Y1VGdW5vNktScmtReFB1eW15YTRQVllodWdsd3hnNzgiLCJ0eXAiOiJKV11234.eyJzdWIiOiIxIiwibmFtZSI6IlJPQkxPWCIsIm5pY2tuYW1lIjoiUk9CTE9YIiwicHJlZmVycmVkX3VzZXJuYW1lIjoiUk9CTE9YIiwiY3JlYXRlZF9hdCI6MSwicHJvZmlsZSI6Imh0dHBzOi8vd3d3LnJvYmxveC5jb20vdXNlcnMvMS9wcm9maWxlIiwibm9uY2UiOiIxMjM0NSIsImp0aSI6IklELnltd3ZjTUdpOVg4azkyNm9qd1I5IiwibmJmIjoxNjkxNjM5Njk4LCJleHAiOjE2OTE2NzU2OTgsImlhdCI6MTY5MTYzOTY5OCwiaXNzIjoiaHR0cHM6Ly9hcGlzLnJvYmxveC5jb20vb2F1dGgvIiwiYXVkIjoiNzI5MDYxMDM5Nzk4NzkzNDk2NCJ9.kZgCMJQGsariwCi8HqsUadUBMM8ZOmf_IPDoWyQY9gVX4Kx3PubDz-Q6MvZ9eU5spNFz0-PEH-G2WSvq2ljDyg",
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