using UnityEngine;

public struct PredationParameters
{
    public float predatorStrengthScoreWeight;
    public float predatorWeightScoreWeight;
    public float herbivoreAgilityScoreWeight;
    public float herbivoreWeightScoreWeight;
    public float predationBias;
    public float predationSharpness;
    public float minPredationSuccessChance;
    public float maxPredationSuccessChance;
    public float predatorEnergyGainPerPreyWeight;
    public float predatorStrengthMin;
    public float predatorStrengthMax;
    public float predatorWeightMin;
    public float predatorWeightMax;
    public float herbivoreAgilityMin;
    public float herbivoreAgilityMax;
    public float herbivoreWeightMin;
    public float herbivoreWeightMax;

    public static PredationParameters FromConfig(GameConfig config)
    {
        return new PredationParameters
        {
            predatorStrengthScoreWeight = config.predatorStrengthScoreWeight,
            predatorWeightScoreWeight = config.predatorWeightScoreWeight,
            herbivoreAgilityScoreWeight = config.herbivoreAgilityScoreWeight,
            herbivoreWeightScoreWeight = config.herbivoreWeightScoreWeight,
            predationBias = config.predationBias,
            predationSharpness = config.predationSharpness,
            minPredationSuccessChance = config.minPredationSuccessChance,
            maxPredationSuccessChance = config.maxPredationSuccessChance,
            predatorEnergyGainPerPreyWeight = config.predatorEnergyGainPerPreyWeight,
            predatorStrengthMin = config.predatorStrengthMin,
            predatorStrengthMax = config.predatorStrengthMax,
            predatorWeightMin = config.predatorWeightMin,
            predatorWeightMax = config.predatorWeightMax,
            herbivoreAgilityMin = config.herbivoreAgilityMin,
            herbivoreAgilityMax = config.herbivoreAgilityMax,
            herbivoreWeightMin = config.herbivoreWeightMin,
            herbivoreWeightMax = config.herbivoreWeightMax
        };
    }
}

/// <summary>
/// Pure predation resolution math extracted from ECSMirrorBridge. Fully deterministic:
/// the success roll (random draw against the returned chance) stays at the call site,
/// as do all managed-object null/type fallbacks.
/// </summary>
public static class PredationCalculator
{
    /// <summary>
    /// Min/max normalization of a stat into [0, 1]. Bounds may arrive swapped;
    /// a (near-)degenerate range yields the neutral 0.5 (Mathf.Approximately guard).
    /// </summary>
    public static float NormalizePredationStat(float value, float min, float max)
    {
        float normalizedMin = Mathf.Min(min, max);
        float normalizedMax = Mathf.Max(min, max);
        if (Mathf.Approximately(normalizedMin, normalizedMax))
            return 0.5f;

        return Mathf.InverseLerp(normalizedMin, normalizedMax, value);
    }

    /// <summary>
    /// Logistic curve over the predator-vs-prey score advantage:
    /// 1 / (1 + e^-(bias + advantage * sharpness)), clamped to the configured
    /// min/max chance (clamp bounds re-ordered in case they are swapped in config).
    /// </summary>
    public static float CalculatePredationSuccessChance(
        float predatorStrength,
        float predatorWeight,
        float preyAgility,
        float preyWeight,
        in PredationParameters parameters)
    {
        float predatorScore =
            parameters.predatorStrengthScoreWeight * NormalizePredationStat(predatorStrength, parameters.predatorStrengthMin, parameters.predatorStrengthMax) +
            parameters.predatorWeightScoreWeight * NormalizePredationStat(predatorWeight, parameters.predatorWeightMin, parameters.predatorWeightMax);

        float herbivoreScore =
            parameters.herbivoreAgilityScoreWeight * NormalizePredationStat(preyAgility, parameters.herbivoreAgilityMin, parameters.herbivoreAgilityMax) +
            parameters.herbivoreWeightScoreWeight * NormalizePredationStat(preyWeight, parameters.herbivoreWeightMin, parameters.herbivoreWeightMax);

        float advantage = predatorScore - herbivoreScore;
        float chance = 1f / (1f + Mathf.Exp(-(parameters.predationBias + advantage * parameters.predationSharpness)));
        float minChance = Mathf.Min(parameters.minPredationSuccessChance, parameters.maxPredationSuccessChance);
        float maxChance = Mathf.Max(parameters.minPredationSuccessChance, parameters.maxPredationSuccessChance);
        return Mathf.Clamp(chance, minChance, maxChance);
    }

    /// <summary>
    /// Energy a predator gains from a kill: the larger of the prey's current energy
    /// and its "stored" energy (a fixed 75% of base max energy — NOT the maturity-scaled
    /// currentMaxEnergy), plus biomass (weight * predatorEnergyGainPerPreyWeight).
    /// </summary>
    public static float CalculateEnergyGainFromPrey(
        float preyCurrentEnergy,
        float preyMaxEnergy,
        float preyWeight,
        in PredationParameters parameters)
    {
        float preyStoredEnergy = preyMaxEnergy * 0.75f;
        float biomassEnergy = preyWeight * parameters.predatorEnergyGainPerPreyWeight;
        return Mathf.Max(preyCurrentEnergy, preyStoredEnergy) + biomassEnergy;
    }
}
