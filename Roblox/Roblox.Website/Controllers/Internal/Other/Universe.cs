using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Website.Middleware;
using MVC = Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Universe : ControllerBase 
    {		
           [HttpGetBypass("toolbox-service/v1/{type}")]
    public async Task<dynamic> GetToolBoxService([FromRoute] string type, [FromQuery] string sortType, [FromQuery] int limit = 30, [FromQuery] string? cursor = null, [FromQuery] string? keyword = null)
    {
        CatalogSearchRequest request = new CatalogSearchRequest
        {
            keyword = keyword,
            category = type,
            subcategory = type,
            sortType = sortType,
            limit = limit,
            cursor = cursor
        };
        var searchResults = await services.assets.SearchCatalog(request);
        return new
        {
            totalResults = searchResults.data!.Count(),
            filteredKeyword = searchResults.keyword,
            searchDebugInfo = (string?)null,
            spellCheckerResult = new
            {
                correctionState = 0,
                correctedQuery = (string?)null,
                userQuery = (string?)null,
            },
            queryFacets = new
            {
                appliedFacets = new List<object>(),
                availableFacets = new List<object>(),
            },
            imageSearchStatus = (string?)null,
            previousPageCursor = searchResults.previousPageCursor,
            nextPageCursor = searchResults.nextPageCursor,
            data = searchResults.data!.Select(c => new
            {
                id = c.id,
                name = (string?)null,
                searchResultSource = "LexicalWithSort"
            })
        };
    }
    [HttpPostBypass("toolbox-service/v1/items/details")]
    public async Task<dynamic> GetToolBoxServiceDetails([FromBody] WebsiteModels.Catalog.MultiGetRequest request)
    {
        var multiGetResults = await services.assets.MultiGetInfoById(request.items.Select(c => c.id));
        return new
        {
            data = multiGetResults.Select(c =>
            {
                return new
                {
                    asset = new
                    {
                        audioDetails = (string?)null,
                        id = c.id,
                        name = c.name,
                        typeId = (int)c.assetType,
                        assetSubTypes = new List<int>(),
                        assetGenres = c.genres,
                        isEndorsed = false,
                        description = c.description,
                        duration = 0,
                        hasScripts = c.assetType == Models.Assets.Type.Model || c.assetType == Models.Assets.Type.Plugin,
                        createdUtc = c.createdAt,
                        updatedUtc = c.updatedAt,
                        creatingUniverseId = (string?)null,
                        isAssetHashApproved = c.moderationStatus == ModerationStatus.ReviewApproved,
                        // TODO: Asset privacy options
                        visibilityStatus = c.moderationStatus == ModerationStatus.ReviewApproved,
                        socialLinks = new List<object>(),
                    },
                    creator = new
                    {
                        id = c.creatorTargetId,
                        name = c.creatorName,
                        type = (int)c.creatorType,
                        isVerifiedCreator = false,
                        latestGroupUpdaterUserId = (string?)null,
                        latestGroupUpdaterUserName = (string?)null,
                    },
                    // TODO: Votes
                    voting = new
                    {
                        showVotes = false,
                        upVotes = 0,
                        downVotes = 0,
                        canVote = false,
                        userVote = (string?)null,
                        hasVoted = false,
                        voteCount = 0,
                        upVotePercent = 0,
                    },
                    fiatProduct = new
                    {
                        currencyCode = "USD",
                        quantity = new
                        {
                            significand = 0,
                            exponent = 0,
                        },
                        published = true,
                        purchasable = true,
                    }
                };
            })
        };
    }
		[HttpGetBypass("Game/LoadPlaceInfo.ashx")]
		public MVC.IActionResult LoadPlaceInfo([Required] long placeId)
		{
			return Ok();
		}
		
		[HttpPostBypass("game/load-place-info")]
        public async Task<dynamic> LoadPlaceInfo()
        {
            var placeId = Request.Headers["roblox-place-id"];
            long.TryParse(placeId, out long assetId);
            var details = await services.assets.GetAssetCatalogInfo(assetId);
            var jsonData = new
            {
                CreatorId =  details.creatorTargetId,
                CreatorType = "User",
                PlaceVersion = details.id,
                GameId = assetId,
                IsRobloxPlace = details.creatorTargetId == 1
            };
            string jsonString = JsonConvert.SerializeObject(jsonData);
            return Content(jsonString, "application/json");
        }
		
		[HttpGetBypass("v1.1/game-start-info")]
        public async Task<dynamic> GameStartInfo(long universeId)
        {
			var placeId = await services.games.GetRootPlaceId(universeId);
			var RigType = await services.games.GetRigType(placeId);
            return new
            {
				gameAvatarType = RigType switch
				{
					"playerChoice" => "PlayerChoice",
					"MorphToR6" => "MorphToR6",
					"MorphToR15" => "MorphToR15",
					_ => "PlayerChoice"
				},
                allowCustomAnimations = "True",
                universeAvatarCollisionType = "OuterBox",
                universeAvatarBodyType = "Standard",
                jointPositioningType = "ArtistIntent",
                message = "",
                universeAvatarMinScales = new
                {
                    height = 0.9,
                    width = 0.7,
                    head = 0.95,
                    depth = 0.0,
                    proportion = 0.0,
                    bodyType = 0.0
                },
                universeAvatarMaxScales = new
                {
                    height = 1.05,
                    width = 1.0,
                    head = 1.0,
                    depth = 0.0,
                    proportion = 1.0,
                    bodyType = 1.0
                },
                universeAvatarAssetOverrides = new List<object>(),
                moderationStatus = ""
            };
        }
		
		[HttpPostBypass("/game/validate-machine")]
        public dynamic ValidateMachine()
        {
            return new
            {
                success = true,
                message = "",
            };
        }
		
		[HttpGetBypass("/universes/validate-place-join")]
		public async Task<string> ValidatePlaceJoin()
		{
			return "true";
		}
		
		[HttpGetBypass("universal-app-configuration/v1/behaviors/app-policy/content")]
        public dynamic AppPolicy()
        {
            string policyContent = System.IO.File.ReadAllText(Configuration.JsonDataDirectory + "AppPolicy.json");
            dynamic? policyJson = JsonConvert.DeserializeObject<ExpandoObject>(policyContent);
            return policyJson ?? "";
        }
		
		[HttpGetBypass("universal-app-configuration/v1/behaviors/app-patch/content")]
        public dynamic AppPatch()
        {
            List<long> CanaryUserIds = new List<long>();
            return new 
            {
                SchemeVersion = "1",
                CanaryUserIds,
                CanaryPercentage = 0,
            };
        }
		
        [HttpGetBypass("universes/get-universe-places")]
        public async Task<dynamic> GetPlaces(long universeId)
        {
            var place = await services.games.GetRootPlaceId(universeId);
            var placeInfo = await services.assets.GetAssetCatalogInfo(place);
            return new
            {
                FinalPage = true,
                RootPlace = place,
                Places = new
                {
                    PlaceId = place,
                    Name = placeInfo.name,
                },
                PageSize = 50
            };
        }
        
        [HttpGetBypass("universes/get-universe-containing-place")]
        public async Task<dynamic> GetUniverse(long placeid)
        {
            return new
            {
                UniverseId = await services.games.GetUniverseId(placeid)
            };
        }

        [HttpGetBypass("v1/gametemplates")]
        public async Task<dynamic> GameTemplates()
        {
            var templates = await services.games.MultiGetPlaceDetails(services.assets.getStarterPlaces.Values.ToList());
            return new
            {
                data = templates.Select(c => new
                {
                    gameTemplateType = "Generic",
                    hasTutorials = false,
                    universe = new
                    {
                        id = c.universeId,
                        name = c.name,
                        description = c.description ?? "skbidii",
                        isArchived = false,
                        rootPlaceId = c.universeRootPlaceId,
                        isActive = true,
                        privacyType = "Public",
                        creatorType = "User",
                        creatorTargetId = c.builderId,
                        creatorName = c.builder,
                        created = c.created,
                        updated = c.updated
                    }
                })
            };
        }
	}
}