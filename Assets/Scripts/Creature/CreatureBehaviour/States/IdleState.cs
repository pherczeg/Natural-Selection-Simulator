using UnityEngine;

public class IdleState : ICreatureState
{
    private readonly CreatureBehaviour creature;
    public IdleState(CreatureBehaviour creatureBehaviour)
    {
        this.creature = creatureBehaviour;
    }
    public CreatureStateType StateType => CreatureStateType.Idle;
    public void EnterState()
    {
    }

    public void ExitState()
    {
    }

    public void UpdateState()
    {
    }
}
