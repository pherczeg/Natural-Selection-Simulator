public static class UtilityAIContextFactory
{
    public static UtilityAIContext FromCreature(BaseCreatureBehaviour creature)
    {
        if (creature == null)
            return UtilityAIContext.Empty;

        return new UtilityAIContext(
            GetEnergyPercent(creature),
            creature.AgeManager != null && creature.AgeManager.MaturityFraction >= 1f,
            creature.ReproductionManager != null && creature.ReproductionManager.IsReadyToReproduction(),
            creature.CurrentStateType,
            HasObservation(creature, ObservationType.Food),
            HasObservation(creature, ObservationType.MatingCreature),
            HasObservation(creature, ObservationType.FoodCreature),
            creature is HerbivoreBehaviour herbivore && herbivore.IsThreatened);
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
