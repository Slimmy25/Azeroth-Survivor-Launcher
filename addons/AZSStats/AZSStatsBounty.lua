-- AZS Stats' bounty board: when a new run starts, a wanted poster shows for a few seconds with the creature that
-- killed you last game (its face, how it did it and how many times it has killed you). When you take down your Nemesis
-- (the creature that has killed you most), a celebration shows instead, naming the friends who were with you; each such
-- win is written down in AZSStatsDB.nemesisSlain (the Nemesis tab lists them). Drawn with the game's own art: the quest
-- parchment, its frames and icons. /azstats bounty turns it off and on.
-- Lua 5.0 / 1.12 interface: scripts read `this`, `event`, `arg1`; no # operator; only the 1.12 API.

local function Num(v) return tonumber(v) or 0; end
local function Big(v)
	local s = tostring(math.floor(Num(v) + 0.5));
	while true do local n; s, n = string.gsub(s, "^(%d+)(%d%d%d)", "%1,%2"); if ( n == 0 ) then break; end end
	return s;
end
local function DB() return type(AZSStatsDB) == "table" and AZSStatsDB or nil; end
local function Key(name) return string.gsub(string.lower(tostring(name or "")), "[^%a%d]", ""); end

-- the creature that has killed you most (all classes), or nil
local function Nemesis()
	local db = DB();
	if ( not db or type(db.nemesis) ~= "table" ) then return nil, 0; end
	local best, kills = nil, 0;
	for name, n in pairs(db.nemesis) do
		if ( type(n) == "table" and Num(n.kills) > kills ) then best, kills = name, Num(n.kills); end
	end
	return best, kills;
end

-- the poster
local poster;
local function Poster()
	if ( poster ) then return poster; end
	local f = CreateFrame("Frame", "AZSStatsBountyFrame", UIParent);
	f:SetWidth(300);
	f:SetHeight(410);
	f:SetPoint("CENTER", UIParent, "CENTER", 0, 90);
	f:SetFrameStrata("DIALOG");
	f:SetBackdrop({ bgFile = "Interface\\QuestFrame\\QuestBG", edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
	                tile = false, edgeSize = 32, insets = { left = 11, right = 12, top = 12, bottom = 11 } });
	f.title = f:CreateFontString(nil, "OVERLAY", "QuestTitleFont");
	f.title:SetPoint("TOP", f, "TOP", 0, -28);
	f.title:SetTextColor(0.45, 0.05, 0.02);
	-- the culprit's face: the picture AZS Stats keeps in its Faces folder, else a skull, in a gold frame
	f.face = f:CreateTexture(nil, "ARTWORK");
	f.face:SetWidth(150);
	f.face:SetHeight(150);
	f.face:SetPoint("TOP", f, "TOP", 0, -68);
	f.frame = f:CreateTexture(nil, "OVERLAY");
	f.frame:SetTexture("Interface\\Buttons\\UI-Quickslot2");
	f.frame:SetWidth(258);
	f.frame:SetHeight(258);
	f.frame:SetPoint("CENTER", f.face, "CENTER", 0, 0);
	f.name = f:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge");
	f.name:SetPoint("TOP", f.face, "BOTTOM", 0, -14);
	f.name:SetTextColor(0.25, 0.1, 0.02);
	f.line = f:CreateFontString(nil, "OVERLAY", "QuestFont");
	f.line:SetPoint("TOP", f.name, "BOTTOM", 0, -10);
	f.line:SetWidth(250);
	f.line:SetTextColor(0.18, 0.12, 0.05);
	f:Hide();
	f.left = 0;
	f:SetScript("OnUpdate", function()
		this.left = this.left - (arg1 or 0);
		if ( this.left <= 0 ) then this:Hide(); return; end
		if ( this.left < 1 ) then this:SetAlpha(this.left); else this:SetAlpha(1); end
	end);
	f:EnableMouse(true);
	f:SetScript("OnMouseUp", function() this:Hide(); end);   -- a click puts it away
	poster = f;
	return f;
end

local function Face(tex, name, fallback)
	local k = Key(name);
	if ( k ~= "" and AZSSTATS_FACES and AZSSTATS_FACES[k] ) then tex:SetTexture("Interface\\AddOns\\AZSStats\\Faces\\" .. k);
	else tex:SetTexture(fallback); end
end

local function Show(title, name, line, icon, sound, seconds)
	local f = Poster();
	f.title:SetText(title);
	f.name:SetText(name);
	f.line:SetText(line);
	Face(f.face, name, icon);
	f.left = seconds or 8;
	f:SetAlpha(1);
	f:Show();
	if ( PlaySound ) then pcall(PlaySound, sound); end
end

local function On() local db = DB(); return db and db.bounty ~= false; end

-- a new run: the wanted poster of what killed you last game
local function Wanted()
	local db = DB();
	if ( not On() or type(db.history) ~= "table" ) then return; end
	local last = db.history[table.getn(db.history)];
	if ( type(last) ~= "table" or not last.killer or last.killer == "" ) then return; end
	if ( db.bountyShown == last.date ) then return; end   -- each death's poster once
	db.bountyShown = last.date;
	local n = type(db.nemesis) == "table" and db.nemesis[last.killer];
	local times = n and Num(n.kills) or 1;
	local nem = Nemesis();
	Show("WANTED", last.killer,
	     "Killed you last game" .. ((last.zname and last.zname ~= "") and (" in " .. last.zname) or "") .. ".\n" ..
	     "It has killed you " .. Big(times) .. (times == 1 and " time." or " times.") ..
	     (nem == last.killer and "\n|cff8a1010Your Nemesis.|r" or "") .. "\nBounty: take it down.",
	     "Interface\\Icons\\INV_Misc_Bone_HumanSkull_01", "QUESTADDED", 8);
end

-- a creature slain: was it your Nemesis?
local slainAt = {};
local function Slain(msg)
	if ( not On() or type(msg) ~= "string" ) then return; end
	local nem, kills = Nemesis();
	if ( not nem or kills < 2 ) then return; end            -- a Nemesis has killed you at least twice
	if ( not string.find(msg, nem, 1, true) ) then return; end
	local now = GetTime();
	if ( slainAt[nem] and now - slainAt[nem] < 60 ) then return; end   -- once a minute at most
	slainAt[nem] = now;
	-- who was with you: your party (co-op), written down with the win
	local with = {};
	if ( GetNumPartyMembers ) then
		local ok, n = pcall(GetNumPartyMembers);
		for i = 1, (ok and tonumber(n) or 0) do
			local who = UnitName("party" .. i);
			if ( who and who ~= "" and who ~= "Unknown" ) then table.insert(with, who); end
		end
	end
	local db = DB();
	if ( type(db.nemesisSlain) ~= "table" ) then db.nemesisSlain = {}; end
	local zname = type(AZS_ZONE) == "table" and tostring(AZS_ZONE.zname or "") or "";
	table.insert(db.nemesisSlain, { name = nem, date = date and date("%Y-%m-%d %H:%M") or "", zname = zname,
	                                t = AZS_GetRunTime and Num(AZS_GetRunTime()) or 0, cname = UnitClass and UnitClass("player") or "",
	                                with = table.concat(with, ", ") });
	while ( table.getn(db.nemesisSlain) > 100 ) do table.remove(db.nemesisSlain, 1); end
	local side = table.getn(with) > 0 and ("At your side: " .. table.concat(with, ", ") .. ".") or "You did it alone.";
	Show("NEMESIS SLAIN!", nem, "It had killed you " .. Big(kills) .. " times.\nNot this time. Well fought!\n" .. side,
	     "Interface\\Icons\\INV_Misc_Head_Dragon_01", "LEVELUP", 6);
end

-- a new run starts when the run clock goes back to its start (the first run of a session too)
local lastT = -1;
local watch = CreateFrame("Frame");
local acc = 0;
watch:RegisterEvent("CHAT_MSG_COMBAT_HOSTILE_DEATH");
watch:SetScript("OnEvent", function() pcall(Slain, arg1); end);
watch:SetScript("OnUpdate", function()
	acc = acc + (arg1 or 0);
	if ( acc < 1 ) then return; end
	acc = 0;
	if ( not AZS_GetRunTime ) then return; end
	local t = Num(AZS_GetRunTime());
	-- like AZS Stats itself: the clock back near zero (more than a co-op wobble) is a new run
	if ( t > 0 and t < 15 and (lastT < 0 or lastT - t > 10) ) then pcall(Wanted); end
	lastT = t;
end);

-- /azstats bounty: on and off
function AZSStats_Bounty(on)
	local db = DB();
	if ( not db ) then return; end
	if ( on == nil ) then on = db.bounty == false; end
	db.bounty = on and true or false;
	if ( AZSStats and AZSStats.Print ) then AZSStats.Print("Bounty board " .. (on and "on" or "off") .. "."); end
end
