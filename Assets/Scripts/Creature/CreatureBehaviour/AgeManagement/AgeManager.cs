using System;
using UnityEngine;

public class AgeManager
{
    private BaseCreatureBehaviour creature;
    public float Age { get; private set; }

    public const float StartScale = 0.25f; // newborn scale

    /// <summary>How far along maturity the creature is (0 = newborn, 1 = fully mature).</summary>
    public float MaturityFraction { get; private set; }

    public AgeManager(BaseCreatureBehaviour creature)
    {
        this.creature = creature;
        creature.transform.localScale = Vector3.one * StartScale;
    }

    public void SetInitialAge(float age)
    {
        float oldAgeStart = GameConfig.Instance != null ? GameConfig.Instance.oldAgeStartAge : 0f;
        Age = Mathf.Clamp(age, 0f, oldAgeStart);
        float maturityAge = GameConfig.Instance != null ? GameConfig.Instance.maturityAge : 1f;
        MaturityFraction = Mathf.Clamp01(Age / Mathf.Max(0.0001f, maturityAge));
        ApplyGrowth();
        ApplyOldAgeEffects();
    }

    public void UpdateAge(float amount)
    {
        Age += amount;
        MaturityFraction = Mathf.Clamp01(Age / GameConfig.Instance.maturityAge);
        ApplyGrowth();
        ApplyOldAgeEffects();
    }

    public bool IsMaxAgeReached()
    {
        var config = GameConfig.Instance;
        if (config == null || config.creatureMaxAge <= 0f)
            return false;

        return Age >= config.creatureMaxAge;
    }

    public void ApplyECSLifecycle(
        float age,
        float maturityFraction,
        float speedMultiplier,
        float senseMultiplier,
        float desirabilityMultiplier)
    {
        Age = Mathf.Max(0f, age);
        MaturityFraction = Mathf.Clamp01(maturityFraction);
        ApplyGrowth();
        ApplyOldAgeEffects(speedMultiplier, senseMultiplier, desirabilityMultiplier);
    }

    private void ApplyGrowth()
    {
        float scale = CreatureLifecycleCalculator.CalculateGrowthScale(MaturityFraction);
        creature.transform.localScale = Vector3.one * scale;
    }

    private void ApplyOldAgeEffects()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return;

        float speedMultiplier = CreatureLifecycleCalculator.CalculateOldAgeMultiplier(
            Age,
            config.oldAgeSpeedDecayPerSecond,
            config.oldAgeStartAge,
            config.oldAgeMinSpeedMultiplier);
        float senseMultiplier = CreatureLifecycleCalculator.CalculateOldAgeMultiplier(
            Age,
            config.oldAgeSenseDecayPerSecond,
            config.oldAgeStartAge,
            config.oldAgeMinSenseMultiplier);
        float desirabilityMultiplier = CreatureLifecycleCalculator.CalculateOldAgeMultiplier(
            Age,
            config.oldAgeDesirabilityDecayPerSecond,
            config.oldAgeStartAge,
            config.oldAgeMinDesirabilityMultiplier);
        ApplyOldAgeEffects(speedMultiplier, senseMultiplier, desirabilityMultiplier);
    }

    private void ApplyOldAgeEffects(
        float speedMultiplier,
        float senseMultiplier,
        float desirabilityMultiplier)
    {
        creature.MovementManager?.SetAgeMultiplier(speedMultiplier);
        creature.ObservationManager?.SetAgeMultiplier(senseMultiplier);
        creature.ReproductionManager?.SetAgeMultiplier(desirabilityMultiplier);
    }
}
