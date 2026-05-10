public static class UtilityAIScoreRules
{
    public static float GetHungerScore(UtilityAIContext context, UtilityAIScoringParameters parameters)
    {
        float hungerThreshold = Clamp01(parameters.eatingEnergyThreshold);
        if (hungerThreshold <= 0f || context.energyPercent >= hungerThreshold)
            return 0f;

        float hungerDepth = 1f - Clamp01(context.energyPercent / hungerThreshold);
        return Lerp(0.9f, 1f, hungerDepth);
    }

    public static float GetReproductionScore(UtilityAIContext context, UtilityAIScoringParameters parameters)
    {
        if (!context.isMature || !context.isReproductionReady)
            return 0f;

        if (IsBelowEatingEnergyThreshold(context, parameters))
            return 0f;

        float reproductionThreshold = Clamp01(parameters.reproductionEnergyThreshold);
        float energyReadiness = reproductionThreshold < 1f
            ? InverseLerp(reproductionThreshold, 1f, context.energyPercent)
            : 1f;

        return Lerp(0.85f, 1f, energyReadiness);
    }

    public static float GetFoodSearchAvailabilityScore(UtilityAIContext context)
    {
        return IsHardTransitionLocked(context.currentState) || context.isThreatened ? 0f : 1f;
    }

    public static float GetMateSearchAvailabilityScore(UtilityAIContext context)
    {
        return IsHardTransitionLocked(context.currentState) ||
               IsMateSearchState(context.currentState) ||
               context.isThreatened
            ? 0f
            : 1f;
    }

    public static float GetKeepCurrentStateScore(UtilityAIContext context, UtilityAIScoringParameters parameters)
    {
        if (context.isThreatened)
            return 1f;

        if (IsHardTransitionLocked(context.currentState))
            return 1f;

        if (IsMateSearchState(context.currentState))
            return GetHungerScore(context, parameters) > 0f ? 0.05f : 0.85f;

        if (context.currentState == CreatureStateType.Wandering)
            return 0.25f;

        return context.currentState == CreatureStateType.Idle ? 0f : 0.1f;
    }

    public static float GetIdleWanderScore(UtilityAIContext context)
    {
        return context.currentState == CreatureStateType.Idle && !context.isThreatened ? 1f : 0f;
    }

    public static float GetFleeScore(UtilityAIContext context)
    {
        return context.isThreatened ? 1f : 0f;
    }

    public static bool IsHardTransitionLocked(CreatureStateType currentState)
    {
        return currentState == CreatureStateType.MovingToFood ||
               currentState == CreatureStateType.Eating ||
               currentState == CreatureStateType.Predation ||
               currentState == CreatureStateType.SearchingForFood ||
               currentState == CreatureStateType.Reproducting;
    }

    public static bool IsMateSearchState(CreatureStateType currentState)
    {
        return currentState == CreatureStateType.SearchingForMate ||
               currentState == CreatureStateType.MovingToMate;
    }

    public static bool IsBelowEatingEnergyThreshold(UtilityAIContext context, UtilityAIScoringParameters parameters)
    {
        return context.energyPercent < Clamp01(parameters.eatingEnergyThreshold);
    }

    public static float Clamp01(float score)
    {
        if (float.IsNaN(score) || float.IsInfinity(score))
            return 0f;

        if (score <= 0f)
            return 0f;

        return score >= 1f ? 1f : score;
    }

    public static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * Clamp01(t);
    }

    public static float InverseLerp(float from, float to, float value)
    {
        if (Abs(to - from) < 0.000001f)
            return value >= to ? 1f : 0f;

        return Clamp01((value - from) / (to - from));
    }

    private static float Abs(float value)
    {
        return value < 0f ? -value : value;
    }
}
