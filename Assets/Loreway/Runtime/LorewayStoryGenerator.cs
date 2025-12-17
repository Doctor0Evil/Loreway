// File: Assets/Loreway/Runtime/LorewayStoryGenerator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cell.Loreway
{
    // ---------- CORE ENUMS & DATA VECTORS ----------

    public enum HorrorMode
    {
        Cosmic,
        Body,
        Bureaucratic,
        Survival
    }

    public enum HorrorFunction
    {
        Dread,
        Uncanny,
        Shock,
        MoralAnxiety
    }

    public enum LineIntent
    {
        ImplyRule,
        ContradictEvidence,
        RevealFlaw,
        EvokeThreat,
        DeflectTruth
    }

    public enum Reliability
    {
        Reliable,
        Ambiguous,
        KnownFalse
    }

    [Serializable]
    public class SpeakerProfile
    {
        public string Id;              // e.g. "CHRLOGKEEPER01"
        public string DisplayName;     // in‑game name
        public string StyleProfile;    // "shellshocked-matter-of-fact", "bureaucratic-cold"
        public bool IsPlayer;
    }

    [Serializable]
    public class SceneContext
    {
        public string RegionId;        // "CELLREGIONPOL-01"
        public string PlaceId;         // "PLCORBITALHADES01"
        public string TimeOfDay;       // "permanent-night-cycle"
        public bool LowOxygen;
        public bool NightCycle;
        public HorrorMode[] ActiveModes;
    }

    // ---------- TEMPLATES FOR AI‑ASSISTED GENERATION ----------

    [CreateAssetMenu(
        fileName = "LorewayNarrativeTemplate",
        menuName = "Cell/Loreway/Narrative Template")]
    public class LorewayNarrativeTemplate : ScriptableObject
    {
        [Header("General")]
        public string TemplateId = "LWT-GENERIC-01";
        public int MaxBeats = 4;

        [Header("Horror Vectors")]
        public HorrorMode[] SupportedModes;
        public HorrorFunction[] SupportedFunctions;

        [Header("Motif Tokens (Slavic / Space)")]
        [Tooltip("Imagery fragments for the generator to weave into descriptions.")]
        public string[] LandscapeMotifs;     // e.g. "vent labyrinth", "frosted bulkheads"
        public string[] SpiritMotifs;        // e.g. "blue corona", "breathing ship"
        public string[] HumanMiseryMotifs;   // e.g. "oxygen debt", "ration riots"

        [Header("Taboo & Ritual Hooks")]
        public string[] TabooIds;            // e.g. "TABSVENTSILENCE01"
        public string[] SpiritIds;           // e.g. "SPRTAZUREHOWLER01";

        [Header("Beat Skeletons")]
        [TextArea(2, 6)]
        public string[] BeatPrompts;         // natural-language seeds for AI

        [Header("Dialogue Style Hints")]
        [Range(1, 20)] public int MaxLinesPerUnit = 6;
        [Range(4, 22)] public int MaxWordsPerLine = 18;
        public bool RequireContradictionLine = true;   // at least one lie per unit
        public bool RequireImpliedRuleLine = true;     // at least one implied taboo/rule
    }

    [Serializable]
    public class GeneratedBeat
    {
        public string Id;
        public HorrorFunction Function;
        [TextArea(2, 6)] public string Description;
        public string[] SystemHooks;   // e.g. "spawn-ambush-pack", "lower-ambient-music"
    }

    [Serializable]
    public class GeneratedLine
    {
        public string Id;
        public string SpeakerId;
        [TextArea(1, 4)] public string Text;
        public LineIntent Intent;
        public Reliability Reliability;
    }

    [Serializable]
    public class GeneratedDialogueUnit
    {
        public string Id;
        public List<GeneratedLine> Lines = new List<GeneratedLine>();
        public string[] Triggers;      // e.g. "on_first_enter_scene"
        public string StyleProfile;    // copy from template or speaker profile
    }

    [Serializable]
    public class GeneratedScenePacket
    {
        public string SceneId;
        public SceneContext Context;
        public List<GeneratedBeat> Beats = new List<GeneratedBeat>();
        public GeneratedDialogueUnit Dialogue;
    }

    // ---------- RUNTIME GENERATOR ----------

    public class LorewayStoryGenerator : ScriptableObject
    {
        [Header("Input Libraries")]
        public List<LorewayNarrativeTemplate> Templates;
        public List<SpeakerProfile> SpeakerProfiles;

        [Header("Randomization")]
        [Range(0f, 1f)] public float LieProbability = 0.35f;
        [Range(0f, 1f)] public float MoralAnxietyBias = 0.5f;

        System.Random _rng;

        void OnEnable()
        {
            _rng = new System.Random(Environment.TickCount);
        }

        public GeneratedScenePacket GenerateScene(SceneContext context, string seed = null)
        {
            if (_rng == null) _rng = new System.Random(Environment.TickCount);
            if (!string.IsNullOrEmpty(seed))
                _rng = new System.Random(seed.GetHashCode());

            var template = PickTemplate(context);
            if (template == null)
            {
                Debug.LogWarning("Loreway: No matching template found, cannot generate scene.");
                return null;
            }

            var packet = new GeneratedScenePacket
            {
                SceneId = BuildSceneId(context, template.TemplateId),
                Context = context
            };

            // Generate beats
            int beatCount = Math.Min(template.MaxBeats, template.BeatPrompts.Length);
            for (int i = 0; i < beatCount; i++)
            {
                var func = PickHorrorFunction(template);
                packet.Beats.Add(new GeneratedBeat
                {
                    Id = $"{packet.SceneId}-BEAT-{i + 1}",
                    Function = func,
                    Description = SynthesizeBeatDescription(template, template.BeatPrompts[i], func),
                    SystemHooks = SynthesizeSystemHooks(func, context)
                });
            }

            // Generate dialogue unit bound to this scene
            packet.Dialogue = GenerateDialogueUnit(context, template, packet.SceneId);

            return packet;
        }

        // ---------- TEMPLATE SELECTION & HELPERS ----------

        LorewayNarrativeTemplate PickTemplate(SceneContext context)
        {
            var candidates = Templates.Where(t =>
                t.SupportedModes.Intersect(context.ActiveModes).Any()).ToList();

            if (candidates.Count == 0)
                candidates = Templates; // fallback

            int idx = _rng.Next(candidates.Count);
            return candidates[idx];
        }

        HorrorFunction PickHorrorFunction(LorewayNarrativeTemplate template)
        {
            // Bias moral anxiety when requested
            if (_rng.NextDouble() < MoralAnxietyBias &&
                template.SupportedFunctions.Contains(HorrorFunction.MoralAnxiety))
            {
                return HorrorFunction.MoralAnxiety;
            }

            int idx = _rng.Next(template.SupportedFunctions.Length);
            return template.SupportedFunctions[idx];
        }

        string BuildSceneId(SceneContext ctx, string templateId)
        {
            return $"SCN-{ctx.PlaceId}-{templateId}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        string SynthesizeBeatDescription(
            LorewayNarrativeTemplate template,
            string prompt,
            HorrorFunction function)
        {
            // This is where your AI model or prompt system plugs in.
            // The stub below gives a fallback for offline/editor preview.

            string landscape = PickRandom(template.LandscapeMotifs);
            string spirit = PickRandom(template.SpiritMotifs);
            string misery = PickRandom(template.HumanMiseryMotifs);

            string functionTag = function.ToString().ToLowerInvariant();

            return $"{prompt} [{functionTag}] " +
                   $"You catch a hint of {spirit} above the {landscape}, " +
                   $"while {misery} presses in from the edges.";
        }

        string[] SynthesizeSystemHooks(HorrorFunction function, SceneContext ctx)
        {
            var hooks = new List<string>();

            switch (function)
            {
                case HorrorFunction.Dread:
                    hooks.Add("lower-ambient-music");
                    hooks.Add("boost-vent-sfx");
                    break;
                case HorrorFunction.Uncanny:
                    hooks.Add("disable-minimap-pings");
                    hooks.Add("offset-lighting-randomly");
                    break;
                case HorrorFunction.Shock:
                    hooks.Add("spawn-ambush-pack");
                    break;
                case HorrorFunction.MoralAnxiety:
                    hooks.Add("flag-moral-choice-upcoming");
                    hooks.Add("log-to-sanity-system");
                    break;
            }

            if (ctx.LowOxygen)
                hooks.Add("tighten-oxygen-threshold");

            return hooks.ToArray();
        }

        string PickRandom(string[] array)
        {
            if (array == null || array.Length == 0) return string.Empty;
            return array[_rng.Next(array.Length)];
        }

        // ---------- DIALOGUE GENERATION ----------

        GeneratedDialogueUnit GenerateDialogueUnit(
            SceneContext context,
            LorewayNarrativeTemplate template,
            string sceneId)
        {
            var unit = new GeneratedDialogueUnit
            {
                Id = $"DLG-{sceneId}",
                StyleProfile = "shellshocked-matter-of-fact",
                Triggers = new[] { "on_first_enter_scene" }
            };

            var speaker = SpeakerProfiles.FirstOrDefault(s => !s.IsPlayer)
                          ?? new SpeakerProfile { Id = "CHRLOGKEEPER01", DisplayName = "Logkeeper" };

            int lineLimit = template.MaxLinesPerUnit;
            bool usedContradiction = false;
            bool usedRule = false;

            for (int i = 0; i < lineLimit; i++)
            {
                LineIntent intent = PickLineIntent(usedContradiction, usedRule, i == lineLimit - 1);
                Reliability reliability = PickReliability(intent);

                string text = SynthesizeLineText(context, template, speaker, intent, reliability);

                unit.Lines.Add(new GeneratedLine
                {
                    Id = $"{unit.Id}-L{i + 1}",
                    SpeakerId = speaker.Id,
                    Intent = intent,
                    Reliability = reliability,
                    Text = text
                });

                if (intent == LineIntent.ContradictEvidence) usedContradiction = true;
                if (intent == LineIntent.ImplyRule) usedRule = true;
            }

            return unit;
        }

        LineIntent PickLineIntent(bool usedContradiction, bool usedRule, bool lastLine)
        {
            // Ensure at least one contradiction and one implied rule if requested
            if (lastLine && !usedContradiction)
                return LineIntent.ContradictEvidence;
            if (lastLine && !usedRule)
                return LineIntent.ImplyRule;

            int roll = _rng.Next(100);
            if (roll < 30) return LineIntent.EvokeThreat;
            if (roll < 55) return LineIntent.ImplyRule;
            if (roll < 75) return LineIntent.RevealFlaw;
            if (roll < 90) return LineIntent.DeflectTruth;
            return LineIntent.ContradictEvidence;
        }

        Reliability PickReliability(LineIntent intent)
        {
            switch (intent)
            {
                case LineIntent.ContradictEvidence:
                    return _rng.NextDouble() < LieProbability
                        ? Reliability.KnownFalse
                        : Reliability.Ambiguous;
                case LineIntent.DeflectTruth:
                    return Reliability.Ambiguous;
                default:
                    return Reliability.Reliable;
            }
        }

        string SynthesizeLineText(
            SceneContext ctx,
            LorewayNarrativeTemplate template,
            SpeakerProfile speaker,
            LineIntent intent,
            Reliability reliability)
        {
            // Stub: structured phrase builder that your AI can replace or extend.
            // Keeps lines short and concrete for horror VO.

            string place = ctx.PlaceId.Contains("HADES")
                ? "these decks"
                : "this place";

            switch (intent)
            {
                case LineIntent.EvokeThreat:
                    return "They learned the vents faster than we learned the map.";
                case LineIntent.ImplyRule:
                    return "If you value your lungs, don’t teach the ducts your voice.";
                case LineIntent.RevealFlaw:
                    return "I shouted once, just to hear something human answer.";
                case LineIntent.ContradictEvidence:
                    return reliability == Reliability.KnownFalse
                        ? "No one’s died up here since the moon turned blue."
                        : "They say the howls stopped after the last purge.";
                case LineIntent.DeflectTruth:
                default:
                    return "It’s just metal settling. That’s what we tell the rookies.";
            }
        }
    }
}
