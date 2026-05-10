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
        if (creature == null)
        {
            return CreatureActionExecutionResult.Skipped(
                action,
                CreatureStateType.None,
                GetTargetStateType(action));
        }

        GameConfig config = GameConfig.Instance;
        bool useEcsActionExecution = config != null && config.useEcsActionExecution;
        CreatureStateType previousStateType = creature.CurrentStateType;
        CreatureStateType targetStateType = GetTargetStateType(action);

        if (!useEcsActionExecution)
        {
            return CreatureActionExecutionResult.Skipped(
                action,
                previousStateType,
                targetStateType);
        }

        bool wasRequestWritten = ECSMirrorBridge.TryRequestCreatureAction(creature, action);

        return new CreatureActionExecutionResult(
            action,
            previousStateType,
            targetStateType,
            wasRequestWritten ? targetStateType : creature.CurrentStateType,
            wasRequestWritten);
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
