using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToMateState : MoveToTargetBase, ICreatureState
{
    public MovingToMateState(CreatureBehaviour creature) : base(creature, CreatureStateType.MovingToMate) { }
    CreatureBehaviour targetCreature;
    public override void EnterState()
    {
        base.EnterState();
    }
    public override void SetTarget(GameObject targetFood)
    {
        base.SetTarget(targetFood);
        targetCreature = target.transform.parent.GetComponent<CreatureBehaviour>();
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
