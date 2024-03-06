using System;
using System.Collections.Generic;
using UnityEngine;
public class EatingManager
{
    private Dictionary<Food, float> blacklistedFoods = new Dictionary<Food, float>();
    private CreatureBehaviour creature;
    public Coroutine eatingCoroutine { get; set; }
    public Food foodTarget { get; set; }
    public EatingManager(CreatureBehaviour creatureBehaviour)
    {
        this.creature = creatureBehaviour;
    }
    public bool IsFoodBlacklisted(Food food)
    {
        if (blacklistedFoods.TryGetValue(food, out float blacklistTime))
        {
            if (Time.time <= blacklistTime)
            {
                return true;  // Food is still within the blacklist duration
            }
            else
            {
                // Remove the food from the blacklist as its duration has expired
                blacklistedFoods.Remove(food);
            }
        }
        return false;
    }
    public void BlacklistFood(Food food)
    {
        if (blacklistedFoods.ContainsKey(food))
        {
            blacklistedFoods[food] = Time.time + creature.config.blackListDuration;
        }
        else
        {
            blacklistedFoods.Add(food, Time.time + creature.config.blackListDuration);
        }
    }
    public void InterruptEating()
    {
        if (eatingCoroutine != null)
        {
            creature.EatingManager.BlacklistFood(foodTarget);
            foodTarget.StopEating();
            //isEating = false;
            creature.stateMachine.TransitionToWandering();
        }
    }
}
