using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class ReproductionManager
{
    CreatureBehaviour creature;
    public Coroutine reproductionCoroutine { get; set; }
    public float ReproductionCooldown { get; private set; }
    public ReproductionManager(CreatureBehaviour creatureBehaviour)
    {
        creature = creatureBehaviour;
    }
    public void UpdateReproductionCooldown(float amount)
    {
        ReproductionCooldown -= amount;
        ReproductionCooldown = Math.Clamp(ReproductionCooldown, 0, GameConfig.Instance.reproductionCooldown);
    }
    public bool IsOnCooldown()
    {
        return ReproductionCooldown > 0;
    }

    public void StartReproductionCooldown()
    {
        ReproductionCooldown = GameConfig.Instance.reproductionCooldown;
    }
    public void Reproduct(CreatureBehaviour mate)
    {
        if (mate == null) { return; }
        CreatureBehaviour offspringBehavior = CreatureSpawner.Instance.SpawnCreature(mate.transform.position);
        var newWeight = InheritWithMutation(creature.Weight, mate.Weight, 2);
        var newMoveSpeed = InheritWithMutation(creature.MovementManager.MoveSpeed, mate.MovementManager.MoveSpeed, 2);
        var newsenseRadius = InheritWithMutation(creature.ObservationManager.SenseRadius, mate.ObservationManager.SenseRadius, 5);
        offspringBehavior.Initialize(newMoveSpeed,newWeight,newsenseRadius);
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
  
    public bool IsReadyToReproduction()
    {
        return !IsOnCooldown() && 
                creature.AgeManager.Age >= GameConfig.Instance.reproductionAge && 
                creature.stateMachine.CurrentState.StateType != CreatureStateType.Reproducting &&
                creature.stateMachine.CurrentState.StateType != CreatureStateType.Eating &&
                creature.EnergyManager.EnergyLevel  >= GameConfig.Instance.reproductionEnergyThreshold* GameConfig.Instance.maxEnergy;
    }
}
