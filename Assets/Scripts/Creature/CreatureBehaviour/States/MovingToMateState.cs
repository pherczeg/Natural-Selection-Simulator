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
                //creature.stateMachine.TransitionToWandering();
                var targetCreature = target.transform.parent.GetComponent<CreatureBehaviour>();
                creature.stateMachine.TransitionToReproductionState(targetCreature);
                targetCreature.stateMachine.TransitionToReproductionState(creature);
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
