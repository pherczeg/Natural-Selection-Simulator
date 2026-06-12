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

                continue;
            }

            if (hasSearchRequest)
            {
                request.hasRequest = false;
                request.cancelRequested = false;
                request.completeRequested = false;

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
                        creature.MovementManager.MoveTowardsOrQueue(ToVector3(wanderState.targetPosition), useEcsMovementExecution);
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
                }
                else
                {
                    wanderState.hasTarget = false;
                    wanderState.targetPosition = float3.zero;
                    creature.MovementManager.MoveTowardsOrQueue(mate.transform.position, useEcsMovementExecution);
                    actionState.status = CreatureActionStatus.Running;
                    actionState.legacyStateType = CreatureStateType.MovingToMate;
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

    private static int SelectMateTargetInstanceId(
        BaseCreatureBehaviour creature,
        CreatureIdentity identity,
        CreatureObservationResultData observation)
    {
        int mateId = observation.closestMateInstanceId;
        if (mateId == 0)
            mateId = SelectObservedMateTarget(creature);

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

    private static int SelectObservedMateTarget(BaseCreatureBehaviour creature)
    {
        var observations = creature?.ObservationManager?.Observations;
        if (observations == null || creature.ReproductionManager == null)
            return 0;

        for (int i = 0; i < observations.Count; i++)
        {
            ObservationData observation = observations[i];
            if (observation.type != ObservationType.MatingCreature || observation.observedObject == null)
                continue;

            BaseCreatureBehaviour mate = observation.observedObject.GetComponent<BaseCreatureBehaviour>();
            if (mate == null ||
                mate == creature ||
                !mate.gameObject.activeInHierarchy ||
                mate.IsDespawnQueued ||
                !creature.ReproductionManager.CanMateWith(mate))
            {
                continue;
            }

            return mate.GetInstanceID();
        }

        return 0;
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
