using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToFoodState : MoveToTargetBase
{
    public MovingToFoodState(BaseCreatureBehaviour creature) : base(creature, CreatureStateType.MovingToFood) { }
    public override void EnterState()
    {
        base.EnterState();
    }

    public override void UpdateState()
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

    public override void ExitState()
    {
        base.ExitState();
    }
}
