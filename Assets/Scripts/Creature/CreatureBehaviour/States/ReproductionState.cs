
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
        if (timer >= GameConfig.Instance.reproductionTime)
        {
            creature.stateMachine.TransitionToWandering();
        }    
    }
    public override void ExitState()
    {
        base.ExitState();
        if(creature.ReproductionManager.reproductionCoroutine != null)
        {
            creature.coroutineRunner.StopCoroutine(creature.ReproductionManager.reproductionCoroutine);
        }
        creature.ReproductionManager.reproductionCoroutine = null;
    }
    public void StartReproductionCoroutine(CreatureBehaviour otherCreature)
    {
        creature.ReproductionManager.reproductionCoroutine = creature.coroutineRunner.StartCoroutine(ReproductionRoutine(otherCreature));
    }

    IEnumerator ReproductionRoutine(CreatureBehaviour otherCreature)
    {
        creature.ReproductionManager.StartReproductionCooldown();
        otherCreature.ReproductionManager.StartReproductionCooldown();
        otherCreature.stateMachine.TransitionToReproductionState(creature, false);
        yield return new WaitForSeconds(1f);
        creature.ReproductionManager.Reproduct(otherCreature);
    }
}
