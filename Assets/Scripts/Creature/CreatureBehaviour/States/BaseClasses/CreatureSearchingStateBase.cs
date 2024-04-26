using System;
using System.Linq;
using UnityEngine;

public abstract class CreatureSearchingStateBase : CreatureStateBase
{
    protected Vector3 wanderTarget;
    protected delegate void TargetAction(GameObject target);
    protected CreatureSearchingStateBase(BaseCreatureBehaviour creature, CreatureStateType initialType)
        : base(creature, initialType) { }

    public override void EnterState()
    {
        base.EnterState();
        EnsureWanderTarget();
    }

    public override void UpdateState()
    {
        if (creature.MovementManager.IsTargetReached(wanderTarget))
        {
            OnTargetReached();
        }
        else
        {
            creature.MovementManager.MoveTowards(wanderTarget);
        }
    }

    public override void ExitState()
    {
        base.ExitState();
        wanderTarget = Vector3.zero;
    }
    protected void SearchForTarget(ObservationType targetType, Action<GameObject> onTargetFound, Func<GameObject, bool> additionalCriteria = null)
    {
        try
        {

            var observation = creature.ObservationManager.Observations.FirstOrDefault(o => o.type == targetType);
            if (observation.observedObject != null)
            {
                float distance = Vector3.Distance(creature.transform.position, observation.observedObject.transform.position);
                if ((additionalCriteria == null || additionalCriteria(observation.observedObject)) && distance < creature.ObservationManager.SenseRadius)
                {
                    onTargetFound(observation.observedObject);
                }
                else
                {
                    EnsureWanderTarget();
                }
            }
            else
            {
                EnsureWanderTarget();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }

    }


    protected abstract void OnTargetReached();

    protected void EnsureWanderTarget()
    {
        wanderTarget = creature.MovementManager.Wander();
    }
}
