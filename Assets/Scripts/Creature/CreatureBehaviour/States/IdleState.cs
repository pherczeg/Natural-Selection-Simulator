using UnityEngine;

public class IdleState : CreatureStateBase
{
    public IdleState(CreatureBehaviour creature) : base(creature, CreatureStateType.Idle) { }

    public override void EnterState()
    {
        base.EnterState();
    }

    public override void ExitState()
    {
        base.ExitState();
    }
    public override void UpdateState()
    {
    }
}
