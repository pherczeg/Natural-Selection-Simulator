using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToFoodState : ICreatureState
{
    private readonly CreatureBehaviour creature;
    private GameObject targetFood;

    public MovingToFoodState(CreatureBehaviour creature)
    {
        this.creature = creature;
    }
    public CreatureStateType StateType => CreatureStateType.MovingToFood;
    public void EnterState()
    {
        Debug.Log($"Creature is moving to food.");
    }

    public void UpdateState()
    {
        if (targetFood != null)
        {
            if (ReachedFood())
            {
                var foodComponent = targetFood.GetComponent<Food>();
                creature.stateMachine.TransitionToEating(foodComponent);
            }
            else
            { 
                MoveTowardsFood();
            }
        }
        else
        {
            creature.stateMachine.TransitionToIdle();
        }
    }

    public void ExitState()
    {
        Debug.Log($"Creature stops moving to food.");
    }
    public void SetTarget(GameObject targetFood)
    {
        this.targetFood = targetFood;
    }
    private void MoveTowardsFood()
    {
        creature.MovementManager.MoveTowards(targetFood.transform.position);
    }

    private bool ReachedFood()
    {
        return Vector3.Distance(creature.transform.position, targetFood.transform.position) < 1.0f;
    }
}
