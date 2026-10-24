-- AZS Stats: keeps a record of every Azeroth Survivors run, your play time, who kills you and who you play with.
-- (It replaces "AS Time Tracker" and "Azeroth Stats"; the launcher carries their records over.)
--
-- When a run ends (the hero fell, won or left), the game sends the run's results as the AZSEND addon
-- message (the same table AZS_GetRunSummary() returns). This addon adds each run to:
--   * the global totals and personal bests, and the same for each class on its own
--   * the abilities taken in each run (spells and ranks from AZS_SPELLS, their damage from AZS_METER)
--   * how often each class was picked, month by month; per race and per zone records; a history of runs
--   * Nemesis: a death log (the killing blow and the hits before it, from the combat log; the biggest damage
--     sources from AZS_TAKEN) and who killed you how often
--   * Co-op: who you played with (AZS_PARTY and your party), how often, your wins and records together
-- and it counts your play time: the time the game is running, by session (a /reload is not a new session).
-- Everything is kept in AZSStatsDB (saved with your addon settings in benilla-config\saved, which the
-- launcher's cloud save uploads). The launcher's STATS page reads that file; in game, /azs opens the same
-- stats in a window (AZSStatsUI.lua).
--
-- Chat commands: /azstats (or /astime) your totals, /azstats last, best, classes, deaths, nemesis, coop,
-- /azstats quiet turns the end-of-run chat lines off or on, /azstats reset yes clears everything.
--
-- Lua 5.0 (as the 1.12 client): no _G, no # operator, table.getn, event handlers read event / arg1 / arg2.

AZSSTATS_VERSION = 4;
AZSSTATS_DEATHS = 50;                 -- deaths kept in the death log
AZSSTATS_HISTORY = 100;               -- runs kept in the history (the totals keep everything)
AZSSTATS_RUN_ABILITIES = 12;          -- abilities kept per run in the history

-- the numbers added up for every run (globally and per class); "time" comes from the run's t
local SUM_FIELDS = { "time", "kills", "elites", "guardians", "bosses", "dmg", "taken", "gold", "chests", "magnets", "levels" };
local COUNT_FIELDS = { "runs", "victory", "defeat", "left", "other", "wins", "bestTime", "bestLvl" };
-- personal bests: the record's name and the run field it comes from
local BEST_FIELDS = { { "time", "t" }, { "lvl", "lvl" }, { "kills", "kills" }, { "dmg", "dmg" }, { "gold", "gold" },
                      { "bosses", "bosses" }, { "elites", "elites" }, { "guardians", "guardians" }, { "endl", "endl" } };

local function Print(msg)
	if ( DEFAULT_CHAT_FRAME ) then
		DEFAULT_CHAT_FRAME:AddMessage("|cffffd100AZS Stats:|r " .. msg);
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

-- "shadow_bolt" -> "Shadow Bolt"
local function Pretty(key)
	local s = string.gsub(Str(key), "_", " ");
	s = string.gsub(s, "(%a)([%w']*)", function(a, b) return string.upper(a) .. b; end);
	return s;
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

-- a block of totals: used for everything together and for each class
local function FillBlock(b)
	for i = 1, table.getn(COUNT_FIELDS) do
		if ( type(b[COUNT_FIELDS[i]]) ~= "number" ) then b[COUNT_FIELDS[i]] = 0; end
	end
	for i = 1, table.getn(SUM_FIELDS) do
		if ( type(b[SUM_FIELDS[i]]) ~= "number" ) then b[SUM_FIELDS[i]] = 0; end
	end
	return b;
end

local function Fresh()
	return { version = AZSSTATS_VERSION, quiet = false, lastSig = "", first = "", last = "",
	         totals = FillBlock({}), best = {}, classes = {}, races = {}, zones = {}, abilities = {}, months = {},
	         results = {}, history = {}, deaths = {}, nemesis = {},
	         play = { seconds = 0, sessions = 0, longest = 0, last = 0, lastDate = "", seen = 0 },
	         coop = { runs = 0, wins = 0, time = 0, kills = 0, bosses = 0, solo = 0, soloWins = 0, best = {}, friends = {} } };
end

local function Month(when)
	if ( string.len(Str(when)) >= 7 ) then
		return string.sub(when, 1, 7);
	end
	return "unknown";
end

-- a class / race / zone entry, made when first seen
local function Group(list, key, name)
	key = Str(key);
	if ( key == "" ) then
		key = "unknown";
	end
	local g = list[key];
	if ( type(g) ~= "table" ) then
		g = { name = "" };
		list[key] = g;
	end
	FillBlock(g);
	if ( type(g.best) ~= "table" ) then g.best = {}; end
	if ( type(g.abilities) ~= "table" ) then g.abilities = {}; end
	if ( type(g.nemesis) ~= "table" ) then g.nemesis = {}; end
	if ( Str(name) ~= "" ) then
		g.name = Str(name);
	end
	return g;
end

-- adds one run to a block of totals
local function Accumulate(b, run)
	FillBlock(b);
	b.runs = b.runs + 1;
	b[run.kind] = (b[run.kind] or 0) + 1;
	if ( run.kind == "victory" ) then
		b.wins = b.wins + 1;
	end
	b.time = b.time + run.t;
	b.levels = b.levels + run.lvl;
	for i = 1, table.getn(SUM_FIELDS) do
		local f = SUM_FIELDS[i];
		if ( f ~= "time" and f ~= "levels" ) then
			b[f] = b[f] + Num(run[f]);
		end
	end
	if ( run.t > b.bestTime ) then b.bestTime = run.t; end
	if ( run.lvl > b.bestLvl ) then b.bestLvl = run.lvl; end
end

-- updates a table of personal bests; returns the names of records that were beaten
local function Bests(best, run)
	local beaten = {};
	for i = 1, table.getn(BEST_FIELDS) do
		local name, field = BEST_FIELDS[i][1], BEST_FIELDS[i][2];
		local v = Num(run[field]);
		local old = best[name];
		if ( v > 0 and (type(old) ~= "table" or v > Num(old.v)) ) then
			best[name] = { v = v, hero = run.hero, cname = run.cname, rname = run.rname, zname = run.zname, date = run.date };
			if ( type(old) == "table" and Num(old.v) > 0 ) then
				table.insert(beaten, name);
			end
		end
	end
	if ( run.kind == "victory" and run.t > 0 ) then
		local fast = best.fastestWin;
		if ( type(fast) ~= "table" or run.t < Num(fast.v) ) then
			best.fastestWin = { v = run.t, hero = run.hero, cname = run.cname, rname = run.rname, zname = run.zname, date = run.date };
		end
	end
	return beaten;
end

-- adds a run's abilities to a list of abilities (global or one class's)
local function AddAbilities(list, run)
	if ( type(run.abilities) ~= "table" ) then
		return;
	end
	for i = 1, table.getn(run.abilities) do
		local a = run.abilities[i];
		local e = list[a.key];
		if ( type(e) ~= "table" ) then
			e = { name = a.name, picks = 0, wins = 0, dmg = 0, bestRank = 0, ranks = 0 };
			list[a.key] = e;
		end
		e.name = a.name;
		e.picks = Num(e.picks) + 1;
		if ( run.kind == "victory" ) then
			e.wins = Num(e.wins) + 1;
		end
		e.dmg = Num(e.dmg) + Num(a.dmg);
		e.ranks = Num(e.ranks) + Num(a.rank);
		if ( Num(a.rank) > Num(e.bestRank) ) then
			e.bestRank = Num(a.rank);
		end
	end
end

local function AddMonth(db, run)
	local m = Month(run.date);
	local e = db.months[m];
	if ( type(e) ~= "table" ) then
		e = { runs = 0, classes = {} };
		db.months[m] = e;
	end
	e.runs = Num(e.runs) + 1;
	local c = run.class;
	if ( c == "" ) then
		c = "unknown";
	end
	e.classes[c] = Num(e.classes[c]) + 1;
end

-- fills in anything an older or damaged AZSStatsDB is missing; version 1 kept less per class and
-- nothing per month: both are rebuilt from the history (complete while it holds every run)
local function Check()
	if ( type(AZSStatsDB) ~= "table" ) then
		AZSStatsDB = Fresh();
		return;
	end
	local db = AZSStatsDB;
	local fresh = Fresh();
	for k, v in pairs(fresh) do
		if ( type(db[k]) ~= type(v) ) then
			db[k] = v;
		end
	end
	FillBlock(db.totals);
	for k, v in pairs(fresh.play) do
		if ( type(db.play[k]) ~= type(v) ) then db.play[k] = v; end
	end
	for k, v in pairs(fresh.coop) do
		if ( type(db.coop[k]) ~= type(v) ) then db.coop[k] = v; end
	end
	if ( db.totals.wins == 0 and db.totals.victory > 0 ) then
		db.totals.wins = db.totals.victory;
	end
	if ( Num(db.version) < 2 ) then
		local history = db.history;
		if ( table.getn(history) >= db.totals.runs ) then
			local names = {};
			for k, g in pairs(db.classes) do
				if ( type(g) == "table" ) then names[k] = g.name; end
			end
			db.classes = {};
			for i = 1, table.getn(history) do
				local run = history[i];
				run.kind = run.kind or Kind(run.result);
				run.t = Num(run.t);
				run.lvl = Num(run.lvl);
				local g = Group(db.classes, run.class, run.cname ~= "" and run.cname or names[run.class]);
				Accumulate(g, run);
				Bests(g.best, run);
			end
		end
		db.months = {};
		for i = 1, table.getn(history) do
			AddMonth(db, history[i]);
		end
	end
	for k, g in pairs(db.classes) do
		if ( type(g) == "table" ) then Group(db.classes, k, nil); end
	end
	db.version = AZSSTATS_VERSION;
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

-- ---------------------------------------------------------------- the build of the run going on
-- The spells and the damage meter are copied every few seconds during a run, so the build is known at the
-- end even if the game has already cleared them when the run's summary arrives.
local lastSpells, lastMeter, lastTaken, lastParty, lastRunTime = nil, nil, nil, nil, 0;

-- ---------------------------------------------------------------- Nemesis: who hits you, who kills you
-- The last hits you took (from the combat log), newest last: { src, spell, amt, at }.
local hits = {};

local HIT_PATTERNS = {
	{ "^(.+)'s (.+) crits you for (%d+)", true }, { "^(.+)'s (.+) hits you for (%d+)", true },
	{ "^(.+) crits you for (%d+)", false }, { "^(.+) hits you for (%d+)", false },
	{ "^You suffer (%d+) .- damage from (.+)'s (.+)%.", "dot" } };

local function OnHit(msg)
	if ( type(msg) ~= "string" ) then
		return;
	end
	for i = 1, table.getn(HIT_PATTERNS) do
		local p = HIT_PATTERNS[i];
		local _, _, a, b, c = string.find(msg, p[1]);
		if ( a ) then
			local h;
			if ( p[2] == "dot" ) then h = { src = b, spell = c, amt = tonumber(a) or 0 };
			elseif ( p[2] ) then h = { src = a, spell = b, amt = tonumber(c) or 0 };
			else h = { src = a, spell = "", amt = tonumber(b) or 0 }; end
			h.at = GetTime();
			table.insert(hits, h);
			while ( table.getn(hits) > 8 ) do table.remove(hits, 1); end
			return;
		end
	end
end

local function CopyList(list, fields)
	if ( type(list) ~= "table" or table.getn(list) == 0 ) then
		return nil;
	end
	local out = {};
	for i = 1, table.getn(list) do
		local e = list[i];
		if ( type(e) == "table" ) then
			local c = {};
			for j = 1, table.getn(fields) do
				c[fields[j]] = e[fields[j]];
			end
			table.insert(out, c);
		end
	end
	return out;
end

local function Snapshot()
	local now = 0;
	if ( AZS_GetRunTime ) then
		now = Num(AZS_GetRunTime());
	end
	if ( now + 1 < lastRunTime ) then           -- a new run started
		lastSpells, lastMeter, lastTaken, lastParty = nil, nil, nil, nil;
		hits = {};
	end
	lastRunTime = now;
	local tk = CopyList(AZS_TAKEN, { "source", "dmg" });
	if ( tk ) then lastTaken = tk; end
	local pt = CopyList(AZS_PARTY, { "name", "class", "dmg", "top" });
	if ( pt ) then lastParty = pt; end
	local s = CopyList(AZS_SPELLS, { "key", "spell", "rank" });
	if ( s ) then lastSpells = s; end
	local m = CopyList(AZS_METER, { "name", "dmg" });
	if ( m ) then lastMeter = m; end
end

-- the run's abilities: { key, name, rank, dmg }, the most damage first
local function Abilities()
	Snapshot();
	local spells = lastSpells or {};
	local meter = lastMeter or {};
	local out = {};
	for i = 1, table.getn(spells) do
		local e = spells[i];
		local key = Str(e.key);
		if ( key == "" ) then key = Str(e.spell); end
		if ( key ~= "" ) then
			local name = Str(e.spell);
			if ( name == "" or tonumber(name) ) then
				name = Pretty(key);
			end
			local dmg = 0;
			for j = 1, table.getn(meter) do
				local mn = string.lower(Str(meter[j].name));
				if ( mn == string.lower(name) or mn == string.lower(Pretty(key)) ) then
					dmg = dmg + Num(meter[j].dmg);
				end
			end
			table.insert(out, { key = key, name = name, rank = Num(e.rank), dmg = dmg });
		end
	end
	table.sort(out, function(a, b) return a.dmg > b.dmg; end);
	while ( table.getn(out) > AZSSTATS_RUN_ABILITIES ) do
		table.remove(out);
	end
	return out;
end

-- the friends in a run: the party meter's heroes and your party, without yourself: { name, class, dmg }
local function PartyOf(run)
	local out, seen = {}, {};
	local me = string.lower(Str(run.hero));
	local player = "";
	if ( UnitName ) then player = string.lower(Str(UnitName("player"))); end
	local function add(name, class, dmg)
		name = Str(name);
		if ( name == "" or name == "Unknown" ) then return; end
		local l = string.lower(name);
		if ( l == me or l == player ) then return; end
		if ( seen[l] ) then
			if ( Str(class) ~= "" and seen[l].class == "" ) then seen[l].class = Str(class); end
			return;
		end
		local f = { name = name, class = Str(class), dmg = Num(dmg) };
		seen[l] = f;
		table.insert(out, f);
	end
	local list = lastParty or CopyList(AZS_PARTY, { "name", "class", "dmg", "top" }) or {};
	for i = 1, table.getn(list) do add(list[i].name, list[i].class, list[i].dmg); end
	if ( GetNumPartyMembers ) then
		local ok, n = pcall(GetNumPartyMembers);
		if ( ok and n ) then
			for i = 1, n do add(UnitName("party" .. i), "", 0); end
		end
	end
	return out;
end

-- ---------------------------------------------------------------- recording a run
function AZSStats_Record(s)
	if ( type(s) ~= "table" or not s.result ) then
		return;
	end
	Check();
	local db = AZSStatsDB;
	local sig = Signature(s);
	if ( sig == db.lastSig ) then
		return;
	end
	db.lastSig = sig;

	local when = Now();
	local run = { result = Str(s.result), kind = Kind(s.result), date = when, t = Num(s.t), lvl = Num(s.lvl),
	              kills = Num(s.kills), elites = Num(s.elites), guardians = Num(s.guardians), bosses = Num(s.bosses),
	              dmg = Num(s.dmg), taken = Num(s.taken), gold = Num(s.gold), chests = Num(s.chests),
	              magnets = Num(s.magnets), endl = Num(s.endl), hero = Str(s.hero), class = Str(s.class),
	              cname = Str(s.cname), race = Str(s.race), rname = Str(s.rname), zone = Str(s.zone), zname = Str(s.zname) };
	if ( run.cname == "" ) then run.cname = Pretty(run.class); end
	if ( run.rname == "" ) then run.rname = Pretty(run.race); end
	if ( run.zname == "" ) then run.zname = Pretty(run.zone); end
	run.abilities = Abilities();

	-- everything together
	Accumulate(db.totals, run);
	local rk = string.lower(run.result);
	db.results[rk] = Num(db.results[rk]) + 1;
	if ( db.first == "" ) then
		db.first = when;
	end
	db.last = when;
	local records = Bests(db.best, run);
	AddAbilities(db.abilities, run);
	AddMonth(db, run);

	-- the class on its own, and the race and zone
	local class = Group(db.classes, run.class, run.cname);
	Accumulate(class, run);
	local classRecords = Bests(class.best, run);
	AddAbilities(class.abilities, run);
	Accumulate(Group(db.races, run.race, run.rname), run);
	Accumulate(Group(db.zones, run.zone, run.zname), run);

	-- a death: who did it, how, and the hits before it
	if ( run.kind == "defeat" ) then
		local d = { date = when, hero = run.hero, class = run.class, cname = run.cname, rname = run.rname, zname = run.zname,
		            t = run.t, lvl = run.lvl, killer = "", spell = "", amt = 0, last = "", top = "" };
		local now = GetTime();
		local recent = {};
		for i = 1, table.getn(hits) do
			if ( now - hits[i].at < 20 ) then table.insert(recent, hits[i]); end
		end
		local k = recent[table.getn(recent)];
		if ( k ) then d.killer = k.src; d.spell = k.spell; d.amt = k.amt; end
		local parts = {};
		for i = table.getn(recent), math.max(1, table.getn(recent) - 4), -1 do
			local h = recent[i];
			local p = h.src;
			if ( h.spell ~= "" ) then p = p .. " (" .. h.spell .. ")"; end
			table.insert(parts, p .. " " .. Big(h.amt));
		end
		d.last = table.concat(parts, ", ");
		local taken = lastTaken or {};
		table.sort(taken, function(a, b) return Num(a.dmg) > Num(b.dmg); end);
		local tops = {};
		for i = 1, math.min(3, table.getn(taken)) do
			table.insert(tops, Str(taken[i].source) .. " " .. Big(taken[i].dmg));
		end
		d.top = table.concat(tops, ", ");
		if ( d.killer == "" and taken[1] ) then d.killer = Str(taken[1].source); end
		if ( d.killer == "" ) then d.killer = "Unknown"; end
		table.insert(db.deaths, d);
		while ( table.getn(db.deaths) > AZSSTATS_DEATHS ) do table.remove(db.deaths, 1); end
		for _, list in ipairs({ db.nemesis, class.nemesis }) do
			local n = list[d.killer];
			if ( type(n) ~= "table" ) then n = { kills = 0, dmg = 0, last = "" }; list[d.killer] = n; end
			n.kills = Num(n.kills) + 1;
			n.dmg = Num(n.dmg) + d.amt;
			n.last = when;
		end
		run.killer = d.killer;
	end
	hits = {};
	lastTaken = nil;

	-- co-op: the friends in this run
	local party = PartyOf(run);
	local co = db.coop;
	local coopNews = {};
	if ( table.getn(party) > 0 ) then
		local names = {};
		for i = 1, table.getn(party) do table.insert(names, party[i].name); end
		run.party = table.concat(names, ", ");
		co.runs = co.runs + 1;
		co.time = co.time + run.t;
		co.kills = co.kills + run.kills;
		co.bosses = co.bosses + run.bosses;
		if ( run.kind == "victory" ) then co.wins = co.wins + 1; end
		Bests(co.best, run);
		for k, v in pairs(co.best) do
			if ( type(v) == "table" and v.date == when and v.with == nil ) then v.with = run.party; end
		end
		for i = 1, table.getn(party) do
			local f = party[i];
			local e = co.friends[f.name];
			if ( type(e) ~= "table" ) then
				e = { runs = 0, wins = 0, time = 0, kills = 0, bosses = 0, dmg = 0, bestTime = 0, bestLvl = 0, mostBosses = 0,
				      fastestWin = 0, first = when, last = "", firstWin = "", class = "", cname = "", classes = {} };
				co.friends[f.name] = e;
			end
			e.runs = Num(e.runs) + 1;
			e.time = Num(e.time) + run.t;
			e.kills = Num(e.kills) + run.kills;
			e.bosses = Num(e.bosses) + run.bosses;
			e.dmg = Num(e.dmg) + Num(f.dmg);
			e.last = when;
			if ( f.class ~= "" ) then
				e.class = f.class;
				e.cname = Pretty(f.class);
				e.classes[f.class] = Num(e.classes[f.class]) + 1;
			end
			if ( run.t > Num(e.bestTime) ) then e.bestTime = run.t; end
			if ( run.lvl > Num(e.bestLvl) ) then e.bestLvl = run.lvl; end
			if ( run.bosses > Num(e.mostBosses) ) then e.mostBosses = run.bosses; end
			if ( run.kind == "victory" ) then
				e.wins = Num(e.wins) + 1;
				if ( Str(e.firstWin) == "" ) then
					e.firstWin = when;
					table.insert(coopNews, "First victory together with " .. f.name .. "!");
				end
				if ( Num(e.fastestWin) == 0 or run.t < Num(e.fastestWin) ) then e.fastestWin = run.t; end
			end
			local steps = { 10, 25, 50, 100, 250, 500, 1000 };
			for j = 1, table.getn(steps) do
				if ( e.runs == steps[j] ) then table.insert(coopNews, e.runs .. " runs together with " .. f.name .. "!"); end
			end
		end
	else
		co.solo = Num(co.solo) + 1;
		if ( run.kind == "victory" ) then co.soloWins = Num(co.soloWins) + 1; end
	end
	lastParty = nil;

	-- history, the newest last
	table.insert(db.history, run);
	while ( table.getn(db.history) > AZSSTATS_HISTORY ) do
		table.remove(db.history, 1);
	end
	lastSpells, lastMeter = nil, nil;

	if ( not db.quiet ) then
		local line = "run " .. Big(db.totals.runs) .. " recorded: " .. run.result .. " after " .. Clock(run.t) .. ", level " ..
		             run.lvl .. ", " .. Big(run.kills) .. " kills.";
		if ( table.getn(records) > 0 ) then
			line = line .. " |cff40ff40New record: " .. table.concat(records, ", ") .. "!|r";
		elseif ( table.getn(classRecords) > 0 ) then
			line = line .. " |cff40ff40New " .. run.cname .. " record: " .. table.concat(classRecords, ", ") .. "!|r";
		end
		Print(line);
		for i = 1, table.getn(coopNews) do Print("|cff40c0ff" .. coopNews[i] .. "|r"); end
		if ( run.killer ) then
			local n = db.nemesis[run.killer];
			local d = db.deaths[table.getn(db.deaths)];
			local how = "";
			if ( d.spell ~= "" ) then how = " with " .. d.spell; end
			if ( d.amt > 0 ) then how = how .. " (" .. Big(d.amt) .. ")"; end
			Print("|cffff4040Slain by " .. run.killer .. how .. ".|r It has killed you " .. Big(n.kills) .. " time(s). /azstats nemesis");
		end
	end
end

-- ---------------------------------------------------------------- chat commands
local function ShowTotals()
	Check();
	local t = AZSStatsDB.totals;
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
	local b = AZSStatsDB.best;
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

local function ShowClasses()
	Check();
	local db = AZSStatsDB;
	local list = {};
	for k, g in pairs(db.classes) do
		if ( type(g) == "table" and g.runs > 0 ) then table.insert(list, g); end
	end
	if ( table.getn(list) == 0 ) then
		Print("no runs recorded yet.");
		return;
	end
	table.sort(list, function(a, b) return a.runs > b.runs; end);
	for i = 1, table.getn(list) do
		local g = list[i];
		Print(g.name .. ": " .. g.runs .. " runs (" .. math.floor(100 * g.runs / db.totals.runs + 0.5) .. "%), " ..
		      g.victory .. " won, " .. Big(g.kills) .. " kills, longest " .. Clock(g.bestTime) .. ".");
	end
end

local function ShowDeaths()
	Check();
	local d = AZSStatsDB.deaths;
	if ( table.getn(d) == 0 ) then Print("no deaths recorded yet."); return; end
	Print("your last deaths:");
	for i = table.getn(d), math.max(1, table.getn(d) - 4), -1 do
		local e = d[i];
		Print(e.date .. ": |cffff4040" .. e.killer .. "|r" .. (e.spell ~= "" and (" (" .. e.spell .. " " .. Big(e.amt) .. ")") or "") ..
		      ", " .. e.cname .. " level " .. e.lvl .. " in " .. e.zname .. " after " .. Clock(e.t) .. ".");
		if ( e.last ~= "" ) then Print("   last hits: " .. e.last); end
	end
end

local function ShowNemesis()
	Check();
	local list = {};
	for name, n in pairs(AZSStatsDB.nemesis) do table.insert(list, { name = name, kills = Num(n.kills) }); end
	if ( table.getn(list) == 0 ) then Print("no one has killed you yet."); return; end
	table.sort(list, function(a, b) return a.kills > b.kills; end);
	Print("your nemesis: |cffff4040" .. list[1].name .. "|r, " .. Big(list[1].kills) .. " kill(s).");
	for i = 2, math.min(5, table.getn(list)) do Print("   " .. list[i].name .. ": " .. Big(list[i].kills)); end
end

local function ShowCoop()
	Check();
	local co = AZSStatsDB.coop;
	if ( co.runs == 0 ) then Print("no co-op runs recorded yet."); return; end
	Print(Big(co.runs) .. " co-op runs, " .. Big(co.wins) .. " won (" .. math.floor(100 * co.wins / co.runs + 0.5) ..
	      "%); solo " .. Big(co.solo) .. " runs, " .. Big(co.soloWins) .. " won.");
	local list = {};
	for name, e in pairs(co.friends) do table.insert(list, { name = name, e = e }); end
	table.sort(list, function(a, b) return Num(a.e.runs) > Num(b.e.runs); end);
	for i = 1, math.min(5, table.getn(list)) do
		local e = list[i].e;
		Print("   " .. list[i].name .. ": " .. Big(e.runs) .. " runs together, " .. Big(e.wins) .. " won, last " .. Str(e.last) .. ".");
	end
end

local function ShowLast()
	Check();
	local h = AZSStatsDB.history;
	local r = h[table.getn(h)];
	if ( not r ) then
		Print("no runs recorded yet.");
		return;
	end
	Print("last run (" .. r.date .. "): " .. r.result .. " as " .. r.cname .. " in " .. r.zname .. " after " .. Clock(r.t) ..
	      ", level " .. r.lvl .. ", " .. Big(r.kills) .. " kills, " .. Big(r.dmg) .. " damage, " .. Big(r.gold) .. " gold.");
	if ( type(r.abilities) == "table" and table.getn(r.abilities) > 0 ) then
		local parts = {};
		for i = 1, table.getn(r.abilities) do
			local a = r.abilities[i];
			local p = a.name;
			if ( Num(a.rank) > 0 ) then p = p .. " " .. a.rank; end
			table.insert(parts, p);
		end
		Print("abilities: " .. table.concat(parts, ", ") .. ".");
	end
end

SLASH_AZSSTATS1 = "/azstats";
SLASH_AZSSTATS2 = "/astime";
SlashCmdList["AZSSTATS"] = function(msg)
	local cmd = string.lower(Str(msg));
	cmd = string.gsub(cmd, "^%s+", "");
	cmd = string.gsub(cmd, "%s+$", "");
	if ( cmd == "" ) then
		ShowTotals();
	elseif ( cmd == "last" ) then
		ShowLast();
	elseif ( cmd == "best" ) then
		ShowBest();
	elseif ( cmd == "deaths" or cmd == "death" ) then
		ShowDeaths();
	elseif ( cmd == "coop" or cmd == "co-op" or cmd == "friends" ) then
		ShowCoop();
	elseif ( cmd == "window" or cmd == "show" ) then
		if ( AZSStats_Toggle ) then AZSStats_Toggle(); end
	elseif ( cmd == "nemesis" ) then
		ShowNemesis();
	elseif ( cmd == "classes" or cmd == "class" ) then
		ShowClasses();
	elseif ( cmd == "quiet" ) then
		Check();
		AZSStatsDB.quiet = not AZSStatsDB.quiet;
		if ( AZSStatsDB.quiet ) then
			Print("the end-of-run line is off.");
		else
			Print("the end-of-run line is on.");
		end
	elseif ( cmd == "reset yes" ) then
		local last = "";
		if ( type(AZSStatsDB) == "table" ) then
			last = Str(AZSStatsDB.lastSig);
		end
		AZSStatsDB = Fresh();
		AZSStatsDB.lastSig = last;             -- the run on the end screen is not counted again
		Print("all stats cleared.");
	elseif ( cmd == "reset" ) then
		Print("type /azstats reset yes to clear every stat (this cannot be undone).");
	else
		Print("/azstats, /azstats last, /azstats best, /azstats classes, /azstats deaths, /azstats nemesis, /azstats coop, /azstats quiet; /azs opens the window, /azstats reset");
	end
end

-- the run's end arrives as an addon message; AZS_GetRunSummary() is checked too, now and then, in case
-- the message came before this addon was listening (each run is counted once either way)
local frame = CreateFrame("Frame", "AZSStatsEventFrame");
local waited = 0;
frame:RegisterEvent("VARIABLES_LOADED");
frame:RegisterEvent("CHAT_MSG_ADDON");
frame:RegisterEvent("CHAT_MSG_COMBAT_CREATURE_VS_SELF_HITS");
frame:RegisterEvent("CHAT_MSG_SPELL_CREATURE_VS_SELF_DAMAGE");
frame:RegisterEvent("CHAT_MSG_SPELL_PERIODIC_SELF_DAMAGE");
frame:RegisterEvent("CHAT_MSG_COMBAT_HOSTILEPLAYER_HITS");
frame:RegisterEvent("CHAT_MSG_SPELL_HOSTILEPLAYER_DAMAGE");
-- play time: the time the game runs, added every frame. A session starts when the interface loads, unless the
-- game was seen less than two minutes ago (a /reload, not a new start).
local session, playing = 0, false;

local function Clock0()
	if ( time ) then
		local ok, t = pcall(time);
		if ( ok and t ) then return t; end
	end
	return 0;
end

local function StartSession()
	Check();
	local p = AZSStatsDB.play;
	local now = Clock0();
	if ( now > 0 and Num(p.seen) > 0 and now - Num(p.seen) < 120 ) then
		session = Num(p.last);                     -- a /reload: the same session goes on
	else
		p.sessions = Num(p.sessions) + 1;
		p.last = 0;
		p.lastDate = Now();
		session = 0;
	end
	p.seen = now;
	playing = true;
end

frame:SetScript("OnEvent", function()
	if ( event == "VARIABLES_LOADED" ) then
		StartSession();
	elseif ( event == "CHAT_MSG_ADDON" ) then
		if ( arg1 == "AZSEND" and arg2 ) then AZSStats_Record(Parse(arg2)); end
	else
		OnHit(arg1);
	end
end);
local seenTick = 0;
frame:SetScript("OnUpdate", function()
	if ( playing and AZSStatsDB and arg1 and arg1 > 0 and arg1 < 10 ) then
		local p = AZSStatsDB.play;
		session = session + arg1;
		p.seconds = Num(p.seconds) + arg1;
		p.last = session;
		if ( session > Num(p.longest) ) then p.longest = session; end
		seenTick = seenTick + arg1;
		if ( seenTick > 20 ) then seenTick = 0; p.seen = Clock0(); end
	end
	waited = waited + (arg1 or 0);
	if ( waited < 2 ) then
		return;
	end
	waited = 0;
	if ( not AZSStatsDB ) then
		return;
	end
	-- the last summary stays readable after its run (until the next one ends), so the build is copied every
	-- time; recording an already counted summary again does nothing
	Snapshot();
	if ( AZS_GetRunSummary ) then
		local s = AZS_GetRunSummary();
		if ( s ) then
			AZSStats_Record(s);
		end
	end
end);

-- for the window (AZSStatsUI.lua)
AZSStats = { Check = Check, Big = Big, Clock = Clock, Pretty = Pretty, Kind = Kind, Print = Print };
