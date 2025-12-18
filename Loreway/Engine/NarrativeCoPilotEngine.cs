using System;
using System.Collections.Generic;
using System.Linq;

namespace Loreway.Engine
{
    // High-level: which medium and structure are we shaping?
    public enum NarrativeMedium
    {
        GameQuestline,
        OpenWorldLore,
        LinearFilm,
        ShortFilm,
        Novel,
        Novella,
        ShortStory,
        ComicSeries,
        VisualAlbum,
        ConceptAlbum,
        PodcastEpisode,
        AnthologyEpisode
    }

    public enum EmotionalAxis
    {
        Hope,
        Dread,
        Grief,
        Rage,
        Wonder,
        Tenderness,
        Numbness
    }

    public enum NarrativeIntent
    {
        Comfort,
        Catharsis,
        Shock,
        Tension,
        Empowerment,
        Reflection
    }

    public enum NarrativeBeatType
    {
        Hook,
        IncitingIncident,
        RisingComplication,
        MidpointReversal,
        DarkNightOfTheSoul,
        Climax,
        Resolution,
        Epilogue,
        AmbientLore,
        SideQuest,
        CharacterMoment
    }

    [Serializable]
    public class EmotionVector
    {
        public Dictionary<EmotionalAxis, float> Intensities = new();

        public EmotionVector()
        {
            foreach (EmotionalAxis axis in Enum.GetValues(typeof(EmotionalAxis)))
                Intensities[axis] = 0.0f;
        }

        public EmotionVector Set(EmotionalAxis axis, float value)
        {
            Intensities[axis] = Math.Clamp(value, 0.0f, 1.0f);
            return this;
        }

        public EmotionalAxis DominantAxis()
        {
            return Intensities.OrderByDescending(kv => kv.Value).First().Key;
        }
    }

    [Serializable]
    public class NarrativeMediumProfile
    {
        public NarrativeMedium Medium;
        public int TargetBeats;          // Scenes / quest steps / chapters
        public bool SupportsBranches;
        public bool EmphasizeAtmosphere;
        public bool EmphasizeCharacter;
        public bool EmphasizePlot;

        public NarrativeMediumProfile(NarrativeMedium medium)
        {
            Medium = medium;
            ConfigureDefaults(medium);
        }

        private void ConfigureDefaults(NarrativeMedium medium)
        {
            switch (medium)
            {
                case NarrativeMedium.GameQuestline:
                    TargetBeats = 8;
                    SupportsBranches = true;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = true;
                    EmphasizePlot = true;
                    break;
                case NarrativeMedium.OpenWorldLore:
                    TargetBeats = 12;
                    SupportsBranches = true;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = false;
                    EmphasizePlot = false;
                    break;
                case NarrativeMedium.LinearFilm:
                    TargetBeats = 10;
                    SupportsBranches = false;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = true;
                    EmphasizePlot = true;
                    break;
                case NarrativeMedium.ShortFilm:
                case NarrativeMedium.ShortStory:
                    TargetBeats = 6;
                    SupportsBranches = false;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = true;
                    EmphasizePlot = true;
                    break;
                case NarrativeMedium.Novel:
                    TargetBeats = 16;
                    SupportsBranches = false;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = true;
                    EmphasizePlot = true;
                    break;
                case NarrativeMedium.Novella:
                    TargetBeats = 10;
                    SupportsBranches = false;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = true;
                    EmphasizePlot = true;
                    break;
                case NarrativeMedium.ConceptAlbum:
                case NarrativeMedium.VisualAlbum:
                    TargetBeats = 10;
                    SupportsBranches = false;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = false;
                    EmphasizePlot = false;
                    break;
                default:
                    TargetBeats = 8;
                    SupportsBranches = false;
                    EmphasizeAtmosphere = true;
                    EmphasizeCharacter = true;
                    EmphasizePlot = true;
                    break;
            }
        }
    }

    [Serializable]
    public class NarrativeBeat
    {
        public string BeatId;
        public NarrativeBeatType BeatType;
        public string HumanSummary;      // Human-readable guidance
        public string LlmPromptSeed;     // A prompt you can send to any AI
        public float TargetIntensity;    // 0..1 perceived intensity
        public EmotionalAxis DominantEmotion;
        public List<string> Tags = new();

        public NarrativeBeat(NarrativeBeatType type)
        {
            BeatId = Guid.NewGuid().ToString();
            BeatType = type;
        }
    }

    [Serializable]
    public class StoryOutline
    {
        public string OutlineId;
        public NarrativeMediumProfile MediumProfile;
        public NarrativeIntent Intent;
        public EmotionVector CreatorEmotion;
        public string Logline;
        public List<NarrativeBeat> Beats = new();

        public StoryOutline(NarrativeMediumProfile profile, NarrativeIntent intent, EmotionVector emotion)
        {
            OutlineId = Guid.NewGuid().ToString();
            MediumProfile = profile;
            Intent = intent;
            CreatorEmotion = emotion;
        }
    }

    public static class NarrativeCoPilotEngine
    {
        // Entry point: build an outline you can then feed to any AI or writer.
        public static StoryOutline BuildOutline(
            NarrativeMedium medium,
            NarrativeIntent intent,
            EmotionVector creatorEmotion,
            string topicOrPremise
        )
        {
            var profile = new NarrativeMediumProfile(medium);
            var outline = new StoryOutline(profile, intent, creatorEmotion);

            outline.Logline = GenerateLogline(medium, intent, creatorEmotion, topicOrPremise);
            outline.Beats = GenerateBeats(profile, intent, creatorEmotion, topicOrPremise);

            return outline;
        }

        private static string GenerateLogline(
            NarrativeMedium medium,
            NarrativeIntent intent,
            EmotionVector emotion,
            string topic
        )
        {
            var tone = emotion.DominantAxis();
            string toneWord = tone switch
            {
                EmotionalAxis.Dread      => "oppressive, slow-burning dread",
                EmotionalAxis.Grief      => "intimate grief and unspoken regrets",
                EmotionalAxis.Rage       => "boiling anger and consequences",
                EmotionalAxis.Hope       => "fragile hope in a hostile world",
                EmotionalAxis.Wonder     => "strange wonder and uncanny beauty",
                EmotionalAxis.Tenderness => "quiet connection in dark places",
                EmotionalAxis.Numbness   => "emotional numbness and disconnection",
                _                        => "complex, conflicting emotions"
            };

            string intentPhrase = intent switch
            {
                NarrativeIntent.Comfort   => "offers a small light for people who feel worn down",
                NarrativeIntent.Catharsis => "lets the audience safely spill what they cannot say out loud",
                NarrativeIntent.Shock     => "jolts the audience out of emotional autopilot",
                NarrativeIntent.Tension   => "keeps the audience on a knife-edge from start to finish",
                NarrativeIntent.Empowerment => "reminds the audience they still have choices",
                NarrativeIntent.Reflection  => "invites the audience to sit with questions, not answers",
                _                           => "speaks to a difficult truth without flinching"
            };

            string mediumLabel = medium switch
            {
                NarrativeMedium.GameQuestline => "a reactive questline",
                NarrativeMedium.OpenWorldLore => "a web of discoverable lore fragments",
                NarrativeMedium.LinearFilm    => "a contained film",
                NarrativeMedium.ShortFilm     => "a focused short film",
                NarrativeMedium.Novel         => "a long-form novel",
                NarrativeMedium.Novella       => "a compact novella",
                NarrativeMedium.ShortStory    => "a short story",
                NarrativeMedium.ComicSeries   => "a serialized comic arc",
                NarrativeMedium.VisualAlbum   => "a visual album",
                NarrativeMedium.ConceptAlbum  => "a concept album",
                NarrativeMedium.PodcastEpisode=> "a narrative podcast episode",
                NarrativeMedium.AnthologyEpisode=>"an anthology episode",
                _                             => "a story"
            };

            return $"{mediumLabel} about {topic}, built around {toneWord}, that {intentPhrase}.";
        }

        private static List<NarrativeBeat> GenerateBeats(
            NarrativeMediumProfile profile,
            NarrativeIntent intent,
            EmotionVector emotion,
            string topic
        )
        {
            var beats = new List<NarrativeBeat>();

            // Pick a basic structure template depending on medium length.
            var structure = ChooseStructure(profile.TargetBeats);

            for (int i = 0; i < structure.Count; i++)
            {
                var beatType = structure[i];
                var beat = new NarrativeBeat(beatType);

                float progress = (structure.Count == 1) ? 0f : (float)i / (structure.Count - 1);
                beat.TargetIntensity = ComputeTargetIntensity(intent, beatType, progress);
                beat.DominantEmotion = ComputeBeatEmotion(emotion, beatType, progress);
                beat.Tags = ComputeTags(profile, intent, beatType, beat.DominantEmotion);

                beat.HumanSummary = HumanSummaryForBeat(beatType, beat.DominantEmotion, topic);
                beat.LlmPromptSeed = PromptSeedForBeat(beat, topic, profile, intent);

                beats.Add(beat);
            }

            // Optionally add a few ambient/side beats for games & open worlds
            if (profile.Medium == NarrativeMedium.GameQuestline ||
                profile.Medium == NarrativeMedium.OpenWorldLore)
            {
                beats.AddRange(GenerateAmbientLoreBeats(profile, emotion, topic));
            }

            return beats;
        }

        private static List<NarrativeBeatType> ChooseStructure(int targetBeats)
        {
            // Very simple structure templates; you can expand or swap externally.
            if (targetBeats <= 6)
            {
                return new List<NarrativeBeatType>
                {
                    NarrativeBeatType.Hook,
                    NarrativeBeatType.IncitingIncident,
                    NarrativeBeatType.RisingComplication,
                    NarrativeBeatType.DarkNightOfTheSoul,
                    NarrativeBeatType.Climax,
                    NarrativeBeatType.Resolution
                };
            }

            if (targetBeats <= 10)
            {
                return new List<NarrativeBeatType>
                {
                    NarrativeBeatType.Hook,
                    NarrativeBeatType.IncitingIncident,
                    NarrativeBeatType.RisingComplication,
                    NarrativeBeatType.RisingComplication,
                    NarrativeBeatType.MidpointReversal,
                    NarrativeBeatType.RisingComplication,
                    NarrativeBeatType.DarkNightOfTheSoul,
                    NarrativeBeatType.Climax,
                    NarrativeBeatType.Resolution,
                    NarrativeBeatType.Epilogue
                };
            }

            // Longer format; more complications.
            var list = new List<NarrativeBeatType>
            {
                NarrativeBeatType.Hook,
                NarrativeBeatType.IncitingIncident
            };
            int remaining = targetBeats - 4; // we reserve Midpoint, Climax, Resolution
            for (int i = 0; i < remaining; i++)
            {
                list.Add(NarrativeBeatType.RisingComplication);
            }
            list.Add(NarrativeBeatType.MidpointReversal);
            list.Add(NarrativeBeatType.Climax);
            list.Add(NarrativeBeatType.Resolution);
            return list;
        }

        private static float ComputeTargetIntensity(
            NarrativeIntent intent,
            NarrativeBeatType type,
            float progress
        )
        {
            // Map progression & intent into an emotional/ludic "loudness" curve.
            float baseCurve = type switch
            {
                NarrativeBeatType.Hook               => 0.4f,
                NarrativeBeatType.IncitingIncident   => 0.5f,
                NarrativeBeatType.RisingComplication => 0.5f + 0.3f * progress,
                NarrativeBeatType.MidpointReversal   => 0.7f,
                NarrativeBeatType.DarkNightOfTheSoul => 0.9f,
                NarrativeBeatType.Climax             => 1.0f,
                NarrativeBeatType.Resolution         => 0.4f,
                NarrativeBeatType.Epilogue           => 0.2f,
                _                                    => 0.5f
            };

            float intentFactor = intent switch
            {
                NarrativeIntent.Shock     => 1.1f,
                NarrativeIntent.Tension   => 1.0f,
                NarrativeIntent.Catharsis => 0.95f,
                NarrativeIntent.Empowerment => 0.9f,
                NarrativeIntent.Comfort   => 0.8f,
                NarrativeIntent.Reflection=> 0.7f,
                _                         => 1.0f
            };

            return Math.Clamp(baseCurve * intentFactor, 0.1f, 1.0f);
        }

        private static EmotionalAxis ComputeBeatEmotion(
            EmotionVector baseEmotion,
            NarrativeBeatType type,
            float progress
        )
        {
            // By default, follow creator's dominant emotion, but bend near key beats.
            EmotionalAxis dominant = baseEmotion.DominantAxis();

            if (type == NarrativeBeatType.Hook)
            {
                // Hooks lean into curiosity/wonder or dread.
                if (baseEmotion.Intensities[EmotionalAxis.Dread] > 0.4f)
                    return EmotionalAxis.Dread;
                if (baseEmotion.Intensities[EmotionalAxis.Wonder] > 0.4f)
                    return EmotionalAxis.Wonder;
            }

            if (type == NarrativeBeatType.Resolution || type == NarrativeBeatType.Epilogue)
            {
                // Resolutions lean toward hope, tenderness, or numbness.
                if (baseEmotion.Intensities[EmotionalAxis.Hope] >= 0.3f)
                    return EmotionalAxis.Hope;
                if (baseEmotion.Intensities[EmotionalAxis.Tenderness] >= 0.3f)
                    return EmotionalAxis.Tenderness;
                return EmotionalAxis.Numbness;
            }

            if (type == NarrativeBeatType.DarkNightOfTheSoul)
            {
                // Dark night leans into grief or dread.
                if (baseEmotion.Intensities[EmotionalAxis.Grief] >= 0.3f)
                    return EmotionalAxis.Grief;
                return EmotionalAxis.Dread;
            }

            return dominant;
        }

        private static List<string> ComputeTags(
            NarrativeMediumProfile profile,
            NarrativeIntent intent,
            NarrativeBeatType type,
            EmotionalAxis emotion
        )
        {
            var tags = new List<string>
            {
                profile.Medium.ToString(),
                intent.ToString(),
                type.ToString(),
                emotion.ToString()
            };

            if (profile.EmphasizeAtmosphere) tags.Add("Atmosphere");
            if (profile.EmphasizeCharacter)  tags.Add("Character");
            if (profile.EmphasizePlot)       tags.Add("Plot");

            if (profile.Medium == NarrativeMedium.GameQuestline &&
                (type == NarrativeBeatType.SideQuest || type == NarrativeBeatType.AmbientLore))
            {
                tags.Add("Optional");
            }

            return tags;
        }

        private static string HumanSummaryForBeat(
            NarrativeBeatType type,
            EmotionalAxis emotion,
            string topic
        )
        {
            // This is written to be legible as-is to a tired creator.
            string emotionalColor = emotion switch
            {
                EmotionalAxis.Dread      => "unease and quiet threat",
                EmotionalAxis.Grief      => "loss, memory, or something half-healed",
                EmotionalAxis.Rage       => "anger and consequence",
                EmotionalAxis.Hope       => "a fragile reason to keep going",
                EmotionalAxis.Wonder     => "something strange but magnetic",
                EmotionalAxis.Tenderness => "a small human connection",
                EmotionalAxis.Numbness   => "emotional distance or burnout",
                _                        => "conflicting feelings"
            };

            return type switch
            {
                NarrativeBeatType.Hook =>
                    $"Open on {topic} with an image or moment that hints at {emotionalColor}. No exposition dump, just a question the audience must lean into.",
                NarrativeBeatType.IncitingIncident =>
                    $"Something happens that forces a choice around {topic}. Make the cost of ignoring it clear, even if the character tries.",
                NarrativeBeatType.RisingComplication =>
                    $"Complicate {topic} through setbacks or discoveries. Each step should deepen {emotionalColor}, not just escalate action.",
                NarrativeBeatType.MidpointReversal =>
                    $"Flip what the audience thought they understood about {topic}. The story is now about a harder, truer version of the same problem.",
                NarrativeBeatType.DarkNightOfTheSoul =>
                    $"Let the character feel the full weight of {topic}. Pull back on spectacle; push into interiority and {emotionalColor}.",
                NarrativeBeatType.Climax =>
                    $"Force a choice that reveals what the character has become because of {topic}. Pay off earlier promises and symbols.",
                NarrativeBeatType.Resolution =>
                    $"Show the quiet wake of what happened around {topic}. Focus on one grounded detail that makes the change feel real.",
                NarrativeBeatType.Epilogue =>
                    $"Offer a last glimpse of how {topic} will echo forward. It can comfort, disturb, or simply linger.",
                NarrativeBeatType.AmbientLore =>
                    $"Create a small fragment (note, mural, rumor) that reframes {topic} from a side angle. It should reward curious players.",
                NarrativeBeatType.SideQuest =>
                    $"Spin a smaller situation that mirrors or contrasts {topic}. Low stakes in plot, high stakes in theme.",
                NarrativeBeatType.CharacterMoment =>
                    $"Pause the plot and let a character react to {topic} in a specific, human way—gesture, habit, or confession.",
                _ =>
                    $"Advance {topic} one step while staying honest to {emotionalColor}."
            };
        }

        private static string PromptSeedForBeat(
            NarrativeBeat beat,
            string topic,
            NarrativeMediumProfile profile,
            NarrativeIntent intent
        )
        {
            // This is the “bridge” between Loreway and any LLM: you send this plus your own style notes.
            string mediumHint = profile.Medium switch
            {
                NarrativeMedium.GameQuestline =>
                    "Write this as a quest step in a narrative-driven game. Include clear objectives, obstacles, and optional player choices.",
                NarrativeMedium.OpenWorldLore =>
                    "Write this as an in-world lore fragment (journal page, overheard dialogue, shrine inscription) discoverable in any order.",
                NarrativeMedium.LinearFilm =>
                    "Write this as a film scene: visual, sound, blocking. Minimal inner monologue; focus on what the camera sees and hears.",
                NarrativeMedium.ShortFilm =>
                    "Write this as a tight short-film scene that can stand almost on its own.",
                NarrativeMedium.Novel =>
                    "Write this as a novel chapter scene with access to inner thoughts and sensory detail.",
                NarrativeMedium.Novella =>
                    "Write this as a focused prose scene: economical but emotionally rich.",
                NarrativeMedium.ShortStory =>
                    "Write this as a short story scene with strong imagery and a clear turn.",
                NarrativeMedium.ComicSeries =>
                    "Write this as a comic script page: panel breakdowns, captions, and dialogue.",
                NarrativeMedium.VisualAlbum =>
                    "Write this as a visual album moment: describe imagery, performance, and mood to align with a song section.",
                NarrativeMedium.ConceptAlbum =>
                    "Write this as liner-note style narrative or spoken-word interlude for a concept album.",
                NarrativeMedium.PodcastEpisode =>
                    "Write this as a scripted audio scene: voices, sound cues, no visuals.",
                NarrativeMedium.AnthologyEpisode =>
                    "Write this as a self-contained yet thematically linked scene in an anthology.",
                _ =>
                    "Write this scene with clear sensory detail and emotional clarity."
            };

            string intentHint = intent switch
            {
                NarrativeIntent.Comfort =>
                    "Keep a small thread of kindness or survivable warmth, even when things are bleak.",
                NarrativeIntent.Catharsis =>
                    "Let difficult feelings actually move; do not undercut them with jokes too early.",
                NarrativeIntent.Shock =>
                    "Use shock sparingly; aim for emotional impact, not just surprise.",
                NarrativeIntent.Tension =>
                    "Maintain a sense of unresolved threat. Answer one question, raise another.",
                NarrativeIntent.Empowerment =>
                    "Show that choices matter, even if the world is harsh.",
                NarrativeIntent.Reflection =>
                    "Allow space for silence and thought; it's okay if not everything is resolved.",
                _ =>
                    "Stay honest to the emotional reality of the characters."
            };

            return
                $"You are assisting a creator who feels primarily {beat.DominantEmotion} and is working on: {topic}. " +
                $"Generate the {beat.BeatType} of the story. " +
                $"{mediumHint} " +
                $"{intentHint} " +
                $"Aim for an intensity around {Math.Round(beat.TargetIntensity, 2)} on a 0-1 scale. " +
                $"Use the following human summary as guidance, not a script: \"{beat.HumanSummary}\".";
        }

        private static IEnumerable<NarrativeBeat> GenerateAmbientLoreBeats(
            NarrativeMediumProfile profile,
            EmotionVector emotion,
            string topic
        )
        {
            var beats = new List<NarrativeBeat>();

            // One AmbientLore, one SideQuest, one CharacterMoment
            var ambient = new NarrativeBeat(NarrativeBeatType.AmbientLore)
            {
                DominantEmotion = emotion.DominantAxis(),
                TargetIntensity = 0.4f
            };
            ambient.Tags = ComputeTags(profile, NarrativeIntent.Reflection, ambient.BeatType, ambient.DominantEmotion);
            ambient.HumanSummary = HumanSummaryForBeat(ambient.BeatType, ambient.DominantEmotion, topic);
            ambient.LlmPromptSeed = PromptSeedForBeat(ambient, topic, profile, NarrativeIntent.Reflection);
            beats.Add(ambient);

            var sideQuest = new NarrativeBeat(NarrativeBeatType.SideQuest)
            {
                DominantEmotion = emotion.DominantAxis(),
                TargetIntensity = 0.5f
            };
            sideQuest.Tags = ComputeTags(profile, NarrativeIntent.Empowerment, sideQuest.BeatType, sideQuest.DominantEmotion);
            sideQuest.HumanSummary = HumanSummaryForBeat(sideQuest.BeatType, sideQuest.DominantEmotion, topic);
            sideQuest.LlmPromptSeed = PromptSeedForBeat(sideQuest, topic, profile, NarrativeIntent.Empowerment);
            beats.Add(sideQuest);

            var characterMoment = new NarrativeBeat(NarrativeBeatType.CharacterMoment)
            {
                DominantEmotion = emotion.DominantAxis(),
                TargetIntensity = 0.6f
            };
            characterMoment.Tags = ComputeTags(profile, NarrativeIntent.Reflection, characterMoment.BeatType, characterMoment.DominantEmotion);
            characterMoment.HumanSummary = HumanSummaryForBeat(characterMoment.BeatType, characterMoment.DominantEmotion, topic);
            characterMoment.LlmPromptSeed = PromptSeedForBeat(characterMoment, topic, profile, NarrativeIntent.Reflection);
            beats.Add(characterMoment);

            return beats;
        }
    }
}
