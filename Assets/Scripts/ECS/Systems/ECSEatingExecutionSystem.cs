using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSFoodSearchExecutionSystem))]
public partial class ECSEatingExecutionSystem : SystemBase
{
    private EntityQuery creatureQuery;
    private EntityQuery foodQuery;
    private readonly Dictionary<int, int> creatureIndexByInstanceId = new Dictionary<int, int>();
    private readonly Dictionary<int, int> foodIndexByInstanceId = new Dictionary<int, int>();
    private readonly HashSet<int> activeFoodConsumerInstanceIds = new HashSet<int>();

    protected override void OnCreate()
    {
        creatureQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
            ComponentType.ReadWrite<CreatureActionRequestData>(),
            ComponentType.ReadWrite<CreatureActionStateData>(),
            ComponentType.ReadWrite<CreatureActionTargetData>(),
            ComponentType.ReadWrite<CreatureActionTimerData>());

        foodQuery = GetEntityQuery(ComponentType.ReadWrite<FoodMirrorData>());

        RequireForUpdate(creatureQuery);
        RequireForUpdate(foodQuery);
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null)
            return;

        float deltaTime = (float)World.Time.DeltaTime;
        EntityManager entityManager = EntityManager;

        NativeArray<Entity> creatureEntities = default;
        NativeArray<CreatureIdentity> creatureIdentities = default;
        NativeArray<CreatureActionRequestData> actionRequests = default;
        NativeArray<CreatureActionStateData> actionStates = default;
        NativeArray<CreatureActionTargetData> actionTargets = default;
        NativeArray<CreatureActionTimerData> actionTimers = default;
        NativeArray<Entity> foodEntities = default;
        NativeArray<FoodMirrorData> foodMirrors = default;

        try
        {
            creatureEntities = creatureQuery.ToEntityArray(Allocator.Temp);
            creatureIdentities = creatureQuery.ToComponentDataArray<CreatureIdentity>(Allocator.Temp);
            actionRequests = creatureQuery.ToComponentDataArray<CreatureActionRequestData>(Allocator.Temp);
            actionStates = creatureQuery.ToComponentDataArray<CreatureActionStateData>(Allocator.Temp);
            actionTargets = creatureQuery.ToComponentDataArray<CreatureActionTargetData>(Allocator.Temp);
            actionTimers = creatureQuery.ToComponentDataArray<CreatureActionTimerData>(Allocator.Temp);

            foodEntities = foodQuery.ToEntityArray(Allocator.Temp);
            foodMirrors = foodQuery.ToComponentDataArray<FoodMirrorData>(Allocator.Temp);

            creatureIndexByInstanceId.Clear();
            for (int i = 0; i < creatureIdentities.Length; i++)
            {
                creatureIndexByInstanceId[creatureIdentities[i].gameObjectInstanceId] = i;
            }

            foodIndexByInstanceId.Clear();
            for (int i = 0; i < foodMirrors.Length; i++)
            {
                foodIndexByInstanceId[foodMirrors[i].gameObjectInstanceId] = i;
            }

            activeFoodConsumerInstanceIds.Clear();
            for (int i = 0; i < creatureIdentities.Length; i++)
            {
                if (!IsFoodConsumingAction(actionRequests[i], actionStates[i]))
                    continue;

                int creatureInstanceId = creatureIdentities[i].gameObjectInstanceId;
                if (creatureInstanceId == 0)
                    continue;

                if (!ECSMirrorBridge.TryGetCreatureByInstanceId(creatureInstanceId, out BaseCreatureBehaviour consumer) ||
                    consumer == null ||
                    consumer.IsDespawnQueued ||
                    !consumer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeFoodConsumerInstanceIds.Add(creatureInstanceId);
            }

            ReleaseStaleFoodLocks(foodEntities, foodMirrors, activeFoodConsumerInstanceIds, entityManager);

            for (int creatureIndex = 0; creatureIndex < creatureEntities.Length; creatureIndex++)
            {
                Entity creatureEntity = creatureEntities[creatureIndex];
                if (!entityManager.Exists(creatureEntity))
                    continue;

                CreatureIdentity identity = creatureIdentities[creatureIndex];
                CreatureActionRequestData request = actionRequests[creatureIndex];
                CreatureActionStateData actionState = actionStates[creatureIndex];
                CreatureActionTargetData actionTarget = actionTargets[creatureIndex];
                CreatureActionTimerData actionTimer = actionTimers[creatureIndex];

                bool isEatingAction =
                    actionState.currentAction == CreatureAction.SearchFood &&
                    actionState.phase == CreatureActionPhase.Executing;
                bool hasEatingExecuteRequest =
                    request.hasRequest &&
                    request.requestedAction == CreatureAction.SearchFood &&
                    request.requestedPhase == CreatureActionPhase.Executing;

                if (!isEatingAction && !hasEatingExecuteRequest)
                    continue;

                if (!ECSMirrorBridge.TryGetCreatureByInstanceId(identity.gameObjectInstanceId, out BaseCreatureBehaviour creature) ||
                    creature == null ||
                    creature.EnergyManager == null)
                {
                    continue;
                }

                int targetFoodId = hasEatingExecuteRequest && request.targetInstanceId != 0
                    ? request.targetInstanceId
                    : actionTarget.targetInstanceId;

                if (request.cancelRequested)
                {
                    ReleaseFoodLockIfOwnedByCreature(
                        targetFoodId,
                        identity.gameObjectInstanceId,
                        foodIndexByInstanceId,
                        foodEntities,
                        foodMirrors,
                        entityManager);
                    FinalizeActionCancelled(ref request, ref actionState, ref actionTarget, ref actionTimer);
                    WriteCreatureAction(entityManager, creatureEntity, request, actionState, actionTarget, actionTimer);
                    continue;
                }

                if (creature.EnergyManager.EnergyLevel >= creature.maxEnergy)
                {
                    FinalizeActionCompleted(ref request, ref actionState, ref actionTarget, ref actionTimer);
                    WriteCreatureAction(entityManager, creatureEntity, request, actionState, actionTarget, actionTimer);
                    continue;
                }

                if (!TryFindFoodIndexByInstanceId(foodIndexByInstanceId, targetFoodId, out int foodIndex))
                {
                    ReleaseFoodLockIfOwnedByCreature(
                        targetFoodId,
                        identity.gameObjectInstanceId,
                        foodIndexByInstanceId,
                        foodEntities,
                        foodMirrors,
                        entityManager);
                    FinalizeActionCancelled(ref request, ref actionState, ref actionTarget, ref actionTimer);
                    WriteCreatureAction(entityManager, creatureEntity, request, actionState, actionTarget, actionTimer);
                    continue;
                }

                FoodMirrorData foodData = foodMirrors[foodIndex];
                Entity foodEntity = foodEntities[foodIndex];
                if (!entityManager.Exists(foodEntity))
                    continue;

                if (foodData.nutritionValue <= 0f)
                {
                    foodData.isBeingEaten = false;
                    foodData.eatingCreatureInstanceId = 0;
                    foodMirrors[foodIndex] = foodData;
                    entityManager.SetComponentData(foodEntity, foodData);
                    CreateFoodDepletionRequest(entityManager, targetFoodId);
                    FinalizeActionCompleted(ref request, ref actionState, ref actionTarget, ref actionTimer);
                    WriteCreatureAction(entityManager, creatureEntity, request, actionState, actionTarget, actionTimer);
                    continue;
                }

                bool hasLock = TryAcquireFoodLock(
                    creature,
                    targetFoodId,
                    ref foodData,
                    actionRequests,
                    actionStates,
                    actionTargets,
                    actionTimers,
                    creatureEntities,
                    creatureIndexByInstanceId,
                    entityManager);

                if (!hasLock)
                {
                    if (ECSMirrorBridge.TryGetFoodByInstanceId(targetFoodId, out Food targetFood) &&
                        targetFood != null &&
                        creature.EatingManager != null)
                    {
                        creature.EatingManager.BlacklistFood(targetFood);
                    }

                    FinalizeActionCancelled(ref request, ref actionState, ref actionTarget, ref actionTimer);
                    WriteCreatureAction(entityManager, creatureEntity, request, actionState, actionTarget, actionTimer);
                    continue;
                }

                actionState.currentAction = CreatureAction.SearchFood;
                actionState.phase = CreatureActionPhase.Executing;
                actionState.status = CreatureActionStatus.Running;
                actionState.legacyStateType = CreatureStateType.Eating;
                actionState.canCancel = true;
                actionState.canComplete = true;
                actionTarget.targetInstanceId = targetFoodId;

                actionTimer.elapsedTime += deltaTime;
                actionTimer.remainingTime = -1f;

                float desiredNutrition = Mathf.Max(0f, config.nutritionConsumptionRatePerSecond) * deltaTime;
                float missingEnergy = Mathf.Max(0f, creature.maxEnergy - creature.EnergyManager.EnergyLevel);
                float energyDesiredThisFrame = Mathf.Min(desiredNutrition, missingEnergy);
                float actualNutrition = Mathf.Min(energyDesiredThisFrame, Mathf.Max(0f, foodData.nutritionValue));

                if (actualNutrition > 0f)
                {
                    creature.EnergyManager.GainEnergy(actualNutrition);
                    foodData.nutritionValue = Mathf.Max(0f, foodData.nutritionValue - actualNutrition);
                }

                foodData.nutritionPercent = foodData.maxNutritionValue > 0f
                    ? Mathf.Clamp01(foodData.nutritionValue / foodData.maxNutritionValue)
                    : 0f;
                foodMirrors[foodIndex] = foodData;
                entityManager.SetComponentData(foodEntity, foodData);

                bool reachedMaxEnergy = creature.EnergyManager.EnergyLevel >= creature.maxEnergy;
                bool depleted = foodData.nutritionValue <= 0f;
                bool ownsFoodLock = foodData.eatingCreatureInstanceId == identity.gameObjectInstanceId;

                if (depleted)
                {
                    foodData.isBeingEaten = false;
                    foodData.eatingCreatureInstanceId = 0;
                    foodMirrors[foodIndex] = foodData;
                    entityManager.SetComponentData(foodEntity, foodData);
                    CreateFoodDepletionRequest(entityManager, targetFoodId);
                    FinalizeActionCompleted(ref request, ref actionState, ref actionTarget, ref actionTimer);
                }
                else if (reachedMaxEnergy)
                {
                    if (ownsFoodLock)
                    {
                        foodData.isBeingEaten = false;
                        foodData.eatingCreatureInstanceId = 0;
                        foodMirrors[foodIndex] = foodData;
                        entityManager.SetComponentData(foodEntity, foodData);
                    }

                    FinalizeActionCompleted(ref request, ref actionState, ref actionTarget, ref actionTimer);
                }
                else
                {
                    ClearActionRequest(ref request);
                }

                WriteCreatureAction(entityManager, creatureEntity, request, actionState, actionTarget, actionTimer);
            }
        }
        finally
        {
            if (foodMirrors.IsCreated) foodMirrors.Dispose();
            if (foodEntities.IsCreated) foodEntities.Dispose();
            if (actionTimers.IsCreated) actionTimers.Dispose();
            if (actionTargets.IsCreated) actionTargets.Dispose();
            if (actionStates.IsCreated) actionStates.Dispose();
            if (actionRequests.IsCreated) actionRequests.Dispose();
            if (creatureIdentities.IsCreated) creatureIdentities.Dispose();
            if (creatureEntities.IsCreated) creatureEntities.Dispose();
        }
    }

    private static bool TryFindFoodIndexByInstanceId(
        Dictionary<int, int> foodIndexByInstanceId,
        int targetFoodId,
        out int foodIndex)
    {
        foodIndex = default;
        if (targetFoodId == 0)
            return false;

        return foodIndexByInstanceId.TryGetValue(targetFoodId, out foodIndex);
    }

    private static bool IsFoodConsumingAction(
        CreatureActionRequestData request,
        CreatureActionStateData actionState)
    {
        bool isEatingAction =
            actionState.currentAction == CreatureAction.SearchFood &&
            actionState.phase == CreatureActionPhase.Executing;
        bool hasEatingExecuteRequest =
            request.hasRequest &&
            request.requestedAction == CreatureAction.SearchFood &&
            request.requestedPhase == CreatureActionPhase.Executing;

        return isEatingAction || hasEatingExecuteRequest;
    }

    private static void ReleaseStaleFoodLocks(
        NativeArray<Entity> foodEntities,
        NativeArray<FoodMirrorData> foodMirrors,
        HashSet<int> activeFoodConsumerInstanceIds,
        EntityManager entityManager)
    {
        for (int i = 0; i < foodMirrors.Length; i++)
        {
            FoodMirrorData foodData = foodMirrors[i];
            if (!foodData.isBeingEaten)
                continue;

            int ownerId = foodData.eatingCreatureInstanceId;
            if (ownerId != 0 && activeFoodConsumerInstanceIds.Contains(ownerId))
                continue;

            foodData.isBeingEaten = false;
            foodData.eatingCreatureInstanceId = 0;
            foodMirrors[i] = foodData;

            Entity foodEntity = foodEntities[i];
            if (entityManager.Exists(foodEntity))
            {
                entityManager.SetComponentData(foodEntity, foodData);
            }
        }
    }

    private static void ReleaseFoodLockIfOwnedByCreature(
        int targetFoodId,
        int creatureInstanceId,
        Dictionary<int, int> foodIndexByInstanceId,
        NativeArray<Entity> foodEntities,
        NativeArray<FoodMirrorData> foodMirrors,
        EntityManager entityManager)
    {
        if (targetFoodId == 0 || creatureInstanceId == 0)
            return;

        if (!TryFindFoodIndexByInstanceId(foodIndexByInstanceId, targetFoodId, out int foodIndex))
            return;

        FoodMirrorData foodData = foodMirrors[foodIndex];
        if (!foodData.isBeingEaten || foodData.eatingCreatureInstanceId != creatureInstanceId)
            return;

        foodData.isBeingEaten = false;
        foodData.eatingCreatureInstanceId = 0;
        foodMirrors[foodIndex] = foodData;

        Entity foodEntity = foodEntities[foodIndex];
        if (entityManager.Exists(foodEntity))
        {
            entityManager.SetComponentData(foodEntity, foodData);
        }
    }

    private static bool TryAcquireFoodLock(
        BaseCreatureBehaviour creature,
        int targetFoodId,
        ref FoodMirrorData foodData,
        NativeArray<CreatureActionRequestData> actionRequests,
        NativeArray<CreatureActionStateData> actionStates,
        NativeArray<CreatureActionTargetData> actionTargets,
        NativeArray<CreatureActionTimerData> actionTimers,
        NativeArray<Entity> creatureEntities,
        Dictionary<int, int> creatureIndexByInstanceId,
        EntityManager entityManager)
    {
        int currentCreatureId = creature.GetInstanceID();
        if (!foodData.isBeingEaten || foodData.eatingCreatureInstanceId == 0 || foodData.eatingCreatureInstanceId == currentCreatureId)
        {
            foodData.isBeingEaten = true;
            foodData.eatingCreatureInstanceId = currentCreatureId;
            return true;
        }

        int previousCreatureId = foodData.eatingCreatureInstanceId;
        if (!ECSMirrorBridge.TryGetCreatureByInstanceId(previousCreatureId, out BaseCreatureBehaviour previousCreature) ||
            previousCreature == null ||
            previousCreature.IsDespawnQueued ||
            !previousCreature.gameObject.activeInHierarchy)
        {
            foodData.isBeingEaten = true;
            foodData.eatingCreatureInstanceId = currentCreatureId;
            return true;
        }

        if (TryResolveHerbivoreFoodCompetition(
                creature,
                previousCreature,
                ref foodData,
                out bool challengerCanEat,
                out bool shouldTakeOver))
        {
            if (!challengerCanEat)
                return false;

            if (!shouldTakeOver)
                return true;

            EvictCurrentFoodOwner(
                targetFoodId,
                previousCreatureId,
                actionRequests,
                actionStates,
                actionTargets,
                actionTimers,
                creatureEntities,
                creatureIndexByInstanceId,
                entityManager);

            foodData.isBeingEaten = true;
            foodData.eatingCreatureInstanceId = currentCreatureId;
            return true;
        }

        float threshold = previousCreature.Weight * GameConfig.Instance.sizeDifferentFactor;
        if (creature.Weight < threshold)
            return false;

        EvictCurrentFoodOwner(
            targetFoodId,
            previousCreatureId,
            actionRequests,
            actionStates,
            actionTargets,
            actionTimers,
            creatureEntities,
            creatureIndexByInstanceId,
            entityManager);

        foodData.isBeingEaten = true;
        foodData.eatingCreatureInstanceId = currentCreatureId;
        return true;
    }

    private static bool TryResolveHerbivoreFoodCompetition(
        BaseCreatureBehaviour challenger,
        BaseCreatureBehaviour owner,
        ref FoodMirrorData foodData,
        out bool challengerCanEat,
        out bool shouldTakeOver)
    {
        challengerCanEat = false;
        shouldTakeOver = false;

        if (!(challenger is HerbivoreBehaviour challengerHerbivore) ||
            !(owner is HerbivoreBehaviour ownerHerbivore))
        {
            return false;
        }

        if (!challengerHerbivore.IsHawk && !ownerHerbivore.IsHawk)
        {
            // Keep single-owner semantics: dove challengers back off instead of consuming in parallel.
            challengerCanEat = false;
            shouldTakeOver = false;
            return true;
        }

        if (!challengerHerbivore.IsHawk && ownerHerbivore.IsHawk)
        {
            challengerCanEat = false;
            shouldTakeOver = false;
            return true;
        }

        if (challengerHerbivore.IsHawk && !ownerHerbivore.IsHawk)
        {
            challengerCanEat = true;
            shouldTakeOver = true;
            return true;
        }

        BaseCreatureBehaviour winner = HerbivoreSocialDynamics.ResolveHawkFight(challenger, owner);
        foodData.nutritionValue = Mathf.Max(0f, foodData.nutritionValue * HerbivoreSocialDynamics.HawkFightFoodRetentionFactor);
        foodData.nutritionPercent = foodData.maxNutritionValue > 0f
            ? Mathf.Clamp01(foodData.nutritionValue / foodData.maxNutritionValue)
            : 0f;

        if (winner == challenger)
        {
            challengerCanEat = !challenger.IsDespawnQueued;
            shouldTakeOver = challengerCanEat;
            return true;
        }

        if (winner == owner)
        {
            challengerCanEat = false;
            shouldTakeOver = false;
            return true;
        }

        foodData.isBeingEaten = false;
        foodData.eatingCreatureInstanceId = 0;
        challengerCanEat = false;
        shouldTakeOver = false;
        return true;
    }

    private static void EvictCurrentFoodOwner(
        int targetFoodId,
        int previousCreatureId,
        NativeArray<CreatureActionRequestData> actionRequests,
        NativeArray<CreatureActionStateData> actionStates,
        NativeArray<CreatureActionTargetData> actionTargets,
        NativeArray<CreatureActionTimerData> actionTimers,
        NativeArray<Entity> creatureEntities,
        Dictionary<int, int> creatureIndexByInstanceId,
        EntityManager entityManager)
    {
        if (ECSMirrorBridge.TryGetFoodByInstanceId(targetFoodId, out Food foodObject) &&
            foodObject != null &&
            ECSMirrorBridge.TryGetCreatureByInstanceId(previousCreatureId, out BaseCreatureBehaviour previousCreature) &&
            previousCreature?.EatingManager != null &&
            !previousCreature.IsDespawnQueued)
        {
            previousCreature.EatingManager.BlacklistFood(foodObject);
        }

        if (creatureIndexByInstanceId.TryGetValue(previousCreatureId, out int previousIndex) &&
            previousIndex >= 0 &&
            previousIndex < creatureEntities.Length)
        {
            CreatureActionRequestData previousRequest = actionRequests[previousIndex];
            CreatureActionStateData previousState = actionStates[previousIndex];
            CreatureActionTargetData previousTarget = actionTargets[previousIndex];
            CreatureActionTimerData previousTimer = actionTimers[previousIndex];

            FinalizeActionCancelled(ref previousRequest, ref previousState, ref previousTarget, ref previousTimer);
            Entity previousEntity = creatureEntities[previousIndex];
            WriteCreatureAction(entityManager, previousEntity, previousRequest, previousState, previousTarget, previousTimer);
        }
    }

    private static void CreateFoodDepletionRequest(EntityManager entityManager, int foodInstanceId)
    {
        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new DespawnFoodRequest
        {
            gameObjectInstanceId = foodInstanceId,
            reason = (int)FoodDespawnReason.Depletion
        });
    }

    private static void FinalizeActionCompleted(
        ref CreatureActionRequestData request,
        ref CreatureActionStateData actionState,
        ref CreatureActionTargetData actionTarget,
        ref CreatureActionTimerData actionTimer)
    {
        SetActionEnded(ref actionState, ref actionTarget, ref actionTimer, CreatureActionStatus.Completed);
        SetWanderRequest(ref request);
    }

    private static void FinalizeActionCancelled(
        ref CreatureActionRequestData request,
        ref CreatureActionStateData actionState,
        ref CreatureActionTargetData actionTarget,
        ref CreatureActionTimerData actionTimer)
    {
        SetActionEnded(ref actionState, ref actionTarget, ref actionTimer, CreatureActionStatus.Cancelled);
        SetWanderRequest(ref request);
    }

    private static void SetActionEnded(
        ref CreatureActionStateData actionState,
        ref CreatureActionTargetData actionTarget,
        ref CreatureActionTimerData actionTimer,
        CreatureActionStatus status)
    {
        actionState.currentAction = CreatureAction.None;
        actionState.phase = CreatureActionPhase.Idle;
        actionState.status = status;
        actionState.legacyStateType = CreatureStateType.Wandering;
        actionState.canCancel = false;
        actionState.canComplete = false;
        actionTarget.targetInstanceId = 0;
        actionTimer.remainingTime = -1f;
    }

    private static void SetWanderRequest(ref CreatureActionRequestData request)
    {
        request.hasRequest = true;
        request.requestedAction = CreatureAction.Wander;
        request.requestedPhase = CreatureActionPhase.Wandering;
        request.targetInstanceId = 0;
        request.cancelRequested = false;
        request.completeRequested = false;
    }

    private static void ClearActionRequest(ref CreatureActionRequestData request)
    {
        request.hasRequest = false;
        request.requestedAction = CreatureAction.None;
        request.requestedPhase = CreatureActionPhase.None;
        request.targetInstanceId = 0;
        request.cancelRequested = false;
        request.completeRequested = false;
    }

    private static void WriteCreatureAction(
        EntityManager entityManager,
        Entity entity,
        CreatureActionRequestData request,
        CreatureActionStateData actionState,
        CreatureActionTargetData actionTarget,
        CreatureActionTimerData actionTimer)
    {
        if (!entityManager.Exists(entity))
            return;

        entityManager.SetComponentData(entity, request);
        entityManager.SetComponentData(entity, actionState);
        entityManager.SetComponentData(entity, actionTarget);
        entityManager.SetComponentData(entity, actionTimer);
    }
}
