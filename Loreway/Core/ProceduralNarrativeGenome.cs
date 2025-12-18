using System;
using System.Collections.Generic;
using System.Linq;

namespace Loreway.Core
{
    public enum EmotionState { Fear, Hope, Grief, Rage, Obsession, Faith }
    public enum Archetype { Hero, Witch, LostSoul, Trickster, Oracle, Beast, Revenant }
    public enum ToneProfile { Tragic, Mythic, Dread, Melancholic, Heroic, Nihilistic }

    [Serializable]
    public class LoreGene
    {
        public string GeneName;
        public Archetype ArchetypeType;
        public ToneProfile Tone;
        public float MythWeight;
        public Dictionary<EmotionState, float> EmotionBias = new();
        public List<string> RelicInfluences = new(); 
        public string OriginEventID;

        public LoreGene(string name, Archetype type, ToneProfile tone)
        {
            GeneName = name;
            ArchetypeType = type;
            Tone = tone;
            MythWeight = UnityEngine.Random.Range(0.3f, 1.0f);
            foreach (EmotionState e in Enum.GetValues(typeof(EmotionState)))
                EmotionBias.Add(e, UnityEngine.Random.Range(0f, 1f));
        }
    }

    [Serializable]
    public class StoryThread
    {
        public string ThreadID;
        public string Title;
        public List<LoreGene> GeneticSequence = new();
        public string Summary;
        public float CoherenceScore;
        public List<string> ConnectedEvents = new();

        public StoryThread(string title, List<LoreGene> genes)
        {
            ThreadID = Guid.NewGuid().ToString();
            Title = title;
            GeneticSequence = genes;
            CoherenceScore = EvaluateCoherence();
            Summary = GenerateSummary();
        }

        private float EvaluateCoherence()
        {
            // Genetic coherence = tone similarity + emotional entropy balance
            float toneHarmony = (float)GeneticSequence
                .GroupBy(g => g.Tone).Max(g => g.Count()) / GeneticSequence.Count;
            float emotionalVariance = GeneticSequence.SelectMany(g => g.EmotionBias.Values).Average();
            return Math.Clamp((toneHarmony + emotionalVariance) / 2f, 0f, 1f);
        }

        private string GenerateSummary()
        {
            string dominantTone = GeneticSequence
                .GroupBy(g => g.Tone).OrderByDescending(g => g.Count()).First().Key.ToString();
            string coreArchetype = GeneticSequence
                .GroupBy(g => g.ArchetypeType).OrderByDescending(g => g.Count()).First().Key.ToString();

            return $"A {dominantTone.ToLower()} tale woven around a {coreArchetype.ToLower()} figure, " +
                   $"haunted by {GeneticSequence.Count} threads of forgotten memory.";
        }
    }

    public static class ProceduralNarrativeGenome
    {
        private static readonly string[] SlavicRoots = { "Leshy", "Rusalka", "Koschei", "Veles", "BabaYaga" };

        public static StoryThread GenerateAdaptiveNarrative(string playerMoralVector)
        {
            List<LoreGene> genes = new();
            Archetype archetype = (Archetype)UnityEngine.Random.Range(0, Enum.GetValues(typeof(Archetype)).Length);
            ToneProfile tone = (ToneProfile)UnityEngine.Random.Range(0, Enum.GetValues(typeof(ToneProfile)).Length);

            int geneCount = UnityEngine.Random.Range(4, 9);
            for (int i = 0; i < geneCount; i++)
            {
                var newGene = new LoreGene(SlavicRoots[UnityEngine.Random.Range(0, SlavicRoots.Length)], archetype, tone);
                newGene.RelicInfluences.Add(playerMoralVector);
                newGene.OriginEventID = Guid.NewGuid().ToString();
                genes.Add(newGene);
            }

            return new StoryThread($"The Song of {SlavicRoots[UnityEngine.Random.Range(0, SlavicRoots.Length)]}", genes);
        }

        public static List<StoryThread> EvolveNarrativeWorld(List<StoryThread> existingThreads)
        {
            List<StoryThread> evolved = new();
            foreach (var thread in existingThreads)
            {
                var mutatedGenes = MutateGenes(thread.GeneticSequence);
                evolved.Add(new StoryThread(thread.Title + " II", mutatedGenes));
            }
            return evolved;
        }

        private static List<LoreGene> MutateGenes(List<LoreGene> originalGenes)
        {
            List<LoreGene> mutated = new();
            foreach (var gene in originalGenes)
            {
                var clone = new LoreGene(gene.GeneName, gene.ArchetypeType, gene.Tone);
                foreach (var key in gene.EmotionBias.Keys.ToList())
                {
                    float mutation = UnityEngine.Random.Range(-0.1f, 0.1f);
                    clone.EmotionBias[key] = Math.Clamp(gene.EmotionBias[key] + mutation, 0f, 1f);
                }
                clone.MythWeight = Math.Clamp(gene.MythWeight + UnityEngine.Random.Range(-0.2f, 0.2f), 0f, 1f);
                mutated.Add(clone);
            }
            return mutated;
        }
    }
}
