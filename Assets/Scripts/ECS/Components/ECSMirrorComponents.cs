using Unity.Entities;
using Unity.Mathematics;

public static class ECSCreatureKind
{
    public const int Unknown = 0;
    public const int Herbivore = 1;
    public const int Predator = 2;
}

public struct CreatureTag : IComponentData
{
}

public struct HerbivoreTag : IComponentData
{
}

public struct PredatorTag : IComponentData
{
}

public struct FoodTag : IComponentData
{
}

public struct SpawnCreatureRequest : IComponentData
{
    public int creatureKind;
    public int sex;
    public float3 position;
    public float moveSpeed;
    public float weight;
    public float senseRadius;
    public float sprintDuration;
    public float sprintFactor;
    public float sprintCooldown;
    public float sprintCooldownSpeedFactor;
    public float desirability;
    public float agility;
    public int herbivoreSocialStrategy;
    public float strength;
    public float utilityKeepCurrentStateWeight;
    public float utilityFoodActionWeight;
    public float utilitySearchMateWeight;
    public float utilityWanderWeight;
    public float initialAge;
}

public struct DespawnCreatureRequest : IComponentData
{
    public int gameObjectInstanceId;
    public int reason;
}

public struct SpawnFoodRequest : IComponentData
{
    public float3 position;
    public float targetNutrition;
}

public struct DespawnFoodRequest : IComponentData
{
    public int gameObjectInstanceId;
    public int reason;
}

public struct CreatureIdentity : IComponentData
{
    public int gameObjectInstanceId;
    public int creatureKind;
    public int sex;
}

public struct CreatureTransformMirror : IComponentData
{
    public float3 position;
    public quaternion rotation;
    public float3 scale;
}

public struct CreatureAIContextData : IComponentData
{
    public float energyPercent;
    public bool isMature;
    public bool isReproductionReady;
    public CreatureStateType currentState;
    public bool hasKnownFood;
    public bool hasKnownMate;
    public bool hasKnownPrey;
    public bool isThreatened;
}

[System.Serializable]
public struct CreatureUtilityBehaviorData : IComponentData
{
    public float keepCurrentStateWeight;
    public float foodActionWeight;
    public float searchMateWeight;
    public float wanderWeight;
}

public struct CreatureObservationSensorData : IComponentData
{
    public float senseRadius;
}

public struct CreatureObservationResultData : IComponentData
{
    public int closestFoodInstanceId;
    public float closestFoodDistanceSq;
    public int closestPreyInstanceId;
    public float closestPreyDistanceSq;
    public int closestMateInstanceId;
    public float closestMateDistanceSq;
}

public struct CreatureLifecycleData : IComponentData
{
    public int gameObjectInstanceId;
    public int creatureKind;
    public CreatureStateType currentState;
    public float age;
    public float maturityFraction;
    public float energyLevel;
    public float maxEnergy;
    public float currentMaxEnergy;
    public float weight;
    public float baseMoveSpeed;
    public float baseSenseRadius;
    public float energyConsumption;
    public float speedAgeMultiplier;
    public float senseAgeMultiplier;
    public float desirabilityAgeMultiplier;
    public float accumulatedDeltaTime;
    public bool starvationDespawnRequested;
    public bool oldAgeDespawnRequested;
}

public struct CreatureUtilityDecisionData : IComponentData
{
    public bool hasDecision;
    public CreatureAction selectedAction;
    public float selectedScore;
    public float keepCurrentStateScore;
    public float foodActionScore;
    public float searchMateScore;
    public float wanderScore;
    public float decisionTime;
    public CreatureStateType transitionState;
    public CreatureStateType resultState;
}

public enum CreatureActionPhase
{
    None = default,
    Idle,
    Searching,
    MovingToTarget,
    Executing,
    Wandering
}

public enum CreatureActionStatus
{
    None = default,
    Running,
    Completed,
    Cancelled,
    Blocked
}

public struct CreatureActionStateData : IComponentData
{
    public CreatureAction currentAction;
    public CreatureActionPhase phase;
    public CreatureActionStatus status;
    public CreatureStateType legacyStateType;
    public bool canCancel;
    public bool canComplete;
}

public struct CreatureActionTargetData : IComponentData
{
    public int targetInstanceId;
}

public struct CreatureActionTimerData : IComponentData
{
    public float elapsedTime;
    public float remainingTime;
}

public struct CreatureActionRequestData : IComponentData
{
    public bool hasRequest;
    public CreatureAction requestedAction;
    public CreatureActionPhase requestedPhase;
    public int targetInstanceId;
    public bool cancelRequested;
    public bool completeRequested;
}

public struct CreatureWanderExecutionData : IComponentData
{
    public bool hasTarget;
    public float3 targetPosition;
}

public struct FoodMirrorData : IComponentData
{
    public int gameObjectInstanceId;
    public float nutritionValue;
    public float maxNutritionValue;
    public float nutritionPercent;
    public float age;
    public float growthFraction;
    public bool despawnRequested;
    public bool isBeingEaten;
    public int eatingCreatureInstanceId;
    public float3 position;
    public quaternion rotation;
    public float3 scale;
}
