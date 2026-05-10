using System;
using UnityEngine;

[Serializable]
public struct CreatureActionExecutionResult
{
    [SerializeField] private CreatureAction action;
    [SerializeField] private CreatureStateType previousStateType;
    [SerializeField] private CreatureStateType targetStateType;
    [SerializeField] private CreatureStateType resultStateType;
    [SerializeField] private bool wasTransitionApplied;

    public CreatureAction Action => action;
    public CreatureStateType PreviousStateType => previousStateType;
    public CreatureStateType TargetStateType => targetStateType;
    public CreatureStateType ResultStateType => resultStateType;
    public bool WasTransitionApplied => wasTransitionApplied;

    public CreatureActionExecutionResult(
        CreatureAction action,
        CreatureStateType previousStateType,
        CreatureStateType targetStateType,
        CreatureStateType resultStateType,
        bool wasTransitionApplied)
    {
        this.action = action;
        this.previousStateType = previousStateType;
        this.targetStateType = targetStateType;
        this.resultStateType = resultStateType;
        this.wasTransitionApplied = wasTransitionApplied;
    }

    public static CreatureActionExecutionResult Skipped(
        CreatureAction action,
        CreatureStateType currentStateType,
        CreatureStateType targetStateType)
    {
        return new CreatureActionExecutionResult(
            action,
            currentStateType,
            targetStateType,
            currentStateType,
            false);
    }
}

public static class CreatureActionExecutor
{
    public static CreatureActionExecutionResult Execute(BaseCreatureBehaviour creature, CreatureAction action)
    {
        if (creature == null || creature.stateMachine == null)
        {
            return CreatureActionExecutionResult.Skipped(
                action,
                CreatureStateType.None,
                GetTargetStateType(action));
        }

        CreatureStateType previousStateType = creature.CurrentStateType;
        CreatureStateType targetStateType = GetTargetStateType(action);
        bool wasTransitionApplied = true;

        switch (action)
        {
            case CreatureAction.SearchFood:
            case CreatureAction.Hunt:
                creature.stateMachine.TransitionToSearchingForFood();
                break;
            case CreatureAction.SearchMate:
                creature.stateMachine.TransitionToSearchingForMate();
                break;
            case CreatureAction.Wander:
                creature.stateMachine.TransitionToWandering();
                break;
            case CreatureAction.None:
            case CreatureAction.Flee:
            default:
                wasTransitionApplied = false;
                break;
        }

        return new CreatureActionExecutionResult(
            action,
            previousStateType,
            targetStateType,
            creature.CurrentStateType,
            wasTransitionApplied);
    }

    public static CreatureStateType GetTargetStateType(CreatureAction action)
    {
        switch (action)
        {
            case CreatureAction.SearchFood:
            case CreatureAction.Hunt:
                return CreatureStateType.SearchingForFood;
            case CreatureAction.SearchMate:
                return CreatureStateType.SearchingForMate;
            case CreatureAction.Wander:
                return CreatureStateType.Wandering;
            case CreatureAction.None:
            case CreatureAction.Flee:
            default:
                return CreatureStateType.None;
        }
    }
}
