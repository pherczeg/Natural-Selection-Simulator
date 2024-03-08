
public class ReproductionState : CreatureStateBase
{

    public ReproductionState(CreatureBehaviour creature) : base(creature, CreatureStateType.Reproducting) { }
    public override void EnterState()
    {
        base.EnterState();
    }
    public override void UpdateState()
    {
    }
    public  override void ExitState()
    {
        base.ExitState();
    }

}
