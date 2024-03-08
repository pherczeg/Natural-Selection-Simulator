using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToMateState : MoveToTargetBase, ICreatureState
{
    public MovingToMateState(CreatureBehaviour creature) : base(creature, CreatureStateType.MovingToMate) { }
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
                var creature = target.GetComponent<CreatureBehaviour>();
                creature.stateMachine.TransitionToReproductionState(creature.gameObject);
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
