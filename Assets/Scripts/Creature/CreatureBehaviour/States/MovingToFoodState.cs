using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToFoodState : MoveToTargetBase, ICreatureState
{
    public MovingToFoodState(CreatureBehaviour creature) : base(creature) { }
    public CreatureStateType StateType => CreatureStateType.MovingToFood;
    public void EnterState()
    {
        Debug.Log($"{creature.GetInstanceID()}Creature is moving to food.");
    }

    public void UpdateState()
    {
        if (target != null)
        {
            if (ReachedTarget())
            {
                var foodComponent = target.GetComponent<Food>();
                creature.stateMachine.TransitionToEating(foodComponent);
            }
            else
            { 
                MoveTowardsTarget();
            }
        }
        else
        {
            creature.stateMachine.TransitionToIdle();
        }
    }

    public void ExitState()
    {
        Debug.Log($"{creature.GetInstanceID()}Creature stops moving to food.");
    }
}
