-- MinimapButtonBag (MBB) for Azeroth Survivors.
-- The vanilla addon by Tunhadil, continued by LaYt (github.com/laytya/MinimapButtonBag-vanilla, v0.508), converted to
-- this game's client: everything is made in Lua (no XML, no game templates it cannot do without), the bag's own
-- button is a plain Frame on the minimap with its clicks from OnMouseDown / OnMouseUp (the way this client's minimap
-- buttons work), every SetPoint has all its parts, and each other addon's button is taken in on its own (one that
-- cannot be moved is left where it is instead of stopping the rest).
--
-- What it does is the same: every minimap button goes into one bag button; click it and they open in a bar.
--   Left click: open / close the bar.   Right click: the options.
--   Ctrl + Right click: take the bag button off the minimap (drag it anywhere) / put it back.
--   Shift + drag: move the bag button.  Ctrl + Right click on a button in the bar: back onto the minimap (and again: back in).
--   /mbb buttons, /mbb reset position, /mbb reset all
-- Lua 5.0 / 1.12 interface: no #, scripts read this / arg1.

MBB_Version = "0.508-azs2";                -- a new version starts from the defaults below
MBB_DragFlag = 0;
MBB_ShowTimeout = -1;
MBB_Buttons = {};
MBB_Exclude = MBB_Exclude or {};

MBB_DefaultOptions = {
	ButtonPos = { 20, 0 },           -- attached: the angle around the minimap (0 = right, 90 = top); detached: x, y on the
	                                 -- screen. 20: the right side, clear of the game's damage meter button and the AZS ones
	AttachToMinimap = 1,
	CollapseTimeout = 3,                    -- seconds after the pointer leaves the bag and its buttons
	ExpandDirection = 1,
	MaxButtonsPerLine = 0,
	AltExpandDirection = 4,
	Scale = 100,
	Version = MBB_Version,
};

-- frames that are part of the minimap itself, or must stay where they are
MBB_Ignore = { "MBB", "MiniMapTrackingFrame", "MiniMapMeetingStoneFrame", "MiniMapMailFrame", "MiniMapBattlefieldFrame",
	"MiniMapPing", "MinimapBackdrop", "MinimapZoomIn", "MinimapZoomOut", "BookOfTracksFrame", "GatherNote",
	"FishingExtravaganzaMini", "MiniNotePOI", "RecipeRadarMinimapIcon", "FWGMinimapPOI", "MBB_MinimapButtonFrame",
	"QuestieNote", "MetaMap", "LootLinkMinimapButton", "TimeManagerClockButton", "pfMiniMapPin", "MinimapCluster",
	"GameTimeFrame" };

local DIR_NAMES = { MBB_OPTIONS_EXPANSIONLEFT or "Left", MBB_OPTIONS_EXPANSIONTOP or "Top",
                    MBB_OPTIONS_EXPANSIONRIGHT or "Right", MBB_OPTIONS_EXPANSIONBOTTOM or "Bottom" };
local RING = 78;

local function Copy(t)
	local n = {};
	for k, v in pairs(t) do
		if ( type(v) == "table" ) then v = Copy(v); end
		n[k] = v;
	end
	return n;
end

function MBB_Print(msg)
	if ( DEFAULT_CHAT_FRAME ) then DEFAULT_CHAT_FRAME:AddMessage(msg, 0.2, 0.8, 0.8); end
end

function MBB_IsInArray(array, needle)
	if ( type(array) == "table" ) then
		for i, e in pairs(array) do
			if ( type(e) == type(needle) and e == needle ) then return i; end
		end
	end
	return nil;
end

function MBB_IsExcluded(name)
	return MBB_IsInArray(MBB_Exclude, name) ~= nil;
end

local function Opts()
	if ( type(MBB_Options) ~= "table" ) then MBB_Options = Copy(MBB_DefaultOptions); end
	for k, v in pairs(MBB_DefaultOptions) do
		if ( MBB_Options[k] == nil ) then MBB_Options[k] = type(v) == "table" and Copy(v) or v; end
	end
	return MBB_Options;
end

-- a script of a frame, or nil (some frames of this client have no HasScript)
local function Script(frame, kind)
	local ok, has = pcall(function() return frame:HasScript(kind); end);
	if ( ok and not has ) then return nil; end
	local ok2, s = pcall(function() return frame:GetScript(kind); end);
	if ( ok2 ) then return s; end
	return nil;
end

-- ---------------------------------------------------------------- the bag's own button (on the minimap)
-- On UIParent (placed next to the minimap), not on the Minimap itself: a frame on the minimap can end up under it or
-- hidden with it on this client, and the bag button must always be there to bring the buttons back.
local bag = CreateFrame("Frame", "MBB_MinimapButtonFrame", UIParent);
bag:SetWidth(33);
bag:SetHeight(33);
bag:SetFrameStrata("MEDIUM");
bag:SetFrameLevel(30);
bag:EnableMouse(true);
bag:SetMovable(true);
bag.icon = bag:CreateTexture("MBB_MinimapButtonFrame_Texture", "BACKGROUND");
bag.icon:SetWidth(21);
bag.icon:SetHeight(21);
bag.icon:SetPoint("CENTER", bag, "CENTER", -1, 1);
bag.icon:SetTexture("Interface\\Icons\\INV_Misc_Bag_10");
bag.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93);
bag.border = bag:CreateTexture(nil, "ARTWORK");
bag.border:SetWidth(52);
bag.border:SetHeight(52);
bag.border:SetPoint("TOPLEFT", bag, "TOPLEFT", 0, 0);
bag.border:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder");
bag.glow = bag:CreateTexture(nil, "OVERLAY");
bag.glow:SetWidth(32);
bag.glow:SetHeight(32);
bag.glow:SetPoint("CENTER", bag, "CENTER", 0, 0);
bag.glow:SetTexture("Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight");
bag.glow:SetBlendMode("ADD");
bag.glow:Hide();

function MBB_SetButtonPosition()
	local o = Opts();
	local s = (o.Scale or 100) / 100;
	bag:SetScale(s);
	bag:ClearAllPoints();
	if ( o.AttachToMinimap == 1 and Minimap and Minimap:GetCenter() ) then
		local a = math.rad(tonumber(o.ButtonPos[1]) or 20);
		local ms = Minimap:GetEffectiveScale() / bag:GetEffectiveScale();
		local cx, cy = Minimap:GetCenter();
		bag:SetPoint("CENTER", UIParent, "BOTTOMLEFT", cx * ms + RING * math.cos(a) / s, cy * ms + RING * math.sin(a) / s);
	elseif ( o.AttachToMinimap == 1 ) then
		bag:SetPoint("CENTER", UIParent, "TOPRIGHT", -170 / s, -170 / s);
	else
		bag:SetPoint("CENTER", UIParent, "BOTTOMLEFT", (tonumber(o.ButtonPos[1]) or 300) / s, (tonumber(o.ButtonPos[2]) or 300) / s);
	end
	bag:Show();
end

-- is the bag button shown and on the screen? If not, it goes back to its place by the minimap
local function CheckBag()
	if ( not bag:IsShown() ) then bag:Show(); end
	local l, b = bag:GetLeft(), bag:GetBottom();
	local w, h = UIParent:GetWidth(), UIParent:GetHeight();
	local s = bag:GetEffectiveScale() / UIParent:GetEffectiveScale();
	if ( not l or not b or l * s < -20 or b * s < -20 or l * s > (w or 2000) - 10 or b * s > (h or 2000) - 10 ) then
		MBB_ResetPosition();
	end
end

function MBB_ResetPosition()
	local o = Opts();
	o.ButtonPos = Copy(MBB_DefaultOptions.ButtonPos);
	o.AttachToMinimap = 1;
	MBB_SetButtonPosition();
end

local function Cursor()
	local x, y = GetCursorPosition();
	local s = UIParent:GetEffectiveScale();
	return x / s, y / s;
end

-- shift + drag: around the minimap (attached) or anywhere (detached)
local function Drag()
	local o = Opts();
	local x, y = Cursor();
	if ( o.AttachToMinimap == 1 and Minimap ) then
		local cx, cy = Minimap:GetCenter();
		if ( cx and cy ) then
			local ms = Minimap:GetEffectiveScale() / UIParent:GetEffectiveScale();
			cx, cy = cx * ms, cy * ms;
			o.ButtonPos = { math.deg(math.atan2(y - cy, x - cx)), 0 };
		end
	else
		o.ButtonPos = { x, y };
	end
	MBB_SetButtonPosition();
end

bag:SetScript("OnMouseDown", function()
	bag.icon:ClearAllPoints();
	bag.icon:SetPoint("CENTER", bag, "CENTER", 0, 0);
	if ( arg1 == "LeftButton" and IsShiftKeyDown() ) then
		MBB_DragFlag = 1;
		GameTooltip:Hide();
		MBB_ShowTimeout = 0;
	end
end);
bag:SetScript("OnMouseUp", function()
	bag.icon:ClearAllPoints();
	bag.icon:SetPoint("CENTER", bag, "CENTER", -1, 1);
	if ( MBB_DragFlag == 1 ) then MBB_DragFlag = 0; return; end
	MBB_OnClick(arg1);
end);
bag:SetScript("OnEnter", function()
	bag.glow:Show();
	MBB_ShowTimeout = -1;
	GameTooltip:SetOwner(bag, "ANCHOR_LEFT");
	GameTooltip:AddLine("MinimapButtonBag v" .. MBB_Version);
	GameTooltip:AddLine("Left click: open / close. Right click: options.", 1, 1, 1);
	GameTooltip:AddLine(MBB_TOOLTIP1 or "", 0, 1, 0);
	GameTooltip:Show();
end);
bag:SetScript("OnLeave", function()
	bag.glow:Hide();
	MBB_ShowTimeout = 0;
	GameTooltip:Hide();
end);

-- ---------------------------------------------------------------- the buttons in the bag
local function Hook(frame, kind, wrap)
	local old = Script(frame, kind);
	if ( not old and kind ~= "OnEnter" and kind ~= "OnLeave" ) then return false; end
	local ok = pcall(function() frame:SetScript(kind, function() wrap(old); end); end);
	return ok;
end

-- Ctrl + Right click on a button: out of the bar onto the minimap, or back in
local function Swap(frame)
	local name = frame:GetName();
	if ( MBB_IsExcluded(name) ) then MBB_AddButton(name); else MBB_RestoreButton(name); end
	MBB_SetPositions();
end

function MBB_PrepareButton(name)
	local frame = getglobal(name);
	if ( not frame or frame.mbbReady ) then return false; end
	if ( frame.RegisterForClicks ) then pcall(function() frame:RegisterForClicks("LeftButtonUp", "RightButtonUp"); end); end
	frame.isvisible = frame:IsVisible() and true or false;
	frame.oshow = frame.Show;
	frame.ohide = frame.Hide;
	frame.Show = function(f)
		f.isvisible = true;
		if ( not MBB_IsExcluded(f:GetName()) ) then MBB_SetPositions(); end
		if ( MBB_IsExcluded(f:GetName()) or MBB_Open() ) then f.oshow(f); end
	end;
	frame.Hide = function(f)
		f.isvisible = false;
		f.ohide(f);
		if ( not MBB_IsExcluded(f:GetName()) ) then MBB_SetPositions(); end
	end;
	local swapped = function(old)
		if ( arg1 == "RightButton" and IsControlKeyDown() ) then Swap(this); return; end
		if ( old ) then old(); end
	end;
	if ( not Hook(frame, "OnClick", swapped) ) then
		if ( not Hook(frame, "OnMouseUp", swapped) ) then Hook(frame, "OnMouseDown", swapped); end
	end
	Hook(frame, "OnEnter", function(old)
		if ( not MBB_IsExcluded(this:GetName()) ) then MBB_ShowTimeout = -1; end
		if ( old ) then old(); end
	end);
	Hook(frame, "OnLeave", function(old)
		if ( not MBB_IsExcluded(this:GetName()) ) then MBB_ShowTimeout = 0; end
		if ( old ) then old(); end
	end);
	frame.mbbReady = true;
	return true;
end

-- Whether the bar is open is kept here, not read from the buttons: the game shows and hides some minimap buttons
-- itself (its HUD parts, like the damage meter's), and one of them showing made the bag think it was open, so a
-- click closed it instead.
local barOpen = false;

function MBB_Open()
	return barOpen;
end

function MBB_ShowButtons()
	MBB_SetPositions();
	barOpen = true;
	for i = 1, table.getn(MBB_Buttons) do
		local f = getglobal(MBB_Buttons[i]);
		if ( f and f.isvisible and f.oshow ) then f.oshow(f); end
	end
	MBB_ShowTimeout = -1;                                -- the pointer is on the bag: the collapse waits until it leaves
end

function MBB_AddButton(name)
	local child = getglobal(name);
	if ( not child or MBB_IsInArray(MBB_Buttons, name) ) then return; end
	local open = MBB_Open();
	child.opoint = { child:GetPoint() };
	if ( not child.opoint[1] ) then child.opoint = { "TOP", Minimap, "BOTTOM", 0, 0 }; end
	child.opoint[2] = child.opoint[2] or Minimap;
	child.opoint[3] = child.opoint[3] or child.opoint[1];
	child.osize = { child:GetHeight(), child:GetWidth() };
	child.oscale = child:GetScale();
	child.oclearallpoints = child.ClearAllPoints;
	child.ClearAllPoints = function() end;
	child.osetpoint = child.SetPoint;
	child.SetPoint = function() end;
	if ( not open ) then child.ohide(child); end
	table.insert(MBB_Buttons, name);
	local i = MBB_IsInArray(MBB_Exclude, name);
	if ( i ) then table.remove(MBB_Exclude, i); end
end

function MBB_RestoreButton(name)
	local b = getglobal(name);
	if ( not b or not b.osetpoint ) then return; end
	b.oclearallpoints(b);
	b.osetpoint(b, b.opoint[1], b.opoint[2], b.opoint[3], b.opoint[4] or 0, b.opoint[5] or 0);
	b:SetHeight(b.osize[1]);
	b:SetWidth(b.osize[2]);
	if ( b.oscale ) then b:SetScale(b.oscale); end
	b.ClearAllPoints = b.oclearallpoints;
	b.SetPoint = b.osetpoint;
	if ( b.isvisible ) then b.oshow(b); else b.ohide(b); end
	if ( not MBB_IsExcluded(name) ) then table.insert(MBB_Exclude, name); end
	local i = MBB_IsInArray(MBB_Buttons, name);
	if ( i ) then table.remove(MBB_Buttons, i); end
end

-- the bar: one line from the bag button, a new line every MaxButtonsPerLine
local DIRS = { { "RIGHT", "LEFT" }, { "BOTTOM", "TOP" }, { "LEFT", "RIGHT" }, { "TOP", "BOTTOM" } };

function MBB_SetPositions()
	local o = Opts();
	local per = tonumber(o.MaxButtonsPerLine) or 0;
	if ( per <= 0 ) then per = 100; end
	local count, prev, lineStart, prevLineStart = 0, nil, nil, nil;
	for i = 1, table.getn(MBB_Buttons) do
		local f = getglobal(MBB_Buttons[i]);
		if ( f and f.isvisible and f.oclearallpoints ) then
			count = count + 1;
			local d = DIRS[o.ExpandDirection] or DIRS[1];
			local parent = prev or bag;
			if ( count > 1 and math.mod(count - 1, per) == 0 ) then
				d = DIRS[o.AltExpandDirection] or DIRS[4];
				parent = lineStart;
			end
			pcall(function()
				f:SetHeight(31);
				f:SetWidth(31);
				f:SetScale((o.Scale or 100) / 100);
				f.oclearallpoints(f);
				f.osetpoint(f, d[1], parent, d[2], 0, 0);
			end);
			if ( count == 1 or math.mod(count - 1, per) == 0 ) then lineStart = f; end
			prev = f;
		end
	end
end

function MBB_HideButtons()
	MBB_ShowTimeout = -1;
	barOpen = false;
	for i = 1, table.getn(MBB_Buttons) do
		local f = getglobal(MBB_Buttons[i]);
		if ( f and f.ohide ) then f.ohide(f); end
	end
end

function MBB_OnClick(button)
	local o = Opts();
	if ( button == "RightButton" and IsControlKeyDown() ) then
		if ( o.AttachToMinimap == 1 ) then
			local x, y = bag:GetCenter();
			local s = bag:GetEffectiveScale() / UIParent:GetEffectiveScale();
			o.AttachToMinimap = 0;
			o.ButtonPos = { (x or 300) * s, (y or 300) * s };
			MBB_SetButtonPosition();
		else
			MBB_ResetPosition();
		end
	elseif ( button == "RightButton" ) then
		MBB_ToggleOptions();
	else
		if ( MBB_Open() ) then MBB_HideButtons(); else MBB_ShowButtons(); end
	end
end

-- every button on the minimap that can be clicked goes into the bag (except those taken out with Ctrl + Right click)
function MBB_GatherIcons()
	if ( not Minimap ) then return; end
	local children = { Minimap:GetChildren() };
	if ( MinimapBackdrop ) then
		local more = { MinimapBackdrop:GetChildren() };
		for i = 1, table.getn(more) do table.insert(children, more[i]); end
	end
	for i = 1, table.getn(children) do
		local child = children[i];
		local name = child and child.GetName and child:GetName();
		if ( name ) then
			local ignore = false;
			for j = 1, table.getn(MBB_Ignore) do
				if ( string.find(name, MBB_Ignore[j], 1, true) ) then ignore = true; end
			end
			if ( not ignore ) then
				local target = child;
				if ( not Script(child, "OnClick") and not Script(child, "OnMouseUp") and not Script(child, "OnMouseDown") ) then
					local subs = { child:GetChildren() };
					for k = 1, table.getn(subs) do
						if ( subs[k]:GetName() and (Script(subs[k], "OnClick") or Script(subs[k], "OnMouseUp")) ) then target = subs[k]; break; end
					end
				end
				local tname = target:GetName();
				if ( tname and (Script(target, "OnClick") or Script(target, "OnMouseUp") or Script(target, "OnMouseDown")) ) then
					local ok = pcall(MBB_PrepareButton, tname);
					if ( ok and not MBB_IsExcluded(tname) ) then pcall(MBB_AddButton, tname); end
				end
			end
		end
	end
	MBB_SetPositions();
end

-- ---------------------------------------------------------------- options (right click)
local opt;

local function Btn(parent, text, w, onClick)
	local ok, b = pcall(CreateFrame, "Button", nil, parent, "UIPanelButtonTemplate");
	if ( not ok or not b ) then b = CreateFrame("Button", nil, parent); end
	if ( not b:GetFontString() ) then
		local fs = b:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall");
		fs:SetPoint("CENTER", b, "CENTER", 0, 1);
		b:SetFontString(fs);
		b:SetNormalTexture("Interface\\Buttons\\UI-Panel-Button-Up");
		b:SetPushedTexture("Interface\\Buttons\\UI-Panel-Button-Down");
		b:SetHighlightTexture("Interface\\Buttons\\UI-Panel-Button-Highlight");
		local t = { b:GetNormalTexture(), b:GetPushedTexture(), b:GetHighlightTexture() };
		for i = 1, 3 do if ( t[i] ) then t[i]:SetTexCoord(0, 0.625, 0, 0.6875); end end
	end
	b:SetWidth(w);
	b:SetHeight(22);
	b:SetText(text);
	b:SetScript("OnClick", onClick);
	return b;
end

-- a line of the options: its label, its value, and - / + (or a button that goes round the choices)
local function Row(y, label, show, minus, plus)
	local l = opt:CreateFontString(nil, "OVERLAY", "GameFontHighlight");
	l:SetPoint("TOPLEFT", opt, "TOPLEFT", 24, y);
	l:SetText(label);
	local v = opt:CreateFontString(nil, "OVERLAY", "GameFontNormal");
	v:SetPoint("TOPLEFT", opt, "TOPLEFT", 170, y);
	local function Refresh() v:SetText(show()); end
	local m = Btn(opt, "-", 26, function() minus(); Refresh(); MBB_SetPositions(); MBB_SetButtonPosition(); end);
	m:SetPoint("TOPLEFT", opt, "TOPLEFT", 236, y + 4);
	local p = Btn(opt, "+", 26, function() plus(); Refresh(); MBB_SetPositions(); MBB_SetButtonPosition(); end);
	p:SetPoint("TOPLEFT", opt, "TOPLEFT", 264, y + 4);
	table.insert(opt.refresh, Refresh);
end

local function OptionsBuild()
	opt = CreateFrame("Frame", "MBB_OptionsFrame", UIParent);
	opt:SetWidth(320);
	opt:SetHeight(250);
	opt:SetPoint("CENTER", UIParent, "CENTER", 0, 40);
	opt:SetFrameStrata("DIALOG");
	opt:SetBackdrop({ bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
	                  edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
	                  tile = true, tileSize = 32, edgeSize = 32, insets = { left = 11, right = 12, top = 12, bottom = 11 } });
	opt:EnableMouse(true);
	opt:SetMovable(true);
	opt:RegisterForDrag("LeftButton");
	opt:SetScript("OnDragStart", function() this:StartMoving(); end);
	opt:SetScript("OnDragStop", function() this:StopMovingOrSizing(); end);
	opt:Hide();
	table.insert(UISpecialFrames, "MBB_OptionsFrame");
	local title = opt:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge");
	title:SetPoint("TOP", opt, "TOP", 0, -18);
	title:SetText("MinimapButtonBag " .. (MBB_OPTIONS_HEADER or "Options"));
	opt.refresh = {};
	local o = Opts;
	Row(-52, MBB_OPTIONS_SLIDERLABEL or "Collapse Timeout:",
		function() local t = o().CollapseTimeout; return t == 0 and (MBB_OPTIONS_SLIDEROFF or "Off") or (t .. " " .. (MBB_OPTIONS_SLIDERSEK or "sec")); end,
		function() o().CollapseTimeout = math.max(0, o().CollapseTimeout - 1); end,
		function() o().CollapseTimeout = math.min(5, o().CollapseTimeout + 1); end);
	Row(-82, MBB_OPTIONS_EXPANSIONLABEL or "Expand to:",
		function() return DIR_NAMES[o().ExpandDirection] or DIR_NAMES[1]; end,
		function() o().ExpandDirection = math.mod(o().ExpandDirection + 2, 4) + 1; end,
		function() o().ExpandDirection = math.mod(o().ExpandDirection, 4) + 1; end);
	Row(-112, MBB_OPTIONS_MAXBUTTONSLABEL or "Max. Buttons/Line:",
		function() local n = o().MaxButtonsPerLine; return n == 0 and "-" or tostring(n); end,
		function() o().MaxButtonsPerLine = math.max(0, o().MaxButtonsPerLine - 1); end,
		function() o().MaxButtonsPerLine = math.min(20, o().MaxButtonsPerLine + 1); end);
	Row(-142, MBB_OPTIONS_ALTEXPANSIONLABEL or "Alt. Expand to:",
		function() return DIR_NAMES[o().AltExpandDirection] or DIR_NAMES[4]; end,
		function() o().AltExpandDirection = math.mod(o().AltExpandDirection + 2, 4) + 1; end,
		function() o().AltExpandDirection = math.mod(o().AltExpandDirection, 4) + 1; end);
	Row(-172, MBB_OPTIONS_SCALE or "Button's scale",
		function() return o().Scale .. "%"; end,
		function() o().Scale = math.max(50, o().Scale - 10); end,
		function() o().Scale = math.min(150, o().Scale + 10); end);
	local ok = Btn(opt, MBB_OPTIONS_OKBUTTON or "Ok", 120, function() opt:Hide(); end);
	ok:SetPoint("BOTTOM", opt, "BOTTOM", 0, 18);
	opt:SetScript("OnShow", function() for i = 1, table.getn(this.refresh) do this.refresh[i](); end end);
end

function MBB_ToggleOptions()
	if ( not opt ) then
		local ok, err = pcall(OptionsBuild);
		if ( not ok ) then MBB_Print("MBB: the options could not be made: " .. tostring(err)); opt = nil; return; end
	end
	if ( opt:IsShown() ) then opt:Hide(); else opt:Show(); end
end

-- ---------------------------------------------------------------- slash command, loading, the collapse timer
SLASH_MBB1 = "/mbb";
SlashCmdList["MBB"] = function(cmd)
	cmd = string.lower(cmd or "");
	if ( cmd == "buttons" ) then
		MBB_Print("MBB Buttons:");
		for i = 1, table.getn(MBB_Buttons) do MBB_Print("  " .. MBB_Buttons[i]); end
	elseif ( cmd == "reset position" ) then
		MBB_ResetPosition();
	elseif ( cmd == "reset all" ) then
		MBB_Options = Copy(MBB_DefaultOptions);
		while ( table.getn(MBB_Exclude) > 0 ) do MBB_AddButton(MBB_Exclude[1]); end
		MBB_SetPositions();
		MBB_ResetPosition();
	elseif ( cmd == "options" ) then
		MBB_ToggleOptions();
	elseif ( cmd == "open" ) then
		MBB_ShowButtons();
		MBB_Print("MBB: " .. table.getn(MBB_Buttons) .. " buttons in the bar.");
	elseif ( cmd == "close" ) then
		MBB_HideButtons();
	elseif ( cmd == "show" ) then
		MBB_ResetPosition();
		MBB_Print("MBB: the bag button is back by the minimap.");
	else
		MBB_Print("MBB v" .. MBB_Version .. ":");
		MBB_Print(MBB_HELP1 or "Type \"/mbb <cmd>\":");
		MBB_Print(MBB_HELP2 or "  buttons");
		MBB_Print(MBB_HELP3 or "  reset position");
		MBB_Print(MBB_HELP4 or "  reset all");
		MBB_Print("  |c00ffffffoptions|r: the options window");
		MBB_Print("  |c00ffffffshow|r: the bag button back by the minimap");
	end
end

local ev = CreateFrame("Frame", "MBBFrame");
ev:RegisterEvent("VARIABLES_LOADED");
local started, scanned, scannedAgain, checked = GetTime(), false, false, 0;
ev:SetScript("OnEvent", function()
	local o = Opts();
	if ( o.Version ~= MBB_Version ) then
		-- the original kept the button's spot as an offset; this one keeps an angle: start from the default
		MBB_Options = Copy(MBB_DefaultOptions);
	end
	if ( type(MBB_Exclude) ~= "table" ) then MBB_Exclude = {}; end
	-- the first default spot (200) was under the game's damage meter button: move it, unless it was dragged there
	local o2 = Opts();
	if ( o2.AttachToMinimap == 1 and tonumber(o2.ButtonPos[1]) == 200 ) then o2.ButtonPos = { 20, 0 }; end
	MBB_SetButtonPosition();
end);
ev:SetScript("OnUpdate", function()
	local now = GetTime();
	-- the other addons make their buttons while loading: look 5 seconds in, and once more at 15 for late ones
	if ( not scanned and now - started > 5 ) then
		scanned = true;
		pcall(MBB_GatherIcons);
		MBB_SetButtonPosition();
	elseif ( not scannedAgain and now - started > 15 ) then
		scannedAgain = true;
		pcall(MBB_GatherIcons);
	end
	if ( now - checked > 3 and MBB_DragFlag ~= 1 ) then
		checked = now;
		pcall(MBB_SetButtonPosition);                       -- follows the minimap if another addon moved it
		pcall(CheckBag);
	end
	if ( MBB_DragFlag == 1 ) then
		if ( IsMouseButtonDown and not IsMouseButtonDown("LeftButton") ) then MBB_DragFlag = 0; else Drag(); end
	end
	local o = MBB_Options;
	if ( o and o.CollapseTimeout and o.CollapseTimeout ~= 0 ) then
		if ( MBB_ShowTimeout >= o.CollapseTimeout and MBB_Open() ) then MBB_HideButtons(); end
		if ( MBB_ShowTimeout ~= -1 ) then MBB_ShowTimeout = MBB_ShowTimeout + (arg1 or 0); end
	end
end);
MBB_SetButtonPosition();
