using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// LorewayDialogueGenerator
/// High-density, procedural dialogue-asset generator for Cell.
/// - Slavic horror focused: dread, surrealism, adult themes (content gating via flags).
/// - Integrates with a Loreway-like KG: Spirits, Places, Families, Events, Taboos, Rumors, Backstories.[file:1]
/// - Outputs DialogueUnits, barks, micro-monologues, and ritual scripts ready for in-game AI.
/// - All names/IDs assumed to be Cell-original; no external IP.
/// </summary>
namespace Cell.Loreway.Dialogue
{
    #region Core Enums

    [Flags]
    public enum HorrorFunction
    {
        None            = 0,
        Dread           = 1 << 0,
        Uncanny         = 1 << 1,
        Shock           = 1 << 2,
        Disgust         = 1 << 3,
        MoralAnxiety    = 1 << 4
    }

    public enum LoreDepth
    {
        SurfaceHint,
        PartialExplanation,
        DeepContext
    }

    public enum PlayerAgencyLevel
    {
        Passive,
        LightInteraction,
        CriticalChoice
    }

    public enum LineReliability
    {
        Reliable,
        Ambiguous,
        KnownFalse
    }

    public enum DialogueStyleProfile
    {
        RuralSparse,
        BureaucraticCold,
        Delirious,
        PriestMonotone,
        SoldierBurntOut
    }

    public enum SpeakerRole
    {
        Player,
        Villager,
        Official,
        Priest,
        Smuggler,
        Soldier,
        SpiritWhisper,
        Archivist
    }

    public enum ToneBand
    {
        SlavicHorror,
        OffTone
    }

    #endregion

    #region KG-facing DTOs (minimal, engine-agnostic)

    /// <summary>
    /// Minimal KG references used by the dialogue generator.
    /// These can be mapped to your full Loreway KG schema (Spirit, Place, Family, Event, Taboo, Rumor, BackstoryPacket).[file:1]
    /// </summary>
    public sealed class SpiritRef
    {
        public string Id;
        public string DisplayName;       // Cell-original epithet.
        public string OriginMotif;
        public string[] TemperamentTags; // e.g. "indifferent","vindictive"
        public string[] Domains;         // e.g. "forest","marsh"
    }

    public sealed class PlaceRef
    {
        public string Id;
        public string DisplayName;
        public string[] EnvironmentTags; // e.g. "wetforestedge","postsovietdecay"[file:1]
    }

    public sealed class TabooRef
    {
        public string Id;
        public string DisplayName;
        public string InWorldDescription; // In-diagesis phrasing of the rule.
    }

    public sealed class RumorRef
    {
        public string Id;
        public string Topic;              // "disappearance","treachery","miracle","corruption"[file:1]
        public string[] Variants;         // 1–2 sentence facts.
        public string TruthStatus;        // "false","partial","true","unknown"
    }

    public sealed class BackstoryPacketRef
    {
        public string Id;
        public string Summary;            // short distilled backstory.
        public string[] DrivingGoals;     // e.g. "protectfamily","payoffdebt"[file:1]
        public string[] Fears;            // "forest","authority"
        public string[] Secrets;          // "falsified death record", etc.[file:1]
    }

    public sealed class CharacterRef
    {
        public string Id;
        public string DisplayName;
        public SpeakerRole Role;
        public string[] PsychologicalTags;    // "secretive","resentful","tired"
        public string[] DialectNotes;         // "rural","clipped","bureaucratic"[file:1]
        public BackstoryPacketRef Backstory;
    }

    #endregion

    #region Dialogue Data Structures

    [Serializable]
    public sealed class DialogueLine
    {
        public string LineId;
        public string SpeakerId;
        public string SpeakerDisplayName;
        public SpeakerRole SpeakerRole;
        public string Text;
        public string Intent;                 // "implyrule","contradictevidence","revealflaw" etc.[file:1]
        public LineReliability Reliability;
        public string[] SystemTags;           // "threatnear","afterplayerbreakstaboo" etc.[file:1]

        public override string ToString()
        {
            return $"{SpeakerDisplayName}: {Text}";
        }
    }

    [Serializable]
    public sealed class DialogueUnit
    {
        public string Id;
        public string SceneId;
        public DialogueStyleProfile StyleProfile;
        public HorrorFunction HorrorFunction;
        public LoreDepth LoreDepth;
        public PlayerAgencyLevel PlayerAgency;
        public List<DialogueLine> Lines = new List<DialogueLine>();

        // Function metadata: enforce loreway-style constraints.[file:1]
        public bool HasContradiction;
        public bool HintsUnseenRule;
        public bool ReferencesTaboo;
        public bool ReferencesRumor;
        public bool ReferencesBackstory;
    }

    [Serializable]
    public sealed class DialogueGenerationConfig
    {
        // Hard limits to keep lines dense and VO-ready.[file:1]
        public int MaxLinesTotal = 14;
        public int MaxWordsPerLine = 14;

        // Probability weights.
        public float ProbabilityOfKnownFalseLine = 0.2f;
        public float ProbabilityOfAmbiguousLine  = 0.4f;

        public float ProbabilityUseRumor         = 0.6f;
        public float ProbabilityUseBackstory     = 0.5f;
        public float ProbabilityMentionTaboo     = 0.75f;

        public HorrorFunction TargetHorror = HorrorFunction.Dread | HorrorFunction.Uncanny;
        public LoreDepth TargetLoreDepth   = LoreDepth.PartialExplanation;

        // Adult / graphic content toggles (you can gate by rating).
        public bool AllowGraphicDescription = true;
        public bool AllowMoralHorror        = true;

        // Tone enforcement.
        public ToneBand RequiredToneBand    = ToneBand.SlavicHorror;
    }

    #endregion

    #region Utility: Random + Helpers

    internal sealed class FastRandom
    {
        private readonly Random _rng;
        public FastRandom(int seed) { _rng = new Random(seed); }
        public FastRandom() : this(Environment.TickCount) { }

        public int Range(int minInclusive, int maxExclusive)
        {
            return _rng.Next(minInclusive, maxExclusive);
        }

        public float Value()
        {
            return (float)_rng.NextDouble();
        }

        public T Pick<T>(IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Range(0, list.Count)];
        }

        public bool Chance(float p)
        {
            if (p <= 0f) return false;
            if (p >= 1f) return true;
            return Value() <= p;
        }
    }

    internal static class StringUtil
    {
        public static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            int count = 0;
            bool inWord = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (inWord) { inWord = false; }
                }
                else
                {
                    if (!inWord)
                    {
                        inWord = true;
                        count++;
                    }
                }
            }
            return count;
        }

        public static string TruncateWords(string text, int maxWords)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (maxWords <= 0) return string.Empty;
            var sb = new StringBuilder();
            int words = 0;
            bool inWord = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (inWord)
                    {
                        inWord = false;
                        words++;
                        if (words >= maxWords) break;
                    }
                    sb.Append(c);
                }
                else
                {
                    inWord = true;
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }
    }

    #endregion

    #region Core Generator

    public sealed class LorewayDialogueGenerator
    {
        private readonly FastRandom _rng;
        private readonly DialogueGenerationConfig _config;

        // Style lexicons – tuned for Slavic horror. These are intentionally generic, IP-safe phrases.
        private readonly string[] _ruralFillers = new[]
        {
            "You know how it is.",
            "You hear it if you listen.",
            "No one writes that down.",
            "Old folks still remember."
        };

        private readonly string[] _bureaucraticFillers = new[]
        {
            "Not for public record.",
            "Form says otherwise.",
            "You did not hear this from me.",
            "Officially, nothing happened."
        };

        private readonly string[] _deliriousFillers = new[]
        {
            "Teeth in the walls, teeth in the snow.",
            "The night walks on all fours.",
            "They counted us wrong on purpose.",
            "The floor remembers every fall."
        };

        public LorewayDialogueGenerator(DialogueGenerationConfig config = null, int? seed = null)
        {
            _config = config ?? new DialogueGenerationConfig();
            _rng = seed.HasValue ? new FastRandom(seed.Value) : new FastRandom();
        }

        #region Public API

        /// <summary>
        /// High-level entry point: generate a DialogueUnit for a given "scene" context.
        /// </summary>
        public DialogueUnit GenerateDialogueUnit(
            string sceneId,
            DialogueStyleProfile styleProfile,
            CharacterRef primaryNpc,
            CharacterRef player,
            PlaceRef place,
            IReadOnlyList<TabooRef> activeTaboos,
            IReadOnlyList<RumorRef> localRumors,
            SpiritRef dominantSpirit = null)
        {
            var unit = new DialogueUnit
            {
                Id = $"DLG_{sceneId}_{primaryNpc.Id}_{Environment.TickCount}",
                SceneId = sceneId,
                StyleProfile = styleProfile,
                HorrorFunction = _config.TargetHorror,
                LoreDepth = _config.TargetLoreDepth,
                PlayerAgency = PlayerAgencyLevel.LightInteraction
            };

            // Structural: at least 3 lines, at most MaxLinesTotal.
            int targetLines = _rng.Range(6, _config.MaxLinesTotal + 1);

            bool tabooMentioned = false;
            bool rumorUsed = false;
            bool backstoryUsed = false;
            bool contradictionPlaced = false;
            bool unseenRulePlaced = false;

            // Line 0: NPC establishes rule / mood.
            var line0 = BuildRuleOrAtmosphereLine(primaryNpc, place, activeTaboos, dominantSpirit, ref unseenRulePlaced);
            unit.Lines.Add(line0);
            tabooMentioned |= unit.ReferencesTaboo;
            unseenRulePlaced |= unit.HintsUnseenRule;

            // Line 1: Player minimal pushback.
            var line1 = BuildPlayerPushbackLine(player, primaryNpc, place);
            unit.Lines.Add(line1);

            // Mid lines: rumors, contradictions, backstory leaks.
            for (int i = 2; i < targetLines - 1; i++)
            {
                DialogueLine next;
                if (!rumorUsed && _rng.Chance(_config.ProbabilityUseRumor) && localRumors != null && localRumors.Count > 0)
                {
                    next = BuildRumorLine(primaryNpc, localRumors, ref contradictionPlaced);
                    rumorUsed = true;
                    unit.ReferencesRumor = true;
                }
                else if (!backstoryUsed && _rng.Chance(_config.ProbabilityUseBackstory) && primaryNpc.Backstory != null)
                {
                    next = BuildBackstoryLeakLine(primaryNpc);
                    backstoryUsed = true;
                    unit.ReferencesBackstory = true;
                }
                else if (!tabooMentioned && _rng.Chance(_config.ProbabilityMentionTaboo) && activeTaboos != null && activeTaboos.Count > 0)
                {
                    next = BuildTabooReinforcementLine(primaryNpc, activeTaboos, ref unseenRulePlaced);
                    tabooMentioned = true;
                    unit.ReferencesTaboo = true;
                }
                else
                {
                    next = BuildFillerLine(primaryNpc, styleProfile);
                }

                EnsureWordLimit(next);
                unit.Lines.Add(next);
            }

            // Last line: closing beat, sometimes a known-false reassurance.
            var closing = BuildClosingBeatLine(primaryNpc, place, ref contradictionPlaced);
            EnsureWordLimit(closing);
            unit.Lines.Add(closing);

            // Mark booleans for validation & systems.
            unit.HasContradiction = contradictionPlaced;
            unit.HintsUnseenRule = unseenRulePlaced;

            // Post-pass: infer tone band (here we just assert SlavicHorror, but you can plug classifier).
            var toneBand = ClassifyTone(unit);
            if (toneBand != _config.RequiredToneBand)
            {
                // In production you might re-roll certain lines or flag for writer.
                // Here we simply tag off-tone lines as ambiguous.
                foreach (var l in unit.Lines)
                {
                    if (l.Reliability == LineReliability.Reliable)
                        l.Reliability = LineReliability.Ambiguous;
                }
            }

            return unit;
        }

        #endregion

        #region Line Builders

        private DialogueLine BuildRuleOrAtmosphereLine(
            CharacterRef npc,
            PlaceRef place,
            IReadOnlyList<TabooRef> taboos,
            SpiritRef spirit,
            ref bool unseenRulePlaced)
        {
            var line = NewLine(npc);
            line.Intent = "implyrule";

            var sb = new StringBuilder();
            var placeName = place?.DisplayName ?? "this place";

            if (taboos != null && taboos.Count > 0 && _rng.Chance(0.8f))
            {
                var t = _rng.Pick(taboos);
                sb.Append($"Here in {placeName}, we do not {VerbifyTaboo(t)} after dark.");
                unseenRulePlaced = true;
                line.Reliability = LineReliability.Ambiguous;
                line.SystemTags = new[] { "imply_taboo", $"taboo:{t.Id}" };
            }
            else
            {
                sb.Append($"Nothing here is truly quiet, not even the ground under {placeName}.");
                line.Reliability = LineReliability.Reliable;
                line.SystemTags = new[] { "atmosphere" };
            }

            if (spirit != null && _rng.Chance(0.6f))
            {
                sb.Append(" The old one in the trees listens when you forget yourself.");
                unseenRulePlaced = true;
                line.SystemTags = line.SystemTags.Concat(new[] { $"spirit_hint:{spirit.Id}" }).ToArray();
            }

            line.Text = ApplyStyle(sb.ToString(), npc, DialogueStyleProfile.RuralSparse);
            EnsureWordLimit(line);
            return line;
        }

        private DialogueLine BuildPlayerPushbackLine(
            CharacterRef player,
            CharacterRef npc,
            PlaceRef place)
        {
            var line = NewLine(player);
            line.Intent = "question_rule";
            line.Reliability = LineReliability.Reliable;

            string placeName = place?.DisplayName ?? "here";

            var variants = new[]
            {
                $"You really believe {placeName} remembers who breaks the rules?",
                "That sounds like a story you tell children.",
                "What happens if someone ignores that?",
                "And you just accept that as normal?"
            };

            line.Text = _rng.Pick(variants);
            line.SystemTags = new[] { "player_pushback" };
            EnsureWordLimit(line);
            return line;
        }

        private DialogueLine BuildRumorLine(
            CharacterRef npc,
            IReadOnlyList<RumorRef> rumors,
            ref bool contradictionPlaced)
        {
            var line = NewLine(npc);
            line.Intent = "contradictevidence";
            var rumor = _rng.Pick(rumors);
            string variant = _rng.Pick(rumor.Variants);

            // Some variants are intentionally wrong or slanted.
            bool makeKnownFalse = _rng.Chance(_config.ProbabilityOfKnownFalseLine);
            if (makeKnownFalse)
            {
                line.Reliability = LineReliability.KnownFalse;
                contradictionPlaced = true;
            }
            else
            {
                line.Reliability = LineReliability.Ambiguous;
            }

            // Wrap variant in speech framing consistent with Slavic horror rumor webs.[file:1]
            var prefixOptions = new[]
            {
                "They swear that",
                "People still whisper that",
                "If you ask in the right kitchen, they say",
                "Off the record,"
            };
            var suffixOptions = new[]
            {
                " but the papers never mentioned it.",
                " though everyone involved is suddenly very pious.",
                " and no one likes to stand near that spot now.",
                " you can check the old files if you dare."
            };

            var sb = new StringBuilder();
            sb.Append(_rng.Pick(prefixOptions));
            sb.Append(" ");
            sb.Append(variant.TrimEnd('.', ' '));
            sb.Append(_rng.Pick(suffixOptions));

            line.Text = ApplyStyle(sb.ToString(), npc, DialogueStyleProfile.RuralSparse);
            line.SystemTags = new[] { "rumor", $"rumor:{rumor.Id}" };
            EnsureWordLimit(line);
            return line;
        }

        private DialogueLine BuildBackstoryLeakLine(CharacterRef npc)
        {
            var line = NewLine(npc);
            line.Intent = "revealflaw";
            line.Reliability = LineReliability.Ambiguous;

            var back = npc.Backstory;
            if (back == null)
            {
                line.Text = ApplyStyle("Some of us already paid once. The forest keeps the receipt.", npc,
                    DialogueStyleProfile.RuralSparse);
                line.SystemTags = new[] { "backstory_hint" };
                EnsureWordLimit(line);
                return line;
            }

            string goal = back.DrivingGoals != null && back.DrivingGoals.Length > 0
                ? _rng.Pick(back.DrivingGoals)
                : "protectfamily";
            string fear = back.Fears != null && back.Fears.Length > 0
                ? _rng.Pick(back.Fears)
                : "forest";
            string secret = back.Secrets != null && back.Secrets.Length > 0
                ? _rng.Pick(back.Secrets)
                : "what really happened that winter";

            var sb = new StringBuilder();

            if (_config.AllowMoralHorror && _rng.Chance(0.6f))
            {
                sb.Append("You do not keep a family alive here without stepping on something that twitches.");
            }
            else
            {
                sb.Append("Everyone here carries something that should have stayed buried.");
            }

            sb.Append(" I did what I had to, when ");
            sb.Append(fear);
            sb.Append(" came to collect on ");
            sb.Append(goal);
            sb.Append(", and now no one asks me about ");
            sb.Append(secret);
            sb.Append(".");

            line.Text = ApplyStyle(sb.ToString(), npc, DialogueStyleProfile.RuralSparse);
            line.SystemTags = new[] { "backstory_leak", $"backstory:{back.Id}" };
            EnsureWordLimit(line);
            return line;
        }

        private DialogueLine BuildTabooReinforcementLine(
            CharacterRef npc,
            IReadOnlyList<TabooRef> taboos,
            ref bool unseenRulePlaced)
        {
            var t = _rng.Pick(taboos);
            var line = NewLine(npc);
            line.Intent = "reinforcetaboo";

            bool asThreat = _rng.Chance(0.7f);
            var sb = new StringBuilder();

            if (asThreat)
            {
                sb.Append($"Break {t.DisplayName.ToLower()} once, and you will spend the next night counting who is missing.");
                line.Reliability = LineReliability.Ambiguous;
            }
            else
            {
                sb.Append($"Keep {t.DisplayName.ToLower()} and the ground pretends not to notice you.");
                line.Reliability = LineReliability.Reliable;
            }

            unseenRulePlaced = true;
            line.Text = ApplyStyle(sb.ToString(), npc, DialogueStyleProfile.RuralSparse);
            line.SystemTags = new[] { "taboo_reinforce", $"taboo:{t.Id}" };
            EnsureWordLimit(line);
            return line;
        }

        private DialogueLine BuildFillerLine(CharacterRef npc, DialogueStyleProfile style)
        {
            var line = NewLine(npc);
            line.Intent = "atmosphere";
            line.Reliability = LineReliability.Ambiguous;

            string baseText;
            switch (style)
            {
                case DialogueStyleProfile.BureaucraticCold:
                    baseText = _rng.Pick(_bureaucraticFillers);
                    break;
                case DialogueStyleProfile.Delirious:
                    baseText = _rng.Pick(_deliriousFillers);
                    break;
                default:
                    baseText = _rng.Pick(_ruralFillers);
                    break;
            }

            line.Text = ApplyStyle(baseText, npc, style);
            line.SystemTags = new[] { "atmosphere_filler" };
            EnsureWordLimit(line);
            return line;
        }

        private DialogueLine BuildClosingBeatLine(
            CharacterRef npc,
            PlaceRef place,
            ref bool contradictionPlaced)
        {
            var line = NewLine(npc);
            line.Intent = "close";
            string placeName = place?.DisplayName ?? "here";

            bool lie = _rng.Chance(_config.ProbabilityOfKnownFalseLine);
            if (lie)
            {
                line.Reliability = LineReliability.KnownFalse;
                line.SystemTags = new[] { "reassurance_lie" };
                contradictionPlaced = true;
                line.Text = ApplyStyle($"Relax. No one has gone missing from {placeName} in years.", npc,
                    DialogueStyleProfile.RuralSparse);
            }
            else
            {
                line.Reliability = LineReliability.Ambiguous;
                line.SystemTags = new[] { "ominous_close" };
                line.Text = ApplyStyle($"If you hear singing out past the last pole, do not turn around. Just count your teeth and keep walking.", npc,
                    DialogueStyleProfile.RuralSparse);
            }

            EnsureWordLimit(line);
            return line;
        }

        #endregion

        #region Helper Construction & Style

        private DialogueLine NewLine(CharacterRef speaker)
        {
            return new DialogueLine
            {
                LineId = $"LINE_{speaker.Id}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                SpeakerId = speaker.Id,
                SpeakerDisplayName = speaker.DisplayName,
                SpeakerRole = speaker.Role,
                Text = string.Empty,
                Intent = "none",
                Reliability = LineReliability.Reliable,
                SystemTags = Array.Empty<string>()
            };
        }

        private string ApplyStyle(string raw, CharacterRef speaker, DialogueStyleProfile style)
        {
            // Lightweight styling to suggest dialect & register without fixing accent details.
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            string text = raw.Trim();

            // RuralSparse: shorter, clipped, removes some conjunctions.
            if (style == DialogueStyleProfile.RuralSparse)
            {
                text = text.Replace(" and ", ", ");
                // Occasionally drop pronoun at start.
                if (_rng.Chance(0.35f) && text.StartsWith("I "))
                {
                    text = text.Substring(2);
                }
            }

            // BureaucraticCold: insert mild bureaucratic jargon.
            if (style == DialogueStyleProfile.BureaucraticCold)
            {
                if (_rng.Chance(0.5f))
                {
                    text = "According to procedure, " + char.ToLowerInvariant(text[0]) + text.Substring(1);
                }
            }

            // Delirious: occasionally break rhythm.
            if (style == DialogueStyleProfile.Delirious)
            {
                if (_rng.Chance(0.4f))
                {
                    text = text + " The numbers do not add up, but they still count us.";
                }
            }

            return text;
        }

        private void EnsureWordLimit(DialogueLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.Text)) return;
            if (StringUtil.CountWords(line.Text) > _config.MaxWordsPerLine)
                line.Text = StringUtil.TruncateWords(line.Text, _config.MaxWordsPerLine);
        }

        private string VerbifyTaboo(TabooRef taboo)
        {
            if (taboo == null || string.IsNullOrEmpty(taboo.DisplayName))
                return "break the rule";

            var lower = taboo.DisplayName.ToLowerInvariant();
            if (lower.StartsWith("no "))
                return lower.Substring(3);
            if (lower.StartsWith("do not "))
                return lower.Substring(7);
            return lower;
        }

        private ToneBand ClassifyTone(DialogueUnit unit)
        {
            // Stub classifier: in production, wire to a style model trained on approved Cell text.[file:1]
            // Here, treat everything as SlavicHorror unless there are obvious non-horror markers.
            foreach (var l in unit.Lines)
            {
                var t = l.Text.ToLowerInvariant();
                if (t.Contains("hero") && t.Contains("glory"))
                    return ToneBand.OffTone;
            }
            return ToneBand.SlavicHorror;
        }

        #endregion
    }

    #endregion
}
