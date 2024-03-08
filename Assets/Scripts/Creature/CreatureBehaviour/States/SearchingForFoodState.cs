using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchingForFoodState : CreatureStateBase
{
    private Vector3 wanderTarget;
    public SearchingForFoodState(CreatureBehaviour creature) : base(creature, CreatureStateType.SearchingForFood)
    {
        this.creature = creature;
    }
    public override void EnterState()
    {
        base.EnterState();
        SearchForFood();
    }

    public override void UpdateState()
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

    public override void ExitState()
    {
        base.ExitState();
        wanderTarget = Vector3.zero;
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
