using Unity.Collections;
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
        EntityManager entityManager = EntityManager;

        using NativeArray<Entity> entities = executionQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<CreatureIdentity> identities = executionQuery.ToComponentDataArray<CreatureIdentity>(Allocator.Temp);
        using NativeArray<CreatureObservationResultData> observations = executionQuery.ToComponentDataArray<CreatureObservationResultData>(Allocator.Temp);
        using NativeArray<CreatureActionRequestData> requests = executionQuery.ToComponentDataArray<CreatureActionRequestData>(Allocator.Temp);
        using NativeArray<CreatureActionStateData> actionStates = executionQuery.ToComponentDataArray<CreatureActionStateData>(Allocator.Temp);
        using NativeArray<CreatureActionTargetData> actionTargets = executionQuery.ToComponentDataArray<CreatureActionTargetData>(Allocator.Temp);
        using NativeArray<CreatureActionTimerData> actionTimers = executionQuery.ToComponentDataArray<CreatureActionTimerData>(Allocator.Temp);
        using NativeArray<CreatureWanderExecutionData> wanderStates = executionQuery.ToComponentDataArray<CreatureWanderExecutionData>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            if (!entityManager.Exists(entity))
                continue;

            CreatureIdentity identity = identities[i];
            CreatureObservationResultData observation = observations[i];
            CreatureActionRequestData request = requests[i];
            CreatureActionStateData actionState = actionStates[i];
            CreatureActionTargetData actionTarget = actionTargets[i];
            CreatureActionTimerData actionTimer = actionTimers[i];
            CreatureWanderExecutionData wanderState = wanderStates[i];

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

                entityManager.SetComponentData(entity, actionState);
                entityManager.SetComponentData(entity, actionTarget);
                entityManager.SetComponentData(entity, actionTimer);
                entityManager.SetComponentData(entity, wanderState);
                continue;
            }

            if (hasSearchRequest)
            {
                request.hasRequest = false;
                request.cancelRequested = false;
                request.completeRequested = false;
                entityManager.SetComponentData(entity, request);

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
                entityManager.SetComponentData(entity, actionState);
                entityManager.SetComponentData(entity, actionTarget);
                entityManager.SetComponentData(entity, actionTimer);
                entityManager.SetComponentData(entity, wanderState);
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

                        creature.MovementManager.MoveTowards(ToVector3(wanderState.targetPosition));
                    }
                }
            }

            if (actionState.phase == CreatureActionPhase.MovingToTarget)
            {
                if (!TryGetTargetPosition(creature, actionState.currentAction, actionTarget.targetInstanceId, out Vector3 targetPosition))
                {
                    actionTarget.targetInstanceId = 0;
                    actionState.phase = CreatureActionPhase.Searching;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.SearchingForFood;
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
                        entityManager.SetComponentData(entity, request);
                    }
                    else if (actionState.currentAction == CreatureAction.Hunt)
                    {
                        request.hasRequest = true;
                        request.requestedAction = CreatureAction.Hunt;
                        request.requestedPhase = CreatureActionPhase.Executing;
                        request.targetInstanceId = actionTarget.targetInstanceId;
                        request.cancelRequested = false;
                        request.completeRequested = false;
                        entityManager.SetComponentData(entity, request);
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

                    creature.MovementManager.MoveTowards(targetPosition);
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToFood;
                }
            }

            entityManager.SetComponentData(entity, actionState);
            entityManager.SetComponentData(entity, actionTarget);
            entityManager.SetComponentData(entity, actionTimer);
            entityManager.SetComponentData(entity, wanderState);
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
            if (preyId == 0)
                preyId = SelectObservedCreatureTarget(creature, ObservationType.FoodCreature);

            if (preyId == 0 || preyId == identity.gameObjectInstanceId)
                return 0;

            if (!ECSMirrorBridge.TryGetCreatureByInstanceId(preyId, out BaseCreatureBehaviour prey) ||
                prey == null ||
                prey == creature ||
                !prey.gameObject.activeInHierarchy)
            {
                return 0;
            }

            if (prey is HerbivoreBehaviour herbivore && herbivore.IsCaptured && !herbivore.IsCapturedBy(creature))
                return 0;

            if (creature is PredatorBehaviour predator && predator.IsPreyBlacklisted(prey))
                return 0;

            return preyId;
        }

        int foodId = observation.closestFoodInstanceId;
        if (foodId == 0)
            foodId = SelectObservedFoodTarget(creature);

        if (foodId == 0)
            return 0;

        if (!ECSMirrorBridge.TryGetFoodByInstanceId(foodId, out Food food) ||
            food == null ||
            !food.gameObject.activeInHierarchy)
        {
            return 0;
        }

        if (creature.EatingManager != null && creature.EatingManager.IsFoodBlacklisted(food))
            return 0;

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
            if (food == null ||
                !food.gameObject.activeInHierarchy ||
                food.IsDespawnQueued ||
                (creature.EatingManager != null && creature.EatingManager.IsFoodBlacklisted(food)))
            {
                continue;
            }

            return food.GetInstanceID();
        }

        return 0;
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
