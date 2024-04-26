using System;
using UnityEngine;

public class AgeManager
{
    private BaseCreatureBehaviour creature;
    public float Age { get; private set; }

    

    public AgeManager(BaseCreatureBehaviour creature)
    {
        this.creature = creature;
    }
    public bool IsMaxAgeReached()
    {
        return Age >= GameConfig.Instance.maxAge;
    }

    public void UpdateAge(float amount)
    {
        Age += amount;
    }
}