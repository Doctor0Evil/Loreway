// src/narrative/DialogueDataLoader.cpp

#include "DialogueDataLoader.h"
#include <fstream>
#include <sstream>
#include <algorithm>
#include <cctype>

// You may replace this with your engine's JSON/YAML library.
// Here we assume a very simple JSON structure and a basic parser stub.
#include "ThirdParty/JsonLite.h" // Replace with your actual JSON include.

static std::string ToLower(const std::string& s)
{
    std::string r = s;
    std::transform(r.begin(), r.end(), r.begin(),
                   [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
    return r;
}

DialogueFunction DialogueDataLoader::ParseFunction(const std::string& s)
{
    const std::string v = ToLower(s);
    if (v == "neutralambient") return DialogueFunction::NeutralAmbient;
    if (v == "dread")          return DialogueFunction::Dread;
    if (v == "misdirection")   return DialogueFunction::Misdirection;
    if (v == "ritualhint")     return DialogueFunction::RitualHint;
    if (v == "rumor")          return DialogueFunction::Rumor;
    if (v == "bureaucratic")   return DialogueFunction::Bureaucratic;
    if (v == "threatbark")     return DialogueFunction::ThreatBark;
    if (v == "pain")           return DialogueFunction::Pain;
    if (v == "surprise")       return DialogueFunction::Surprise;
    return DialogueFunction::NeutralAmbient;
}

ReliabilityTag DialogueDataLoader::ParseReliability(const std::string& s)
{
    const std::string v = ToLower(s);
    if (v == "truthful")    return ReliabilityTag::Truthful;
    if (v == "partial")     return ReliabilityTag::Partial;
    if (v == "knownfalse")  return ReliabilityTag::KnownFalse;
    return ReliabilityTag::Unknown;
}

RegionTone DialogueDataLoader::ParseRegionTone(const std::string& s)
{
    const std::string v = ToLower(s);
    if (v == "forestvillage")   return RegionTone::ForestVillage;
    if (v == "sovietapartment") return RegionTone::SovietApartment;
    if (v == "industrialblock") return RegionTone::IndustrialBlock;
    if (v == "borderoutpost")   return RegionTone::BorderOutpost;
    return RegionTone::ForestVillage;
}

SpeakerSocialRole DialogueDataLoader::ParseRole(const std::string& s)
{
    const std::string v = ToLower(s);
    if (v == "villager")   return SpeakerSocialRole::Villager;
    if (v == "bureaucrat") return SpeakerSocialRole::Bureaucrat;
    if (v == "priest")     return SpeakerSocialRole::Priest;
    if (v == "smuggler")   return SpeakerSocialRole::Smuggler;
    if (v == "soldier")    return SpeakerSocialRole::Soldier;
    if (v == "doctor")     return SpeakerSocialRole::Doctor;
    if (v == "hermit")     return SpeakerSocialRole::Hermit;
    return SpeakerSocialRole::Villager;
}

// Very strict external IP guard.
// In practice, back this with your IDEGenerationGuardrails blacklist. [file:1]
bool DialogueDataLoader::ContainsForbiddenIPTokens(const std::string& text)
{
    static const std::vector<std::string> forbiddenMarkers = {
        "™", "®"
    };

    for (const std::string& m : forbiddenMarkers)
    {
        if (text.find(m) != std::string::npos)
            return true;
    }

    return false;
}

void DialogueDataLoader::ValidateKGLinks(const DialogueTemplate& t,
                                         const LorewayKGView& kg,
                                         std::vector<std::string>& outWarnings)
{
    for (const auto& tb : t.requiredTabooIds)
    {
        if (!kg.HasTaboo(tb))
        {
            outWarnings.push_back("DialogueTemplate '" + t.id +
                "' references missing Taboo ID '" + tb + "'");
        }
    }

    for (const auto& ev : t.requiredEventIds)
    {
        if (!kg.HasEvent(ev))
        {
            outWarnings.push_back("DialogueTemplate '" + t.id +
                "' references missing Event ID '" + ev + "'");
        }
    }

    // DisallowedLocationIds are game/level IDs, not KG Place IDs,
    // so they are not validated here.
}

bool DialogueDataLoader::LoadDialogueUnitsFromFile(const std::string& path,
                                                   const LorewayKGView& kg,
                                                   DialogueSystem& outSystem,
                                                   std::vector<std::string>& outWarnings)
{
    std::ifstream in(path.c_str());
    if (!in.is_open())
    {
        outWarnings.push_back("DialogueDataLoader: Failed to open file '" + path + "'");
        return false;
    }

    std::stringstream buffer;
    buffer << in.rdbuf();
    const std::string content = buffer.str();
    in.close();

    // Parse JSON (array of DialogueUnit objects).
    JsonLite::Value root;
    if (!JsonLite::Parse(content, root) || !root.IsArray())
    {
        outWarnings.push_back("DialogueDataLoader: File '" + path +
                              "' is not a valid DialogueUnit array JSON.");
        return false;
    }

    for (size_t i = 0; i < root.Size(); ++i)
    {
        const JsonLite::Value& node = root[i];
        if (!node.IsObject())
            continue;

        DialogueTemplate t;
        t.condition = nullptr;
        t.weight = 1.0f;

        // Required fields
        const std::string id    = node.GetString("id", "");
        const std::string fn    = node.GetString("function", "NeutralAmbient");
        const std::string rel   = node.GetString("reliability", "Unknown");
        const std::string tone  = node.GetString("regionTone", "ForestVillage");
        const std::string text  = node.GetString("text", "");

        if (id.empty() || text.empty())
        {
            outWarnings.push_back("DialogueDataLoader: Skipping DialogueUnit with missing id/text in '" + path + "'");
            continue;
        }

        if (ContainsForbiddenIPTokens(text))
        {
            outWarnings.push_back("DialogueDataLoader: Text for '" + id +
                                  "' failed IP guardrail (forbidden markers).");
            continue;
        }

        t.id          = id;
        t.function    = ParseFunction(fn);
        t.reliability = ParseReliability(rel);
        t.regionTone  = ParseRegionTone(tone);
        t.text        = text;
        t.weight      = static_cast<float>(node.GetNumber("weight", 1.0));

        // Allowed roles
        if (node.HasMember("allowedRoles") && node["allowedRoles"].IsArray())
        {
            const JsonLite::Value& rolesArr = node["allowedRoles"];
            for (size_t r = 0; r < rolesArr.Size(); ++r)
            {
                const std::string rs = rolesArr[r].GetString("", "");
                if (!rs.empty())
                    t.allowedRoles.push_back(ParseRole(rs));
            }
        }

        // Required taboos / events / disallowed locations
        if (node.HasMember("requiredTabooIds") && node["requiredTabooIds"].IsArray())
        {
            const JsonLite::Value& arr = node["requiredTabooIds"];
            for (size_t j = 0; j < arr.Size(); ++j)
            {
                const std::string s = arr[j].GetString("", "");
                if (!s.empty())
                    t.requiredTabooIds.push_back(s);
            }
        }

        if (node.HasMember("requiredEventIds") && node["requiredEventIds"].IsArray())
        {
            const JsonLite::Value& arr = node["requiredEventIds"];
            for (size_t j = 0; j < arr.Size(); ++j)
            {
                const std::string s = arr[j].GetString("", "");
                if (!s.empty())
                    t.requiredEventIds.push_back(s);
            }
        }

        if (node.HasMember("disallowedLocationIds") && node["disallowedLocationIds"].IsArray())
        {
            const JsonLite::Value& arr = node["disallowedLocationIds"];
            for (size_t j = 0; j < arr.Size(); ++j)
            {
                const std::string s = arr[j].GetString("", "");
                if (!s.empty())
                    t.disallowedLocationIds.push_back(s);
            }
        }

        // Example simple condition flags compiled from data:
        const bool requiresNight       = node.GetBool("requiresNight", false);
        const bool requiresPlayerBleed = node.GetBool("requiresPlayerBleeding", false);
        const double minThreat         = node.GetNumber("minThreatLevel01", -1.0);

        if (requiresNight || requiresPlayerBleed || minThreat >= 0.0)
        {
            t.condition = [requiresNight, requiresPlayerBleed, minThreat]
                          (const DialogueContext& ctx, const NPCVoiceProfile&)
            {
                if (requiresNight && !ctx.isNight)
                    return false;
                if (requiresPlayerBleed && !ctx.playerIsBleeding)
                    return false;
                if (minThreat >= 0.0 && ctx.threatLevel01 < static_cast<float>(minThreat))
                    return false;
                return true;
            };
        }

        ValidateKGLinks(t, kg, outWarnings);

        // Finally, register this template into the DialogueSystem.
        outSystem.AddTemplate(t);
    }

    return true;
}
