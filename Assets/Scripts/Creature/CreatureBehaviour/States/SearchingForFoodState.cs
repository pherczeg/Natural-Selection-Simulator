using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchingForFoodState : ICreatureState
{
    private readonly CreatureBehaviour creature;

    private Vector3 wanderTarget;
    public SearchingForFoodState(CreatureBehaviour creature)
    {
        this.creature = creature;
    }
    public CreatureStateType StateType => CreatureStateType.SearchingForFood;
    public void EnterState()
    {
        Debug.Log($"{creature.GetInstanceID()}Creature starts searching for food.");
        SearchForFood();
    }

    public void UpdateState()
    {
        //if (wanderTarget != Vector3.zero)
        //{
        if (creature.MovementManager.IsTargetReached(wanderTarget))
        {
            SearchForFood();
        }
        else
        {
            creature.MovementManager.MoveTowards(wanderTarget);
        }
        //}
        //SearchForFood();
    }

    public void ExitState()
    {
        wanderTarget = Vector3.zero;
        Debug.Log($"{creature.GetInstanceID()}Creature stops searching for food.");
    }

    private void SearchForFood()
    {
        Food closestFood = FindClosestNonBlacklistedFood();

        if (closestFood != null)
        {
            creature.stateMachine.TransitionToMovingToFood(closestFood.gameObject);
        }
        else
        {
            EnsureWanderTarget();
        }
    }

    private Food FindClosestNonBlacklistedFood()
    {
        float closestFoodDistance = float.MaxValue;
        Food closestFood = null;

        foreach (var observation in creature.ObservationManager.Observations)
        {
            if (observation.Value.observedObject == null)
            {
                continue;
            }
            Food observedFood = observation.Value.observedObject?.GetComponent<Food>();
            if (observation.Value.type == ObservationType.Food && observedFood && !creature.EatingManager.IsFoodBlacklisted(observedFood))
            {
                float distance = observation.Value.distance;
                if (distance < closestFoodDistance)
                {
                    closestFoodDistance = distance;
                    closestFood = observedFood;
                }
            }
        }

        return closestFood;
    }

    private void EnsureWanderTarget()
    {
        wanderTarget = creature.MovementManager.Wander();
    }
}
