-- DoomWithin for the Azeroth Survivors client (Lua 5.0, 1.12 API): the helpers the retail code expects.
local floor = math.floor

-- Lua 5.1 a % b (floored, sign of b); math.mod in 5.0 truncates instead
function DWMod(a, b)
    return a - floor(a / b) * b
end

function DWPrint(...)
    local s = ""
    for i = 1, arg.n do
        s = s .. (i > 1 and " " or "") .. tostring(arg[i])
    end
    if DEFAULT_CHAT_FRAME then DEFAULT_CHAT_FRAME:AddMessage(s) end
end

function DWClockMs()
    return GetTime() * 1000
end

-- the emulator expects unsigned 32-bit results from bit.*; wrap the client's library if it returns signed ones
if type(bit) ~= "table" then
    DWPrint("|cffff0000DoomWithin: this client has no bit library, Doom cannot run.|r")
else
    local U = 4294967296
    if bit.bor(2147483648, 0) < 0 or bit.bnot(0) < 0 then
        local raw = {}
        for _, n in ipairs({"band", "bor", "bxor", "bnot", "lshift", "rshift", "arshift"}) do
            raw[n] = bit[n]
        end
        local function u(x) if x < 0 then return x + U end return x end
        bit.band = function(a, b) return u(raw.band(a, b)) end
        bit.bor = function(a, b) return u(raw.bor(a, b)) end
        bit.bxor = function(a, b) return u(raw.bxor(a, b)) end
        bit.bnot = function(a) return u(raw.bnot(a)) end
        bit.lshift = function(a, b) return u(raw.lshift(a, b)) end
        bit.rshift = function(a, b) return u(raw.rshift(a, b)) end
        bit.arshift = function(a, b) return u(raw.arshift(a, b)) end
    end
end

-- RunNextFrame / C_Timer.After on one OnUpdate frame
local queue, timers = {}, {}
local ticker = CreateFrame("Frame")
ticker:SetScript("OnUpdate", function()
    if table.getn(queue) > 0 then
        local run = queue
        queue = {}
        for i = 1, table.getn(run) do run[i]() end
    end
    if table.getn(timers) > 0 then
        local now, keep, due = GetTime(), {}, {}
        for i = 1, table.getn(timers) do
            local t = timers[i]
            if t[1] <= now then table.insert(due, t[2]) else table.insert(keep, t) end
        end
        timers = keep
        for i = 1, table.getn(due) do due[i]() end
    end
end)

function DWRunNextFrame(fn)
    table.insert(queue, fn)
end

function DWAfter(sec, fn)
    table.insert(timers, {GetTime() + sec, fn})
end
