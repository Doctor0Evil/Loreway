// src/narrative/DialogueDataLoader.h

#pragma once

#include <string>
#include <vector>
#include <unordered_set>
#include "DialogueSystem.h"

// Simple Loreway KG view for validation.
// In production, back this with your actual KG service / data.
struct LorewayKGView
{
    std::unordered_set<std::string> spiritIds;
    std::unordered_set<std::string> placeIds;
    std::unordered_set<std::string> eventIds;
    std::unordered_set<std::string> tabooIds;
    std::unordered_set<std::string> rumorIds;

    bool HasSpirit(const std::string& id)  const { return spiritIds.count(id)  > 0; }
    bool HasPlace(const std::string& id)   const { return placeIds.count(id)   > 0; }
    bool HasEvent(const std::string& id)   const { return eventIds.count(id)   > 0; }
    bool HasTaboo(const std::string& id)   const { return tabooIds.count(id)   > 0; }
    bool HasRumor(const std::string& id)   const { return rumorIds.count(id)   > 0; }
};

// Loader for compiled Loreway DialogueUnit JSON/YAML.
class DialogueDataLoader
{
public:
    // Load from a single file containing an array of DialogueUnit objects.
    // Returns true on success, false on hard failure.
    static bool LoadDialogueUnitsFromFile(const std::string& path,
                                          const LorewayKGView& kg,
                                          DialogueSystem& outSystem,
                                          std::vector<std::string>& outWarnings);

private:
    static DialogueFunction ParseFunction(const std::string& s);
    static ReliabilityTag   ParseReliability(const std::string& s);
    static RegionTone       ParseRegionTone(const std::string& s);
    static SpeakerSocialRole ParseRole(const std::string& s);

    // Hard IP guardrail: reject any external IP references in surface text.
    static bool ContainsForbiddenIPTokens(const std::string& text);

    // Validation helpers.
    static void ValidateKGLinks(const DialogueTemplate& t,
                                const LorewayKGView& kg,
                                std::vector<std::string>& outWarnings);
};
