-- AZS Stats in game: /azs, or the AZS Stats button by the minimap, opens the same STATS page as the launcher's, built
-- the same way: a header card (your portrait or the class's icon, a heading, a line and stat chips), then tiles and
-- rows in a scrolling list (an icon, a coloured name, a green detail line, the value on the right and a thin bar), and
-- the character sheet's tabs along the bottom: Overview, Classes, Abilities, Records, Nemesis, Co-op and History.
-- CLASS picks every class or one class on its own. Inside the game it uses the game's own art: the dialog frame and
-- header, the tooltip borders, the scroll bar, the tabs, your portrait, class and spell icons and item icons.
-- Lua 5.0 / 1.12 interface: scripts read `this`, `arg1`; no # operator; only the 1.12 API.

local W, H = 640, 540
local CW = W - 64                                       -- the list's width (inside the frame, beside the scroll bar)
local TABS = { "Overview", "Classes", "Abilities", "Records", "Nemesis", "Co-op", "History" };
local CLASS_COLORS = { warrior = "c79c6e", paladin = "f58cba", hunter = "abd473", rogue = "fff569", priest = "ffffff",
                       shaman = "2f8fff", mage = "69ccf0", warlock = "9482c9", druid = "ff7d0a", death_knight = "c41f3b" };

local tab, classKey = 1, nil;
local frame, tabButtons, classButton, scroll, child, hero;

-- icons: { texture, left, right, top, bottom } (the coordinates cut one class out of the class sheet)
local CLASS_SHEET = "Interface\\Glues\\CharacterCreate\\UI-CharacterCreate-Classes";
local CLASS_COORDS = { warrior = { 0, 0.25, 0, 0.25 }, mage = { 0.25, 0.49609375, 0, 0.25 }, rogue = { 0.49609375, 0.7421875, 0, 0.25 },
                       druid = { 0.7421875, 0.98828125, 0, 0.25 }, hunter = { 0, 0.25, 0.25, 0.5 }, shaman = { 0.25, 0.49609375, 0.25, 0.5 },
                       priest = { 0.49609375, 0.7421875, 0.25, 0.5 }, warlock = { 0.7421875, 0.98828125, 0.25, 0.5 },
                       paladin = { 0, 0.25, 0.5, 0.75 } };
local QUESTION = "Interface\\Icons\\INV_Misc_QuestionMark";
local function I(name) return { "Interface\\Icons\\" .. name }; end

local function ClassIcon(key)
	key = string.lower(tostring(key or ""));
	for k, c in pairs(CLASS_COORDS) do
		if ( string.find(key, k, 1, true) ) then return { CLASS_SHEET, c[1], c[2], c[3], c[4] }; end
	end
	return nil;
end

-- a spell's name as the icon table knows it: no case, spaces or punctuation ("Shadow Word: Pain" = "shadowwordpain"),
-- and the usual short forms
local SHORT = { swpain = "shadowwordpain", swp = "shadowwordpain", pwshield = "powerwordshield", pws = "powerwordshield",
                swdeath = "shadowworddeath", pwfortitude = "powerwordfortitude", mark = "huntersmark" };
local function SpellKey(name)
	local k = string.lower(tostring(name or ""));
	k = string.gsub(k, "%s*%(.-%)%s*$", "");            -- "(Rank 4)"
	k = string.gsub(k, "%s+%d+$", "");                    -- "Frostbolt 4"
	k = string.gsub(k, "[^%a%d]", "");
	return SHORT[k] or k;
end

local function SpellIcon(name)
	local icon = AZSSTATS_SPELL_ICONS and AZSSTATS_SPELL_ICONS[SpellKey(name)];
	if ( icon ) then return I(icon); end
	return { QUESTION };
end

local function SetIcon(tex, icon)
	if ( not icon ) then tex:Hide(); return; end
	tex:SetTexture(icon[1]);
	if ( icon[2] ) then tex:SetTexCoord(icon[2], icon[3], icon[4], icon[5]); else tex:SetTexCoord(0.07, 0.93, 0.07, 0.93); end
	tex:Show();
end

local function S() return AZSStats; end
local function DB() S().Check(); return AZSStatsDB; end
local function Num(v) return tonumber(v) or 0; end
local function Big(n) return S().Big(n); end
local function Clock(n) return S().Clock(n); end

local function Hours(sec)
	sec = math.floor(Num(sec));
	local h = math.floor(sec / 3600);
	local m = math.floor(math.mod(sec, 3600) / 60);
	if ( h > 0 ) then return h .. "h " .. string.format("%02d", m) .. "m"; end
	return m .. "m";
end

local function Pct(a, b)
	if ( Num(b) <= 0 ) then return "0%"; end
	return math.floor(100 * Num(a) / Num(b) + 0.5) .. "%";
end

local function C(hex, text) return "|cff" .. hex .. tostring(text) .. "|r"; end

local function ClassHex(key)
	key = string.lower(tostring(key or ""));
	for k, v in pairs(CLASS_COLORS) do
		if ( string.find(key, k, 1, true) ) then return v; end
	end
	return "ffd100";
end

-- "a1b2c3" -> r, g, b (0..1)
local function RGB(hex)
	local function h(i) return (tonumber(string.sub(hex, i, i + 1), 16) or 255) / 255; end
	return h(1), h(3), h(5);
end

-- the block shown: every run, or one class's
local function Block()
	local db = DB();
	if ( classKey and type(db.classes[classKey]) == "table" ) then return db.classes[classKey], db.classes[classKey].name; end
	classKey = nil;
	return db.totals, "All classes";
end

-- sorted list of { key, v } from a table, by f(value), largest first
local function Sorted(t, f)
	local out = {};
	for k, v in pairs(t or {}) do
		if ( type(v) == "table" ) then table.insert(out, { key = k, v = v }); end
	end
	table.sort(out, function(a, b) return f(a.v) > f(b.v); end);
	return out;
end

-- ---------------------------------------------------------------- what each tab shows
-- A tab is a list of items, the same blocks as the launcher's STATS page:
--   { kind = "head", text }                                   a section title
--   { kind = "tiles", items = { { value, label }, ... } }     tiles, four to a row
--   { kind = "row", icon, name, hex, sub, sub2, value, vsub, vhex, bar, bhex }   a row card
--   { kind = "note", text }                                   a dim line
local function Head(out, text) table.insert(out, { kind = "head", text = text }); end
local function Tiles(out, items) table.insert(out, { kind = "tiles", items = items }); end
local function Note(out, text) table.insert(out, { kind = "note", text = text }); end
local function Row(out, r) r.kind = "row"; table.insert(out, r); end

local RECORD_ICONS = { time = "INV_Misc_PocketWatch_01", fastestWin = "Spell_Nature_Swiftness", lvl = "Spell_Holy_SealOfWisdom",
                       dps = "Ability_Warrior_BattleShout", kills = "Ability_DualWield", dmg = "Spell_Fire_Fireball02",
                       gold = "INV_Misc_Coin_01", bosses = "INV_Misc_Head_Dragon_01", elites = "Ability_Warrior_Challange",
                       guardians = "INV_Shield_06" };

local function Overview()
	local out = {};
	local db = DB();
	local b, name = Block();
	local best = classKey and b.best or db.best;
	Head(out, classKey and (name .. ": your runs") or "Your runs");
	Tiles(out, { { Big(b.runs), "Runs" }, { Big(b.wins), "Victories" }, { Pct(b.wins, b.runs), "Win rate" }, { Hours(b.time), "Time in runs" },
	             { Big(b.kills), "Enemies slain" }, { Big(b.elites), "Elites" }, { Big(b.guardians), "Guardians" }, { Big(b.bosses), "Bosses" },
	             { Big(b.dmg), "Damage dealt" }, { Big(b.taken), "Damage taken" }, { Big(b.gold), "Gold collected" }, { Big(b.chests), "Caches opened" } });
	Head(out, "Highlights");
	local bd = best.dps;
	Row(out, { icon = I(RECORD_ICONS.dps), name = "Highest DPS", hex = "ff8000", sub = type(bd) == "table" and ((bd.cname or "") .. " · " .. (bd.zname or "")) or "Finish a run to set one",
	           value = type(bd) == "table" and Big(bd.v) or "-", vhex = "ff8000", vsub = type(bd) == "table" and (bd.date or "") or "" });
	local h = db.history;
	for i = table.getn(h), 1, -1 do
		local r = h[i];
		if ( not classKey or r.class == classKey ) then
			Row(out, { icon = ClassIcon(r.class), name = "Last run: " .. (r.result or ""), hex = r.kind == "victory" and "1eff00" or r.kind == "defeat" and "ff4040" or "9d9d9d",
			           sub = (r.cname or "") .. " · " .. (r.zname or "") .. " · " .. Clock(r.t) .. ", level " .. Big(r.lvl),
			           value = Num(r.dps) > 0 and (Big(r.dps) .. " DPS") or "", vhex = "ff8000", vsub = r.date or "" });
			break;
		end
	end
	if ( not classKey ) then
		local p = db.play;
		Head(out, "Play time");
		Tiles(out, { { Hours(p.seconds), "Time played" }, { Big(p.sessions), "Sessions" }, { Hours(p.last), "This session" }, { Hours(p.longest), "Longest session" } });
	end
	return out;
end

local function Classes()
	local out = {};
	local db = DB();
	Head(out, "Class pick rate");
	local list = Sorted(db.classes, function(v) return Num(v.runs); end);
	for i = 1, table.getn(list) do
		local g = list[i].v;
		Row(out, { icon = ClassIcon(list[i].key), name = g.name ~= "" and g.name or S().Pretty(list[i].key), hex = ClassHex(list[i].key),
		           sub = "Runs: " .. Big(g.runs) .. " · Won: " .. Big(g.wins) .. " · Longest: " .. Clock(g.bestTime) .. " · Best level: " .. Big(g.bestLvl),
		           value = Pct(g.runs, db.totals.runs), vsub = "Win rate " .. Pct(g.wins, g.runs),
		           bar = Num(g.runs) / math.max(1, Num(db.totals.runs)), bhex = ClassHex(list[i].key) });
	end
	if ( table.getn(list) == 0 ) then Note(out, "No runs recorded yet."); end
	local races = Sorted(db.races, function(v) return Num(v.runs); end);
	if ( table.getn(races) > 0 ) then
		Head(out, "Races");
		for i = 1, table.getn(races) do
			local g = races[i].v;
			Row(out, { icon = I("INV_Misc_Head_Human_01"), name = g.name ~= "" and g.name or S().Pretty(races[i].key), hex = "ffd100",
			           sub = "Runs: " .. Big(g.runs) .. " · Won: " .. Big(g.wins), value = Clock(g.bestTime), vsub = "Level " .. Big(g.bestLvl) });
		end
	end
	local zones = Sorted(db.zones, function(v) return Num(v.runs); end);
	if ( table.getn(zones) > 0 ) then
		Head(out, "Zones");
		for i = 1, table.getn(zones) do
			local g = zones[i].v;
			Row(out, { icon = I("INV_Misc_Map_01"), name = g.name ~= "" and g.name or S().Pretty(zones[i].key), hex = "69ccf0",
			           sub = "Runs: " .. Big(g.runs) .. " · Won: " .. Big(g.wins), value = Clock(g.bestTime), vsub = "Level " .. Big(g.bestLvl) });
		end
	end
	return out;
end

local function Abilities()
	local out = {};
	local b, name = Block();
	local list = Sorted(classKey and b.abilities or DB().abilities, function(v) return Num(v.picks); end);
	Head(out, "Abilities  -  " .. name);
	for i = 1, table.getn(list) do
		local a = list[i].v;
		local nm = a.name or S().Pretty(list[i].key);
		Row(out, { icon = SpellIcon(nm), name = nm, hex = "f5e9c8",
		           sub = "Picked in " .. Big(a.picks) .. " runs (" .. Pct(a.picks, b.runs) .. ") · Win rate " .. Pct(a.wins, a.picks),
		           value = Big(a.dmg) .. " damage", vsub = Num(a.bestRank) > 0 and ("Best rank " .. a.bestRank) or "",
		           bar = Num(a.picks) / math.max(1, Num(b.runs)), bhex = "b39b62" });
	end
	if ( table.getn(list) == 0 ) then Note(out, "No abilities recorded yet."); end
	return out;
end

local function Records()
	local out = {};
	local b, name = Block();
	local best = classKey and b.best or DB().best;
	Head(out, "Personal bests  -  " .. name);
	local order = { { "time", "Longest run", true }, { "fastestWin", "Fastest victory", true }, { "lvl", "Highest level" },
	                 { "dps", "Highest DPS" }, { "kills", "Most kills in a run" }, { "dmg", "Most damage in a run" }, { "gold", "Most gold in a run" },
	                 { "bosses", "Most bosses in a run" }, { "elites", "Most elites in a run" }, { "guardians", "Most guardians in a run" } };
	local any = false;
	for i = 1, table.getn(order) do
		local r = best[order[i][1]];
		if ( type(r) == "table" ) then
			any = true;
			Row(out, { icon = I(RECORD_ICONS[order[i][1]] or "INV_Misc_QuestionMark"), name = order[i][2], hex = "ff8000",
			           sub = (r.cname or "") .. " · " .. (r.zname or ""), value = order[i][3] and Clock(r.v) or Big(r.v), vsub = r.date or "" });
		end
	end
	if ( not any ) then Note(out, "No records yet."); end
	-- the builds behind your best runs: highest DPS first
	local builds = {};
	for i = 1, table.getn(DB().builds or {}) do
		local bd = DB().builds[i];
		if ( not classKey or bd.class == classKey ) then table.insert(builds, bd); end
	end
	table.sort(builds, function(a, c) return Num(a.dps) > Num(c.dps); end);
	Head(out, "Best builds");
	for i = 1, math.min(8, table.getn(builds)) do
		local bd = builds[i];
		local parts = {};
		for j = 1, table.getn(bd.abilities or {}) do
			local a = bd.abilities[j];
			table.insert(parts, (a.name or "?") .. (Num(a.rank) > 0 and (" " .. a.rank) or ""));
		end
		Row(out, { icon = ClassIcon(bd.class), name = (bd.cname or "") .. " · " .. (bd.kind == "victory" and "Victory" or (bd.result or "")) .. " · " .. (bd.zname or ""),
		           hex = ClassHex(bd.class), sub = table.concat(parts, ", "), value = Big(bd.dps) .. " DPS", vhex = "ff8000",
		           vsub = (bd.date or "") .. " · " .. Clock(bd.t), spells = bd.abilities });
	end
	if ( table.getn(builds) == 0 ) then Note(out, "Your best builds show up here after a DPS high or a victory."); end
	return out;
end

local function Nemesis()
	local out = {};
	local db = DB();
	local b, name = Block();
	local killers = Sorted(classKey and b.nemesis or db.nemesis, function(v) return Num(v.kills); end);
	local deaths = 0;
	for i = 1, table.getn(killers) do deaths = deaths + Num(killers[i].v.kills); end
	local beaten = db.bossKills or {};
	if ( killers[1] ) then
		Head(out, "Your nemesis: " .. killers[1].key);
		Tiles(out, { { Big(killers[1].v.kills), "Times it killed you" }, { Pct(killers[1].v.kills, deaths), "Of your deaths" },
		             { Big(deaths), "Deaths" }, { Big(Num(beaten[killers[1].key])), "Times you beat it" } });
	end
	Head(out, "Who kills you");
	for i = 1, math.min(15, table.getn(killers)) do
		local k = killers[i];
		local won = Num(beaten[k.key]);
		Row(out, { icon = I("Ability_Creature_Cursed_02"), name = k.key, hex = "ff5a4a",
		           sub = "Killed you " .. Big(k.v.kills) .. " times (" .. Pct(k.v.kills, deaths) .. ")" .. (won > 0 and (" · You beat it " .. Big(won) .. " times") or ""),
		           value = Big(k.v.kills), vsub = k.v.last or "", bar = Num(k.v.kills) / math.max(1, deaths), bhex = "c41f3b" });
	end
	if ( table.getn(killers) == 0 ) then Note(out, "No one has killed you yet."); end
	-- revenge: the bosses you have beaten most
	local list = {};
	for nm, n in pairs(beaten) do table.insert(list, { key = nm, n = Num(n) }); end
	table.sort(list, function(a, c) return a.n > c.n; end);
	if ( table.getn(list) > 0 ) then
		Head(out, "Revenge: bosses you beat");
		for i = 1, math.min(15, table.getn(list)) do
			local k = db.nemesis[list[i].key];
			local got = type(k) == "table" and Num(k.kills) or 0;
			Row(out, { icon = I("Ability_Warrior_Revenge"), name = list[i].key, hex = "f5e9c8",
			           sub = "You beat it " .. Big(list[i].n) .. " times" .. (got > 0 and (" · It killed you " .. Big(got) .. " times") or ""),
			           value = Big(list[i].n) .. " : " .. Big(got), vhex = list[i].n >= got and "5fd35f" or "ff4040",
			           vsub = list[i].n >= got and "you lead" or "it leads", bar = list[i].n / math.max(1, list[1].n), bhex = "5fd35f" });
		end
	end
	Head(out, "Death log");
	local d = db.deaths;
	local shown = 0;
	for i = table.getn(d), 1, -1 do
		local e = d[i];
		if ( (not classKey or e.class == classKey) and shown < 30 ) then
			shown = shown + 1;
			local how = "";
			if ( (e.spell or "") ~= "" ) then how = " · " .. e.spell; end
			if ( Num(e.amt) > 0 ) then how = how .. " " .. Big(e.amt); end
			Row(out, { icon = ClassIcon(e.class), name = "Slain by " .. (e.killer or "?") .. how, hex = "ff5a4a",
			           sub = (e.cname or "") .. " · " .. (e.zname or "") .. " · Survived " .. Clock(e.t) .. " · Level " .. Big(e.lvl),
			           sub2 = (e.last or "") ~= "" and ("Last hits: " .. e.last) or nil, value = Clock(e.t), vsub = e.date or "" });
		end
	end
	if ( shown == 0 ) then Note(out, "No deaths recorded yet."); end
	return out;
end

local function Coop()
	local out = {};
	local co = DB().coop;
	Head(out, "Co-op");
	Tiles(out, { { Big(co.runs), "Runs with friends" }, { Pct(co.wins, co.runs), "Co-op win rate" }, { Pct(co.soloWins, co.solo), "Solo win rate" },
	             { Hours(co.time), "Time together" } });
	local list = Sorted(co.friends, function(v) return Num(v.runs); end);
	if ( list[1] ) then
		local t = list[1].v;
		Head(out, "Your top partner: " .. list[1].key .. ((t.cname or "") ~= "" and (" (" .. t.cname .. ")") or ""));
		Tiles(out, { { Big(t.runs), "Runs together" }, { Big(t.wins), "Victories together" }, { Pct(t.wins, t.runs), "Win rate together" },
		             { Hours(t.time), "Time together" } });
	end
	Head(out, "Your party");
	local most = list[1] and Num(list[1].v.runs) or 1;
	for i = 1, table.getn(list) do
		local e = list[i].v;
		local more = {};
		if ( Num(e.bestTime) > 0 ) then table.insert(more, "Best together: " .. Clock(e.bestTime) .. ", level " .. Big(e.bestLvl)); end
		if ( Num(e.fastestWin) > 0 ) then table.insert(more, "fastest win " .. Clock(e.fastestWin)); end
		if ( (e.first or "") ~= "" ) then table.insert(more, "since " .. string.sub(e.first, 1, 10)); end
		Row(out, { icon = ClassIcon(e.class) or I("INV_Misc_GroupNeedMore"), name = list[i].key .. ((e.cname or "") ~= "" and (" · " .. e.cname) or ""),
		           hex = ClassHex(e.class), sub = Big(e.runs) .. " runs together · " .. Big(e.wins) .. " won (" .. Pct(e.wins, e.runs) .. ")",
		           sub2 = table.concat(more, " · "), value = string.sub(e.last or "", 1, 10), vsub = "last played",
		           bar = Num(e.runs) / math.max(1, most), bhex = ClassHex(e.class) });
	end
	if ( table.getn(list) == 0 ) then Note(out, "No co-op runs recorded yet. Play a run with friends and they show up here."); end
	return out;
end

local function History()
	local out = {};
	local h = DB().history;
	local b, name = Block();
	Head(out, "Recent runs  -  " .. name);
	local shown = 0;
	for i = table.getn(h), 1, -1 do
		local r = h[i];
		if ( (not classKey or r.class == classKey) and shown < 40 ) then
			shown = shown + 1;
			local hex = r.kind == "victory" and "1eff00" or r.kind == "defeat" and "ff4040" or "9d9d9d";
			local parts = {};
			for j = 1, table.getn(r.abilities or {}) do table.insert(parts, r.abilities[j].name or "?"); end
			Row(out, { icon = ClassIcon(r.class), name = (r.result or "") .. " · " .. (r.cname or "") .. " · " .. (r.zname or ""), hex = hex,
			           sub = (r.date or "") .. (r.party and ("  with " .. r.party) or "") .. (r.killer and ("  slain by " .. r.killer) or ""),
			           sub2 = table.concat(parts, ", "), value = Clock(r.t), vhex = hex,
			           vsub = "Level " .. Big(r.lvl) .. " · " .. Big(r.kills) .. " kills" .. (Num(r.dps) > 0 and (" · " .. Big(r.dps) .. " DPS") or "") });
		end
	end
	if ( shown == 0 ) then Note(out, "No runs recorded yet."); end
	return out;
end

local BUILD = { Overview, Classes, Abilities, Records, Nemesis, Coop, History };

-- ---------------------------------------------------------------- drawing the blocks (frames are reused)
local pools = { row = {}, tile = {}, head = {}, note = {} };
local used = { row = 0, tile = 0, head = 0, note = 0 };

-- a card in the tooltip style: dark inside, a thin border
local function Card(f, r, g, b)
	f:SetBackdrop({ bgFile = "Interface\\Tooltips\\UI-Tooltip-Background", edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border",
	                tile = true, tileSize = 16, edgeSize = 12, insets = { left = 3, right = 3, top = 3, bottom = 3 } });
	f:SetBackdropColor(0.08, 0.07, 0.11, 0.92);
	f:SetBackdropBorderColor(r or 0.35, g or 0.33, b or 0.4);
end

local function GetRow()
	used.row = used.row + 1;
	local f = pools.row[used.row];
	if ( f ) then return f; end
	f = CreateFrame("Frame", nil, child);
	Card(f);
	f.icon = f:CreateTexture(nil, "ARTWORK");
	f.icon:SetWidth(30);
	f.icon:SetHeight(30);
	f.icon:SetPoint("TOPLEFT", f, "TOPLEFT", 7, -6);
	f.name = f:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	f.name:SetPoint("TOPLEFT", f, "TOPLEFT", 44, -6);
	f.name:SetJustifyH("LEFT");
	f.sub = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
	f.sub:SetPoint("TOPLEFT", f.name, "BOTTOMLEFT", 0, -2);
	f.sub:SetJustifyH("LEFT");
	f.sub:SetTextColor(0.37, 0.83, 0.37);
	f.sub2 = f:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	f.sub2:SetPoint("TOPLEFT", f.sub, "BOTTOMLEFT", 0, -2);
	f.sub2:SetJustifyH("LEFT");
	f.value = f:CreateFontString(nil, "OVERLAY", "GameFontHighlight");
	f.value:SetPoint("TOPRIGHT", f, "TOPRIGHT", -9, -7);
	f.value:SetJustifyH("RIGHT");
	f.vsub = f:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	f.vsub:SetPoint("TOPRIGHT", f.value, "BOTTOMRIGHT", 0, -3);
	f.vsub:SetJustifyH("RIGHT");
	f.bar = CreateFrame("StatusBar", nil, f);
	f.bar:SetStatusBarTexture("Interface\\TargetingFrame\\UI-StatusBar");
	f.bar:SetMinMaxValues(0, 1);
	f.bar:SetHeight(3);
	f.bar:SetPoint("BOTTOMLEFT", f, "BOTTOMLEFT", 44, 5);
	f.bar:SetPoint("BOTTOMRIGHT", f, "BOTTOMRIGHT", -9, 5);
	f.spells = {};
	pools.row[used.row] = f;
	return f;
end

local function GetTile()
	used.tile = used.tile + 1;
	local f = pools.tile[used.tile];
	if ( f ) then return f; end
	f = CreateFrame("Frame", nil, child);
	Card(f);
	f.top = f:CreateTexture(nil, "ARTWORK");
	f.top:SetTexture(0.95, 0.76, 0.3);                     -- the launcher's gold line along the top
	f.top:SetHeight(2);
	f.top:SetPoint("TOPLEFT", f, "TOPLEFT", 6, -4);
	f.top:SetPoint("TOPRIGHT", f, "TOPRIGHT", -6, -4);
	f.value = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightLarge");
	f.value:SetPoint("TOP", f, "TOP", 0, -9);
	f.label = f:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	f.label:SetPoint("TOP", f.value, "BOTTOM", 0, -2);
	pools.tile[used.tile] = f;
	return f;
end

local function GetHead()
	used.head = used.head + 1;
	local f = pools.head[used.head];
	if ( f ) then return f; end
	f = CreateFrame("Frame", nil, child);
	f.text = f:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	f.text:SetPoint("TOPLEFT", f, "TOPLEFT", 2, -6);
	f.line = f:CreateTexture(nil, "ARTWORK");
	f.line:SetTexture(0.7, 0.6, 0.38, 0.6);
	f.line:SetHeight(1);
	f.line:SetPoint("BOTTOMLEFT", f, "BOTTOMLEFT", 0, 2);
	f.line:SetPoint("BOTTOMRIGHT", f, "BOTTOMRIGHT", 0, 2);
	pools.head[used.head] = f;
	return f;
end

local function GetNote()
	used.note = used.note + 1;
	local f = pools.note[used.note];
	if ( f ) then return f; end
	f = CreateFrame("Frame", nil, child);
	f.text = f:CreateFontString(nil, "OVERLAY", "GameFontDisable");
	f.text:SetPoint("TOPLEFT", f, "TOPLEFT", 6, -4);
	f.text:SetJustifyH("LEFT");
	pools.note[used.note] = f;
	return f;
end

-- a build's spells as small icons under its row
local function Spells(f, list, y)
	for i = 1, table.getn(f.spells) do f.spells[i]:Hide(); end
	if ( type(list) ~= "table" ) then return; end
	for i = 1, math.min(12, table.getn(list)) do
		local t = f.spells[i];
		if ( not t ) then
			t = f:CreateTexture(nil, "ARTWORK");
			t:SetWidth(22);
			t:SetHeight(22);
			f.spells[i] = t;
		end
		t:ClearAllPoints();
		t:SetPoint("TOPLEFT", f, "TOPLEFT", 44 + (i - 1) * 25, y);
		SetIcon(t, SpellIcon(list[i].name));
	end
end

local function Lay(items)
	for k, _ in pairs(used) do
		used[k] = 0;
		for i = 1, table.getn(pools[k]) do pools[k][i]:Hide(); end
	end
	local y = 0;
	for _, it in ipairs(items) do
		if ( it.kind == "head" ) then
			local f = GetHead();
			f:SetWidth(CW);
			f:SetHeight(24);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
			f.text:SetText(it.text);
			f:Show();
			y = y + 28;
		elseif ( it.kind == "note" ) then
			local f = GetNote();
			f:SetWidth(CW);
			f:SetHeight(20);
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
			f.text:SetText(it.text);
			f:Show();
			y = y + 22;
		elseif ( it.kind == "tiles" ) then
			local tw = math.floor((CW - 3 * 6) / 4);
			for i = 1, table.getn(it.items) do
				local f = GetTile();
				f:SetWidth(tw);
				f:SetHeight(42);
				f:ClearAllPoints();
				f:SetPoint("TOPLEFT", child, "TOPLEFT", math.mod(i - 1, 4) * (tw + 6), -(y + math.floor((i - 1) / 4) * 48));
				f.value:SetText(it.items[i][1]);
				f.label:SetText(it.items[i][2]);
				f:Show();
			end
			y = y + math.ceil(table.getn(it.items) / 4) * 48 + 4;
		else
			local f = GetRow();
			local h = 44;
			if ( it.sub2 and it.sub2 ~= "" ) then h = h + 12; end
			if ( it.spells ) then h = h + 26; end
			f:SetWidth(CW);
			f:SetHeight(h);
			f:ClearAllPoints();
			f:SetPoint("TOPLEFT", child, "TOPLEFT", 0, -y);
			SetIcon(f.icon, it.icon);
			f.name:SetText(it.name or "");
			f.name:SetTextColor(RGB(it.hex or "ffd100"));
			f.sub:SetText(it.sub or "");
			f.sub2:SetText(it.sub2 or "");
			f.value:SetText(it.value or "");
			f.value:SetTextColor(RGB(it.vhex or "f5e9c8"));
			f.vsub:SetText(it.vsub or "");
			local vw = math.max(f.value:GetStringWidth(), f.vsub:GetStringWidth());
			f.name:SetWidth(CW - 44 - vw - 24);
			f.sub:SetWidth(CW - 44 - vw - 24);
			f.sub2:SetWidth(CW - 44 - 16);
			if ( it.bar ) then
				f.bar:SetValue(math.min(1, math.max(0, it.bar)));
				f.bar:SetStatusBarColor(RGB(it.bhex or "b39b62"));
				f.bar:Show();
			else
				f.bar:Hide();
			end
			Spells(f, it.spells, -(h - 32));
			f:Show();
			y = y + h + 5;
		end
	end
	child:SetHeight(math.max(1, y));
	return y;
end

-- ---------------------------------------------------------------- the header card
local CHIPS = { { "kills", "Ability_DualWield", "Enemies slain" }, { "bosses", "INV_Misc_Head_Dragon_01", "Bosses" },
                { "gold", "INV_Misc_Coin_01", "Gold" }, { "winrate", "Spell_Holy_SealOfWisdom", "Win rate" },
                { "dps", "Ability_Warrior_BattleShout", "Highest DPS" }, { "time", "INV_Misc_PocketWatch_01", "Time played" } };

local function Hero()
	local db = DB();
	local b, name = Block();
	if ( classKey ) then
		SetIcon(hero.portrait, ClassIcon(classKey));
		hero.heading:SetText(C(ClassHex(classKey), name));
		hero.line:SetText("Runs " .. Big(b.runs) .. " · Victories " .. Big(b.wins) .. " · Best level " .. Big(b.bestLvl) ..
		                  "   ·   picked in " .. Pct(b.runs, db.totals.runs) .. " of your runs");
	else
		if ( SetPortraitTexture ) then SetPortraitTexture(hero.portrait, "player"); hero.portrait:SetTexCoord(0, 1, 0, 1); hero.portrait:Show(); end
		hero.heading:SetText(UnitName and UnitName("player") or "Your Survivor");
		hero.line:SetText("Runs " .. Big(b.runs) .. " · Victories " .. Big(b.wins) .. " · Best level " .. Big(b.bestLvl));
	end
	local best = classKey and b.best or db.best;
	local vals = { kills = Big(b.kills), bosses = Big(b.bosses), gold = Big(b.gold), winrate = Pct(b.wins, b.runs),
	               dps = type(best.dps) == "table" and Big(best.dps.v) or "-", time = classKey and Hours(b.time) or Hours(db.play.seconds) };
	for i = 1, table.getn(CHIPS) do
		hero.chips[i].value:SetText(vals[CHIPS[i][1]]);
	end
end

-- ---------------------------------------------------------------- the window
local function Refresh()
	if ( not frame or not frame:IsShown() ) then return; end
	if ( frame.numTabs and PanelTemplates_SetTab ) then
		PanelTemplates_SetTab(frame, tab);
	else
		for i = 1, table.getn(tabButtons) do
			if ( i == tab ) then tabButtons[i]:LockHighlight(); else tabButtons[i]:UnlockHighlight(); end
		end
	end
	local _, name = Block();
	classButton:SetText(classKey and name or "All classes");
	if ( tab == 2 or tab == 6 ) then classButton:Hide(); else classButton:Show(); end
	Hero();
	Lay(BUILD[tab]());
	if ( scroll.UpdateScrollChildRect ) then scroll:UpdateScrollChildRect(); end
	scroll:SetVerticalScroll(0);
	if ( AZSStatsScrollScrollBar ) then AZSStatsScrollScrollBar:SetValue(0); end
end

-- CLASS: every class, then each class you played (most played first)
local function NextClass()
	local list = Sorted(DB().classes, function(v) return Num(v.runs); end);
	local idx = 0;
	for i = 1, table.getn(list) do
		if ( list[i].key == classKey ) then idx = i; end
	end
	idx = idx + 1;
	if ( idx > table.getn(list) ) then classKey = nil; else classKey = list[idx].key; end
	Refresh();
end

local function Button(parent, text, width, onClick)
	local b = CreateFrame("Button", nil, parent, "UIPanelButtonTemplate");
	b:SetWidth(width);
	b:SetHeight(22);
	b:SetText(text);
	if ( onClick ) then b:SetScript("OnClick", onClick); end
	return b;
end

-- a frame from one of the game's templates, or a plain one if the game does not have that template
local function Templated(kind, name, parent, template)
	local ok, f = pcall(CreateFrame, kind, name, parent, template);
	if ( ok and f ) then return f, true; end
	return CreateFrame(kind, name, parent), false;
end

local function Sound(name) if ( PlaySound ) then pcall(PlaySound, name); end end

local function Build()
	frame = CreateFrame("Frame", "AZSStatsFrame", UIParent);
	frame:SetWidth(W);
	frame:SetHeight(H);
	frame:SetPoint("CENTER", UIParent, "CENTER", 0, 20);
	frame:SetFrameStrata("DIALOG");
	frame:SetBackdrop({ bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
	                    edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
	                    tile = true, tileSize = 32, edgeSize = 32, insets = { left = 11, right = 12, top = 12, bottom = 11 } });
	frame:EnableMouse(true);
	frame:SetMovable(true);
	if ( frame.SetClampedToScreen ) then frame:SetClampedToScreen(true); end   -- not in every client
	frame:RegisterForDrag("LeftButton");
	frame:SetScript("OnDragStart", function() this:StartMoving(); end);
	frame:SetScript("OnDragStop", function() this:StopMovingOrSizing(); end);
	frame:SetScript("OnShow", function() Sound("igCharacterInfoOpen"); Refresh(); end);
	frame:SetScript("OnHide", function() Sound("igCharacterInfoClose"); end);
	frame:Hide();
	table.insert(UISpecialFrames, "AZSStatsFrame");      -- Escape closes it

	-- the dialog header with the title
	local header = frame:CreateTexture(nil, "ARTWORK");
	header:SetTexture("Interface\\DialogFrame\\UI-DialogBox-Header");
	header:SetWidth(300);
	header:SetHeight(64);
	header:SetPoint("TOP", frame, "TOP", 0, 12);
	local title = frame:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	title:SetPoint("TOP", header, "TOP", 0, -14);
	title:SetText("AZS Stats");
	local close = CreateFrame("Button", nil, frame, "UIPanelCloseButton");
	close:SetPoint("TOPRIGHT", frame, "TOPRIGHT", -6, -6);

	-- the header card: portrait in a gold ring, heading, line, and six chips with item icons
	hero = CreateFrame("Frame", nil, frame);
	hero:SetWidth(W - 40);
	hero:SetHeight(104);
	hero:SetPoint("TOPLEFT", frame, "TOPLEFT", 20, -30);
	Card(hero, 0.6, 0.5, 0.3);
	hero.portrait = hero:CreateTexture(nil, "ARTWORK");
	hero.portrait:SetWidth(52);
	hero.portrait:SetHeight(52);
	hero.portrait:SetPoint("TOPLEFT", hero, "TOPLEFT", 12, -10);
	local ring = hero:CreateTexture(nil, "OVERLAY");
	ring:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder");
	ring:SetWidth(92);
	ring:SetHeight(92);
	ring:SetPoint("TOPLEFT", hero.portrait, "TOPLEFT", -7, 6);
	hero.heading = hero:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge");
	hero.heading:SetPoint("TOPLEFT", hero, "TOPLEFT", 78, -12);
	hero.line = hero:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
	hero.line:SetPoint("TOPLEFT", hero.heading, "BOTTOMLEFT", 0, -4);
	classButton = Button(hero, "All classes", 130, function() Sound("igMainMenuOptionCheckBoxOn"); NextClass(); end);
	classButton:SetPoint("TOPRIGHT", hero, "TOPRIGHT", -10, -10);
	hero.chips = {};
	local cw = math.floor((W - 40 - 24 - 5 * 4) / 6);
	for i = 1, table.getn(CHIPS) do
		local c = CreateFrame("Frame", nil, hero);
		c:SetWidth(cw);
		c:SetHeight(32);
		c:SetPoint("BOTTOMLEFT", hero, "BOTTOMLEFT", 12 + (i - 1) * (cw + 4), 8);
		Card(c);
		c.icon = c:CreateTexture(nil, "ARTWORK");
		c.icon:SetWidth(20);
		c.icon:SetHeight(20);
		c.icon:SetPoint("LEFT", c, "LEFT", 6, 0);
		SetIcon(c.icon, I(CHIPS[i][2]));
		c.value = c:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
		c.value:SetPoint("TOPLEFT", c.icon, "TOPRIGHT", 4, 1);
		c.label = c:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
		c.label:SetPoint("BOTTOMLEFT", c.icon, "BOTTOMRIGHT", 4, -1);
		c.label:SetText(CHIPS[i][3]);
		hero.chips[i] = c;
	end

	-- the list: the game's scroll frame and scroll bar
	local ok;
	scroll, ok = Templated("ScrollFrame", "AZSStatsScroll", frame, "UIPanelScrollFrameTemplate");
	scroll:SetPoint("TOPLEFT", hero, "BOTTOMLEFT", 2, -8);
	scroll:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -40, 24);
	child = CreateFrame("Frame", nil, scroll);
	child:SetWidth(CW);
	child:SetHeight(10);
	scroll:SetScrollChild(child);
	if ( not ok ) then                                  -- no template: the mouse wheel still scrolls
		scroll:EnableMouseWheel(true);
		scroll:SetScript("OnMouseWheel", function()
			local max = math.max(0, child:GetHeight() - this:GetHeight());
			this:SetVerticalScroll(math.max(0, math.min(max, this:GetVerticalScroll() - arg1 * 40)));
		end);
	end

	-- the character sheet's tabs, along the bottom edge
	tabButtons = {};
	local templated = true;
	for i = 1, table.getn(TABS) do
		local index = i;
		local b, tok = Templated("Button", "AZSStatsFrameTab" .. i, frame, "CharacterFrameTabButtonTemplate");
		templated = templated and tok;
		if ( not tok ) then
			b:Hide();
			b = Button(frame, TABS[i], 78, nil);
		end
		b:SetID(i);
		b:SetText(TABS[i]);
		b:SetScript("OnClick", function() Sound("igCharacterInfoTab"); tab = index; Refresh(); end);
		if ( i == 1 ) then b:SetPoint("TOPLEFT", frame, "BOTTOMLEFT", 12, tok and 8 or -2);
		else b:SetPoint("LEFT", tabButtons[i - 1], "RIGHT", tok and -14 or 2, 0); end
		if ( tok and PanelTemplates_TabResize ) then pcall(PanelTemplates_TabResize, 0, b); end
		tabButtons[i] = b;
	end
	if ( templated and PanelTemplates_SetNumTabs ) then
		PanelTemplates_SetNumTabs(frame, table.getn(TABS));
		frame.selectedTab = 1;
		PanelTemplates_UpdateTabs(frame);
	end
end

function AZSStats_Toggle()
	if ( not frame ) then
		local ok, err = pcall(Build);
		if ( not ok ) then S().Print("|cffff4040the stats window could not be made:|r " .. tostring(err)); frame = nil; return; end
	end
	if ( frame:IsShown() ) then frame:Hide(); else frame:Show(); end
end

SLASH_AZSSTATSUI1 = "/azs";
SlashCmdList["AZSSTATSUI"] = function() AZSStats_Toggle(); end

-- ---------------------------------------------------------------- the minimap button
-- The tracking-button look: a round icon in the minimap's gold ring. Click: the window. Drag: move it around the
-- minimap (the angle is saved). Its tooltip shows your runs and best results at a glance.
local mini;

-- around the minimap when the game shows one; otherwise (or before it is laid out) a free spot, top right
local function OnMinimap()
	return Minimap and Minimap.IsVisible and Minimap:IsVisible() and Minimap:GetCenter() ~= nil;
end

local function MiniPlace()
	local m = DB().minimap;
	mini:ClearAllPoints();
	if ( OnMinimap() ) then
		local a = math.rad(Num(m.angle));
		mini:SetPoint("CENTER", Minimap, "CENTER", 80 * math.cos(a), 80 * math.sin(a));
	else
		mini:SetPoint("CENTER", UIParent, "TOPRIGHT", m.x ~= nil and Num(m.x) or -60, m.y ~= nil and Num(m.y) or -60);
	end
end

local function MiniTooltip()
	GameTooltip:SetOwner(this, "ANCHOR_LEFT");
	GameTooltip:AddLine("AZS Stats");
	local db = DB();
	local t = db.totals;
	GameTooltip:AddDoubleLine("Runs", Big(t.runs) .. "  (" .. Pct(t.wins, t.runs) .. " won)", 1, 1, 1, 1, 1, 1);
	local b = db.best;
	if ( type(b.dps) == "table" ) then GameTooltip:AddDoubleLine("Highest DPS", Big(b.dps.v), 1, 1, 1, 1, 0.5, 0); end
	if ( type(b.time) == "table" ) then GameTooltip:AddDoubleLine("Longest run", Clock(b.time.v), 1, 1, 1, 1, 1, 1); end
	if ( type(b.lvl) == "table" ) then GameTooltip:AddDoubleLine("Highest level", Big(b.lvl.v), 1, 1, 1, 1, 1, 1); end
	GameTooltip:AddDoubleLine("Time played", Hours(db.play.seconds), 1, 1, 1, 1, 1, 1);
	GameTooltip:AddLine(" ");
	GameTooltip:AddLine("Click: your stats. Right-click: the live DPS tracker.", 0.6, 0.6, 0.6);
	GameTooltip:AddLine("Drag to move this button.", 0.6, 0.6, 0.6);
	GameTooltip:Show();
end

-- only what a 1.12 client has: a plain Button on UIParent (so it shows even when the minimap is hidden)
local function MiniBuild()
	if ( mini ) then MiniPlace(); return; end
	local db = DB();
	if ( type(db.minimap) ~= "table" ) then db.minimap = { angle = 200, hide = false }; end
	mini = CreateFrame("Button", "AZSStatsMinimapButton", UIParent);
	mini:SetWidth(31);
	mini:SetHeight(31);
	mini:SetFrameStrata("MEDIUM");
	mini:SetFrameLevel(8);
	mini:SetMovable(true);
	local icon = mini:CreateTexture(nil, "BACKGROUND");
	icon:SetTexture("Interface\\Icons\\INV_Misc_Book_09");
	icon:SetTexCoord(0.07, 0.93, 0.07, 0.93);
	icon:SetWidth(20);
	icon:SetHeight(20);
	icon:SetPoint("TOPLEFT", mini, "TOPLEFT", 6, -5);
	local border = mini:CreateTexture(nil, "OVERLAY");
	border:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder");
	border:SetWidth(53);
	border:SetHeight(53);
	border:SetPoint("TOPLEFT", mini, "TOPLEFT", 0, 0);
	mini:SetHighlightTexture("Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight");
	mini:RegisterForClicks("LeftButtonUp", "RightButtonUp");
	mini:RegisterForDrag("LeftButton");
	mini:SetScript("OnClick", function()
		if ( arg1 == "RightButton" ) then AZSStats_ToggleTracker(); else AZSStats_Toggle(); end
	end);
	mini:SetScript("OnEnter", MiniTooltip);
	mini:SetScript("OnLeave", function() GameTooltip:Hide(); end);
	mini:SetScript("OnDragStart", function()
		if ( OnMinimap() ) then
			this:SetScript("OnUpdate", function()
				local x, y = GetCursorPosition();
				local s = Minimap:GetEffectiveScale();
				local cx, cy = Minimap:GetCenter();
				DB().minimap.angle = math.deg(math.atan2(y / s - cy, x / s - cx));
				MiniPlace();
			end);
		else
			this:StartMoving();
		end
	end);
	mini:SetScript("OnDragStop", function()
		this:SetScript("OnUpdate", nil);
		this:StopMovingOrSizing();
		if ( not OnMinimap() ) then              -- remember the free spot (from the top right corner)
			local x, y = this:GetCenter();
			DB().minimap.x, DB().minimap.y = x - UIParent:GetRight(), y - UIParent:GetTop();
			MiniPlace();
		end
	end);
	MiniPlace();
	if ( db.minimap.hide ) then mini:Hide(); else mini:Show(); end
end

local miniError = false;
local function MiniSafe()
	local ok, err = pcall(MiniBuild);
	if ( not ok and not miniError ) then
		miniError = true;
		S().Print("|cffff4040the minimap button could not be made:|r " .. tostring(err));
	end
end

function AZSStats_ToggleMinimapButton()
	MiniSafe();
	if ( not mini ) then return; end
	local m = DB().minimap;
	m.hide = not m.hide;
	if ( m.hide ) then mini:Hide(); S().Print("the minimap button is hidden. /azstats minimap brings it back.");
	else mini:Show(); S().Print("the minimap button is back."); end
end

local loader = CreateFrame("Frame");
loader:RegisterEvent("VARIABLES_LOADED");
loader:RegisterEvent("PLAYER_ENTERING_WORLD");
loader:SetScript("OnEvent", function() MiniSafe(); end);   -- again when entering the world: the minimap may show by then

-- ---------------------------------------------------------------- the live DPS tracker
-- A small frame during a run: your DPS on the game's damage meter, against your best for this class ("82% of your
-- record"), with a bar. Passing the record plays the level-up chime and says so in chat (once a run, and only after
-- the first minute, when the meter has settled). Drag it anywhere; /azstats tracker (or right-click the minimap
-- button) turns it off and on.
local tracker, trackerElapsed, trackerPassed, trackerLastTime = nil, 0, false, 0;

local function PlayerClassKey()
	if ( not UnitClass ) then return nil; end
	local _, token = UnitClass("player");
	if ( not token ) then return nil; end
	return string.lower(token);
end

local function ClassRecord(key)
	local db = DB();
	if ( key and type(db.classes[key]) == "table" and type(db.classes[key].best) == "table" ) then
		local b = db.classes[key].best.dps;
		if ( type(b) == "table" ) then return Num(b.v), db.classes[key].name; end
	end
	return 0, nil;
end

local function TrackerUpdate()
	if ( not AZS_GetRunTime or not AZSStats_CurrentDPS ) then tracker:Hide(); return; end
	local t = Num(AZS_GetRunTime());
	if ( t <= 0 ) then tracker.box:Hide(); return; end
	if ( t + 5 < trackerLastTime ) then trackerPassed = false; end      -- a new run
	trackerLastTime = t;
	local dps = AZSStats_CurrentDPS();
	local key = PlayerClassKey();
	local rec, cname = ClassRecord(key);
	tracker.box:Show();
	tracker.dps:SetText(Big(math.floor(dps + 0.5)) .. " DPS");
	if ( rec > 0 ) then
		local pct = dps / rec;
		tracker.rec:SetText(math.floor(pct * 100 + 0.5) .. "% of your " .. (cname or "class") .. " record (" .. Big(rec) .. ")");
		tracker.bar:SetValue(math.min(1, pct));
		if ( pct >= 1 ) then tracker.bar:SetStatusBarColor(1, 0.5, 0); else tracker.bar:SetStatusBarColor(0.95, 0.76, 0.3); end
		if ( pct > 1 and t >= 60 and not trackerPassed ) then
			trackerPassed = true;
			if ( PlaySound ) then if ( not pcall(PlaySound, "LEVELUPSOUND") ) then pcall(PlaySound, "QUESTCOMPLETED"); end end
			S().Print("|cffff8000You just passed your " .. (cname or "class") .. " DPS record of " .. Big(rec) .. "!|r");
		end
	else
		tracker.rec:SetText("No " .. ((cname or "class") ~= "class" and cname or "DPS") .. " record yet: finish a run to set one");
		tracker.bar:SetValue(0);
	end
end

local function TrackerBuild()
	if ( tracker ) then return; end
	tracker = CreateFrame("Frame", "AZSStatsTracker", UIParent);
	tracker:SetWidth(210);
	tracker:SetHeight(52);
	local db = DB();
	if ( type(db.tracker) ~= "table" ) then db.tracker = { show = true, x = 0, y = -150 }; end
	tracker:SetPoint("CENTER", UIParent, "CENTER", Num(db.tracker.x), Num(db.tracker.y));
	tracker:SetMovable(true);
	tracker:EnableMouse(true);
	tracker:RegisterForDrag("LeftButton");
	tracker:SetScript("OnDragStart", function() this:StartMoving(); end);
	tracker:SetScript("OnDragStop", function()
		this:StopMovingOrSizing();
		local cx, cy = this:GetCenter();
		local ux, uy = UIParent:GetCenter();
		DB().tracker.x, DB().tracker.y = cx - ux, cy - uy;
	end);
	local box = CreateFrame("Frame", nil, tracker);
	box:SetAllPoints(tracker);
	box:SetBackdrop({ bgFile = "Interface\\Tooltips\\UI-Tooltip-Background", edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border",
	                  tile = true, tileSize = 16, edgeSize = 14, insets = { left = 4, right = 4, top = 4, bottom = 4 } });
	box:SetBackdropColor(0.05, 0.05, 0.08, 0.85);
	box:SetBackdropBorderColor(0.8, 0.65, 0.3);
	tracker.box = box;
	local dps = box:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	dps:SetPoint("TOPLEFT", box, "TOPLEFT", 10, -8);
	tracker.dps = dps;
	local rec = box:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
	rec:SetPoint("TOPLEFT", dps, "BOTTOMLEFT", 0, -2);
	rec:SetWidth(192);
	rec:SetJustifyH("LEFT");
	tracker.rec = rec;
	local bar = CreateFrame("StatusBar", nil, box);
	bar:SetStatusBarTexture("Interface\\TargetingFrame\\UI-StatusBar");
	bar:SetMinMaxValues(0, 1);
	bar:SetWidth(190);
	bar:SetHeight(5);
	bar:SetPoint("BOTTOMLEFT", box, "BOTTOMLEFT", 10, 7);
	tracker.bar = bar;
	tracker:SetScript("OnUpdate", function()
		trackerElapsed = trackerElapsed + (arg1 or 0);
		if ( trackerElapsed < 0.5 ) then return; end
		trackerElapsed = 0;
		TrackerUpdate();
	end);
	box:Hide();
	if ( not db.tracker.show ) then tracker:Hide(); end
end

function AZSStats_ToggleTracker()
	TrackerBuild();
	local t = DB().tracker;
	t.show = not t.show;
	if ( t.show ) then tracker:Show(); S().Print("the live DPS tracker is on."); else tracker:Hide(); S().Print("the live DPS tracker is off. /azstats tracker turns it back on."); end
end

local trackerLoader = CreateFrame("Frame");
trackerLoader:RegisterEvent("VARIABLES_LOADED");
trackerLoader:SetScript("OnEvent", function()
	local ok, err = pcall(TrackerBuild);
	if ( not ok ) then S().Print("|cffff4040the live DPS tracker could not be made:|r " .. tostring(err)); end
end);
