public static class UtilityAIDefaultScorer
{
    public const float SearchMateBaseScore = 0.85f;
    public const float WanderBaseScore = 0.3f;
    public const string ECSScoringContextSource = "ECS Utility Scoring";

    public static CreatureUtilityDecisionData Score(
        CreatureIdentity identity,
        CreatureAIContextData contextData,
        CreatureObservationResultData observationResult,
        UtilityAIScoringParameters parameters,
        float decisionTime)
    {
        UtilityAIContext context = new UtilityAIContext(
            contextData.energyPercent,
            contextData.isMature,
            contextData.isReproductionReady,
            contextData.currentState,
            contextData.hasKnownFood || observationResult.closestFoodInstanceId != 0,
            contextData.hasKnownMate || observationResult.closestMateInstanceId != 0,
            contextData.hasKnownPrey || observationResult.closestPreyInstanceId != 0,
            contextData.isThreatened);

        return Score(context, identity.creatureKind, parameters, decisionTime);
    }

    public static CreatureUtilityDecisionData Score(
        UtilityAIContext context,
        int creatureKind,
        UtilityAIScoringParameters parameters,
        float decisionTime)
    {
        bool hasSupportedCreatureKind =
            creatureKind == ECSCreatureKind.Herbivore ||
            creatureKind == ECSCreatureKind.Predator;

        if (!hasSupportedCreatureKind)
        {
            return new CreatureUtilityDecisionData
            {
                hasDecision = false,
                selectedAction = CreatureAction.None,
                selectedScore = 0f,
                decisionTime = decisionTime,
                transitionState = CreatureStateType.None,
                resultState = context.currentState
            };
        }

        float keepCurrentStateScore = UtilityAIScoreRules.GetKeepCurrentStateScore(context, parameters);
        float foodActionScore =
            UtilityAIScoreRules.GetHungerScore(context, parameters) *
            UtilityAIScoreRules.GetFoodSearchAvailabilityScore(context);
        float searchMateScore =
            SearchMateBaseScore *
            UtilityAIScoreRules.GetReproductionScore(context, parameters) *
            UtilityAIScoreRules.GetMateSearchAvailabilityScore(context);
        float wanderScore = WanderBaseScore * UtilityAIScoreRules.GetIdleWanderScore(context);

        CreatureAction foodAction = creatureKind == ECSCreatureKind.Predator
            ? CreatureAction.Hunt
            : CreatureAction.SearchFood;

        CreatureAction selectedAction = CreatureAction.None;
        float selectedScore = keepCurrentStateScore;

        if (foodActionScore > selectedScore)
        {
            selectedAction = foodAction;
            selectedScore = foodActionScore;
        }

        if (searchMateScore > selectedScore)
        {
            selectedAction = CreatureAction.SearchMate;
            selectedScore = searchMateScore;
        }

        if (wanderScore > selectedScore)
        {
            selectedAction = CreatureAction.Wander;
            selectedScore = wanderScore;
        }

        return new CreatureUtilityDecisionData
        {
            hasDecision = true,
            selectedAction = selectedAction,
            selectedScore = UtilityAIScoreRules.Clamp01(selectedScore),
            keepCurrentStateScore = UtilityAIScoreRules.Clamp01(keepCurrentStateScore),
            foodActionScore = UtilityAIScoreRules.Clamp01(foodActionScore),
            searchMateScore = UtilityAIScoreRules.Clamp01(searchMateScore),
            wanderScore = UtilityAIScoreRules.Clamp01(wanderScore),
            decisionTime = decisionTime,
            transitionState = GetTargetStateType(selectedAction),
            resultState = context.currentState
        };
    }

    public static float GetActionScore(CreatureUtilityDecisionData decisionData, CreatureAction action)
    {
        switch (action)
        {
            case CreatureAction.None:
                return decisionData.keepCurrentStateScore;
            case CreatureAction.SearchFood:
            case CreatureAction.Hunt:
                return decisionData.foodActionScore;
            case CreatureAction.SearchMate:
                return decisionData.searchMateScore;
            case CreatureAction.Wander:
                return decisionData.wanderScore;
            case CreatureAction.Flee:
            default:
                return 0f;
        }
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
