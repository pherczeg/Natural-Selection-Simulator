using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSObservationSystem))]
public partial class ECSFoodSearchExecutionSystem : SystemBase
{
    private EntityQuery executionQuery;

    protected override void OnCreate()
    {
        executionQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
            ComponentType.ReadOnly<CreatureObservationResultData>(),
            ComponentType.ReadWrite<CreatureActionRequestData>(),
            ComponentType.ReadWrite<CreatureActionStateData>(),
            ComponentType.ReadWrite<CreatureActionTargetData>(),
            ComponentType.ReadWrite<CreatureActionTimerData>(),
            ComponentType.ReadWrite<CreatureWanderExecutionData>());

        RequireForUpdate(executionQuery);
    }

    protected override void OnUpdate()
    {
        float deltaTime = (float)World.Time.DeltaTime;
        GameConfig config = GameConfig.Instance;
        bool useEcsMovementExecution = config != null && config.useEcsMovementExecution;

        foreach (var (identityRef, observationRef, requestRef, actionStateRef, actionTargetRef, actionTimerRef, wanderStateRef)
                 in SystemAPI.Query<
                     RefRO<CreatureIdentity>,
                     RefRO<CreatureObservationResultData>,
                     RefRW<CreatureActionRequestData>,
                     RefRW<CreatureActionStateData>,
                     RefRW<CreatureActionTargetData>,
                     RefRW<CreatureActionTimerData>,
                     RefRW<CreatureWanderExecutionData>>())
        {
            CreatureIdentity identity = identityRef.ValueRO;
            CreatureObservationResultData observation = observationRef.ValueRO;
            ref CreatureActionRequestData request = ref requestRef.ValueRW;
            ref CreatureActionStateData actionState = ref actionStateRef.ValueRW;
            ref CreatureActionTargetData actionTarget = ref actionTargetRef.ValueRW;
            ref CreatureActionTimerData actionTimer = ref actionTimerRef.ValueRW;
            ref CreatureWanderExecutionData wanderState = ref wanderStateRef.ValueRW;

            bool hasSearchRequest =
                request.hasRequest &&
                IsFoodSearchAction(request.requestedAction) &&
                request.requestedPhase == CreatureActionPhase.Searching;
            bool hasBlockingRequest = request.hasRequest && !IsFoodSearchAction(request.requestedAction);
            bool isFoodSearchAction = IsFoodSearchAction(actionState.currentAction);

            if (hasBlockingRequest)
            {
                if (isFoodSearchAction)
                {
                    actionState.currentAction = CreatureAction.None;
                    actionState.phase = CreatureActionPhase.Idle;
                    actionState.status = CreatureActionStatus.Cancelled;
                    actionState.legacyStateType = CreatureStateType.Idle;
                    actionState.canCancel = false;
                    actionState.canComplete = false;
                    actionTarget.targetInstanceId = 0;
                    actionTimer.elapsedTime = 0f;
                    actionTimer.remainingTime = -1f;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                }

                continue;
            }

            if (hasSearchRequest)
            {
                request.hasRequest = false;
                request.cancelRequested = false;
                request.completeRequested = false;

                actionState.currentAction = request.requestedAction;
                actionState.phase = CreatureActionPhase.Searching;
                actionState.status = CreatureActionStatus.Running;
                actionState.legacyStateType = CreatureStateType.SearchingForFood;
                actionState.canCancel = true;
                actionState.canComplete = true;

                actionTarget.targetInstanceId = 0;
                actionTimer.elapsedTime = 0f;
                actionTimer.remainingTime = -1f;
                wanderState.hasTarget = false;
                wanderState.targetPosition = float3.zero;
                isFoodSearchAction = true;
            }

            if (!isFoodSearchAction)
                continue;

            actionTimer.elapsedTime += deltaTime;
            actionTimer.remainingTime = -1f;

            if (!ECSMirrorBridge.TryGetCreatureByInstanceId(identity.gameObjectInstanceId, out BaseCreatureBehaviour creature) ||
                creature == null ||
                creature.MovementManager == null)
            {
                actionState.status = CreatureActionStatus.Blocked;
                continue;
            }

            if (actionState.phase == CreatureActionPhase.Searching)
            {
                actionTarget.targetInstanceId = SelectTargetInstanceId(creature, identity, observation, actionState.currentAction);
                if (actionTarget.targetInstanceId != 0)
                {
                    actionState.phase = CreatureActionPhase.MovingToTarget;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToFood;
                    actionTimer.elapsedTime = 0f;
                    actionTimer.remainingTime = -1f;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                }
                else
                {
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.SearchingForFood;

                    Vector3 searchTarget = ToVector3(wanderState.targetPosition);
                    if (!wanderState.hasTarget || creature.MovementManager.IsTargetReached(searchTarget))
                    {
                        searchTarget = creature.MovementManager.ChooseNewWanderTarget();
                        if (searchTarget != Vector3.zero)
                        {
                            wanderState.hasTarget = true;
                            wanderState.targetPosition = ToFloat3(searchTarget);
                        }
                        else
                        {
                            wanderState.hasTarget = false;
                            wanderState.targetPosition = float3.zero;
                            actionState.status = CreatureActionStatus.Blocked;
                        }
                    }

                    if (wanderState.hasTarget)
                    {
                        if (actionState.currentAction == CreatureAction.Hunt)
                        {
                            creature.MovementManager.TryStartSprint();
                        }

                        creature.MovementManager.MoveTowardsOrQueue(ToVector3(wanderState.targetPosition), useEcsMovementExecution);
                    }
                }
            }

            if (actionState.phase == CreatureActionPhase.MovingToTarget)
            {
                if (actionState.currentAction == CreatureAction.Hunt &&
                    TryAbandonHopelessPredatorChase(
                        creature,
                        actionTarget.targetInstanceId,
                        actionTimer.elapsedTime,
                        config))
                {
                    actionTarget.targetInstanceId = 0;
                    actionState.phase = CreatureActionPhase.Searching;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.SearchingForFood;
                    actionTimer.elapsedTime = 0f;
                    actionTimer.remainingTime = -1f;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                    continue;
                }

                if (!TryGetTargetPosition(creature, actionState.currentAction, actionTarget.targetInstanceId, out Vector3 targetPosition))
                {
                    actionTarget.targetInstanceId = 0;
                    actionState.phase = CreatureActionPhase.Searching;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.SearchingForFood;
                    actionTimer.elapsedTime = 0f;
                    actionTimer.remainingTime = -1f;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                }
                else if (creature.MovementManager.IsTargetReached(targetPosition))
                {
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                    actionState.phase = CreatureActionPhase.Executing;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = actionState.currentAction == CreatureAction.Hunt
                        ? CreatureStateType.Predation
                        : CreatureStateType.Eating;

                    if (actionState.currentAction == CreatureAction.SearchFood)
                    {
                        request.hasRequest = true;
                        request.requestedAction = CreatureAction.SearchFood;
                        request.requestedPhase = CreatureActionPhase.Executing;
                        request.targetInstanceId = actionTarget.targetInstanceId;
                        request.cancelRequested = false;
                        request.completeRequested = false;
                    }
                    else if (actionState.currentAction == CreatureAction.Hunt)
                    {
                        request.hasRequest = true;
                        request.requestedAction = CreatureAction.Hunt;
                        request.requestedPhase = CreatureActionPhase.Executing;
                        request.targetInstanceId = actionTarget.targetInstanceId;
                        request.cancelRequested = false;
                        request.completeRequested = false;
                    }
                }
                else
                {
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                    if (actionState.currentAction == CreatureAction.Hunt)
                    {
                        creature.MovementManager.TryStartSprint();
                    }

                    creature.MovementManager.MoveTowardsOrQueue(targetPosition, useEcsMovementExecution);
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToFood;
                }
            }
        }
    }

    private static float3 ToFloat3(Vector3 source)
    {
        return new float3(source.x, source.y, source.z);
    }

    private static Vector3 ToVector3(float3 source)
    {
        return new Vector3(source.x, source.y, source.z);
    }

    private static bool IsFoodSearchAction(CreatureAction action)
    {
        return action == CreatureAction.SearchFood || action == CreatureAction.Hunt;
    }

    private static int SelectTargetInstanceId(
        BaseCreatureBehaviour creature,
        CreatureIdentity identity,
        CreatureObservationResultData observation,
        CreatureAction action)
    {
        if (action == CreatureAction.Hunt)
        {
            int preyId = observation.closestPreyInstanceId;
            if (!IsValidPreyTarget(creature, identity.gameObjectInstanceId, preyId))
                preyId = SelectObservedCreatureTarget(creature, ObservationType.FoodCreature);

            if (!IsValidPreyTarget(creature, identity.gameObjectInstanceId, preyId))
                return 0;

            return preyId;
        }

        int foodId = observation.closestFoodInstanceId;
        if (!IsValidFoodTarget(creature, foodId))
            foodId = SelectObservedFoodTarget(creature);

        return foodId;
    }

    private static int SelectObservedFoodTarget(BaseCreatureBehaviour creature)
    {
        var observations = creature?.ObservationManager?.Observations;
        if (observations == null)
            return 0;

        for (int i = 0; i < observations.Count; i++)
        {
            ObservationData observation = observations[i];
            if (observation.type != ObservationType.Food || observation.observedObject == null)
                continue;

            Food food = observation.observedObject.GetComponent<Food>();
            if (!IsValidFoodTarget(creature, food))
                continue;

            return food.GetInstanceID();
        }

        return 0;
    }

    private static bool IsValidFoodTarget(BaseCreatureBehaviour creature, int foodId)
    {
        if (foodId == 0 ||
            !ECSMirrorBridge.TryGetFoodByInstanceId(foodId, out Food food) ||
            food == null)
        {
            return false;
        }

        return IsValidFoodTarget(creature, food);
    }

    private static bool IsValidFoodTarget(BaseCreatureBehaviour creature, Food food)
    {
        if (creature == null ||
            food == null ||
            !food.gameObject.activeInHierarchy ||
            food.IsDespawnQueued)
        {
            return false;
        }

        if (creature.EatingManager != null && creature.EatingManager.IsFoodBlacklisted(food))
            return false;

        int creatureInstanceId = creature.GetInstanceID();
        if (creatureInstanceId == 0)
            return false;

        return CanTargetFoodLock(creatureInstanceId, food);
    }

    private static bool CanTargetFoodLock(
        int creatureInstanceId,
        Food food)
    {
        bool lockFound = ECSMirrorBridge.TryGetFoodLockData(
            food.GetInstanceID(),
            out bool isBeingEaten,
            out int eatingCreatureInstanceId);

        if (!lockFound)
        {
            isBeingEaten = food.IsBeingEaten;
            BaseCreatureBehaviour eatingCreature = food.GetEatingCreature();
            eatingCreatureInstanceId = eatingCreature != null ? eatingCreature.GetInstanceID() : 0;
        }

        if (!isBeingEaten)
            return true;

        return eatingCreatureInstanceId != 0 && eatingCreatureInstanceId == creatureInstanceId;
    }

    private static int SelectObservedCreatureTarget(BaseCreatureBehaviour creature, ObservationType observationType)
    {
        var observations = creature?.ObservationManager?.Observations;
        if (observations == null)
            return 0;

        for (int i = 0; i < observations.Count; i++)
        {
            ObservationData observation = observations[i];
            if (observation.type != observationType || observation.observedObject == null)
                continue;

            BaseCreatureBehaviour target = observation.observedObject.GetComponent<BaseCreatureBehaviour>();
            if (target == null ||
                target == creature ||
                !target.gameObject.activeInHierarchy ||
                target.IsDespawnQueued)
            {
                continue;
            }

            if (target is HerbivoreBehaviour herbivore && herbivore.IsCaptured && !herbivore.IsCapturedBy(creature))
                continue;

            if (creature is PredatorBehaviour predator && predator.IsPreyBlacklisted(target))
                continue;

            return target.GetInstanceID();
        }

        return 0;
    }

    private static bool IsValidPreyTarget(BaseCreatureBehaviour creature, int creatureInstanceId, int preyId)
    {
        if (creature == null || preyId == 0 || preyId == creatureInstanceId)
            return false;

        if (!ECSMirrorBridge.TryGetCreatureByInstanceId(preyId, out BaseCreatureBehaviour prey) ||
            prey == null ||
            prey == creature ||
            !prey.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (prey is HerbivoreBehaviour herbivore && herbivore.IsCaptured && !herbivore.IsCapturedBy(creature))
            return false;

        if (creature is PredatorBehaviour predator && predator.IsPreyBlacklisted(prey))
            return false;

        return true;
    }

    private static bool TryAbandonHopelessPredatorChase(
        BaseCreatureBehaviour creature,
        int preyInstanceId,
        float chaseElapsedTime,
        GameConfig config)
    {
        if (!(creature is PredatorBehaviour predator) || preyInstanceId == 0)
            return false;

        if (!ECSMirrorBridge.TryGetCreatureByInstanceId(preyInstanceId, out BaseCreatureBehaviour prey) ||
            prey == null)
        {
            return false;
        }

        float maxChaseDuration = config != null ? Mathf.Max(0f, config.predatorMaxChaseDuration) : 0f;
        bool chaseTimedOut = maxChaseDuration > 0f && chaseElapsedTime >= maxChaseDuration;

        bool preyLikelyTooFast = false;
        if (!chaseTimedOut &&
            config != null &&
            predator.MovementManager != null &&
            prey.MovementManager != null)
        {
            float minChaseTimeBeforeSpeedCheck = Mathf.Max(0f, config.predatorMinChaseTimeBeforeSpeedCheck);
            float speedGiveUpFactor = Mathf.Max(1f, config.predatorPreySpeedGiveUpFactor);

            if (speedGiveUpFactor > 1f && chaseElapsedTime >= minChaseTimeBeforeSpeedCheck)
            {
                float predatorSpeed = Mathf.Max(0.01f, predator.MovementManager.MoveSpeed);
                float preySpeed = Mathf.Max(0.01f, prey.MovementManager.MoveSpeed);
                preyLikelyTooFast = preySpeed > predatorSpeed * speedGiveUpFactor;
            }
        }

        if (!chaseTimedOut && !preyLikelyTooFast)
            return false;

        if (preyLikelyTooFast)
            predator.BlacklistPrey(prey);

        return true;
    }

    private static bool TryGetTargetPosition(
        BaseCreatureBehaviour creature,
        CreatureAction action,
        int targetInstanceId,
        out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;
        if (targetInstanceId == 0)
            return false;

        if (action == CreatureAction.Hunt)
        {
            if (!ECSMirrorBridge.TryGetCreatureByInstanceId(targetInstanceId, out BaseCreatureBehaviour prey) ||
                prey == null ||
                prey == creature ||
                !prey.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (prey is HerbivoreBehaviour herbivore && herbivore.IsCaptured && !herbivore.IsCapturedBy(creature))
                return false;

            if (creature is PredatorBehaviour predator && predator.IsPreyBlacklisted(prey))
                return false;

            targetPosition = prey.transform.position;
            return true;
        }

        if (!ECSMirrorBridge.TryGetFoodByInstanceId(targetInstanceId, out Food food) ||
            food == null ||
            !food.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (creature.EatingManager != null && creature.EatingManager.IsFoodBlacklisted(food))
            return false;

        targetPosition = food.transform.position;
        return true;
    }
}
