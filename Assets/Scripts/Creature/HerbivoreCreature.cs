using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HerbivoreCreature : Creature
{


    private Dictionary<Food, float> blacklistedFoods = new Dictionary<Food, float>();
    // További növényevő-specifikus változók...

    protected override void Start()
    {
        base.Start();
        StartCoroutine(RemoveExpiredBlacklistedFoods());
    }

    //protected override void Update()
    //{
    //    base.Update();
    //    // Növényevő-specifikus viselkedések...
    //    switch (currentState)
    //    {
    //        case CreatureState.Wandering:
    //            Wander();
    //            break;
    //            // További esetek...
    //    }
    //}

    private void Wander()
    {
        // Implementáld a növényevő vándorlási logikáját
    }
    private IEnumerator RemoveExpiredBlacklistedFoods()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f); // wait for 10 seconds

            List<Food> foodsToRemove = new List<Food>();

            foreach (var entry in blacklistedFoods)
            {
                if (entry.Value <= Time.time)
                {
                    foodsToRemove.Add(entry.Key);
                }
            }

            foreach (Food food in foodsToRemove)
            {
                blacklistedFoods.Remove(food);
            }
        }
    }
}
