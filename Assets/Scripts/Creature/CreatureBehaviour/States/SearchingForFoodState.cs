using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchingForFoodState : CreatureSearchingStateBase
{
    public SearchingForFoodState(CreatureBehaviour creature) : base(creature, CreatureStateType.SearchingForFood) { }
    public override void EnterState()
    {
        base.EnterState();
        SearchForFood();
    }
    public override void ExitState()
    {
        base.ExitState();
        wanderTarget = Vector3.zero;
    }

    public override void UpdateState()
    {
        if (creature.MovementManager.IsTargetReached(wanderTarget))
        {
            SearchForFood();
        }
        else
        {
            creature.MovementManager.MoveTowards(wanderTarget);
        }
    }
    private void SearchForFood()
    {
        SearchForTarget(ObservationType.Food, (target) =>
        {
            creature.stateMachine.TransitionToMovingToFood(target);
        }, 
        (target) => !creature.EatingManager.IsFoodBlacklisted(target.GetComponent<Food>()
        ));
    }

    protected override void OnTargetReached()
    {
        SearchForFood();
    }
}
