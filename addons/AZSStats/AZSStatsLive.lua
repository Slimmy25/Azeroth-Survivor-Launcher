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
-- Lua 5.0 / 1.12: event handlers read event and arg1; no # operator.

local function Num(v) v = tonumber(v); return v or 0; end

local HIGHS = { "crit", "hit", "heal", "burst1", "burst5" };
local HIGH_NAMES = { crit = "highest crit", hit = "highest normal hit", heal = "highest heal",
                     burst1 = "most kills in 1 second", burst5 = "most kills in 5 seconds" };

-- the run going on
local cur, prev;
local lastT = 0;

local function NewRun()
	return { t = 0, dmg = 0, kills = 0, marks = { dmg = {}, kills = {} }, hi = {}, announced = {}, deaths = {} };
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

local function OnDeath(msg)
	if ( type(msg) ~= "string" or Num(lastT) <= 0 ) then return; end
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
	if ( event == "CHAT_MSG_COMBAT_HOSTILE_DEATH" ) then OnDeath(arg1); else OnCombat(arg1); end
end);
local acc = 0;
ev:SetScript("OnUpdate", function()
	acc = acc + (arg1 or 0);
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
