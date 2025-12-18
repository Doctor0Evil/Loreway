----------------------------------------------------------------------
-- Loreway Dialogue + AI Behaviour Core (Lua)
-- For Cell – Slavic horror, adult-centric, IP-safe.[file:1]
-- Engine-agnostic: plug into your NPC blackboards / behavior trees / GOAP.
----------------------------------------------------------------------

local Loreway = {}

----------------------------------------------------------------------
-- Utility: pseudo-random with seedable state
----------------------------------------------------------------------

local Random = {}
Random.__index = Random

function Random.new(seed)
    local self = setmetatable({}, Random)
    self._seed = seed or os.time()
    return self
end

-- Simple LCG for deterministic behaviour per NPC.
local function lcg(seed)
    return (1103515245 * seed + 12345) % 2^31
end

function Random:nextFloat()
    self._seed = lcg(self._seed)
    return (self._seed % 100000) / 100000.0
end

function Random:chance(p)
    if p <= 0 then return false end
    if p >= 1 then return true end
    return self:nextFloat() <= p
end

function Random:range(minInclusive, maxExclusive)
    local f = self:nextFloat()
    local span = maxExclusive - minInclusive
    if span <= 0 then return minInclusive end
    return minInclusive + math.floor(f * span)
end

function Random:pick(list)
    if not list or #list == 0 then return nil end
    local idx = self:range(1, #list + 1)
    return list[idx]
end

Loreway.Random = Random

----------------------------------------------------------------------
-- Config / enums (Lua-style tables)
----------------------------------------------------------------------

Loreway.HorrorFunction = {
    None        = 0,
    Dread       = 1,
    Uncanny     = 2,
    Shock       = 4,
    Disgust     = 8,
    MoralAnxiety= 16
}

Loreway.LoreDepth = {
    SurfaceHint        = "surface_hint",
    PartialExplanation = "partial_explanation",
    DeepContext        = "deep_context"
}

Loreway.PlayerAgencyLevel = {
    Passive         = "passive",
    LightInteraction= "light_interaction",
    CriticalChoice  = "critical_choice"
}

Loreway.LineReliability = {
    Reliable   = "reliable",
    Ambiguous  = "ambiguous",
    KnownFalse = "known_false"
}

Loreway.DialogueStyleProfile = {
    RuralSparse     = "rural_sparse",
    BureaucraticCold= "bureaucratic_cold",
    Delirious       = "delirious",
    SoldierBurntOut = "soldier_burnt_out"
}

Loreway.SpeakerRole = {
    Player      = "player",
    Villager    = "villager",
    Official    = "official",
    Priest      = "priest",
    Smuggler    = "smuggler",
    Soldier     = "soldier",
    Spirit      = "spirit",
    Archivist   = "archivist"
}

Loreway.ToneBand = {
    SlavicHorror = "slavic_horror",
    OffTone      = "off_tone"
}

local defaultConfig = {
    maxLinesTotal           = 14,
    maxWordsPerLine         = 14,
    probabilityKnownFalse   = 0.25,
    probabilityAmbiguous    = 0.4,
    probabilityUseRumor     = 0.7,
    probabilityUseBackstory = 0.6,
    probabilityMentionTaboo = 0.8,
    targetHorrorFlags       = Loreway.HorrorFunction.Dread + Loreway.HorrorFunction.Uncanny,
    targetLoreDepth         = Loreway.LoreDepth.PartialExplanation,
    allowGraphicDescription = true,
    allowMoralHorror        = true,
    requiredToneBand        = Loreway.ToneBand.SlavicHorror
}

----------------------------------------------------------------------
-- Text helpers
----------------------------------------------------------------------

local function countWords(text)
    if not text or text == "" then return 0 end
    local count = 0
    local inWord = false
    for c in text:gmatch(".") do
        if c:match("%s") then
            if inWord then
                inWord = false
            end
        else
            if not inWord then
                inWord = true
                count = count + 1
            end
        end
    end
    return count
end

local function truncateWords(text, maxWords)
    if not text or text == "" then return text end
    if maxWords <= 0 then return "" end
    local words = {}
    for w in text:gmatch("%S+") do
        table.insert(words, w)
        if #words >= maxWords then
            break
        end
    end
    return table.concat(words, " ")
end

----------------------------------------------------------------------
-- Lexicons tuned for Slavic horror / bureaucracy / delirium.[file:1]
----------------------------------------------------------------------

local LEX = {
    ruralFillers = {
        "You know how it is.",
        "No one writes that down.",
        "Old folks still remember.",
        "If you ask the right drunk, youll hear it."
    },
    bureaucraticFillers = {
        "This is not for public record.",
        "The form says nothing, which is the problem.",
        "Officially, nothing happened here.",
        "You did not hear this from me."
    },
    deliriousFillers = {
        "Teeth in the walls, teeth in the snow.",
        "The night walks on all fours.",
        "They miscounted us on purpose.",
        "Floorboards keep every secret in their joints."
    },
    soldierFillers = {
        "We were told not to look back.",
        "Orders dont cover what crawls under the snow.",
        "You sleep when the generator does, if it lets you.",
        "There are names we dont write in the reports."
    }
}

----------------------------------------------------------------------
-- Dialogue line / unit construction
----------------------------------------------------------------------

local function newLine(speaker)
    return {
        lineId             = "LINE_" .. speaker.id .. "_" .. tostring(math.random(1000000,9999999)),
        speakerId          = speaker.id,
        speakerDisplayName = speaker.displayName,
        speakerRole        = speaker.role,
        text               = "",
        intent             = "none",
        reliability        = Loreway.LineReliability.Reliable,
        systemTags         = {}
    }
end

local function ensureWordLimit(line, cfg)
    if not line.text then return end
    if countWords(line.text) > cfg.maxWordsPerLine then
        line.text = truncateWords(line.text, cfg.maxWordsPerLine)
    end
end

local function appendTag(line, tag)
    if not line.systemTags then line.systemTags = {} end
    table.insert(line.systemTags, tag)
end

local function applyStyle(raw, speaker, style, rng)
    if not raw or raw == "" then return raw end
    local t = raw

    if style == Loreway.DialogueStyleProfile.RuralSparse then
        t = t:gsub(" and ", ", ")
        if t:sub(1,2) == "I " and rng:chance(0.35) then
            t = t:sub(3)
        end
    elseif style == Loreway.DialogueStyleProfile.BureaucraticCold then
        if rng:chance(0.5) then
            local first = t:sub(1,1):lower()
            t = "According to procedure, " .. first .. t:sub(2)
        end
    elseif style == Loreway.DialogueStyleProfile.Delirious then
        if rng:chance(0.4) then
            t = t .. " The numbers do not add up, but they still count us."
        end
    elseif style == Loreway.DialogueStyleProfile.SoldierBurntOut then
        if rng:chance(0.4) then
            t = t .. " You start hearing footsteps that arent there long before you see them."
        end
    end

    return t
end

----------------------------------------------------------------------
-- Helper: taboo verbification
----------------------------------------------------------------------

local function verbifyTaboo(taboo)
    if not taboo or not taboo.displayName then
        return "break the rule"
    end
    local lower = taboo.displayName:lower()
    if lower:sub(1,3) == "no " then
        return lower:sub(4)
    end
    if lower:sub(1,7) == "do not " then
        return lower:sub(8)
    end
    return lower
end

----------------------------------------------------------------------
-- Tone classifier (stub to be replaced by ML / rule system)[file:1]
----------------------------------------------------------------------

local function classifyTone(dialogueUnit)
    for _, l in ipairs(dialogueUnit.lines) do
        local t = (l.text or ""):lower()
        if t:find("hero") and t:find("glory") then
            return Loreway.ToneBand.OffTone
        end
    end
    return Loreway.ToneBand.SlavicHorror
end

----------------------------------------------------------------------
-- AI Behaviour scaffolding: stateful NPC blackboard
----------------------------------------------------------------------

-- Each NPC can have behaviourState, exposure to rumors/taboos, fear level, trust, etc.[file:1]
local function newAIState(npcId, rngSeed)
    return {
        npcId           = npcId,
        rng             = Random.new(rngSeed or os.time()),
        fearLevel       = 0.3,     -- 0..1
        trustPlayer     = 0.5,     -- 0..1
        paranoia        = 0.4,     -- 0..1
        rumorExposure   = {},      -- [rumorId] = { heard = true, stance = "denier"/"embellisher"/"believer" }
        tabooBreaks     = {},      -- [tabooId] = count
        lastBarkTime    = 0,
        barkCooldown    = 3.0,     -- seconds
        behaviourFlags  = {        -- quick switches for BTs
            warnsAboutForest = true,
            hidesOwnGuilt    = true,
            downplaysThreat  = false
        }
    }
end

Loreway.newAIState = newAIState

-- Update fear/trust/paranoia when player breaks taboos etc.[file:1]
function Loreway.updateAIStateFromEvent(aiState, eventType, payload)
    if eventType == "PLAYER_BREAKS_TABOO" then
        local tabooId = payload.tabooId
        aiState.tabooBreaks[tabooId] = (aiState.tabooBreaks[tabooId] or 0) + 1
        aiState.fearLevel = math.min(1.0, aiState.fearLevel + 0.15)
        aiState.paranoia  = math.min(1.0, aiState.paranoia + 0.1)
    elseif eventType == "PLAYER_HELPS_NPC" then
        aiState.trustPlayer = math.min(1.0, aiState.trustPlayer + 0.2)
        aiState.paranoia    = math.max(0.0, aiState.paranoia - 0.1)
    elseif eventType == "NPC_SEES_SPIRIT_SIGN" then
        aiState.fearLevel = math.min(1.0, aiState.fearLevel + 0.25)
        aiState.paranoia  = math.min(1.0, aiState.paranoia + 0.2)
    end
end

----------------------------------------------------------------------
-- Rumor exposure / stance system
----------------------------------------------------------------------

function Loreway.registerRumorExposure(aiState, rumor, stance)
    aiState.rumorExposure[rumor.id] = {
        heard  = true,
        topic  = rumor.topic,
        stance = stance or "embellisher" -- "denier","believer","embellisher"
    }
end

----------------------------------------------------------------------
-- Dialogue generator object
----------------------------------------------------------------------

local Generator = {}
Generator.__index = Generator

function Loreway.newGenerator(config, seed)
    local self = setmetatable({}, Generator)
    self.cfg  = {}
    for k,v in pairs(defaultConfig) do
        self.cfg[k] = v
    end
    if config then
        for k,v in pairs(config) do
            self.cfg[k] = v
        end
    end
    self.rng = Random.new(seed or os.time())
    return self
end

----------------------------------------------------------------------
-- Line builders (now behaviour‑aware)
----------------------------------------------------------------------

function Generator:buildRuleOrAtmosphereLine(npc, place, taboos, spirit, unseenRuleRef)
    local line = newLine(npc)
    line.intent = "imply_rule"
    local cfg   = self.cfg

    local placeName = (place and place.displayName) or "this place"
    local sb = {}

    if taboos and #taboos > 0 and self.rng:chance(0.8) then
        local t = self.rng:pick(taboos)
        table.insert(sb, string.format("Here in %s, we do not %s after dark.", placeName, verbifyTaboo(t)))
        unseenRuleRef.value = true
        line.reliability = Loreway.LineReliability.Ambiguous
        appendTag(line, "imply_taboo")
        appendTag(line, "taboo:" .. t.id)
    else
        table.insert(sb, string.format("Nothing here is truly quiet, not even the ground under %s.", placeName))
        line.reliability = Loreway.LineReliability.Reliable
        appendTag(line, "atmosphere")
    end

    if spirit and self.rng:chance(0.6) then
        table.insert(sb, "The old one in the trees listens when you forget yourself.")
        unseenRuleRef.value = true
        appendTag(line, "spirit_hint:" .. spirit.id)
    end

    line.text = table.concat(sb, " ")
    line.text = applyStyle(line.text, npc, Loreway.DialogueStyleProfile.RuralSparse, self.rng)
    ensureWordLimit(line, cfg)
    return line
end

function Generator:buildPlayerPushbackLine(player, npc, place)
    local line = newLine(player)
    line.intent = "question_rule"
    line.reliability = Loreway.LineReliability.Reliable

    local placeName = (place and place.displayName) or "here"
    local variants = {
        string.format("You really believe %s remembers who breaks the rules?", placeName),
        "That sounds like a story you tell children.",
        "What happens if someone ignores that?",
        "And you just accept that as normal?"
    }
    line.text = self.rng:pick(variants)
    appendTag(line, "player_pushback")
    ensureWordLimit(line, self.cfg)
    return line
end

function Generator:buildRumorLine(npc, aiState, rumors, contradictionRef)
    local line = newLine(npc)
    line.intent = "contradict_evidence"

    local rumor = self.rng:pick(rumors)
    local variant = self.rng:pick(rumor.textVariants or rumor.variants or { rumor.topic or "Something they dont print." })

    -- stance can flip reliability.
    local stanceData = aiState and aiState.rumorExposure[rumor.id]
    local stance = stanceData and stanceData.stance or "embellisher"

    if stance == "denier" then
        line.reliability = Loreway.LineReliability.KnownFalse
        contradictionRef.value = true
    elseif self.rng:chance(self.cfg.probabilityKnownFalse) then
        line.reliability = Loreway.LineReliability.KnownFalse
        contradictionRef.value = true
    else
        line.reliability = Loreway.LineReliability.Ambiguous
    end

    local prefixOptions = {
        "They swear that",
        "People still whisper that",
        "If you ask in the right kitchen, they say",
        "Off the record,"
    }
    local suffixOptions = {
        " but the papers never mentioned it.",
        " though everyone involved is suddenly very pious.",
        " and no one likes to stand near that spot now.",
        " you can check the old files if you dare."
    }

    local sb = {}
    table.insert(sb, self.rng:pick(prefixOptions))
    table.insert(sb, variant:gsub("[%. ]*$",""))
    table.insert(sb, self.rng:pick(suffixOptions))

    line.text = table.concat(sb, " ")
    line.text = applyStyle(line.text, npc, Loreway.DialogueStyleProfile.RuralSparse, self.rng)
    appendTag(line, "rumor")
    appendTag(line, "rumor:" .. rumor.id)
    ensureWordLimit(line, self.cfg)
    return line
end

function Generator:buildBackstoryLeakLine(npc)
    local line = newLine(npc)
    line.intent = "reveal_flaw"
    line.reliability = Loreway.LineReliability.Ambiguous

    local back = npc.backstory
    local sb = {}

    if not back then
        table.insert(sb, "Some of us already paid once. The forest keeps the receipt.")
    else
        local goals = back.drivingGoals or { "protectfamily" }
        local fears = back.fears or { "forest" }
        local secrets = back.secrets or { "what really happened that winter" }
        local goal = self.rng:pick(goals)
        local fear = self.rng:pick(fears)
        local secret = self.rng:pick(secrets)

        if self.cfg.allowMoralHorror and self.rng:chance(0.6) then
            table.insert(sb, "You do not keep a family alive here without stepping on something that twitches.")
        else
            table.insert(sb, "Everyone here carries something that should have stayed buried.")
        end

        table.insert(sb, "I did what I had to, when " .. fear .. " came to collect on " .. goal ..
                            ", and now no one asks me about " .. secret .. ".")
        appendTag(line, "backstory:" .. back.id)
    end

    line.text = table.concat(sb, " ")
    line.text = applyStyle(line.text, npc, Loreway.DialogueStyleProfile.RuralSparse, self.rng)
    appendTag(line, "backstory_leak")
    ensureWordLimit(line, self.cfg)
    return line
end

function Generator:buildTabooReinforcementLine(npc, taboos, unseenRuleRef)
    local t = self.rng:pick(taboos)
    local line = newLine(npc)
    line.intent = "reinforce_taboo"
    local sb = {}

    if self.rng:chance(0.7) then
        table.insert(sb, "Break " .. t.displayName:lower() .. " once, and you will spend the next night counting who is missing.")
        line.reliability = Loreway.LineReliability.Ambiguous
    else
        table.insert(sb, "Keep " .. t.displayName:lower() .. " and the ground pretends not to notice you.")
        line.reliability = Loreway.LineReliability.Reliable
    end

    unseenRuleRef.value = true
    line.text = table.concat(sb, " ")
    line.text = applyStyle(line.text, npc, Loreway.DialogueStyleProfile.RuralSparse, self.rng)
    appendTag(line, "taboo_reinforce")
    appendTag(line, "taboo:" .. t.id)
    ensureWordLimit(line, self.cfg)
    return line
end

function Generator:buildFillerLine(npc, style)
    local line = newLine(npc)
    line.intent = "atmosphere"
    line.reliability = Loreway.LineReliability.Ambiguous

    local baseText
    if style == Loreway.DialogueStyleProfile.BureaucraticCold then
        baseText = self.rng:pick(LEX.bureaucraticFillers)
    elseif style == Loreway.DialogueStyleProfile.Delirious then
        baseText = self.rng:pick(LEX.deliriousFillers)
    elseif style == Loreway.DialogueStyleProfile.SoldierBurntOut then
        baseText = self.rng:pick(LEX.soldierFillers)
    else
        baseText = self.rng:pick(LEX.ruralFillers)
    end

    line.text = applyStyle(baseText, npc, style, self.rng)
    appendTag(line, "atmosphere_filler")
    ensureWordLimit(line, self.cfg)
    return line
end

function Generator:buildClosingBeatLine(npc, place, aiState, contradictionRef)
    local line = newLine(npc)
    line.intent = "close"

    local placeName = (place and place.displayName) or "here"
    local lieChance = self.cfg.probabilityKnownFalse

    -- NPC paranoia + desire to hide guilt pushes toward lying.[file:1]
    if aiState then
        if aiState.behaviourFlags.hidesOwnGuilt then
            lieChance = lieChance + 0.2
        end
        if aiState.fearLevel > 0.7 then
            lieChance = lieChance - 0.1
        end
    end

    if self.rng:chance(lieChance) then
        line.reliability = Loreway.LineReliability.KnownFalse
        contradictionRef.value = true
        line.text = string.format("Relax. No one has gone missing from %s in years.", placeName)
        appendTag(line, "reassurance_lie")
    else
        line.reliability = Loreway.LineReliability.Ambiguous
        line.text = "If you hear singing out past the last pole, do not turn around. Just count your teeth and keep walking."
        appendTag(line, "ominous_close")
    end

    line.text = applyStyle(line.text, npc, Loreway.DialogueStyleProfile.RuralSparse, self.rng)
    ensureWordLimit(line, self.cfg)
    return line
end

----------------------------------------------------------------------
-- Core: generate a DialogueUnit from KG + AI state
----------------------------------------------------------------------

function Generator:generateDialogueUnit(args)
    -- args: {
    --   sceneId,
    --   styleProfile,
    --   primaryNpc,
    --   primaryNpcAIState,
    --   player,
    --   place,
    --   activeTaboos = {...},
    --   localRumors  = {...},
    --   spirit       = SpiritRef or nil
    -- }
    local cfg = self.cfg
    local unit = {
        id               = "DLG_" .. (args.sceneId or "UNKNOWN") .. "_" .. args.primaryNpc.id .. "_" .. tostring(os.time()),
        sceneId          = args.sceneId,
        styleProfile     = args.styleProfile or Loreway.DialogueStyleProfile.RuralSparse,
        horrorFunction   = cfg.targetHorrorFlags,
        loreDepth        = cfg.targetLoreDepth,
        playerAgency     = Loreway.PlayerAgencyLevel.LightInteraction,
        hasContradiction = false,
        hintsUnseenRule  = false,
        referencesTaboo  = false,
        referencesRumor  = false,
        referencesBackstory = false,
        lines            = {}
    }

    local unseenRuleRef   = { value = false }
    local contradictionRef= { value = false }

    local targetLines = self.rng:range(6, cfg.maxLinesTotal + 1)

    -- Opening rule / atmosphere.
    local l0 = self:buildRuleOrAtmosphereLine(args.primaryNpc, args.place, args.activeTaboos, args.spirit, unseenRuleRef)
    table.insert(unit.lines, l0)
    if #l0.systemTags > 0 then
        for _,tag in ipairs(l0.systemTags) do
            if tag:match("^taboo:") then
                unit.referencesTaboo = true
            end
        end
    end

    -- Player pushback.
    local l1 = self:buildPlayerPushbackLine(args.player, args.primaryNpc, args.place)
    table.insert(unit.lines, l1)

    local tabooMentioned  = unit.referencesTaboo
    local rumorUsed       = false
    local backstoryUsed   = false

    for i = 3, targetLines - 1 do
        local line

        if not rumorUsed and args.localRumors and #args.localRumors > 0 and self.rng:chance(cfg.probabilityUseRumor) then
            line = self:buildRumorLine(args.primaryNpc, args.primaryNpcAIState, args.localRumors, contradictionRef)
            rumorUsed = true
            unit.referencesRumor = true
        elseif not backstoryUsed and args.primaryNpc.backstory and self.rng:chance(cfg.probabilityUseBackstory) then
            line = self:buildBackstoryLeakLine(args.primaryNpc)
            backstoryUsed = true
            unit.referencesBackstory = true
        elseif not tabooMentioned and args.activeTaboos and #args.activeTaboos > 0 and self.rng:chance(cfg.probabilityMentionTaboo) then
            line = self:buildTabooReinforcementLine(args.primaryNpc, args.activeTaboos, unseenRuleRef)
            tabooMentioned = true
            unit.referencesTaboo = true
        else
            line = self:buildFillerLine(args.primaryNpc, unit.styleProfile)
        end

        table.insert(unit.lines, line)
    end

    local closing = self:buildClosingBeatLine(args.primaryNpc, args.place, args.primaryNpcAIState, contradictionRef)
    table.insert(unit.lines, closing)

    unit.hasContradiction = contradictionRef.value
    unit.hintsUnseenRule  = unseenRuleRef.value

    local toneBand = classifyTone(unit)
    if toneBand ~= cfg.requiredToneBand then
        -- Tag lines as ambiguous instead of throwing them away: design can filter or re-request.[file:1]
        for _, l in ipairs(unit.lines) do
            if l.reliability == Loreway.LineReliability.Reliable then
                l.reliability = Loreway.LineReliability.Ambiguous
            end
        end
    end

    return unit
end

Loreway.Generator = Generator

----------------------------------------------------------------------
-- Behaviour-driven bark selection API
-- This is the layer that your behaviour tree / GOAP can call to get
-- context-appropriate one-liners without full conversation.
----------------------------------------------------------------------

function Loreway.selectBarkFromUnit(unit, aiState, gameContext)
    -- gameContext: { threatLevel, playerBrokeTabooRecently, inSafehouse, timeOfDay }
    local candidates = {}
    local nowThreat = gameContext.threatLevel or 0.0
    local tabooRecent = gameContext.playerBrokeTabooRecently
    local inSafe = gameContext.inSafehouse

    for _, line in ipairs(unit.lines) do
        local tags = line.systemTags or {}
        local isThreatBark = false
        local isSafehouseBark = false
        local isTabooBark = false

        for _,tag in ipairs(tags) do
            if tag == "threatnear" or tag == "ominous_close" then
                isThreatBark = true
            elseif tag == "entersafehouse" or tag == "atmosphere_filler" then
                isSafehouseBark = true
            elseif tag:match("^taboo:") or tag == "taboo_reinforce" then
                isTabooBark = true
            end
        end

        local score = 0

        if nowThreat > 0.6 and isThreatBark then
            score = score + 3
        end
        if tabooRecent and isTabooBark then
            score = score + 2
        end
        if inSafe and isSafehouseBark then
            score = score + 1
        end

        -- paranoia pushes preference toward ambiguous / known-false lines.[file:1]
        if aiState and aiState.paranoia > 0.5 then
            if line.reliability ~= Loreway.LineReliability.Reliable then
                score = score + 1
            end
        end

        if score > 0 then
            table.insert(candidates, { line = line, score = score })
        end
    end

    if #candidates == 0 then
        -- fallback: any line
        return unit.lines[ math.random(1, #unit.lines) ]
    end

    table.sort(candidates, function(a,b) return a.score > b.score end)
    local topScore = candidates[1].score
    local top = {}
    for _,c in ipairs(candidates) do
        if c.score == topScore then
            table.insert(top, c.line)
        else
            break
        end
    end

    return top[ math.random(1, #top) ]
end

----------------------------------------------------------------------
-- Example: factory-style helper for minimal KG refs.
-- In production, map these from your Loreway KG (Spirits, Places, etc.).[file:1]
----------------------------------------------------------------------

function Loreway.newCharacterRef(id, displayName, role, psychologicalTags, dialectNotes, backstory)
    return {
        id                = id,
        displayName       = displayName,
        role              = role,
        psychologicalTags = psychologicalTags or {},
        dialectNotes      = dialectNotes or {},
        backstory         = backstory
    }
end

function Loreway.newPlaceRef(id, displayName, environmentTags)
    return {
        id              = id,
        displayName     = displayName,
        environmentTags = environmentTags or {}
    }
end

function Loreway.newTabooRef(id, displayName, description)
    return {
        id               = id,
        displayName      = displayName,
        descriptionInWorld = description
    }
end

function Loreway.newRumorRef(id, topic, textVariants, truthStatus)
    return {
        id          = id,
        topic       = topic,
        textVariants= textVariants or {},
        truthStatus = truthStatus or "unknown"
    }
end

function Loreway.newBackstoryPacket(id, summary, drivingGoals, fears, secrets)
    return {
        id           = id,
        summary      = summary,
        drivingGoals = drivingGoals or {},
        fears        = fears or {},
        secrets      = secrets or {}
    }
end

function Loreway.newSpiritRef(id, displayName, originMotif, temperamentTags, domains)
    return {
        id              = id,
        displayName     = displayName,
        originMotif     = originMotif,
        temperamentTags = temperamentTags or {},
        domains         = domains or {}
    }
end

----------------------------------------------------------------------
-- Usage sketch (for designers / scripters):
--
-- local gen = Loreway.newGenerator({ maxLinesTotal = 10 }, 1234)
-- local npcBackstory = Loreway.newBackstoryPacket("BACK_OLDWOMAN01",
--      "Lived through the night the well sank.",
--      {"protectfamily"},{"forest"},{"what happened at the well"})
-- local npc = Loreway.newCharacterRef("NPC_OLDWOMAN01","Old Neighbor",
--      Loreway.SpeakerRole.Villager, {"secretive","protective"},{"rural","clipped"}, npcBackstory)
-- local player = Loreway.newCharacterRef("PLAYER","You",Loreway.SpeakerRole.Player)
-- local place  = Loreway.newPlaceRef("PLC_ASHDITCH","Ashditch",{"wetforestedge","postsovietdecay"})
-- local taboo  = Loreway.newTabooRef("TABS_WHISTLE","No Whistling After Dark","Whistling after sunset calls the ones who got lost.")
-- local rumor  = Loreway.newRumorRef("RMR_WELL","disappearance",
--      {"They say the well swallowed them whole.","Old men whisper it was the forest, not the stones."},"partial")
-- local spirit = Loreway.newSpiritRef("SPRT_BENTONE","The Bent One","forgottenburialground",
--      {"indifferent","vindictive"},{"forest","borderpath"})
-- local npcAI  = Loreway.newAIState(npc.id, 999)
-- Loreway.registerRumorExposure(npcAI, rumor, "embellisher")
--
-- local unit = gen:generateDialogueUnit{
--     sceneId          = "CELL_CH1_FORESTAPPROACH01",
--     styleProfile     = Loreway.DialogueStyleProfile.RuralSparse,
--     primaryNpc       = npc,
--     primaryNpcAIState= npcAI,
--     player           = player,
--     place            = place,
--     activeTaboos     = { taboo },
--     localRumors      = { rumor },
--     spirit           = spirit
-- }
--
-- local bark = Loreway.selectBarkFromUnit(unit, npcAI, {
--     threatLevel               = 0.7,
--     playerBrokeTabooRecently  = true,
--     inSafehouse               = false
-- })
-- print(bark.speakerDisplayName .. ": " .. bark.text)
--
----------------------------------------------------------------------

return Loreway
