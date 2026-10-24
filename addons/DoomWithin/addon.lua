
-- params
local ProfilingEnabled = false
local ProfilingSamplingRate = 100 -- every N instructions

-- globals
local game = nil
DW_IsRunning = false

local frame = CreateFrame("Frame")
frame:RegisterEvent("ADDON_LOADED")
frame:RegisterEvent("PLAYER_LOGOUT")

frame:SetScript("OnEvent", function()
    if event == "ADDON_LOADED" and arg1 == "DoomWithin" then
        DoomWithinProfiling = {}
    elseif event == "PLAYER_LOGOUT" then
        if game ~= nil and ProfilingEnabled then
            DoomWithinProfiling = game.cpu.profiling_log
        end
    end
end)


local function StartDoom()
    
    DWPrint("Starting Doom...")
    if DW_IsRunning then
        DWPrint("Doom is already running.")
        return
    end
    game = _DW_Game()
    if ProfilingEnabled then
        game.profiling_enabled = true
        game.profiling_sampling_rate = ProfilingSamplingRate
    end
    game:Start()
    DW_IsRunning = true
end

local function StopDoom()
    if not DW_IsRunning then
        DWPrint("Doom is not running.")
        return
    end

    game:Stop()
    DW_IsRunning = false
    DWPrint("Doom stopped.")
end


local function AddonCommands(msg, editbox)
    local _, _, cmd, args = string.find(msg, "%s?(%w+)%s?(.*)")
    if cmd == "start" then
        StartDoom()
    elseif cmd == "stop" then
        StopDoom()
    else
        DWPrint("Syntax: /doomwithin (start|stop)");
    end
end
  
SLASH_DOOMWITHIN1, SLASH_DOOMWITHIN2 = '/doomwithin', '/dw'
SlashCmdList["DOOMWITHIN"] = AddonCommands
