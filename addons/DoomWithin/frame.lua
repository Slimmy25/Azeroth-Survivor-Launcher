-- The Doom window: 320x200 palette pixels drawn as textures (1.12 API).
function _DW_GetFrame()
    local frame = CreateFrame("Frame", "DoomWithin", UIParent)
    frame:SetWidth(640 + 46)
    frame:SetHeight(400 + 46)
    frame:SetPoint("CENTER", UIParent, "CENTER", 0, 0)
    frame:SetFrameStrata("DIALOG")
    frame:SetBackdrop({
        bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
        edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
        tile = true, tileSize = 32, edgeSize = 32,
        insets = { left = 11, right = 12, top = 12, bottom = 11 },
    })
    frame:EnableMouse(true)
    frame:SetMovable(true)
    frame:RegisterForDrag("LeftButton")
    frame:SetScript("OnDragStart", function() this:StartMoving() end)
    frame:SetScript("OnDragStop", function() this:StopMovingOrSizing() end)
    frame:SetScript("OnHide", function() this:StopMovingOrSizing() end)

    local close = CreateFrame("Button", "DoomWithinClose", frame, "UIPanelCloseButton")
    close:SetPoint("TOPRIGHT", frame, "TOPRIGHT", -4, -4)
    frame.close = close

    local text = frame:CreateFontString(nil, "OVERLAY", "GameFontHighlightLarge")
    text:SetPoint("CENTER", frame, "CENTER", 0, 0)
    frame.text = text

    -- four 320x50 strips (the original splits it too); pixel n = row*320 + col, 2x2 screen pixels each
    frame.pixels = {}
    frame.framefuffer_parts = {}
    local n = 0
    for part = 1, 4 do
        local strip = CreateFrame("Frame", nil, frame)
        strip:SetWidth(640)
        strip:SetHeight(100)
        strip:SetPoint("TOPLEFT", frame, "TOPLEFT", 23, -23 - (part - 1) * 100)
        for row = 0, 49 do
            for col = 0, 319 do
                local pixel = strip:CreateTexture(nil, "ARTWORK")
                pixel:SetTexture("Interface\\AddOns\\DoomWithin\\palette")
                pixel:SetTexCoord(0, 1 / 256, 0, 1)
                pixel:SetWidth(2)
                pixel:SetHeight(2)
                pixel:SetPoint("TOPLEFT", strip, "TOPLEFT", col * 2, -row * 2)
                pixel.color = 0
                n = n + 1
                frame.pixels[n] = pixel
            end
        end
        strip:Hide()
        frame.framefuffer_parts[part] = strip
    end
    frame.opened = false

    function frame:RenderFrame(framebuffer)
        local pixels = self.pixels
        for offset = 0, 63999 do
            local data = framebuffer[offset]
            local pixel = pixels[offset + 1]
            if data ~= pixel.color then
                local tex_pos = data / 256
                pixel:SetTexCoord(tex_pos + 0.0009765625, tex_pos + 0.001953125, 0, 1)
                pixel.color = data
            end
        end
    end

    function frame:ShowGameView()
        for i = 1, 4 do self.framefuffer_parts[i]:Show() end
        self.opened = true
    end
    function frame:HideGameView()
        for i = 1, 4 do self.framefuffer_parts[i]:Hide() end
        self.opened = false
    end
    function frame:ShowStatusText() self.text:Show() end
    function frame:HideStatusText() self.text:Hide() end
    function frame:SetStatusText(t) self.text:SetText(t) end
    function frame:OnClose(handler)
        self.close:SetScript("OnClick", function() handler() end)
    end
    return frame
end
