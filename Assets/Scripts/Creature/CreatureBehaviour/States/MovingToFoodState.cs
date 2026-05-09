using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        if (creature is PredatorBehaviour)
        {
            RefreshPredatorTarget();
        }

        if (target != null)
        {
            if (ReachedTarget())
            {
                if (creature is PredatorBehaviour)
                {
                    var prey = target.GetComponent<BaseCreatureBehaviour>();
                    if (IsValidPredatorPrey(prey))
                    {
                        creature.stateMachine.TransitionToPredation(prey);
                        return;
                    }

                    creature.stateMachine.TransitionToWandering();
                    return;
                }

                var foodComponent = target.GetComponent<Food>();
                if (foodComponent != null)
                {
                    creature.stateMachine.TransitionToEating(foodComponent);
                }
                else
                {
                    creature.stateMachine.TransitionToIdle();
                }
            }
            else
            { 
                if (creature is PredatorBehaviour)
                {
                    creature.MovementManager.TryStartSprint();
                }
                MoveTowardsTarget();
            }
        }
        else
        {
            creature.stateMachine.TransitionToIdle();
        }
    }

    private void RefreshPredatorTarget()
    {
        if (target != null && target.activeInHierarchy && IsValidPredatorPrey(target.GetComponent<BaseCreatureBehaviour>()))
            return;

        target = null;

        var observation = creature.ObservationManager.Observations
            .FirstOrDefault(o => o.type == ObservationType.FoodCreature &&
                                 o.observedObject != null &&
                                 o.observedObject.activeInHierarchy &&
                                 IsValidPredatorPrey(o.observedObject.GetComponent<BaseCreatureBehaviour>()));

        if (observation.observedObject != null)
        {
            target = observation.observedObject;
        }
    }

    private bool IsValidPredatorPrey(BaseCreatureBehaviour prey)
    {
        if (prey == null || prey == creature || !prey.gameObject.activeInHierarchy)
            return false;

        if (prey is HerbivoreBehaviour herbivore && herbivore.IsCaptured)
            return false;

        if (creature is PredatorBehaviour predator && predator.IsPreyBlacklisted(prey))
            return false;

        return true;
    }

    public override void ExitState()
    {
        base.ExitState();
    }
}
