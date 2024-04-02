using System;
using System.Collections.Generic;
using UnityEngine;
public class EatingManager
{
    private Dictionary<Food, float> blacklistedFoods;
    private CreatureBehaviour creature;
    public Coroutine eatingCoroutine { get; set; }
    public Food foodTarget { get; set; }
    public EatingManager(CreatureBehaviour creatureBehaviour)
    {
        this.creature = creatureBehaviour;
        blacklistedFoods = new Dictionary<Food, float>();
    }
    public bool IsFoodBlacklisted(Food food)
    {
        if (blacklistedFoods.TryGetValue(food, out float blacklistTime))
        {
            if (Time.time <= blacklistTime)
            {
                return true;  
            }
            else
            {
                blacklistedFoods.Remove(food);
            }
        }
        return false;
    }
    public void BlacklistFood(Food food)
    {
        if (blacklistedFoods.ContainsKey(food))
        {
            blacklistedFoods[food] = Time.time + GameConfig.Instance.blackListDuration;
        }
        else
        {
            blacklistedFoods.Add(food, Time.time + GameConfig.Instance.blackListDuration);
        }
    }
    public void InterruptEating()
    {
        if (eatingCoroutine != null)
        {
            if (foodTarget != null)
            {
                creature.EatingManager.BlacklistFood(foodTarget);
                foodTarget.StopEating();
                //isEating = false;
                creature.stateMachine.TransitionToWandering();
                creature.DEBUG_string += "eating interrubted" + Time.realtimeSinceStartup + "\n";
            }
            else 
            {
                Debug.Log("");
            }
        }
    }
}
