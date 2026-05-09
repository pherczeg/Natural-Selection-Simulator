using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchingForFoodState : CreatureSearchingStateBase
{
    public SearchingForFoodState(BaseCreatureBehaviour creature) : base(creature, CreatureStateType.SearchingForFood) { }
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
        ObservationType targetType = creature is PredatorBehaviour ? ObservationType.FoodCreature : ObservationType.Food;
        SearchForTarget(targetType, (target) =>
        {
            creature.stateMachine.TransitionToMovingToFood(target);
        }, 
        (target) =>
        {
            if (creature is PredatorBehaviour)
            {
                var prey = target.GetComponent<BaseCreatureBehaviour>();
                if (prey == null || prey == creature || !prey.gameObject.activeInHierarchy)
                    return false;

                if (prey is HerbivoreBehaviour herbivore && herbivore.IsCaptured)
                    return false;

                return creature is PredatorBehaviour predator && !predator.IsPreyBlacklisted(prey);
            }

            var food = target.GetComponent<Food>();
            return food != null && !creature.EatingManager.IsFoodBlacklisted(food);
        });
    }

    protected override void OnTargetReached()
    {
        SearchForFood();
    }
}
