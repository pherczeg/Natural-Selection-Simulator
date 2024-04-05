using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class SearchingForMateState : CreatureSearchingStateBase
{
    public SearchingForMateState(CreatureBehaviour creature) : base(creature, CreatureStateType.SearchingForMate) { }
    public override void EnterState()
    {
        base.EnterState();
        SearchForMate();
    }

    private void SearchForMate() 
    {
        SearchForTarget(ObservationType.Creature, (target) =>
        {
            creature.stateMachine.TransitionToMovingToMate(target);
        },
        target => {
            var creatureBehaviour = target.transform.GetComponent<CreatureBehaviour>();
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
