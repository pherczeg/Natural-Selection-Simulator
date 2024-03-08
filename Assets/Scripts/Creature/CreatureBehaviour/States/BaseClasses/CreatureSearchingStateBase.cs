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
    protected void SearchForTarget(ObservationType targetType, TargetAction onTargetFound)
    {
        GameObject closestTarget = null;
        float closestTargetDistance = float.MaxValue;

        foreach (var observation in creature.ObservationManager.Observations)
        {
            if (observation.Value.type != targetType || observation.Value.observedObject == null)
            {
                continue;
            }

            float distance = observation.Value.distance;
            if (distance < closestTargetDistance)
            {
                closestTargetDistance = distance;
                closestTarget = observation.Value.observedObject;
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
