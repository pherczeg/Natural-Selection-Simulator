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
        //var debugString = $"{creature.GetInstanceID()}: Entering {stateType}";
        var debugString = $"Entering {stateType}";
        //Debug.Log(debugString);
        creature.DEBUG_string += debugString + Time.realtimeSinceStartup + "\n";
        if (creature?.EatingManager?.eatingCoroutine != null)
        {
            Debug.Log("adsf");
        }
    }

    public abstract void UpdateState();

    public virtual void ExitState()
    {
        //var debugString = $"{creature.GetInstanceID()}: Ending {stateType}";
        var debugString = $"Ending {stateType}";
        //Debug.Log(debugString);
        creature.DEBUG_string += debugString + Time.realtimeSinceStartup + "\n";
    }
}
