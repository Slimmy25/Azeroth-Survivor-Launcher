-- AZS Stats, live: what happens during a run, for the live tracker (AZSStatsUI.lua) and the run's record.
--
-- Pace: every minute of a run, your damage so far (from the game's damage meter) and the monsters that died are
-- written down. When a run sets a new DPS record for its class, those minute marks are kept as that class's pace
-- (AZSStatsDB.classes[class].pace). In the next runs the tracker compares you, second by second and minute by
-- minute, with that record run: ahead or behind, and by how much. A class without a pace yet is compared with its
-- record DPS (record DPS x time).
--
-- Highlights, read from the combat log: your highest hit, highest crit, highest heal and the most monsters that
-- died within 1 second and within 5 seconds. Each run keeps its own (run.hi in the history); your best ever, and per
-- class, are kept in AZSStatsDB.highs and AZSStatsDB.classes[class].highs. Beating one says so in chat at once.
--
-- Tooltips: whenever the game shows the tooltip of one of your abilities (hover a spell on the action bar, a level-up
-- card or anywhere else), its text is written down in AZSStatsDB.tooltips under the spell's name, so the launcher's
-- New Player Guide can show what the game itself says the spell does.
--
-- Menus: while you pick a level-up card or an item (in co-op the fight goes on around you, and the game keeps you
-- safe with Divine Shield), kills are not counted for "most kills in 1 / 5 seconds".
--
-- Level-up saves: a level-up while your health was at 15% or less (it heals you) counts as a save.
-- Legendaries: every legendary item you collect (a legendary amulet or piece of gear: its name in orange, or the
-- word "legendary" in its notice), and the most in one run.
-- Co-op: the time you are down is kept, so the DPS counter stops while you are down (AZSStats_CurrentDPS).
--
-- Lua 5.0 / 1.12: event handlers read event and arg1; no # operator.

local function Num(v) v = tonumber(v); return v or 0; end

local HIGHS = { "crit", "hit", "heal", "burst1", "burst5", "legendary", "saves" };
local HIGH_NAMES = { crit = "highest crit", hit = "highest normal hit", heal = "highest heal",
                     burst1 = "most kills in 1 second", burst5 = "most kills in 5 seconds",
                     legendary = "most legendaries in a run", saves = "most level-up saves in a run" };

-- the run going on
local cur, prev;
local lastT = 0;

local function NewRun()
	return { t = 0, dmg = 0, kills = 0, marks = { dmg = {}, kills = {} }, hi = {}, announced = {}, deaths = {},
	         legendary = 0, legSeen = {}, saves = 0, dead = 0, coopDeaths = 0 };
end
cur = NewRun();

local function MeterDamage()
	if ( type(AZS_METER) ~= "table" ) then return 0; end
	local d = 0;
	for i = 1, table.getn(AZS_METER) do
		if ( type(AZS_METER[i]) == "table" ) then d = d + Num(AZS_METER[i].dmg); end
	end
	return d;
end

local function ClassKey()
	if ( not UnitClass ) then return nil; end
	local _, token = UnitClass("player");
	return token and string.lower(token) or nil;
end

local function Print(msg)
	if ( AZSStats and AZSStats.Print ) then AZSStats.Print(msg);
	elseif ( DEFAULT_CHAT_FRAME ) then DEFAULT_CHAT_FRAME:AddMessage("|cffffd100AZS Stats|r " .. msg); end
end

local function Big(n)
	if ( AZSStats and AZSStats.Big ) then return AZSStats.Big(n); end
	return tostring(math.floor(Num(n) + 0.5));
end

-- your best ever for a highlight (overall), or 0
local function BestEver(key)
	if ( type(AZSStatsDB) ~= "table" or type(AZSStatsDB.highs) ~= "table" ) then return 0; end
	local h = AZSStatsDB.highs[key];
	return type(h) == "table" and Num(h.v) or 0;
end

-- a highlight of this run went up: kept, and announced once a run when it beats your best ever
local function Raise(key, v, spell)
	if ( Num(lastT) <= 0 ) then return; end                 -- only during a run
	local h = cur.hi[key];
	if ( type(h) == "table" and Num(h.v) >= v ) then return; end
	cur.hi[key] = { v = v, spell = spell or "" };
	local best = BestEver(key);
	if ( best > 0 and v > best and not cur.announced[key] ) then
		cur.announced[key] = true;
		Print("|cffff8000New " .. HIGH_NAMES[key] .. ": " .. Big(v) .. ((spell or "") ~= "" and (" (" .. spell .. ")") or "") ..
		      "!|r Your best was " .. Big(best) .. ".");
	end
end

-- ---- the combat log: your hits, crits and heals, and every death
local OUT = {
	{ "^Your (.-) crits .+ for (%d+)", "crit", 1, 2 }, { "^Your (.-) hits .+ for (%d+)", "hit", 1, 2 },
	{ "^You crit .+ for (%d+)", "crit", nil, 1 }, { "^You hit .+ for (%d+)", "hit", nil, 1 },
	{ "^.+ suffers (%d+) .- damage from your (.+)%.", "hit", 2, 1 },
	{ "^Your (.-) critically heals .+ for (%d+)", "heal", 1, 2 }, { "^Your (.-) heals .+ for (%d+)", "heal", 1, 2 },
	{ "^You gain (%d+) health from your (.+)%.", "heal", 2, 1 }, { "^You gain (%d+) health from (.+)%.", "heal", 2, 1 } };

local function OnCombat(msg)
	if ( type(msg) ~= "string" ) then return; end
	for i = 1, table.getn(OUT) do
		local p = OUT[i];
		local r = { string.find(msg, p[1]) };
		if ( r[1] ) then
			local spell = p[3] and r[2 + p[3]] or "Attack";
			local v = tonumber(r[2 + p[4]]) or 0;
			if ( v > 0 ) then Raise(p[2], v, spell); end    -- "hit": the biggest hit that was not a crit
			return;
		end
	end
end

-- ---- in a menu (a level-up card or an item to pick): the phase says so, or Divine Shield is on you
local menuUntil = 0;
local function InMenu()
	if ( AZS_GetPhase ) then
		local ok, ph = pcall(AZS_GetPhase);
		if ( ok and (ph == "choosing" or ph == "paused") ) then return true; end
	end
	if ( UnitBuff ) then
		for i = 1, 32 do
			local tex = UnitBuff("player", i);
			if ( not tex ) then break; end
			if ( string.find(string.lower(tex), "divineintervention", 1, true) ) then return true; end   -- Divine Shield's icon
		end
	end
	-- AZS_BUFFS holds spell ids ({ spell, left }): Divine Shield is 642 (rank 1) or 1020 (rank 2)
	if ( type(AZS_BUFFS) == "table" ) then
		for i = 1, table.getn(AZS_BUFFS) do
			local b = AZS_BUFFS[i];
			local id = type(b) == "table" and tonumber(b.spell) or nil;
			if ( id == 642 or id == 1020 ) then return true; end
		end
	end
	return false;
end

local function OnDeath(msg)
	if ( type(msg) ~= "string" or Num(lastT) <= 0 ) then return; end
	if ( InMenu() or GetTime() < menuUntil ) then         -- picking a card: the kills around you are not yours to burst
		menuUntil = math.max(menuUntil, GetTime() + 1);
		cur.deaths = {};
		return;
	end
	if ( not string.find(msg, " dies%.$") and not string.find(msg, "^You have slain ") ) then return; end
	local now = GetTime();
	cur.kills = cur.kills + 1;
	table.insert(cur.deaths, now);
	while ( cur.deaths[1] and now - cur.deaths[1] > 5 ) do table.remove(cur.deaths, 1); end
	local in1 = 0;
	for i = 1, table.getn(cur.deaths) do if ( now - cur.deaths[i] <= 1 ) then in1 = in1 + 1; end end
	if ( in1 > 1 ) then Raise("burst1", in1); end
	if ( table.getn(cur.deaths) > 1 ) then Raise("burst5", table.getn(cur.deaths)); end
end

-- ---- legendaries collected: an orange (legendary) item name, or "legendary" in the notice
local function Legendary(text, name)
	if ( Num(lastT) <= 0 or type(text) ~= "string" ) then return; end
	local low = string.lower(text);
	if ( not string.find(low, "ff8000", 1, true) and not string.find(low, "legendary", 1, true) ) then return; end
	local key = name;
	if ( not key or key == "" ) then
		local _, _, n = string.find(text, "%[(.-)%]");
		key = n or text;
	end
	if ( cur.legSeen[key] ) then return; end
	cur.legSeen[key] = true;
	cur.legendary = cur.legendary + 1;
	Raise("legendary", cur.legendary);
end

if ( AZS_RegisterBannerHandler ) then
	pcall(AZS_RegisterBannerHandler, function(b)
		if ( type(b) == "table" ) then Legendary(tostring(b.text or "") .. " " .. tostring(b.name or ""), b.name); end
	end);
end

-- ---- level-up saves: your lowest health in the last moment before a level-up
local lowHp, lowAt, lastLevel = 1, 0, nil;
local function WatchHealth()
	-- the run's own health (AZS_RUN.hp / hpm), else the unit's
	local h, m;
	if ( type(AZS_RUN) == "table" and Num(AZS_RUN.hpm) > 0 ) then h, m = Num(AZS_RUN.hp), Num(AZS_RUN.hpm);
	elseif ( UnitHealth and UnitHealthMax ) then h, m = UnitHealth("player"), UnitHealthMax("player"); end
	if ( not m or m <= 0 ) then return; end
	local r = h / m;
	local now = GetTime();
	if ( r < lowHp or now - lowAt > 1.5 ) then lowHp, lowAt = r, now; end
end

local function LevelUp()
	if ( Num(lastT) <= 0 ) then return; end
	if ( lowHp > 0 and lowHp <= 0.15 and GetTime() - lowAt <= 1.5 ) then
		cur.saves = cur.saves + 1;
		Raise("saves", cur.saves);
		lowHp = 1;
	end
end

-- ---- co-op: the time you are down (the DPS counter stops), and each time you go down
local downNow, downSince = false, 0;
local function DownNow()
	local inParty = type(AZS_PARTY) == "table" and table.getn(AZS_PARTY) > 0;
	if ( not inParty ) then return false; end
	if ( type(AZS_REVIVE) == "table" and Num(AZS_REVIVE.down) == 1 ) then return true; end
	return UnitIsDeadOrGhost and UnitIsDeadOrGhost("player") and true or false;
end

function AZSStats_LiveCoopDown()
	cur.coopDeaths = cur.coopDeaths + 1;
end

-- what AZSStats_Record keeps of this run: legendaries, level-up saves, the time down and co-op deaths
function AZSStats_LiveCounts(run)
	local r = cur;
	if ( prev and run and math.abs(Num(prev.t) - Num(run.t)) < math.abs(Num(cur.t) - Num(run.t)) ) then r = prev; end
	return { legendary = r.legendary, saves = r.saves, dead = r.dead, coopDeaths = r.coopDeaths };
end

-- for the DPS counter: the seconds spent down in co-op this run, and whether you are down now
function AZSStats_LiveDead()
	return cur.dead, downNow;
end

-- ---- the minute marks, and a new run
local function Tick()
	local t = AZS_GetRunTime and Num(AZS_GetRunTime()) or 0;
	if ( t < 15 and lastT - t > 10 ) then            -- the clock went back: a new run (as AZSStats.lua decides it)
		prev = cur;
		cur = NewRun();
	end
	lastT = t;
	if ( t <= 0 ) then return; end
	cur.t = t;
	cur.dmg = MeterDamage();
	local m = math.floor(t / 60);
	if ( m >= 1 and cur.marks.dmg[m] == nil ) then   -- the first look past each full minute
		cur.marks.dmg[m] = cur.dmg;
		cur.marks.kills[m] = cur.kills;
	end
end

local ev = CreateFrame("Frame");
for _, e in ipairs({ "CHAT_MSG_SPELL_SELF_DAMAGE", "CHAT_MSG_COMBAT_SELF_HITS", "CHAT_MSG_SPELL_PERIODIC_CREATURE_DAMAGE",
                     "CHAT_MSG_SPELL_SELF_BUFF", "CHAT_MSG_SPELL_PERIODIC_SELF_BUFFS", "CHAT_MSG_SPELL_FRIENDLYPLAYER_BUFF",
                     "CHAT_MSG_COMBAT_HOSTILE_DEATH" }) do
	pcall(ev.RegisterEvent, ev, e);
end
ev:SetScript("OnEvent", function()
	if ( event == "CHAT_MSG_COMBAT_HOSTILE_DEATH" ) then OnDeath(arg1);
	elseif ( event == "PLAYER_LEVEL_UP" ) then LevelUp();
	elseif ( event == "CHAT_MSG_LOOT" or event == "CHAT_MSG_SYSTEM" ) then Legendary(arg1);
	else OnCombat(arg1); end
end);
for _, e in ipairs({ "PLAYER_LEVEL_UP", "CHAT_MSG_LOOT", "CHAT_MSG_SYSTEM" }) do pcall(ev.RegisterEvent, ev, e); end
local acc, fast = 0, 0;
ev:SetScript("OnUpdate", function()
	local dt = arg1 or 0;
	fast = fast + dt;
	if ( fast >= 0.1 ) then
		fast = 0;
		pcall(WatchHealth);
		-- a level-up the event did not tell about: the level went up
		local lv = (type(AZS_RUN) == "table" and Num(AZS_RUN.lvl) > 0) and Num(AZS_RUN.lvl) or (UnitLevel and UnitLevel("player") or nil);
		if ( lv and lastLevel and lv > lastLevel ) then pcall(LevelUp); end
		lastLevel = lv;
		if ( InMenu() ) then menuUntil = GetTime() + 1; end
	end
	local d = DownNow();
	if ( d and Num(lastT) > 0 ) then cur.dead = cur.dead + dt; end
	downNow = d;
	acc = acc + dt;
	if ( acc < 1 ) then return; end
	acc = 0;
	Tick();
end);

-- ---- for the tracker
-- a pace's damage at second t: straight lines between its minute marks (0 at the start)
local function At(marks, t)
	if ( type(marks) ~= "table" ) then return nil; end
	local m = math.floor(t / 60);
	local a = m == 0 and 0 or marks[m];
	local b = marks[m + 1];
	if ( a == nil ) then return nil; end
	if ( b == nil ) then return nil; end              -- past the record run's end: the record DPS is used instead
	return Num(a) + (Num(b) - Num(a)) * (t - m * 60) / 60;
end

-- the class's pace: { dps, t, dmg = { [minute] = damage so far }, kills = {...}, date } or nil
local function Pace(key)
	if ( not key or type(AZSStatsDB) ~= "table" or type(AZSStatsDB.classes) ~= "table" ) then return nil; end
	local c = AZSStatsDB.classes[key];
	if ( type(c) == "table" and type(c.pace) == "table" and type(c.pace.dmg) == "table" ) then return c.pace; end
	return nil;
end

-- what the tracker shows about pace: (difference as a share, your damage, the record's damage at this moment, text)
function AZSStats_LivePace(recordDps)
	local t = cur.t;
	if ( t < 20 ) then return nil; end
	local p = Pace(ClassKey());
	local want, from = nil, "record run";
	if ( p ) then want = At(p.dmg, t); end
	if ( want == nil and Num(recordDps) > 0 ) then want = Num(recordDps) * t; from = "record DPS"; end
	if ( want == nil or want <= 0 ) then return nil; end
	local diff = (cur.dmg - want) / want;
	-- the minute just finished, against the record run's same minute
	local m = math.floor(t / 60);
	local split = nil;
	if ( p and m >= 1 and cur.marks.dmg[m] and p.dmg[m] ) then
		local mine = cur.marks.dmg[m] - (m > 1 and Num(cur.marks.dmg[m - 1]) or 0);
		local theirs = Num(p.dmg[m]) - (m > 1 and Num(p.dmg[m - 1]) or 0);
		split = { minute = m, mine = mine, theirs = theirs };
	end
	return { diff = diff, mine = cur.dmg, want = want, from = from, split = split, t = t };
end

-- this run's highlights so far, and your best ever: { key = { v, spell, best } }
function AZSStats_LiveHighs()
	local out = {};
	for i = 1, table.getn(HIGHS) do
		local k = HIGHS[i];
		local h = cur.hi[k];
		out[k] = { v = type(h) == "table" and Num(h.v) or 0, spell = type(h) == "table" and h.spell or "", best = BestEver(k) };
	end
	return out;
end

-- ---- when a run is recorded (AZSStats_Record calls this): its highlights and minute marks
local function Better(store, key, v, run, spell)
	if ( v <= 0 ) then return false; end
	local h = store[key];
	if ( type(h) == "table" and Num(h.v) >= v ) then return false; end
	store[key] = { v = v, spell = spell or "", date = run.date, cname = run.cname, zname = run.zname, hero = run.hero };
	return true;
end

function AZSStats_LiveRunEnded(db, run, classBlock, newClassDps)
	-- the live data of this run: the current one, or the one before when a new run has already begun
	local r = cur;
	if ( prev and math.abs(Num(prev.t) - Num(run.t)) < math.abs(Num(cur.t) - Num(run.t)) ) then r = prev; end
	if ( Num(r.t) <= 0 ) then return; end
	run.hi = {};
	if ( type(db.highs) ~= "table" ) then db.highs = {}; end
	if ( type(classBlock.highs) ~= "table" ) then classBlock.highs = {}; end
	for i = 1, table.getn(HIGHS) do
		local k = HIGHS[i];
		local h = r.hi[k];
		if ( type(h) == "table" and Num(h.v) > 0 ) then
			run.hi[k] = Num(h.v);
			Better(db.highs, k, Num(h.v), run, h.spell);
			Better(classBlock.highs, k, Num(h.v), run, h.spell);
		end
	end
	-- the class's pace: this run's minute marks, when it set a new DPS record (and lasted a minute or more)
	if ( newClassDps and r.marks.dmg[1] ) then
		local marks = { dmg = {}, kills = {} };
		for m, v in pairs(r.marks.dmg) do marks.dmg[m] = v; end
		for m, v in pairs(r.marks.kills) do marks.kills[m] = v; end
		local last = math.floor(Num(run.t) / 60) + 1;  -- where the run ended, so the line runs to the end
		if ( marks.dmg[last] == nil and Num(run.t) > 0 ) then
			local full = Num(r.dmg) > 0 and Num(r.dmg) or Num(run.dmg);
			marks.dmg[last] = full + (full / Num(run.t)) * (last * 60 - Num(run.t));
		end
		classBlock.pace = { dps = run.dps, t = run.t, dmg = marks.dmg, kills = marks.kills, date = run.date };
		Print("|cff40ff40This run is your new " .. (run.cname or "") .. " pace:|r the tracker compares your next runs with it, minute by minute.");
	end
	if ( r == cur ) then cur.hi = {}; cur.announced = {}; end
end

-- ---- the game's own tooltips of your abilities
local function TipKey(name)
	local k = string.lower(tostring(name or ""));
	k = string.gsub(k, "%s*%(.-%)%s*$", "");
	k = string.gsub(k, "%s+%d+$", "");
	k = string.gsub(k, "[^%a%d]", "");
	return k;
end

local lastTip = "";
local function CaptureTooltip()
	if ( not GameTooltip or not GameTooltip:IsVisible() or type(AZSStatsDB) ~= "table" ) then return; end
	local first = getglobal("GameTooltipTextLeft1");
	local name = first and first:GetText();
	if ( not name or name == "" ) then return; end
	local k = TipKey(name);
	if ( not AZSSTATS_SPELL_ICONS or not AZSSTATS_SPELL_ICONS[k] ) then return; end      -- only abilities
	local lines = {};
	local n = GameTooltip.NumLines and GameTooltip:NumLines() or 12;
	for i = 2, math.min(n, 20) do
		local l, r = getglobal("GameTooltipTextLeft" .. i), getglobal("GameTooltipTextRight" .. i);
		local lt = l and l:IsVisible() and l:GetText() or nil;
		local rt = r and r:IsVisible() and r:GetText() or nil;
		if ( lt and lt ~= "" ) then
			if ( rt and rt ~= "" ) then lt = lt .. "   " .. rt; end
			table.insert(lines, lt);
		end
	end
	if ( table.getn(lines) == 0 ) then return; end
	local text = table.concat(lines, "\n");
	if ( name .. text == lastTip ) then return; end
	lastTip = name .. text;
	if ( type(AZSStatsDB.tooltips) ~= "table" ) then AZSStatsDB.tooltips = {}; end
	local clean = string.gsub(name, "%s*%(.-%)%s*$", "");
	AZSStatsDB.tooltips[clean] = { text = text, title = name, date = date and date("%Y-%m-%d") or "" };
end

local tipFrame = CreateFrame("Frame");
local tipAcc = 0;
tipFrame:SetScript("OnUpdate", function()
	tipAcc = tipAcc + (arg1 or 0);
	if ( tipAcc < 0.25 ) then return; end
	tipAcc = 0;
	pcall(CaptureTooltip);
end);

-- ---- the armory, for the launcher's STATS > Armory: what each class's hero wears, and their numbers on the
-- character pane (AZS_STATS), written down whenever the gear changes and every half a minute
local ARMORY_SLOTS = { { 1, "Head" }, { 2, "Neck" }, { 3, "Shoulder" }, { 15, "Back" }, { 5, "Chest" }, { 4, "Shirt" },
                       { 19, "Tabard" }, { 9, "Wrist" }, { 10, "Hands" }, { 6, "Waist" }, { 7, "Legs" }, { 8, "Feet" },
                       { 11, "Finger" }, { 12, "Finger" }, { 13, "Trinket" }, { 14, "Trinket" }, { 16, "Main Hand" },
                       { 17, "Off Hand" }, { 18, "Ranged" } };
local ARMORY_STATS = { "hp", "dmg", "cd", "crit", "cm", "arm", "area", "cnt", "echo", "spd", "reg", "pick" };

local function CaptureArmory()
	if ( type(AZSStatsDB) ~= "table" or not GetInventoryItemLink ) then return; end
	local key = ClassKey();
	if ( not key ) then return; end
	local a = { hero = UnitName and UnitName("player") or "", race = UnitRace and UnitRace("player") or "",
	            level = UnitLevel and UnitLevel("player") or 0, cname = UnitClass and UnitClass("player") or "",
	            date = date and date("%Y-%m-%d %H:%M") or "", gear = {}, stats = {} };
	local any = false;
	for i = 1, table.getn(ARMORY_SLOTS) do
		local slot, label = ARMORY_SLOTS[i][1], ARMORY_SLOTS[i][2];
		local link = GetInventoryItemLink("player", slot);
		if ( link ) then
			local _, _, color = string.find(link, "|c%x%x(%x%x%x%x%x%x)");
			local _, _, name = string.find(link, "%[(.-)%]");
			local _, _, id = string.find(link, "item:(%d+)");
			local tex = GetInventoryItemTexture and GetInventoryItemTexture("player", slot) or "";
			tex = string.gsub(tostring(tex or ""), "^.*\\", "");
			table.insert(a.gear, { slot = label, name = name or "?", color = color or "ffffff", id = tonumber(id) or 0, icon = tex });
			any = true;
		end
	end
	if ( type(AZS_STATS) == "table" ) then
		for i = 1, table.getn(ARMORY_STATS) do
			local k = ARMORY_STATS[i];
			if ( tonumber(AZS_STATS[k]) ) then a.stats[k] = tonumber(AZS_STATS[k]); any = true; end
		end
	end
	if ( not any ) then return; end
	if ( type(AZSStatsDB.armory) ~= "table" ) then AZSStatsDB.armory = {}; end
	AZSStatsDB.armory[key] = a;
end

local armoryFrame = CreateFrame("Frame");
armoryFrame:RegisterEvent("PLAYER_ENTERING_WORLD");
armoryFrame:RegisterEvent("UNIT_INVENTORY_CHANGED");
armoryFrame:SetScript("OnEvent", function()
	if ( event == "UNIT_INVENTORY_CHANGED" and arg1 ~= "player" ) then return; end
	pcall(CaptureArmory);
end);
local armoryAcc = 0;
armoryFrame:SetScript("OnUpdate", function()
	armoryAcc = armoryAcc + (arg1 or 0);
	if ( armoryAcc < 30 ) then return; end
	armoryAcc = 0;
	pcall(CaptureArmory);
end);
