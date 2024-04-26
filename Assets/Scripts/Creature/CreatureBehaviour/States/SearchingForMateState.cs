using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class SearchingForMateState : CreatureSearchingStateBase
{
    public SearchingForMateState(BaseCreatureBehaviour creature) : base(creature, CreatureStateType.SearchingForMate) { }
    public override void EnterState()
    {
        base.EnterState();
        SearchForMate();
    }

    private void SearchForMate() 
    {
        SearchForTarget(ObservationType.MatingCreature, (target) =>
        {
            creature.stateMachine.TransitionToMovingToMate(target);
        },
        target => {
            var creatureBehaviour = target.transform.GetComponent<BaseCreatureBehaviour>();
            return creatureBehaviour != null && creatureBehaviour.ReproductionManager.IsReadyToReproduction();
        });

    }
    public override void UpdateState()
    {
        if (creature.MovementManager.IsTargetReached(wanderTarget))
        {
            SearchForMate();
        }
        else
        {
            creature.MovementManager.MoveTowards(wanderTarget);
        }
    }

    public override void ExitState()
    {
        base.ExitState();
        wanderTarget = Vector3.zero;
    }
    protected override void OnTargetReached()
    {
        SearchForMate();
    }
}
