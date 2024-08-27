local addonname = ...
local Options = CreateFrame("Frame", "MyAddonPanel", InterfaceOptionsFramePanelContainer)
Options:Hide()
Options.name = addonname



-- Variable for easy positioning
local lastItem

--CreateFont function for easy FontString creation
local function CreateFont(fontName, r, g, b, anchorPoint, relativeTo, relativePoint, cx, cy, xoff, yoff, text)
	local font = Options:CreateFontString(nil, "BACKGROUND", fontName)
	font:SetJustifyH("LEFT")
	font:SetJustifyV("TOP")
	if type(r) == "string" then -- r is text, no positioning
		text = r
	else
		if r then
			font:SetTextColor(r, g, b, 1)
		end
		font:SetSize(cx, cy)
		font:SetPoint(anchorPoint, relativeTo, relativePoint, xoff, yoff)
	end
	font:SetText(text)
	return font
end



local wideWidth = 600

local title = CreateFont("GameFontNormalLarge", "Custom Font Loader")
title:SetPoint("TOPLEFT",16,-12)
local ver = CreateFont("GameFontNormalSmall", "version " .. GetAddOnMetadata(addonname, "Version"))
ver:SetPoint("BOTTOMLEFT", title, "BOTTOMRIGHT", 4, 0)
local auth = CreateFont("GameFontNormalSmall", "by "..GetAddOnMetadata(addonname, "Author"))
auth:SetPoint("BOTTOMLEFT", ver, "BOTTOMRIGHT", 3, 0)
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -4, "This addon allows you to load fonts that are compatible with LibSharedMedia-3.0.")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -40, "How to add a NEW FONT:")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -60, "1. Add your font file (.ttf) to the Fonts folder:")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -80, "     a.   \\World of Warcraft\\_classic_\\Interface\\AddOns\\CustomFontLoader\\Fonts")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -100, "2. Edit the fonts.lua file with a text editor (example: Notepad++).")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -120, "3. Copy the example font (line 4), paste it onto a new line.")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -140, "4. Replace the 'YOUR FONT NAME' with your font file name.")
local desc = CreateFont("GameFontHighlight", nil, nil, nil, "TOPLEFT", title, "BOTTOMLEFT", wideWidth, 40, 0, -160, "5. Replace 'FONT-FILE-NAME.ttf' with the name of your font file including the .ttf extension.")



-- Add the options panel to the Blizzard list
InterfaceOptions_AddCategory(Options)
