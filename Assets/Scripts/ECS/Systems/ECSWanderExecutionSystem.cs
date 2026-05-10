using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSUtilityScoringSystem))]
public partial class ECSWanderExecutionSystem : SystemBase
{
    private EntityQuery executionQuery;

    protected override void OnCreate()
    {
        executionQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
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
            CreatureActionRequestData request = requests[i];
            CreatureActionStateData actionState = actionStates[i];
            CreatureActionTargetData actionTarget = actionTargets[i];
            CreatureActionTimerData actionTimer = actionTimers[i];
            CreatureWanderExecutionData wanderState = wanderStates[i];

            bool hasWanderRequest = request.hasRequest && request.requestedAction == CreatureAction.Wander;
            bool hasBlockingRequest = request.hasRequest && request.requestedAction != CreatureAction.Wander;
            bool isWandering = actionState.currentAction == CreatureAction.Wander;

            if (hasBlockingRequest)
            {
                if (isWandering)
                {
                    actionState.status = CreatureActionStatus.Cancelled;
                    actionState.currentAction = CreatureAction.None;
                    actionState.phase = CreatureActionPhase.Idle;
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

            if (hasWanderRequest)
            {
                request.hasRequest = false;
                request.cancelRequested = false;
                request.completeRequested = false;
                entityManager.SetComponentData(entity, request);

                if (!isWandering)
                {
                    actionTimer.elapsedTime = 0f;
                    actionTimer.remainingTime = -1f;
                    actionTarget.targetInstanceId = 0;
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                }

                actionState.currentAction = CreatureAction.Wander;
                actionState.phase = CreatureActionPhase.Wandering;
                actionState.status = CreatureActionStatus.Running;
                actionState.legacyStateType = CreatureStateType.Wandering;
                actionState.canCancel = true;
                actionState.canComplete = true;
                isWandering = true;
            }

            if (!isWandering)
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

            Vector3 target = ToVector3(wanderState.targetPosition);
            if (!wanderState.hasTarget || creature.MovementManager.IsTargetReached(target))
            {
                target = creature.MovementManager.ChooseNewWanderTarget();
                if (target != Vector3.zero)
                {
                    wanderState.hasTarget = true;
                    wanderState.targetPosition = ToFloat3(target);
                }
                else
                {
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                    actionState.status = CreatureActionStatus.Blocked;
                    entityManager.SetComponentData(entity, actionState);
                    entityManager.SetComponentData(entity, actionTarget);
                    entityManager.SetComponentData(entity, actionTimer);
                    entityManager.SetComponentData(entity, wanderState);
                    continue;
                }
            }

            creature.MovementManager.MoveTowards(target);
            actionState.status = CreatureActionStatus.Running;

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
}
