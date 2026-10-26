-- AZS Handbook: the launcher's Guide in game. Every topic and page of the guide (AZSHandbookData.lua,
-- written by the launcher from its own guide, so the two always match), with the game's own icons: each spell of a
-- class with its icon and the talents that change it, every class's three talent trees as the game draws them,
-- and the spells and talents named in the text in gold. /handbook (or /azsh), or the book by the minimap, opens it.
-- The look is AZS Stats' (AZSKit.lua). When AZS Stats has seen a spell's tooltip in game, pointing at the spell
-- shows the game's own text.
-- Lua 5.0 / 1.12 interface: scripts read `this`, `arg1`; no # operator; only the 1.12 API.

local K = AZSKIT;
local W, H = 680, 560;
local CW = W - 64;                                      -- the list's width (inside the frame, beside the scroll bar)
local NAME = "AZS Handbook";

local view = { topic = nil, page = nil, talents = nil };
local frame, hero, scroll, child, backBtn, homeBtn, prevBtn, nextBtn;

local function D() return AZSHANDBOOK_DATA or { topics = {}, talents = {}, icons = {}, names = {} }; end
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
                      ["Pickups, Caches & Guardians"] = "INV_Misc_Coin_01", ["Gear & the Armory"] = "INV_Chest_Plate06",
                      ["Talents"] = "Ability_Marksmanship", ["Achievements & Rewards"] = "INV_Misc_Bag_10",
                      ["Run Options & Endless"] = "Spell_Shadow_DeathScream", ["Co-op"] = "Ability_Warrior_BattleShout",
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
	local marks = {};
	for i = 1, table.getn(names) do
		if ( string.find(text, names[i], 1, true) ) then
			table.insert(marks, names[i]);
			text = string.gsub(text, escaped[i], "\001" .. table.getn(marks) .. "\002");
		end
	end
	text = string.gsub(text, "\001(%d+)\002", function(n) return "|cffffd100" .. marks[tonumber(n)] .. "|r"; end);
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

-- the game's own tooltip text of a spell, when AZS Stats has seen it
local function GameTip(name)
	if ( type(AZSStatsDB) == "table" and type(AZSStatsDB.tooltips) == "table" ) then
		local t = AZSStatsDB.tooltips[name];
		if ( type(t) == "table" and t.text ) then return t.text; end
	end
	return nil;
end

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

-- ---------------------------------------------------------------- what a view shows
-- items: { kind = "head" | "para" | "bullet" | "tip" | "note" | "cards" | "spell" | "button" | "trees", ... }
local function Items()
	local data = D();
	local out = {};
	local topics = data.topics;
	if ( view.talents ) then
		table.insert(out, { kind = "note", text = "Point at a talent to read it. A new row opens for every 5 points spent in a tree; " ..
		                                           "the purple ones are the milestones at 10, 20 and 30 points." });
		table.insert(out, { kind = "trees", class = view.talents });
		return out;
	end
	if ( not view.topic ) then
		local cards = {};
		for i = 1, table.getn(topics) do
			local index = i;
			table.insert(cards, { title = topics[i].title, desc = topics[i].desc, go = function() view.topic, view.page = index, nil; end });
		end
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
			table.insert(out, { kind = "head", text = section });
			if ( class and string.sub(section, 1, 6) == "Talent" ) then
				local specs = {};
				for j = 1, table.getn(data.talents[class]) do table.insert(specs, data.talents[class][j].spec); end
				table.insert(out, { kind = "para", text = "The " .. class .. "'s three talent trees: " .. table.concat(specs, ", ") .. "." });
				local c = class;
				table.insert(out, { kind = "button", text = "Talent trees", go = function() view.talents = c; end });
			end
		elseif ( class and string.sub(section, 1, 6) == "Talent" ) then
			-- the trees have their own view
		elseif ( string.sub(line, 1, 2) == "- " ) then
			Flush();
			local text = string.sub(line, 3);
			local name, rest = SplitBullet(text);
			if ( class and string.sub(section, 1, 10) == "Spell pool" and name ) then
				local spell = StripRank(name);
				table.insert(out, { kind = "spell", name = name, spell = spell, text = string.upper(string.sub(rest, 1, 1)) .. string.sub(rest, 2),
				                    icon = IconOf(spell), changers = Changers(class, spell) });
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
		f.cells[i] = c;
	end
	return f;
end

local function DrawTree(f, tree, w)
	f:SetWidth(w);
	local cell = math.floor((w - 16) / 4);
	f:SetHeight(30 + 7 * cell + 8);
	f.title:SetText(tree.spec);
	for i = 1, 28 do f.cells[i]:Hide(); f.cells[i].t = nil; end
	for i = 1, math.min(28, table.getn(tree.t)) do
		local t, c = tree.t[i], f.cells[i];
		c.t, c.spec = t, tree.spec;
		c:ClearAllPoints();
		c:SetPoint("TOPLEFT", f, "TOPLEFT", 8 + t[4] * cell + math.floor((cell - 34) / 2), -(30 + t[3] * cell + math.floor((cell - 34) / 2)));
		K.Icon(c.icon, t[2]);
		if ( t[8] ) then c.edge:SetVertexColor(0.75, 0.5, 1); else c.edge:SetVertexColor(1, 1, 1); end
		c.rank:SetText("0/" .. t[5]);
		c:Show();
	end
	return f:GetHeight();
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
				SetPicture(f.icon, c.title);
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
		elseif ( it.kind == "trees" ) then
			local trees = D().talents[it.class] or {};
			local tw, th = math.floor((CW - 16) / 3), 0;
			for i = 1, table.getn(trees) do
				local f = Get("tree", MakeTree);
				f:SetPoint("TOPLEFT", child, "TOPLEFT", (i - 1) * (tw + 8), -y);
				th = math.max(th, DrawTree(f, trees[i], tw));
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
	if ( view.topic or view.talents ) then backBtn:Show(); homeBtn:Show(); else backBtn:Hide(); homeBtn:Hide(); end
	-- previous / next: the pages of the topic, or the topics
	local topics = D().topics;
	local count, at = 0, 0;
	if ( view.talents ) then count = 0;
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
	if ( view.talents ) then view.talents = nil;
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
	homeBtn = K.Button(hero, "Home", 70, function() Go(function() view.topic, view.page, view.talents = nil, nil, nil; end); end);
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
		if ( string.lower(topics[i].title) == what ) then view.topic, view.page, view.talents = i, nil, nil; return true; end
		for j = 1, table.getn(topics[i].pages) do
			if ( string.lower(topics[i].pages[j].title) == what ) then view.topic, view.page, view.talents = i, j, nil; return true; end
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
