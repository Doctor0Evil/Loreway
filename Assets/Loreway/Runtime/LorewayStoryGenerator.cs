// File: Assets/Loreway/Runtime/LorewayStoryGenerator.cs
// Purpose: Runtime generator for short horror scenes & dialogue
// aligned with Cell / Loreway KG concepts.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cell.Loreway
{
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
        public string Id;
        public string DisplayName;
        public string StyleProfile;
        public bool IsPlayer;
    }

    [Serializable]
    public class SceneContext
    {
        public string RegionId;
        public string PlaceId;
        public string TimeOfDay;   // e.g. "permanent-night-cycle"
        public bool LowOxygen;
        public bool NightCycle;
        public HorrorMode[] ActiveModes;
        public string[] ActiveTaboos;   // e.g. ["TABSVENTSILENCE01"]
    }

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
        public string[] LandscapeMotifs;
        public string[] SpiritMotifs;
        public string[] HumanMiseryMotifs;

        [Header("Taboo & Spirit Hooks")]
        public string[] TabooIds;
        public string[] SpiritIds;

        [Header("Beat Skeletons")]
        [TextArea(2, 6)]
        public string[] BeatPrompts;

        [Header("Dialogue Style Hints")]
        [Range(1, 20)] public int MaxLinesPerUnit = 6;
        [Range(4, 22)] public int MaxWordsPerLine = 18;
        public bool RequireContradictionLine = true;
        public bool RequireImpliedRuleLine = true;
    }

    [Serializable]
    public class GeneratedBeat
    {
        public string Id;
        public HorrorFunction Function;
        [TextArea(2, 6)] public string Description;
        public string[] SystemHooks;
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
        public string[] Triggers;
        public string StyleProfile;
    }

    [Serializable]
    public class GeneratedScenePacket
    {
        public string SceneId;
        public SceneContext Context;
        public List<GeneratedBeat> Beats = new List<GeneratedBeat>();
        public GeneratedDialogueUnit Dialogue;
    }

    public class LorewayStoryGenerator : ScriptableObject
    {
        [Header("Input Libraries")]
        public List<LorewayNarrativeTemplate> Templates;
        public List<SpeakerProfile> SpeakerProfiles;

        [Header("Randomization")]
        [Range(0f, 1f)] public float LieProbability = 0.35f;
        [Range(0f, 1f)] public float MoralAnxietyBias = 0.5f;

        private System.Random _rng;

        private void OnEnable()
        {
            _rng = new System.Random(Environment.TickCount);
        }

        public GeneratedScenePacket GenerateScene(SceneContext context, string seed = null)
        {
            EnsureRng(seed);

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

            int beatCount = Mathf.Min(template.MaxBeats, template.BeatPrompts.Length);
            for (int i = 0; i < beatCount; i++)
            {
                var func = PickHorrorFunction(template);

                var beat = new GeneratedBeat
                {
                    Id = $"{packet.SceneId}-BEAT-{i + 1}",
                    Function = func,
                    Description = SynthesizeBeatDescription(
                        template,
                        template.BeatPrompts[i],
                        func,
                        context),
                    SystemHooks = SynthesizeSystemHooks(func, context)
                };

                packet.Beats.Add(beat);
            }

            packet.Dialogue = GenerateDialogueUnit(context, template, packet.SceneId);
            return packet;
        }

        void EnsureRng(string seed)
        {
            if (_rng == null)
                _rng = new System.Random(Environment.TickCount);

            if (!string.IsNullOrEmpty(seed))
                _rng = new System.Random(seed.GetHashCode());
        }

        LorewayNarrativeTemplate PickTemplate(SceneContext context)
        {
            var candidates = Templates.Where(t =>
                    t.SupportedModes.Intersect(context.ActiveModes ?? Array.Empty<HorrorMode>()).Any())
                .ToList();

            if (candidates.Count == 0)
                candidates = Templates;

            int idx = _rng.Next(candidates.Count);
            return candidates[idx];
        }

        HorrorFunction PickHorrorFunction(LorewayNarrativeTemplate template)
        {
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
            var guid = Guid.NewGuid().ToString("N").Substring(0, 6);
            return $"SCN-{ctx.PlaceId}-{templateId}-{guid}";
        }

        string SynthesizeBeatDescription(
            LorewayNarrativeTemplate template,
            string prompt,
            HorrorFunction function,
            SceneContext ctx)
        {
            var landscape = PickRandom(template.LandscapeMotifs);
            var spirit = PickRandom(template.SpiritMotifs);
            var misery = PickRandom(template.HumanMiseryMotifs);
            var functionTag = function.ToString().ToLowerInvariant();

            string tabooHint = string.Empty;
            if (ctx.ActiveTaboos != null && ctx.ActiveTaboos.Length > 0)
            {
                tabooHint = $" Something in the metal remembers \"{ctx.ActiveTaboos[0]}\".";
            }

            return $"{prompt} [{functionTag}] " +
                   $"You catch a smear of {spirit} above the {landscape}, " +
                   $"while {misery} presses at the edge of your breath." +
                   tabooHint;
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

            if (ctx.ActiveTaboos != null &&
                ctx.ActiveTaboos.Contains("TABSVENTSILENCE01"))
            {
                hooks.Add("reroute_growlers_toward_vent_noise");
            }

            return hooks.ToArray();
        }

        string PickRandom(string[] array)
        {
            if (array == null || array.Length == 0) return string.Empty;
            return array[_rng.Next(array.Length)];
        }

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
                bool lastLine = (i == lineLimit - 1);
                var intent = PickLineIntent(usedContradiction, usedRule, lastLine, template);
                var reliability = PickReliability(intent);

                var text = SynthesizeLineText(context, template, speaker, intent, reliability);

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

        LineIntent PickLineIntent(
            bool usedContradiction,
            bool usedRule,
            bool lastLine,
            LorewayNarrativeTemplate template)
        {
            if (lastLine && template.RequireContradictionLine && !usedContradiction)
                return LineIntent.ContradictEvidence;

            if (lastLine && template.RequireImpliedRuleLine && !usedRule)
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
            bool ventTaboo = ctx.ActiveTaboos != null &&
                             ctx.ActiveTaboos.Contains("TABSVENTSILENCE01");

            switch (intent)
            {
                case LineIntent.EvokeThreat:
                    return ventTaboo
                        ? "They hear through the vents now; the ship teaches them our paths."
                        : "Out here, noise travels farther than light and cuts deeper.";
                case LineIntent.ImplyRule:
                    return ventTaboo
                        ? "If you want to live, don’t let the ducts learn your voice."
                        : "Don’t name what you see after midnight; it remembers.";
                case LineIntent.RevealFlaw:
                    return "I broke the rule once, just to feel less alone.";
                case LineIntent.ContradictEvidence:
                    if (reliability == Reliability.KnownFalse)
                        return "No one’s died on this deck since the blue moon came up.";
                    return "They say the howls stopped after the last purge.";
                case LineIntent.DeflectTruth:
                default:
                    return "It’s just metal settling. That’s what we tell the rookies.";
            }
        }
    }
}
