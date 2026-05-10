public struct CreatureLifecycleParameters
{
    public float updateInterval;
    public float maturityAge;
    public float creatureMaxAge;
    public float oldAgeStartAge;
    public float oldAgeSpeedDecayPerSecond;
    public float oldAgeSenseDecayPerSecond;
    public float oldAgeDesirabilityDecayPerSecond;
    public float oldAgeMinSpeedMultiplier;
    public float oldAgeMinSenseMultiplier;
    public float oldAgeMinDesirabilityMultiplier;
    public float initialEnergyPercentageHerbivore;
    public float initialEnergyPercentagePredator;
    public float energyConsumptionCoefficient;

    public static CreatureLifecycleParameters FromConfig(GameConfig config)
    {
        return new CreatureLifecycleParameters
        {
            updateInterval = config.updateInterval,
            maturityAge = config.maturityAge,
            creatureMaxAge = config.creatureMaxAge,
            oldAgeStartAge = config.oldAgeStartAge,
            oldAgeSpeedDecayPerSecond = config.oldAgeSpeedDecayPerSecond,
            oldAgeSenseDecayPerSecond = config.oldAgeSenseDecayPerSecond,
            oldAgeDesirabilityDecayPerSecond = config.oldAgeDesirabilityDecayPerSecond,
            oldAgeMinSpeedMultiplier = config.oldAgeMinSpeedMultiplier,
            oldAgeMinSenseMultiplier = config.oldAgeMinSenseMultiplier,
            oldAgeMinDesirabilityMultiplier = config.oldAgeMinDesirabilityMultiplier,
            initialEnergyPercentageHerbivore = config.initialEnergyPercentageHerbivore,
            initialEnergyPercentagePredator = config.initialEnergyPercentagePredator,
            energyConsumptionCoefficient = config.energyConsumptionCoefficient
        };
    }
}

public static class CreatureLifecycleCalculator
{
    public const float StartScale = 0.25f;

    public static CreatureLifecycleData Step(
        CreatureLifecycleData data,
        CreatureLifecycleParameters parameters,
        float deltaTime)
    {
        data.starvationDespawnRequested = false;
        data.oldAgeDespawnRequested = false;
        data.energyConsumption = 0f;
        data.accumulatedDeltaTime += Max(0f, deltaTime);

        float updateInterval = Max(0.0001f, parameters.updateInterval);
        if (data.accumulatedDeltaTime < updateInterval)
        {
            RefreshDerivedValues(ref data, parameters);
            return data;
        }

        float simulationDeltaTime = data.accumulatedDeltaTime;
        data.accumulatedDeltaTime = 0f;
        data.age = Max(0f, data.age + simulationDeltaTime);
        RefreshDerivedValues(ref data, parameters);

        data.energyConsumption = CalculateEnergyConsumption(data, parameters);
        data.energyLevel = Clamp(data.energyLevel - data.energyConsumption, 0f, data.currentMaxEnergy);
        data.starvationDespawnRequested = data.energyLevel <= 0f;
        data.oldAgeDespawnRequested = parameters.creatureMaxAge > 0f && data.age >= parameters.creatureMaxAge;

        return data;
    }

    public static void RefreshDerivedValues(
        ref CreatureLifecycleData data,
        CreatureLifecycleParameters parameters)
    {
        data.maturityFraction = CalculateMaturityFraction(data.age, parameters.maturityAge);
        data.currentMaxEnergy = CalculateCurrentMaxEnergy(
            data.maxEnergy,
            data.maturityFraction,
            data.creatureKind,
            parameters);
        data.energyLevel = Clamp(data.energyLevel, 0f, data.currentMaxEnergy);
        data.speedAgeMultiplier = CalculateOldAgeMultiplier(
            data.age,
            parameters.oldAgeSpeedDecayPerSecond,
            parameters.oldAgeStartAge,
            parameters.oldAgeMinSpeedMultiplier);
        data.senseAgeMultiplier = CalculateOldAgeMultiplier(
            data.age,
            parameters.oldAgeSenseDecayPerSecond,
            parameters.oldAgeStartAge,
            parameters.oldAgeMinSenseMultiplier);
        data.desirabilityAgeMultiplier = CalculateOldAgeMultiplier(
            data.age,
            parameters.oldAgeDesirabilityDecayPerSecond,
            parameters.oldAgeStartAge,
            parameters.oldAgeMinDesirabilityMultiplier);
    }

    public static float CalculateMaturityFraction(float age, float maturityAge)
    {
        return Clamp01(age / Max(0.0001f, maturityAge));
    }

    public static float CalculateGrowthScale(float maturityFraction)
    {
        return Lerp(StartScale, 1f, SmoothStep(Clamp01(maturityFraction)));
    }

    public static float CalculateCurrentMaxEnergy(
        float maxEnergy,
        float maturityFraction,
        int creatureKind,
        CreatureLifecycleParameters parameters)
    {
        float initialPercentage = creatureKind == ECSCreatureKind.Predator
            ? parameters.initialEnergyPercentagePredator
            : parameters.initialEnergyPercentageHerbivore;
        float startMax = Max(0f, maxEnergy) * Clamp01(initialPercentage);
        return Lerp(startMax, Max(0f, maxEnergy), maturityFraction);
    }

    public static float CalculateEnergyConsumption(
        CreatureLifecycleData data,
        CreatureLifecycleParameters parameters)
    {
        if (data.currentState == CreatureStateType.Eating ||
            data.currentState == CreatureStateType.Predation)
        {
            return 0f;
        }

        float metabolicSpeed = Max(0.01f, data.baseMoveSpeed);
        float metabolicSense = Max(0.01f, data.baseSenseRadius);
        return 0.5f *
               Max(0f, data.weight) *
               metabolicSpeed *
               metabolicSpeed *
               Max(0.0001f, parameters.updateInterval) *
               metabolicSense *
               Max(0f, parameters.energyConsumptionCoefficient);
    }

    public static float CalculateOldAgeMultiplier(
        float age,
        float decayPerSecond,
        float oldAgeStart,
        float minMultiplier)
    {
        if (age <= oldAgeStart)
            return 1f;

        float elapsedOldAge = age - oldAgeStart;
        float unclamped = 1f - elapsedOldAge * Max(0f, decayPerSecond);
        return Clamp(unclamped, Clamp01(minMultiplier), 1f);
    }

    public static float Clamp01(float value)
    {
        return Clamp(value, 0f, 1f);
    }

    public static float Clamp(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return min;

        if (value <= min)
            return min;

        return value >= max ? max : value;
    }

    private static float SmoothStep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * Clamp01(t);
    }

    private static float Max(float a, float b)
    {
        return a >= b ? a : b;
    }
}
