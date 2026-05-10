using System;

[Serializable]
public struct UtilityAIContext
{
    public float energyPercent;
    public bool isMature;
    public bool isReproductionReady;
    public CreatureStateType currentState;
    public bool hasKnownFood;
    public bool hasKnownMate;
    public bool hasKnownPrey;
    public bool isThreatened;

    public UtilityAIContext(
        float energyPercent,
        bool isMature,
        bool isReproductionReady,
        CreatureStateType currentState,
        bool hasKnownFood,
        bool hasKnownMate,
        bool hasKnownPrey,
        bool isThreatened)
    {
        this.energyPercent = UtilityAIScoreRules.Clamp01(energyPercent);
        this.isMature = isMature;
        this.isReproductionReady = isReproductionReady;
        this.currentState = currentState;
        this.hasKnownFood = hasKnownFood;
        this.hasKnownMate = hasKnownMate;
        this.hasKnownPrey = hasKnownPrey;
        this.isThreatened = isThreatened;
    }

    public static UtilityAIContext Empty => new UtilityAIContext(
        0f,
        false,
        false,
        CreatureStateType.None,
        false,
        false,
        false,
        false);
}
