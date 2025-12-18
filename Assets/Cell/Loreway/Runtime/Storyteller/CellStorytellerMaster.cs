// Path: Assets/Cell/Loreway/Runtime/Storyteller/CellStorytellerMaster.cs

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CellStorytellerMaster
/// Central narrative director for Cell's Loreway-based Slavic horror storytelling.
/// Consumes KG / asset data produced by Loreway (Spirits, Places, Events, Rumors,
/// DialogueUnits, NarrativeScenes, RitualScripts, AudioAmbienceSpecs, BackstoryPackets).
/// </summary>
namespace Cell.Loreway.Storyteller
{
    #region Storyteller Enums & Data

    public enum StoryMode
    {
        SlowBurn,
        Escalation,
        Panic,
        Comedown,
        DreamLogic
    }

    /// <summary>
    /// High-level temperature profile for narrative behavior.
    /// Values are normalized 0-1.
    /// </summary>
    [Serializable]
    public class StoryTemperatureProfile
    {
        [Range(0f, 1f)] public float NarrativeTemperature = 0.25f;
        [Range(0f, 1f)] public float HorrorTemperature = 0.25f;
        [Range(0f, 1f)] public float SurrealTemperature = 0.10f;

        public StoryTemperatureProfile Clone()
        {
            return new StoryTemperatureProfile
            {
                NarrativeTemperature = NarrativeTemperature,
                HorrorTemperature = HorrorTemperature,
                SurrealTemperature = SurrealTemperature
            };
        }
    }

    /// <summary>
    /// Snapshot of player/world state that the storyteller reasons over.
    /// This struct should be populated by game systems each frame/tick.
    /// </summary>
    [Serializable]
    public struct StoryWorldState
    {
        public string RegionId;                 // e.g. "CELLREGIONPOL-01"
        public string CurrentPlaceId;           // KG Place.id nearest to the player
        public float NormalizedTimeOfNight;     // 0 = day, 1 = deep night
        public float PlayerHealth01;            // 0-1
        public float PlayerSanity01;            // 0-1
        public float ResourceScarcity01;        // 0-1 (0 = comfortable, 1 = desperate)
        public int RecentTabooBreaks;           // last N minutes/sections
        public bool InCombatOrThreat;
        public bool JustChangedRegion;
        public bool JustSurvivedNight;
        public int NightsSurvived;
        public int MajorStoryBeatsCompleted;

        // Soft flags
        public bool InSafeSpace;
        public bool InHauntedSpace;
    }

    /// <summary>
    /// Simple representation of a Loreway NarrativeScene asset.
    /// This mirrors the LorewayMasterSpec NarrativeScene fields.
    /// </summary>
    [Serializable]
    public class NarrativeScene
    {
        public string Id;
        public string Scope;               // "local", "area", "global"
        public string LocationId;          // KG Place.id
        public List<SceneBeat> Beats = new List<SceneBeat>();
        public List<string> LinkedEventIds = new List<string>(); // Event.id
        public List<string> LinkedDialogueUnitIds = new List<string>();
        public string HorrorFunction;      // "dread", "uncanny", "shock", "disgust", "moralanxiety"
        public string SystemHooks;         // Arbitrary tags for systems
        public bool ExternalReferenceAllowed; // Must be false for shipping content
    }

    [Serializable]
    public class SceneBeat
    {
        public string Id;
        public string Description;
        public string HorrorFunction;
        public string SystemHooks;
    }

    /// <summary>
    /// Minimal Loreway Event node mirror for driving beats.
    /// </summary>
    [Serializable]
    public class LoreEvent
    {
        public string Id;
        public string DisplayName;
        public string RegionId;
        public string Type;            // "localcatastrophe", "ritualfailure", "disappearance", etc.
        public string LocationId;
        public int Year;
        public string Season;
        public string TimeOfDay;
        public List<string> NarrativeTags;
        public List<string> RumorIds;
        public bool ExternalReferenceAllowed;
    }

    /// <summary>
    /// Minimal Rumor node representation.
    /// </summary>
    [Serializable]
    public class Rumor
    {
        public string Id;
        public string RegionId;
        public string Topic;           // "disappearance", "treachery", etc.
        public List<string> TextVariants;
        public string TruthStatus;     // "false", "partial", "true", "unknown"
        public string LinkedEventId;
        public bool ExternalReferenceAllowed;
    }

    /// <summary>
    /// Minimal DialogueUnit representation.
    /// Each DialogueUnit is a small exchange with reliability flags and functions.
    /// </summary>
    [Serializable]
    public class DialogueUnit
    {
        public string Id;
        public string SceneId;         // Optional NarrativeScene link
        public string StyleProfile;    // "ruralsparse", "bureaucraticcold", etc.
        public int LineLimit;
        public int MaxWordsPerLine;
        public List<DialogueLine> Lines;
        public string HorrorFunction;  // e.g. "dread", "uncanny"
        public string SystemTags;      // e.g. "onfirstenterlocation"
        public bool ExternalReferenceAllowed;
    }

    [Serializable]
    public class DialogueLine
    {
        public string SpeakerId;       // Character.id or NPC tag
        public string Text;
        public string Intent;          // "implyrule", "contradictevidence", etc.
        public string Reliability;     // "reliable", "ambiguous", "knownfalse"
    }

    /// <summary>
    /// Represents a single "beat" decision from the storyteller, which
    /// the rest of the game can subscribe to and enact.
    /// </summary>
    public struct StoryBeatDecision
    {
        public string Reason;          // Textual debug summary
        public NarrativeScene Scene;   // Optional
        public SceneBeat SceneBeat;    // Optional
        public LoreEvent Event;        // Optional
        public Rumor Rumor;            // Optional
        public DialogueUnit Dialogue;  // Optional

        public bool IsEmpty =>
            Scene == null && Event == null && Rumor == null && Dialogue == null;
    }

    #endregion

    /// <summary>
    /// Main Storyteller MonoBehaviour.
    /// Attach to a central game object (e.g., "LorewayDirector") and feed with
    /// KG / asset repositories. Other systems subscribe to OnStoryBeatChosen.
    /// </summary>
    public class CellStorytellerMaster : MonoBehaviour
    {
        [Header("Loreway Data Sources")]
        [Tooltip("Repository providing access to Loreway scenes, events, rumors, dialogues, etc.")]
        public LorewayRepository Repository;

        [Header("Story Mode & Temperatures")]
        public StoryMode CurrentMode = StoryMode.SlowBurn;
        public StoryTemperatureProfile BaseProfile = new StoryTemperatureProfile();
        public StoryTemperatureProfile CurrentProfile = new StoryTemperatureProfile();

        [Header("Timing / Pacing")]
        [Tooltip("Seconds between storyteller evaluation ticks at NarrativeTemperature=0.5.")]
        public float BaseTickIntervalSeconds = 20f;

        [Tooltip("Random +/- seconds on top of calculated interval.")]
        public float TickJitterSeconds = 5f;

        [Tooltip("Minimum time between aggressive beats in seconds.")]
        public float MinAggressiveBeatCooldown = 40f;

        private float _timeToNextTick;
        private float _timeSinceLastAggressiveBeat;

        [Header("Debug")]
        public bool DebugLogging = false;

        // Latest world state injected by the game.
        private StoryWorldState _currentWorldState;

        // Event fired when a new beat is chosen.
        public event Action<StoryBeatDecision> OnStoryBeatChosen;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Repository == null)
            {
                Debug.LogError("[CellStorytellerMaster] LorewayRepository is not assigned.");
            }

            CurrentProfile = BaseProfile.Clone();
            ScheduleNextTick(initial: true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timeToNextTick -= dt;
            _timeSinceLastAggressiveBeat += dt;

            if (_timeToNextTick <= 0f)
            {
                EvaluateTick();
                ScheduleNextTick(initial: false);
            }
        }

        #endregion

        #region External API

        /// <summary>
        /// Called by external systems (player controller, region manager, etc.)
        /// to update the world state the storyteller reasons over.
        /// </summary>
        public void UpdateWorldState(StoryWorldState state)
        {
            _currentWorldState = state;
        }

        /// <summary>
        /// Force the storyteller into a specific mode, overriding usual progression.
        /// </summary>
        public void ForceMode(StoryMode mode, StoryTemperatureProfile overrideProfile = null)
        {
            CurrentMode = mode;
            if (overrideProfile != null)
                CurrentProfile = overrideProfile.Clone();
        }

        /// <summary>
        /// Manually trigger an immediate evaluation tick (e.g., after a key event).
        /// </summary>
        public void ForceImmediateTick()
        {
            EvaluateTick();
            ScheduleNextTick(initial: false);
        }

        #endregion

        #region Tick Evaluation

        private void ScheduleNextTick(bool initial)
        {
            float baseInterval = BaseTickIntervalSeconds;

            // Faster ticks at higher narrative temperature (down to 40% base).
            float narrativeFactor = Mathf.Lerp(1.4f, 0.4f, CurrentProfile.NarrativeTemperature);
            float interval = baseInterval * narrativeFactor;

            float jitter = UnityEngine.Random.Range(-TickJitterSeconds, TickJitterSeconds);
            _timeToNextTick = Mathf.Max(5f, interval + jitter);

            if (initial && DebugLogging)
            {
                Debug.Log($"[Storyteller] Initial tick in {_timeToNextTick:F1}s");
            }
        }

        private void EvaluateTick()
        {
            if (Repository == null || !Repository.IsInitialized)
                return;

            // 1. Update mode and temperature profile based on world state.
            UpdateModeAndTemperatures();

            // 2. Query candidates.
            List<NarrativeScene> sceneCandidates;
            List<LoreEvent> eventCandidates;
            List<Rumor> rumorCandidates;
            List<DialogueUnit> dialogueCandidates;

            QueryCandidates(out sceneCandidates, out eventCandidates, out rumorCandidates, out dialogueCandidates);

            // 3. Score & pick a beat.
            StoryBeatDecision decision = PickBestBeat(sceneCandidates, eventCandidates, rumorCandidates, dialogueCandidates);

            // 4. Emit beat.
            if (!decision.IsEmpty)
            {
                if (DebugLogging)
                {
                    Debug.Log($"[Storyteller] Chosen beat: {decision.Reason}");
                }

                if (OnStoryBeatChosen != null)
                    OnStoryBeatChosen.Invoke(decision);

                // Track aggressive beats for cooldown.
                if (IsAggressive(decision))
                {
                    _timeSinceLastAggressiveBeat = 0f;
                }
            }
            else if (DebugLogging)
            {
                Debug.Log("[Storyteller] No suitable beat found this tick.");
            }
        }

        #endregion

        #region Mode & Temperature Logic

        private void UpdateModeAndTemperatures()
        {
            var s = _currentWorldState;

            // Basic progression: early nights = SlowBurn; mid = Escalation; late + low sanity = Panic or DreamLogic.
            if (s.NightsSurvived <= 1)
            {
                CurrentMode = StoryMode.SlowBurn;
            }
            else if (s.NightsSurvived <= 3)
            {
                CurrentMode = StoryMode.Escalation;
            }
            else
            {
                // Late game modes depend on sanity and taboo breaks.
                if (s.PlayerSanity01 < 0.35f || s.RecentTabooBreaks >= 3)
                {
                    // Flip between Panic and DreamLogic depending on sanity vs threat.
                    if (s.PlayerSanity01 < 0.2f)
                        CurrentMode = StoryMode.DreamLogic;
                    else
                        CurrentMode = StoryMode.Panic;
                }
                else
                {
                    CurrentMode = StoryMode.Comedown;
                }
            }

            // Temperature shaping per mode.
            switch (CurrentMode)
            {
                case StoryMode.SlowBurn:
                    CurrentProfile.NarrativeTemperature = Mathf.Lerp(CurrentProfile.NarrativeTemperature, 0.25f, 0.15f);
                    CurrentProfile.HorrorTemperature = Mathf.Lerp(CurrentProfile.HorrorTemperature, 0.20f, 0.15f);
                    CurrentProfile.SurrealTemperature = Mathf.Lerp(CurrentProfile.SurrealTemperature, 0.10f, 0.10f);
                    break;

                case StoryMode.Escalation:
                    CurrentProfile.NarrativeTemperature = Mathf.Lerp(CurrentProfile.NarrativeTemperature, 0.55f, 0.20f);
                    CurrentProfile.HorrorTemperature = Mathf.Lerp(CurrentProfile.HorrorTemperature, 0.65f, 0.20f);
                    CurrentProfile.SurrealTemperature = Mathf.Lerp(CurrentProfile.SurrealTemperature, 0.20f, 0.10f);
                    break;

                case StoryMode.Panic:
                    CurrentProfile.NarrativeTemperature = Mathf.Lerp(CurrentProfile.NarrativeTemperature, 0.85f, 0.25f);
                    CurrentProfile.HorrorTemperature = Mathf.Lerp(CurrentProfile.HorrorTemperature, 0.95f, 0.25f);
                    CurrentProfile.SurrealTemperature = Mathf.Lerp(CurrentProfile.SurrealTemperature, 0.35f, 0.15f);
                    break;

                case StoryMode.Comedown:
                    CurrentProfile.NarrativeTemperature = Mathf.Lerp(CurrentProfile.NarrativeTemperature, 0.50f, 0.20f);
                    CurrentProfile.HorrorTemperature = Mathf.Lerp(CurrentProfile.HorrorTemperature, 0.40f, 0.20f);
                    CurrentProfile.SurrealTemperature = Mathf.Lerp(CurrentProfile.SurrealTemperature, 0.25f, 0.15f);
                    break;

                case StoryMode.DreamLogic:
                    CurrentProfile.NarrativeTemperature = Mathf.Lerp(CurrentProfile.NarrativeTemperature, 0.70f, 0.20f);
                    CurrentProfile.HorrorTemperature = Mathf.Lerp(CurrentProfile.HorrorTemperature, 0.70f, 0.15f);
                    CurrentProfile.SurrealTemperature = Mathf.Lerp(CurrentProfile.SurrealTemperature, 0.90f, 0.30f);
                    break;
            }

            // Additional dynamic modulation based on immediate danger and sanity.
            if (_currentWorldState.InCombatOrThreat)
            {
                CurrentProfile.HorrorTemperature = Mathf.Clamp01(CurrentProfile.HorrorTemperature + 0.15f);
            }

            if (_currentWorldState.PlayerSanity01 < 0.4f)
            {
                CurrentProfile.SurrealTemperature = Mathf.Clamp01(CurrentProfile.SurrealTemperature + 0.10f);
            }

            if (DebugLogging)
            {
                Debug.Log($"[Storyteller] Mode={CurrentMode} | T(N={CurrentProfile.NarrativeTemperature:F2}, " +
                          $"H={CurrentProfile.HorrorTemperature:F2}, S={CurrentProfile.SurrealTemperature:F2})");
            }
        }

        #endregion

        #region Candidate Query & Scoring

        private void QueryCandidates(
            out List<NarrativeScene> sceneCandidates,
            out List<LoreEvent> eventCandidates,
            out List<Rumor> rumorCandidates,
            out List<DialogueUnit> dialogueCandidates)
        {
            sceneCandidates = Repository.GetScenesForRegionAndPlace(
                _currentWorldState.RegionId,
                _currentWorldState.CurrentPlaceId,
                CurrentMode);

            eventCandidates = Repository.GetEventsForRegionAndPlace(
                _currentWorldState.RegionId,
                _currentWorldState.CurrentPlaceId,
                CurrentMode);

            rumorCandidates = Repository.GetRumorsForRegion(
                _currentWorldState.RegionId,
                CurrentMode);

            dialogueCandidates = Repository.GetDialogueUnitsForPlaceAndMode(
                _currentWorldState.RegionId,
                _currentWorldState.CurrentPlaceId,
                CurrentMode);

            // Enforce IP safety: filter out anything with ExternalReferenceAllowed != false.
            sceneCandidates.RemoveAll(s => s.ExternalReferenceAllowed);
            eventCandidates.RemoveAll(e => e.ExternalReferenceAllowed);
            rumorCandidates.RemoveAll(r => r.ExternalReferenceAllowed);
            dialogueCandidates.RemoveAll(d => d.ExternalReferenceAllowed);
        }

        private StoryBeatDecision PickBestBeat(
            List<NarrativeScene> scenes,
            List<LoreEvent> events,
            List<Rumor> rumors,
            List<DialogueUnit> dialogues)
        {
            StoryBeatDecision best = default;
            float bestScore = float.NegativeInfinity;

            // If we recently fired something aggressive, throttle.
            bool throttleAggressive = _timeSinceLastAggressiveBeat < MinAggressiveBeatCooldown;

            // Evaluate scenes.
            foreach (var scn in scenes)
            {
                float score = ScoreScene(scn, out SceneBeat chosenBeat);
                if (throttleAggressive && IsAggressiveHorrorFunction(scn.HorrorFunction))
                    score -= 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new StoryBeatDecision
                    {
                        Reason = $"Scene {scn.Id} / beat {chosenBeat?.Id} (score={score:F2})",
                        Scene = scn,
                        SceneBeat = chosenBeat
                    };
                }
            }

            // Evaluate events.
            foreach (var ev in events)
            {
                float score = ScoreEvent(ev);
                if (throttleAggressive && IsAggressiveEventType(ev.Type))
                    score -= 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new StoryBeatDecision
                    {
                        Reason = $"Event {ev.Id} (score={score:F2})",
                        Event = ev
                    };
                }
            }

            // Evaluate rumors: especially useful in SlowBurn/Escalation.
            foreach (var rm in rumors)
            {
                float score = ScoreRumor(rm);
                if (throttleAggressive)
                    score -= 1f; // Rumors are rarely fully aggressive.

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new StoryBeatDecision
                    {
                        Reason = $"Rumor {rm.Id} (score={score:F2})",
                        Rumor = rm
                    };
                }
            }

            // Evaluate dialogues.
            foreach (var dlg in dialogues)
            {
                float score = ScoreDialogueUnit(dlg);
                if (throttleAggressive && IsAggressiveHorrorFunction(dlg.HorrorFunction))
                    score -= 3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new StoryBeatDecision
                    {
                        Reason = $"Dialogue {dlg.Id} (score={score:F2})",
                        Dialogue = dlg
                    };
                }
            }

            // If score never rose above some minimal threshold, return empty (no beat).
            if (bestScore < -2f)
                return default;

            return best;
        }

        private float ScoreScene(NarrativeScene scene, out SceneBeat chosenBeat)
        {
            chosenBeat = null;

            float score = 0f;
            var s = _currentWorldState;

            // Match horror function to mode/temperatures.
            score += HorrorFunctionAffinity(scene.HorrorFunction);

            // Scope vs player location / region.
            if (scene.Scope == "local" && scene.LocationId == s.CurrentPlaceId)
                score += 2.0f;
            else if (scene.Scope == "area")
                score += 1.0f;
            else if (scene.Scope == "global")
                score += 0.5f;

            // Favor scenes not yet fully consumed by this run (Repository should track usage).
            float freshness = Repository.GetSceneFreshness(scene.Id); // 0-1
            score += freshness * 2.0f;

            // Pick a beat inside the scene. For now, pick beat with best horror function match.
            float bestBeatScore = float.NegativeInfinity;
            foreach (var beat in scene.Beats)
            {
                float bScore = HorrorFunctionAffinity(beat.HorrorFunction);
                if (bScore > bestBeatScore)
                {
                    bestBeatScore = bScore;
                    chosenBeat = beat;
                }
            }

            score += bestBeatScore * 0.5f;

            return score + UnityEngine.Random.Range(-0.5f, 0.5f);
        }

        private float ScoreEvent(LoreEvent ev)
        {
            float score = 0f;

            score += HorrorFunctionFromEventType(ev.Type);

            if (ev.LocationId == _currentWorldState.CurrentPlaceId)
                score += 1.5f;

            // Events tied to rumors already whispered get a small bonus.
            if (ev.RumorIds != null && ev.RumorIds.Count > 0)
                score += 0.75f;

            // Avoid repeating the same event too much.
            float freshness = Repository.GetEventFreshness(ev.Id);
            score += freshness * 1.5f;

            return score + UnityEngine.Random.Range(-0.5f, 0.5f);
        }

        private float ScoreRumor(Rumor rm)
        {
            float score = 0f;

            // Rumors are stronger in SlowBurn/Escalation.
            if (CurrentMode == StoryMode.SlowBurn || CurrentMode == StoryMode.Escalation)
                score += 2.0f;
            else
                score += 0.5f;

            // Partial/unknown truths are best for Slavic horror.
            if (rm.TruthStatus == "partial" || rm.TruthStatus == "unknown")
                score += 1.0f;

            float freshness = Repository.GetRumorFreshness(rm.Id);
            score += freshness * 1.5f;

            return score + UnityEngine.Random.Range(-0.5f, 0.5f);
        }

        private float ScoreDialogueUnit(DialogueUnit dlg)
        {
            float score = 0f;

            // Dialogue preferred when not actively in combat.
            if (!_currentWorldState.InCombatOrThreat)
                score += 1.0f;

            // Style profile matching safe vs haunted spaces.
            if (_currentWorldState.InSafeSpace && dlg.StyleProfile == "ruralsparse")
                score += 0.5f;
            if (_currentWorldState.InHauntedSpace && dlg.StyleProfile == "delirious")
                score += 0.75f;

            // Horror function affinity.
            score += HorrorFunctionAffinity(dlg.HorrorFunction);

            float freshness = Repository.GetDialogueFreshness(dlg.Id);
            score += freshness * 1.5f;

            return score + UnityEngine.Random.Range(-0.5f, 0.5f);
        }

        #endregion

        #region Helpers

        private float HorrorFunctionAffinity(string horrorFunction)
        {
            float score = 0f;

            switch (horrorFunction)
            {
                case "dread":
                case "uncanny":
                    score += Mathf.Lerp(2f, 0.5f, CurrentProfile.HorrorTemperature); // early-game better
                    break;
                case "shock":
                case "disgust":
                    score += Mathf.Lerp(0.5f, 2.5f, CurrentProfile.HorrorTemperature); // late-game better
                    break;
                case "moralanxiety":
                    score += Mathf.Lerp(1f, 2f, CurrentProfile.NarrativeTemperature);
                    break;
                default:
                    score += 0.5f;
                    break;
            }

            // Surreal temperature boosts weird ones.
            if (CurrentMode == StoryMode.DreamLogic)
                score += CurrentProfile.SurrealTemperature * 1.5f;

            return score;
        }

        private float HorrorFunctionFromEventType(string type)
        {
            switch (type)
            {
                case "disappearance":
                    return 1.8f * CurrentProfile.HorrorTemperature;
                case "ritualfailure":
                    return 1.5f * CurrentProfile.NarrativeTemperature;
                case "localcatastrophe":
                    return 2.0f * CurrentProfile.HorrorTemperature;
                default:
                    return 0.5f;
            }
        }

        private bool IsAggressive(StoryBeatDecision decision)
        {
            if (decision.Event != null && IsAggressiveEventType(decision.Event.Type))
                return true;

            if (decision.Scene != null && IsAggressiveHorrorFunction(decision.Scene.HorrorFunction))
                return true;

            if (decision.Dialogue != null && IsAggressiveHorrorFunction(decision.Dialogue.HorrorFunction))
                return true;

            return false;
        }

        private bool IsAggressiveEventType(string type)
        {
            return type == "localcatastrophe" || type == "ritualfailure";
        }

        private bool IsAggressiveHorrorFunction(string horrorFunction)
        {
            return horrorFunction == "shock" || horrorFunction == "disgust";
        }

        #endregion
    }

    /// <summary>
    /// LorewayRepository
    /// Game-side facade over Loreway KG and asset taxonomy.
    /// Implement these methods to query your actual data (JSON, ScriptableObjects, DB).
    /// </summary>
    public class LorewayRepository : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }

        // Internally, store deserialized KG + assets here.
        // These should be Cell-owned, IP-clean data matching the LorewayMasterSpec.
        private Dictionary<string, NarrativeScene> _scenes = new Dictionary<string, NarrativeScene>();
        private Dictionary<string, LoreEvent> _events = new Dictionary<string, LoreEvent>();
        private Dictionary<string, Rumor> _rumors = new Dictionary<string, Rumor>();
        private Dictionary<string, DialogueUnit> _dialogues = new Dictionary<string, DialogueUnit>();

        // Usage / freshness trackers.
        private Dictionary<string, float> _sceneUsage = new Dictionary<string, float>();
        private Dictionary<string, float> _eventUsage = new Dictionary<string, float>();
        private Dictionary<string, float> _rumorUsage = new Dictionary<string, float>();
        private Dictionary<string, float> _dialogueUsage = new Dictionary<string, float>();

        private void Awake()
        {
            // TODO: Load KG and assets from JSON / ScriptableObjects / addressables.
            // Mark IsInitialized when done.
            // For now, leave as stub to be filled with actual Loreway import logic.
            IsInitialized = false;
        }

        #region Public Query API

        public List<NarrativeScene> GetScenesForRegionAndPlace(string regionId, string placeId, StoryMode mode)
        {
            var list = new List<NarrativeScene>();
            foreach (var kv in _scenes)
            {
                var scn = kv.Value;
                if (scn == null) continue;
                // Filter by place/region (assume scenes carry place/region via LocationId & KG Place).
                if (!string.IsNullOrEmpty(placeId) && scn.LocationId == placeId)
                {
                    list.Add(scn);
                }
            }
            return list;
        }

        public List<LoreEvent> GetEventsForRegionAndPlace(string regionId, string placeId, StoryMode mode)
        {
            var list = new List<LoreEvent>();
            foreach (var kv in _events)
            {
                var ev = kv.Value;
                if (ev == null) continue;
                if (!string.IsNullOrEmpty(regionId) && ev.RegionId != regionId)
                    continue;
                if (!string.IsNullOrEmpty(placeId) && ev.LocationId != placeId)
                    continue;
                list.Add(ev);
            }
            return list;
        }

        public List<Rumor> GetRumorsForRegion(string regionId, StoryMode mode)
        {
            var list = new List<Rumor>();
            foreach (var kv in _rumors)
            {
                var rm = kv.Value;
                if (rm == null) continue;
                if (!string.IsNullOrEmpty(regionId) && rm.RegionId != regionId)
                    continue;
                list.Add(rm);
            }
            return list;
        }

        public List<DialogueUnit> GetDialogueUnitsForPlaceAndMode(string regionId, string placeId, StoryMode mode)
        {
            var list = new List<DialogueUnit>();
            foreach (var kv in _dialogues)
            {
                var dlg = kv.Value;
                if (dlg == null) continue;

                // In a full implementation, DialogueUnit would carry region/place tags.
                // For now we simply return all, leaving fine-grained filtering for later.
                list.Add(dlg);
            }
            return list;
        }

        #endregion

        #region Freshness Tracking

        public float GetSceneFreshness(string id)
        {
            if (!_sceneUsage.TryGetValue(id, out float t))
                return 1f; // Never used.

            // 0 = very recently used; 1 = long ago.
            float dt = Time.time - t;
            return Mathf.Clamp01(dt / 300f); // 5 minutes scale.
        }

        public float GetEventFreshness(string id)
        {
            if (!_eventUsage.TryGetValue(id, out float t))
                return 1f;
            float dt = Time.time - t;
            return Mathf.Clamp01(dt / 300f);
        }

        public float GetRumorFreshness(string id)
        {
            if (!_rumorUsage.TryGetValue(id, out float t))
                return 1f;
            float dt = Time.time - t;
            return Mathf.Clamp01(dt / 300f);
        }

        public float GetDialogueFreshness(string id)
        {
            if (!_dialogueUsage.TryGetValue(id, out float t))
                return 1f;
            float dt = Time.time - t;
            return Mathf.Clamp01(dt / 300f);
        }

        public void MarkSceneUsed(string id)
        {
            _sceneUsage[id] = Time.time;
        }

        public void MarkEventUsed(string id)
        {
            _eventUsage[id] = Time.time;
        }

        public void MarkRumorUsed(string id)
        {
            _rumorUsage[id] = Time.time;
        }

        public void MarkDialogueUsed(string id)
        {
            _dialogueUsage[id] = Time.time;
        }

        #endregion
    }
}
