local baseUrl, characterAppearanceUrl = ...

local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailScriptStarted")

pcall(function() game:GetService("ContentProvider"):SetBaseUrl(baseUrl) end)
game:GetService("ScriptContext").ScriptsDisabled = true
game:GetService("UserInputService").MouseIconEnabled = false

local player = game:GetService("Players"):CreateLocalPlayer(0)
player.CharacterAppearance = characterAppearanceUrl
player:LoadCharacterBlocking()

ThumbnailGenerator:AddProfilingCheckpoint("PlayerCharacterLoaded")


local result, requestedUrls = ThumbnailGenerator:Click("SplitObjs", 0, 0, true)
ThumbnailGenerator:AddProfilingCheckpoint("SplitObjsComplete")

return result, requestedUrls

