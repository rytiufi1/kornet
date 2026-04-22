local assetUrl, baseUrl = ...

local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailScriptStarted")

pcall(function() game:GetService("ContentProvider"):SetBaseUrl(baseUrl) end)
game:GetService("ScriptContext").ScriptsDisabled = true
game:GetService("UserInputService").MouseIconEnabled = false

local objects = game:GetObjects(assetUrl)
for _, obj in pairs(objects) do
	obj.Parent = workspace
end

ThumbnailGenerator:AddProfilingCheckpoint("ObjectsLoaded")

local result, requestedUrls = ThumbnailGenerator:Click("SplitObjs", 0, 0, true)
ThumbnailGenerator:AddProfilingCheckpoint("SplitObjsComplete")

return result, requestedUrls

