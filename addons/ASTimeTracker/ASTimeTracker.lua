-- AS Time Tracker: keeps a record of every Azeroth Survivors run.
--
-- When a run ends (the hero fell, won or left), the game sends the run's results as the AZSEND addon
-- message (the same table AZS_GetRunSummary() returns). This addon adds each run to its totals, personal
-- bests, per class / race / zone records and a history of the last runs, all kept in ASTimeTrackerDB
-- (saved with your addon settings in benilla-config\saved). The launcher's STATS page reads that file.
--
-- In game: /azstats (or /astime) shows your totals, /azstats last your last run, /azstats best your records,
-- /azstats quiet turns the end-of-run chat line off or on, /azstats reset yes clears everything.
--
-- Lua 5.0 (as the 1.12 client): no _G, no # operator, table.getn, event handlers read event / arg1 / arg2.

ASTIMETRACKER_VERSION = 1;
ASTIMETRACKER_HISTORY = 100;                -- runs kept in the history (the totals keep everything)

local SUM_FIELDS = { "time", "kills", "elites", "guardians", "bosses", "dmg", "taken", "gold", "chests", "magnets", "levels" };
-- personal bests: the record's name and the run field it comes from
local BEST_FIELDS = { { "time", "t" }, { "lvl", "lvl" }, { "kills", "kills" }, { "dmg", "dmg" }, { "gold", "gold" },
                      { "bosses", "bosses" }, { "elites", "elites" }, { "guardians", "guardians" }, { "endl", "endl" } };

local function Print(msg)
	if ( DEFAULT_CHAT_FRAME ) then
		DEFAULT_CHAT_FRAME:AddMessage("|cffffd100AS Time Tracker:|r " .. msg);
	end
end

local function Now()
	if ( date ) then
		local ok, s = pcall(date, "%Y-%m-%d %H:%M");
		if ( ok and s ) then
			return s;
		end
	end
	return "";
end

local function Num(v)
	v = tonumber(v);
	if ( not v ) then
		return 0;
	end
	return v;
end

local function Str(v)
	if ( v == nil ) then
		return "";
	end
	return tostring(v);
end

-- 754 -> "12:34", 4000 -> "1:06:40"
local function Clock(sec)
	sec = math.floor(Num(sec));
	local h = math.floor(sec / 3600);
	local m = math.floor(math.mod(sec, 3600) / 60);
	local s = math.mod(sec, 60);
	if ( h > 0 ) then
		return string.format("%d:%02d:%02d", h, m, s);
	end
	return string.format("%d:%02d", m, s);
end

-- 1234567 -> "1,234,567"
local function Big(n)
	local s = string.format("%d", math.floor(Num(n)));
	local out = "";
	while ( string.len(s) > 3 ) do
		out = "," .. string.sub(s, -3) .. out;
		s = string.sub(s, 1, -4);
	end
	return s .. out;
end

-- "victory", "defeat", "left" (and "endless": the run went on past the final boss, a win)
local function Kind(result)
	local r = string.lower(Str(result));
	if ( r == "victory" or r == "won" or r == "win" or r == "endless" ) then
		return "victory";
	elseif ( r == "defeat" or r == "dead" or r == "fell" or r == "died" or r == "lost" ) then
		return "defeat";
	elseif ( r == "left" or r == "quit" or r == "abandoned" ) then
		return "left";
	end
	return "other";
end

local function Fresh()
	local db = { version = ASTIMETRACKER_VERSION, quiet = false, lastSig = "", first = "", last = "",
	             totals = { runs = 0, victory = 0, defeat = 0, left = 0, other = 0 },
	             best = {}, classes = {}, races = {}, zones = {}, results = {}, history = {} };
	for i = 1, table.getn(SUM_FIELDS) do
		db.totals[SUM_FIELDS[i]] = 0;
	end
	return db;
end

-- fills in anything an older or damaged ASTimeTrackerDB is missing
local function Check()
	if ( type(ASTimeTrackerDB) ~= "table" ) then
		ASTimeTrackerDB = Fresh();
		return;
	end
	local fresh = Fresh();
	for k, v in pairs(fresh) do
		if ( type(ASTimeTrackerDB[k]) ~= type(v) ) then
			ASTimeTrackerDB[k] = v;
		end
	end
	for k, v in pairs(fresh.totals) do
		if ( type(ASTimeTrackerDB.totals[k]) ~= "number" ) then
			ASTimeTrackerDB.totals[k] = v;
		end
	end
	ASTimeTrackerDB.version = ASTIMETRACKER_VERSION;
end

-- "result=defeat;t=754;lvl=23;..." -> table (numbers as numbers)
local function Parse(msg)
	local t = {};
	local pos = 1;
	while ( true ) do
		local s, e, k, v = string.find(msg, "(%w+)=([^;]*)", pos);
		if ( not s ) then
			break;
		end
		t[k] = tonumber(v) or v;
		pos = e + 1;
	end
	return t;
end

-- one run's fingerprint: the same summary seen twice (the message and the API, or a /reload on the end
-- screen) is only counted once
local function Signature(s)
	return Str(s.result) .. "|" .. Str(s.t) .. "|" .. Str(s.lvl) .. "|" .. Str(s.kills) .. "|" .. Str(s.dmg) .. "|" ..
	       Str(s.taken) .. "|" .. Str(s.gold) .. "|" .. Str(s.zone) .. "|" .. Str(s.class) .. "|" .. Str(s.hero);
end

local function Group(list, key, name)
	key = Str(key);
	if ( key == "" ) then
		key = "unknown";
	end
	local g = list[key];
	if ( type(g) ~= "table" ) then
		g = { name = "", runs = 0, wins = 0, time = 0, kills = 0, bestTime = 0, bestLvl = 0 };
		list[key] = g;
	end
	if ( Str(name) ~= "" ) then
		g.name = Str(name);
	end
	return g;
end

function ASTimeTracker_Record(s)
	if ( type(s) ~= "table" or not s.result ) then
		return;
	end
	Check();
	local db = ASTimeTrackerDB;
	local sig = Signature(s);
	if ( sig == db.lastSig ) then
		return;
	end
	db.lastSig = sig;

	local when = Now();
	local kind = Kind(s.result);
	local run = { result = Str(s.result), kind = kind, date = when, t = Num(s.t), lvl = Num(s.lvl), kills = Num(s.kills),
	              elites = Num(s.elites), guardians = Num(s.guardians), bosses = Num(s.bosses), dmg = Num(s.dmg),
	              taken = Num(s.taken), gold = Num(s.gold), chests = Num(s.chests), magnets = Num(s.magnets),
	              endl = Num(s.endl), hero = Str(s.hero), class = Str(s.class), cname = Str(s.cname), race = Str(s.race),
	              rname = Str(s.rname), zone = Str(s.zone), zname = Str(s.zname) };
	if ( run.cname == "" ) then run.cname = run.class; end
	if ( run.rname == "" ) then run.rname = run.race; end
	if ( run.zname == "" ) then run.zname = run.zone; end

	-- totals
	local tot = db.totals;
	tot.runs = tot.runs + 1;
	tot[kind] = (tot[kind] or 0) + 1;
	tot.time = tot.time + run.t;
	tot.levels = tot.levels + run.lvl;
	for i = 1, table.getn(SUM_FIELDS) do
		local f = SUM_FIELDS[i];
		if ( f ~= "time" and f ~= "levels" ) then
			tot[f] = tot[f] + run[f];
		end
	end
	local rk = string.lower(run.result);
	db.results[rk] = (db.results[rk] or 0) + 1;
	if ( db.first == "" ) then
		db.first = when;
	end
	db.last = when;

	-- personal bests
	local records = {};
	for i = 1, table.getn(BEST_FIELDS) do
		local name, field = BEST_FIELDS[i][1], BEST_FIELDS[i][2];
		local v = run[field];
		local old = db.best[name];
		if ( v > 0 and (type(old) ~= "table" or v > Num(old.v)) ) then
			db.best[name] = { v = v, hero = run.hero, cname = run.cname, rname = run.rname, zname = run.zname, date = when };
			if ( type(old) == "table" and Num(old.v) > 0 ) then
				table.insert(records, name);
			end
		end
	end
	if ( kind == "victory" ) then
		local fast = db.best.fastestWin;
		if ( run.t > 0 and (type(fast) ~= "table" or run.t < Num(fast.v)) ) then
			db.best.fastestWin = { v = run.t, hero = run.hero, cname = run.cname, rname = run.rname, zname = run.zname, date = when };
		end
	end

	-- per class, race and zone
	local win = 0;
	if ( kind == "victory" ) then
		win = 1;
	end
	local groups = { Group(db.classes, run.class, run.cname), Group(db.races, run.race, run.rname), Group(db.zones, run.zone, run.zname) };
	for i = 1, 3 do
		local g = groups[i];
		g.runs = g.runs + 1;
		g.wins = g.wins + win;
		g.time = g.time + run.t;
		g.kills = g.kills + run.kills;
		if ( run.t > g.bestTime ) then g.bestTime = run.t; end
		if ( run.lvl > g.bestLvl ) then g.bestLvl = run.lvl; end
	end

	-- history, the newest last
	table.insert(db.history, run);
	while ( table.getn(db.history) > ASTIMETRACKER_HISTORY ) do
		table.remove(db.history, 1);
	end

	if ( not db.quiet ) then
		local line = "run " .. Big(tot.runs) .. " recorded: " .. run.result .. " after " .. Clock(run.t) .. ", level " ..
		             run.lvl .. ", " .. Big(run.kills) .. " kills.";
		if ( table.getn(records) > 0 ) then
			line = line .. " |cff40ff40New record: " .. table.concat(records, ", ") .. "!|r";
		end
		Print(line);
	end
end

local function ShowTotals()
	Check();
	local t = ASTimeTrackerDB.totals;
	if ( t.runs == 0 ) then
		Print("no runs recorded yet. Finish a run and it shows up here and on the launcher's STATS page.");
		return;
	end
	Print(Big(t.runs) .. " runs: " .. t.victory .. " won, " .. t.defeat .. " fell, " .. t.left .. " left. Time in runs " ..
	      Clock(t.time) .. ".");
	Print(Big(t.kills) .. " kills (" .. Big(t.elites) .. " elites, " .. Big(t.guardians) .. " guardians, " .. Big(t.bosses) ..
	      " bosses), " .. Big(t.dmg) .. " damage dealt, " .. Big(t.gold) .. " gold.");
end

local function ShowBest()
	Check();
	local b = ASTimeTrackerDB.best;
	local names = { { "time", "Longest run", true }, { "lvl", "Highest level" }, { "kills", "Most kills" },
	                { "dmg", "Most damage" }, { "gold", "Most gold" }, { "bosses", "Most bosses" }, { "fastestWin", "Fastest win", true } };
	local any = false;
	for i = 1, table.getn(names) do
		local r = b[names[i][1]];
		if ( type(r) == "table" ) then
			local v = Big(r.v);
			if ( names[i][3] ) then v = Clock(r.v); end
			Print(names[i][2] .. ": " .. v .. " (" .. Str(r.cname) .. ", " .. Str(r.zname) .. ", " .. Str(r.date) .. ")");
			any = true;
		end
	end
	if ( not any ) then
		Print("no records yet.");
	end
end

local function ShowLast()
	Check();
	local h = ASTimeTrackerDB.history;
	local r = h[table.getn(h)];
	if ( not r ) then
		Print("no runs recorded yet.");
		return;
	end
	Print("last run (" .. r.date .. "): " .. r.result .. " as " .. r.cname .. " in " .. r.zname .. " after " .. Clock(r.t) ..
	      ", level " .. r.lvl .. ", " .. Big(r.kills) .. " kills, " .. Big(r.dmg) .. " damage, " .. Big(r.gold) .. " gold.");
end

SLASH_ASTIMETRACKER1 = "/azstats";
SLASH_ASTIMETRACKER2 = "/astime";
SlashCmdList["ASTIMETRACKER"] = function(msg)
	local cmd = string.lower(Str(msg));
	cmd = string.gsub(cmd, "^%s+", "");
	cmd = string.gsub(cmd, "%s+$", "");
	if ( cmd == "" ) then
		ShowTotals();
	elseif ( cmd == "last" ) then
		ShowLast();
	elseif ( cmd == "best" ) then
		ShowBest();
	elseif ( cmd == "quiet" ) then
		Check();
		ASTimeTrackerDB.quiet = not ASTimeTrackerDB.quiet;
		if ( ASTimeTrackerDB.quiet ) then
			Print("the end-of-run line is off.");
		else
			Print("the end-of-run line is on.");
		end
	elseif ( cmd == "reset yes" ) then
		local last = "";
		if ( type(ASTimeTrackerDB) == "table" ) then
			last = Str(ASTimeTrackerDB.lastSig);
		end
		ASTimeTrackerDB = Fresh();
		ASTimeTrackerDB.lastSig = last;             -- the run on the end screen is not counted again
		Print("all stats cleared.");
	elseif ( cmd == "reset" ) then
		Print("type /azstats reset yes to clear every stat (this cannot be undone).");
	else
		Print("/azstats, /azstats last, /azstats best, /azstats quiet, /azstats reset");
	end
end

-- the run's end arrives as an addon message; AZS_GetRunSummary() is checked too, now and then, in case
-- the message came before this addon was listening (each run is counted once either way)
local frame = CreateFrame("Frame", "ASTimeTrackerFrame");
local waited = 0;
frame:RegisterEvent("VARIABLES_LOADED");
frame:RegisterEvent("CHAT_MSG_ADDON");
frame:SetScript("OnEvent", function()
	if ( event == "VARIABLES_LOADED" ) then
		Check();
	elseif ( event == "CHAT_MSG_ADDON" and arg1 == "AZSEND" and arg2 ) then
		ASTimeTracker_Record(Parse(arg2));
	end
end);
frame:SetScript("OnUpdate", function()
	waited = waited + (arg1 or 0);
	if ( waited < 2 ) then
		return;
	end
	waited = 0;
	if ( AZS_GetRunSummary and ASTimeTrackerDB ) then
		local s = AZS_GetRunSummary();
		if ( s ) then
			ASTimeTracker_Record(s);
		end
	end
end);
