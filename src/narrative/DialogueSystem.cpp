#include <string>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <random>
#include <chrono>
#include <algorithm>
#include <functional>
#include <sstream>

// ------------------------------------------------------
// Utility: RNG wrapper
// ------------------------------------------------------
class RNG
{
public:
    RNG()
    {
        std::random_device rd;
        engine.seed(rd());
    }

    int RandomInt(int minInclusive, int maxInclusive)
    {
        std::uniform_int_distribution<int> dist(minInclusive, maxInclusive);
        return dist(engine);
    }

    float RandomFloat(float minInclusive, float maxInclusive)
    {
        std::uniform_real_distribution<float> dist(minInclusive, maxInclusive);
        return dist(engine);
    }

    bool Chance(float probability01)
    {
        if (probability01 <= 0.0f) return false;
        if (probability01 >= 1.0f) return true;
        std::bernoulli_distribution dist(probability01);
        return dist(engine);
    }

private:
    std::mt19937 engine;
};

// ------------------------------------------------------
// Dialogue enums and small structs
// ------------------------------------------------------
enum class DialogueFunction
{
    NeutralAmbient,
    Dread,
    Misdirection,
    RitualHint,
    Rumor,
    Bureaucratic,
    ThreatBark,
    Pain,
    Surprise
};

enum class ReliabilityTag
{
    Unknown,
    Truthful,
    Partial,
    KnownFalse
};

enum class RegionTone
{
    ForestVillage,
    SovietApartment,
    IndustrialBlock,
    BorderOutpost
};

enum class SpeakerSocialRole
{
    Villager,
    Bureaucrat,
    Priest,
    Smuggler,
    Soldier,
    Doctor,
    Hermit
};

struct DialogueContext
{
    RegionTone          regionTone = RegionTone::ForestVillage;
    float               threatLevel01 = 0.0f;       // 0 = calm, 1 = lethal
    bool                isIndoors = false;
    bool                isNight = false;
    bool                playerRecentlyBrokeTaboo = false;
    bool                playerLowHealth = false;
    bool                playerIsBleeding = false;
    bool                inSafeRoomFlagged = false;
    std::string         locationId;                 // e.g., "PLC_VILLAGE_ASHDITCH"
    std::unordered_set<std::string> activeTabooIds; // e.g., "TABS_WHISTLE_AT_NIGHT"
    std::unordered_set<std::string> recentEventIds; // e.g., "EV_WELL_COLLAPSE"
    std::unordered_set<std::string> knownRumorIds;  // events NPC knows about
};

// ------------------------------------------------------
// Voice profile per NPC
// ------------------------------------------------------
struct NPCVoiceProfile
{
    std::string         npcId;                // unique ID
    std::string         displayName;          // optional, for debugging
    SpeakerSocialRole   role = SpeakerSocialRole::Villager;

    // Style sliders (0..1)
    float                verbosity01 = 0.4f;       // higher = longer sentences
    float                superstition01 = 0.8f;    // higher = more taboos, spirits
    float                bureaucratic01 = 0.0f;    // higher = drier, official tone
    float                religiosity01 = 0.3f;
    float                cruelty01 = 0.2f;         // matter‑of‑fact cruelty
    float                unreliability01 = 0.4f;   // chance to lie or distort
    float                fatalism01 = 0.7f;        // resigned, hopeless vibe

    // Vocabulary knobs
    std::string          dialectTag;              // e.g., "rural_east", "block_1988"
    std::vector<std::string> personalMotifs;      // e.g., "debts", "missing_children"

    // Internal cooldowns (per function), in seconds
    std::unordered_map<DialogueFunction, float> cooldownSeconds =
    {
        { DialogueFunction::NeutralAmbient, 20.0f },
        { DialogueFunction::Dread,          15.0f },
        { DialogueFunction::Misdirection,   25.0f },
        { DialogueFunction::RitualHint,     45.0f },
        { DialogueFunction::Rumor,          40.0f },
        { DialogueFunction::Bureaucratic,   35.0f },
        { DialogueFunction::ThreatBark,      5.0f },
        { DialogueFunction::Pain,            3.0f },
        { DialogueFunction::Surprise,        8.0f }
    };
};

// ------------------------------------------------------
// Dialogue template definition
// ------------------------------------------------------
//
// Text uses simple tokens that get replaced at runtime:
//   {PLAYER_CALLSIGN}, {LOCAL_SPIRIT}, {TABOO}, {PLACE}, {BODYSYMPTOM}, etc.
// The "weight" field is used for RNG selection.
//
struct DialogueTemplate
{
    std::string                 id;
    DialogueFunction            function = DialogueFunction::NeutralAmbient;
    ReliabilityTag              reliability = ReliabilityTag::Unknown;
    RegionTone                  regionTone = RegionTone::ForestVillage;

    // Optional tags (used as soft filters)
    std::vector<std::string>   requiredTabooIds;
    std::vector<std::string>   requiredEventIds;
    std::vector<std::string>   disallowedLocationIds;
    std::vector<SpeakerSocialRole> allowedRoles;

    // Short template text (one line). Slavic‑horror tone is controlled via content.
    std::string                 text;
    float                       weight = 1.0f;

    // Conditions as lambdas (can be set at data load time)
    std::function<bool(const DialogueContext&, const NPCVoiceProfile&)> condition;
};

// ------------------------------------------------------
// DialogueSystem core
// ------------------------------------------------------
class DialogueSystem
{
public:
    DialogueSystem()
    {
        InitializeDefaultTemplates();
        lastFireTimestamps.reserve(256);
    }

    // Call this each frame or tick with global time (seconds)
    void SetCurrentTimeSeconds(double t)
    {
        currentTimeSeconds = t;
    }

    void RegisterNPCProfile(const NPCVoiceProfile& profile)
    {
        npcProfiles[profile.npcId] = profile;
    }

    const NPCVoiceProfile* GetNPCProfile(const std::string& npcId) const
    {
        auto it = npcProfiles.find(npcId);
        if (it == npcProfiles.end()) return nullptr;
        return &it->second;
    }

    // Main API used by AI / scripts.
    // "triggerTag" can be something like "on_enter_safehouse",
    // "on_player_breaks_taboo", "on_night_heartbeat", "on_enemy_spotted", etc.
    std::string GenerateLine(const std::string& npcId,
                             const std::string& triggerTag,
                             const DialogueContext& ctx)
    {
        const NPCVoiceProfile* profile = GetNPCProfile(npcId);
        if (!profile) return std::string();

        // Map triggerTag to a target function
        DialogueFunction desiredFunction = MapTriggerToFunction(triggerTag, ctx, *profile);

        // Cooldown check
        if (!CanFire(*profile, desiredFunction))
            return std::string();

        // Collect valid templates
        std::vector<const DialogueTemplate*> candidates;
        CollectCandidates(ctx, *profile, desiredFunction, candidates);

        if (candidates.empty())
            return std::string();

        // Weighted random pick
        const DialogueTemplate* chosen = PickTemplateWeighted(candidates);
        if (!chosen)
            return std::string();

        // Record cooldown timestamp
        TouchCooldown(profile->npcId, desiredFunction);

        // Generate surface text with substitutions and stylistic passes
        std::string line = RealizeTemplate(*chosen, ctx, *profile);

        return line;
    }

    // Hook for registering emergent events as rumor seeds, etc.
    void NotifyEvent(const std::string& eventId,
                     const std::string& regionId,
                     float severity01)
    {
        EmergentEvent e;
        e.eventId = eventId;
        e.regionId = regionId;
        e.severity01 = severity01;
        e.timestampSeconds = currentTimeSeconds;
        emergentEvents.push_back(e);
    }

private:
    struct EmergentEvent
    {
        std::string eventId;
        std::string regionId;
        float       severity01 = 0.0f;
        double      timestampSeconds = 0.0;
    };

    RNG rng;
    double currentTimeSeconds = 0.0;

    std::unordered_map<std::string, NPCVoiceProfile> npcProfiles;
    std::vector<DialogueTemplate> templates;
    std::vector<EmergentEvent> emergentEvents;

    // Per‑NPC per‑function last fire time
    struct CooldownKey
    {
        std::string npcId;
        DialogueFunction function;

        bool operator==(const CooldownKey& other) const
        {
            return npcId == other.npcId && function == other.function;
        }
    };

    struct CooldownKeyHasher
    {
        std::size_t operator()(const CooldownKey& key) const
        {
            std::hash<std::string> h1;
            std::hash<int> h2;
            return (h1(key.npcId) ^ (h2(static_cast<int>(key.function)) << 1));
        }
    };

    std::unordered_map<CooldownKey, double, CooldownKeyHasher> lastFireTimestamps;

private:
    // --------------------------------------------------
    // Template loading / initialization
    // --------------------------------------------------
    void InitializeDefaultTemplates()
    {
        // In production you would load these from Loreway YAML/JSON,
        // already tagged with KG IDs and reliability flags.[file:1]

        // Example: short dread line, forest village, generic villager.
        DialogueTemplate t1;
        t1.id = "ONB_FOREST_DREAD_01";
        t1.function = DialogueFunction::Dread;
        t1.reliability = ReliabilityTag::Unknown;
        t1.regionTone = RegionTone::ForestVillage;
        t1.allowedRoles = { SpeakerSocialRole::Villager, SpeakerSocialRole::Hermit };
        t1.text = "The trees remember what the village forgets.";
        t1.weight = 2.0f;
        t1.condition = [](const DialogueContext& ctx, const NPCVoiceProfile&)
        {
            return ctx.isNight && ctx.threatLevel01 > 0.3f;
        };
        templates.push_back(t1);

        // Example: explicit lie about disappearances, flagged KnownFalse
        DialogueTemplate t2;
        t2.id = "ONB_VILLAGER_LIE_DISAPPEAR";
        t2.function = DialogueFunction::Misdirection;
        t2.reliability = ReliabilityTag::KnownFalse;
        t2.regionTone = RegionTone::ForestVillage;
        t2.allowedRoles = { SpeakerSocialRole::Villager };
        t2.requiredEventIds = { "EV_WELL_COLLAPSE_ASHDITCH" };
        t2.text = "No one has gone missing since they fixed the wires.";
        t2.weight = 1.0f;
        t2.condition = [](const DialogueContext& ctx, const NPCVoiceProfile&)
        {
            return ctx.isNight; // later, KG can confirm this conflicts with posters
        };
        templates.push_back(t2);

        // Example: ritual hint line tied to a taboo
        DialogueTemplate t3;
        t3.id = "ONB_RITUAL_HINT_WHISTLE";
        t3.function = DialogueFunction::RitualHint;
        t3.reliability = ReliabilityTag::Partial;
        t3.regionTone = RegionTone::ForestVillage;
        t3.allowedRoles = { SpeakerSocialRole::Villager, SpeakerSocialRole::Priest };
        t3.requiredTabooIds = { "TABS_WHISTLE_AT_NIGHT" };
        t3.text = "If the branches start singing, count your teeth and keep walking.";
        t3.weight = 1.5f;
        t3.condition = [](const DialogueContext& ctx, const NPCVoiceProfile&)
        {
            return ctx.isNight && ctx.threatLevel01 > 0.2f;
        };
        templates.push_back(t3);

        // Example: bureaucratic tone, block apartment
        DialogueTemplate t4;
        t4.id = "BUREAU_FLAT_NOTICE_01";
        t4.function = DialogueFunction::Bureaucratic;
        t4.reliability = ReliabilityTag::Truthful;
        t4.regionTone = RegionTone::SovietApartment;
        t4.allowedRoles = { SpeakerSocialRole::Bureaucrat, SpeakerSocialRole::Doctor };
        t4.text = "If you hear singing in the stairwell, do not open your door. The building committee is handling it.";
        t4.weight = 1.0f;
        t4.condition = [](const DialogueContext& ctx, const NPCVoiceProfile&)
        {
            return ctx.isIndoors && ctx.isNight;
        };
        templates.push_back(t4);

        // Example: pain bark with small body substitution
        DialogueTemplate t5;
        t5.id = "GENERIC_PAIN_01";
        t5.function = DialogueFunction::Pain;
        t5.reliability = ReliabilityTag::Truthful;
        t5.text = "Hold still. You're leaking like the old well.";
        t5.weight = 3.0f;
        t5.condition = [](const DialogueContext& ctx, const NPCVoiceProfile&)
        {
            return ctx.playerIsBleeding;
        };
        templates.push_back(t5);

        // You can keep adding templates or load them from external data here.
    }

    // --------------------------------------------------
    // Trigger → Function mapping
    // --------------------------------------------------
    DialogueFunction MapTriggerToFunction(const std::string& triggerTag,
                                          const DialogueContext& ctx,
                                          const NPCVoiceProfile& profile) const
    {
        // High‑priority explicit triggers
        if (triggerTag == "on_enemy_spotted")
            return DialogueFunction::ThreatBark;
        if (triggerTag == "on_player_pain")
            return DialogueFunction::Pain;
        if (triggerTag == "on_player_surprised")
            return DialogueFunction::Surprise;

        if (triggerTag == "on_player_breaks_taboo")
            return DialogueFunction::RitualHint;

        if (triggerTag == "on_night_heartbeat")
        {
            // Choose between dread vs rumor based on superstition
            if (profile.superstition01 > 0.6f)
                return DialogueFunction::Dread;
            return DialogueFunction::Rumor;
        }

        if (triggerTag == "on_enter_safehouse")
        {
            if (profile.bureaucratic01 > 0.5f)
                return DialogueFunction::Bureaucratic;
            return DialogueFunction::NeutralAmbient;
        }

        // Fallback: choose something mood‑aligned
        if (ctx.threatLevel01 > 0.6f)
            return DialogueFunction::Dread;
        if (!ctx.knownRumorIds.empty())
            return DialogueFunction::Rumor;

        return DialogueFunction::NeutralAmbient;
    }

    // --------------------------------------------------
    // Cooldown handling
    // --------------------------------------------------
    bool CanFire(const NPCVoiceProfile& profile, DialogueFunction fn)
    {
        CooldownKey key{ profile.npcId, fn };
        auto it = lastFireTimestamps.find(key);
        float cooldown = 0.0f;
        auto jt = profile.cooldownSeconds.find(fn);
        if (jt != profile.cooldownSeconds.end())
            cooldown = jt->second;

        if (cooldown <= 0.0f)
            return true;

        if (it == lastFireTimestamps.end())
            return true;

        double lastTime = it->second;
        if (currentTimeSeconds - lastTime >= cooldown)
            return true;

        return false;
    }

    void TouchCooldown(const std::string& npcId, DialogueFunction fn)
    {
        CooldownKey key{ npcId, fn };
        lastFireTimestamps[key] = currentTimeSeconds;
    }

    // --------------------------------------------------
    // Candidate collection
    // --------------------------------------------------
    void CollectCandidates(const DialogueContext& ctx,
                           const NPCVoiceProfile& profile,
                           DialogueFunction fn,
                           std::vector<const DialogueTemplate*>& out) const
    {
        out.clear();

        for (const auto& t : templates)
        {
            if (t.function != fn)
                continue;

            // Region filter (soft: allow mismatch with lower weight if needed)
            if (t.regionTone != ctx.regionTone && t.regionTone != RegionTone::ForestVillage && ctx.regionTone != RegionTone::ForestVillage)
                continue;

            // Role filter
            if (!t.allowedRoles.empty())
            {
                bool roleOk = false;
                for (auto r : t.allowedRoles)
                {
                    if (r == profile.role)
                    {
                        roleOk = true;
                        break;
                    }
                }
                if (!roleOk) continue;
            }

            // Required taboos
            bool taboosOk = true;
            for (const auto& tb : t.requiredTabooIds)
            {
                if (ctx.activeTabooIds.find(tb) == ctx.activeTabooIds.end())
                {
                    taboosOk = false;
                    break;
                }
            }
            if (!taboosOk) continue;

            // Required events
            bool eventsOk = true;
            for (const auto& ev : t.requiredEventIds)
            {
                if (ctx.recentEventIds.find(ev) == ctx.recentEventIds.end())
                {
                    eventsOk = false;
                    break;
                }
            }
            if (!eventsOk) continue;

            // Location blacklist
            if (!ctx.locationId.empty())
            {
                bool locationBlocked = false;
                for (const auto& loc : t.disallowedLocationIds)
                {
                    if (loc == ctx.locationId)
                    {
                        locationBlocked = true;
                        break;
                    }
                }
                if (locationBlocked) continue;
            }

            // Custom condition
            if (t.condition && !t.condition(ctx, profile))
                continue;

            out.push_back(&t);
        }

        // If no candidates and function is not Threat/Pain, you may decide to
        // fall back to NeutralAmbient in calling code.
    }

    // --------------------------------------------------
    // Weighted selection
    // --------------------------------------------------
    const DialogueTemplate* PickTemplateWeighted(const std::vector<const DialogueTemplate*>& candidates)
    {
        if (candidates.empty())
            return nullptr;

        float totalWeight = 0.0f;
        for (auto* t : candidates)
            totalWeight += t->weight;

        if (totalWeight <= 0.0f)
            return candidates[0];

        float roll = rng.RandomFloat(0.0f, totalWeight);
        float cumulative = 0.0f;

        for (auto* t : candidates)
        {
            cumulative += t->weight;
            if (roll <= cumulative)
                return t;
        }
        return candidates.back();
    }

    // --------------------------------------------------
    // Template realization: token replacement + style
    // --------------------------------------------------
    std::string RealizeTemplate(const DialogueTemplate& t,
                                const DialogueContext& ctx,
                                const NPCVoiceProfile& profile)
    {
        std::string base = t.text;

        // Basic token replacements. In production these would come from KG queries.[file:1]
        ReplaceToken(base, "{PLAYER_CALLSIGN}", PickPlayerCallsign(profile));
        ReplaceToken(base, "{LOCAL_SPIRIT}", PickLocalSpiritEpithet(ctx));
        ReplaceToken(base, "{TABOO}", PickTabooPhrase(ctx));
        ReplaceToken(base, "{PLACE}", PickPlaceName(ctx));
        ReplaceToken(base, "{BODYSYMPTOM}", PickBodySymptom(ctx));

        // Style pass: adjust punctuation and add micro‑tails based on sliders.
        ApplyStyleNoise(base, profile, t.function);

        return base;
    }

    void ReplaceToken(std::string& text, const std::string& token, const std::string& value)
    {
        if (token.empty()) return;
        std::size_t pos = 0;
        while ((pos = text.find(token, pos)) != std::string::npos)
        {
            text.replace(pos, token.length(), value);
            pos += value.length();
        }
    }

    std::string PickPlayerCallsign(const NPCVoiceProfile& profile)
    {
        // Simple example – in Cell you can base this on reputation, faction, etc.[file:1]
        switch (profile.role)
        {
            case SpeakerSocialRole::Bureaucrat: return "citizen";
            case SpeakerSocialRole::Soldier:    return "strannik";
            case SpeakerSocialRole::Priest:     return "soul";
            default:                             return "you";
        }
    }

    std::string PickLocalSpiritEpithet(const DialogueContext& ctx)
    {
        // For demo: tie to region tone.[file:1]
        switch (ctx.regionTone)
        {
            case RegionTone::ForestVillage:   return "the bent one";
            case RegionTone::SovietApartment: return "the stairwell listener";
            case RegionTone::IndustrialBlock: return "the thing in the ducts";
            case RegionTone::BorderOutpost:   return "the one beyond the fence";
        }
        return "it";
    }

    std::string PickTabooPhrase(const DialogueContext& ctx)
    {
        if (ctx.activeTabooIds.empty())
            return "the old rules";

        // Take one arbitrary taboo ID and map to short phrase.[file:1]
        const std::string& anyId = *ctx.activeTabooIds.begin();
        if (anyId == "TABS_WHISTLE_AT_NIGHT")
            return "no whistling after dark";
        if (anyId == "TABS_NO_BUCKETS_UPSIDE_DOWN")
            return "never leave a bucket mouth‑down";

        return "the village law";
    }

    std::string PickPlaceName(const DialogueContext& ctx)
    {
        if (ctx.locationId.find("ASHDITCH") != std::string::npos)
            return "Ash Ditch";
        if (ctx.locationId.find("BLOCK_A") != std::string::npos)
            return "Block A stairwell";

        return "this place";
    }

    std::string PickBodySymptom(const DialogueContext& ctx)
    {
        if (ctx.playerIsBleeding)
            return "bleeding";
        if (ctx.playerLowHealth)
            return "shaking";
        return "breathing";
    }

    void ApplyStyleNoise(std::string& line,
                         const NPCVoiceProfile& profile,
                         DialogueFunction fn)
    {
        // Shorten or slightly fragment lines when verbosity is low.[file:1]
        if (profile.verbosity01 < 0.3f)
        {
            if (line.size() > 60)
            {
                // Cut at the last space before 60 and add ellipsis occasionally.
                std::size_t cutPos = line.rfind(' ', 60);
                if (cutPos != std::string::npos)
                {
                    line.erase(cutPos);
                    if (rng.Chance(0.5f))
                        line += "...";
                }
            }
        }

        // Add resigned tails for high fatalism.
        if (profile.fatalism01 > 0.6f && rng.Chance(0.4f))
        {
            if (fn == DialogueFunction::Dread || fn == DialogueFunction::Rumor)
            {
                static const std::vector<std::string> tails = {
                    " You get used to it.",
                    " It was worse before.",
                    " It never really stops.",
                    " That's just how it is here."
                };
                int idx = rng.RandomInt(0, static_cast<int>(tails.size()) - 1);
                line += tails[idx];
            }
        }

        // Add bureaucratic flavor.
        if (profile.bureaucratic01 > 0.5f && fn == DialogueFunction::Bureaucratic)
        {
            if (rng.Chance(0.5f))
                line = "According to regulations, " + line;
        }

        // Very small chance of fragmented syntax for high superstition.
        if (profile.superstition01 > 0.7f && rng.Chance(0.35f))
        {
            if (line.back() == '.')
                line.back() = ' ';
            line += "Just... don't ask.";
        }
    }
};

// ------------------------------------------------------
// Example usage in a game loop / AI script
// ------------------------------------------------------
#ifdef DIALOGUE_SYSTEM_DEMO_MAIN
#include <iostream>

int main()
{
    DialogueSystem dlg;
    double timeSec = 0.0;
    dlg.SetCurrentTimeSeconds(timeSec);

    NPCVoiceProfile oldNeighbor;
    oldNeighbor.npcId = "NPC_OLD_NEIGHBOR";
    oldNeighbor.displayName = "Old Neighbor";
    oldNeighbor.role = SpeakerSocialRole::Villager;
    oldNeighbor.verbosity01 = 0.4f;
    oldNeighbor.superstition01 = 0.9f;
    oldNeighbor.bureaucratic01 = 0.0f;
    oldNeighbor.religiosity01 = 0.5f;
    oldNeighbor.cruelty01 = 0.3f;
    oldNeighbor.unreliability01 = 0.5f;
    oldNeighbor.fatalism01 = 0.8f;
    oldNeighbor.dialectTag = "rural_polish_like";
    oldNeighbor.personalMotifs = { "missing_children", "forest_debts" };

    dlg.RegisterNPCProfile(oldNeighbor);

    DialogueContext ctx;
    ctx.regionTone = RegionTone::ForestVillage;
    ctx.isNight = true;
    ctx.threatLevel01 = 0.5f;
    ctx.locationId = "PLC_VILLAGE_ASHDITCH";
    ctx.activeTabooIds.insert("TABS_WHISTLE_AT_NIGHT");
    ctx.recentEventIds.insert("EV_WELL_COLLAPSE_ASHDITCH");
    ctx.playerLowHealth = true;
    ctx.playerIsBleeding = true;

    // Simulate heartbeat event
    std::string line = dlg.GenerateLine("NPC_OLD_NEIGHBOR",
                                        "on_night_heartbeat",
                                        ctx);
    if (!line.empty())
        std::cout << oldNeighbor.displayName << ": " << line << "\n";

    // Simulate taboo break
    timeSec += 10.0;
    dlg.SetCurrentTimeSeconds(timeSec);
    std::string line2 = dlg.GenerateLine("NPC_OLD_NEIGHBOR",
                                         "on_player_breaks_taboo",
                                         ctx);
    if (!line2.empty())
        std::cout << oldNeighbor.displayName << ": " << line2 << "\n";

    return 0;
}
#endif
