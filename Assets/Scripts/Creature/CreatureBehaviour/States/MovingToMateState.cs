using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MovingToMateState : MoveToTargetBase, ICreatureState
{
    public MovingToMateState(BaseCreatureBehaviour creature) : base(creature, CreatureStateType.MovingToMate) { }
    BaseCreatureBehaviour targetCreature;
    public override void EnterState()
    {
        base.EnterState();
    }
    public override void SetTarget(GameObject targetCreature)
    {
        base.SetTarget(targetCreature);
        this.targetCreature = target.transform.GetComponent<BaseCreatureBehaviour>();
    }
    public override void UpdateState()
    {
        if (target != null)
        {
            if (ReachedTarget() && targetCreature.ReproductionManager.IsReadyToReproduction())
            {
                //creature.stateMachine.TransitionToWandering();
                creature.stateMachine.TransitionToReproductionState(targetCreature, true);
                targetCreature.stateMachine.TransitionToReproductionState(creature, false);
            }
            else if (!targetCreature.ReproductionManager.IsReadyToReproduction())
            {
                creature.stateMachine.TransitionToIdle();
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
