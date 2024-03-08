using UnityEngine;

public abstract class MoveToTargetBase : CreatureStateBase
{
    protected GameObject target;
    protected MoveToTargetBase(CreatureBehaviour creature, CreatureStateType stateType) : base(creature, stateType)
    {
    }
    public void SetTarget(GameObject targetFood)
    {
        this.target = targetFood;
    }
    protected void MoveTowardsTarget()
    {
        creature.MovementManager.MoveTowards(target.transform.position);
    }

    protected bool ReachedTarget()
    {
        return Vector3.Distance(creature.transform.position, target.transform.position) < 1.0f;
    }
}