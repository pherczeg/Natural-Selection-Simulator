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
        Debug.Log("Creature starts searching for mate.");
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
        Debug.Log("Creature stops searching for mate.");
    }

    private void SearchForMate()
    {
        
    }
}
