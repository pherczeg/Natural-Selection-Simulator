using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReproductionManager
{
    BaseCreatureBehaviour creature;
    private readonly Dictionary<int, float> rejectedMateCooldowns = new Dictionary<int, float>();
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
        return creatureBehaviour != null && creatureBehaviour.gameObject.activeInHierarchy;
    }

    public void UpdateReproductionCooldown(float amount)
    {
        ReproductionCooldown -= amount;
        ReproductionCooldown = Math.Clamp(ReproductionCooldown, 0, GetCooldownDuration());
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

        if (creature.Sex == CreatureSex.Female)
            return config.femaleReproductionCooldown;

        return config.maleReproductionCooldown;
    }
    public void Reproduct(BaseCreatureBehaviour mate)
    {
        if (!IsActiveCreature(creature) || !IsActiveCreature(mate) || !IsOppositeSex(mate) || !IsSameSpecies(mate)) { return; }

        int offspringCount = GetOffspringCount();
        var config = GameConfig.Instance;
        GameObject offspringPrefab = GetOffspringPrefab();
        if (offspringPrefab == null)
            return;

        int spawnedOffspring = 0;
        for (int i = 0; i < offspringCount; i++)
        {
            BaseCreatureBehaviour offspringBehavior = CreatureSpawner.Instance.SpawnCreature(mate.transform.position, offspringPrefab);
            offspringBehavior.SetSex(UnityEngine.Random.value < 0.5f ? CreatureSex.Female : CreatureSex.Male);
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
            offspringBehavior.Initialize(newMoveSpeed,newWeight,newsenseRadius);
            offspringBehavior.MovementManager?.SetSprintProfile(newSprintDuration, newSprintFactor, newSprintCooldown, newSprintCooldownSpeedFactor);
            offspringBehavior.ReproductionManager?.SetDesirability(newDesirability);
            if (offspringBehavior is HerbivoreBehaviour offspringHerbivore)
            {
                offspringHerbivore.SetAgility(newAgility);
            }
            else if (offspringBehavior is PredatorBehaviour offspringPredator)
            {
                offspringPredator.SetStrength(GetInheritedStrength(mate, config));
            }

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

        rejectedMateCooldowns[mate.GetInstanceID()] = Mathf.Max(0f, config.rejectedMateCooldown);
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

        int minCount = Math.Min(config.minOffspringPerReproduction, config.maxOffspringPerReproduction);
        int maxCount = Math.Max(config.minOffspringPerReproduction, config.maxOffspringPerReproduction);

        float sampled = SampleNormalDistribution(config.offspringCountMean, Math.Max(0.0001f, config.offspringCountStdDev));
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

        float normalized = Mathf.InverseLerp(config.herbivoreDesirabilityMin, config.herbivoreDesirabilityMax, candidateDesirability);
        float acceptanceChance = Mathf.Lerp(config.minMateAcceptanceChance, config.maxMateAcceptanceChance, normalized);
        return UnityEngine.Random.value <= acceptanceChance;
    }

    private void UpdateRejectedMateCooldowns(float amount)
    {
        if (rejectedMateCooldowns.Count == 0 || amount <= 0f)
            return;

        List<int> keys = new List<int>(rejectedMateCooldowns.Keys);
        foreach (int key in keys)
        {
            float updated = rejectedMateCooldowns[key] - amount;
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
  
    public bool IsReadyToReproduction()
    {
        if (!IsActiveCreature(creature) ||
            creature.stateMachine?.CurrentState == null ||
            creature.AgeManager == null ||
            creature.EnergyManager == null)
        {
            return false;
        }

        CreatureStateType currentState = creature.stateMachine.CurrentState.StateType;
        return !IsOnCooldown() && 
                creature.AgeManager.Age >= GameConfig.Instance.maturityAge && 
                currentState != CreatureStateType.Reproducting &&
                currentState != CreatureStateType.Eating &&
                currentState != CreatureStateType.Predation &&
                currentState != CreatureStateType.MovingToFood &&
                currentState != CreatureStateType.SearchingForFood &&
                creature.EnergyManager.EnergyLevel >= GameConfig.Instance.reproductionEnergyThreshold * creature.EnergyManager.CurrentMaxEnergy;
    }
}
