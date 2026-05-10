using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MovingToMateState : MoveToTargetBase, ICreatureState
{
    public MovingToMateState(BaseCreatureBehaviour creature) : base(creature, CreatureStateType.MovingToMate) { }
    BaseCreatureBehaviour targetCreature;
    public override void EnterState()
    {
        base.EnterState();
    }
    public override void SetTarget(GameObject targetCreature)
    {
        base.SetTarget(targetCreature);
        this.targetCreature = targetCreature != null
            ? targetCreature.GetComponent<BaseCreatureBehaviour>()
            : null;
    }
    public override void UpdateState()
    {
        if (!HasActiveTargetCreature())
        {
            creature.stateMachine.TransitionToIdle();
            return;
        }

        if (!creature.ReproductionManager.CanMateWith(targetCreature))
        {
            creature.stateMachine.TransitionToIdle();
            return;
        }

        if (ReachedTarget())
        {
            if (creature.ReproductionManager.TryMutualAcceptance(targetCreature))
            {
                if (creature.Sex == CreatureSex.Female)
                {
                    creature.stateMachine.TransitionToReproductionState(targetCreature, true);
                    targetCreature.stateMachine.TransitionToReproductionState(creature, false);
                }
                else
                {
                    creature.stateMachine.TransitionToReproductionState(targetCreature, false);
                    targetCreature.stateMachine.TransitionToReproductionState(creature, true);
                }
            }
            else
            {
                creature.stateMachine.TransitionToIdle();
                targetCreature.stateMachine.TransitionToIdle();
            }

            return;
        }

        MoveTowardsTarget();
    }

    private bool HasActiveTargetCreature()
    {
        return target != null &&
               target.activeInHierarchy &&
               targetCreature != null &&
               targetCreature.gameObject.activeInHierarchy;
    }

    public override void ExitState()
    {
        base.ExitState();
    }
}
