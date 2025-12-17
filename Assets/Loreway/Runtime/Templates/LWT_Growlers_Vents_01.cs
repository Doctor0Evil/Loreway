// File: Assets/Loreway/Runtime/Templates/LWT_Growlers_Vents_01.cs

using UnityEngine;
using Cell.Loreway;

[CreateAssetMenu(
    fileName = "LWT_Growlers_Vents_01",
    menuName = "Cell/Loreway/Templates/Growlers Vents")]
public class LWT_Growlers_Vents_01 : LorewayNarrativeTemplate
{
    private void Reset()
    {
        TemplateId = "LWT-GROWLERS-VENTS-01";
        MaxBeats = 3;

        SupportedModes = new[] {
            HorrorMode.Cosmic,
            HorrorMode.Body,
            HorrorMode.Survival
        };

        SupportedFunctions = new[] {
            HorrorFunction.Dread,
            HorrorFunction.Uncanny,
            HorrorFunction.MoralAnxiety
        };

        LandscapeMotifs = new[] {
            "vent labyrinth sweating cold condensation",
            "frost-whitened bulkheads scored by claws",
            "cargo maze stacked with gutted containers",
            "half-lit maintenance catwalks over dark shafts",
            "oxygen-scarred walls peppered with failed patch-jobs"
        };

        SpiritMotifs = new[] {
            "thin blue corona clinging to the station’s ribs",
            "radio hiss that tastes like old blood on the teeth",
            "breath that arrives before the sound",
            "invisible weight listening at every grille",
            "moonlight smeared across metal like dried antiseptic"
        };

        HumanMiseryMotifs = new[] {
            "oxygen debt making thoughts drag in the skull",
            "starving bellies arguing louder than the alarms",
            "tremor in the fingers from reusing filters too long",
            "sleep stolen in thirty-second blinks between howls",
            "paranoia that every voice over comms is already dead"
        };

        TabooIds = new[] { "TABSVENTSILENCE01" };
        SpiritIds = new[] { "SPRTAZUREHOWLER01" };

        BeatPrompts = new[] {
            "Your own words do not come back right from the vent.",
            "Something deeper in the ducts adjusts its breathing to yours.",
            "The station quietly updates its idea of how to reach you."
        };

        MaxLinesPerUnit = 5;
        MaxWordsPerLine = 18;
        RequireContradictionLine = true;
        RequireImpliedRuleLine = true;
    }
}
