// File: /Assets/NarrativeDB/Lore/Growlers_Moonhowl_Event.cs
// Purpose: Runtime-binding for Loreway "Lycanthropy-in-Space" entry in Cell game.

using Cell.Narrative;
using UnityEngine;

[CreateAssetMenu(fileName = "Growlers_Moonhowl_Event", menuName = "Cell/LoreEntries/GrowlersMoonhowl")]
public class GrowlersMoonhowlEvent : LoreEntryAsset
{
    [Header("Core Metadata")]
    public string entryId = "LORE-GROWLERS-MOONHOWL-01";
    public string title = "Lycanthropy-in-Space: The Moonhowl Event";
    public string region = "CELLREGIONPOL-01";

    [Header("Systemic Hooks")]
    public AudioClip ventHowlSFX;
    public AudioClip whisperWarning;
    public bool triggersVentSilenceTaboo = true;

    [Header("Linked Entities")]
    public SpiritData azureHowler;
    public LocationData hadesTheta;
    public SpeciesData growlerSpecies;
    public DialogueUnit logKeeperDialogue;

    [Header("Debug / Runtime Flags")]
    [TextArea(4, 10)] public string developerNotes =
        "First documented outbreak linking lunar psionics and Cell-nanovirus cross-speciation. "
        + "Use eventID=EVMOONHOWLDEC2063 to control ambient AI hostility.";
}
