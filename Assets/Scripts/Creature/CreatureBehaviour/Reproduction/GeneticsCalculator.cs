using System;
using UnityEngine;

public struct GeneticsParameters
{
    public float mutationChance;
    public float mutationRate;
    // Historical quirk: the herbivore desirability range is used for BOTH species
    // (offspring desirability and the acceptance-chance normalization).
    public float desirabilityMin;
    public float desirabilityMax;
    public float agilityMin;
    public float agilityMax;
    public float strengthMin;
    public float strengthMax;
    public float sprintDurationMin;
    public float sprintDurationMax;
    public float sprintFactorMin;
    public float sprintFactorMax;
    public float sprintCooldownMin;
    public float sprintCooldownMax;
    public float sprintCooldownSpeedFactorMin;
    public float sprintCooldownSpeedFactorMax;
    public int minOffspringPerReproduction;
    public int maxOffspringPerReproduction;
    public float offspringCountMean;
    public float offspringCountStdDev;
    public float minMateAcceptanceChance;
    public float maxMateAcceptanceChance;
    public float utilityBehaviorWeightMin;
    public float utilityBehaviorWeightMax;

    // Mirror of UtilityBehaviorGenetics' private DefaultInitialMinWeight/MaxWeight fallback.
    private const float DefaultUtilityBehaviorWeightMin = 0.75f;
    private const float DefaultUtilityBehaviorWeightMax = 1.25f;

    public static GeneticsParameters FromConfig(GameConfig config, bool isPredator)
    {
        GetUtilityBehaviorWeightRange(config, out float utilityWeightMin, out float utilityWeightMax);

        return new GeneticsParameters
        {
            mutationChance = config.mutationChance,
            mutationRate = config.mutationRate,
            desirabilityMin = config.herbivoreDesirabilityMin,
            desirabilityMax = config.herbivoreDesirabilityMax,
            agilityMin = config.herbivoreAgilityMin,
            agilityMax = config.herbivoreAgilityMax,
            strengthMin = config.predatorStrengthMin,
            strengthMax = config.predatorStrengthMax,
            sprintDurationMin = isPredator ? config.predatorSprintDurationMin : config.herbivoreSprintDurationMin,
            sprintDurationMax = isPredator ? config.predatorSprintDurationMax : config.herbivoreSprintDurationMax,
            sprintFactorMin = isPredator ? config.predatorSprintFactorMin : config.herbivoreSprintFactorMin,
            sprintFactorMax = isPredator ? config.predatorSprintFactorMax : config.herbivoreSprintFactorMax,
            sprintCooldownMin = isPredator ? config.predatorSprintCooldownMin : config.herbivoreSprintCooldownMin,
            sprintCooldownMax = isPredator ? config.predatorSprintCooldownMax : config.herbivoreSprintCooldownMax,
            sprintCooldownSpeedFactorMin = isPredator
                ? config.predatorSprintCooldownSpeedFactorMin
                : config.herbivoreSprintCooldownSpeedFactorMin,
            sprintCooldownSpeedFactorMax = isPredator
                ? config.predatorSprintCooldownSpeedFactorMax
                : config.herbivoreSprintCooldownSpeedFactorMax,
            minOffspringPerReproduction = config.GetMinOffspringPerReproduction(isPredator),
            maxOffspringPerReproduction = config.GetMaxOffspringPerReproduction(isPredator),
            offspringCountMean = config.GetOffspringCountMean(isPredator),
            offspringCountStdDev = config.GetOffspringCountStdDev(isPredator),
            minMateAcceptanceChance = config.GetMinMateAcceptanceChance(isPredator),
            maxMateAcceptanceChance = config.GetMaxMateAcceptanceChance(isPredator),
            utilityBehaviorWeightMin = utilityWeightMin,
            utilityBehaviorWeightMax = utilityWeightMax
        };
    }

    /// <summary>
    /// Mirrors UtilityBehaviorGenetics.GetWeightRange (private there) so the reproduction
    /// path keeps the exact same sanitized random-mutation range. Keep the two in sync.
    /// </summary>
    private static void GetUtilityBehaviorWeightRange(GameConfig config, out float minWeight, out float maxWeight)
    {
        if (config == null)
        {
            minWeight = DefaultUtilityBehaviorWeightMin;
            maxWeight = DefaultUtilityBehaviorWeightMax;
            return;
        }

        minWeight = UtilityBehaviorScoring.ClampWeight(Mathf.Min(config.utilityBehaviorWeightMin, config.utilityBehaviorWeightMax));
        maxWeight = UtilityBehaviorScoring.ClampWeight(Mathf.Max(config.utilityBehaviorWeightMin, config.utilityBehaviorWeightMax));
        if (maxWeight <= minWeight ||
            (maxWeight <= UtilityBehaviorScoring.DefaultWeight * 0.01f &&
             minWeight <= UtilityBehaviorScoring.DefaultWeight * 0.01f))
        {
            minWeight = DefaultUtilityBehaviorWeightMin;
            maxWeight = DefaultUtilityBehaviorWeightMax;
        }
    }
}

/// <summary>
/// Pure inheritance/genetics math shared by ReproductionManager (legacy mono path) and
/// future ECS reproduction jobs. Must stay behaviorally identical to the historical
/// ReproductionManager implementation; randomness is injected via IRandomSource so the
/// mono path keeps its UnityEngine.Random draw order and tests/jobs stay deterministic.
/// </summary>
public static class GeneticsCalculator
{
    public const float SocialStrategyFatherInheritanceChance = 0.475f;
    public const float SocialStrategyMotherInheritanceChance = 0.475f;

    public static float InheritWithMutation<TRandom>(
        float trait1,
        float trait2,
        float minvalue,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        float inheritedTrait = rng.NextFloat() < 0.5f ? trait1 : trait2;

        if (rng.NextFloat() < parameters.mutationChance)
        {
            float mutationAmount = rng.NextFloat((-1) * parameters.mutationRate, parameters.mutationRate);
            inheritedTrait += mutationAmount;
        }

        // Historical quirk preserved verbatim: raises the trait by its deficit (numerically
        // a clamp to minvalue) — do not normalize to Mathf.Max.
        if (inheritedTrait < minvalue)
        {
            inheritedTrait += (minvalue - inheritedTrait);
        }

        return inheritedTrait;
    }

    public static float InheritWithMutation<TRandom>(
        float trait1,
        float trait2,
        float minvalue,
        float maxValue,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        float inheritedTrait = InheritWithMutation(trait1, trait2, minvalue, parameters, ref rng);
        return Mathf.Clamp(inheritedTrait, minvalue, maxValue);
    }

    /// <summary>Box-Muller transform; consumes exactly two draws.</summary>
    public static float SampleNormalDistribution<TRandom>(float mean, float stdDev, ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        float u1 = 1f - rng.NextFloat();
        float u2 = 1f - rng.NextFloat();
        float standardNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return mean + stdDev * standardNormal;
    }

    public static int GetOffspringCount<TRandom>(in GeneticsParameters parameters, ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        int minCount = Math.Min(parameters.minOffspringPerReproduction, parameters.maxOffspringPerReproduction);
        int maxCount = Math.Max(parameters.minOffspringPerReproduction, parameters.maxOffspringPerReproduction);

        float sampled = SampleNormalDistribution(
            parameters.offspringCountMean,
            Math.Max(0.0001f, parameters.offspringCountStdDev),
            ref rng);
        int rounded = Mathf.RoundToInt(sampled);
        return Mathf.Clamp(rounded, minCount, maxCount);
    }

    /// <summary>
    /// Desirability → acceptance-chance mapping. Normalizes against the (herbivore)
    /// desirability range, lerps between the species min/max acceptance chance, then
    /// scales by the population mating-intent multiplier (clamped to 01).
    /// </summary>
    public static float ComputeAcceptanceChance(
        float candidateDesirability,
        float populationIntentMultiplier,
        in GeneticsParameters parameters)
    {
        float normalized = Mathf.InverseLerp(parameters.desirabilityMin, parameters.desirabilityMax, candidateDesirability);
        float acceptanceChance = Mathf.Lerp(
            parameters.minMateAcceptanceChance,
            parameters.maxMateAcceptanceChance,
            normalized);
        return Mathf.Clamp01(acceptanceChance * populationIntentMultiplier);
    }

    public static bool RollAcceptance<TRandom>(
        float candidateDesirability,
        float populationIntentMultiplier,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return rng.NextFloat() <= ComputeAcceptanceChance(candidateDesirability, populationIntentMultiplier, parameters);
    }

    /// <summary>
    /// Father 47.5% / mother 47.5% / uniform coin flip for the remaining 5%.
    /// Consumes one draw, plus a second draw only on the random remainder.
    /// </summary>
    public static HerbivoreSocialStrategy InheritSocialStrategy<TRandom>(
        HerbivoreSocialStrategy fatherStrategy,
        HerbivoreSocialStrategy motherStrategy,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        float roll = rng.NextFloat();
        if (roll < SocialStrategyFatherInheritanceChance)
            return fatherStrategy;

        if (roll < SocialStrategyFatherInheritanceChance + SocialStrategyMotherInheritanceChance)
            return motherStrategy;

        return rng.NextFloat() < 0.5f
            ? HerbivoreSocialStrategy.Hawk
            : HerbivoreSocialStrategy.Dove;
    }

    public static float GetInheritedSprintDuration<TRandom>(
        float parentSprintDuration,
        float mateSprintDuration,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentSprintDuration,
            mateSprintDuration,
            parameters.sprintDurationMin,
            parameters.sprintDurationMax,
            parameters,
            ref rng);
    }

    public static float GetInheritedSprintFactor<TRandom>(
        float parentSprintFactor,
        float mateSprintFactor,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentSprintFactor,
            mateSprintFactor,
            parameters.sprintFactorMin,
            parameters.sprintFactorMax,
            parameters,
            ref rng);
    }

    public static float GetInheritedSprintCooldown<TRandom>(
        float parentSprintCooldown,
        float mateSprintCooldown,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentSprintCooldown,
            mateSprintCooldown,
            parameters.sprintCooldownMin,
            parameters.sprintCooldownMax,
            parameters,
            ref rng);
    }

    public static float GetInheritedSprintCooldownSpeedFactor<TRandom>(
        float parentSprintCooldownSpeedFactor,
        float mateSprintCooldownSpeedFactor,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentSprintCooldownSpeedFactor,
            mateSprintCooldownSpeedFactor,
            parameters.sprintCooldownSpeedFactorMin,
            parameters.sprintCooldownSpeedFactorMax,
            parameters,
            ref rng);
    }

    /// <summary>Midpoint fallback used when a parent has no agility gene (non-herbivore).</summary>
    public static float GetDefaultAgility(in GeneticsParameters parameters)
    {
        return Mathf.Lerp(parameters.agilityMin, parameters.agilityMax, 0.5f);
    }

    public static float GetInheritedAgility<TRandom>(
        float parentAgility,
        float mateAgility,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentAgility,
            mateAgility,
            parameters.agilityMin,
            parameters.agilityMax,
            parameters,
            ref rng);
    }

    /// <summary>Midpoint fallback used when a parent has no strength gene (non-predator).</summary>
    public static float GetDefaultStrength(in GeneticsParameters parameters)
    {
        return Mathf.Lerp(parameters.strengthMin, parameters.strengthMax, 0.5f);
    }

    public static float GetInheritedStrength<TRandom>(
        float parentStrength,
        float mateStrength,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentStrength,
            mateStrength,
            parameters.strengthMin,
            parameters.strengthMax,
            parameters,
            ref rng);
    }

    public static float GetInheritedDesirability<TRandom>(
        float parentDesirability,
        float mateDesirability,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        return InheritWithMutation(
            parentDesirability,
            mateDesirability,
            parameters.desirabilityMin,
            parameters.desirabilityMax,
            parameters,
            ref rng);
    }

    /// <summary>
    /// RNG-injected port of UtilityBehaviorGenetics.InheritProfile (kept there for the
    /// spawn-time random profile path); contribution constants are shared from that class.
    /// Consumes four draws, one per gene in declaration order.
    /// </summary>
    public static CreatureUtilityBehaviorData InheritUtilityBehaviorProfile<TRandom>(
        in CreatureUtilityBehaviorData fatherProfile,
        in CreatureUtilityBehaviorData motherProfile,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        CreatureUtilityBehaviorData inherited = new CreatureUtilityBehaviorData
        {
            keepCurrentStateWeight = BlendUtilityGene(
                fatherProfile.keepCurrentStateWeight,
                motherProfile.keepCurrentStateWeight,
                parameters,
                ref rng),
            foodActionWeight = BlendUtilityGene(
                fatherProfile.foodActionWeight,
                motherProfile.foodActionWeight,
                parameters,
                ref rng),
            searchMateWeight = BlendUtilityGene(
                fatherProfile.searchMateWeight,
                motherProfile.searchMateWeight,
                parameters,
                ref rng),
            wanderWeight = BlendUtilityGene(
                fatherProfile.wanderWeight,
                motherProfile.wanderWeight,
                parameters,
                ref rng)
        };

        return UtilityBehaviorScoring.Sanitize(inherited);
    }

    private static float BlendUtilityGene<TRandom>(
        float fatherValue,
        float motherValue,
        in GeneticsParameters parameters,
        ref TRandom rng)
        where TRandom : struct, IRandomSource
    {
        float randomMutationValue = rng.NextFloat(parameters.utilityBehaviorWeightMin, parameters.utilityBehaviorWeightMax);
        return fatherValue * UtilityBehaviorGenetics.FatherContribution +
               motherValue * UtilityBehaviorGenetics.MotherContribution +
               randomMutationValue * UtilityBehaviorGenetics.MutationContribution;
    }
}
