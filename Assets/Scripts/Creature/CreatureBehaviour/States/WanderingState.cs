
using Unity.VisualScripting;
using UnityEngine;

internal class WanderingState : ICreatureState
{
    private readonly CreatureBehaviour creature;
    
    private Vector3 target;
    public WanderingState(CreatureBehaviour creatureBehaviour)
    {
        this.creature = creatureBehaviour;
    }

    public CreatureStateType StateType => CreatureStateType.Wandering;

    public void EnterState()
    {
        target = creature.MovementManager.Wander();
    }
    public void UpdateState()
    {
        if (target != Vector3.zero)
        {
            if (creature.MovementManager.IsTargetReached(target))
            {
                target = creature.MovementManager.Wander();
                target = Vector3.zero;
            }
            creature.MovementManager.MoveTowards(target);
        }
    }
    public void ExitState()
    {
        target = Vector3.zero;
    }
}
