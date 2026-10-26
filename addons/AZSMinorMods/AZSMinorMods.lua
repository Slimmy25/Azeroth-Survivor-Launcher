-- AZS Minor Mods: small changes to the game's chat, in AZS Stats' look (AZSKit.lua).
--   * The level-up text is taken out of the chat ("Level up!", "You have reached level 12", ...). Lines are matched
--     without case; /azsmods hide <words> hides lines with those words too, /azsmods unhide <words> stops that.
--   * The camera goes as far out as the game allows, 50 yards (the Max Camera Distance setting at its top), and starts there. Turn it off in
--     /azsmods to keep the game's own setting.
--   * A second chat box above the game's own one collects what is said ("Say") and your party's messages, so you
--     can read them while you play. Drag its title to move it; the mouse wheel scrolls it.
--   * Remove bloat from chat: the "you have learned a new spell" lines are hidden, and Say and Party chat go only
--     to the Say & Party box, not the main chat.
--   * Boss markers: every boss alive gets a raid marker, the first a Skull, the second a Cross, the third a Moon.
--   * In co-op, a friend who falls gets the Square over their head, blinking 5 times.
--   * Move the UI: unlock it, drag the player, target and party frames, the minimap, buffs, cast bar, action bars and
--     the Say & Party box where you want them, then lock it again. The places are kept (Reset puts them back).
--   * Hide the target frame: this game picks its own targets, so the frame can go.
-- /azsmods (or the gear by the minimap) opens the settings window: every one of these on or off.
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
	if ( d.bloat == nil ) then d.bloat = false; end
	if ( d.bossMarks == nil ) then d.bossMarks = true; end
	if ( d.deathMarks == nil ) then d.deathMarks = true; end
	if ( d.hideTarget == nil ) then d.hideTarget = false; end
	if ( type(d.places) ~= "table" ) then d.places = {}; end
	if ( type(d.words) ~= "table" ) then d.words = {}; end
	return d;
end

-- the "you have learned a new spell" lines (Remove bloat from chat): the game's own texts in its language, the part
-- before the spell's name, and the English ways of saying it
local LEARN_WORDS = { "you have learned a new spell", "you have learned a new ability", "you have learned", "you learned",
                      "you learn " };

local function Prefix(text)
	if ( type(text) ~= "string" ) then return nil; end
	local s = string.find(text, "%", 1, true);
	local head = s and string.sub(text, 1, s - 1) or text;
	head = string.lower(string.gsub(head, "^%s+", ""));
	if ( string.len(head) < 6 ) then return nil; end
	return head;
end

local function LearnLine(low)
	for _, g in ipairs({ "ERR_LEARN_SPELL_S", "ERR_LEARN_ABILITY_S", "ERR_LEARN_RECIPE_S" }) do
		local own = Prefix(getglobal(g));
		if ( own and string.find(low, own, 1, true) ) then return true; end
	end
	for i = 1, table.getn(LEARN_WORDS) do
		if ( string.find(low, LEARN_WORDS[i], 1, true) ) then return true; end
	end
	return false;
end

-- ---------------------------------------------------------------- the level-up text out of the chat
local function Hidden(msg)
	if ( type(msg) ~= "string" ) then return false; end
	local d = DB();
	local low = string.lower(string.gsub(msg, "|c%x%x%x%x%x%x%x%x", ""));
	if ( d.bloat and LearnLine(low) ) then return true; end
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
			local e = ev or event;
			-- Remove bloat from chat: Say and Party only in the Say & Party box
			if ( DB().bloat and DB().box and (e == "CHAT_MSG_SAY" or e == "CHAT_MSG_PARTY" or e == "CHAT_MSG_PARTY_LEADER") ) then return; end
			if ( type(arg1) == "string" and e and string.find(e, "^CHAT_MSG_") and Hidden(arg1) ) then return; end
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
	box:SetScript("OnDragStart", function() if ( DB().unlocked ) then this:StartMoving(); end end);
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

-- ---------------------------------------------------------------- raid markers on bosses, and on friends who fall
-- The game's raid markers (they work alone and in co-op): the bosses alive get a Skull, a Cross and a Moon, in the
-- order they came (AZS_GetBosses). A marker needs the boss as a unit: the target, the mouseover, a friend's target,
-- or, when none of those is it, a quick target-and-back by its name.
local BOSS_ICONS = { 8, 7, 5 };                          -- Skull, Cross, Moon
local SQUARE = 6;
local marked = {};                                        -- boss id -> its marker
local tries = {};                                         -- boss id -> target-and-back tries (at most 3)
local fallen = {};                                        -- party unit -> was down at the last look
local blinks = {};                                        -- party unit -> { left = times, at = next change, on = shown }

local function Units()
	local u = { "target", "mouseover", "targettarget" };
	for i = 1, 4 do table.insert(u, "party" .. i .. "target"); end
	return u;
end

local function Mark(unit, icon)
	if ( not SetRaidTarget or not UnitExists(unit) ) then return false; end
	if ( GetRaidTargetIndex and GetRaidTargetIndex(unit) == icon ) then return true; end
	return pcall(SetRaidTarget, unit, icon);
end

local function MarkBoss(name, icon, id)
	local list = Units();
	for i = 1, table.getn(list) do
		local u = list[i];
		if ( UnitExists(u) and UnitName(u) == name and not UnitIsPlayer(u) and not UnitIsDead(u) ) then
			return Mark(u, icon);
		end
	end
	-- not one of them: target it by its name, mark it, and give back the target there was
	if ( not TargetByName or (tries[id] or 0) >= 3 ) then return false; end
	tries[id] = (tries[id] or 0) + 1;
	local had = UnitExists("target") and UnitName("target") or nil;
	if ( not pcall(TargetByName, name, true) ) then return false; end
	local ok = false;
	if ( UnitExists("target") and UnitName("target") == name ) then ok = Mark("target", icon); end
	if ( had and had ~= name ) then
		if ( TargetLastTarget ) then pcall(TargetLastTarget); end
	elseif ( not had and ClearTarget ) then
		pcall(ClearTarget);
	end
	return ok;
end

local function BossMarks()
	if ( not DB().bossMarks or type(AZS_GetBosses) ~= "function" ) then return; end
	-- only while a run is being played: out of it the list can still hold the last run's bosses
	if ( type(AZS_GetPhase) == "function" and AZS_GetPhase() ~= "playing" ) then return; end
	local ok, bosses = pcall(AZS_GetBosses);
	if ( not ok or type(bosses) ~= "table" ) then return; end
	local alive = {};
	for i = 1, table.getn(bosses) do
		local b = bosses[i];
		if ( b.id ) then alive[b.id] = true; end
		if ( i <= table.getn(BOSS_ICONS) and b.id and b.name and marked[b.id] ~= BOSS_ICONS[i] ) then
			if ( MarkBoss(b.name, BOSS_ICONS[i], b.id) ) then marked[b.id] = BOSS_ICONS[i]; end
		end
	end
	for id in pairs(marked) do if ( not alive[id] ) then marked[id] = nil; end end
	for id in pairs(tries) do if ( not alive[id] ) then tries[id] = nil; end end
end

-- a friend down in co-op: the Square over their head, on and off 5 times
local function DeathMarks(now)
	if ( not DB().deathMarks ) then return; end
	for i = 1, 4 do
		local u = "party" .. i;
		local down = UnitExists(u) and (UnitIsDeadOrGhost and UnitIsDeadOrGhost(u) or UnitHealth(u) == 0) and true or false;
		if ( down and not fallen[u] ) then blinks[u] = { left = 10, at = now, on = false }; end
		fallen[u] = down;
	end
	for u, b in pairs(blinks) do
		if ( now >= b.at ) then
			b.on = not b.on;
			if ( UnitExists(u) ) then pcall(SetRaidTarget, u, b.on and SQUARE or 0); end
			b.left = b.left - 1;
			b.at = now + 0.4;
			if ( b.left <= 0 ) then
				if ( UnitExists(u) ) then pcall(SetRaidTarget, u, 0); end
				blinks[u] = nil;
			end
		end
	end
end

local markFrame = CreateFrame("Frame");
local nextBoss = 0;
markFrame:SetScript("OnUpdate", function()
	local now = GetTime();
	pcall(DeathMarks, now);
	if ( now >= nextBoss ) then
		nextBoss = now + 0.75;
		pcall(BossMarks);
	end
end);


-- ---------------------------------------------------------------- moving the UI, and the target frame
-- Unlocked, each of these frames gets a handle over it (its name on a green panel): drag the handle and the frame
-- follows. Locked, the handles go and the frames stay where they were put; the places are put back at every login.
local MOVABLE = { { "PlayerFrame", "Player" }, { "TargetFrame", "Target" }, { "PartyMemberFrame1", "Party" },
                  { "MinimapCluster", "Minimap" }, { "BuffFrame", "Buffs" }, { "CastingBarFrame", "Cast bar" },
                  { "MainMenuBar", "Action bars" }, { "MultiBarBottomLeft", "Bar 2" }, { "MultiBarRight", "Right bar" },
                  { "AZSMinorModsChat", "Say & Party" } };
local handles = {};

local function Place(name)
	local f = getglobal(name);
	local p = DB().places[name];
	if ( not f or type(p) ~= "table" or not p.x or not p.y ) then return; end
	f:ClearAllPoints();
	f:SetPoint("BOTTOMLEFT", UIParent, "BOTTOMLEFT", K.Num(p.x), K.Num(p.y));
	if ( name == "AZSMinorModsChat" ) then local d = DB(); d.x, d.y = p.x, p.y; end
end

local function PlaceAll()
	for i = 1, table.getn(MOVABLE) do pcall(Place, MOVABLE[i][1]); end
end

local function Handle(name, label)
	local f = getglobal(name);
	if ( not f ) then return nil; end
	local h = handles[name];
	if ( not h ) then
		h = CreateFrame("Frame", nil, UIParent);
		h:SetFrameStrata("DIALOG");
		h:SetBackdrop({ bgFile = "Interface\\Tooltips\\UI-Tooltip-Background", edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border",
		                tile = true, tileSize = 16, edgeSize = 12, insets = { left = 3, right = 3, top = 3, bottom = 3 } });
		h:SetBackdropColor(0.1, 0.6, 0.1, 0.45);
		h:SetBackdropBorderColor(0.3, 1, 0.3, 0.9);
		h:EnableMouse(true);
		h.text = h:CreateFontString(nil, "OVERLAY", "GameFontNormal");
		h.text:SetPoint("CENTER", h, "CENTER", 0, 0);
		h.text:SetText(label);
		h:SetScript("OnMouseDown", function()
			local t = getglobal(name);
			if ( not t ) then return; end
			t:SetMovable(true);
			if ( t.SetClampedToScreen ) then t:SetClampedToScreen(true); end
			t:StartMoving();
			this.moving = true;
		end);
		h:SetScript("OnMouseUp", function()
			local t = getglobal(name);
			if ( not t or not this.moving ) then return; end
			this.moving = nil;
			t:StopMovingOrSizing();
			DB().places[name] = { x = t:GetLeft(), y = t:GetBottom() };
			Place(name);
		end);
		h:SetScript("OnUpdate", function()
			local t = getglobal(name);
			if ( not t ) then return; end
			this:ClearAllPoints();
			this:SetPoint("TOPLEFT", t, "TOPLEFT", 0, 0);
			this:SetPoint("BOTTOMRIGHT", t, "BOTTOMRIGHT", 0, 0);
		end);
		handles[name] = h;
	end
	local w, ht = f:GetWidth(), f:GetHeight();
	if ( not w or w < 20 ) then f:SetWidth(math.max(w or 0, 120)); end
	if ( not ht or ht < 16 ) then f:SetHeight(math.max(ht or 0, 24)); end
	return h;
end

-- unlocked: the handles show (the frames are shown too, so even an empty party frame can be placed)
function AZSMinorMods_SetUnlocked(on)
	DB().unlocked = on and true or false;
	for i = 1, table.getn(MOVABLE) do
		local name = MOVABLE[i][1];
		if ( on ) then
			local ok, h = pcall(Handle, name, MOVABLE[i][2]);
			if ( ok and h ) then h:Show(); end
		elseif ( handles[name] ) then
			handles[name]:Hide();
		end
	end
	K.Print(NAME, on and "the UI is unlocked: drag the green handles, then lock it in /azsmods." or "the UI is locked.");
end

function AZSMinorMods_ResetPlaces()
	DB().places = {};
	local d = DB();
	d.x, d.y = nil, nil;
	K.Print(NAME, "the frames go back to their own places after a /reload.");
	if ( ReloadUI ) then ReloadUI(); end
end

-- the target frame: hidden for good while the setting is on (this game targets for you)
local targetShow;
local function TargetFrameHidden(on)
	if ( not TargetFrame ) then return; end
	if ( not targetShow ) then targetShow = TargetFrame.Show; end
	if ( on ) then
		TargetFrame.Show = function() end;
		TargetFrame:Hide();
	else
		TargetFrame.Show = targetShow;
		if ( UnitExists("target") ) then TargetFrame:Show(); end
	end
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
	win = K.Window("AZSMinorModsFrame", NAME, 420, 500);
	local hero = K.Hero(win, 380);
	hero:SetPoint("TOPLEFT", win, "TOPLEFT", 20, -30);
	K.Icon(hero.icon, "INV_Misc_Gear_02");
	hero.heading:SetText("Minor Mods");
	hero.line:SetText("Every change on its own: tick what you want.");
	local y = -104;
	local function Row(label, get, set)
		Check(win, label, y, get, set);
		y = y - 28;
	end
	Row("Take the level-up text out of the chat", function() return DB().hideLevel; end, function(v) DB().hideLevel = v; end);
	Row("Remove bloat from chat", function() return DB().bloat; end, function(v)
		DB().bloat = v;
		if ( v and not DB().box ) then DB().box = true; BoxBuild(); BoxPlace(); end   -- Say & Party go to the box
	end);
	Row("Say & Party box above the chat", function() return DB().box; end,
	    function(v) DB().box = v; BoxBuild(); BoxPlace(); end);
	Row("Camera as far out as it goes (50)", function() return DB().camera; end,
	    function(v) DB().camera = v; Camera(v); end);
	Row("Mark bosses: Skull, Cross, Moon", function() return DB().bossMarks; end, function(v) DB().bossMarks = v; end);
	Row("Blink a Square over a friend who falls", function() return DB().deathMarks; end, function(v) DB().deathMarks = v; end);
	Row("Hide the target frame", function() return DB().hideTarget; end, function(v) DB().hideTarget = v; TargetFrameHidden(v); end);
	Row("Unlock the UI to move it (untick to lock)", function() return DB().unlocked; end, function(v) AZSMinorMods_SetUnlocked(v); end);
	Row("Minimap button", function() local m = DB().minimap; return not (m and m.hide); end,
	    function(v) if ( AZSMinorMods_Mini ) then local m = DB().minimap; if ( (m and m.hide and true or false) == v ) then AZSMinorMods_Mini.Toggle(); end end end);
	local note = win:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	note:SetPoint("TOPLEFT", win, "TOPLEFT", 30, y - 4);
	note:SetWidth(360);
	note:SetJustifyH("LEFT");
	note:SetText("Remove bloat: no \"you have learned\" lines, and Say and Party only in the box. Unlocked, drag the green handles; lock it again when done.");
	local clear = K.Button(win, "Clear the box", 120, function() BoxBuild(); box.msg:Clear(); end);
	clear:SetPoint("BOTTOMLEFT", win, "BOTTOMLEFT", 24, 20);
	local reset = K.Button(win, "Reset positions", 150, function() AZSMinorMods_ResetPlaces(); end);
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
	elseif ( cmd == "minimap" ) then
		local shown = AZSMinorMods_Mini and AZSMinorMods_Mini.Toggle();
		K.Print(NAME, shown and "the minimap button is back." or "the minimap button is hidden. /azsmods minimap brings it back.");
	elseif ( cmd == "unlock" or cmd == "move" ) then
		AZSMinorMods_SetUnlocked(true);
	elseif ( cmd == "lock" ) then
		AZSMinorMods_SetUnlocked(false);
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
		DB().unlocked = false;                             -- every session starts locked
	end
	-- the frames back where they were put (the game places some of them again as it loads), and the target frame
	pcall(PlaceAll);
	pcall(TargetFrameHidden, DB().hideTarget);
end);
pcall(HookChat);                                          -- right away too, so lines printed while loading are caught

-- the minimap button: a gear, at the left of the minimap (AZS Stats' is at the bottom, AZS Handbook's at the top
-- left)
local okMini, errMini = pcall(function()
	AZSMinorMods_Mini = K.Minimap({ name = "AZSMinorModsMinimapButton", icon = "INV_Misc_Gear_02", angle = 190, db = DB,
		click = function() AZSMinorMods_Toggle(); end,
		tooltip = function(tt)
			tt:AddLine(NAME);
			tt:AddLine("Small changes, each on or off.", 1, 1, 1);
			tt:AddLine(" ");
			tt:AddLine("Click: the settings. /azsmods minimap hides this button.", 0.6, 0.6, 0.6);
		end });
end);
if ( not okMini ) then K.Print(NAME, "|cffff4040the minimap button could not be made:|r " .. tostring(errMini)); end
