-- BugSack for Azeroth Survivors.
-- BugSack 2.1.0 by Fritti and Rabbit (developers: Rowne, Ramble, industrial, Fritti, kergoth, Rabbit; credits:
-- Gamefaq, thomasmo, damjau), converted to this game's client. The original needs Ace2, Dewdrop, Tablet and FuBar
-- and builds its window in XML; here everything is plain Lua:
--   * the window: the error, "Error 3 of 7", First / Previous / Next / Last (the text can be selected and copied)
--   * /bugsack (or /bs) with the same commands: show / list (current, session, previous, all or a session number),
--     auto, chat, msg, mute, save, limit <10-100>, bug, reset; /bugsack options opens the options window
--   * a minimap button in place of the FuBar plugin: how many errors this session; click: this session's errors,
--     right click: the options
-- Lua 5.0 / 1.12 interface: no #, scripts read this / arg1.

local L = {
	["You have no errors, yay!"] = "You have no errors, yay!",
	["List of errors:"] = "List of errors:",
	["An error has been generated."] = "An error has been generated.",
	["All errors were wiped."] = "All errors were wiped.",
	["An error has been recorded."] = "An error has been recorded.",
	["%d errors have been recorded."] = "%d errors have been recorded.",
	[" (... more ...)"] = " (... more ...)",
	["No errors found"] = "No errors found",
	["Error %d of %d"] = "Error %d of %d",
	[" (viewing last error)"] = " (viewing last error)",
	[" (viewing session errors)"] = " (viewing session errors)",
	[" (viewing previous session errors)"] = " (viewing previous session errors)",
	[" (viewing all errors)"] = " (viewing all errors)",
	[" (viewing errors for session %d)"] = " (viewing errors for session %d)",
}
if GetLocale and GetLocale() == "deDE" then
	L["You have no errors, yay!"] = "Keine Fehler, juhu!"
	L["List of errors:"] = "Liste der Fehler:"
	L["An error has been generated."] = "Ein Fehler wurde erzeugt."
	L["All errors were wiped."] = "Alle Fehler wurden gelöscht."
	L["An error has been recorded."] = "Ein Fehler wurde aufgezeichnet."
	L["%d errors have been recorded."] = "%d Fehler wurden aufgezeichnet."
	L["No errors found"] = "Keine Fehler gefunden"
	L["Error %d of %d"] = "Fehler %d von %d"
end

BugSack = {}
local db                                                -- BugSackDB (per account)
local NAME = "BugSack"

function BugSack:Print(fmt, a, b)
	if not DEFAULT_CHAT_FRAME then return end
	local s = fmt
	if a ~= nil then s = string.format(fmt, a, b) end
	DEFAULT_CHAT_FRAME:AddMessage("|cff33ff99" .. NAME .. "|r: " .. tostring(s))
end

local function DB()
	if type(BugSackDB) ~= "table" then BugSackDB = {} end
	db = BugSackDB
	if type(db.profile) ~= "table" then db.profile = {} end
	local p = db.profile
	if p.chatframe == nil then p.chatframe = true end   -- a line in the chat when an error happens
	if p.mute == nil then p.mute = false end
	return db
end

-- ---------------------------------------------------------------- the errors
function BugSack:GetErrors(which)
	local all = BugGrabber.GetDB()
	local cs = BugGrabberDB.session
	local errs = {}
	if which == "current" then
		local n = table.getn(all)
		if n ~= 0 and all[n].session == cs then table.insert(errs, all[n]) end
		return errs
	end
	for i = 1, table.getn(all) do
		local e = all[i]
		if which == "all" or (which == "session" and cs == tonumber(e.session))
		   or (which == "previous" and cs - 1 == tonumber(e.session)) or (tonumber(which) and tonumber(which) == tonumber(e.session)) then
			table.insert(errs, e)
		end
	end
	return errs
end

function BugSack:ColorError(err)
	local r = err
	r = string.gsub(r, "|([^chHr])", "||%1")
	r = string.gsub(r, "|$", "||")
	r = string.gsub(r, ":(%d+): ", ":|cff00ff00%1|r: ")
	r = string.gsub(r, "\n(.-):", "\n|cffeda55f%1|r:")
	r = string.gsub(r, "([`'\"])(.-)([`'\"])", "|cff8888ff%1%2%3|r")
	r = string.gsub(r, "^(.-):", "|cffeda55f%1|r:")
	return r
end

function BugSack:FormatError(err)
	if type(err) ~= "table" or not err.time or not err.session then
		return "Not a valid BugGrabber error."
	end
	return string.format("|cff999999[%s-%d-x%d]|r: %s", tostring(err.time), tonumber(err.session) or 0, tonumber(err.counter) or 1,
	                     self:ColorError(tostring(err.message)))
end

-- ---------------------------------------------------------------- the window
local win, text, caption, btn = nil, nil, nil, {}

local function Button(parent, label, w, onClick)
	local ok, b = pcall(CreateFrame, "Button", nil, parent, "UIPanelButtonTemplate")
	if not ok or not b then b = CreateFrame("Button", nil, parent) end
	if not b:GetFontString() then
		local fs = b:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall")
		fs:SetPoint("CENTER", b, "CENTER", 0, 1)
		b:SetFontString(fs)
		b:SetNormalTexture("Interface\\Buttons\\UI-Panel-Button-Up")
		b:SetPushedTexture("Interface\\Buttons\\UI-Panel-Button-Down")
		b:SetHighlightTexture("Interface\\Buttons\\UI-Panel-Button-Highlight")
		local t = { b:GetNormalTexture(), b:GetPushedTexture(), b:GetHighlightTexture() }
		for i = 1, 3 do if t[i] then t[i]:SetTexCoord(0, 0.625, 0, 0.6875) end end
	end
	b:SetWidth(w)
	b:SetHeight(24)
	b:SetText(label)
	b:SetScript("OnClick", onClick)
	return b
end

local function Enable(b, on)
	if on then b:Enable() else b:Disable() end
end

local function Build()
	win = CreateFrame("Frame", "BugSackFrame", UIParent)
	win:SetWidth(520)
	win:SetHeight(420)
	win:SetPoint("CENTER", UIParent, "CENTER", 0, 30)
	win:SetFrameStrata("DIALOG")
	win:SetBackdrop({ bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background", edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border",
	                  tile = true, tileSize = 16, edgeSize = 16, insets = { left = 5, right = 5, top = 5, bottom = 5 } })
	win:EnableMouse(true)
	win:SetMovable(true)
	win:RegisterForDrag("LeftButton")
	win:SetScript("OnDragStart", function() this:StartMoving() end)
	win:SetScript("OnDragStop", function() this:StopMovingOrSizing() end)
	win:Hide()
	table.insert(UISpecialFrames, "BugSackFrame")
	caption = win:CreateFontString("BugSackErrorText", "OVERLAY", "GameFontHighlight")
	caption:SetPoint("TOPLEFT", win, "TOPLEFT", 12, -12)
	caption:SetJustifyH("LEFT")
	local close = CreateFrame("Button", nil, win, "UIPanelCloseButton")
	close:SetPoint("TOPRIGHT", win, "TOPRIGHT", -2, -2)

	-- the text: an edit box (so it can be copied) in a scroll frame; the mouse wheel scrolls it
	local ok, scroll = pcall(CreateFrame, "ScrollFrame", "BugSackFrameScroll", win, "UIPanelScrollFrameTemplate")
	if not ok or not scroll then scroll = CreateFrame("ScrollFrame", "BugSackFrameScroll", win) end
	scroll:SetWidth(470)
	scroll:SetHeight(320)
	scroll:SetPoint("TOPLEFT", win, "TOPLEFT", 14, -34)
	local okEdit, edit = pcall(CreateFrame, "EditBox", "BugSackFrameScrollText", scroll)
	if okEdit and edit and edit.SetMultiLine then
		edit:SetMultiLine(true)
		edit:SetAutoFocus(false)
		edit:SetFontObject(ChatFontNormal)
		edit:SetWidth(460)
		edit:SetHeight(320)
		edit:SetScript("OnEscapePressed", function() win:Hide() end)
		edit:SetScript("OnTextChanged", function()                 -- read only: typing puts the error back
			if BugSack.str and this:GetText() ~= BugSack.str then this:SetText(BugSack.str) end
			if scroll.UpdateScrollChildRect then scroll:UpdateScrollChildRect() end
		end)
	else
		edit = CreateFrame("Frame", "BugSackFrameScrollText", scroll)
		edit:SetWidth(460)
		edit:SetHeight(320)
		edit.fs = edit:CreateFontString(nil, "OVERLAY", "ChatFontNormal")
		edit.fs:SetPoint("TOPLEFT", edit, "TOPLEFT", 0, 0)
		edit.fs:SetWidth(460)
		edit.fs:SetJustifyH("LEFT")
		edit.SetText = function(self, s) self.fs:SetText(s); self:SetHeight(math.max(320, (self.fs:GetHeight() or 0) + 10)) end
	end
	scroll:SetScrollChild(edit)
	scroll:EnableMouseWheel(true)
	scroll:SetScript("OnMouseWheel", function()
		local max = math.max(0, edit:GetHeight() - this:GetHeight())
		this:SetVerticalScroll(math.max(0, math.min(max, this:GetVerticalScroll() - arg1 * 40)))
	end)
	text = edit

	btn.prev = Button(win, "Previous", 80, function() BugSack.cur = BugSack.cur - 1; BugSack:UpdateFrameText() end)
	btn.prev:SetPoint("BOTTOMLEFT", win, "BOTTOMLEFT", 16, 16)
	btn.next = Button(win, "Next", 80, function() BugSack.cur = BugSack.cur + 1; BugSack:UpdateFrameText() end)
	btn.next:SetPoint("BOTTOMLEFT", win, "BOTTOMLEFT", 100, 16)
	btn.first = Button(win, "First", 80, function() BugSack.cur = math.min(BugSack.max, 1); BugSack:UpdateFrameText() end)
	btn.first:SetPoint("BOTTOMRIGHT", win, "BOTTOMRIGHT", -100, 16)
	btn.last = Button(win, "Last", 80, function() BugSack.cur = BugSack.max; BugSack:UpdateFrameText() end)
	btn.last:SetPoint("BOTTOMRIGHT", win, "BOTTOMRIGHT", -16, 16)
	local clear = Button(win, "Clear", 70, function() BugSack:Reset(); BugSack:ShowFrame(BugSack.which) end)
	clear:SetPoint("BOTTOM", win, "BOTTOM", 0, 16)
end

function BugSack:UpdateFrameText()
	local cap
	if self.cur == 0 then
		self.str = L["You have no errors, yay!"]
		cap = L["No errors found"]
	else
		self.str = self:FormatError(self.errs[self.cur])
		cap = string.format(L["Error %d of %d"], self.cur, self.max)
	end
	local w = self.which
	if w == "current" then cap = cap .. L[" (viewing last error)"]
	elseif w == "session" then cap = cap .. L[" (viewing session errors)"]
	elseif w == "previous" then cap = cap .. L[" (viewing previous session errors)"]
	elseif w == "all" then cap = cap .. L[" (viewing all errors)"]
	else cap = cap .. string.format(L[" (viewing errors for session %d)"], tonumber(w) or 0) end
	caption:SetText(cap)
	if string.len(self.str) > 4000 then self.str = string.sub(self.str, 1, 3950) .. L[" (... more ...)"] end
	text:SetText(self.str)
	Enable(btn.next, self.cur < self.max)
	Enable(btn.last, self.cur < self.max)
	Enable(btn.prev, self.cur > 1)
	Enable(btn.first, self.cur > 1)
end

function BugSack:ShowFrame(which, nr)
	if not win then
		local ok, err = pcall(Build)
		if not ok then self:Print("the window could not be made: " .. tostring(err)); win = nil; self:ListErrors(which); return end
	end
	self.which = which or "session"
	self.errs = self:GetErrors(self.which)
	self.max = table.getn(self.errs)
	self.cur = nr and math.min(self.max, math.abs(nr)) or math.min(self.max, 1)
	self:UpdateFrameText()
	win:Show()
end

function BugSack:ShowCurrent() self:ShowFrame("current") end
function BugSack:ShowSession() self:ShowFrame("session") end

function BugSack:ListErrors(which)
	local errs = self:GetErrors(which)
	if table.getn(errs) == 0 then self:Print(L["You have no errors, yay!"]) return end
	self:Print(L["List of errors:"])
	for i = 1, table.getn(errs) do self:Print("%d. %s", i, self:FormatError(errs[i])) end
end

function BugSack:Reset()
	BugGrabber.loadErrors = nil
	BugGrabber.errors = {}
	BugGrabberDB.errors = {}
	self:Print(L["All errors were wiped."])
	if BugSack.Mini then BugSack.Mini.Count() end
end

-- each new error: a sound, the window if "auto", a line in the chat
function BugSack:OnError(err, isNew)
	local p = DB().profile
	if not isNew then return end
	if not p.mute then
		local ok = PlaySoundFile and pcall(PlaySoundFile, "Interface\\AddOns\\BugSack\\error.wav")
		if not ok and PlaySound then pcall(PlaySound, "igQuestFailed") end
	end
	if p.auto then self:ShowCurrent() end
	if p.chatframe and p.showmsg then self:Print(self:FormatError(err))
	elseif p.chatframe then self:Print(L["An error has been recorded."]) end
	if win and win:IsShown() then self:ShowFrame(self.which, self.cur) end
	if BugSack.Mini then BugSack.Mini.Count() end
end

-- ---------------------------------------------------------------- options window (right click on the minimap button)
local opt
local function OptionsBuild()
	opt = CreateFrame("Frame", "BugSackOptionsFrame", UIParent)
	opt:SetWidth(320)
	opt:SetHeight(270)
	opt:SetPoint("CENTER", UIParent, "CENTER", 0, 40)
	opt:SetFrameStrata("DIALOG")
	opt:SetBackdrop({ bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background", edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
	                  tile = true, tileSize = 32, edgeSize = 32, insets = { left = 11, right = 12, top = 12, bottom = 11 } })
	opt:EnableMouse(true)
	opt:SetMovable(true)
	opt:RegisterForDrag("LeftButton")
	opt:SetScript("OnDragStart", function() this:StartMoving() end)
	opt:SetScript("OnDragStop", function() this:StopMovingOrSizing() end)
	opt:Hide()
	table.insert(UISpecialFrames, "BugSackOptionsFrame")
	local title = opt:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge")
	title:SetPoint("TOP", opt, "TOP", 0, -18)
	title:SetText("BugSack")
	opt.rows = {}
	local y = -46
	local function Toggle(label, get, set)
		local ok, cb = pcall(CreateFrame, "CheckButton", nil, opt, "UICheckButtonTemplate")
		if not ok or not cb then
			cb = CreateFrame("CheckButton", nil, opt)
			cb:SetNormalTexture("Interface\\Buttons\\UI-CheckBox-Up")
			cb:SetPushedTexture("Interface\\Buttons\\UI-CheckBox-Down")
			cb:SetHighlightTexture("Interface\\Buttons\\UI-CheckBox-Highlight")
			cb:SetCheckedTexture("Interface\\Buttons\\UI-CheckBox-Check")
		end
		cb:SetWidth(26)
		cb:SetHeight(26)
		cb:SetPoint("TOPLEFT", opt, "TOPLEFT", 24, y)
		local fs = opt:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
		fs:SetPoint("LEFT", cb, "RIGHT", 4, 1)
		fs:SetText(label)
		cb:SetScript("OnClick", function() set(this:GetChecked() and true or false) end)
		table.insert(opt.rows, function() cb:SetChecked(get() and 1 or nil) end)
		y = y - 28
	end
	local p = function() return DB().profile end
	Toggle("Auto popup", function() return p().auto end, function(v) p().auto = v end)
	Toggle("Chatframe output", function() return p().chatframe end, function(v) p().chatframe = v end)
	Toggle("Errors to chatframe", function() return p().showmsg end, function(v) p().showmsg = v end)
	Toggle("Mute", function() return p().mute end, function(v) p().mute = v end)
	Toggle("Save errors", function() return BugGrabber.GetSave() end, function(v) if BugGrabber.GetSave() ~= v then BugGrabber.ToggleSave() end end)
	local lim = opt:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
	lim:SetPoint("TOPLEFT", opt, "TOPLEFT", 30, y - 6)
	local function LimText() lim:SetText("Limit: " .. tostring(BugGrabber.GetLimit()) .. " errors") end
	table.insert(opt.rows, LimText)
	local minus = Button(opt, "-", 26, function() BugGrabber.SetLimit(BugGrabber.GetLimit() - 10); LimText() end)
	minus:SetPoint("TOPLEFT", opt, "TOPLEFT", 200, y - 2)
	local plus = Button(opt, "+", 26, function() BugGrabber.SetLimit(BugGrabber.GetLimit() + 10); LimText() end)
	plus:SetPoint("TOPLEFT", opt, "TOPLEFT", 228, y - 2)
	local clear = Button(opt, "Clear errors", 110, function() BugSack:Reset() end)
	clear:SetPoint("BOTTOMLEFT", opt, "BOTTOMLEFT", 24, 18)
	local okb = Button(opt, "Ok", 90, function() opt:Hide() end)
	okb:SetPoint("BOTTOMRIGHT", opt, "BOTTOMRIGHT", -24, 18)
	opt:SetScript("OnShow", function() for i = 1, table.getn(this.rows) do this.rows[i]() end end)
end

function BugSack:ToggleOptions()
	if not opt then
		local ok, err = pcall(OptionsBuild)
		if not ok then self:Print("the options could not be made: " .. tostring(err)); opt = nil; return end
	end
	if opt:IsShown() then opt:Hide() else opt:Show() end
end

-- ---------------------------------------------------------------- the minimap button (instead of the FuBar plugin)
local function MakeMini()
	local RING = 78
	local mini = CreateFrame("Frame", "BugSackMinimapButton", Minimap or UIParent)
	mini:SetWidth(33)
	mini:SetHeight(33)
	mini:SetFrameStrata("MEDIUM")
	mini:SetFrameLevel(20)
	mini:EnableMouse(true)
	mini.icon = mini:CreateTexture(nil, "BACKGROUND")
	mini.icon:SetWidth(21)
	mini.icon:SetHeight(21)
	mini.icon:SetPoint("CENTER", mini, "CENTER", -1, 1)
	mini.icon:SetTexture("Interface\\Icons\\INV_Misc_Bag_07_Green")
	mini.icon:SetTexCoord(0.07, 0.93, 0.07, 0.93)
	local border = mini:CreateTexture(nil, "ARTWORK")
	border:SetWidth(52)
	border:SetHeight(52)
	border:SetPoint("TOPLEFT", mini, "TOPLEFT", 0, 0)
	border:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder")
	mini.count = mini:CreateFontString(nil, "OVERLAY", "NumberFontNormalSmall")
	mini.count:SetPoint("BOTTOMRIGHT", mini, "BOTTOMRIGHT", -4, 4)
	local function M()
		local d = DB()
		if type(d.minimap) ~= "table" then d.minimap = { angle = 300 } end
		return d.minimap
	end
	function mini.Place()
		local m = M()
		mini:ClearAllPoints()
		local a = math.rad(tonumber(m.angle) or 300)
		mini:SetPoint("CENTER", Minimap or UIParent, "CENTER", RING * math.cos(a), RING * math.sin(a))
		if m.hide then mini:Hide() else mini:Show() end
	end
	function mini.Count()
		local n = table.getn(BugSack:GetErrors("session"))
		mini.count:SetText(n > 0 and tostring(n) or "")
	end
	mini:SetScript("OnMouseDown", function() mini.down = true; mini.moved = false end)
	mini:SetScript("OnMouseUp", function()
		mini.down = false
		if mini.moved then return end
		if arg1 == "RightButton" then BugSack:ToggleOptions()
		elseif win and win:IsShown() then win:Hide()
		else BugSack:ShowSession() end
	end)
	mini:SetScript("OnUpdate", function()
		if not mini.down or not Minimap then return end
		local x, y = GetCursorPosition()
		local s = Minimap:GetEffectiveScale()
		local cx, cy = Minimap:GetCenter()
		if not cx then return end
		local a = math.deg(math.atan2(y / s - cy, x / s - cx))
		if math.abs(a - (tonumber(M().angle) or 300)) > 2 then mini.moved = true; M().angle = a; mini.Place() end
	end)
	mini:SetScript("OnEnter", function()
		GameTooltip:SetOwner(mini, "ANCHOR_LEFT")
		GameTooltip:AddLine("BugSack")
		GameTooltip:AddLine(table.getn(BugSack:GetErrors("session")) .. " errors this session", 1, 1, 1)
		GameTooltip:AddLine("Click: this session's errors. Right click: options. Drag: move.", 0.6, 0.6, 0.6)
		GameTooltip:Show()
	end)
	mini:SetScript("OnLeave", function() GameTooltip:Hide() end)
	mini.Place()
	return mini
end

-- ---------------------------------------------------------------- slash command
local function Which(s)
	if s == "" or s == "session" then return "session" end
	if s == "current" or s == "curr" or s == "previous" or s == "all" then return s == "curr" and "current" or s end
	if tonumber(s) then return tonumber(s) end
	return nil
end

SLASH_BUGSACK1 = "/bugsack"
SLASH_BUGSACK2 = "/bs"
SlashCmdList["BUGSACK"] = function(msg)
	msg = string.lower(msg or "")
	local _, _, cmd, rest = string.find(msg, "^(%S*)%s*(.-)%s*$")
	cmd, rest = cmd or "", rest or ""
	local p = DB().profile
	if cmd == "" then BugSack:ShowSession()
	elseif cmd == "show" then BugSack:ShowFrame(Which(rest) or "session")
	elseif cmd == "list" then BugSack:ListErrors(Which(rest) or "session")
	elseif cmd == "auto" then p.auto = not p.auto; BugSack:Print("Auto popup: " .. (p.auto and "on" or "off"))
	elseif cmd == "chat" then p.chatframe = not p.chatframe; BugSack:Print("Chatframe output: " .. (p.chatframe and "on" or "off"))
	elseif cmd == "msg" then p.showmsg = not p.showmsg; BugSack:Print("Errors to chatframe: " .. (p.showmsg and "on" or "off"))
	elseif cmd == "mute" then p.mute = not p.mute; BugSack:Print("Mute: " .. (p.mute and "on" or "off"))
	elseif cmd == "save" then BugGrabber.ToggleSave(); BugSack:Print("Save errors: " .. (BugGrabber.GetSave() and "on" or "off"))
	elseif cmd == "limit" then
		if BugGrabber.SetLimit(tonumber(rest)) then BugSack:Print("Limit: " .. BugGrabber.GetLimit())
		else BugSack:Print("the limit is a number from 10 to 100.") end
	elseif cmd == "bug" then
		BugSack:Print(L["An error has been generated."])
		BugSack_BugGeneratedByBugSack()
	elseif cmd == "reset" then BugSack:Reset()
	elseif cmd == "options" then BugSack:ToggleOptions()
	elseif cmd == "minimap" then
		local m = DB().minimap or {}
		m.hide = not m.hide
		DB().minimap = m
		if BugSack.Mini then BugSack.Mini.Place() end
	else
		BugSack:Print("/bugsack (or /bs): show|list [current|session|previous|all|<session>], auto, chat, msg, mute, save, limit <10-100>, bug, reset, options, minimap")
	end
end

-- a test error (/bugsack bug)
function BugSack_BugGeneratedByBugSack()
	local t = nil
	return t.fake                                          -- BugSack generated this fake error
end

-- ---------------------------------------------------------------- start
local ev = CreateFrame("Frame")
ev:RegisterEvent("VARIABLES_LOADED")
ev:SetScript("OnEvent", function()
	DB()
	-- the errors from while the game loaded, kept by BugGrabber until now
	if BugGrabber and type(BugGrabber.bugsackErrors) == "table" then
		local list = BugGrabber.bugsackErrors
		BugGrabber.bugsackErrors = nil
		if table.getn(list) > 0 then BugSack:Print(L["%d errors have been recorded."], table.getn(list)) end
	end
	local ok, m = pcall(MakeMini)
	if ok then BugSack.Mini = m; m.Count() else BugSack:Print("the minimap button could not be made: " .. tostring(m)) end
end)
if BugGrabber and BugGrabber.RegisterCallback then
	BugGrabber.RegisterCallback(function(err, isNew) BugSack:OnError(err, isNew) end)
end
