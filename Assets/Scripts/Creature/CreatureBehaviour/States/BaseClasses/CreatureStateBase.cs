using UnityEngine;

public abstract class CreatureStateBase : ICreatureState
{
    protected CreatureBehaviour creature;

    private CreatureStateType stateType;
    public CreatureStateType StateType
    {
        get => stateType;
        protected set => stateType = value;
    }
    protected CreatureStateBase(CreatureBehaviour creature, CreatureStateType  initialType)
    {
        this.creature = creature;
        this.StateType = initialType;
    }

    public virtual void EnterState()
    {
        Debug.Log($"{creature.GetInstanceID()}: Entering {stateType}");
    }

    public abstract void UpdateState();

    public virtual void ExitState()
    {
        Debug.Log($"{creature.GetInstanceID()}: Ending {stateType}");
    }
}
