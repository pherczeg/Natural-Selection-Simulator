using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ReproductionManager
{
    private const float MatingIntentFalloffStartUsage = 0.9f;

    BaseCreatureBehaviour creature;
    private readonly Dictionary<int, float> rejectedMateCooldowns = new Dictionary<int, float>();
    private readonly List<int> rejectedMateKeyBuffer = new List<int>();
    private float cachedMatingIntentSample01 = -1f;
    public Coroutine reproductionCoroutine { get; set; }
    public float ReproductionCooldown { get; private set; }
    public float BaseDesirability { get; private set; }
    public float Desirability { get; private set; }
    public ReproductionManager(BaseCreatureBehaviour creatureBehaviour)
    {
        creature = creatureBehaviour;
    }

    private static bool IsActiveCreature(BaseCreatureBehaviour creatureBehaviour)
    {
        return creatureBehaviour != null &&
               !creatureBehaviour.IsDespawnQueued &&
               creatureBehaviour.gameObject.activeInHierarchy;
    }

    public void UpdateReproductionCooldown(float amount)
    {
        if (amount <= 0f)
            return;

        if (ReproductionCooldown <= 0f && rejectedMateCooldowns.Count == 0)
            return;

        if (ReproductionCooldown > 0f)
        {
            ReproductionCooldown -= amount;
            if (ReproductionCooldown < 0f)
            {
                ReproductionCooldown = 0f;
            }
            else
            {
                float cooldownDuration = GetCooldownDuration();
                if (ReproductionCooldown > cooldownDuration)
                    ReproductionCooldown = cooldownDuration;
            }
        }

        UpdateRejectedMateCooldowns(amount);
    }
    public bool IsOnCooldown()
    {
        return ReproductionCooldown > 0;
    }

    public void StartReproductionCooldown()
    {
        ReproductionCooldown = GetCooldownDuration();
    }

    private float GetCooldownDuration()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return 0f;

        bool isPredator = creature is PredatorBehaviour;
        bool isFemale = creature != null && creature.Sex == CreatureSex.Female;
        return Mathf.Max(0f, config.GetReproductionCooldown(isPredator, isFemale));
    }
    public void Reproduct(BaseCreatureBehaviour mate)
    {
        if (!IsActiveCreature(creature) || !IsActiveCreature(mate) || !IsOppositeSex(mate) || !IsSameSpecies(mate)) { return; }

        var config = GameConfig.Instance;
        if (config == null)
            return;

        CreatureSpawner spawner = CreatureSpawner.Instance;
        if (spawner == null)
            return;

        bool isPredatorSpecies = creature is PredatorBehaviour;
        int offspringCount = GetOffspringCount();
        int availableSlots = spawner.GetRemainingCreatureSlots(isPredatorSpecies);
        if (availableSlots <= 0)
            return;

        if (availableSlots != int.MaxValue)
        {
            offspringCount = Mathf.Min(offspringCount, availableSlots);
        }

        int spawnedOffspring = 0;
        for (int i = 0; i < offspringCount; i++)
        {
            if (!spawner.CanSpawnCreature(isPredatorSpecies))
                break;

            var newWeight = InheritWithMutation(creature.Weight, mate.Weight, 2);
            var newMoveSpeed = InheritWithMutation(creature.MovementManager.BaseMoveSpeed, mate.MovementManager.BaseMoveSpeed, 2);
            var newSprintDuration = GetInheritedSprintDuration(mate, config);
            var newSprintFactor = GetInheritedSprintFactor(mate, config);
            var newSprintCooldown = GetInheritedSprintCooldown(mate, config);
            var newSprintCooldownSpeedFactor = GetInheritedSprintCooldownSpeedFactor(mate, config);
            var newsenseRadius = InheritWithMutation(creature.ObservationManager.BaseSenseRadius, mate.ObservationManager.BaseSenseRadius, 5);
            float newDesirability = InheritWithMutation(
                BaseDesirability,
                mate.ReproductionManager.BaseDesirability,
                config.herbivoreDesirabilityMin,
                config.herbivoreDesirabilityMax);
            float newAgility = GetInheritedAgility(mate, config);
            HerbivoreSocialStrategy offspringSocialStrategy = GetInheritedHerbivoreSocialStrategy(mate);
            float newStrength = creature is PredatorBehaviour ? GetInheritedStrength(mate, config) : 0f;
            CreatureUtilityBehaviorData offspringUtilityBehavior = GetInheritedUtilityBehaviorProfile(mate, config);
            Vector3 spawnPosition = mate.transform.position;
            SpawnCreatureRequest request = new SpawnCreatureRequest
            {
                creatureKind = isPredatorSpecies ? ECSCreatureKind.Predator : ECSCreatureKind.Herbivore,
                sex = (int)(UnityEngine.Random.value < 0.5f ? CreatureSex.Female : CreatureSex.Male),
                position = new float3(spawnPosition.x, spawnPosition.y, spawnPosition.z),
                moveSpeed = newMoveSpeed,
                weight = newWeight,
                senseRadius = newsenseRadius,
                sprintDuration = newSprintDuration,
                sprintFactor = newSprintFactor,
                sprintCooldown = newSprintCooldown,
                sprintCooldownSpeedFactor = newSprintCooldownSpeedFactor,
                desirability = newDesirability,
                agility = newAgility,
                herbivoreSocialStrategy = (int)offspringSocialStrategy,
                strength = newStrength,
                utilityKeepCurrentStateWeight = offspringUtilityBehavior.keepCurrentStateWeight,
                utilityFoodActionWeight = offspringUtilityBehavior.foodActionWeight,
                utilitySearchMateWeight = offspringUtilityBehavior.searchMateWeight,
                utilityWanderWeight = offspringUtilityBehavior.wanderWeight,
                initialAge = 0f
            };

            if (ECSMirrorBridge.TryRequestSpawnCreature(request))
                spawnedOffspring++;
        }

        if (spawnedOffspring > 0)
        {
            Statistics.Instance?.RecordReproduction(spawnedOffspring);
        }
    }

    public void SetDesirability(float desirability)
    {
        var config = GameConfig.Instance;
        if (config == null)
        {
            BaseDesirability = desirability;
            Desirability = desirability;
            return;
        }

        float clamped = Mathf.Clamp(desirability, config.herbivoreDesirabilityMin, config.herbivoreDesirabilityMax);
        BaseDesirability = clamped;
        Desirability = clamped;
    }

    public void SetAgeMultiplier(float multiplier)
    {
        var config = GameConfig.Instance;
        if (config == null)
        {
            Desirability = Mathf.Max(0f, BaseDesirability * multiplier);
            return;
        }

        float min = Mathf.Min(config.herbivoreDesirabilityMin, config.herbivoreDesirabilityMax);
        float max = Mathf.Max(config.herbivoreDesirabilityMin, config.herbivoreDesirabilityMax);
        Desirability = Mathf.Clamp(BaseDesirability * multiplier, min, max);
    }

    public bool IsRejectedMateOnCooldown(BaseCreatureBehaviour mate)
    {
        if (mate == null)
            return false;

        return rejectedMateCooldowns.TryGetValue(mate.GetInstanceID(), out float remaining) && remaining > 0f;
    }

    public void StartRejectedMateCooldown(BaseCreatureBehaviour mate)
    {
        if (mate == null)
            return;

        var config = GameConfig.Instance;
        if (config == null)
            return;

        bool isPredator = creature is PredatorBehaviour;
        rejectedMateCooldowns[mate.GetInstanceID()] = Mathf.Max(0f, config.GetRejectedMateCooldown(isPredator));
    }

    public bool TryMutualAcceptance(BaseCreatureBehaviour mate)
    {
        if (!CanMateWith(mate))
            return false;

        bool thisAcceptsMate = RollAcceptance(mate.ReproductionManager.Desirability);
        bool mateAcceptsThis = mate.ReproductionManager.RollAcceptance(Desirability);
        if (thisAcceptsMate && mateAcceptsThis)
            return true;

        StartRejectedMateCooldown(mate);
        mate.ReproductionManager.StartRejectedMateCooldown(creature);
        return false;
    }

    private int GetOffspringCount()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return 1;

        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetOffspringCount(GetGeneticsParameters(config), ref rng);
    }

    public bool IsOppositeSex(BaseCreatureBehaviour mate)
    {
        return mate != null && mate != creature && creature.Sex != mate.Sex;
    }

    public bool CanMateWith(BaseCreatureBehaviour mate)
    {
        return IsActiveCreature(creature)
               && IsActiveCreature(mate)
               && IsOppositeSex(mate)
               && IsSameSpecies(mate)
               && IsReadyToReproduction()
               && mate.ReproductionManager != null
               && mate.ReproductionManager.IsReadyToReproduction()
               && !IsRejectedMateOnCooldown(mate)
               && !mate.ReproductionManager.IsRejectedMateOnCooldown(creature);
    }

    private bool RollAcceptance(float candidateDesirability)
    {
        var config = GameConfig.Instance;
        if (config == null)
            return true;

        float populationIntentMultiplier = GetSpeciesMatingIntentMultiplier();
        if (!IsPopulationAllowedToSeekMate(populationIntentMultiplier))
            return false;

        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.RollAcceptance(
            candidateDesirability,
            populationIntentMultiplier,
            GetGeneticsParameters(config),
            ref rng);
    }

    private float GetSpeciesMatingIntentMultiplier()
    {
        if (creature == null)
            return 1f;

        CreatureSpawner spawner = CreatureSpawner.Instance;
        if (spawner == null)
            return 1f;

        bool isPredator = creature is PredatorBehaviour;
        return spawner.GetSpeciesMatingIntentMultiplier(isPredator, MatingIntentFalloffStartUsage);
    }

    private bool IsPopulationAllowedToSeekMate(float populationIntentMultiplier)
    {
        if (populationIntentMultiplier <= 0f)
            return false;

        if (populationIntentMultiplier >= 1f || creature == null)
            return true;

        if (cachedMatingIntentSample01 < 0f)
            cachedMatingIntentSample01 = GetStableInstanceSample01(creature.GetInstanceID());

        return cachedMatingIntentSample01 <= populationIntentMultiplier;
    }

    private static float GetStableInstanceSample01(int seed)
    {
        uint hash = (uint)seed;
        hash ^= 2747636419u;
        hash *= 2654435769u;
        hash ^= hash >> 16;
        hash *= 2246822519u;
        hash ^= hash >> 13;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private void UpdateRejectedMateCooldowns(float amount)
    {
        if (rejectedMateCooldowns.Count == 0 || amount <= 0f)
            return;

        rejectedMateKeyBuffer.Clear();
        foreach (var pair in rejectedMateCooldowns)
        {
            rejectedMateKeyBuffer.Add(pair.Key);
        }

        for (int i = 0; i < rejectedMateKeyBuffer.Count; i++)
        {
            int key = rejectedMateKeyBuffer[i];
            if (!rejectedMateCooldowns.TryGetValue(key, out float remaining))
                continue;

            float updated = remaining - amount;
            if (updated <= 0f)
            {
                rejectedMateCooldowns.Remove(key);
            }
            else
            {
                rejectedMateCooldowns[key] = updated;
            }
        }
    }

    private GeneticsParameters GetGeneticsParameters(GameConfig config)
    {
        return GeneticsParameters.FromConfig(config, creature is PredatorBehaviour);
    }

    private float InheritWithMutation(float trait1, float trait2, float minvalue)
    {
        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.InheritWithMutation(
            trait1,
            trait2,
            minvalue,
            GetGeneticsParameters(GameConfig.Instance),
            ref rng);
    }

    private float InheritWithMutation(float trait1, float trait2, float minvalue, float maxValue)
    {
        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.InheritWithMutation(
            trait1,
            trait2,
            minvalue,
            maxValue,
            GetGeneticsParameters(GameConfig.Instance),
            ref rng);
    }

    private float GetInheritedAgility(BaseCreatureBehaviour mate, GameConfig config)
    {
        GeneticsParameters parameters = GetGeneticsParameters(config);
        float parentAgility = creature is HerbivoreBehaviour herbivore
            ? herbivore.Agility
            : GeneticsCalculator.GetDefaultAgility(parameters);

        float mateAgility = mate is HerbivoreBehaviour mateHerbivore
            ? mateHerbivore.Agility
            : GeneticsCalculator.GetDefaultAgility(parameters);

        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetInheritedAgility(parentAgility, mateAgility, parameters, ref rng);
    }

    private float GetInheritedStrength(BaseCreatureBehaviour mate, GameConfig config)
    {
        GeneticsParameters parameters = GetGeneticsParameters(config);
        float parentStrength = creature is PredatorBehaviour predator
            ? predator.Strength
            : GeneticsCalculator.GetDefaultStrength(parameters);

        float mateStrength = mate is PredatorBehaviour matePredator
            ? matePredator.Strength
            : GeneticsCalculator.GetDefaultStrength(parameters);

        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetInheritedStrength(parentStrength, mateStrength, parameters, ref rng);
    }

    private float GetInheritedSprintDuration(BaseCreatureBehaviour mate, GameConfig config)
    {
        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetInheritedSprintDuration(
            creature.MovementManager.BaseSprintDuration,
            mate.MovementManager.BaseSprintDuration,
            GetGeneticsParameters(config),
            ref rng);
    }

    private float GetInheritedSprintFactor(BaseCreatureBehaviour mate, GameConfig config)
    {
        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetInheritedSprintFactor(
            creature.MovementManager.BaseSprintFactor,
            mate.MovementManager.BaseSprintFactor,
            GetGeneticsParameters(config),
            ref rng);
    }

    private float GetInheritedSprintCooldown(BaseCreatureBehaviour mate, GameConfig config)
    {
        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetInheritedSprintCooldown(
            creature.MovementManager.BaseSprintCooldown,
            mate.MovementManager.BaseSprintCooldown,
            GetGeneticsParameters(config),
            ref rng);
    }

    private float GetInheritedSprintCooldownSpeedFactor(BaseCreatureBehaviour mate, GameConfig config)
    {
        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.GetInheritedSprintCooldownSpeedFactor(
            creature.MovementManager.BaseSprintCooldownSpeedFactor,
            mate.MovementManager.BaseSprintCooldownSpeedFactor,
            GetGeneticsParameters(config),
            ref rng);
    }

    private bool IsSameSpecies(BaseCreatureBehaviour mate)
    {
        return mate != null && creature.GetType() == mate.GetType();
    }

    private HerbivoreSocialStrategy GetInheritedHerbivoreSocialStrategy(BaseCreatureBehaviour mate)
    {
        if (!(creature is HerbivoreBehaviour parentHerbivore) ||
            !(mate is HerbivoreBehaviour mateHerbivore))
        {
            return HerbivoreSocialStrategy.Dove;
        }

        HerbivoreSocialStrategy fatherStrategy = GetParentSocialStrategyBySex(
            parentHerbivore,
            mateHerbivore,
            CreatureSex.Male);
        HerbivoreSocialStrategy motherStrategy = GetParentSocialStrategyBySex(
            parentHerbivore,
            mateHerbivore,
            CreatureSex.Female);

        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.InheritSocialStrategy(fatherStrategy, motherStrategy, ref rng);
    }

    private CreatureUtilityBehaviorData GetInheritedUtilityBehaviorProfile(BaseCreatureBehaviour mate, GameConfig config)
    {
        CreatureUtilityBehaviorData thisProfile = creature != null
            ? UtilityBehaviorScoring.Sanitize(creature.UtilityBehaviorProfile)
            : UtilityBehaviorScoring.DefaultProfile;
        CreatureUtilityBehaviorData mateProfile = mate != null
            ? UtilityBehaviorScoring.Sanitize(mate.UtilityBehaviorProfile)
            : UtilityBehaviorScoring.DefaultProfile;

        CreatureUtilityBehaviorData fatherProfile = GetParentUtilityBehaviorProfileBySex(
            thisProfile,
            mateProfile,
            creature,
            mate,
            CreatureSex.Male);
        CreatureUtilityBehaviorData motherProfile = GetParentUtilityBehaviorProfileBySex(
            thisProfile,
            mateProfile,
            creature,
            mate,
            CreatureSex.Female);

        UnityRandomSource rng = new UnityRandomSource();
        return GeneticsCalculator.InheritUtilityBehaviorProfile(
            fatherProfile,
            motherProfile,
            GetGeneticsParameters(config),
            ref rng);
    }

    private static CreatureUtilityBehaviorData GetParentUtilityBehaviorProfileBySex(
        CreatureUtilityBehaviorData firstParentProfile,
        CreatureUtilityBehaviorData secondParentProfile,
        BaseCreatureBehaviour firstParent,
        BaseCreatureBehaviour secondParent,
        CreatureSex sex)
    {
        if (firstParent != null && firstParent.Sex == sex)
            return firstParentProfile;

        if (secondParent != null && secondParent.Sex == sex)
            return secondParentProfile;

        if (firstParent != null)
            return firstParentProfile;

        if (secondParent != null)
            return secondParentProfile;

        return UtilityBehaviorScoring.DefaultProfile;
    }

    private static HerbivoreSocialStrategy GetParentSocialStrategyBySex(
        HerbivoreBehaviour firstParent,
        HerbivoreBehaviour secondParent,
        CreatureSex sex)
    {
        if (firstParent != null && firstParent.Sex == sex)
            return firstParent.SocialStrategy;

        if (secondParent != null && secondParent.Sex == sex)
            return secondParent.SocialStrategy;

        if (firstParent != null)
            return firstParent.SocialStrategy;

        if (secondParent != null)
            return secondParent.SocialStrategy;

        return HerbivoreSocialStrategy.Dove;
    }
  
    public bool IsReadyToReproduction()
    {
        if (!IsActiveCreature(creature) ||
            creature.AgeManager == null ||
            creature.EnergyManager == null)
        {
            return false;
        }

        GameConfig config = GameConfig.Instance;
        if (config == null)
            return false;

        float populationIntentMultiplier = GetSpeciesMatingIntentMultiplier();
        if (!IsPopulationAllowedToSeekMate(populationIntentMultiplier))
            return false;

        CreatureStateType currentState = creature.CurrentStateType;
        bool isPredator = creature is PredatorBehaviour;
        return !IsOnCooldown() &&
            creature.AgeManager.Age >= config.maturityAge &&
            currentState != CreatureStateType.Reproducting &&
            currentState != CreatureStateType.Eating &&
            currentState != CreatureStateType.Predation &&
            creature.EnergyManager.EnergyLevel >= config.GetReproductionEnergyThreshold(isPredator) * creature.EnergyManager.CurrentMaxEnergy;
    }
}
