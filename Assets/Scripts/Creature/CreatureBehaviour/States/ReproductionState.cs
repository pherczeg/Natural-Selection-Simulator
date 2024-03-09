
using System.Collections;
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
        timer += Time.fixedDeltaTime;
        if (timer >= creature.config.reproductionTime)
        {
            creature.stateMachine.TransitionToIdle();
        }    
    }
    public void StartReproductionCoroutine(CreatureBehaviour otherCreature)
    {
        creature.EatingManager.eatingCoroutine = creature.coroutineRunner.StartCoroutine(ReproductionRoutine(otherCreature));
    }

    IEnumerator ReproductionRoutine(CreatureBehaviour otherCreature)
    {
        yield return new WaitForSeconds(1f);
        creature.ReproductionManager.Reproduct(otherCreature);
        creature.ReproductionManager.StartReproductionCooldown();
        otherCreature.ReproductionManager.StartReproductionCooldown();
    }
    public  override void ExitState()
    {
        base.ExitState();
    }

}
