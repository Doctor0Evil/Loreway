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
