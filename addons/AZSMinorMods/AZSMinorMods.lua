-- AZS Minor Mods: small changes to the game's chat, in AZS Stats' look (AZSKit.lua).
--   * The level-up text is taken out of the chat ("Level up!", "You have reached level 12", ...). Lines are matched
--     without case; /azsmods hide <words> hides lines with those words too, /azsmods unhide <words> stops that.
--   * The camera goes as far out as the game allows, 50 yards (the Max Camera Distance setting at its top), and starts there. Turn it off in
--     /azsmods to keep the game's own setting.
--   * A second chat box above the game's own one collects what is said ("Say") and your party's messages, so you
--     can read them while you play. Drag its title to move it; the mouse wheel scrolls it.
-- /azsmods opens the settings window.
-- Lua 5.0 / 1.12 interface: scripts read `this`, `event`, `arg1`, `arg2`; no # operator; only the 1.12 API.

local K = AZSKIT;
local NAME = "AZS Minor Mods";

-- the words that make a chat line a level-up line (matched without case): the game's own two level-up lines,
-- "Congratulations, you have reached level 12!" (its LEVEL_UP text) and "You have gained 3% damage, 12 maximum health
-- and 0.5 health per second.", and the usual ways of saying it
local LEVEL_WORDS = { "you have reached level", "you have gained", "level up", "leveled up", "levelled up", "reached level",
                      "gained a level", "you are now level", "level-up", "ding!" };

-- the game's LEVEL_UP text in the game's language: the part before the number
local function LevelText()
	if ( type(LEVEL_UP) ~= "string" ) then return nil; end
	local s = string.find(LEVEL_UP, "%", 1, true);
	local head = s and string.sub(LEVEL_UP, 1, s - 1) or LEVEL_UP;
	head = string.lower(string.gsub(head, "^%s+", ""));
	if ( string.len(head) < 6 ) then return nil; end
	return head;
end

local function DB()
	if ( type(AZSMinorModsDB) ~= "table" ) then AZSMinorModsDB = {}; end
	local d = AZSMinorModsDB;
	if ( d.hideLevel == nil ) then d.hideLevel = true; end
	if ( d.box == nil ) then d.box = true; end
	if ( d.camera == nil ) then d.camera = true; end
	if ( type(d.words) ~= "table" ) then d.words = {}; end
	return d;
end

-- ---------------------------------------------------------------- the level-up text out of the chat
local function Hidden(msg)
	if ( type(msg) ~= "string" ) then return false; end
	local d = DB();
	local low = string.lower(string.gsub(msg, "|c%x%x%x%x%x%x%x%x", ""));
	if ( d.hideLevel ) then
		local own = LevelText();
		if ( own and string.find(low, own, 1, true) ) then return true; end
		for i = 1, table.getn(LEVEL_WORDS) do
			if ( string.find(low, LEVEL_WORDS[i], 1, true) ) then return true; end
		end
	end
	for i = 1, table.getn(d.words) do
		if ( string.find(low, d.words[i], 1, true) ) then return true; end
	end
	return false;
end

-- every chat window's AddMessage (what the game and the addons print), and the chat's own event handler
local hooked = {};
local function HookFrame(cf)
	if ( not cf or hooked[cf] or not cf.AddMessage ) then return; end
	hooked[cf] = true;
	local orig = cf.AddMessage;
	cf.AddMessage = function(self, msg, r, g, b, id, hold)
		if ( Hidden(msg) ) then return; end
		return orig(self, msg, r, g, b, id, hold);
	end;
end

local function HookChat()
	for i = 1, 7 do pcall(HookFrame, getglobal("ChatFrame" .. i)); end
	pcall(HookFrame, DEFAULT_CHAT_FRAME);
	if ( type(ChatFrame_OnEvent) == "function" and not hooked.event ) then
		hooked.event = true;
		local orig = ChatFrame_OnEvent;
		ChatFrame_OnEvent = function(ev)
			if ( type(arg1) == "string" and (ev or event) and string.find(ev or event, "^CHAT_MSG_") and Hidden(arg1) ) then return; end
			return orig(ev);
		end;
	end
end

-- ---------------------------------------------------------------- the camera at its farthest: 50
-- The game's Max Camera Distance slider is cameraDistanceMaxFactor, a factor of cameraDistanceMax (15 yards): its
-- top, 3.33, is 50 yards. Minor Mods sets it to 50 yards when the game starts (and zooms out to it once); turned off
-- in /azsmods, the game's own setting stays.
local MAX_CAMERA = 50;

local function Camera(zoomOut)
	if ( not DB().camera or not SetCVar ) then return; end
	local base = 15;
	if ( GetCVar ) then
		local ok, v = pcall(GetCVar, "cameraDistanceMax");
		if ( ok and tonumber(v) and tonumber(v) > 0 ) then base = tonumber(v); end
	end
	pcall(SetCVar, "cameraDistanceMaxFactor", string.format("%.4f", MAX_CAMERA / base));
	if ( zoomOut and CameraZoomOut ) then pcall(CameraZoomOut, MAX_CAMERA); end
end

-- ---------------------------------------------------------------- the Say & Party box
local box;

local function BoxPlace()
	local d = DB();
	box:ClearAllPoints();
	if ( d.x and d.y ) then
		box:SetPoint("BOTTOMLEFT", UIParent, "BOTTOMLEFT", K.Num(d.x), K.Num(d.y));
	elseif ( ChatFrame1 ) then
		box:SetPoint("BOTTOMLEFT", ChatFrame1, "TOPLEFT", -4, 34);     -- above the chat and its tabs
	else
		box:SetPoint("BOTTOMLEFT", UIParent, "BOTTOMLEFT", 32, 300);
	end
	if ( d.box ) then box:Show(); else box:Hide(); end
end

local function Color(kind)
	local t = ChatTypeInfo and ChatTypeInfo[kind];
	if ( t ) then return t.r or 1, t.g or 1, t.b or 1; end
	if ( kind == "PARTY" ) then return 0.67, 0.67, 1; end
	return 1, 1, 1;
end

local function BoxAdd()
	local kind = (event == "CHAT_MSG_SAY") and "SAY" or "PARTY";
	local who = arg2 or "";
	local text;
	if ( kind == "SAY" ) then text = "[" .. who .. "] says: " .. tostring(arg1 or "");
	else text = "[Party] [" .. who .. "]: " .. tostring(arg1 or ""); end
	local r, g, b = Color(kind);
	box.msg:AddMessage(text, r, g, b);
end

local function BoxBuild()
	if ( box ) then return; end
	local width = ChatFrame1 and ChatFrame1:GetWidth() or 430;
	if ( not width or width < 200 ) then width = 430; end
	box = CreateFrame("Frame", "AZSMinorModsChat", UIParent);
	box:SetWidth(width + 8);
	box:SetHeight(112);
	K.Card(box, 0.6, 0.5, 0.3);
	box:SetBackdropColor(0.04, 0.04, 0.06, 0.55);
	box:SetMovable(true);
	box:EnableMouse(true);
	if ( box.SetClampedToScreen ) then box:SetClampedToScreen(true); end
	box:RegisterForDrag("LeftButton");
	box:SetScript("OnDragStart", function() if ( not DB().lock ) then this:StartMoving(); end end);
	box:SetScript("OnDragStop", function()
		this:StopMovingOrSizing();
		local d = DB();
		d.x, d.y = this:GetLeft(), this:GetBottom();
	end);
	box.title = box:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall");
	box.title:SetPoint("TOPLEFT", box, "TOPLEFT", 8, -5);
	box.title:SetText("Say & Party");
	box.msg = CreateFrame("ScrollingMessageFrame", "AZSMinorModsChatMessages", box);
	box.msg:SetPoint("TOPLEFT", box, "TOPLEFT", 8, -18);
	box.msg:SetPoint("BOTTOMRIGHT", box, "BOTTOMRIGHT", -8, 6);
	box.msg:SetFontObject(ChatFontNormal or GameFontHighlightSmall);
	box.msg:SetJustifyH("LEFT");
	box.msg:SetMaxLines(200);
	box.msg:SetFading(false);
	box.msg:EnableMouseWheel(true);
	box.msg:SetScript("OnMouseWheel", function()
		if ( arg1 > 0 ) then this:ScrollUp(); else this:ScrollDown(); end
	end);
	box:SetScript("OnEvent", function() pcall(BoxAdd); end);
	box:RegisterEvent("CHAT_MSG_SAY");
	box:RegisterEvent("CHAT_MSG_PARTY");
	pcall(box.RegisterEvent, box, "CHAT_MSG_PARTY_LEADER");   -- not in every client
	BoxPlace();
end

-- ---------------------------------------------------------------- the settings window
local win;

local function Check(parent, label, y, get, set)
	local cb, ok = K.Templated("CheckButton", nil, parent, "UICheckButtonTemplate");
	if ( not ok or not cb.SetChecked ) then
		cb = CreateFrame("CheckButton", nil, parent);
		cb:SetNormalTexture("Interface\\Buttons\\UI-CheckBox-Up");
		cb:SetPushedTexture("Interface\\Buttons\\UI-CheckBox-Down");
		cb:SetHighlightTexture("Interface\\Buttons\\UI-CheckBox-Highlight");
		cb:SetCheckedTexture("Interface\\Buttons\\UI-CheckBox-Check");
	end
	cb:SetWidth(26);
	cb:SetHeight(26);
	cb:SetPoint("TOPLEFT", parent, "TOPLEFT", 26, y);
	local fs = parent:CreateFontString(nil, "OVERLAY", "GameFontHighlight");
	fs:SetPoint("LEFT", cb, "RIGHT", 4, 1);
	fs:SetText(label);
	cb:SetScript("OnShow", function() this:SetChecked(get() and 1 or nil); end);
	cb:SetScript("OnClick", function()
		set(this:GetChecked() and true or false);
		K.Sound(this:GetChecked() and "igMainMenuOptionCheckBoxOn" or "igMainMenuOptionCheckBoxOff");
	end);
	return cb;
end

local function WinBuild()
	win = K.Window("AZSMinorModsFrame", NAME, 380, 280);
	local hero = K.Hero(win, 340);
	hero:SetPoint("TOPLEFT", win, "TOPLEFT", 20, -30);
	K.Icon(hero.icon, "INV_Misc_Gear_02");
	hero.heading:SetText("Minor Mods");
	hero.line:SetText("Small changes to the chat.");
	Check(win, "Take the level-up text out of the chat", -104, function() return DB().hideLevel; end,
	      function(v) DB().hideLevel = v; end);
	Check(win, "Say & Party box above the chat", -132, function() return DB().box; end,
	      function(v) DB().box = v; BoxBuild(); BoxPlace(); end);
	Check(win, "Lock the box where it is", -160, function() return DB().lock; end, function(v) DB().lock = v; end);
	Check(win, "Camera as far out as it goes (50)", -188, function() return DB().camera; end,
	      function(v) DB().camera = v; Camera(v); end);
	local clear = K.Button(win, "Clear the box", 120, function() BoxBuild(); box.msg:Clear(); end);
	clear:SetPoint("BOTTOMLEFT", win, "BOTTOMLEFT", 24, 20);
	local reset = K.Button(win, "Box back above the chat", 170, function()
		local d = DB();
		d.x, d.y = nil, nil;
		BoxBuild();
		BoxPlace();
	end);
	reset:SetPoint("BOTTOMRIGHT", win, "BOTTOMRIGHT", -24, 20);
end

function AZSMinorMods_Toggle()
	if ( not win ) then
		local ok, err = pcall(WinBuild);
		if ( not ok ) then K.Print(NAME, "|cffff4040the settings window could not be made:|r " .. tostring(err)); win = nil; return; end
	end
	if ( win:IsShown() ) then win:Hide(); else win:Show(); end
end

SLASH_AZSMINORMODS1 = "/azsmods";
SLASH_AZSMINORMODS2 = "/minormods";
SlashCmdList["AZSMINORMODS"] = function(msg)
	msg = msg or "";
	local _, _, cmd, rest = string.find(msg, "^(%S*)%s*(.-)$");
	cmd = string.lower(cmd or "");
	rest = string.lower(rest or "");
	local d = DB();
	if ( cmd == "hide" and rest ~= "" ) then
		table.insert(d.words, rest);
		K.Print(NAME, "chat lines with \"" .. rest .. "\" are hidden now.");
	elseif ( cmd == "unhide" and rest ~= "" ) then
		for i = table.getn(d.words), 1, -1 do if ( d.words[i] == rest ) then table.remove(d.words, i); end end
		K.Print(NAME, "chat lines with \"" .. rest .. "\" are shown again.");
	elseif ( cmd == "box" ) then
		d.box = not d.box;
		BoxBuild();
		BoxPlace();
	else
		AZSMinorMods_Toggle();
	end
end

local loader = CreateFrame("Frame");
loader:RegisterEvent("VARIABLES_LOADED");
loader:RegisterEvent("PLAYER_ENTERING_WORLD");
loader:SetScript("OnEvent", function()
	local ok, err = pcall(HookChat);
	if ( not ok ) then K.Print(NAME, "|cffff4040the chat could not be changed:|r " .. tostring(err)); end
	pcall(Camera, event == "PLAYER_ENTERING_WORLD");
	if ( event == "VARIABLES_LOADED" ) then
		ok, err = pcall(BoxBuild);
		if ( not ok ) then K.Print(NAME, "|cffff4040the Say & Party box could not be made:|r " .. tostring(err)); end
	end
end);
pcall(HookChat);                                          -- right away too, so lines printed while loading are caught
