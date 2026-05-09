
using System.Collections;
using UnityEngine;

public class ReproductionState : CreatureStateBase
{
    private float timer;
    public ReproductionState(BaseCreatureBehaviour creature) : base(creature, CreatureStateType.Reproducting) { }
    public override void EnterState()
    {
        base.EnterState();
        timer = 0f;
    }
    public override void UpdateState()
    {
        if (creature.ReproductionManager.reproductionCoroutine != null)
            return;

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
    public bool StartReproductionCoroutine(BaseCreatureBehaviour otherCreature)
    {
        if (!CanRunReproductionRoutine(otherCreature))
        {
            return false;
        }

        creature.ReproductionManager.reproductionCoroutine = creature.coroutineRunner.StartCoroutine(ReproductionRoutine(otherCreature));
        return true;
    }

    IEnumerator ReproductionRoutine(BaseCreatureBehaviour otherCreature)
    {
        if (!CanRunReproductionRoutine(otherCreature))
        {
            creature.ReproductionManager.reproductionCoroutine = null;
            yield break;
        }

        creature.ReproductionManager.StartReproductionCooldown();
        otherCreature.ReproductionManager.StartReproductionCooldown();
        otherCreature.stateMachine.TransitionToReproductionState(creature, false);

        float reproductionTime = GameConfig.Instance != null
            ? Mathf.Max(0f, GameConfig.Instance.reproductionTime)
            : 0f;
        yield return new WaitForSeconds(reproductionTime);

        if (CanRunReproductionRoutine(otherCreature))
        {
            creature.ReproductionManager.Reproduct(otherCreature);
        }

        creature.ReproductionManager.reproductionCoroutine = null;

        if (creature.CurrentStateType == CreatureStateType.Reproducting)
        {
            creature.stateMachine.TransitionToWandering();
        }

        if (otherCreature.CurrentStateType == CreatureStateType.Reproducting)
        {
            otherCreature.stateMachine.TransitionToWandering();
        }
    }

    private bool CanRunReproductionRoutine(BaseCreatureBehaviour otherCreature)
    {
        return creature != null &&
               creature.gameObject.activeInHierarchy &&
               creature.coroutineRunner != null &&
               creature.coroutineRunner.isActiveAndEnabled &&
               creature.ReproductionManager != null &&
               otherCreature != null &&
               otherCreature.gameObject.activeInHierarchy &&
               otherCreature.ReproductionManager != null &&
               otherCreature.stateMachine != null;
    }
}
