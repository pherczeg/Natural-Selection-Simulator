using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class ReproductionManager
{
    CreatureBehaviour creature;
    public float ReproductionCooldown { get; private set; }
    public ReproductionManager(CreatureBehaviour creatureBehaviour)
    {
        creature = creatureBehaviour;
    }
    public void UpdateReproductionCooldown(float amount)
    {
        ReproductionCooldown -= amount;
        ReproductionCooldown = Math.Clamp(ReproductionCooldown, 0, creature.config.reproductionCooldown);
    }
    public bool IsOnCooldown()
    {
        return ReproductionCooldown > 0;
    }

    public void StartReproductionCooldown()
    {
        ReproductionCooldown = creature.config.reproductionCooldown;
    }


    private float InheritWithMutation(float trait1, float trait2, float minvalue)
    {
        float inheritedTrait = UnityEngine.Random.value < 0.5f ? trait1 : trait2;

        float mutationChance = creature.config.mutationChance;
        float mutationRate = creature.config.mutationRate;
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
        return !IsOnCooldown() && creature.AgeManager.Age >= creature.config.reproductionAge;
    }
}
