using System;
using System.Linq;
using UnityEngine;

public abstract class CreatureSearchingStateBase : CreatureStateBase
{
    protected Vector3 wanderTarget;
    protected delegate void TargetAction(GameObject target);
    protected CreatureSearchingStateBase(CreatureBehaviour creature, CreatureStateType initialType)
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
    protected void SearchForTarget(ObservationType targetType, TargetAction onTargetFound, Func<GameObject, bool> additionalCriteria = null)
    {
        GameObject closestTarget = null;
        float closestTargetDistance = float.MaxValue;

        foreach (var observation in creature.ObservationManager.Observations.Where(x=>x.Value.observedObject != null))
        {
            GameObject observedObject = observation.Value.observedObject;
            if (observation.Value.type != targetType || observedObject == null)
            {
                continue;
            }
            if (additionalCriteria != null && !additionalCriteria(observedObject))
            {
                continue;
            }

            float distance = observation.Value.distance;
            if (distance < closestTargetDistance)
            {
                closestTargetDistance = distance;
                closestTarget = observedObject;
            }
        }

        if (closestTarget != null)
        {
            onTargetFound(closestTarget);
        }
        else
        {
            EnsureWanderTarget();
        }
    }


    protected abstract void OnTargetReached();

    protected void EnsureWanderTarget()
    {
        wanderTarget = creature.MovementManager.Wander();
    }
}
