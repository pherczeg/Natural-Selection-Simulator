using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToMateState : MoveToTargetBase, ICreatureState
{
    public MovingToMateState(CreatureBehaviour creature) : base(creature) { }
    public CreatureStateType StateType => CreatureStateType.MovingToMate;
    public void EnterState()
    {
        Debug.Log($"{creature.GetInstanceID()}Creature is moving to mate.");
    }

    public void UpdateState()
    {
        if (target != null)
        {
            if (ReachedTarget())
            {
                var creature = target.GetComponent<CreatureBehaviour>();
                creature.stateMachine.TransitionToMovingToMate(creature.gameObject);
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

    public void ExitState()
    {
        Debug.Log($"{creature.GetInstanceID()}Creature stops moving to mate.");
    }
}
