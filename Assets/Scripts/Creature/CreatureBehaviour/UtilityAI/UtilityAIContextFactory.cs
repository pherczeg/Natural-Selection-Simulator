public static class UtilityAIContextFactory
{
    public const string MonoContextSource = "Mono";
    public const string ECSMirrorContextSource = "ECS Mirror";
    public const string ManualContextSource = "Manual";
    public const string EmptyContextSource = "Empty";

    public static UtilityAIContext FromCreature(BaseCreatureBehaviour creature)
    {
        TryCreateContext(creature, out UtilityAIContext context, out _);
        return context;
    }

    public static bool TryCreateContext(
        BaseCreatureBehaviour creature,
        out UtilityAIContext context,
        out string source)
    {
        if (creature == null)
        {
            context = UtilityAIContext.Empty;
            source = EmptyContextSource;
            return false;
        }

        GameConfig config = GameConfig.Instance;
        if (config != null &&
            config.useEcsAIContext &&
            ECSMirrorBridge.TryGetUtilityAIContext(creature, out context))
        {
            source = ECSMirrorContextSource;
            return true;
        }

        context = FromMonoCreature(creature);
        source = MonoContextSource;
        return true;
    }

    public static UtilityAIContext FromMonoCreature(BaseCreatureBehaviour creature)
    {
        if (creature == null)
            return UtilityAIContext.Empty;

        return new UtilityAIContext(
            GetEnergyPercent(creature),
            creature.AgeManager != null && creature.AgeManager.MaturityFraction >= 1f,
            creature.ReproductionManager != null && creature.ReproductionManager.IsReadyToReproduction(),
            CreatureActionStatusAdapter.GetLegacyStateType(creature),
            HasObservation(creature, ObservationType.Food),
            HasObservation(creature, ObservationType.MatingCreature),
            HasObservation(creature, ObservationType.FoodCreature),
            creature is HerbivoreBehaviour herbivore && herbivore.IsThreatened);
    }

    public static UtilityAIContext FromECSMirrorData(CreatureAIContextData data)
    {
        return FromECSMirrorData(data, default);
    }

    public static UtilityAIContext FromECSMirrorData(
        CreatureAIContextData data,
        CreatureObservationResultData observationResult)
    {
        return new UtilityAIContext(
            data.energyPercent,
            data.isMature,
            data.isReproductionReady,
            data.currentState,
            data.hasKnownFood || observationResult.closestFoodInstanceId != 0,
            data.hasKnownMate || observationResult.closestMateInstanceId != 0,
            data.hasKnownPrey || observationResult.closestPreyInstanceId != 0,
            data.isThreatened);
    }

    private static float GetEnergyPercent(BaseCreatureBehaviour creature)
    {
        if (creature.EnergyManager == null)
            return 0f;

        float currentMaxEnergy = creature.EnergyManager.CurrentMaxEnergy;
        if (currentMaxEnergy <= 0f)
            return 0f;

        return UtilityAIScoreRules.Clamp01(creature.EnergyManager.EnergyLevel / currentMaxEnergy);
    }

    private static bool HasObservation(BaseCreatureBehaviour creature, ObservationType type)
    {
        var observations = creature.ObservationManager?.Observations;
        if (observations == null)
            return false;

        for (int i = 0; i < observations.Count; i++)
        {
            ObservationData observation = observations[i];
            if (observation.type == type && observation.observedObject != null && observation.observedObject.activeInHierarchy)
                return true;
        }

        return false;
    }
}
