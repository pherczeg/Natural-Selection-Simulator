public static class CreatureActionStatusAdapter
{
    public static bool TryGetActionState(
        BaseCreatureBehaviour creature,
        out CreatureActionStateData actionState)
    {
        actionState = default;
        if (creature == null)
            return false;

        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useEcsActionExecution)
            return false;

        return ECSMirrorBridge.TryGetCreatureActionState(creature, out actionState);
    }

    public static CreatureStateType GetLegacyStateType(BaseCreatureBehaviour creature)
    {
        if (creature == null)
            return CreatureStateType.None;

        if (TryGetActionState(creature, out CreatureActionStateData actionState))
            return MapToLegacyStateType(actionState);

        return CreatureStateType.None;
    }

    public static CreatureStateType MapToLegacyStateType(CreatureActionStateData actionState)
    {
        if (actionState.currentAction == CreatureAction.SearchFood ||
            actionState.currentAction == CreatureAction.Hunt)
        {
            switch (actionState.phase)
            {
                case CreatureActionPhase.Searching:
                    return CreatureStateType.SearchingForFood;
                case CreatureActionPhase.MovingToTarget:
                    return CreatureStateType.MovingToFood;
                case CreatureActionPhase.Executing:
                    return actionState.currentAction == CreatureAction.Hunt
                        ? CreatureStateType.Predation
                        : CreatureStateType.Eating;
            }
        }

        if (actionState.currentAction == CreatureAction.SearchMate)
        {
            switch (actionState.phase)
            {
                case CreatureActionPhase.Searching:
                    return CreatureStateType.SearchingForMate;
                case CreatureActionPhase.MovingToTarget:
                    return CreatureStateType.MovingToMate;
                case CreatureActionPhase.Executing:
                    return CreatureStateType.Reproducting;
            }
        }

        if (actionState.currentAction == CreatureAction.Wander &&
            actionState.phase == CreatureActionPhase.Wandering)
        {
            return CreatureStateType.Wandering;
        }

        if (actionState.currentAction == CreatureAction.None &&
            actionState.phase == CreatureActionPhase.Idle)
        {
            return CreatureStateType.Idle;
        }

        return actionState.legacyStateType;
    }

    public static bool IsEnergyDrainBlocked(BaseCreatureBehaviour creature)
    {
        if (!TryGetActionState(creature, out CreatureActionStateData actionState))
            return false;

        return actionState.phase == CreatureActionPhase.Executing &&
               (actionState.currentAction == CreatureAction.SearchFood ||
                actionState.currentAction == CreatureAction.Hunt);
    }
}
