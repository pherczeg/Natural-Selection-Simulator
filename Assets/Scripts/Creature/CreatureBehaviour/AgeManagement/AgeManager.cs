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

    private void ApplyGrowth()
    {
        float scale = Mathf.Lerp(StartScale, 1f, Mathf.SmoothStep(0f, 1f, MaturityFraction));
        creature.transform.localScale = Vector3.one * scale;
    }

    private void ApplyOldAgeEffects()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return;

        float speedMultiplier = CalculateOldAgeMultiplier(config.oldAgeSpeedDecayPerSecond, config.oldAgeStartAge, config.oldAgeMinSpeedMultiplier);
        float senseMultiplier = CalculateOldAgeMultiplier(config.oldAgeSenseDecayPerSecond, config.oldAgeStartAge, config.oldAgeMinSenseMultiplier);
        float desirabilityMultiplier = CalculateOldAgeMultiplier(config.oldAgeDesirabilityDecayPerSecond, config.oldAgeStartAge, config.oldAgeMinDesirabilityMultiplier);

        creature.MovementManager?.SetAgeMultiplier(speedMultiplier);
        creature.ObservationManager?.SetAgeMultiplier(senseMultiplier);
        creature.ReproductionManager?.SetAgeMultiplier(desirabilityMultiplier);
    }

    private float CalculateOldAgeMultiplier(float decayPerSecond, float oldAgeStart, float minMultiplier)
    {
        if (Age <= oldAgeStart)
            return 1f;

        float elapsedOldAge = Age - oldAgeStart;
        float unclamped = 1f - elapsedOldAge * Mathf.Max(0f, decayPerSecond);
        return Mathf.Clamp(unclamped, Mathf.Clamp01(minMultiplier), 1f);
    }
}