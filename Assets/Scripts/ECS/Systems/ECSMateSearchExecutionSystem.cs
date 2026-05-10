using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSObservationSystem))]
public partial class ECSMateSearchExecutionSystem : SystemBase
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
        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useEcsActionExecution)
            return;

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
                request.requestedAction == CreatureAction.SearchMate &&
                request.requestedPhase == CreatureActionPhase.Searching;
            bool hasBlockingRequest = request.hasRequest && request.requestedAction != CreatureAction.SearchMate;
            bool isSearchMateAction = actionState.currentAction == CreatureAction.SearchMate;

            if (hasBlockingRequest)
            {
                if (isSearchMateAction)
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

                actionState.currentAction = CreatureAction.SearchMate;
                actionState.phase = CreatureActionPhase.Searching;
                actionState.status = CreatureActionStatus.Running;
                actionState.legacyStateType = CreatureStateType.SearchingForMate;
                actionState.canCancel = true;
                actionState.canComplete = true;

                actionTarget.targetInstanceId = 0;
                actionTimer.elapsedTime = 0f;
                actionTimer.remainingTime = -1f;
                wanderState.hasTarget = false;
                wanderState.targetPosition = float3.zero;
                isSearchMateAction = true;
            }

            if (!isSearchMateAction)
                continue;

            actionTimer.elapsedTime += deltaTime;
            actionTimer.remainingTime = -1f;

            if (!ECSMirrorBridge.TryGetCreatureByInstanceId(identity.gameObjectInstanceId, out BaseCreatureBehaviour creature) ||
                creature == null ||
                creature.MovementManager == null ||
                creature.ReproductionManager == null)
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
                int mateId = SelectMateTargetInstanceId(creature, identity, observation);
                actionTarget.targetInstanceId = mateId;
                if (mateId != 0)
                {
                    actionState.phase = CreatureActionPhase.MovingToTarget;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToMate;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                }
                else
                {
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.SearchingForMate;

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
                        creature.MovementManager.MoveTowards(ToVector3(wanderState.targetPosition));
                    }
                }
            }

            if (actionState.phase == CreatureActionPhase.MovingToTarget)
            {
                if (!TryGetMateTarget(creature, actionTarget.targetInstanceId, out BaseCreatureBehaviour mate))
                {
                    actionTarget.targetInstanceId = 0;
                    actionState.phase = CreatureActionPhase.Searching;
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.SearchingForMate;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                }
                else if (creature.MovementManager.IsTargetReached(mate.transform.position))
                {
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToMate;

                    request.hasRequest = true;
                    request.requestedAction = CreatureAction.SearchMate;
                    request.requestedPhase = CreatureActionPhase.Executing;
                    request.targetInstanceId = actionTarget.targetInstanceId;
                    request.cancelRequested = false;
                    request.completeRequested = false;
                    entityManager.SetComponentData(entity, request);
                }
                else
                {
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                    creature.MovementManager.MoveTowards(mate.transform.position);
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToMate;
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

    private static int SelectMateTargetInstanceId(
        BaseCreatureBehaviour creature,
        CreatureIdentity identity,
        CreatureObservationResultData observation)
    {
        int mateId = observation.closestMateInstanceId;
        if (mateId == 0 || mateId == identity.gameObjectInstanceId)
            return 0;

        if (!ECSMirrorBridge.TryGetCreatureByInstanceId(mateId, out BaseCreatureBehaviour mate) ||
            mate == null ||
            !mate.gameObject.activeInHierarchy ||
            mate == creature)
        {
            return 0;
        }

        return creature.ReproductionManager != null && creature.ReproductionManager.CanMateWith(mate)
            ? mateId
            : 0;
    }

    private static bool TryGetMateTarget(
        BaseCreatureBehaviour creature,
        int targetInstanceId,
        out BaseCreatureBehaviour mate)
    {
        mate = null;
        if (targetInstanceId == 0 || creature == null || creature.ReproductionManager == null)
            return false;

        if (!ECSMirrorBridge.TryGetCreatureByInstanceId(targetInstanceId, out mate) ||
            mate == null ||
            !mate.gameObject.activeInHierarchy ||
            mate == creature)
        {
            mate = null;
            return false;
        }

        if (!creature.ReproductionManager.CanMateWith(mate))
        {
            mate = null;
            return false;
        }

        return true;
    }
}
