using System;

[Serializable]
public struct UtilityAIScoringParameters
{
    public float eatingEnergyThreshold;
    public float reproductionEnergyThreshold;

    public UtilityAIScoringParameters(float eatingEnergyThreshold, float reproductionEnergyThreshold)
    {
        this.eatingEnergyThreshold = UtilityAIScoreRules.Clamp01(eatingEnergyThreshold);
        this.reproductionEnergyThreshold = UtilityAIScoreRules.Clamp01(reproductionEnergyThreshold);
    }

    public static UtilityAIScoringParameters Default => new UtilityAIScoringParameters(0.7f, 0.6f);
}
