-- !BugGrabber for Azeroth Survivors.
-- BugGrabber 2.1.1 by Fritti and Rabbit (credits: Rowne, Ramble, kergoth, ckknight), converted to this game's client:
-- no Ace2 (BugSack is told about each error through BugGrabber.RegisterCallback instead of AceEvent), the game's
-- error handler is used (seterrorhandler) and the old error box only where this client has one, and anything the
-- client may not have (debugstack, date) is checked first.
-- Lua 5.0 / 1.12 interface: no #, scripts read event / arg1.

local USAGE = "Usage: /buggrabber <index>."
local ERROR_INDEX = "The provided index must be a number."
local ERROR_UNKNOWN_INDEX = "The index %d does not exist in the load error table."
local STARTUP_ERRORS = "There were %d startup errors:"
local STARTUP_ERRORS_MANY = "There were %d startup errors, please use /buggrabber <number> to print them."
local UNIQUE_CAPTURE = "BugGrabber captured a unique error:\n%s\n---"
local ADDON_CALL_PROTECTED = "[%s] AddOn '%s' tried to call the protected function '%s'."
local ADDON_CALL_PROTECTED_MATCH = "^%[(.*)%] (AddOn '.*' tried to call the protected function '.*'.)$"

if GetLocale and GetLocale() == "deDE" then
	USAGE = "Benutzung: /buggrabber <index>."
	ERROR_INDEX = "Der zur Verfügung gestellte Index muß eine Zahl sein."
	ERROR_UNKNOWN_INDEX = "Der Index %d existiert nicht in der geladenen Fehlerliste."
	STARTUP_ERRORS = "Es gab %d Errors beim starten:"
	STARTUP_ERRORS_MANY = "Es gab %d Errors beim starten, verwende bitte /buggrabber <nummer>, um sie zu aufzulisten."
	UNIQUE_CAPTURE = "BugGrabber hat einen einzigartigen error aufgezeichnet:\n%s\n---"
end

local f = CreateFrame("Frame", "BugGrabberFrame")
BugGrabber = {}

local gmatch = string.gmatch or string.gfind

BugGrabber.loaded = false
BugGrabber.loadErrors = {}
BugGrabber.bugsackErrors = {}
BugGrabber.errors = {}
BugGrabber.callbacks = {}

BugGrabberDB = { session = 0, save = true, limit = 100, errors = {} }

-- BugSack (or any addon) hears about every error: fn(err, isNew)
function BugGrabber.RegisterCallback(fn)
	table.insert(BugGrabber.callbacks, fn)
end

function BugGrabber.GetDB()
	if not BugGrabber.loaded then return BugGrabber.loadErrors end
	if not BugGrabberDB.save then return BugGrabber.errors end
	return BugGrabberDB.errors
end

local function Stamp()
	if date then
		local ok, s = pcall(date, "%Y/%m/%d %H:%M:%S")
		if ok and s then return s end
	end
	return string.format("%.0f s", GetTime and GetTime() or 0)
end

local grabbing = false

function BugGrabber.GrabError(_, err)
	if grabbing then return end                    -- an error while grabbing an error: not again
	grabbing = true
	err = tostring(err or "")
	if debugstack then
		local ok, stack = pcall(debugstack, 4)
		if ok and stack then err = err .. "\n" .. stack end
	end
	if string.sub(err, -1) ~= "\n" then err = err .. "\n" end

	-- shorten the paths to their last folder and file
	local errmsg = ""
	for trace in gmatch(err, "(.-)\n") do
		local match, _, path, file, line, msg
		match, _, path, file, line, msg = string.find(trace, "^.-([^\\/]+[\\/])([^\\/]-)(:%d+):(.*)$")
		if not match then
			match, _, path, file, line, msg = string.find(trace, "^%[string \".-([^\\/]+[\\/])([^\\/]-)\"%](:%d+):(.*)$")
		end
		if not match then
			match, _, file, line, msg = string.find(trace, "^%[string (\".-\")%](:%d+):(.*)$")
			if match then path = "<string>:" end
		end
		if not match then
			match, _, msg = string.find(trace, "^%[C%]:(.*)$")
			if match then path, file, line = "<in C code>", "", "" end
		end
		if not match then
			match, _, file, msg = string.find(trace, ADDON_CALL_PROTECTED_MATCH)
			if match then path, file, line = "<event>", "ADDON_ACTION_BLOCKED", "" end
		end
		if match then
			errmsg = errmsg .. (path or "") .. (file or "") .. (line or "") .. ":" .. (msg or "") .. "\n"
		elseif trace ~= "" then
			errmsg = errmsg .. trace .. "\n"
		end
	end
	pcall(BugGrabber.SaveError, errmsg)
	grabbing = false
end

local function Same(list, oe)
	for i = 1, table.getn(list) do
		local e = list[i]
		if e.message == oe.message and e.session == oe.session then return e end
	end
	return nil
end

function BugGrabber.SaveError(message)
	local oe = { message = message .. "\n  ---", session = BugGrabberDB.session, time = Stamp(), counter = 1 }
	-- long strings in the saved variables can break them: cut it
	if string.len(oe.message) > 980 then oe.message = string.sub(oe.message, 1, 980) end

	local added = false
	local db = BugGrabber.GetDB()
	local old = Same(db, oe)
	if old then
		old.counter = (tonumber(old.counter) or 1) + 1
		oe.counter, oe.time = old.counter, old.time
	else
		table.insert(db, oe)
		added = true
	end
	if type(BugGrabber.bugsackErrors) == "table" then
		local o2 = Same(BugGrabber.bugsackErrors, oe)
		if o2 then o2.counter = (tonumber(o2.counter) or 1) + 1
		else table.insert(BugGrabber.bugsackErrors, oe); added = true end
	end
	while table.getn(db) > (BugGrabberDB.limit or 100) do table.remove(db, 1) end

	-- tell whoever listens (BugSack); with no one listening, a new error goes to the chat so it is not lost
	if not BugGrabber.bugsackErrors then
		if table.getn(BugGrabber.callbacks) > 0 then
			for i = 1, table.getn(BugGrabber.callbacks) do pcall(BugGrabber.callbacks[i], oe, added) end
		elseif added and DEFAULT_CHAT_FRAME then
			DEFAULT_CHAT_FRAME:AddMessage(string.format(UNIQUE_CAPTURE, oe.message))
		end
	end
end

function BugGrabber.AddonLoaded()
	BugGrabber.loaded = true
	if type(BugGrabberDB) ~= "table" then BugGrabberDB = {} end
	if type(BugGrabberDB.session) ~= "number" then BugGrabberDB.session = 0 end
	if type(BugGrabberDB.errors) ~= "table" then BugGrabberDB.errors = {} end
	if type(BugGrabberDB.limit) ~= "number" then BugGrabberDB.limit = 100 end
	if type(BugGrabberDB.save) ~= "boolean" then BugGrabberDB.save = true end
	BugGrabberDB.session = BugGrabberDB.session + 1
	local db = BugGrabber.GetDB()
	local loadErrs = BugGrabber.loadErrors or {}
	while table.getn(db) > 0 and table.getn(db) + table.getn(loadErrs) > BugGrabberDB.limit do table.remove(db, 1) end
	for i = 1, table.getn(loadErrs) do
		loadErrs[i].session = BugGrabberDB.session
		table.insert(db, loadErrs[i])
	end
	BugGrabber.loadErrors = nil
end

local temporaryList = nil
function BugGrabber.PrintLoadError(index)
	if not index or tostring(index) == "" then DEFAULT_CHAT_FRAME:AddMessage(USAGE) return end
	index = tonumber(index)
	if not index then DEFAULT_CHAT_FRAME:AddMessage(ERROR_INDEX) return end
	if not temporaryList or not temporaryList[index] then
		DEFAULT_CHAT_FRAME:AddMessage(string.format(ERROR_UNKNOWN_INDEX, index))
		return
	end
	DEFAULT_CHAT_FRAME:AddMessage(tostring(index) .. ". " .. tostring(temporaryList[index].message))
end

-- at login: the errors from while the game loaded go to BugSack (it takes them and clears the list); with no
-- BugSack they are printed
function BugGrabber.PlayerLogin()
	local err = BugGrabber.bugsackErrors
	if type(err) == "table" and table.getn(err) > 0 then
		local num = table.getn(err)
		if num > 4 then
			DEFAULT_CHAT_FRAME:AddMessage(string.format(STARTUP_ERRORS_MANY, num))
			SlashCmdList["BUGGRABBER"] = BugGrabber.PrintLoadError
			SLASH_BUGGRABBER1 = "/buggrabber"
			temporaryList = {}
			for i = 1, num do table.insert(temporaryList, err[i]) end
		else
			DEFAULT_CHAT_FRAME:AddMessage(string.format(STARTUP_ERRORS, num))
			for i = 1, num do DEFAULT_CHAT_FRAME:AddMessage(tostring(i) .. ". " .. tostring(err[i].message)) end
		end
	end
	BugGrabber.bugsackErrors = nil
end

f:SetScript("OnEvent", function()
	if event == "ADDON_LOADED" and arg1 == "!BugGrabber" then
		BugGrabber.AddonLoaded()
	elseif event == "VARIABLES_LOADED" and not BugGrabber.loaded then
		BugGrabber.AddonLoaded()
	elseif event == "PLAYER_LOGIN" or event == "PLAYER_ENTERING_WORLD" then
		if BugGrabber.bugsackErrors then BugGrabber.PlayerLogin() end
	elseif event == "ADDON_ACTION_BLOCKED" or event == "ADDON_ACTION_FORBIDDEN" then
		BugGrabber.GrabError(nil, string.format(ADDON_CALL_PROTECTED, event, tostring(arg1), tostring(arg2)))
	end
end)

function BugGrabber.GetSave() return BugGrabberDB.save end

function BugGrabber.ToggleSave()
	BugGrabberDB.save = not BugGrabberDB.save
	if BugGrabberDB.save then
		BugGrabberDB.errors = BugGrabber.errors
		BugGrabber.errors = {}
	else
		BugGrabber.errors = BugGrabberDB.errors
		BugGrabberDB.errors = {}
	end
end

function BugGrabber.GetLimit() return BugGrabberDB.limit end

function BugGrabber.SetLimit(l)
	l = tonumber(l)
	if not l or l < 10 or l > 100 then return false end
	BugGrabberDB.limit = math.floor(l)
	local db = BugGrabber.GetDB()
	while table.getn(db) > l do table.remove(db, 1) end
	return true
end

-- catch the errors: the game's error handler (and the 1.12 error box, where this client has one)
BugGrabber.olderrorhandler = geterrorhandler and geterrorhandler()
if seterrorhandler then
	pcall(seterrorhandler, function(e) BugGrabber.GrabError(nil, e) end)
	BugGrabber.oldseterrorhandler = seterrorhandler
	seterrorhandler = function() end                   -- other addons cannot take the errors away
end
if type(ScriptErrors_Message) == "table" and type(ScriptErrors) == "table" then
	BugGrabber.oldset = ScriptErrors_Message.SetText
	BugGrabber.oldshow = ScriptErrors.Show
	ScriptErrors_Message.SetText = BugGrabber.GrabError
	ScriptErrors.Show = function() end
end

f:RegisterEvent("ADDON_LOADED")
f:RegisterEvent("VARIABLES_LOADED")
f:RegisterEvent("PLAYER_LOGIN")
f:RegisterEvent("PLAYER_ENTERING_WORLD")
pcall(function() f:RegisterEvent("ADDON_ACTION_BLOCKED") end)
pcall(function() f:RegisterEvent("ADDON_ACTION_FORBIDDEN") end)
