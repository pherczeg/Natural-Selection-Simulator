using UnityEngine;

public enum HerbivoreSocialStrategy
{
    Dove = 0,
    Hawk = 1
}

public static class HerbivoreSocialDynamics
{
    public const float HawkFightFoodRetentionFactor = 0.75f;
    private const float HawkFightLoserEnergyLossFraction = 0.5f;
    private const float HawkFightDeathChance = 0.1f;

    public static BaseCreatureBehaviour ResolveHawkFight(BaseCreatureBehaviour first, BaseCreatureBehaviour second)
    {
        if (first == null || second == null)
            return null;

        float totalWeight = Mathf.Max(0.0001f, first.Weight + second.Weight);
        float firstWinChance = Mathf.Clamp01(first.Weight / totalWeight);

        BaseCreatureBehaviour winner = Random.value < firstWinChance ? first : second;
        BaseCreatureBehaviour loser = winner == first ? second : first;

        if (loser?.EnergyManager != null)
        {
            float halfCurrentEnergy = Mathf.Max(0f, loser.EnergyManager.EnergyLevel * HawkFightLoserEnergyLossFraction);
            loser.EnergyManager.ConsumeEnergy(halfCurrentEnergy);
        }

        bool winnerDied = TryApplyBattleDeath(winner);
        TryApplyBattleDeath(loser);

        return winnerDied ? null : winner;
    }

    private static bool TryApplyBattleDeath(BaseCreatureBehaviour creature)
    {
        if (creature == null || creature.IsDespawnQueued)
            return false;

        if (Random.value > HawkFightDeathChance)
            return false;

        creature.Despawn(CreatureDeathReason.Unknown);
        return true;
    }
}