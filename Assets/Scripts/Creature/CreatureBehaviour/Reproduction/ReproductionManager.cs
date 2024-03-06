using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReproductionManager
{
    CreatureBehaviour creature;
    float InheritWithMutation(float trait1, float trait2, float minvalue)
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
}
