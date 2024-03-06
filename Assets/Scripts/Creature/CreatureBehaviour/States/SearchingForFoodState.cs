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
        Debug.Log("Creature starts searching for food.");
        SearchForFood();
    }

    public void UpdateState()
    {
        if (wanderTarget != Vector3.zero)
        {
            if (creature.MovementManager.IsTargetReached(wanderTarget))
            {
                SearchForFood();
                wanderTarget = Vector3.zero;
            }
            creature.MovementManager.MoveTowards(wanderTarget);
        }
        SearchForFood();
    }

    public void ExitState()
    {
        wanderTarget = Vector3.zero;
        Debug.Log("Creature stops searching for food.");
    }

    private void SearchForFood()
    {
        float closestFoodDistance = float.MaxValue;
        Vector3 closestFoodPosition = Vector3.zero;
        GameObject closestFood = null;

        foreach (var observation in creature.ObservationManager.Observations)
        {
            if (observation.Value.type == ObservationType.Food && observation.Value.distance < closestFoodDistance)
            {
                if (observation.Value.observedObject == null)
                {
                    continue;
                }
                if (creature.EatingManager.IsFoodBlacklisted(observation.Value.observedObject.GetComponent<Food>())) // If the food is blacklisted we ignore it
                {
                    continue;
                }
                closestFoodDistance = observation.Value.distance;
                closestFoodPosition = observation.Value.observedObject.transform.position;
                closestFood = observation.Value.observedObject;
            }
        }

        if (closestFoodDistance < float.MaxValue)
        {
            creature.stateMachine.TransitionToMovingToFood(closestFood);
            return; // exit after making a decision
        }
        else if (wanderTarget.Equals(Vector3.zero))
        {
            wanderTarget = creature.MovementManager.Wander();
        }
    }
    
}
