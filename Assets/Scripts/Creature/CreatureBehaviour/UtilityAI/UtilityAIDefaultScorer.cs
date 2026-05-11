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
        return Score(
            identity,
            contextData,
            UtilityBehaviorScoring.DefaultProfile,
            observationResult,
            parameters,
            decisionTime);
    }

    public static CreatureUtilityDecisionData Score(
        CreatureIdentity identity,
        CreatureAIContextData contextData,
        CreatureUtilityBehaviorData behaviorData,
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

        return Score(context, identity.creatureKind, parameters, behaviorData, decisionTime);
    }

    public static CreatureUtilityDecisionData Score(
        UtilityAIContext context,
        int creatureKind,
        UtilityAIScoringParameters parameters,
        float decisionTime)
    {
        return Score(context, creatureKind, parameters, UtilityBehaviorScoring.DefaultProfile, decisionTime);
    }

    public static CreatureUtilityDecisionData Score(
        UtilityAIContext context,
        int creatureKind,
        UtilityAIScoringParameters parameters,
        CreatureUtilityBehaviorData behaviorData,
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

        CreatureUtilityBehaviorData sanitizedBehavior = UtilityBehaviorScoring.Sanitize(behaviorData);

        float keepCurrentStateBaseScore = UtilityAIScoreRules.GetKeepCurrentStateScore(context, parameters, creatureKind);
        float foodActionBaseScore =
            UtilityAIScoreRules.GetHungerScore(context, parameters) *
            UtilityAIScoreRules.GetFoodSearchAvailabilityScore(context, parameters, creatureKind);
        float searchMateBaseScore =
            SearchMateBaseScore *
            UtilityAIScoreRules.GetReproductionScore(context, parameters) *
            UtilityAIScoreRules.GetMateSearchAvailabilityScore(context);
        float wanderBaseScore = WanderBaseScore * UtilityAIScoreRules.GetIdleWanderScore(context);

        float keepCurrentStateScore = UtilityBehaviorScoring.ApplyActionWeight(
            CreatureAction.None,
            keepCurrentStateBaseScore,
            sanitizedBehavior);
        float foodActionScore = UtilityBehaviorScoring.ApplyActionWeight(
            creatureKind == ECSCreatureKind.Predator ? CreatureAction.Hunt : CreatureAction.SearchFood,
            foodActionBaseScore,
            sanitizedBehavior);
        float searchMateScore = UtilityBehaviorScoring.ApplyActionWeight(
            CreatureAction.SearchMate,
            searchMateBaseScore,
            sanitizedBehavior);
        float wanderScore = UtilityBehaviorScoring.ApplyActionWeight(
            CreatureAction.Wander,
            wanderBaseScore,
            sanitizedBehavior);

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

public static class UtilityBehaviorScoring
{
    public const float MinWeight = 0f;
    public const float MaxWeight = 2f;
    public const float DefaultWeight = 1f;
    private const float UnsetThreshold = 0.0001f;

    public static CreatureUtilityBehaviorData DefaultProfile => new CreatureUtilityBehaviorData
    {
        keepCurrentStateWeight = DefaultWeight,
        foodActionWeight = DefaultWeight,
        searchMateWeight = DefaultWeight,
        wanderWeight = DefaultWeight
    };

    public static CreatureUtilityBehaviorData Sanitize(in CreatureUtilityBehaviorData profile)
    {
        CreatureUtilityBehaviorData sanitized = new CreatureUtilityBehaviorData
        {
            keepCurrentStateWeight = ClampWeight(profile.keepCurrentStateWeight),
            foodActionWeight = ClampWeight(profile.foodActionWeight),
            searchMateWeight = ClampWeight(profile.searchMateWeight),
            wanderWeight = ClampWeight(profile.wanderWeight)
        };

        return IsUnset(sanitized)
            ? DefaultProfile
            : sanitized;
    }

    public static bool IsUnset(in CreatureUtilityBehaviorData profile)
    {
        return profile.keepCurrentStateWeight <= UnsetThreshold &&
               profile.foodActionWeight <= UnsetThreshold &&
               profile.searchMateWeight <= UnsetThreshold &&
               profile.wanderWeight <= UnsetThreshold;
    }

    public static float ApplyActionWeight(
        CreatureAction action,
        float baseScore,
        in CreatureUtilityBehaviorData profile)
    {
        float weight = GetActionWeight(action, profile);
        float weightedScore = baseScore * weight;
        return IsFinite(weightedScore) ? weightedScore : 0f;
    }

    public static float GetActionWeight(CreatureAction action, in CreatureUtilityBehaviorData profile)
    {
        switch (action)
        {
            case CreatureAction.None:
                return ClampWeight(profile.keepCurrentStateWeight);
            case CreatureAction.SearchFood:
            case CreatureAction.Hunt:
                return ClampWeight(profile.foodActionWeight);
            case CreatureAction.SearchMate:
                return ClampWeight(profile.searchMateWeight);
            case CreatureAction.Wander:
                return ClampWeight(profile.wanderWeight);
            case CreatureAction.Flee:
            default:
                return DefaultWeight;
        }
    }

    public static float ClampWeight(float value)
    {
        if (!IsFinite(value))
            return DefaultWeight;

        if (value < MinWeight)
            return MinWeight;

        if (value > MaxWeight)
            return MaxWeight;

        return value;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public static class UtilityBehaviorGenetics
{
    public const float FatherContribution = 0.475f;
    public const float MotherContribution = 0.475f;
    public const float MutationContribution = 0.05f;

    private const float DefaultInitialMinWeight = 0.75f;
    private const float DefaultInitialMaxWeight = 1.25f;

    public static CreatureUtilityBehaviorData CreateRandomProfile(GameConfig config)
    {
        GetWeightRange(config, out float minWeight, out float maxWeight);

        CreatureUtilityBehaviorData profile = new CreatureUtilityBehaviorData
        {
            keepCurrentStateWeight = UnityEngine.Random.Range(minWeight, maxWeight),
            foodActionWeight = UnityEngine.Random.Range(minWeight, maxWeight),
            searchMateWeight = UnityEngine.Random.Range(minWeight, maxWeight),
            wanderWeight = UnityEngine.Random.Range(minWeight, maxWeight)
        };

        return UtilityBehaviorScoring.Sanitize(profile);
    }

    public static CreatureUtilityBehaviorData InheritProfile(
        in CreatureUtilityBehaviorData fatherProfile,
        in CreatureUtilityBehaviorData motherProfile,
        GameConfig config)
    {
        GetWeightRange(config, out float minWeight, out float maxWeight);

        CreatureUtilityBehaviorData inherited = new CreatureUtilityBehaviorData
        {
            keepCurrentStateWeight = BlendGene(fatherProfile.keepCurrentStateWeight, motherProfile.keepCurrentStateWeight, minWeight, maxWeight),
            foodActionWeight = BlendGene(fatherProfile.foodActionWeight, motherProfile.foodActionWeight, minWeight, maxWeight),
            searchMateWeight = BlendGene(fatherProfile.searchMateWeight, motherProfile.searchMateWeight, minWeight, maxWeight),
            wanderWeight = BlendGene(fatherProfile.wanderWeight, motherProfile.wanderWeight, minWeight, maxWeight)
        };

        return UtilityBehaviorScoring.Sanitize(inherited);
    }

    private static float BlendGene(float fatherValue, float motherValue, float randomMin, float randomMax)
    {
        float randomMutationValue = UnityEngine.Random.Range(randomMin, randomMax);
        return fatherValue * FatherContribution +
               motherValue * MotherContribution +
               randomMutationValue * MutationContribution;
    }

    private static void GetWeightRange(GameConfig config, out float minWeight, out float maxWeight)
    {
        if (config == null)
        {
            minWeight = DefaultInitialMinWeight;
            maxWeight = DefaultInitialMaxWeight;
            return;
        }

        minWeight = UtilityBehaviorScoring.ClampWeight(UnityEngine.Mathf.Min(config.utilityBehaviorWeightMin, config.utilityBehaviorWeightMax));
        maxWeight = UtilityBehaviorScoring.ClampWeight(UnityEngine.Mathf.Max(config.utilityBehaviorWeightMin, config.utilityBehaviorWeightMax));
        if (maxWeight <= minWeight ||
            (maxWeight <= UtilityBehaviorScoring.DefaultWeight * 0.01f &&
             minWeight <= UtilityBehaviorScoring.DefaultWeight * 0.01f))
        {
            minWeight = DefaultInitialMinWeight;
            maxWeight = DefaultInitialMaxWeight;
            return;
        }

        if (maxWeight < minWeight)
        {
            float temp = minWeight;
            minWeight = maxWeight;
            maxWeight = temp;
        }
    }
}
