-- Path: /Cell/Loreway/Config/LorewayPersonality.lua
-- Purpose: Personality-vector + brutality automation for Loreway-driven systems.
-- Any AI chat, IDE agent, or in-game tool calls this to align output with Cell.

local LorewayPersonality = {}

----------------------------------------------------------------------
-- Core scalar and vector types
----------------------------------------------------------------------

---@class BrutalityProfile
---@field global number                -- 0.0–1.0
---@field physical number              -- 0.0–1.0
---@field psychological number         -- 0.0–1.0
---@field social number                -- 0.0–1.0

---@class HorrorVector
---@field dread number                 -- 0.0–1.0
---@field shock number
---@field disgust number
---@field uncanny number
---@field moral_anxiety number

---@class SlavicToneVector
---@field rural_decay number           -- panelki, wet forests, collapsing farms
---@field bureaucratic_horror number   -- documents, offices, quotas, trials
---@field domestic_haunting number     -- apartments, family, neighbors
---@field cosmic_rot number            -- slow metaphysical infection

---@class LorewayPersonalityProfile
---@field id string
---@field label string
---@field brutality BrutalityProfile
---@field horror HorrorVector
---@field slavic SlavicToneVector
---@field surreal_temperature number   -- 0.0–1.0
---@field narrative_temperature number -- 0.0–1.0
---@field horror_temperature number    -- 0.0–1.0

----------------------------------------------------------------------
-- Default Cell-wide personality
----------------------------------------------------------------------

local DEFAULT_PROFILE = {
    id    = "CELL_CORE_DEFAULT",
    label = "Cell Core – Wet Forest Bureaucratic Rot",

    brutality = {
        global        = 0.85,
        physical      = 0.80,
        psychological = 0.95,
        social        = 0.90,
    },

    horror = {
        dread         = 0.95,
        shock         = 0.80,
        disgust       = 0.75,
        uncanny       = 0.85,
        moral_anxiety = 1.00,
    },

    slavic = {
        rural_decay         = 0.95,
        bureaucratic_horror = 0.90,
        domestic_haunting   = 0.85,
        cosmic_rot          = 0.70,
    },

    surreal_temperature   = 0.55,
    narrative_temperature = 0.70,
    horror_temperature    = 0.80,
}

LorewayPersonality.DEFAULT_PROFILE = DEFAULT_PROFILE
----------------------------------------------------------------------
-- Utility helpers
----------------------------------------------------------------------

local function clamp01(x)
    if x < 0.0 then return 0.0 end
    if x > 1.0 then return 1.0 end
    return x
end

local function lerp(a, b, t)
    return a + (b - a) * t
end

local function deep_copy(tbl)
    local out = {}
    for k, v in pairs(tbl) do
        if type(v) == "table" then
            out[k] = deep_copy(v)
        else
            out[k] = v
        end
    end
    return out
end

----------------------------------------------------------------------
-- Personality construction and blending
----------------------------------------------------------------------

---Create a new personality by overriding defaults with partial fields.
---@param id string
---@param label string
---@param overrides table|nil
---@return LorewayPersonalityProfile
function LorewayPersonality.new_profile(id, label, overrides)
    local p = deep_copy(DEFAULT_PROFILE)
    p.id = id or p.id
    p.label = label or p.label

    overrides = overrides or {}

    local function merge_scalar(path, defaultValue)
        local t = overrides
        for i = 1, #path - 1 do
            local key = path[i]
            t = t[key]
            if t == nil then return end
        end
        local leaf = path[#path]
        if t and t[leaf] ~= nil then
            local current = p
            for i = 1, #path - 1 do
                current = current[path[i]]
            end
            current[leaf] = clamp01(t[leaf])
        end
    end

    -- Brutality
    merge_scalar({ "brutality", "global" }, p.brutality.global)
    merge_scalar({ "brutality", "physical" }, p.brutality.physical)
    merge_scalar({ "brutality", "psychological" }, p.brutality.psychological)
    merge_scalar({ "brutality", "social" }, p.brutality.social)

    -- Horror
    merge_scalar({ "horror", "dread" }, p.horror.dread)
    merge_scalar({ "horror", "shock" }, p.horror.shock)
    merge_scalar({ "horror", "disgust" }, p.horror.disgust)
    merge_scalar({ "horror", "uncanny" }, p.horror.uncanny)
    merge_scalar({ "horror", "moral_anxiety" }, p.horror.moral_anxiety)

    -- Slavic tones
    merge_scalar({ "slavic", "rural_decay" }, p.slavic.rural_decay)
    merge_scalar({ "slavic", "bureaucratic_horror" }, p.slavic.bureaucratic_horror)
    merge_scalar({ "slavic", "domestic_haunting" }, p.slavic.domestic_haunting)
    merge_scalar({ "slavic", "cosmic_rot" }, p.slavic.cosmic_rot)

    -- Temperatures
    if overrides.surreal_temperature ~= nil then
        p.surreal_temperature = clamp01(overrides.surreal_temperature)
    end
    if overrides.narrative_temperature ~= nil then
        p.narrative_temperature = clamp01(overrides.narrative_temperature)
    end
    if overrides.horror_temperature ~= nil then
        p.horror_temperature = clamp01(overrides.horror_temperature)
    end

    return p
end

---Blend two profiles over t (0..1).
---@param a LorewayPersonalityProfile
---@param b LorewayPersonalityProfile
---@param t number
---@return LorewayPersonalityProfile
function LorewayPersonality.blend_profiles(a, b, t)
    t = clamp01(t)
    local p = deep_copy(a)
    p.id    = a.id .. "_BLEND_" .. b.id
    p.label = "Blend(" .. a.label .. ", " .. b.label .. ")"

    local function blend_field(path)
        local left = a
        local right = b
        local dest = p
        for i = 1, #path - 1 do
            left  = left[path[i]]
            right = right[path[i]]
            dest  = dest[path[i]]
        end
        local k = path[#path]
        dest[k] = clamp01(lerp(left[k], right[k], t))
    end

    -- Brutality
    blend_field({ "brutality", "global" })
    blend_field({ "brutality", "physical" })
    blend_field({ "brutality", "psychological" })
    blend_field({ "brutality", "social" })

    -- Horror
    blend_field({ "horror", "dread" })
    blend_field({ "horror", "shock" })
    blend_field({ "horror", "disgust" })
    blend_field({ "horror", "uncanny" })
    blend_field({ "horror", "moral_anxiety" })

    -- Slavic tones
    blend_field({ "slavic", "rural_decay" })
    blend_field({ "slavic", "bureaucratic_horror" })
    blend_field({ "slavic", "domestic_haunting" })
    blend_field({ "slavic", "cosmic_rot" })

    -- Temperatures
    p.surreal_temperature   = clamp01(lerp(a.surreal_temperature,   b.surreal_temperature,   t))
    p.narrative_temperature = clamp01(lerp(a.narrative_temperature, b.narrative_temperature, t))
    p.horror_temperature    = clamp01(lerp(a.horror_temperature,    b.horror_temperature,    t))

    return p
end
