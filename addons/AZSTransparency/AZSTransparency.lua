-- AZS Transparency (a test): which of your spells each item upgrade makes stronger. Every upgrade a ring, trinket,
-- amulet or piece of boss gear can give (the game's own list of them) is matched with your class's spells by the
-- game's own rules, as its character pane words them: Damage is "every spell you cast", Area is "your spells'
-- areas", Targets is "every spell that picks its targets", and so on. What kind of spell each one is comes from the
-- launcher's guide (AZSTransparencyData.lua, written by the launcher). The spells you have this run are lit.
-- /transparency (or /azst), or the button by the minimap, opens it. The look is AZS Stats' (AZSKit.lua).
-- Lua 5.0 / 1.12 interface: scripts read `this`, `arg1`; no # operator; only the 1.12 API.

local K = AZSKIT;
local NAME = "AZS Transparency";
local W, H = 640, 540;
local CW = W - 64;

local CLASS_COORDS = { WARRIOR = { 0, 0.25, 0, 0.25 }, MAGE = { 0.25, 0.49609375, 0, 0.25 }, ROGUE = { 0.49609375, 0.7421875, 0, 0.25 },
                       DRUID = { 0.7421875, 0.98828125, 0, 0.25 }, HUNTER = { 0, 0.25, 0.25, 0.5 }, SHAMAN = { 0.25, 0.49609375, 0.25, 0.5 },
                       PRIEST = { 0.49609375, 0.7421875, 0.25, 0.5 }, WARLOCK = { 0.7421875, 0.98828125, 0.25, 0.5 },
                       PALADIN = { 0, 0.25, 0.5, 0.75 } };
local CLASSES = { "WARRIOR", "PALADIN", "HUNTER", "ROGUE", "PRIEST", "SHAMAN", "MAGE", "WARLOCK", "DRUID" };

local function DB()
	if ( type(AZSTransparencyDB) ~= "table" ) then AZSTransparencyDB = {}; end
	return AZSTransparencyDB;
end

local function D() return AZSTRANSPARENCY_DATA or {}; end

-- the upgrades, as the game words them, with an item that gives each one (its icon), the spells they make stronger
-- (by kind: tags in the data) and, for some, the spells that make them work
local UPGRADES = {
	{ "Damage", "+% damage", "INV_Jewelry_Ring_15", "Band of Accuria, boss gear, amulets",
	  "Every spell you cast deals this much more damage.", { "damage" } },
	{ "Haste", "-% cooldowns", "INV_Jewelry_Ring_07", "Quick Strike Ring, boss gear, amulets",
	  "Your spells come back this much sooner.", { "all" } },
	{ "Crit", "+% critical strike", "INV_Jewelry_Ring_41", "Master Dragonslayer's Ring, boss gear, amulets",
	  "The chance a spell strikes critically.", { "damage" } },
	{ "Crit Dmg", "+% critical strike damage", "INV_Misc_ArmorKit_09", "Blackhand's Breadth, boss gear, amulets",
	  "A critical strike deals this much of a spell's damage.", { "damage" } },
	{ "Echo", "% chance for a spell to cast twice", "INV_Jewelry_Talisman_01", "Hand of Justice, boss gear, amulets",
	  "The chance a spell strikes twice.", { "damage" } },
	{ "Area", "+% area of effect", "INV_Jewelry_Ring_38", "Ring of Spell Power, boss gear, amulets",
	  "Your spells' areas are this much larger.", { "area" } },
	{ "Targets", "+ targets to every spell", "INV_Jewelry_Necklace_17", "boss gear, amulets",
	  "Every spell that picks its targets picks this many more.", { "targets" } },
	{ "Faster ticks", "damage over time ticks faster", "INV_Jewelry_Ring_51", "Seal of the Damned",
	  "Your damage over time spells tick faster: more ticks, and more damage, in the same time.", { "dot" } },
	{ "Lifesteal", "% of all damage dealt heals you", "INV_Jewelry_Ring_49", "Blooddrinker's Band, boss gear, amulets",
	  "Heals you for part of all the damage your spells deal.", { "damage" } },
	{ "Buff power", "% stronger buffs", "INV_Misc_Horn_01", "Horn of the Warchief, boss gear, amulets",
	  "Your damage, haste, shield and thorns buffs are stronger.", { "buff", "shield", "thorns" } },
	{ "Weapon damage", "+% weapon damage", "INV_Sword_04", "boss gear, amulets",
	  "Your weapon's spells (strikes, shots and claws) deal more damage.", { "weapon" } },
	{ "Pet damage", "+% pet damage", "Ability_Hunter_BeastCall", "boss gear, amulets",
	  "Your pets, totems and demons deal more damage.", { "pet" } },
	{ "Thorns", "melee attackers take damage", "Spell_Nature_Thorns", "boss gear, amulets",
	  "Enemies that hit you in melee take part of the damage back.", { "thorns" } },
	{ "Crowd control", "+% damage to crowd-controlled enemies", "Spell_Frost_FrostNova", "boss gear, amulets",
	  "More damage to enemies you slowed, rooted, stunned or scared.", { "damage" }, "cc" },
	{ "Afflicted", "+% damage to afflicted enemies", "Spell_Shadow_AbominationExplosion", "boss gear, amulets",
	  "More damage to enemies suffering your damage over time.", { "damage" }, "dot" },
	{ "Shielded", "+% damage while shielded", "Spell_Holy_PowerWordShield", "boss gear, amulets",
	  "More damage while a shield holds.", { "damage" }, "shield" },
	{ "Elites & bosses", "+% damage to elites and bosses", "INV_Misc_Head_Dragon_Black", "boss gear, amulets",
	  "More damage to elites, guardians and bosses.", { "damage" } },
	{ "Execute", "+% damage to enemies below 30% health", "INV_Sword_48", "boss gear, amulets",
	  "More damage to enemies below 30% health.", { "damage" } },
	{ "Just you", "health, armor, regen, speed, pickup, experience, gold, luck, lives", "INV_Jewelry_Ring_14",
	  "Heavy Dark Iron Ring, Ring of the Devoured, Carrot on a Stick, Gnomish Universal Remote, Major Soulstone...",
	  "These make you tougher, faster or richer; they change none of your spells.", {} },
};

local KIND_WORDS = { damage = "deals damage", area = "an area spell", targets = "picks its targets", dot = "damage over time",
                     heal = "heals you", shield = "a shield", buff = "a buff", thorns = "hurts melee attackers", cc = "crowd control",
                     pet = "a pet, totem or demon", weapon = "a weapon spell" };

local function Key(s) return (string.gsub(string.lower(tostring(s or "")), "[^%a%d]", "")); end

-- the spells you have this run (the game's spell line for addons), by Key(name)
local function Learned()
	local have = {};
	if ( type(AZS_SPELLS) == "table" ) then
		for i = 1, table.getn(AZS_SPELLS) do
			local s = AZS_SPELLS[i];
			if ( s.key ) then have[Key(s.key)] = tonumber(s.rank) or 1; end
			if ( type(s.spell) == "string" ) then have[Key(s.spell)] = tonumber(s.rank) or 1; end
		end
	end
	return have;
end

local function Affects(up, spell)
	local kinds = up[6];
	for i = 1, table.getn(kinds) do
		if ( kinds[i] == "all" or spell.tags[kinds[i]] ) then return kinds[i]; end
	end
	return nil;
end

-- ---------------------------------------------------------------- the window
local frame, hero, scroll, child, prevBtn, nextBtn;
local rows = {};
local shownClass;

local function SpellTip(owner, spell, why, have)
	GameTooltip:SetOwner(owner, "ANCHOR_RIGHT");
	GameTooltip:AddLine(spell.name, 1, 0.82, 0);
	GameTooltip:AddLine(spell.text, 1, 1, 1, 1);
	local kinds = {};
	for k in pairs(spell.tags) do if ( KIND_WORDS[k] ) then table.insert(kinds, KIND_WORDS[k]); end end
	table.sort(kinds);
	GameTooltip:AddLine(" ");
	GameTooltip:AddLine("This spell: " .. table.concat(kinds, ", "), 0.6, 0.6, 0.6, 1);
	if ( why ) then GameTooltip:AddLine(why, 0.37, 0.83, 0.37, 1); end
	if ( have ) then GameTooltip:AddLine("You have it this run (rank " .. have .. ").", 0.37, 0.83, 0.37);
	else GameTooltip:AddLine("Not learned this run.", 0.5, 0.5, 0.5); end
	GameTooltip:Show();
end

local function MakeRow()
	local f = CreateFrame("Frame", nil, child);
	K.Card(f);
	f.icon = f:CreateTexture(nil, "ARTWORK");
	f.icon:SetWidth(36);
	f.icon:SetHeight(36);
	f.icon:SetPoint("TOPLEFT", f, "TOPLEFT", 8, -8);
	f.name = f:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	f.name:SetPoint("TOPLEFT", f, "TOPLEFT", 52, -8);
	f.name:SetJustifyH("LEFT");
	f.what = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
	f.what:SetPoint("LEFT", f.name, "RIGHT", 8, 0);
	f.what:SetJustifyH("LEFT");
	f.why = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
	f.why:SetPoint("TOPLEFT", f.name, "BOTTOMLEFT", 0, -3);
	f.why:SetWidth(CW - 64);
	f.why:SetJustifyH("LEFT");
	f.from = f:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	f.from:SetPoint("TOPLEFT", f.why, "BOTTOMLEFT", 0, -2);
	f.from:SetWidth(CW - 64);
	f.from:SetJustifyH("LEFT");
	f.none = f:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	f.none:SetJustifyH("LEFT");
	f.chips = {};
	return f;
end

local function Chip(f, i)
	local c = f.chips[i];
	if ( c ) then return c; end
	c = CreateFrame("Frame", nil, f);
	c:SetWidth(26);
	c:SetHeight(26);
	c:EnableMouse(true);
	c.icon = c:CreateTexture(nil, "ARTWORK");
	c.icon:SetAllPoints(c);
	c.edge = c:CreateTexture(nil, "OVERLAY");
	c.edge:SetTexture("Interface\\Buttons\\UI-ActionButton-Border");
	c.edge:SetBlendMode("ADD");
	c.edge:SetWidth(46);
	c.edge:SetHeight(46);
	c.edge:SetPoint("CENTER", c, "CENTER", 0, 0);
	c:SetScript("OnEnter", function() SpellTip(this, this.spell, this.why, this.have); end);
	c:SetScript("OnLeave", function() GameTooltip:Hide(); end);
	f.chips[i] = c;
	return c;
end

local function Refresh()
	if ( not frame or not frame:IsShown() ) then return; end
	local _, token = UnitClass("player");
	shownClass = shownClass or token or "WARRIOR";
	local spells = D()[shownClass] or {};
	local have = Learned();
	local cname = string.sub(shownClass, 1, 1) .. string.lower(string.sub(shownClass, 2));
	hero.heading:SetText("Item upgrades: " .. cname);
	hero.line:SetText("Which of the " .. cname .. "'s spells each upgrade makes stronger. Lit: you have it this run. A test.");
	local c = CLASS_COORDS[shownClass];
	if ( c ) then
		hero.icon:SetTexture("Interface\\Glues\\CharacterCreate\\UI-CharacterCreate-Classes");
		hero.icon:SetTexCoord(c[1], c[2], c[3], c[4]);
		hero.icon:Show();
	else
		K.Icon(hero.icon, "INV_Misc_Spyglass_02");
	end
	for i = 1, table.getn(rows) do rows[i]:Hide(); end
	local y = 0;
	for u = 1, table.getn(UPGRADES) do
		local up = UPGRADES[u];
		local f = rows[u];
		if ( not f ) then f = MakeRow(); rows[u] = f; end
		f:ClearAllPoints();
		f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
		f:SetWidth(CW);
		K.Icon(f.icon, up[3]);
		f.name:SetText(up[1]);
		f.what:SetText("|cff9d9d9d(" .. up[2] .. ")|r");
		f.why:SetText(up[5]);
		f.from:SetText("From: " .. up[4]);
		local h = 8 + 16 + K.TextHeight(f.why, CW - 64) + 2 + K.TextHeight(f.from, CW - 64) + 6;
		for i = 1, table.getn(f.chips) do f.chips[i]:Hide(); end
		local n, perRow = 0, math.floor((CW - 60) / 30);
		for s = 1, table.getn(spells) do
			local spell = spells[s];
			local kind = Affects(up, spell);
			if ( kind ) then
				n = n + 1;
				local chip = Chip(f, n);
				chip:ClearAllPoints();
				chip:SetPoint("TOPLEFT", f, "TOPLEFT", 52 + math.mod(n - 1, perRow) * 30, -(h + math.floor((n - 1) / perRow) * 30));
				K.Icon(chip.icon, spell.icon);
				chip.spell = spell;
				chip.have = have[Key(spell.name)];
				chip.why = "Made stronger by " .. up[1] .. (kind ~= "all" and KIND_WORDS[kind] and (": it is " .. KIND_WORDS[kind] .. ".") or ".");
				if ( chip.have ) then
					chip.icon:SetVertexColor(1, 1, 1);
					chip.edge:Show();
				else
					chip.icon:SetVertexColor(0.4, 0.4, 0.4);
					chip.edge:Hide();
				end
				chip:Show();
			end
		end
		-- the spells that make it work (enemies slowed, damage over time, a shield)
		local need = "";
		if ( up[7] ) then
			local names = {};
			for s = 1, table.getn(spells) do if ( spells[s].tags[up[7]] ) then table.insert(names, spells[s].name); end end
			need = table.getn(names) > 0 and ("Works with: " .. table.concat(names, ", ")) or ("None of the " .. cname .. "'s spells sets it up.");
		end
		f.none:ClearAllPoints();
		if ( n == 0 ) then
			f.none:SetPoint("TOPLEFT", f, "TOPLEFT", 52, -h);
			f.none:SetText(table.getn(up[6]) == 0 and "Changes none of your spells." or ("None of the " .. cname .. "'s spells."));
			f.none:Show();
			h = h + 16;
		else
			h = h + math.ceil(n / perRow) * 30;
			if ( need ~= "" ) then
				f.none:SetPoint("TOPLEFT", f, "TOPLEFT", 52, -h);
				f.none:SetText(need);
				f.none:SetWidth(CW - 64);
				f.none:Show();
				h = h + K.TextHeight(f.none, CW - 64) + 2;
			else
				f.none:Hide();
			end
		end
		f:SetHeight(h + 6);
		f:Show();
		y = y + h + 12;
	end
	child:SetHeight(math.max(1, y));
	scroll.Top();
end

local function Step(d)
	local at = 1;
	for i = 1, table.getn(CLASSES) do if ( CLASSES[i] == shownClass ) then at = i; end end
	at = math.mod(at - 1 + d + table.getn(CLASSES), table.getn(CLASSES)) + 1;
	shownClass = CLASSES[at];
	K.Sound("igMainMenuOptionCheckBoxOn");
	Refresh();
end

local function Build()
	frame = K.Window("AZSTransparencyFrame", NAME, W, H);
	frame:SetScript("OnShow", function() K.Sound("igCharacterInfoOpen"); Refresh(); end);
	frame:SetScript("OnHide", function() K.Sound("igCharacterInfoClose"); end);
	hero = K.Hero(frame, W - 40);
	hero:SetPoint("TOPLEFT", frame, "TOPLEFT", 20, -30);
	hero.heading:SetWidth(W - 40 - 90 - 10);
	scroll, child = K.Scroll("AZSTransparencyScroll", frame, CW);
	scroll:SetPoint("TOPLEFT", hero, "BOTTOMLEFT", 2, -8);
	scroll:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -40, 48);
	prevBtn = K.Button(frame, "< Class", 100, function() Step(-1); end);
	prevBtn:SetPoint("BOTTOMLEFT", frame, "BOTTOMLEFT", 20, 18);
	nextBtn = K.Button(frame, "Class >", 100, function() Step(1); end);
	nextBtn:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -20, 18);
	local mine = K.Button(frame, "My class", 100, function() shownClass = nil; Refresh(); end);
	mine:SetPoint("BOTTOM", frame, "BOTTOM", 0, 18);
	-- the lit spells follow the run
	local watch = CreateFrame("Frame", nil, frame);
	watch:RegisterEvent("CHAT_MSG_ADDON");
	watch:SetScript("OnEvent", function() if ( arg1 == "AZSSPELLS" and frame:IsShown() ) then pcall(Refresh); end end);
end

function AZSTransparency_Toggle()
	if ( not frame ) then
		local ok, err = pcall(Build);
		if ( not ok ) then K.Print(NAME, "|cffff4040the window could not be made:|r " .. tostring(err)); frame = nil; return; end
	end
	if ( frame:IsShown() ) then frame:Hide(); else frame:Show(); end
end

SLASH_AZSTRANSPARENCY1 = "/transparency";
SLASH_AZSTRANSPARENCY2 = "/azst";
SlashCmdList["AZSTRANSPARENCY"] = function(msg)
	msg = string.lower(msg or "");
	if ( msg == "minimap" ) then
		local shown = AZSTransparency_Mini and AZSTransparency_Mini.Toggle();
		K.Print(NAME, shown and "the minimap button is back." or "the minimap button is hidden. /transparency minimap brings it back.");
		return;
	end
	for i = 1, table.getn(CLASSES) do
		if ( string.lower(CLASSES[i]) == msg ) then shownClass = CLASSES[i]; end
	end
	if ( frame and frame:IsShown() and msg ~= "" ) then Refresh(); return; end
	AZSTransparency_Toggle();
end

-- the minimap button: a spyglass at the right of the minimap (AZS Stats' is at the bottom, AZS Handbook's at the
-- top left)
local ok, err = pcall(function()
	AZSTransparency_Mini = K.Minimap({ name = "AZSTransparencyMinimapButton", icon = "INV_Misc_Spyglass_02", angle = 20, db = DB,
		click = function() AZSTransparency_Toggle(); end,
		tooltip = function(tt)
			tt:AddLine(NAME);
			tt:AddLine("Which of your spells each item upgrade makes stronger.", 1, 1, 1, 1);
			tt:AddLine(" ");
			tt:AddLine("Click: open it. /transparency <class> shows another class.", 0.6, 0.6, 0.6);
		end });
end);
if ( not ok ) then K.Print(NAME, "|cffff4040the minimap button could not be made:|r " .. tostring(err)); end
