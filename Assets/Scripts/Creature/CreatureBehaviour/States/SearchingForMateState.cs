using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchingForMateState : ICreatureState
{
    private readonly CreatureBehaviour creature;

    private Vector3 wanderTarget;
    public SearchingForMateState(CreatureBehaviour creature)
    {
        this.creature = creature;
    }
    public CreatureStateType StateType => CreatureStateType.SearchingForMate;
    public void EnterState()
    {
        Debug.Log($"{creature.GetInstanceID()}Creature starts searching for mate.");
        SearchForMate();
    }

    public void UpdateState()
    {
        if (wanderTarget != Vector3.zero)
        {
            if (creature.MovementManager.IsTargetReached(wanderTarget))
            {
                SearchForMate();
                wanderTarget = Vector3.zero;
            }
            creature.MovementManager.MoveTowards(wanderTarget);
        }
        SearchForMate();
    }

    public void ExitState()
    {
        wanderTarget = Vector3.zero;
        Debug.Log($"{creature.GetInstanceID()}Creature stops searching for mate.");
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
        if (wanderTarget.Equals(Vector3.zero))
        {
            wanderTarget = creature.MovementManager.Wander();
        }
    }



}
