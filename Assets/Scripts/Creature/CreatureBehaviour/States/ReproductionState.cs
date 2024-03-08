
using UnityEngine;

public class ReproductionState : CreatureStateBase
{
    private float timer;
    public ReproductionState(CreatureBehaviour creature) : base(creature, CreatureStateType.Reproducting) { }
    public override void EnterState()
    {
        base.EnterState();
        timer = 0f;
    }
    public override void UpdateState()
    {
        timer += Time.deltaTime;
        if (timer >= creature.config.reproductionTime)
        {

        }    
    }
    public  override void ExitState()
    {
        base.ExitState();
    }

}
