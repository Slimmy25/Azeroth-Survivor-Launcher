-- AZS Handbook: the launcher's Guide in game. Every topic and page of the guide (AZSHandbookData.lua,
-- written by the launcher from its own guide, so the two always match), with the game's own icons: each spell of a
-- class with its icon and the talents that change it, every class's three talent trees as the game draws them,
-- and the spells and talents named in the text in gold. /handbook (or /azsh), or the book by the minimap, opens it.
-- The look is AZS Stats' (AZSKit.lua). When AZS Stats has seen a spell's tooltip in game, pointing at the spell
-- shows the game's own text. Like the launcher's Guide it also has the Talent Tree Builder (try a build: left-click
-- adds a point, right-click takes one away) and the Gear Planner (each class's gear and the jewelry, with what the
-- chosen pieces add up to), and each class page ends with the gear that class can wear.
-- Lua 5.0 / 1.12 interface: scripts read `this`, `arg1`; no # operator; only the 1.12 API.

local K = AZSKIT;
local W, H = 680, 560;
local CW = W - 64;                                      -- the list's width (inside the frame, beside the scroll bar)
local NAME = "AZS Handbook";

local view = { topic = nil, page = nil, talents = nil, prompt = nil, gear = nil };
-- the test build of the talent trees (forgotten when the trees are left) and the Gear Planner's pieces (kept until
-- the game is closed), as in the launcher
local build = { class = nil, pts = {} };
local planner = { class = nil, slots = {}, pick = "Head", stars = {}, mode = "single" };   -- mode: "single" or "tier", as in the launcher
local CLASSES = { "Warrior", "Paladin", "Hunter", "Rogue", "Priest", "Shaman", "Mage", "Warlock", "Druid" };
local RARITY_RGB = { Common = { 1, 1, 1 }, Uncommon = { 0.12, 1, 0 }, Rare = { 0.23, 0.56, 0.94 }, Epic = { 0.7, 0.36, 0.96 },
                     PvP = { 0.7, 0.36, 0.96 }, Legendary = { 1, 0.5, 0 }, Ring = { 0.23, 0.56, 0.94 }, Trinket = { 0.23, 0.56, 0.94 },
                     Amulet = { 0.23, 0.56, 0.94 } };
local frame, hero, scroll, child, backBtn, homeBtn, prevBtn, nextBtn;

local function D() return AZSHANDBOOK_DATA or { topics = {}, talents = {}, icons = {}, names = {}, gear = {}, jewels = {}, spellchanges = {} }; end
local function DB()
	if ( type(AZSHandbookDB) ~= "table" ) then AZSHandbookDB = {}; end
	return AZSHandbookDB;
end

-- a name as the icon table knows it: no case, spaces or punctuation
local function Key(name)
	local k = string.lower(tostring(name or ""));
	k = string.gsub(k, "%s*%(.-%)%s*$", "");
	return (string.gsub(k, "[^%a%d]", ""));
end

local function IconOf(name) return D().icons[Key(name)]; end

local CLASS_SHEET = "Interface\\Glues\\CharacterCreate\\UI-CharacterCreate-Classes";
local CLASS_COORDS = { warrior = { 0, 0.25, 0, 0.25 }, mage = { 0.25, 0.49609375, 0, 0.25 }, rogue = { 0.49609375, 0.7421875, 0, 0.25 },
                       druid = { 0.7421875, 0.98828125, 0, 0.25 }, hunter = { 0, 0.25, 0.25, 0.5 }, shaman = { 0.25, 0.49609375, 0.25, 0.5 },
                       priest = { 0.49609375, 0.7421875, 0.25, 0.5 }, warlock = { 0.7421875, 0.98828125, 0.25, 0.5 },
                       paladin = { 0, 0.25, 0.5, 0.75 } };

-- a topic's icon, or a class's
local TOPIC_ICONS = { ["Getting Started"] = "INV_Misc_Book_09", ["Build Guides"] = "INV_Misc_Spyglass_02", ["Controls"] = "INV_Misc_Gear_01",
                      ["Your First Run"] = "Ability_Rogue_Sprint", ["Classes"] = "INV_Sword_04", ["Races"] = "INV_Misc_Head_Human_01",
                      ["Zones & Bosses"] = "INV_Misc_Map_01", ["Level-Ups & Cards"] = "Spell_Holy_SealOfWisdom",
                      ["Pickups, Caches & Guardians"] = "INV_Misc_Coin_01", ["Gear & the Armory"] = "INV_Chest_Plate06", ["Transmog"] = "INV_Shirt_GuildTabard_01",
                      ["Talents"] = "Ability_Marksmanship", ["Achievements & Rewards"] = "INV_Misc_Bag_10",
                      ["Heroic & Endless"] = "Spell_Shadow_DeathScream", ["Co-op"] = "Ability_Warrior_BattleShout",
                      ["Tips for New Players"] = "INV_Misc_Note_01" };

local function SetPicture(tex, title)
	local c = CLASS_COORDS[string.lower(tostring(title or ""))];
	if ( c ) then
		tex:SetTexture(CLASS_SHEET);
		tex:SetTexCoord(c[1], c[2], c[3], c[4]);
		tex:Show();
		return;
	end
	K.Icon(tex, TOPIC_ICONS[title] or IconOf(title) or "INV_Misc_Book_09");
end

-- the spells and talents in a text, in gold (the longest names first, each place only once)
local function Escape(s) return (string.gsub(s, "([%^%$%(%)%%%.%[%]%*%+%-%?])", "%%%1")); end
local escaped;
local function Highlight(text)
	text = tostring(text or "");
	local names = D().names or {};
	if ( not escaped ) then
		escaped = {};
		for i = 1, table.getn(names) do escaped[i] = Escape(names[i]); end
	end
	-- the bosses' names first, kept plain: "Smite" in "Mr. Smite" is not the spell
	local plain, kept = D().plain or {}, {};
	for i = 1, table.getn(plain) do
		if ( string.find(text, plain[i], 1, true) ) then
			table.insert(kept, plain[i]);
			text = string.gsub(text, Escape(plain[i]), "\003" .. table.getn(kept) .. "\004");
		end
	end
	local marks = {};
	for i = 1, table.getn(names) do
		if ( string.find(text, names[i], 1, true) ) then
			table.insert(marks, names[i]);
			text = string.gsub(text, escaped[i], "\001" .. table.getn(marks) .. "\002");
		end
	end
	text = string.gsub(text, "\001(%d+)\002", function(n) return "|cffffd100" .. marks[tonumber(n)] .. "|r"; end);
	text = string.gsub(text, "\003(%d+)\004", function(n) return kept[tonumber(n)]; end);
	return text;
end

-- "Name (starting spell): text" -> name, text (the first ": " followed by a small letter or a number:
-- "Shadow Word: Pain: shadow damage ..." is Shadow Word: Pain)
local function SplitBullet(line)
	local from = 1;
	while ( true ) do
		local s = string.find(line, ": ", from, true);
		if ( not s ) then return nil, line; end
		local c = string.sub(line, s + 2, s + 2);
		if ( string.find(c, "[%l%d]") ) then return string.sub(line, 1, s - 1), string.sub(line, s + 2); end
		from = s + 2;
	end
end

local function StripRank(name) return (string.gsub(name or "", "%s*%(.-%)%s*$", "")); end

-- (AZS Stats no longer writes down the game's tooltips, so there is no game text to show here)
local function GameTip(name) return nil; end

-- the talents of a class that change a spell
local function Changers(class, spell)
	local out = {};
	local trees = D().talents[class];
	if ( not trees ) then return out; end
	for i = 1, table.getn(trees) do
		local t = trees[i].t;
		for j = 1, table.getn(t) do
			local ch = t[j][7];
			for k = 1, table.getn(ch) do
				if ( ch[k] == spell ) then table.insert(out, { t[j], trees[i].spec }); break; end
			end
		end
	end
	return out;
end

-- ---------------------------------------------------------------- the test build (the launcher's rules: up to 51
-- points, no more than a talent's ranks, and a row opens once its tree has 5 points for every row above it)
local function TreePts(tree, belowRow)
	local n, pts = 0, build.pts[tree] or {};
	local trees = D().talents[build.class] or {};
	local t = trees[tree] and trees[tree].t or {};
	for i = 1, table.getn(t) do
		if ( not belowRow or t[i][3] < belowRow ) then n = n + (pts[i] or 0); end
	end
	return n;
end

local function TotalPts()
	local n = 0;
	for tr = 1, 3 do n = n + TreePts(tr); end
	return n;
end

local function RankOf(tree, i) return (build.pts[tree] or {})[i] or 0; end

local function CanAdd(tree, i, t)
	return TotalPts() < 51 and RankOf(tree, i) < t[5] and TreePts(tree, t[3]) >= t[3] * 5;
end

-- a point can come back out when every talent further down its tree still has its 5 points a row above it
local function CanTake(tree, i, t)
	if ( RankOf(tree, i) <= 0 ) then return false; end
	build.pts[tree][i] = build.pts[tree][i] - 1;
	local ok = true;
	local all = D().talents[build.class][tree].t;
	for j = 1, table.getn(all) do
		if ( RankOf(tree, j) > 0 and TreePts(tree, all[j][3]) < all[j][3] * 5 ) then ok = false; end
	end
	build.pts[tree][i] = build.pts[tree][i] + 1;
	return ok;
end

-- the slots of the Gear Planner for a class: its gear's slots (a hunter's Ranged, a rogue's or shaman's Off Hand),
-- then the jewelry
local function GearSlots(class)
	local slots, seen = {}, {};
	local g = D().gear[class] or {};
	for i = 1, table.getn(g) do
		if ( not seen[g[i][2]] ) then seen[g[i][2]] = true; table.insert(slots, g[i][2]); end
	end
	for _, s in ipairs({ "Neck", "Finger 1", "Finger 2", "Trinket 1", "Trinket 2" }) do table.insert(slots, s); end
	return slots;
end

local function Choices(class, slot)
	local out = {};
	if ( slot == "Neck" or string.find(slot, "Finger") or string.find(slot, "Trinket") ) then
		local kind = (slot == "Neck" and "Amulet") or (string.find(slot, "Finger") and "Ring") or "Trinket";
		local j = D().jewels or {};
		for i = 1, table.getn(j) do
			if ( j[i][1] == kind ) then table.insert(out, { name = j[i][2], rarity = kind, stats = j[i][4], card = j[i][5], known = true, health = 0, icon = j[i][3] }); end
		end
	else
		local g = D().gear[class] or {};
		for i = 1, table.getn(g) do
			if ( g[i][2] == slot ) then table.insert(out, { name = g[i][3], rarity = g[i][1], slot = slot, health = g[i][4], known = g[i][5], stats = g[i][6], card = g[i][7],
			                                                }); end
		end
	end
	return out;
end

-- the totals: the pieces' bonuses, and the 4-piece bonus of every set worn at 4 pieces of 1+ stars (two sets of four give
-- both). Stars decide the set bonuses reached; what a star adds to a piece is not known yet, so it is not
-- counted. worn[rarity] = { pieces, at 1+ stars, at 3+, at 5 }
local WEAPON_SLOTS = { ["Main Hand"] = true, ["Off Hand"] = true, ["Ranged"] = true };
local STARS_NOT_COUNTED = "Stars decide which set bonuses you reach. How much each star adds to a piece is not known yet, so it is not counted here.";
local function Totals(class)
	local t, health, unknown, worn = {}, 0, 0, {};
	for slot, p in pairs(planner.slots) do
		local st = planner.stars[slot] or 0;
		if ( p.slot and not WEAPON_SLOTS[p.slot] ) then
			local c = worn[p.rarity] or { 0, 0, 0, 0 };
			worn[p.rarity] = c;
			c[1] = c[1] + 1;
			if ( st >= 1 ) then c[2] = c[2] + 1; end
			if ( st >= 3 ) then c[3] = c[3] + 1; end
			if ( st >= 5 ) then c[4] = c[4] + 1; end
		end
		if ( p.known ) then
			health = health + (p.health or 0);
			for k, v in pairs(p.stats or {}) do t[k] = (t[k] or 0) + v; end
		else unknown = unknown + 1; end
	end
	local sets = (D().sets or {})[class or ""] or {};
	for r, c in pairs(worn) do
		if ( sets[r] and c[2] >= 4 ) then
			for k, v in pairs(sets[r][3] or {}) do t[k] = (t[k] or 0) + v; end
		end
	end
	return t, health, unknown, worn;
end

-- a piece's own icon: the real item's icon (the launcher writes them, itemicons), the game's own texture; else nil
-- and the piece shows "Image soon"
local function ItemIcon(name)
	local icons = D().itemicons;
	if ( type(icons) == "table" and name and icons[name] ) then return icons[name]; end
	return nil;
end

local function ItemTooltip(owner, p)
	GameTooltip:SetOwner(owner, "ANCHOR_RIGHT");
	local c = RARITY_RGB[p.rarity] or RARITY_RGB.Common;
	GameTooltip:AddLine(p.name, c[1], c[2], c[3]);
	local card = p.card or "";
	if ( not p.known ) then card = card .. "\n(its bonuses are not known yet)"; end
	local st = (p.slot and planner.slots[p.slot] == p) and (planner.stars[p.slot] or 0) or 0;
	if ( st > 0 ) then card = "Stars: " .. st .. "/5\n" .. card; end
	for line in string.gfind(card, "[^\n]+") do
		if ( string.find(line, "^Equip:") or string.find(line, "^%+") ) then GameTooltip:AddLine(line, 0.12, 1, 0, 1);
		else GameTooltip:AddLine(line, 0.9, 0.87, 0.75, 1); end
	end
	GameTooltip:Show();
end

-- the Gear Planner: the totals, the slots (a click picks one), and what the picked slot can hold
local STAT_ROWS = { { "Health", "health" }, { "Damage", "damage", "%" }, { "Haste", "cooldown", "%" }, { "Crit", "crit", "%" },
                    { "Crit Dmg", "critdmg", "%" }, { "Armor", "armor", "%" }, { "Area", "area", "%" }, { "Targets", "targets", "" },
                    { "Echo", "echo", "%" }, { "Speed", "speed", "%" }, { "Regen", "regen", "" }, { "Pickup", "pickup", "%" } };

local function Fmt(v)
	if ( math.abs(v - math.floor(v + 0.5)) < 0.05 ) then return tostring(math.floor(v + 0.5)); end
	return string.format("%.1f", v);
end

-- Tier Bonus, as in the launcher's Gear Planner: a set is a starting point, not a pile. The whole planner (armor,
-- weapons, jewelry and their stars) is emptied, then the set's pieces go in: 4 pieces at T1, or all 8 at T3.
local ARMOR_ORDER = { "Head", "Shoulder", "Chest", "Waist", "Legs", "Feet", "Wrist", "Hands" };
local function FillSet(class, rarity, pieces)
	planner.slots = {}; planner.stars = {};
	local n = 0;
	for i = 1, table.getn(ARMOR_ORDER) do
		if ( n >= pieces ) then break; end
		local s = ARMOR_ORDER[i];
		local ch = Choices(class, s);
		for j = 1, table.getn(ch) do
			if ( ch[j].rarity == rarity ) then
				planner.slots[s] = ch[j];
				planner.stars[s] = (pieces >= 8) and 3 or 1;
				n = n + 1;
				break;
			end
		end
	end
end

local function ItemsGear(out)
	local class = view.gear;
	local t, health, unknown, worn = Totals(class);
	local lines = {};
	for i = 1, table.getn(STAT_ROWS) do
		local r = STAT_ROWS[i];
		local v = (r[2] == "health") and health or (t[r[2]] or 0);
		local txt = "+" .. Fmt(v) .. (r[3] or "");
		if ( r[2] == "health" and (t["health%"] or 0) ~= 0 ) then txt = txt .. " +" .. Fmt(t["health%"]) .. "%"; end
		table.insert(lines, { r[1], txt });
	end
	table.insert(out, { kind = "buttons", items = { { text = "Clear", go = function() planner.slots = {}; planner.stars = {}; end },
	                                                { text = "Talent trees", go = function() local c = view.gear; view.gear = nil; view.talents = c; end } } });
	table.insert(out, { kind = "stats", lines = lines, note = (unknown > 0 and ("Pieces with bonuses not known yet: " .. unknown .. ". ") or "") ..
	                    "The bonuses of everything chosen, added together, with the set bonuses reached. Click a piece in Slots to add a star (up to 5, then back to 0). " ..
	                    STARS_NOT_COUNTED });
	-- the sets worn: their pieces, and their 4-piece and 8-piece bonuses
	local sets = (D().sets or {})[class] or {};
	local rar = D().rarities or {};
	local any = false;
	local reached = {};
	for i = 1, table.getn(rar) do
		local c, s = worn[rar[i]], sets[rar[i]];
		if ( c and s ) then
			if ( c[2] >= 4 ) then table.insert(reached, s[1] .. " (4)"); end
			if ( c[3] >= 8 ) then table.insert(reached, s[1] .. " (8)" .. (c[4] >= 8 and " empowered" or "")); end
		end
	end
	table.insert(out, { kind = "head", text = "Set bonuses" });
	any = true;
	table.insert(out, { kind = "note", text = (table.getn(reached) > 0) and ("Reached: " .. table.concat(reached, ", "))
	                    or "Wear armor pieces of a set to see its bonuses here. Four pieces of two sets give both 4-piece bonuses." });
	for i = 1, table.getn(rar) do
		local c, s = worn[rar[i]], sets[rar[i]];
		if ( c and s ) then
			if ( not any ) then table.insert(out, { kind = "head", text = "Set bonuses" }); any = true; end
			local on4, on8, emp = c[2] >= 4, c[3] >= 8, c[4] >= 8;
			table.insert(out, { kind = "para", text = s[1] .. " " .. c[1] .. "/8\n" ..
				"(4) " .. s[2] .. "  [" .. c[2] .. "/4 at 1+ stars]" .. (on4 and "  |cff40d040reached|r" or "") .. "\n" ..
				"(8) " .. s[4] .. "  [" .. c[3] .. "/8 at 3+ stars]" .. (on8 and (emp and "  |cff40d040empowered|r" or "  |cff40d040reached|r") or "") });
		end
	end
	-- Single Item / Tier Bonus, as in the launcher
	table.insert(out, { kind = "buttons", items = {
		{ text = (planner.mode == "single" and "> " or "") .. "Single Item", go = function() planner.mode = "single"; end },
		{ text = (planner.mode == "tier" and "> " or "") .. "Tier Bonus", go = function() planner.mode = "tier"; end } } });
	if ( planner.mode == "tier" ) then
		table.insert(out, { kind = "head", text = "Tier Bonus" });
		for i = 1, table.getn(rar) do
			local r = rar[i];
			if ( sets[r] ) then
				table.insert(out, { kind = "buttons", right = r, items = {
					{ text = "4 piece", go = function() FillSet(class, r, 4); end },
					{ text = "8 piece", go = function() FillSet(class, r, 8); end } } });
			end
		end
		table.insert(out, { kind = "note", text = "4 piece: four pieces at T1. 8 piece: all eight at T3. Everything else in the planner is emptied." });
		return out;
	end
	table.insert(out, { kind = "head", text = "Slots" });
	local slots = GearSlots(class);
	for i = 1, table.getn(slots) do
		local s, p = slots[i], planner.slots[slots[i]];
		local st = planner.stars[s] or 0;
		-- a click on a piece: one more star (after 5 back to 0); on an empty slot: pick it
		table.insert(out, { kind = "row", label = s, text = p and (p.name .. (p.slot and st > 0 and ("  T" .. st) or "")) or "Empty", rarity = p and p.rarity, item = p,
		                    selected = (planner.pick == s), go = function()
		                        if ( p and p.slot ) then planner.stars[s] = math.mod((planner.stars[s] or 0) + 1, 6); end
		                        planner.pick = s;
		                    end });
	end
	local held = planner.slots[planner.pick];
	table.insert(out, { kind = "head", text = "Choose: " .. planner.pick .. (held and (" - " .. held.name) or "") });
	table.insert(out, { kind = "row", label = "", text = "Empty", go = function() planner.slots[planner.pick] = nil; planner.stars[planner.pick] = nil; end });
	local ch = Choices(class, planner.pick);
	for i = 1, table.getn(ch) do
		local p = ch[i];
		local worn = planner.slots[planner.pick] and planner.slots[planner.pick].name == p.name;
		table.insert(out, { kind = "row", label = p.rarity, text = p.name .. (p.known and "" or " *"), rarity = p.rarity, item = p, selected = worn,
		                    go = function() planner.slots[planner.pick] = p; planner.stars[planner.pick] = nil; end });   -- a new piece: 0 stars
	end
	return out;
end

-- ---------------------------------------------------------------- what a view shows
-- items: { kind = "head" | "para" | "bullet" | "tip" | "note" | "cards" | "spell" | "button" | "trees", ... }
local function Items()
	local data = D();
	local out = {};
	local topics = data.topics;
	if ( view.prompt ) then
		table.insert(out, { kind = "note", text = "What class do you want to build for?" });
		local cards, tool = {}, view.prompt;
		for i = 1, table.getn(CLASSES) do
			local c = CLASSES[i];
			table.insert(cards, { title = c, desc = "", go = function()
				view.prompt = nil;
				if ( tool == "gear" ) then
					if ( planner.class ~= c ) then planner.slots = {}; planner.stars = {}; planner.pick = "Head"; end
					planner.class = c; view.gear = c;
				else view.talents = c; end
			end });
		end
		table.insert(out, { kind = "cards", items = cards });
		return out;
	end
	if ( view.gear ) then return ItemsGear(out); end
	if ( view.talents ) then
		if ( build.class ~= view.talents ) then build.class, build.pts = view.talents, {}; end
		table.insert(out, { kind = "note", text = "Point at a talent to read it. Left-click adds a point, right-click takes one away. " ..
		                                           "A new row opens for every 5 points spent in a tree; the purple ones are the milestones at 10, 20 and 30 points." });
		table.insert(out, { kind = "buttons", items = { { text = "Reset", go = function() build.pts = {}; end },
		                                                { text = "Gear Planner", go = function() local c = view.talents; view.talents = nil; build.class = nil;
		                                                    if ( planner.class ~= c ) then planner.slots = {}; planner.stars = {}; planner.pick = "Head"; end
		                                                    planner.class = c; view.gear = c; end } },
		                    right = "Points: " .. TotalPts() .. " / 51" });
		table.insert(out, { kind = "trees", class = view.talents });
		return out;
	end
	if ( not view.topic ) then
		local cards = {};
		for i = 1, table.getn(topics) do
			local index = i;
			table.insert(cards, { title = topics[i].title, desc = topics[i].desc, go = function() view.topic, view.page = index, nil; end });
		end
		table.insert(cards, { title = "Talent Tree Builder", desc = "Pick a class and try out a build of up to 51 points.", icon = "Ability_Marksmanship",
		                      go = function() view.prompt = "talents"; end });
		table.insert(cards, { title = "Gear Planner", desc = "Put on any gear and jewelry and see what it adds up to.", icon = "INV_Chest_Plate06",
		                      go = function() view.prompt = "gear"; end });
		table.insert(out, { kind = "cards", items = cards });
		return out;
	end
	local topic = topics[view.topic];
	if ( not view.page and table.getn(topic.pages) > 0 ) then
		if ( topic.desc ~= "" ) then table.insert(out, { kind = "note", text = topic.desc }); end
		local cards = {};
		for i = 1, table.getn(topic.pages) do
			local index = i;
			table.insert(cards, { title = topic.pages[i].title, desc = topic.pages[i].desc, go = function() view.page = index; end });
		end
		table.insert(out, { kind = "cards", items = cards });
		return out;
	end
	local page = view.page and topic.pages[view.page] or topic;
	local class = (topic.title == "Classes" and data.talents[page.title]) and page.title or nil;
	local section, para = "", {};
	if ( class ) then
		local c = class;
		table.insert(out, { kind = "buttons", items = { { text = "Talent trees", go = function() view.talents = c; end } }, alignRight = true });
	end
	local function Flush()
		if ( table.getn(para) > 0 ) then table.insert(out, { kind = "para", text = table.concat(para, " ") }); para = {}; end
	end
	for i = 1, table.getn(page.body) do
		local line = page.body[i];
		if ( line == "" ) then
			Flush();
		elseif ( string.sub(line, 1, 7) == "@video " ) then
			Flush();
			local v = page.videos[(tonumber(string.sub(line, 8)) or 0) + 1];
			if ( v ) then table.insert(out, { kind = "tip", head = "VIDEO", text = v .. "  - watch it in the launcher's Guide." }); end
		elseif ( string.sub(line, 1, 2) == "# " ) then
			Flush();
			section = string.sub(line, 3);
			if ( not (class and string.sub(section, 1, 6) == "Talent") ) then table.insert(out, { kind = "head", text = section }); end
		elseif ( class and string.sub(section, 1, 6) == "Talent" ) then
			-- the trees have their own view
		elseif ( string.sub(line, 1, 2) == "- " ) then
			Flush();
			local text = string.sub(line, 3);
			local name, rest = SplitBullet(text);
			if ( class and string.sub(section, 1, 10) == "Spell pool" and name ) then
				local spell = StripRank(name);
				local sc = (data.spellchanges or {})[class] or {};
				table.insert(out, { kind = "spell", name = name, spell = spell, text = string.upper(string.sub(rest, 1, 1)) .. string.sub(rest, 2),
				                    icon = IconOf(spell), changers = Changers(class, spell), spells = sc[Key(spell)] or {},
				                    sets = (((data.setchanges or {})[class] or {})[Key(spell)]) or {} });
			else
				-- the bullet's icon: its name's, else the first spell it names
				local icon = name and IconOf(name);
				if ( not icon ) then
					local names = data.names or {};
					for j = 1, table.getn(names) do
						if ( string.find(text, names[j], 1, true) ) then icon = IconOf(names[j]); if ( icon ) then break; end end
					end
				end
				table.insert(out, { kind = "bullet", text = text, icon = icon });
			end
		elseif ( string.sub(line, 1, 1) == "\"" ) then
			Flush();
			table.insert(out, { kind = "tip", head = "TIP", text = (string.gsub(line, "\"", "")) });
		else
			table.insert(para, line);
		end
	end
	Flush();
	if ( class and data.gear and data.gear[class] ) then          -- the gear this class can wear, by rarity
		table.insert(out, { kind = "head", text = "Gear" });
		table.insert(out, { kind = "note", text = "Every piece a " .. class .. " can wear. Point at one to read it, click it to try it in the Gear Planner." });
		local rar = data.rarities or {};
		for r = 1, table.getn(rar) do
			local list = {};
			local g = data.gear[class];
			for i = 1, table.getn(g) do
				if ( g[i][1] == rar[r] ) then
					table.insert(list, { name = g[i][3], rarity = g[i][1], slot = g[i][2], health = g[i][4], known = g[i][5], stats = g[i][6], card = g[i][7] });
				end
			end
			if ( table.getn(list) > 0 ) then
				table.insert(out, { kind = "chips", label = rar[r], items = list, class = class });
				local set = ((data.sets or {})[class] or {})[rar[r]];      -- the set's bonuses, under its pieces
				if ( set ) then
					table.insert(out, { kind = "note", text = set[1] .. " set: (4) " .. set[2] .. " [4 pieces at 1+ stars]  (8) " .. set[4] ..
					                    " [all 8 at 3+ stars; empowered at 5 stars: half again as strong]" });
				end
			end
		end
	end
	return out;
end

-- ---------------------------------------------------------------- drawing (frames are reused, kind by kind)
local pools, used = {}, {};

local function Get(kind, make)
	pools[kind] = pools[kind] or {};
	used[kind] = (used[kind] or 0) + 1;
	local f = pools[kind][used[kind]];
	if ( not f ) then
		f = make();
		pools[kind][used[kind]] = f;
	end
	f:ClearAllPoints();
	f:Show();
	return f;
end

local function Text(parent, font)
	local fs = parent:CreateFontString(nil, "OVERLAY", font);
	fs:SetJustifyH("LEFT");
	if ( fs.SetJustifyV ) then fs:SetJustifyV("TOP"); end
	return fs;
end

local function Go(fn)
	K.Sound("igMainMenuOptionCheckBoxOn");
	fn();
	if ( frame ) then frame.Refresh(); end
end

local function SpellTooltip(owner, it)
	GameTooltip:SetOwner(owner, "ANCHOR_RIGHT");
	GameTooltip:AddLine(it.spell, 1, 0.82, 0);
	GameTooltip:AddLine(GameTip(it.spell) or it.text, 1, 1, 1, 1);
	if ( table.getn(it.changers) > 0 ) then
		GameTooltip:AddLine(" ");
		GameTooltip:AddLine("Talents that change it:", 0.6, 0.6, 0.6);
		for i = 1, table.getn(it.changers) do
			GameTooltip:AddLine(it.changers[i][1][1] .. "  (" .. it.changers[i][2] .. ")", 0.37, 0.83, 0.37);
		end
	end
	GameTooltip:Show();
end

local function TalentTooltip(owner, t, spec)
	GameTooltip:SetOwner(owner, "ANCHOR_RIGHT");
	GameTooltip:AddLine(t[1], 1, 0.82, 0);
	GameTooltip:AddLine(spec .. "  -  " .. (t[5] > 1 and (t[5] .. " ranks") or "1 rank") ..
	                    (t[3] > 0 and ("  -  needs " .. (t[3] * 5) .. " points") or ""), 0.6, 0.6, 0.6);
	GameTooltip:AddLine(t[6], 1, 1, 1, 1);
	if ( table.getn(t[7]) > 0 ) then
		GameTooltip:AddLine(" ");
		GameTooltip:AddLine("Changes: " .. table.concat(t[7], ", "), 0.37, 0.83, 0.37, 1);
	end
	if ( string.find(t[6], "X", 1, true) ) then GameTooltip:AddLine("X grows with every rank.", 0.6, 0.6, 0.6); end
	GameTooltip:Show();
end

-- one tree: the spec's name over its talents' icons on the game's grid (7 rows, 4 columns)
local function MakeTree()
	local f = CreateFrame("Frame", nil, child);
	K.Card(f, 0.6, 0.5, 0.3);
	f.title = Text(f, "GameFontNormal");
	f.title:SetPoint("TOPLEFT", f, "TOPLEFT", 10, -8);
	f.cells = {};
	for i = 1, 28 do
		local c = CreateFrame("Frame", nil, f);
		c:SetWidth(34);
		c:SetHeight(34);
		c:EnableMouse(true);
		c.icon = c:CreateTexture(nil, "ARTWORK");
		c.icon:SetAllPoints(c);
		c.edge = c:CreateTexture(nil, "OVERLAY");
		c.edge:SetTexture("Interface\\Buttons\\UI-Quickslot2");
		c.edge:SetWidth(58);
		c.edge:SetHeight(58);
		c.edge:SetPoint("CENTER", c, "CENTER", 0, 0);
		c.rank = c:CreateFontString(nil, "OVERLAY", "NumberFontNormalSmall");
		c.rank:SetPoint("BOTTOMRIGHT", c, "BOTTOMRIGHT", 4, -4);
		c:SetScript("OnEnter", function() if ( this.t ) then TalentTooltip(this, this.t, this.spec); end end);
		c:SetScript("OnLeave", function() GameTooltip:Hide(); end);
		c:SetScript("OnMouseUp", function()
			if ( not this.t or not build.class ) then return; end
			if ( arg1 == "RightButton" ) then
				if ( CanTake(this.tree, this.i, this.t) ) then build.pts[this.tree][this.i] = build.pts[this.tree][this.i] - 1; Go(function() end); end
			elseif ( CanAdd(this.tree, this.i, this.t) ) then
				build.pts[this.tree] = build.pts[this.tree] or {};
				build.pts[this.tree][this.i] = (build.pts[this.tree][this.i] or 0) + 1;
				Go(function() end);
			end
		end);
		f.cells[i] = c;
	end
	return f;
end

local function DrawTree(f, tree, w, index)
	f:SetWidth(w);
	local cell = math.floor((w - 16) / 4);
	f:SetHeight(30 + 7 * cell + 8);
	f.title:SetText(tree.spec .. "  (" .. TreePts(index) .. ")");
	for i = 1, 28 do f.cells[i]:Hide(); f.cells[i].t = nil; end
	for i = 1, math.min(28, table.getn(tree.t)) do
		local t, c = tree.t[i], f.cells[i];
		c.t, c.spec, c.tree, c.i = t, tree.spec, index, i;
		c:ClearAllPoints();
		c:SetPoint("TOPLEFT", f, "TOPLEFT", 8 + t[4] * cell + math.floor((cell - 34) / 2), -(30 + t[3] * cell + math.floor((cell - 34) / 2)));
		K.Icon(c.icon, t[2]);
		if ( t[8] ) then c.edge:SetVertexColor(0.75, 0.5, 1); else c.edge:SetVertexColor(1, 1, 1); end
		local rank = RankOf(index, i);
		c.rank:SetText(rank .. "/" .. t[5]);
		if ( rank > 0 ) then c.rank:SetTextColor(0.12, 1, 0); c.icon:SetVertexColor(1, 1, 1);
		elseif ( TreePts(index, t[3]) >= t[3] * 5 ) then c.rank:SetTextColor(1, 0.82, 0); c.icon:SetVertexColor(1, 1, 1);
		else c.rank:SetTextColor(0.5, 0.5, 0.5); c.icon:SetVertexColor(0.35, 0.35, 0.35); end
		c:Show();
	end
	return f:GetHeight();
end

-- under a spell: the class's other spells whose text names it
local function LaySpellsBy(f, it, h)
	f.sby = f.sby or Text(f, "GameFontDisableSmall");
	f.schips = f.schips or {};
	for i = 1, table.getn(f.schips) do f.schips[i]:Hide(); end
	if ( table.getn(it.spells or {}) == 0 ) then f.sby:Hide(); return h; end
	f.sby:ClearAllPoints();
	f.sby:SetPoint("TOPLEFT", f, "TOPLEFT", 50, -(h - 2));
	f.sby:SetText("Spells that change it:");
	f.sby:Show();
	local x = 50 + f.sby:GetStringWidth() + 6;
	for i = 1, math.min(14, table.getn(it.spells)) do
		local c = f.schips[i];
		if ( not c ) then
			c = CreateFrame("Frame", nil, f);
			c:SetWidth(18);
			c:SetHeight(18);
			c:EnableMouse(true);
			c.icon = c:CreateTexture(nil, "ARTWORK");
			c.icon:SetAllPoints(c);
			c:SetScript("OnEnter", function()
				GameTooltip:SetOwner(this, "ANCHOR_RIGHT");
				GameTooltip:AddLine(this.name, 1, 0.82, 0);
				GameTooltip:AddLine("Its text names this spell.", 0.6, 0.6, 0.6);
				GameTooltip:Show();
			end);
			c:SetScript("OnLeave", function() GameTooltip:Hide(); end);
			f.schips[i] = c;
		end
		c.name = it.spells[i];
		c:ClearAllPoints();
		c:SetPoint("TOPLEFT", f, "TOPLEFT", x + (i - 1) * 21, -(h - 5));
		K.Icon(c.icon, IconOf(it.spells[i]) or "INV_Misc_QuestionMark");
		c:Show();
	end
	return h + 20;
end

-- the sets whose 8-piece bonus changes the spell: their names in their rarity's colour; the hover reads the bonus
local function LaySetsBy(f, it, h)
	f.setby = f.setby or Text(f, "GameFontDisableSmall");
	f.setchips = f.setchips or {};
	for i = 1, table.getn(f.setchips) do f.setchips[i]:Hide(); end
	if ( table.getn(it.sets or {}) == 0 ) then f.setby:Hide(); return h; end
	f.setby:ClearAllPoints();
	f.setby:SetPoint("TOPLEFT", f, "TOPLEFT", 50, -(h - 2));
	f.setby:SetText("Set bonuses that change it:");
	f.setby:Show();
	local x = 50 + f.setby:GetStringWidth() + 6;
	for i = 1, math.min(6, table.getn(it.sets)) do
		local c = f.setchips[i];
		if ( not c ) then
			c = CreateFrame("Frame", nil, f);
			c:SetHeight(16);
			c:EnableMouse(true);
			c.t = Text(c, "GameFontNormalSmall");
			c.t:SetPoint("LEFT", c, "LEFT", 2, 0);
			c:SetScript("OnEnter", function()
				GameTooltip:SetOwner(this, "ANCHOR_RIGHT");
				local rgb = RARITY_RGB[this.rarity] or RARITY_RGB.Common;
				GameTooltip:AddLine(this.label, rgb[1], rgb[2], rgb[3]);
				GameTooltip:AddLine(this.text, 0.12, 1, 0, 1);
				GameTooltip:AddLine("All 8 armor pieces at 3+ stars; at 5 stars on all eight it is empowered: half again as strong.", 0.6, 0.6, 0.6, 1);
				GameTooltip:Show();
			end);
			c:SetScript("OnLeave", function() GameTooltip:Hide(); end);
			f.setchips[i] = c;
		end
		local d = it.sets[i];
		c.label, c.text, c.rarity = d[1], d[2], d[3];
		local rgb = RARITY_RGB[d[3]] or RARITY_RGB.Common;
		c.t:SetText(d[1]);
		c.t:SetTextColor(rgb[1], rgb[2], rgb[3]);
		c:SetWidth(c.t:GetStringWidth() + 6);
		c:ClearAllPoints();
		c:SetPoint("TOPLEFT", f, "TOPLEFT", x, -(h - 3));
		x = x + c.t:GetStringWidth() + 12;
		c:Show();
	end
	return h + 20;
end

-- the Gear Planner's and the class pages' pieces: a row of buttons, the totals, a line to click, a rarity's pieces
local function LayMore(it, y)
	if ( it.kind == "buttons" ) then
		local f = Get("buttons", function()
			local f = CreateFrame("Frame", nil, child);
			f.b = {};
			for i = 1, 6 do
				f.b[i] = K.Button(f, "", 120, function() if ( this.go ) then Go(this.go); end end);
				f.b[i]:SetHeight(22);
			end
			f.right = Text(f, "GameFontNormal");
			f.right:SetPoint("RIGHT", f, "RIGHT", -4, 0);
			return f;
		end);
		f:SetWidth(CW);
		f:SetHeight(26);
		f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
		local w = it.width or 120;
		for i = 1, 6 do
			local b, d = f.b[i], it.items[i];
			b:ClearAllPoints();
			if ( d ) then
				b:SetText(d.text);
				b:SetWidth(w);
				b.go = d.go;
				if ( it.alignRight ) then b:SetPoint("TOPRIGHT", f, "TOPRIGHT", -(i - 1) * (w + 6), 0);
				else b:SetPoint("TOPLEFT", f, "TOPLEFT", (i - 1) * (w + 6), 0); end
				b:Show();
			else b:Hide(); end
		end
		f.right:SetText(it.right or "");
		return y + 32;
	elseif ( it.kind == "stats" ) then
		local f = Get("stats", function()
			local f = CreateFrame("Frame", nil, child);
			K.Card(f, 0.6, 0.5, 0.3);
			f.l, f.v = {}, {};
			for i = 1, 12 do
				f.l[i] = Text(f, "GameFontNormalSmall");
				f.v[i] = Text(f, "GameFontHighlightSmall");
				f.v[i]:SetJustifyH("RIGHT");
			end
			f.note = Text(f, "GameFontDisableSmall");
			return f;
		end);
		local half = math.floor((CW - 30) / 2);
		for i = 1, 12 do
			local col, row = math.floor((i - 1) / 6), math.mod(i - 1, 6);
			local l = it.lines[i];
			f.l[i]:ClearAllPoints();
			f.l[i]:SetPoint("TOPLEFT", f, "TOPLEFT", 12 + col * (half + 6), -(10 + row * 15));
			f.v[i]:ClearAllPoints();
			f.v[i]:SetPoint("TOPRIGHT", f, "TOPLEFT", 12 + col * (half + 6) + half - 8, -(10 + row * 15));
			f.v[i]:SetWidth(80);
			f.l[i]:SetText(l and l[1] or "");
			f.v[i]:SetText(l and l[2] or "");
		end
		f.note:ClearAllPoints();
		f.note:SetPoint("TOPLEFT", f, "TOPLEFT", 12, -(10 + 6 * 15 + 4));
		f.note:SetWidth(CW - 24);
		f.note:SetText(it.note or "");
		f:SetWidth(CW);
		f:SetHeight(10 + 6 * 15 + 4 + 16 + 8);
		f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
		return y + f:GetHeight() + 8;
	elseif ( it.kind == "row" ) then
		local f = Get("row", function()
			local f = CreateFrame("Frame", nil, child);
			f:EnableMouse(true);
			f.bg = f:CreateTexture(nil, "BACKGROUND");
			f.bg:SetAllPoints(f);
			f.label = Text(f, "GameFontDisableSmall");
			f.label:SetPoint("LEFT", f, "LEFT", 6, 0);
			f.label:SetWidth(90);
			f.icon = f:CreateTexture(nil, "ARTWORK");
			f.icon:SetWidth(16);
			f.icon:SetHeight(16);
			f.icon:SetPoint("LEFT", f, "LEFT", 98, 0);
			f.text = Text(f, "GameFontHighlight");
			f.text:SetPoint("LEFT", f, "LEFT", 118, 0);
			f:SetScript("OnEnter", function()
				this.bg:SetTexture(0.95, 0.76, 0.3, 0.15);
				if ( this.item ) then ItemTooltip(this, this.item); end
			end);
			f:SetScript("OnLeave", function() this.bg:SetTexture(0.95, 0.76, 0.3, this.sel and 0.1 or 0); GameTooltip:Hide(); end);
			f:SetScript("OnMouseUp", function() if ( this.go ) then Go(this.go); end end);
			return f;
		end);
		f.go, f.item, f.sel = it.go, it.item, it.selected;
		f.bg:SetTexture(0.95, 0.76, 0.3, it.selected and 0.1 or 0);
		f.label:SetText(it.label or "");
		f.text:SetText(it.text or "");
		local ic = it.item and (ItemIcon(it.item.name) or it.item.icon);   -- the game's own icon, when known
		if ( ic ) then
			if ( not string.find(ic, "\\", 1, true) ) then ic = "Interface\\Icons\\" .. ic; end
			f.icon:SetTexture(ic);
			f.icon:Show();
		else f.icon:Hide(); end
		local c = it.rarity and RARITY_RGB[it.rarity] or { 0.9, 0.87, 0.75 };
		f.text:SetTextColor(c[1], c[2], c[3]);
		f:SetWidth(CW);
		f:SetHeight(18);
		f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
		return y + 19;
	end
	-- "chips": a rarity's pieces as small boxes, the slot's short name inside, in the rarity's colour
	local f = Get("chips", function()
		local f = CreateFrame("Frame", nil, child);
		f.label = Text(f, "GameFontNormalSmall");
		f.label:SetPoint("LEFT", f, "LEFT", 2, 0);
		f.c = {};
		for i = 1, 10 do
			local c = CreateFrame("Frame", nil, f);
			c:SetWidth(32);
			c:SetHeight(26);
			c:EnableMouse(true);
			K.Card(c);
			c.t = Text(c, "GameFontHighlightSmall");
			c.t:SetPoint("CENTER", c, "CENTER", 0, 0);
			local font = c.t:GetFont();
			if ( font ) then c.t:SetFont(font, 7); end     -- small enough for "Image soon" on two lines
			c.t:SetJustifyH("CENTER");
			c:SetScript("OnEnter", function() if ( this.p ) then ItemTooltip(this, this.p); end end);
			c:SetScript("OnLeave", function() GameTooltip:Hide(); end);
			c:SetScript("OnMouseUp", function()
				local p, cls = this.p, this.class;
				if ( not p ) then return; end
				Go(function()
					if ( planner.class ~= cls ) then planner.slots = {}; planner.stars = {}; end
					planner.class = cls;
					planner.slots[p.slot] = p; planner.stars[p.slot] = nil;   -- a new piece: 0 stars
					planner.pick = p.slot;
					view.gear = cls;
				end);
			end);
			f.c[i] = c;
		end
		return f;
	end);
	local col = RARITY_RGB[it.label] or RARITY_RGB.Common;
	f.label:SetText(it.label);
	f.label:SetTextColor(col[1], col[2], col[3]);
	for i = 1, 10 do
		local c, p = f.c[i], it.items[i];
		if ( p ) then
			c.p, c.class = p, it.class;
			c:ClearAllPoints();
			c:SetPoint("LEFT", f, "LEFT", 90 + (i - 1) * 36, 0);
			c:SetBackdropBorderColor(col[1], col[2], col[3]);
			local ab = (p.slot == "Main Hand" and "MH") or (p.slot == "Off Hand" and "OH") or (p.slot == "Ranged" and "Ra") or string.sub(p.slot, 1, 2);
			local ic = ItemIcon(p.name);                   -- the game's own icon once a hero has worn it; else the slot
			if ( not c.icon ) then
				c.icon = c:CreateTexture(nil, "ARTWORK");
				c.icon:SetPoint("TOPLEFT", c, "TOPLEFT", 3, -3);
				c.icon:SetPoint("BOTTOMRIGHT", c, "BOTTOMRIGHT", -3, 3);
			end
			if ( ic ) then
				c.icon:SetTexture(string.find(ic, "\\", 1, true) and ic or ("Interface\\Icons\\" .. ic));
				c.icon:Show();
				c.t:SetText("");
			else
				c.icon:Hide();
				c.t:SetText("Image\nsoon");             -- not a real item: no picture yet
			end
			c:Show();
		else c.p = nil; c:Hide(); end
	end
	f:SetWidth(CW);
	f:SetHeight(28);
	f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
	return y + 31;
end

local function Lay(items)
	for k, list in pairs(pools) do
		used[k] = 0;
		for i = 1, table.getn(list) do list[i]:Hide(); end
	end
	local y = 0;
	for _, it in ipairs(items) do
		if ( it.kind == "head" ) then
			local f = Get("head", function()
				local f = CreateFrame("Frame", nil, child);
				f.text = Text(f, "GameFontNormal");
				f.text:SetPoint("TOPLEFT", f, "TOPLEFT", 2, -6);
				f.line = f:CreateTexture(nil, "ARTWORK");
				f.line:SetTexture(0.7, 0.6, 0.38, 0.6);
				f.line:SetHeight(1);
				f.line:SetPoint("BOTTOMLEFT", f, "BOTTOMLEFT", 0, 2);
				f.line:SetPoint("BOTTOMRIGHT", f, "BOTTOMRIGHT", 0, 2);
				return f;
			end);
			f:SetWidth(CW);
			f:SetHeight(24);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -(y + 4));
			f.text:SetText(it.text);
			y = y + 32;
		elseif ( it.kind == "para" or it.kind == "note" ) then
			local f = Get("para", function()
				local f = CreateFrame("Frame", nil, child);
				f.text = Text(f, "GameFontHighlight");
				f.text:SetPoint("TOPLEFT", f, "TOPLEFT", 2, 0);
				return f;
			end);
			f.text:SetWidth(CW - 4);
			if ( it.kind == "note" ) then f.text:SetTextColor(0.7, 0.62, 0.42); else f.text:SetTextColor(0.9, 0.87, 0.75); end
			f.text:SetText(Highlight(it.text));
			local h = K.TextHeight(f.text, CW - 4);
			f.text:SetHeight(h);
			f:SetWidth(CW);
			f:SetHeight(h);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
			y = y + h + 8;
		elseif ( it.kind == "bullet" ) then
			local f = Get("bullet", function()
				local f = CreateFrame("Frame", nil, child);
				f.icon = f:CreateTexture(nil, "ARTWORK");
				f.icon:SetWidth(18);
				f.icon:SetHeight(18);
				f.icon:SetPoint("TOPLEFT", f, "TOPLEFT", 2, 1);
				f.dot = f:CreateTexture(nil, "ARTWORK");
				f.dot:SetTexture(0.95, 0.76, 0.3);
				f.dot:SetWidth(5);
				f.dot:SetHeight(5);
				f.dot:SetPoint("TOPLEFT", f, "TOPLEFT", 8, -5);
				f.text = Text(f, "GameFontHighlight");
				f.text:SetPoint("TOPLEFT", f, "TOPLEFT", 26, 0);
				return f;
			end);
			if ( it.icon ) then K.Icon(f.icon, it.icon); f.dot:Hide(); else f.icon:Hide(); f.dot:Show(); end
			f.text:SetWidth(CW - 30);
			f.text:SetTextColor(0.9, 0.87, 0.75);
			f.text:SetText(Highlight(it.text));
			local h = math.max(18, K.TextHeight(f.text, CW - 30));
			f.text:SetHeight(h);
			f:SetWidth(CW);
			f:SetHeight(h);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
			y = y + h + 6;
		elseif ( it.kind == "tip" ) then
			local f = Get("tip", function()
				local f = CreateFrame("Frame", nil, child);
				K.Card(f, 0.6, 0.5, 0.3);
				f.bar = f:CreateTexture(nil, "ARTWORK");
				f.bar:SetTexture(0.95, 0.76, 0.3);
				f.bar:SetWidth(3);
				f.bar:SetPoint("TOPLEFT", f, "TOPLEFT", 5, -5);
				f.bar:SetPoint("BOTTOMLEFT", f, "BOTTOMLEFT", 5, 5);
				f.head = Text(f, "GameFontNormalSmall");
				f.head:SetPoint("TOPLEFT", f, "TOPLEFT", 16, -8);
				f.text = Text(f, "GameFontHighlight");
				f.text:SetPoint("TOPLEFT", f.head, "BOTTOMLEFT", 0, -3);
				return f;
			end);
			f.head:SetText(it.head);
			f.text:SetWidth(CW - 32);
			f.text:SetTextColor(0.92, 0.87, 0.72);
			f.text:SetText(Highlight(it.text));
			local h = K.TextHeight(f.text, CW - 32);
			f.text:SetHeight(h);
			f:SetWidth(CW);
			f:SetHeight(h + 34);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -(y + 2));
			y = y + h + 42;
		elseif ( it.kind == "cards" ) then
			local cw, ch = math.floor((CW - 8) / 2), 64;
			for i = 1, table.getn(it.items) do
				local c = it.items[i];
				local f = Get("card", function()
					local f = CreateFrame("Frame", nil, child);
					K.Card(f);
					f:EnableMouse(true);
					f.icon = f:CreateTexture(nil, "ARTWORK");
					f.icon:SetWidth(36);
					f.icon:SetHeight(36);
					f.icon:SetPoint("TOPLEFT", f, "TOPLEFT", 9, -9);
					f.num = Text(f, "GameFontDisableSmall");
					f.num:SetPoint("TOPRIGHT", f, "TOPRIGHT", -9, -8);
					f.title = Text(f, "GameFontNormal");
					f.title:SetPoint("TOPLEFT", f, "TOPLEFT", 54, -9);
					f.desc = Text(f, "GameFontHighlightSmall");
					f.desc:SetPoint("TOPLEFT", f.title, "BOTTOMLEFT", 0, -3);
					f:SetScript("OnEnter", function() this:SetBackdropBorderColor(0.95, 0.76, 0.3); end);
					f:SetScript("OnLeave", function() this:SetBackdropBorderColor(0.35, 0.33, 0.4); end);
					f:SetScript("OnMouseUp", function() if ( this.go ) then Go(this.go); end end);
					return f;
				end);
				f:SetWidth(cw);
				f:SetHeight(ch);
				f:SetPoint("TOPLEFT", child, "TOPLEFT", math.mod(i - 1, 2) * (cw + 8), -(y + math.floor((i - 1) / 2) * (ch + 8)));
				if ( c.icon ) then K.Icon(f.icon, c.icon); else SetPicture(f.icon, c.title); end
				f.num:SetText(i);
				f.title:SetText(c.title);
				f.title:SetWidth(cw - 90);
				f.desc:SetText(c.desc);
				f.desc:SetWidth(cw - 64);
				f.desc:SetHeight(26);
				f.desc:SetTextColor(0.7, 0.66, 0.55);
				f.go = c.go;
				f:SetBackdropBorderColor(0.35, 0.33, 0.4);
			end
			y = y + math.ceil(table.getn(it.items) / 2) * (ch + 8);
		elseif ( it.kind == "spell" ) then
			local f = Get("spell", function()
				local f = CreateFrame("Frame", nil, child);
				K.Card(f);
				f:EnableMouse(true);
				f.icon = f:CreateTexture(nil, "ARTWORK");
				f.icon:SetWidth(34);
				f.icon:SetHeight(34);
				f.icon:SetPoint("TOPLEFT", f, "TOPLEFT", 7, -7);
				f.name = Text(f, "GameFontNormal");
				f.name:SetPoint("TOPLEFT", f, "TOPLEFT", 50, -7);
				f.text = Text(f, "GameFontHighlightSmall");
				f.text:SetPoint("TOPLEFT", f.name, "BOTTOMLEFT", 0, -3);
				f.by = Text(f, "GameFontDisableSmall");
				f.chips = {};
				f:SetScript("OnEnter", function() SpellTooltip(this, this.it); end);
				f:SetScript("OnLeave", function() GameTooltip:Hide(); end);
				return f;
			end);
			f.it = it;
			K.Icon(f.icon, it.icon or "INV_Misc_QuestionMark");
			f.name:SetText(it.name);
			f.text:SetWidth(CW - 62);
			f.text:SetTextColor(0.9, 0.87, 0.75);
			f.text:SetText(Highlight(it.text));
			local th = K.TextHeight(f.text, CW - 62);
			f.text:SetHeight(th);
			local h = math.max(48, 7 + 16 + th + 10);
			for i = 1, table.getn(f.chips) do f.chips[i]:Hide(); end
			if ( table.getn(it.changers) > 0 ) then
				f.by:ClearAllPoints();
				f.by:SetPoint("TOPLEFT", f, "TOPLEFT", 50, -(h - 2));
				f.by:SetText("Talents that change it:");
				f.by:Show();
				local x = 50 + f.by:GetStringWidth() + 6;
				for i = 1, math.min(14, table.getn(it.changers)) do
					local c = f.chips[i];
					if ( not c ) then
						c = CreateFrame("Frame", nil, f);
						c:SetWidth(18);
						c:SetHeight(18);
						c:EnableMouse(true);
						c.icon = c:CreateTexture(nil, "ARTWORK");
						c.icon:SetAllPoints(c);
						c:SetScript("OnEnter", function() TalentTooltip(this, this.t, this.spec); end);
						c:SetScript("OnLeave", function() GameTooltip:Hide(); end);
						f.chips[i] = c;
					end
					c.t, c.spec = it.changers[i][1], it.changers[i][2];
					c:ClearAllPoints();
					c:SetPoint("TOPLEFT", f, "TOPLEFT", x + (i - 1) * 21, -(h - 5));
					K.Icon(c.icon, c.t[2]);
					c:Show();
				end
				h = h + 20;
			else
				f.by:Hide();
			end
			h = LaySpellsBy(f, it, h);
			h = LaySetsBy(f, it, h);
			f:SetWidth(CW);
			f:SetHeight(h);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
			y = y + h + 5;
		elseif ( it.kind == "button" ) then
			local f = Get("button", function()
				local b = K.Button(child, "", 160, function() if ( this.go ) then Go(this.go); end end);
				b:SetHeight(24);
				return b;
			end);
			f:SetText(it.text);
			f.go = it.go;
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -(y + 4));
			y = y + 34;
		elseif ( it.kind == "buttons" or it.kind == "stats" or it.kind == "row" or it.kind == "chips" ) then
			y = LayMore(it, y);
		elseif ( it.kind == "trees" ) then
			local trees = D().talents[it.class] or {};
			local tw, th = math.floor((CW - 16) / 3), 0;
			for i = 1, table.getn(trees) do
				local f = Get("tree", MakeTree);
				f:SetPoint("TOPLEFT", child, "TOPLEFT", (i - 1) * (tw + 8), -y);
				th = math.max(th, DrawTree(f, trees[i], tw, i));
			end
			y = y + th + 8;
		end
	end
	child:SetHeight(math.max(1, y));
end

-- ---------------------------------------------------------------- the window
local function Where()
	local topics = D().topics;
	local topic = view.topic and topics[view.topic];
	if ( view.prompt == "gear" ) then return "Gear Planner", "Pick a class: its gear and the jewelry, and what they add up to", "Gear & the Armory"; end
	if ( view.prompt ) then return "Talent Tree Builder", "Pick a class and try out a build of up to 51 points", "Talents"; end
	if ( view.gear ) then return view.gear .. " Gear Planner", "Pick a slot, then a piece: the totals add everything chosen", view.gear; end
	if ( view.talents ) then return view.talents .. " Talents", "The " .. view.talents .. "'s three talent trees, from the game", view.talents; end
	if ( not topic ) then return "Guide", "Everything the launcher's Guide has, inside the game. Pick a topic.", "Getting Started"; end
	local page = view.page and topic.pages[view.page];
	if ( page ) then return page.title, topic.title .. "  -  " .. page.desc, page.title; end
	return topic.title, topic.desc, topic.title;
end

local function Refresh()
	if ( not frame or not frame:IsShown() ) then return; end
	local title, line, pic = Where();
	hero.heading:SetText(title);
	hero.line:SetText(line);
	SetPicture(hero.icon, pic);
	if ( view.topic or view.talents or view.prompt or view.gear ) then backBtn:Show(); homeBtn:Show(); else backBtn:Hide(); homeBtn:Hide(); end
	-- previous / next: the pages of the topic, or the topics
	local topics = D().topics;
	local count, at = 0, 0;
	if ( view.talents or view.prompt or view.gear ) then count = 0;
	elseif ( view.topic and view.page ) then count, at = table.getn(topics[view.topic].pages), view.page;
	elseif ( view.topic ) then count, at = table.getn(topics), view.topic; end
	if ( count > 0 and at > 1 ) then prevBtn:Show(); else prevBtn:Hide(); end
	if ( count > 0 and at < count ) then nextBtn:Show(); else nextBtn:Hide(); end
	local ok, err = pcall(Lay, Items());
	if ( not ok ) then K.Print(NAME, "|cffff4040this page could not be shown:|r " .. tostring(err)); end
	scroll.Top();
end

local function Step(d)
	if ( view.page ) then view.page = view.page + d; else view.topic = view.topic + d; end
end

local function Back()
	if ( view.prompt ) then view.prompt = nil;
	elseif ( view.gear ) then view.gear = nil;
	elseif ( view.talents ) then view.talents = nil; build.class = nil;
	elseif ( view.page ) then view.page = nil;
	else view.topic = nil; end
end

local function Build()
	frame = K.Window("AZSHandbookFrame", NAME, W, H);
	frame:SetScript("OnShow", function() K.Sound("igSpellBookOpen"); Refresh(); end);
	frame:SetScript("OnHide", function() K.Sound("igSpellBookClose"); end);
	frame.Refresh = Refresh;

	hero = K.Hero(frame, W - 40);
	hero:SetPoint("TOPLEFT", frame, "TOPLEFT", 20, -30);
	homeBtn = K.Button(hero, "Home", 70, function() Go(function() view.topic, view.page, view.talents, view.prompt, view.gear = nil, nil, nil, nil, nil; build.class = nil; end); end);
	homeBtn:SetPoint("TOPRIGHT", hero, "TOPRIGHT", -10, -10);
	backBtn = K.Button(hero, "Back", 70, function() Go(Back); end);
	backBtn:SetPoint("RIGHT", homeBtn, "LEFT", -4, 0);
	hero.heading:SetWidth(W - 40 - 70 - 170);

	scroll, child = K.Scroll("AZSHandbookScroll", frame, CW);
	scroll:SetPoint("TOPLEFT", hero, "BOTTOMLEFT", 2, -8);
	scroll:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -40, 48);

	prevBtn = K.Button(frame, "< Previous", 110, function() Go(function() Step(-1); end); end);
	prevBtn:SetPoint("BOTTOMLEFT", frame, "BOTTOMLEFT", 20, 18);
	nextBtn = K.Button(frame, "Next >", 110, function() Go(function() Step(1); end); end);
	nextBtn:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -20, 18);
end

function AZSHandbook_Toggle()
	if ( not frame ) then
		local ok, err = pcall(Build);
		if ( not ok ) then K.Print(NAME, "|cffff4040the handbook window could not be made:|r " .. tostring(err)); frame = nil; return; end
	end
	if ( frame:IsShown() ) then frame:Hide(); else frame:Show(); end
end

-- open a class's page straight away: /handbook mage
local function Open(what)
	what = string.lower(what or "");
	local topics = D().topics;
	for i = 1, table.getn(topics) do
		if ( string.lower(topics[i].title) == what ) then view.topic, view.page, view.talents, view.prompt, view.gear = i, nil, nil, nil, nil; return true; end
		for j = 1, table.getn(topics[i].pages) do
			if ( string.lower(topics[i].pages[j].title) == what ) then view.topic, view.page, view.talents, view.prompt, view.gear = i, j, nil, nil, nil; return true; end
		end
	end
	return false;
end

SLASH_AZSHANDBOOK1 = "/handbook";
SLASH_AZSHANDBOOK2 = "/azsh";
SlashCmdList["AZSHANDBOOK"] = function(msg)
	msg = msg or "";
	if ( msg == "minimap" ) then
		local shown = AZSHandbook_Mini and AZSHandbook_Mini.Toggle();
		K.Print(NAME, shown and "the minimap button is back." or "the minimap button is hidden. /handbook minimap brings it back.");
		return;
	end
	if ( msg ~= "" and Open(msg) ) then
		if ( not frame ) then AZSHandbook_Toggle(); elseif ( not frame:IsShown() ) then frame:Show(); else Refresh(); end
		return;
	end
	AZSHandbook_Toggle();
end

-- the minimap button: a book, at the top left of the minimap (AZS Stats' is at the bottom)
local ok, err = pcall(function()
	AZSHandbook_Mini = K.Minimap({ name = "AZSHandbookMinimapButton", icon = "INV_Misc_Book_11", angle = 110, db = DB,
		click = function() AZSHandbook_Toggle(); end,
		tooltip = function(tt)
			tt:AddLine(NAME);
			tt:AddLine("The launcher's Guide in game.", 1, 1, 1);
			tt:AddLine(" ");
			tt:AddLine("Click: open the handbook. /handbook <class> opens a class.", 0.6, 0.6, 0.6);
		end });
end);
if ( not ok ) then K.Print(NAME, "|cffff4040the minimap button could not be made:|r " .. tostring(err)); end
