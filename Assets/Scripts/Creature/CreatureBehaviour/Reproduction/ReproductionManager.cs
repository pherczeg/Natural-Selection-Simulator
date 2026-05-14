using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ReproductionManager
{
    private const float SocialStrategyFatherInheritanceChance = 0.475f;
    private const float SocialStrategyMotherInheritanceChance = 0.475f;

    BaseCreatureBehaviour creature;
    private readonly Dictionary<int, float> rejectedMateCooldowns = new Dictionary<int, float>();
    private readonly List<int> rejectedMateKeyBuffer = new List<int>();
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

        GameObject offspringPrefab = GetOffspringPrefab();
        if (offspringPrefab == null)
            return;

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

            bool spawnAccepted = ECSMirrorBridge.TryRequestSpawnCreature(request);
            if (!spawnAccepted)
            {
                BaseCreatureBehaviour offspringBehavior = spawner.SpawnCreature(spawnPosition, offspringPrefab);
                if (offspringBehavior == null)
                    break;

                InitializeOffspringFromRequest(offspringBehavior, request);
                spawnAccepted = true;
            }

            if (spawnAccepted)
            {
                spawnedOffspring++;
            }
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

        bool isPredator = creature is PredatorBehaviour;
        int minCount = Math.Min(
            config.GetMinOffspringPerReproduction(isPredator),
            config.GetMaxOffspringPerReproduction(isPredator));
        int maxCount = Math.Max(
            config.GetMinOffspringPerReproduction(isPredator),
            config.GetMaxOffspringPerReproduction(isPredator));

        float sampled = SampleNormalDistribution(
            config.GetOffspringCountMean(isPredator),
            Math.Max(0.0001f, config.GetOffspringCountStdDev(isPredator)));
        int rounded = Mathf.RoundToInt(sampled);
        return Mathf.Clamp(rounded, minCount, maxCount);
    }

    private float SampleNormalDistribution(float mean, float stdDev)
    {
        float u1 = 1f - UnityEngine.Random.value;
        float u2 = 1f - UnityEngine.Random.value;
        float standardNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return mean + stdDev * standardNormal;
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

        bool isPredator = creature is PredatorBehaviour;
        float normalized = Mathf.InverseLerp(config.herbivoreDesirabilityMin, config.herbivoreDesirabilityMax, candidateDesirability);
        float acceptanceChance = Mathf.Lerp(
            config.GetMinMateAcceptanceChance(isPredator),
            config.GetMaxMateAcceptanceChance(isPredator),
            normalized);
        return UnityEngine.Random.value <= acceptanceChance;
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

    private float InheritWithMutation(float trait1, float trait2, float minvalue)
    {
        float inheritedTrait = UnityEngine.Random.value < 0.5f ? trait1 : trait2;

        float mutationChance = GameConfig.Instance.mutationChance;
        float mutationRate = GameConfig.Instance.mutationRate;
        if (UnityEngine.Random.value < mutationChance)
        {
            float mutationAmount = UnityEngine.Random.Range((-1) * mutationRate, mutationRate);
            inheritedTrait += mutationAmount;
        }

        if (inheritedTrait < minvalue)
        {
            inheritedTrait += (minvalue - inheritedTrait);
        }

        return inheritedTrait;
    }

    private float InheritWithMutation(float trait1, float trait2, float minvalue, float maxValue)
    {
        float inheritedTrait = InheritWithMutation(trait1, trait2, minvalue);
        return Mathf.Clamp(inheritedTrait, minvalue, maxValue);
    }

    private float GetInheritedAgility(BaseCreatureBehaviour mate, GameConfig config)
    {
        float parentAgility = creature is HerbivoreBehaviour herbivore
            ? herbivore.Agility
            : Mathf.Lerp(config.herbivoreAgilityMin, config.herbivoreAgilityMax, 0.5f);

        float mateAgility = mate is HerbivoreBehaviour mateHerbivore
            ? mateHerbivore.Agility
            : Mathf.Lerp(config.herbivoreAgilityMin, config.herbivoreAgilityMax, 0.5f);

        return InheritWithMutation(
            parentAgility,
            mateAgility,
            config.herbivoreAgilityMin,
            config.herbivoreAgilityMax);
    }

    private float GetInheritedStrength(BaseCreatureBehaviour mate, GameConfig config)
    {
        float parentStrength = creature is PredatorBehaviour predator
            ? predator.Strength
            : Mathf.Lerp(config.predatorStrengthMin, config.predatorStrengthMax, 0.5f);

        float mateStrength = mate is PredatorBehaviour matePredator
            ? matePredator.Strength
            : Mathf.Lerp(config.predatorStrengthMin, config.predatorStrengthMax, 0.5f);

        return InheritWithMutation(
            parentStrength,
            mateStrength,
            config.predatorStrengthMin,
            config.predatorStrengthMax);
    }

    private float GetInheritedSprintDuration(BaseCreatureBehaviour mate, GameConfig config)
    {
        if (creature is PredatorBehaviour)
        {
            return InheritWithMutation(
                creature.MovementManager.BaseSprintDuration,
                mate.MovementManager.BaseSprintDuration,
                config.predatorSprintDurationMin,
                config.predatorSprintDurationMax);
        }

        return InheritWithMutation(
            creature.MovementManager.BaseSprintDuration,
            mate.MovementManager.BaseSprintDuration,
            config.herbivoreSprintDurationMin,
            config.herbivoreSprintDurationMax);
    }

    private float GetInheritedSprintFactor(BaseCreatureBehaviour mate, GameConfig config)
    {
        if (creature is PredatorBehaviour)
        {
            return InheritWithMutation(
                creature.MovementManager.BaseSprintFactor,
                mate.MovementManager.BaseSprintFactor,
                config.predatorSprintFactorMin,
                config.predatorSprintFactorMax);
        }

        return InheritWithMutation(
            creature.MovementManager.BaseSprintFactor,
            mate.MovementManager.BaseSprintFactor,
            config.herbivoreSprintFactorMin,
            config.herbivoreSprintFactorMax);
    }

    private float GetInheritedSprintCooldown(BaseCreatureBehaviour mate, GameConfig config)
    {
        if (creature is PredatorBehaviour)
        {
            return InheritWithMutation(
                creature.MovementManager.BaseSprintCooldown,
                mate.MovementManager.BaseSprintCooldown,
                config.predatorSprintCooldownMin,
                config.predatorSprintCooldownMax);
        }

        return InheritWithMutation(
            creature.MovementManager.BaseSprintCooldown,
            mate.MovementManager.BaseSprintCooldown,
            config.herbivoreSprintCooldownMin,
            config.herbivoreSprintCooldownMax);
    }

    private float GetInheritedSprintCooldownSpeedFactor(BaseCreatureBehaviour mate, GameConfig config)
    {
        if (creature is PredatorBehaviour)
        {
            return InheritWithMutation(
                creature.MovementManager.BaseSprintCooldownSpeedFactor,
                mate.MovementManager.BaseSprintCooldownSpeedFactor,
                config.predatorSprintCooldownSpeedFactorMin,
                config.predatorSprintCooldownSpeedFactorMax);
        }

        return InheritWithMutation(
            creature.MovementManager.BaseSprintCooldownSpeedFactor,
            mate.MovementManager.BaseSprintCooldownSpeedFactor,
            config.herbivoreSprintCooldownSpeedFactorMin,
            config.herbivoreSprintCooldownSpeedFactorMax);
    }

    private bool IsSameSpecies(BaseCreatureBehaviour mate)
    {
        return mate != null && creature.GetType() == mate.GetType();
    }

    private GameObject GetOffspringPrefab()
    {
        if (CreatureSpawner.Instance == null)
            return null;

        if (creature is PredatorBehaviour)
            return CreatureSpawner.Instance.predatorPrefab;

        return CreatureSpawner.Instance.herbivorPrefab;
    }

    private static void InitializeOffspringFromRequest(
        BaseCreatureBehaviour offspringBehavior,
        SpawnCreatureRequest request)
    {
        if (offspringBehavior == null)
            return;

        CreatureSex sex = request.sex == (int)CreatureSex.Male
            ? CreatureSex.Male
            : CreatureSex.Female;
        offspringBehavior.SetSex(sex);
        offspringBehavior.Initialize(request.moveSpeed, request.weight, request.senseRadius);
        offspringBehavior.MovementManager?.SetSprintProfile(
            request.sprintDuration,
            request.sprintFactor,
            request.sprintCooldown,
            request.sprintCooldownSpeedFactor);
        offspringBehavior.ReproductionManager?.SetDesirability(request.desirability);
        offspringBehavior.SetUtilityBehaviorProfile(new CreatureUtilityBehaviorData
        {
            keepCurrentStateWeight = request.utilityKeepCurrentStateWeight,
            foodActionWeight = request.utilityFoodActionWeight,
            searchMateWeight = request.utilitySearchMateWeight,
            wanderWeight = request.utilityWanderWeight
        });

        if (offspringBehavior is HerbivoreBehaviour offspringHerbivore)
        {
            offspringHerbivore.SetAgility(request.agility);
            offspringHerbivore.SetSocialStrategy((HerbivoreSocialStrategy)request.herbivoreSocialStrategy);
        }
        else if (offspringBehavior is PredatorBehaviour offspringPredator)
        {
            offspringPredator.SetStrength(request.strength);
        }
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

        float roll = UnityEngine.Random.value;
        if (roll < SocialStrategyFatherInheritanceChance)
            return fatherStrategy;

        if (roll < SocialStrategyFatherInheritanceChance + SocialStrategyMotherInheritanceChance)
            return motherStrategy;

        return UnityEngine.Random.value < 0.5f
            ? HerbivoreSocialStrategy.Hawk
            : HerbivoreSocialStrategy.Dove;
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

        return UtilityBehaviorGenetics.InheritProfile(fatherProfile, motherProfile, config);
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
