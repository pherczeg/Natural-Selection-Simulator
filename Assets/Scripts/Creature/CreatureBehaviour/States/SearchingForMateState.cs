using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
