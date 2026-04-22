-- Video v1.0.0
local assetUrl, fileExtension, x, y, baseUrl = ...

local ThumbnailGenerator = game:GetService("ThumbnailGenerator")

pcall(function() game:GetService("ContentProvider"):SetBaseUrl(baseUrl) end)
game:GetService("ScriptContext").ScriptsDisabled = true
game:GetService("UserInputService").MouseIconEnabled = false


local ok, result, requestedUrls = pcall(function()
    return ThumbnailGenerator:ClickTexture(assetUrl, fileExtension, x, y)
end)

if ok and result then
    return result, requestedUrls
end

return ThumbnailGenerator:ClickTexture(baseUrl .. "/img/Video.png", fileExtension, x, y)
