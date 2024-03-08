using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchingForMateState : CreatureStateBase
{
    private Vector3 wanderTarget;
    public SearchingForMateState(CreatureBehaviour creature) : base(creature, CreatureStateType.SearchingForMate) { }
    public override void EnterState()
    {
        base.EnterState();
        SearchForMate();
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

    private void SearchForMate()
    {
        CreatureBehaviour closestCreature = FindClosestReproductiveCreature();

        if (closestCreature != null)
        {
            creature.stateMachine.TransitionToMovingToMate(closestCreature.gameObject);
        }
        else
        {
            EnsureWanderTarget();
        }
    }

    private CreatureBehaviour FindClosestReproductiveCreature()
    {
        float closestFoodDistance = float.MaxValue;
        CreatureBehaviour closestMate = null;

        foreach (var observation in creature.ObservationManager.Observations)
        {
            if (observation.Value.observedObject == null)
            {
                continue;
            }
            CreatureBehaviour observedCreature = observation.Value.observedObject?.GetComponent<CreatureBehaviour>();
            if (observation.Value.type == ObservationType.Food && observedCreature && observedCreature.ReproductionManager.IsReadyToReproduction())
            {
                float distance = observation.Value.distance;
                if (distance < closestFoodDistance)
                {
                    closestFoodDistance = distance;
                    closestMate = observedCreature;
                }
            }
        }

        return closestMate;
    }

    private void EnsureWanderTarget()
    {
        wanderTarget = creature.MovementManager.Wander();
    }



}
