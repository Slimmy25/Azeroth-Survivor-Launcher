-- AZS Stats in game: /azs, or the button on the minimap, opens a window with the same stats as the launcher's STATS
-- page, in tabs along its bottom like the character sheet's: Overview, Classes, Abilities, Records, Nemesis, Co-op and
-- History. CLASS picks every class or one class on its own (Overview, Abilities, Records, Nemesis and History follow
-- it). Long lists turn pages with the arrows or the mouse wheel. The window is made of the game's own interface art:
-- the dialog frame and header, your portrait, the character sheet's tabs, the spellbook's page arrows, spell and
-- class icons. The minimap button can be dragged around the minimap; /azstats minimap hides or shows it.
-- Lua 5.0 / 1.12 interface: scripts read `this`, `arg1`; no # operator.

local W, H, ROWS = 600, 486, 20
local TABS = { "Overview", "Classes", "Abilities", "Records", "Nemesis", "Co-op", "History" };
local CLASS_COLORS = { warrior = "c79c6e", paladin = "f58cba", hunter = "abd473", rogue = "fff569", priest = "ffffff",
                       shaman = "2f8fff", mage = "69ccf0", warlock = "9482c9", druid = "ff7d0a", death_knight = "c41f3b" };

local tab, page, classKey = 1, 1, nil;
local frame, lines, tabButtons, classButton, classIcon, pageText, prevButton, nextButton;

-- icons: { texture, left, right, top, bottom } (the coordinates cut one class out of the class sheet)
local CLASS_SHEET = "Interface\\Glues\\CharacterCreate\\UI-CharacterCreate-Classes";
local CLASS_COORDS = { warrior = { 0, 0.25, 0, 0.25 }, mage = { 0.25, 0.49609375, 0, 0.25 }, rogue = { 0.49609375, 0.7421875, 0, 0.25 },
                       druid = { 0.7421875, 0.98828125, 0, 0.25 }, hunter = { 0, 0.25, 0.25, 0.5 }, shaman = { 0.25, 0.49609375, 0.25, 0.5 },
                       priest = { 0.49609375, 0.7421875, 0.25, 0.5 }, warlock = { 0.7421875, 0.98828125, 0.25, 0.5 },
                       paladin = { 0, 0.25, 0.5, 0.75 } };
local QUESTION = "Interface\\Icons\\INV_Misc_QuestionMark";

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
	if ( icon ) then return { "Interface\\Icons\\" .. icon }; end
	return { QUESTION };
end

-- puts an icon on a texture (a spell without a known icon gets the question mark)
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

-- the block shown: every run, or one class's
local function Block()
	local db = DB();
	if ( classKey and type(db.classes[classKey]) == "table" ) then return db.classes[classKey], db.classes[classKey].name; end
	classKey = nil;
	return db.totals, "All classes";
end

-- sorted list of { key, value } from a table, by f(value), largest first
local function Sorted(t, f)
	local out = {};
	for k, v in pairs(t or {}) do
		if ( type(v) == "table" ) then table.insert(out, { key = k, v = v }); end
	end
	table.sort(out, function(a, b) return f(a.v) > f(b.v); end);
	return out;
end

-- ---------------------------------------------------------------- what each tab shows: { left, right } lines
local function Head(out, text) table.insert(out, { C("ffd100", text), "" }); end
local function Line(out, l, r, icon) table.insert(out, { l, r or "", icon }); end
local function Gap(out) table.insert(out, { " ", "" }); end

local function Overview()
	local out = {};
	local db = DB();
	local b, name = Block();
	Head(out, name);
	Line(out, "Runs", Big(b.runs));
	Line(out, "Victories", C("1eff00", Big(b.wins)) .. "  (" .. Pct(b.wins, b.runs) .. ")");
	Line(out, "Defeats / left", Big(b.defeat) .. " / " .. Big(b.left));
	Line(out, "Time in runs", Hours(b.time));
	Line(out, "Highest level", Big(b.bestLvl));
	Line(out, "Longest run", Clock(b.bestTime));
	Line(out, "Enemies slain", Big(b.kills));
	Line(out, "Elites / guardians", Big(b.elites) .. " / " .. Big(b.guardians));
	Line(out, "Bosses", C("ff8000", Big(b.bosses)));
	Line(out, "Damage dealt", Big(b.dmg));
	local bd = (classKey and b.best or db.best).dps;
	Line(out, "Highest DPS", C("ff8000", type(bd) == "table" and Big(bd.v) or "-"));
	local h = db.history;
	for i = table.getn(h), 1, -1 do
		if ( not classKey or h[i].class == classKey ) then
			if ( Num(h[i].dps) > 0 ) then Line(out, "Last run's DPS", Big(h[i].dps)); end
			break;
		end
	end
	Line(out, "Damage taken", Big(b.taken));
	Line(out, "Gold collected", C("ffd100", Big(b.gold)));
	Line(out, "Caches opened", Big(b.chests));
	if ( not classKey ) then
		local p = db.play;
		Gap(out);
		Head(out, "Play time");
		Line(out, "Time played", Hours(p.seconds));
		Line(out, "Sessions", Big(p.sessions));
		Line(out, "This session", Hours(p.last));
		Line(out, "Longest session", Hours(p.longest));
	else
		Line(out, "Pick rate", Pct(b.runs, db.totals.runs));
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
		Line(out, C(ClassHex(list[i].key), g.name ~= "" and g.name or S().Pretty(list[i].key)),
		     Pct(g.runs, db.totals.runs) .. " of runs  ·  won " .. Pct(g.wins, g.runs), ClassIcon(list[i].key));
		Line(out, "    " .. C("9d9d9d", Big(g.runs) .. " runs, " .. Big(g.kills) .. " kills, best level " .. Big(g.bestLvl) ..
		     ", longest " .. Clock(g.bestTime)), "");
	end
	if ( table.getn(list) == 0 ) then Line(out, "No runs recorded yet.", ""); end
	local months = {};
	for m, e in pairs(db.months) do
		if ( type(e) == "table" ) then table.insert(months, m); end
	end
	table.sort(months);
	if ( table.getn(months) > 0 ) then
		Gap(out);
		Head(out, "Picks per month");
		for i = table.getn(months), math.max(1, table.getn(months) - 11), -1 do
			local e = db.months[months[i]];
			local parts = {};
			local cl = {};
			for k, n in pairs(e.classes or {}) do table.insert(cl, { k = k, n = Num(n) }); end
			table.sort(cl, function(a, b) return a.n > b.n; end);
			for j = 1, math.min(3, table.getn(cl)) do
				local g = db.classes[cl[j].k];
				local nm = (type(g) == "table" and g.name ~= "") and g.name or S().Pretty(cl[j].k);
				table.insert(parts, C(ClassHex(cl[j].k), nm) .. " " .. Pct(cl[j].n, e.runs));
			end
			Line(out, months[i], table.concat(parts, "  "));
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
		Line(out, C("ffffff", nm) .. (Num(a.bestRank) > 0 and C("9d9d9d", "  rank " .. a.bestRank) or ""),
		     "picked " .. Pct(a.picks, b.runs) .. "  ·  won " .. Pct(a.wins, a.picks) .. "  ·  " .. Big(a.dmg) .. " dmg", SpellIcon(nm));
	end
	if ( table.getn(list) == 0 ) then Line(out, "No abilities recorded yet.", ""); end
	return out;
end

local function Records()
	local out = {};
	local b, name = Block();
	local best = classKey and b.best or DB().best;
	Head(out, "Personal bests  -  " .. name);
	local order = { { "time", "Longest run", true }, { "fastestWin", "Fastest victory", true }, { "lvl", "Highest level" },
	                 { "dps", "Highest DPS" },
	                 { "kills", "Most kills in a run" }, { "dmg", "Most damage in a run" }, { "gold", "Most gold in a run" },
	                 { "bosses", "Most bosses in a run" }, { "elites", "Most elites in a run" }, { "guardians", "Most guardians in a run" } };
	for i = 1, table.getn(order) do
		local r = best[order[i][1]];
		if ( type(r) == "table" ) then
			Line(out, C("ff8000", order[i][2]), order[i][3] and Clock(r.v) or Big(r.v));
			Line(out, "    " .. C("9d9d9d", (r.cname or "") .. " · " .. (r.zname or "") .. " · " .. (r.date or "")), "");
		end
	end
	if ( table.getn(out) == 1 ) then Line(out, "No records yet.", ""); end
	return out;
end

local function Nemesis()
	local out = {};
	local db = DB();
	local b, name = Block();
	local killers = Sorted(classKey and b.nemesis or db.nemesis, function(v) return Num(v.kills); end);
	Head(out, "Nemesis  -  " .. name);
	if ( killers[1] ) then
		Line(out, "Your nemesis: " .. C("ff4040", killers[1].key), Big(killers[1].v.kills) .. " kills");
	else
		Line(out, "No one has killed you yet.", "");
	end
	local deaths = 0;
	for i = 1, table.getn(killers) do deaths = deaths + Num(killers[i].v.kills); end
	for i = 2, math.min(8, table.getn(killers)) do
		Line(out, "    " .. killers[i].key, Big(killers[i].v.kills) .. " kills  (" .. Pct(killers[i].v.kills, deaths) .. ")");
	end
	Gap(out);
	Head(out, "Death log");
	local d = db.deaths;
	local shown = 0;
	for i = table.getn(d), 1, -1 do
		local e = d[i];
		if ( not classKey or e.class == classKey ) then
			shown = shown + 1;
			local how = "";
			if ( (e.spell or "") ~= "" ) then how = " - " .. e.spell; end
			if ( Num(e.amt) > 0 ) then how = how .. " " .. Big(e.amt); end
			Line(out, C("ff4040", e.killer) .. how, C(ClassHex(e.class), e.cname) .. " " .. Big(e.lvl) .. "  ·  " .. Clock(e.t));
			Line(out, "    " .. C("9d9d9d", (e.date or "") .. "  " .. (e.zname or "")), "");
			if ( (e.last or "") ~= "" ) then Line(out, "    " .. C("9d9d9d", "last hits: " .. e.last), ""); end
		end
	end
	if ( shown == 0 ) then Line(out, "No deaths recorded yet.", ""); end
	return out;
end

local function Coop()
	local out = {};
	local co = DB().coop;
	Head(out, "Co-op");
	Line(out, "Runs with friends", Big(co.runs) .. "  (won " .. Pct(co.wins, co.runs) .. ")");
	Line(out, "Solo runs", Big(co.solo) .. "  (won " .. Pct(co.soloWins, co.solo) .. ")");
	Line(out, "Time together", Hours(co.time));
	Line(out, "Kills / bosses together", Big(co.kills) .. " / " .. Big(co.bosses));
	local bt = co.best and co.best.time;
	if ( type(bt) == "table" ) then Line(out, "Longest run together", Clock(bt.v) .. "  " .. C("9d9d9d", bt.with or "")); end
	local fw = co.best and co.best.fastestWin;
	if ( type(fw) == "table" ) then Line(out, "Fastest win together", Clock(fw.v) .. "  " .. C("9d9d9d", fw.with or "")); end
	Gap(out);
	Head(out, "Who you play with");
	local list = Sorted(co.friends, function(v) return Num(v.runs); end);
	for i = 1, table.getn(list) do
		local e = list[i].v;
		Line(out, C(ClassHex(e.class), list[i].key) .. ((e.cname or "") ~= "" and C("9d9d9d", "  " .. e.cname) or ""),
		     Big(e.runs) .. " runs  ·  " .. Big(e.wins) .. " won", ClassIcon(e.class));
		local acc = "first " .. (e.first or "?");
		if ( (e.firstWin or "") ~= "" ) then acc = acc .. "  ·  first win " .. e.firstWin; end
		if ( Num(e.fastestWin) > 0 ) then acc = acc .. "  ·  fastest win " .. Clock(e.fastestWin); end
		Line(out, "    " .. C("9d9d9d", acc), "");
		Line(out, "    " .. C("9d9d9d", "longest " .. Clock(e.bestTime) .. "  ·  most bosses " .. Big(e.mostBosses) ..
		     "  ·  their damage " .. Big(e.dmg) .. "  ·  last " .. (e.last or "")), "");
	end
	if ( table.getn(list) == 0 ) then Line(out, "No co-op runs recorded yet.", ""); end
	return out;
end

local function History()
	local out = {};
	local h = DB().history;
	local b, name = Block();
	Head(out, "Recent runs  -  " .. name);
	for i = table.getn(h), 1, -1 do
		local r = h[i];
		if ( not classKey or r.class == classKey ) then
			local hex = r.kind == "victory" and "1eff00" or r.kind == "defeat" and "ff4040" or "9d9d9d";
			Line(out, C(hex, r.result) .. "  " .. C(ClassHex(r.class), r.cname) .. "  " .. (r.zname or ""),
			     Clock(r.t) .. "  ·  lvl " .. Big(r.lvl) .. "  ·  " .. Big(r.kills) .. " kills" ..
			     (Num(r.dps) > 0 and ("  ·  " .. C("ff8000", Big(r.dps) .. " DPS")) or ""), ClassIcon(r.class));
			local extra = r.date or "";
			if ( r.party ) then extra = extra .. "  with " .. r.party; end
			if ( r.killer ) then extra = extra .. "  slain by " .. r.killer; end
			Line(out, "    " .. C("9d9d9d", extra), "");
		end
	end
	if ( table.getn(out) == 1 ) then Line(out, "No runs recorded yet.", ""); end
	return out;
end

local BUILD = { Overview, Classes, Abilities, Records, Nemesis, Coop, History };

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
	SetIcon(classIcon, classKey and ClassIcon(classKey) or { "Interface\\Icons\\INV_Misc_Book_09" });
	if ( tab == 2 or tab == 6 ) then classButton:Hide(); classIcon:Hide(); else classButton:Show(); end
	local all = BUILD[tab]();
	local pages = math.max(1, math.ceil(table.getn(all) / ROWS));
	if ( page > pages ) then page = pages; end
	if ( page < 1 ) then page = 1; end
	for i = 1, ROWS do
		local e = all[(page - 1) * ROWS + i];
		lines[i].l:SetText(e and e[1] or "");
		lines[i].r:SetText(e and e[2] or "");
		SetIcon(lines[i].icon, e and e[3] or nil);
		lines[i].l:SetPoint("TOPLEFT", frame, "TOPLEFT", (e and e[3]) and 44 or 26, lines[i].y);
	end
	pageText:SetText(page .. " / " .. pages);
	if ( page > 1 ) then prevButton:Enable(); else prevButton:Disable(); end
	if ( page < pages ) then nextButton:Enable(); else nextButton:Disable(); end
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
	page = 1;
	Refresh();
end

local function Button(parent, text, width, onClick)
	local b = CreateFrame("Button", nil, parent, "UIPanelButtonTemplate");
	b:SetWidth(width);
	b:SetHeight(22);
	b:SetText(text);
	b:SetScript("OnClick", onClick);
	return b;
end

-- a frame from one of the game's templates, or a plain one if the game does not have that template
local function Templated(kind, name, parent, template)
	local ok, f = pcall(CreateFrame, kind, name, parent, template);
	if ( ok and f ) then return f, true; end
	return CreateFrame(kind, name, parent), false;
end

local function Sound(name) if ( PlaySound ) then pcall(PlaySound, name); end end

-- the spellbook's page arrows
local function Arrow(parent, dir, onClick)
	local b = CreateFrame("Button", nil, parent);
	b:SetWidth(28);
	b:SetHeight(28);
	b:SetNormalTexture("Interface\\Buttons\\UI-SpellbookIcon-" .. dir .. "Page-Up");
	b:SetPushedTexture("Interface\\Buttons\\UI-SpellbookIcon-" .. dir .. "Page-Down");
	b:SetDisabledTexture("Interface\\Buttons\\UI-SpellbookIcon-" .. dir .. "Page-Disabled");
	b:SetHighlightTexture("Interface\\Buttons\\UI-Common-MouseHilight", "ADD");
	b:SetScript("OnClick", function() Sound("igAbiliityPageTurn"); onClick(); end);
	return b;
end

local function Build()
	frame = CreateFrame("Frame", "AZSStatsFrame", UIParent);
	frame:SetWidth(W);
	frame:SetHeight(H);
	frame:SetPoint("CENTER", UIParent, "CENTER", 0, 20);
	frame:SetFrameStrata("DIALOG");
	frame:SetToplevel(true);
	frame:SetBackdrop({ bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
	                    edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
	                    tile = true, tileSize = 32, edgeSize = 32, insets = { left = 11, right = 12, top = 12, bottom = 11 } });
	frame:EnableMouse(true);
	frame:EnableMouseWheel(true);
	frame:SetMovable(true);
	frame:SetClampedToScreen(true);
	frame:RegisterForDrag("LeftButton");
	frame:SetScript("OnDragStart", function() this:StartMoving(); end);
	frame:SetScript("OnDragStop", function() this:StopMovingOrSizing(); end);
	frame:SetScript("OnMouseWheel", function() page = page - arg1; Refresh(); end);   -- the wheel turns the pages
	frame:SetScript("OnShow", function() Sound("igCharacterInfoOpen"); Refresh(); end);
	frame:SetScript("OnHide", function() Sound("igCharacterInfoClose"); end);
	frame:Hide();
	table.insert(UISpecialFrames, "AZSStatsFrame");      -- Escape closes it

	-- the dialog header with the title, and your portrait in a gold ring at the top left
	local header = frame:CreateTexture(nil, "ARTWORK");
	header:SetTexture("Interface\\DialogFrame\\UI-DialogBox-Header");
	header:SetWidth(300);
	header:SetHeight(64);
	header:SetPoint("TOP", frame, "TOP", 0, 12);
	local title = frame:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	title:SetPoint("TOP", header, "TOP", 0, -14);
	title:SetText("AZS Stats");
	local portrait = frame:CreateTexture(nil, "ARTWORK");
	portrait:SetWidth(46);
	portrait:SetHeight(46);
	portrait:SetPoint("TOPLEFT", frame, "TOPLEFT", 18, -16);
	if ( SetPortraitTexture ) then SetPortraitTexture(portrait, "player"); end
	local ring = frame:CreateTexture(nil, "OVERLAY");
	ring:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder");
	ring:SetWidth(84);
	ring:SetHeight(84);
	ring:SetPoint("TOPLEFT", portrait, "TOPLEFT", -6, 5);
	frame:RegisterEvent("UNIT_PORTRAIT_UPDATE");
	frame:SetScript("OnEvent", function() if ( arg1 == "player" and SetPortraitTexture ) then SetPortraitTexture(portrait, "player"); end end);
	local close = CreateFrame("Button", nil, frame, "UIPanelCloseButton");
	close:SetPoint("TOPRIGHT", frame, "TOPRIGHT", -6, -6);

	-- CLASS: all classes or one, with its icon
	classIcon = frame:CreateTexture(nil, "ARTWORK");
	classIcon:SetWidth(22);
	classIcon:SetHeight(22);
	classIcon:SetPoint("TOPLEFT", frame, "TOPLEFT", 74, -38);
	classButton = Button(frame, "All classes", 150, function() Sound("igMainMenuOptionCheckBoxOn"); NextClass(); end);
	classButton:SetPoint("LEFT", classIcon, "RIGHT", 6, 0);
	local hint = frame:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	hint:SetPoint("LEFT", classButton, "RIGHT", 8, 0);
	hint:SetText("click to switch class");

	-- the lines, each with room for an icon
	local rule = frame:CreateTexture(nil, "ARTWORK");
	rule:SetTexture("Interface\\DialogFrame\\UI-DialogBox-Divider");
	rule:SetWidth(W - 40);
	rule:SetHeight(16);
	rule:SetPoint("TOPLEFT", frame, "TOPLEFT", 20, -70);
	lines = {};
	for i = 1, ROWS do
		local y = -86 - (i - 1) * 17;
		local icon = frame:CreateTexture(nil, "ARTWORK");
		icon:SetWidth(15);
		icon:SetHeight(15);
		icon:SetPoint("TOPLEFT", frame, "TOPLEFT", 24, y);
		local l = frame:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
		l:SetPoint("TOPLEFT", frame, "TOPLEFT", 26, y);
		l:SetWidth(320);
		l:SetHeight(16);
		l:SetJustifyH("LEFT");
		local r = frame:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall");
		r:SetPoint("TOPRIGHT", frame, "TOPRIGHT", -24, y);
		r:SetWidth(W - 48 - 150);
		r:SetHeight(16);
		r:SetJustifyH("RIGHT");
		lines[i] = { l = l, r = r, icon = icon, y = y };
	end

	-- the spellbook's arrows and the page
	prevButton = Arrow(frame, "Prev", function() page = page - 1; Refresh(); end);
	prevButton:SetPoint("BOTTOMLEFT", frame, "BOTTOMLEFT", 22, 18);
	nextButton = Arrow(frame, "Next", function() page = page + 1; Refresh(); end);
	nextButton:SetPoint("LEFT", prevButton, "RIGHT", 2, 0);
	pageText = frame:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall");
	pageText:SetPoint("LEFT", nextButton, "RIGHT", 6, 0);
	local foot = frame:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall");
	foot:SetPoint("BOTTOMRIGHT", frame, "BOTTOMRIGHT", -22, 26);
	foot:SetText("Also on the launcher's STATS page");

	-- the character sheet's tabs, along the bottom edge
	tabButtons = {};
	local templated = true;
	for i = 1, table.getn(TABS) do
		local index = i;
		local b, ok = Templated("Button", "AZSStatsFrameTab" .. i, frame, "CharacterFrameTabButtonTemplate");
		templated = templated and ok;
		if ( not ok ) then
			b:Hide();
			b = Button(frame, TABS[i], 78, nil);
		end
		b:SetID(i);
		b:SetText(TABS[i]);
		b:SetScript("OnClick", function() Sound("igCharacterInfoTab"); tab = index; page = 1; Refresh(); end);
		if ( i == 1 ) then b:SetPoint("TOPLEFT", frame, "BOTTOMLEFT", 12, ok and 8 or -2);
		else b:SetPoint("LEFT", tabButtons[i - 1], "RIGHT", ok and -14 or 2, 0); end
		if ( ok and PanelTemplates_TabResize ) then pcall(PanelTemplates_TabResize, 0, b); end
		tabButtons[i] = b;
	end
	if ( templated and PanelTemplates_SetNumTabs ) then
		PanelTemplates_SetNumTabs(frame, table.getn(TABS));
		frame.selectedTab = 1;
		PanelTemplates_UpdateTabs(frame);
	end
end

function AZSStats_Toggle()
	if ( not frame ) then Build(); end
	if ( frame:IsShown() ) then frame:Hide(); else frame:Show(); end
end

SLASH_AZSSTATSUI1 = "/azs";
SlashCmdList["AZSSTATSUI"] = function() AZSStats_Toggle(); end

-- ---------------------------------------------------------------- the minimap button
-- The tracking-button look: a round icon in the minimap's gold ring. Click: the window. Drag: move it around the
-- minimap (the angle is saved). Its tooltip shows your runs and best results at a glance.
local mini;

local function MiniPlace()
	local a = math.rad(Num(DB().minimap.angle));
	local r = 80;
	mini:ClearAllPoints();
	mini:SetPoint("CENTER", Minimap, "CENTER", r * math.cos(a), r * math.sin(a));
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
	GameTooltip:AddLine("Click to open your stats. Drag to move this button.", 0.6, 0.6, 0.6);
	GameTooltip:Show();
end

local function MiniBuild()
	if ( mini or not Minimap ) then return; end
	local db = DB();
	if ( type(db.minimap) ~= "table" ) then db.minimap = { angle = 200, hide = false }; end
	mini = CreateFrame("Button", "AZSStatsMinimapButton", Minimap);
	mini:SetWidth(31);
	mini:SetHeight(31);
	mini:SetFrameStrata("MEDIUM");
	mini:SetFrameLevel(8);
	mini:SetToplevel(true);
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
	mini:SetScript("OnClick", function() AZSStats_Toggle(); end);
	mini:SetScript("OnEnter", MiniTooltip);
	mini:SetScript("OnLeave", function() GameTooltip:Hide(); end);
	mini:SetScript("OnDragStart", function()
		this:SetScript("OnUpdate", function()
			local x, y = GetCursorPosition();
			local s = Minimap:GetEffectiveScale();
			local cx, cy = Minimap:GetCenter();
			DB().minimap.angle = math.deg(math.atan2(y / s - cy, x / s - cx));
			MiniPlace();
		end);
	end);
	mini:SetScript("OnDragStop", function() this:SetScript("OnUpdate", nil); end);
	MiniPlace();
	if ( db.minimap.hide ) then mini:Hide(); end
end

function AZSStats_ToggleMinimapButton()
	MiniBuild();
	if ( not mini ) then return; end
	local m = DB().minimap;
	m.hide = not m.hide;
	if ( m.hide ) then mini:Hide(); S().Print("the minimap button is hidden. /azstats minimap brings it back.");
	else mini:Show(); S().Print("the minimap button is back."); end
end

local loader = CreateFrame("Frame");
loader:RegisterEvent("VARIABLES_LOADED");
loader:RegisterEvent("PLAYER_ENTERING_WORLD");
loader:SetScript("OnEvent", function() MiniBuild(); end);
