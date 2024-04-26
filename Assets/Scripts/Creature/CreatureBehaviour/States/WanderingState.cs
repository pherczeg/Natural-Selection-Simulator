
using Unity.VisualScripting;
using UnityEngine;

internal class WanderingState : CreatureStateBase
{
    
    private Vector3 target;
    public WanderingState(BaseCreatureBehaviour creature):base(creature,CreatureStateType.Wandering){}

    public override void EnterState()
    {
        base.EnterState();
        target = creature.MovementManager.Wander();
    }
    public override void UpdateState()
    {
        if (target != Vector3.zero)
        {
            if (creature.MovementManager.IsTargetReached(target))
            {
                target = creature.MovementManager.Wander();
            }
            creature.MovementManager.MoveTowards(target);
        }
    }
    public override void ExitState()
    {
        base.ExitState();
        target = Vector3.zero;
    }
}
