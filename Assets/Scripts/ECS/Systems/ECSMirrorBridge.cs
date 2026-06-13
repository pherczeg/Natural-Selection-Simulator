using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class ECSMirrorBridge : MonoBehaviour
{
    private static readonly WaitForFixedUpdate WaitForFixedUpdateCached = new WaitForFixedUpdate();

    private static ECSMirrorBridge instance;

    private readonly Dictionary<int, Entity> creatureEntities = new Dictionary<int, Entity>();
    private readonly Dictionary<int, Entity> foodEntities = new Dictionary<int, Entity>();
    private readonly Dictionary<int, BaseCreatureBehaviour> activeCreaturesByInstanceId = new Dictionary<int, BaseCreatureBehaviour>();
    private readonly Dictionary<int, Food> activeFoodByInstanceId = new Dictionary<int, Food>();
    private readonly HashSet<int> unresolvedCreatureIds = new HashSet<int>();
    private readonly HashSet<int> unresolvedFoodIds = new HashSet<int>();
    private readonly HashSet<int> seenCreatureIds = new HashSet<int>();
    private readonly HashSet<int> seenFoodIds = new HashSet<int>();
    private readonly List<int> staleIds = new List<int>();
    private readonly Dictionary<int, int> reproductionPartnerByCreatureId = new Dictionary<int, int>();
    private readonly Dictionary<int, Coroutine> reproductionCoroutineByBirthOwnerId = new Dictionary<int, Coroutine>();
    private readonly Dictionary<int, int> predationPreyByPredatorId = new Dictionary<int, int>();
    private readonly Dictionary<int, CreatureUtilityDecisionData> utilityDecisionByCreatureId = new Dictionary<int, CreatureUtilityDecisionData>();
    private readonly Dictionary<int, UtilityAIContext> utilityContextByCreatureId = new Dictionary<int, UtilityAIContext>();

    private World mirroredWorld;
    private float lastEcsObservation;
    private float slowMirrorSyncTimer;
    private bool hasRunSlowMirrorSync;

    [Header("Mirror Debug")]
    [SerializeField] private int lastActiveCreatureCount;
    [SerializeField] private int lastActiveFoodCount;
    [SerializeField] private int lastMirroredCreatureCount;
    [SerializeField] private int lastMirroredFoodCount;
    [SerializeField] private int lastMirroredEntityCount;
    [SerializeField] private bool lastMirrorCountMatchesActiveObjects;
    [SerializeField] private string lastMirrorStatus;
    [SerializeField] private int lastEcsFoodObservationCount;
    [SerializeField] private int lastEcsPreyObservationCount;
    [SerializeField] private int lastEcsMateObservationCount;
    [SerializeField] private int lastEcsFoodLifecycleDespawnRequestCount;
    [SerializeField] private int lastEcsCreatureLifecycleDespawnRequestCount;

    public int LastActiveCreatureCount => lastActiveCreatureCount;
    public int LastActiveFoodCount => lastActiveFoodCount;
    public int LastMirroredCreatureCount => lastMirroredCreatureCount;
    public int LastMirroredFoodCount => lastMirroredFoodCount;
    public int LastMirroredEntityCount => lastMirroredEntityCount;
    public bool LastMirrorCountMatchesActiveObjects => lastMirrorCountMatchesActiveObjects;

    public static bool TryGetUtilityAIContext(BaseCreatureBehaviour creature, out UtilityAIContext context)
    {
        context = UtilityAIContext.Empty;
        if (instance == null || creature == null)
            return false;

        return instance.TryGetUtilityAIContextFromMirror(creature, out context);
    }

    public static bool TryGetUtilityAIDecision(
        BaseCreatureBehaviour creature,
        out CreatureUtilityDecisionData decisionData)
    {
        decisionData = default;
        if (instance == null || creature == null)
            return false;

        return instance.TryGetUtilityAIDecisionFromMirror(creature, out decisionData);
    }

    public static bool TryGetCreatureActionState(
        BaseCreatureBehaviour creature,
        out CreatureActionStateData actionState)
    {
        actionState = default;
        if (instance == null || creature == null)
            return false;

        return instance.TryGetCreatureActionStateFromMirror(creature, out actionState);
    }

    public static bool TryGetCreatureByInstanceId(int instanceId, out BaseCreatureBehaviour creature)
    {
        if (instance != null)
            return instance.TryResolveCreatureByInstanceId(instanceId, out creature);

        creature = FindCreatureByInstanceId(instanceId);
        return creature != null && creature.gameObject.activeInHierarchy;
    }

    public static bool TryGetFoodByInstanceId(int instanceId, out Food food)
    {
        if (instance != null)
            return instance.TryResolveFoodByInstanceId(instanceId, out food);

        food = FindFoodByInstanceId(instanceId);
        return food != null && food.gameObject.activeInHierarchy;
    }

    public static bool TryGetFoodLockData(
        int instanceId,
        out bool isBeingEaten,
        out int eatingCreatureInstanceId)
    {
        isBeingEaten = false;
        eatingCreatureInstanceId = 0;

        if (instance == null)
            return false;

        return instance.TryGetFoodLockDataFromMirror(
            instanceId,
            out isBeingEaten,
            out eatingCreatureInstanceId);
    }

    private bool TryResolveCreatureByInstanceId(int instanceId, out BaseCreatureBehaviour creature)
    {
        if (instanceId == 0)
        {
            creature = null;
            return false;
        }

        if (activeCreaturesByInstanceId.TryGetValue(instanceId, out creature))
        {
            if (creature != null && creature.gameObject.activeInHierarchy)
            {
                unresolvedCreatureIds.Remove(instanceId);
                return true;
            }

            activeCreaturesByInstanceId.Remove(instanceId);
        }

        if (unresolvedCreatureIds.Contains(instanceId))
        {
            creature = null;
            return false;
        }

        creature = FindCreatureByInstanceId(instanceId);
        if (creature == null || !creature.gameObject.activeInHierarchy)
        {
            unresolvedCreatureIds.Add(instanceId);
            creature = null;
            return false;
        }

        activeCreaturesByInstanceId[instanceId] = creature;
        unresolvedCreatureIds.Remove(instanceId);
        return true;
    }

    private bool TryResolveFoodByInstanceId(int instanceId, out Food food)
    {
        if (instanceId == 0)
        {
            food = null;
            return false;
        }

        if (activeFoodByInstanceId.TryGetValue(instanceId, out food))
        {
            if (food != null && food.gameObject.activeInHierarchy)
            {
                unresolvedFoodIds.Remove(instanceId);
                return true;
            }

            activeFoodByInstanceId.Remove(instanceId);
        }

        if (unresolvedFoodIds.Contains(instanceId))
        {
            food = null;
            return false;
        }

        food = FindFoodByInstanceId(instanceId);
        if (food == null || !food.gameObject.activeInHierarchy)
        {
            unresolvedFoodIds.Add(instanceId);
            food = null;
            return false;
        }

        activeFoodByInstanceId[instanceId] = food;
        unresolvedFoodIds.Remove(instanceId);
        return true;
    }

    private bool TryGetFoodLockDataFromMirror(
        int instanceId,
        out bool isBeingEaten,
        out int eatingCreatureInstanceId)
    {
        isBeingEaten = false;
        eatingCreatureInstanceId = 0;

        if (instanceId == 0 || !TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (!foodEntities.TryGetValue(instanceId, out Entity entity) ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<FoodMirrorData>(entity))
        {
            return false;
        }

        FoodMirrorData mirrorData = entityManager.GetComponentData<FoodMirrorData>(entity);
        if (mirrorData.gameObjectInstanceId != instanceId)
            return false;

        isBeingEaten = mirrorData.isBeingEaten;
        eatingCreatureInstanceId = mirrorData.eatingCreatureInstanceId;
        return true;
    }

    public static bool TryRequestSpawnCreature(SpawnCreatureRequest request)
    {
        return instance != null && instance.TryCreateSpawnCreatureRequest(request);
    }

    public static bool TryRequestDespawnCreature(BaseCreatureBehaviour creature, CreatureDeathReason reason)
    {
        if (instance == null || creature == null)
            return false;

        return instance.TryCreateDespawnCreatureRequest(creature.GetInstanceID(), reason);
    }

    public static bool TryRequestSpawnFood(SpawnFoodRequest request)
    {
        return instance != null && instance.TryCreateSpawnFoodRequest(request);
    }

    public static bool TryRequestDespawnFood(Food food, FoodDespawnReason reason)
    {
        if (instance == null || food == null)
            return false;

        return instance.TryCreateDespawnFoodRequest(food.GetInstanceID(), reason);
    }

    public static bool TryRequestCreatureAction(BaseCreatureBehaviour creature, CreatureAction action)
    {
        if (instance == null || creature == null)
            return false;

        return instance.TryWriteCreatureActionRequest(creature.GetInstanceID(), action);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBridgeExists()
    {
        if (FindObjectOfType<ECSMirrorBridge>() != null)
            return;

        var bridgeObject = new GameObject("ECS Mirror Bridge");
        bridgeObject.AddComponent<ECSMirrorBridge>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        if (instance != null && instance != this)
            return;

        CreatureMovementBatch.EnsureCreated();
    }

    private void LateUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (!TryGetEntityManager(out EntityManager entityManager))
            return;

        bool shouldRunSlowMirrorSync = ShouldRunSlowMirrorSync(config);

        if (shouldRunSlowMirrorSync)
        {
            SyncCreatures(entityManager, config);
            SyncFood(entityManager, config);
            RefreshMirrorDebugStatus();
        }

        ProcessECSActionExecutionBridge(entityManager, config);
        UpdateECSObservationBridge(entityManager, config);
    }

    private bool ShouldRunSlowMirrorSync(GameConfig config)
    {
        if (config == null)
        {
            slowMirrorSyncTimer = 0f;
            hasRunSlowMirrorSync = false;
            return true;
        }

        slowMirrorSyncTimer += Time.deltaTime;
        if (hasRunSlowMirrorSync && slowMirrorSyncTimer < Mathf.Max(0.0001f, config.updateInterval))
            return false;

        slowMirrorSyncTimer = 0f;
        hasRunSlowMirrorSync = true;
        return true;
    }

    private void OnDisable()
    {
        StopActiveReproductionBridgeCoroutines();
        StopActivePredationSessions();

        if (TryGetEntityManager(out EntityManager entityManager))
        {
            DestroyAllMirroredEntities(entityManager);
            DestroyAllRequestEntities(entityManager);
        }

        if (instance == this)
        {
            CreatureMovementBatch.DisposeShared();
        }
    }

    private void StopActivePredationSessions()
    {
        foreach (var pair in predationPreyByPredatorId)
        {
            if (TryGetCreatureByInstanceId(pair.Key, out BaseCreatureBehaviour predator) &&
                TryGetCreatureByInstanceId(pair.Value, out BaseCreatureBehaviour prey))
            {
                ReleaseCapturedPrey(predator, prey);
            }
        }

        predationPreyByPredatorId.Clear();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            CreatureMovementBatch.DisposeShared();
            instance = null;
        }
    }

    private bool TryGetEntityManager(out EntityManager entityManager)
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            entityManager = default;
            return false;
        }

        if (mirroredWorld != world)
        {
            creatureEntities.Clear();
            foodEntities.Clear();
            activeCreaturesByInstanceId.Clear();
            activeFoodByInstanceId.Clear();
            unresolvedCreatureIds.Clear();
            unresolvedFoodIds.Clear();
            utilityDecisionByCreatureId.Clear();
            utilityContextByCreatureId.Clear();
            mirroredWorld = world;
        }

        entityManager = world.EntityManager;
        return true;
    }

    private bool TryCreateSpawnCreatureRequest(SpawnCreatureRequest request)
    {
        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, request);
        return true;
    }

    private bool TryCreateDespawnCreatureRequest(int instanceId, CreatureDeathReason reason)
    {
        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new DespawnCreatureRequest
        {
            gameObjectInstanceId = instanceId,
            reason = (int)reason
        });
        return true;
    }

    private bool TryCreateSpawnFoodRequest(SpawnFoodRequest request)
    {
        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, request);
        return true;
    }

    private bool TryCreateDespawnFoodRequest(int instanceId, FoodDespawnReason reason)
    {
        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        Entity requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new DespawnFoodRequest
        {
            gameObjectInstanceId = instanceId,
            reason = (int)reason
        });
        return true;
    }

    private bool TryWriteCreatureActionRequest(int instanceId, CreatureAction action)
    {
        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (!creatureEntities.TryGetValue(instanceId, out Entity entity) ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<CreatureActionRequestData>(entity))
        {
            return false;
        }

        if (entityManager.HasComponent<CreatureActionStateData>(entity))
        {
            CreatureActionStateData actionState = entityManager.GetComponentData<CreatureActionStateData>(entity);
            bool isActionLockPhase =
                actionState.status == CreatureActionStatus.Running &&
                (actionState.phase == CreatureActionPhase.MovingToTarget ||
                 actionState.phase == CreatureActionPhase.Executing);

            if (isActionLockPhase)
                return false;
        }

        CreatureActionRequestData requestData = entityManager.GetComponentData<CreatureActionRequestData>(entity);

        if (requestData.hasRequest && requestData.requestedPhase == CreatureActionPhase.Executing)
            return false;

        requestData.hasRequest = action != CreatureAction.None;
        requestData.requestedAction = action;
        requestData.requestedPhase = GetRequestedPhaseForAction(action);
        requestData.targetInstanceId = 0;
        requestData.cancelRequested = false;
        requestData.completeRequested = false;
        entityManager.SetComponentData(entity, requestData);
        return requestData.hasRequest;
    }

    private void ProcessECSActionExecutionBridge(EntityManager entityManager, GameConfig config)
    {
        bool syncUtilityScoringData = config != null && config.useUtilityAI && config.useEcsUtilityScoring;
        bool syncEcsContextData = config != null && config.useUtilityAI && config.useEcsAIContext;
        bool includeObservationInContext = config != null && config.useEcsObservation;

        foreach (var pair in creatureEntities)
        {
            int instanceId = pair.Key;
            Entity entity = pair.Value;
            if (!entityManager.Exists(entity))
            {
                utilityDecisionByCreatureId.Remove(instanceId);
                utilityContextByCreatureId.Remove(instanceId);
                continue;
            }

            RefreshUtilityMirrorCacheForEntity(
                entityManager,
                entity,
                instanceId,
                syncUtilityScoringData,
                syncEcsContextData,
                includeObservationInContext);

            if (!entityManager.HasComponent<CreatureActionRequestData>(entity) ||
                !entityManager.HasComponent<CreatureActionStateData>(entity))
            {
                continue;
            }

            CreatureActionRequestData request = entityManager.GetComponentData<CreatureActionRequestData>(entity);
            CreatureActionStateData actionState = entityManager.GetComponentData<CreatureActionStateData>(entity);
            if (!NeedsActionExecutionBridge(request, actionState))
                continue;

            if (!entityManager.HasComponent<CreatureIdentity>(entity) ||
                !entityManager.HasComponent<CreatureActionTargetData>(entity) ||
                !entityManager.HasComponent<CreatureActionTimerData>(entity))
            {
                continue;
            }

            CreatureIdentity identity = entityManager.GetComponentData<CreatureIdentity>(entity);
            if (!TryGetCreatureByInstanceId(identity.gameObjectInstanceId, out BaseCreatureBehaviour creature) ||
                creature == null)
            {
                continue;
            }

            CreatureActionTargetData actionTarget = entityManager.GetComponentData<CreatureActionTargetData>(entity);
            CreatureActionTimerData actionTimer = entityManager.GetComponentData<CreatureActionTimerData>(entity);

            ProcessECSMateExecutionRequest(
                entityManager,
                entity,
                creature,
                ref request,
                ref actionState,
                ref actionTarget,
                ref actionTimer);

            ProcessECSPredationExecutionRequest(
                entityManager,
                entity,
                creature,
                ref request,
                ref actionState,
                ref actionTarget,
                ref actionTimer);
        }
    }

    private void RefreshUtilityMirrorCacheForEntity(
        EntityManager entityManager,
        Entity entity,
        int instanceId,
        bool syncUtilityScoringData,
        bool syncEcsContextData,
        bool includeObservationInContext)
    {
        if (syncUtilityScoringData && entityManager.HasComponent<CreatureUtilityDecisionData>(entity))
        {
            utilityDecisionByCreatureId[instanceId] = entityManager.GetComponentData<CreatureUtilityDecisionData>(entity);
        }
        else
        {
            utilityDecisionByCreatureId.Remove(instanceId);
        }

        if (syncEcsContextData && entityManager.HasComponent<CreatureAIContextData>(entity))
        {
            CreatureAIContextData contextData = entityManager.GetComponentData<CreatureAIContextData>(entity);
            CreatureObservationResultData observationResult =
                includeObservationInContext && entityManager.HasComponent<CreatureObservationResultData>(entity)
                    ? entityManager.GetComponentData<CreatureObservationResultData>(entity)
                    : default;
            utilityContextByCreatureId[instanceId] = UtilityAIContextFactory.FromECSMirrorData(contextData, observationResult);
        }
        else
        {
            utilityContextByCreatureId.Remove(instanceId);
        }
    }

    private static bool NeedsActionExecutionBridge(
        CreatureActionRequestData request,
        CreatureActionStateData actionState)
    {
        bool hasMateExecutionRequest =
            request.hasRequest &&
            request.requestedAction == CreatureAction.SearchMate &&
            request.requestedPhase == CreatureActionPhase.Executing;
        bool isMateExecuting =
            actionState.currentAction == CreatureAction.SearchMate &&
            actionState.phase == CreatureActionPhase.Executing;

        bool hasPredationExecutionRequest =
            request.hasRequest &&
            request.requestedAction == CreatureAction.Hunt &&
            request.requestedPhase == CreatureActionPhase.Executing;
        bool isPredationExecuting =
            actionState.currentAction == CreatureAction.Hunt &&
            actionState.phase == CreatureActionPhase.Executing;

        return hasMateExecutionRequest ||
               isMateExecuting ||
               hasPredationExecutionRequest ||
               isPredationExecuting;
    }

    private static void ProcessECSPredationExecutionRequest(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        ref CreatureActionRequestData request,
        ref CreatureActionStateData actionState,
        ref CreatureActionTargetData actionTarget,
        ref CreatureActionTimerData actionTimer)
    {
        bool isPredationAction =
            actionState.currentAction == CreatureAction.Hunt &&
            actionState.phase == CreatureActionPhase.Executing;
        bool hasPredationExecuteRequest =
            request.hasRequest &&
            request.requestedAction == CreatureAction.Hunt &&
            request.requestedPhase == CreatureActionPhase.Executing;

        if (!isPredationAction && !hasPredationExecuteRequest)
            return;

        int preyId = hasPredationExecuteRequest && request.targetInstanceId != 0
            ? request.targetInstanceId
            : actionTarget.targetInstanceId;

        if (request.cancelRequested)
        {
            instance?.ReleasePredationTargetIfCaptured(creature, preyId);
            SetActionEnded(
                ref actionState,
                ref actionTarget,
                ref actionTimer,
                CreatureActionStatus.Cancelled,
                CreatureStateType.Wandering);
            SetWanderRequest(ref request);
            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            return;
        }

        bool hasPrey = TryGetCreatureByInstanceId(preyId, out BaseCreatureBehaviour prey);
        bool isValidPrey = hasPrey && IsValidPredationPreyForBridge(creature, prey);

        if (hasPredationExecuteRequest)
        {
            if (!isValidPrey)
            {
                SetActionEnded(
                    ref actionState,
                    ref actionTarget,
                    ref actionTimer,
                    CreatureActionStatus.Cancelled,
                    CreatureStateType.Wandering);
                SetWanderRequest(ref request);
                WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
                return;
            }

            if (!TryAcquirePredationTarget(creature, prey))
            {
                SetActionEnded(
                    ref actionState,
                    ref actionTarget,
                    ref actionTimer,
                    CreatureActionStatus.Cancelled,
                    CreatureStateType.Wandering);
                SetWanderRequest(ref request);
                WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
                return;
            }

            instance?.TrackPredationTarget(creature, prey);
            Statistics.Instance?.RecordPredationAttempt();

            actionState.currentAction = CreatureAction.Hunt;
            actionState.phase = CreatureActionPhase.Executing;
            actionState.status = CreatureActionStatus.Running;
            actionState.legacyStateType = CreatureStateType.Predation;
            actionState.canCancel = true;
            actionState.canComplete = true;
            actionTarget.targetInstanceId = preyId;
            actionTimer.elapsedTime = 0f;
            actionTimer.remainingTime = -1f;

            ClearRequest(ref request);
            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            return;
        }

        bool isPredatingNow =
            actionState.currentAction == CreatureAction.Hunt &&
            actionState.phase == CreatureActionPhase.Executing;

        if (isPredatingNow)
        {
            if (!isValidPrey)
            {
                instance?.ReleasePredationTargetIfCaptured(creature, preyId);
                SetActionEnded(
                    ref actionState,
                    ref actionTarget,
                    ref actionTimer,
                    CreatureActionStatus.Cancelled,
                    CreatureStateType.Wandering);
                SetWanderRequest(ref request);
                WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
                return;
            }

            actionTimer.elapsedTime += Time.fixedDeltaTime;
            actionTimer.remainingTime = -1f;
            actionState.status = CreatureActionStatus.Running;
            actionState.legacyStateType = CreatureStateType.Predation;
            actionState.canCancel = true;
            actionState.canComplete = true;

            GameConfig config = GameConfig.Instance;
            float eatingDuration = config != null ? Mathf.Max(0f, config.predatorEatingDuration) : 0f;
            if (eatingDuration <= 0f || actionTimer.elapsedTime >= eatingDuration)
            {
                CreatureActionStatus resolvedStatus = ResolvePredationOutcome(creature, prey);
                SetActionEnded(
                    ref actionState,
                    ref actionTarget,
                    ref actionTimer,
                    resolvedStatus,
                    CreatureStateType.Wandering);
                SetWanderRequest(ref request);
            }

            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            return;
        }

        CreatureActionStatus endStatus;
        if (!hasPrey || prey == null || prey.IsDespawnQueued || !prey.gameObject.activeInHierarchy)
        {
            endStatus = CreatureActionStatus.Completed;
        }
        else if (creature is PredatorBehaviour predator && predator.IsPreyBlacklisted(prey))
        {
            endStatus = CreatureActionStatus.Cancelled;
        }
        else
        {
            endStatus = CreatureActionStatus.Cancelled;
        }

        instance?.ReleasePredationTargetIfCaptured(creature, preyId);
        SetActionEnded(
            ref actionState,
            ref actionTarget,
            ref actionTimer,
            endStatus,
            CreatureStateType.Wandering);
        SetWanderRequest(ref request);
        WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
    }

    private static bool IsValidPredationPreyForBridge(BaseCreatureBehaviour predatorCreature, BaseCreatureBehaviour prey)
    {
        if (predatorCreature == null || prey == null || prey == predatorCreature)
            return false;

        if (prey.IsDespawnQueued || !prey.gameObject.activeInHierarchy)
            return false;

        if (prey is HerbivoreBehaviour herbivore && herbivore.IsCaptured && !herbivore.IsCapturedBy(predatorCreature))
            return false;

        if (predatorCreature is PredatorBehaviour predator && predator.IsPreyBlacklisted(prey))
            return false;

        return true;
    }

    private static bool TryAcquirePredationTarget(BaseCreatureBehaviour predator, BaseCreatureBehaviour prey)
    {
        if (predator == null || prey == null)
            return false;

        if (prey is HerbivoreBehaviour herbivore)
            return herbivore.TryStartCapture(predator);

        return true;
    }

    private static void ReleaseCapturedPrey(BaseCreatureBehaviour predator, BaseCreatureBehaviour prey)
    {
        if (predator == null || prey == null)
            return;

        if (prey is HerbivoreBehaviour herbivore)
            herbivore.ReleaseCapture(predator);
    }

    private static float CalculateEnergyGainFromPrey(BaseCreatureBehaviour prey)
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || prey == null || prey.EnergyManager == null)
            return 0f;

        return PredationCalculator.CalculateEnergyGainFromPrey(
            prey.EnergyManager.EnergyLevel,
            prey.maxEnergy,
            prey.Weight,
            PredationParameters.FromConfig(config));
    }

    private static float CalculatePredationSuccessChance(BaseCreatureBehaviour predatorCreature, BaseCreatureBehaviour prey)
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || predatorCreature == null || prey == null)
            return 0.75f;

        float predatorStrength = predatorCreature is PredatorBehaviour predator
            ? predator.Strength
            : config.predatorStrengthMax;
        float herbivoreAgility = prey is HerbivoreBehaviour herbivore
            ? herbivore.Agility
            : config.herbivoreAgilityMin;

        return PredationCalculator.CalculatePredationSuccessChance(
            predatorStrength,
            predatorCreature.Weight,
            herbivoreAgility,
            prey.Weight,
            PredationParameters.FromConfig(config));
    }

    private static CreatureActionStatus ResolvePredationOutcome(BaseCreatureBehaviour predator, BaseCreatureBehaviour prey)
    {
        if (predator == null || prey == null)
            return CreatureActionStatus.Cancelled;

        bool predatorSucceeded = UnityEngine.Random.value <= CalculatePredationSuccessChance(predator, prey);
        ReleaseCapturedPrey(predator, prey);

        if (predatorSucceeded)
        {
            Statistics.Instance?.RecordPredationResolved(true);
            float gainedEnergy = CalculateEnergyGainFromPrey(prey);
            predator.EnergyManager?.GainEnergy(gainedEnergy);
            prey.Despawn(CreatureDeathReason.Predation);
            return CreatureActionStatus.Completed;
        }

        Statistics.Instance?.RecordPredationResolved(false);
        if (prey is HerbivoreBehaviour escapingHerbivore)
            escapingHerbivore.TriggerEscapeFrom(predator);

        if (predator is PredatorBehaviour predatorBehaviour)
            predatorBehaviour.BlacklistPrey(prey);

        GameConfig config = GameConfig.Instance;
        if (config != null && predator.MovementManager != null)
        {
            float baseSpeed = Mathf.Max(0.01f, predator.MovementManager.BaseMoveSpeed);
            float currentSpeed = Mathf.Max(0f, predator.MovementManager.MoveSpeed);
            bool alreadyHeavilySlowed = currentSpeed < baseSpeed * 0.8f;

            if (!alreadyHeavilySlowed)
            {
                predator.MovementManager.ApplyTemporarySpeedMultiplier(
                    config.predatorFailedHuntSpeedMultiplier,
                    config.predatorFailedHuntDebuffDuration);
            }
        }

        return CreatureActionStatus.Cancelled;
    }

    private void ReleasePredationTargetIfCaptured(BaseCreatureBehaviour predator, int preyId)
    {
        if (predator == null)
            return;

        if (preyId != 0 && TryGetCreatureByInstanceId(preyId, out BaseCreatureBehaviour prey))
            ReleaseCapturedPrey(predator, prey);

        predationPreyByPredatorId.Remove(predator.GetInstanceID());
    }

    private void TrackPredationTarget(BaseCreatureBehaviour predator, BaseCreatureBehaviour prey)
    {
        if (predator == null)
            return;

        int predatorId = predator.GetInstanceID();
        if (prey == null)
        {
            predationPreyByPredatorId.Remove(predatorId);
            return;
        }

        predationPreyByPredatorId[predatorId] = prey.GetInstanceID();
    }

    private void ProcessECSMateExecutionRequest(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        ref CreatureActionRequestData request,
        ref CreatureActionStateData actionState,
        ref CreatureActionTargetData actionTarget,
        ref CreatureActionTimerData actionTimer)
    {
        bool isMateAction =
            actionState.currentAction == CreatureAction.SearchMate &&
            actionState.phase == CreatureActionPhase.Executing;
        bool hasMateExecuteRequest =
            request.hasRequest &&
            request.requestedAction == CreatureAction.SearchMate &&
            request.requestedPhase == CreatureActionPhase.Executing;

        if (!isMateAction && !hasMateExecuteRequest)
            return;

        int mateId = hasMateExecuteRequest && request.targetInstanceId != 0
            ? request.targetInstanceId
            : actionTarget.targetInstanceId;

        if (request.cancelRequested)
        {
            CancelReproductionSessionByCreature(entityManager, creature.GetInstanceID());
            return;
        }

        if (!TryGetCreatureByInstanceId(mateId, out BaseCreatureBehaviour mate) ||
            mate == null ||
            mate == creature ||
            creature.ReproductionManager == null ||
            mate.ReproductionManager == null)
        {
            SetActionEnded(
                ref actionState,
                ref actionTarget,
                ref actionTimer,
                CreatureActionStatus.Cancelled,
                CreatureStateType.Wandering);
            SetWanderRequest(ref request);
            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            CancelReproductionSessionByCreature(entityManager, creature.GetInstanceID());
            return;
        }

        if (hasMateExecuteRequest && !creature.ReproductionManager.CanMateWith(mate))
        {
            SetActionEnded(
                ref actionState,
                ref actionTarget,
                ref actionTimer,
                CreatureActionStatus.Cancelled,
                CreatureStateType.Wandering);
            SetWanderRequest(ref request);
            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            CancelReproductionSessionByCreature(entityManager, creature.GetInstanceID());
            return;
        }

        if (hasMateExecuteRequest)
        {
            bool accepted = creature.ReproductionManager.TryMutualAcceptance(mate);
            if (!accepted)
            {
                SetActionEnded(
                    ref actionState,
                    ref actionTarget,
                    ref actionTimer,
                    CreatureActionStatus.Cancelled,
                    CreatureStateType.Wandering);
                SetWanderRequest(ref request);
                WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
                ApplyRejectedMateFallbackToOtherCreature(entityManager, mate, creature);
                return;
            }

            if (!TryBeginReproductionBridgeSession(entityManager, creature, mate))
            {
                SetActionEnded(
                    ref actionState,
                    ref actionTarget,
                    ref actionTimer,
                    CreatureActionStatus.Cancelled,
                    CreatureStateType.Wandering);
                SetWanderRequest(ref request);
                WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
                ApplyRejectedMateFallbackToOtherCreature(entityManager, mate, creature);
                return;
            }

            actionState.currentAction = CreatureAction.SearchMate;
            actionState.phase = CreatureActionPhase.Executing;
            actionState.status = CreatureActionStatus.Running;
            actionState.legacyStateType = CreatureStateType.Reproducting;
            actionState.canCancel = false;
            actionState.canComplete = false;
            actionTarget.targetInstanceId = mateId;
            actionTimer.elapsedTime = 0f;
            actionTimer.remainingTime = -1f;
            ClearRequest(ref request);
            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            ApplyReproductionLockToOtherCreature(entityManager, mate, creature);
            return;
        }

        if (!IsReproductionSessionActive(creature.GetInstanceID(), mateId))
        {
            SetActionEnded(
                ref actionState,
                ref actionTarget,
                ref actionTimer,
                CreatureActionStatus.Completed,
                CreatureStateType.Wandering);
            SetWanderRequest(ref request);
            WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
            return;
        }

        actionTimer.elapsedTime += Time.fixedDeltaTime;
        actionTimer.remainingTime = Mathf.Max(0f, GetReproductionDuration(creature) - actionTimer.elapsedTime);
        actionState.status = CreatureActionStatus.Running;
        actionState.legacyStateType = CreatureStateType.Reproducting;
        actionState.canCancel = false;
        actionState.canComplete = false;
        WriteActionExecution(entityManager, entity, request, actionState, actionTarget, actionTimer);
    }

    private void ApplyReproductionLockToOtherCreature(
        EntityManager entityManager,
        BaseCreatureBehaviour creature,
        BaseCreatureBehaviour mate)
    {
        if (creature == null || mate == null)
            return;

        if (!TryGetCreatureEntityByInstanceId(creature.GetInstanceID(), out Entity creatureEntity) ||
            !entityManager.Exists(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionStateData>(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionTargetData>(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionRequestData>(creatureEntity))
        {
            return;
        }

        CreatureActionStateData state = entityManager.GetComponentData<CreatureActionStateData>(creatureEntity);
        CreatureActionTargetData target = entityManager.GetComponentData<CreatureActionTargetData>(creatureEntity);
        CreatureActionRequestData request = entityManager.GetComponentData<CreatureActionRequestData>(creatureEntity);

        state.currentAction = CreatureAction.SearchMate;
        state.phase = CreatureActionPhase.Executing;
        state.status = CreatureActionStatus.Running;
        state.legacyStateType = CreatureStateType.Reproducting;
        state.canCancel = false;
        state.canComplete = false;
        target.targetInstanceId = mate.GetInstanceID();
        ClearRequest(ref request);

        entityManager.SetComponentData(creatureEntity, state);
        entityManager.SetComponentData(creatureEntity, target);
        entityManager.SetComponentData(creatureEntity, request);
    }

    private void ApplyRejectedMateFallbackToOtherCreature(
        EntityManager entityManager,
        BaseCreatureBehaviour creature,
        BaseCreatureBehaviour mate)
    {
        if (creature == null || mate == null)
            return;

        if (!TryGetCreatureEntityByInstanceId(creature.GetInstanceID(), out Entity creatureEntity) ||
            !entityManager.Exists(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionStateData>(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionTargetData>(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionRequestData>(creatureEntity) ||
            !entityManager.HasComponent<CreatureActionTimerData>(creatureEntity))
        {
            return;
        }

        CreatureActionStateData state = entityManager.GetComponentData<CreatureActionStateData>(creatureEntity);
        CreatureActionTargetData target = entityManager.GetComponentData<CreatureActionTargetData>(creatureEntity);
        CreatureActionTimerData timer = entityManager.GetComponentData<CreatureActionTimerData>(creatureEntity);
        CreatureActionRequestData otherRequest = entityManager.GetComponentData<CreatureActionRequestData>(creatureEntity);

        if (state.currentAction == CreatureAction.SearchMate)
        {
            SetActionEnded(
                ref state,
                ref target,
                ref timer,
                CreatureActionStatus.Cancelled,
                CreatureStateType.Wandering);
            SetWanderRequest(ref otherRequest);
            entityManager.SetComponentData(creatureEntity, state);
            entityManager.SetComponentData(creatureEntity, target);
            entityManager.SetComponentData(creatureEntity, timer);
            entityManager.SetComponentData(creatureEntity, otherRequest);
        }
    }

    private bool TryBeginReproductionBridgeSession(
        EntityManager entityManager,
        BaseCreatureBehaviour creature,
        BaseCreatureBehaviour mate)
    {
        if (!IsValidReproductionPairForBridge(creature, mate))
            return false;

        int creatureId = creature.GetInstanceID();
        int mateId = mate.GetInstanceID();

        if (IsReproductionSessionActive(creatureId, mateId))
            return true;

        if (reproductionPartnerByCreatureId.ContainsKey(creatureId) ||
            reproductionPartnerByCreatureId.ContainsKey(mateId))
        {
            return false;
        }

        creature.ReproductionManager.StartReproductionCooldown();
        mate.ReproductionManager.StartReproductionCooldown();

        reproductionPartnerByCreatureId[creatureId] = mateId;
        reproductionPartnerByCreatureId[mateId] = creatureId;

        BaseCreatureBehaviour birthOwner = creature.Sex == CreatureSex.Female
            ? creature
            : mate;
        BaseCreatureBehaviour nonBirthMate = birthOwner == creature ? mate : creature;

        Coroutine coroutine = StartCoroutine(ReproductionBridgeRoutine(birthOwner, nonBirthMate));
        int birthOwnerId = birthOwner.GetInstanceID();
        reproductionCoroutineByBirthOwnerId[birthOwnerId] = coroutine;
        birthOwner.ReproductionManager.reproductionCoroutine = coroutine;
        nonBirthMate.ReproductionManager.reproductionCoroutine = coroutine;

        ApplyReproductionLockToOtherCreature(entityManager, birthOwner, nonBirthMate);
        ApplyReproductionLockToOtherCreature(entityManager, nonBirthMate, birthOwner);
        return true;
    }

    private IEnumerator ReproductionBridgeRoutine(
        BaseCreatureBehaviour birthOwner,
        BaseCreatureBehaviour mate)
    {
        int birthOwnerId = birthOwner != null ? birthOwner.GetInstanceID() : 0;
        int mateId = mate != null ? mate.GetInstanceID() : 0;
        float duration = GetReproductionDuration(birthOwner);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!TryGetCreatureByInstanceId(birthOwnerId, out BaseCreatureBehaviour owner) ||
                !TryGetCreatureByInstanceId(mateId, out BaseCreatureBehaviour partner) ||
                !IsValidReproductionPairForBridge(owner, partner, ignoreCooldown: true) ||
                !IsReproductionSessionActive(birthOwnerId, mateId))
            {
                if (TryGetEntityManager(out EntityManager manager))
                {
                    CancelReproductionSession(manager, birthOwnerId, mateId);
                }
                yield break;
            }

            elapsed += Time.fixedDeltaTime;
            yield return WaitForFixedUpdateCached;
        }

        if (TryGetCreatureByInstanceId(birthOwnerId, out BaseCreatureBehaviour reproductionOwner) &&
            TryGetCreatureByInstanceId(mateId, out BaseCreatureBehaviour reproductionMate) &&
            IsValidReproductionPairForBridge(reproductionOwner, reproductionMate, ignoreCooldown: true))
        {
            reproductionOwner.ReproductionManager.Reproduct(reproductionMate);
        }

        if (TryGetEntityManager(out EntityManager entityManager))
        {
            CompleteReproductionSession(entityManager, birthOwnerId, mateId);
        }
    }

    private void CancelReproductionSessionByCreature(EntityManager entityManager, int creatureId)
    {
        if (!reproductionPartnerByCreatureId.TryGetValue(creatureId, out int mateId))
            return;

        CancelReproductionSession(entityManager, creatureId, mateId);
    }

    private void CancelReproductionSession(EntityManager entityManager, int creatureAId, int creatureBId)
    {
        StopReproductionCoroutineIfOwner(creatureAId);
        StopReproductionCoroutineIfOwner(creatureBId);
        EndReproductionSession(entityManager, creatureAId, creatureBId, CreatureActionStatus.Cancelled);
    }

    private void CompleteReproductionSession(EntityManager entityManager, int creatureAId, int creatureBId)
    {
        EndReproductionSession(entityManager, creatureAId, creatureBId, CreatureActionStatus.Completed);
    }

    private void EndReproductionSession(
        EntityManager entityManager,
        int creatureAId,
        int creatureBId,
        CreatureActionStatus endStatus)
    {
        ClearReproductionSessionBookkeeping(creatureAId, creatureBId);
        ApplyReproductionSessionEndToCreature(entityManager, creatureAId, endStatus);
        ApplyReproductionSessionEndToCreature(entityManager, creatureBId, endStatus);
    }

    private void ApplyReproductionSessionEndToCreature(
        EntityManager entityManager,
        int creatureInstanceId,
        CreatureActionStatus endStatus)
    {
        if (!TryGetCreatureEntityByInstanceId(creatureInstanceId, out Entity entity) ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<CreatureActionStateData>(entity) ||
            !entityManager.HasComponent<CreatureActionTargetData>(entity) ||
            !entityManager.HasComponent<CreatureActionTimerData>(entity) ||
            !entityManager.HasComponent<CreatureActionRequestData>(entity))
        {
            return;
        }

        CreatureActionStateData state = entityManager.GetComponentData<CreatureActionStateData>(entity);
        CreatureActionTargetData target = entityManager.GetComponentData<CreatureActionTargetData>(entity);
        CreatureActionTimerData timer = entityManager.GetComponentData<CreatureActionTimerData>(entity);
        CreatureActionRequestData request = entityManager.GetComponentData<CreatureActionRequestData>(entity);

        SetActionEnded(
            ref state,
            ref target,
            ref timer,
            endStatus,
            CreatureStateType.Wandering);
        SetWanderRequest(ref request);

        entityManager.SetComponentData(entity, state);
        entityManager.SetComponentData(entity, target);
        entityManager.SetComponentData(entity, timer);
        entityManager.SetComponentData(entity, request);
    }

    private void ClearReproductionSessionBookkeeping(int creatureAId, int creatureBId)
    {
        reproductionPartnerByCreatureId.Remove(creatureAId);
        reproductionPartnerByCreatureId.Remove(creatureBId);

        reproductionCoroutineByBirthOwnerId.Remove(creatureAId);
        reproductionCoroutineByBirthOwnerId.Remove(creatureBId);

        if (TryGetCreatureByInstanceId(creatureAId, out BaseCreatureBehaviour creatureA) && creatureA?.ReproductionManager != null)
        {
            creatureA.ReproductionManager.reproductionCoroutine = null;
        }

        if (TryGetCreatureByInstanceId(creatureBId, out BaseCreatureBehaviour creatureB) && creatureB?.ReproductionManager != null)
        {
            creatureB.ReproductionManager.reproductionCoroutine = null;
        }
    }

    private bool IsReproductionSessionActive(int creatureId, int mateId)
    {
        return reproductionPartnerByCreatureId.TryGetValue(creatureId, out int partnerId) &&
               partnerId == mateId &&
               reproductionPartnerByCreatureId.TryGetValue(mateId, out int reversePartnerId) &&
               reversePartnerId == creatureId;
    }

    private static bool IsValidReproductionPairForBridge(
        BaseCreatureBehaviour creature,
        BaseCreatureBehaviour mate,
        bool ignoreCooldown = false)
    {
        if (creature == null || mate == null)
            return false;

        if (creature == mate ||
            creature.IsDespawnQueued || mate.IsDespawnQueued ||
            !creature.gameObject.activeInHierarchy ||
            !mate.gameObject.activeInHierarchy ||
            creature.ReproductionManager == null ||
            mate.ReproductionManager == null)
        {
            return false;
        }

        if (creature.GetType() != mate.GetType() || creature.Sex == mate.Sex)
            return false;

        if (ignoreCooldown)
            return true;

        return creature.ReproductionManager.CanMateWith(mate);
    }

    private float GetReproductionDuration(BaseCreatureBehaviour creature)
    {
        GameConfig config = GameConfig.Instance;
        bool isPredator = creature is PredatorBehaviour;
        return config != null ? Mathf.Max(0f, config.GetReproductionTime(isPredator)) : 0f;
    }

    private void StopActiveReproductionBridgeCoroutines()
    {
        foreach (int creatureId in reproductionPartnerByCreatureId.Keys)
        {
            if (TryGetCreatureByInstanceId(creatureId, out BaseCreatureBehaviour creature) && creature?.ReproductionManager != null)
            {
                creature.ReproductionManager.reproductionCoroutine = null;
            }
        }

        foreach (Coroutine coroutine in reproductionCoroutineByBirthOwnerId.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        reproductionCoroutineByBirthOwnerId.Clear();
        reproductionPartnerByCreatureId.Clear();
    }

    private void StopReproductionCoroutineIfOwner(int creatureId)
    {
        if (!reproductionCoroutineByBirthOwnerId.TryGetValue(creatureId, out Coroutine coroutine))
            return;

        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        reproductionCoroutineByBirthOwnerId.Remove(creatureId);
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

    private bool TryGetCreatureEntityByInstanceId(int instanceId, out Entity entity)
    {
        return creatureEntities.TryGetValue(instanceId, out entity) && entity != Entity.Null;
    }

    private static void SetActionEnded(
        ref CreatureActionStateData actionState,
        ref CreatureActionTargetData actionTarget,
        ref CreatureActionTimerData actionTimer,
        CreatureActionStatus endStatus,
        CreatureStateType legacyStateType)
    {
        actionState.currentAction = CreatureAction.None;
        actionState.phase = CreatureActionPhase.Idle;
        actionState.status = endStatus;
        actionState.legacyStateType = legacyStateType;
        actionState.canCancel = false;
        actionState.canComplete = false;
        actionTarget.targetInstanceId = 0;
        actionTimer.remainingTime = -1f;
    }

    private static void ClearRequest(ref CreatureActionRequestData request)
    {
        request.hasRequest = false;
        request.requestedAction = CreatureAction.None;
        request.requestedPhase = CreatureActionPhase.None;
        request.targetInstanceId = 0;
        request.cancelRequested = false;
        request.completeRequested = false;
    }

    private static void WriteActionExecution(
        EntityManager entityManager,
        Entity entity,
        CreatureActionRequestData request,
        CreatureActionStateData actionState,
        CreatureActionTargetData actionTarget,
        CreatureActionTimerData actionTimer)
    {
        entityManager.SetComponentData(entity, request);
        entityManager.SetComponentData(entity, actionState);
        entityManager.SetComponentData(entity, actionTarget);
        entityManager.SetComponentData(entity, actionTimer);
    }

    private static BaseCreatureBehaviour FindCreatureByInstanceId(int instanceId)
    {
        CreatureSpawner spawner = CreatureSpawner.Instance;
        if (spawner == null)
            return null;

        BaseCreatureBehaviour creature = FindCreatureByInstanceId(spawner.herbivorCreatures, instanceId);
        return creature != null
            ? creature
            : FindCreatureByInstanceId(spawner.predatorCreatures, instanceId);
    }

    private static BaseCreatureBehaviour FindCreatureByInstanceId(
        List<BaseCreatureBehaviour> creatures,
        int instanceId)
    {
        if (creatures == null)
            return null;

        for (int i = 0; i < creatures.Count; i++)
        {
            BaseCreatureBehaviour creature = creatures[i];
            if (creature != null && creature.GetInstanceID() == instanceId)
                return creature;
        }

        return null;
    }

    private static Food FindFoodByInstanceId(int instanceId)
    {
        FoodSpawner spawner = FoodSpawner.Instance;
        if (spawner == null || spawner.foods == null)
            return null;

        for (int i = 0; i < spawner.foods.Count; i++)
        {
            Food food = spawner.foods[i];
            if (food != null && food.GetInstanceID() == instanceId)
                return food;
        }

        return null;
    }

    private void SyncCreatures(EntityManager entityManager, GameConfig config)
    {
        seenCreatureIds.Clear();
        unresolvedCreatureIds.Clear();
        lastEcsCreatureLifecycleDespawnRequestCount = 0;

        bool syncObservationData = config != null && config.useEcsObservation;
        bool syncLifecycleData = config != null && config.useEcsCreatureLifecycle;
        bool syncUtilityScoringData = config != null && config.useUtilityAI && config.useEcsUtilityScoring;
        bool syncEcsContextData = config != null && config.useUtilityAI && config.useEcsAIContext;
        bool syncAIContextData = syncObservationData || syncUtilityScoringData || syncEcsContextData;

        CreatureSpawner spawner = CreatureSpawner.Instance;
        if (spawner != null)
        {
            SyncCreatureList(
                entityManager,
                spawner.herbivorCreatures,
                config,
                syncObservationData,
                syncLifecycleData,
                syncAIContextData,
                syncUtilityScoringData);
            SyncCreatureList(
                entityManager,
                spawner.predatorCreatures,
                config,
                syncObservationData,
                syncLifecycleData,
                syncAIContextData,
                syncUtilityScoringData);
        }

        RemoveStaleEntities(entityManager, creatureEntities, seenCreatureIds);
        RemoveStaleObjects(activeCreaturesByInstanceId, seenCreatureIds);
        RemoveStaleData(utilityDecisionByCreatureId, seenCreatureIds);
        RemoveStaleData(utilityContextByCreatureId, seenCreatureIds);
        lastActiveCreatureCount = seenCreatureIds.Count;
    }

    private void SyncCreatureList(
        EntityManager entityManager,
        List<BaseCreatureBehaviour> creatures,
        GameConfig config,
        bool syncObservationData,
        bool syncLifecycleData,
        bool syncAIContextData,
        bool syncUtilityScoringData)
    {
        if (creatures == null)
            return;

        for (int i = 0; i < creatures.Count; i++)
        {
            BaseCreatureBehaviour creature = creatures[i];
            if (creature == null || !creature.gameObject.activeInHierarchy || creature.IsDespawnQueued)
                continue;

            int instanceId = creature.GetInstanceID();
            if (!seenCreatureIds.Add(instanceId))
                continue;

            if (!activeCreaturesByInstanceId.TryGetValue(instanceId, out BaseCreatureBehaviour cachedCreature) ||
                cachedCreature != creature)
            {
                activeCreaturesByInstanceId[instanceId] = creature;
            }

            unresolvedCreatureIds.Remove(instanceId);
            Entity entity = GetOrCreateCreatureEntity(entityManager, creature, instanceId, out bool createdEntity);

            if (!syncLifecycleData)
            {
                creature.EnergyManager?.ConsumePendingECSExternalEnergyDelta();
            }

            if (syncLifecycleData &&
                TryApplyECSCreatureLifecycleResult(entityManager, entity, creature, instanceId))
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }

                creatureEntities.Remove(instanceId);
                activeCreaturesByInstanceId.Remove(instanceId);
                unresolvedCreatureIds.Remove(instanceId);
                seenCreatureIds.Remove(instanceId);
                CreatureMovementBatch.Instance?.Unregister(instanceId);
                i--;
                continue;
            }

            if (syncObservationData)
            {
                entityManager.SetComponentData(entity, CreateCreatureTransformMirror(creature.transform));
                entityManager.SetComponentData(entity, CreateCreatureObservationSensorData(creature));
            }

            if (createdEntity)
            {
                entityManager.SetComponentData(entity, CreateCreatureIdentity(creature, instanceId));
            }

            if (syncLifecycleData)
            {
                entityManager.SetComponentData(entity, CreateCreatureLifecycleData(entityManager, entity, creature, instanceId));
            }

            if (syncAIContextData)
            {
                entityManager.SetComponentData(entity, CreateCreatureAIContextData(UtilityAIContextFactory.FromMonoCreature(creature)));
            }

            if (syncUtilityScoringData)
            {
                entityManager.SetComponentData(entity, CreateCreatureUtilityBehaviorData(creature));
            }

            if (createdEntity)
            {
                entityManager.SetComponentData(entity, CreateCreatureActionStateData(entityManager, entity, creature, config));
                entityManager.SetComponentData(entity, CreateCreatureActionTargetData(entityManager, entity, creature, config));
                entityManager.SetComponentData(entity, CreateCreatureActionTimerData(entityManager, entity, creature, config));
                entityManager.SetComponentData(entity, GenomeFactory.FromCreature(creature));
                entityManager.SetComponentData(entity, GenomeFactory.SeedRandomState(instanceId, (uint)GetCreatureKind(creature)));
            }

            if (config == null || !config.useUtilityAI || !config.useEcsUtilityScoring)
            {
                entityManager.SetComponentData(entity, CreateCreatureUtilityDecisionData(creature));
            }
        }
    }

    private Entity GetOrCreateCreatureEntity(
        EntityManager entityManager,
        BaseCreatureBehaviour creature,
        int instanceId,
        out bool createdEntity)
    {
        if (creatureEntities.TryGetValue(instanceId, out Entity entity) && entityManager.Exists(entity))
        {
            EnsureCreatureComponents(entityManager, entity, creature);
            createdEntity = false;
            return entity;
        }

        entity = entityManager.CreateEntity();
        EnsureCreatureComponents(entityManager, entity, creature);

        creatureEntities[instanceId] = entity;
        createdEntity = true;
        return entity;
    }

    private bool TryApplyECSCreatureLifecycleResult(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        int instanceId)
    {
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<CreatureLifecycleData>(entity))
        {
            return false;
        }

        CreatureLifecycleData lifecycleData = entityManager.GetComponentData<CreatureLifecycleData>(entity);
        if (lifecycleData.gameObjectInstanceId != instanceId)
            return false;

        float externalEnergyDelta = creature.EnergyManager != null
            ? creature.EnergyManager.ConsumePendingECSExternalEnergyDelta()
            : 0f;
        if (!Mathf.Approximately(externalEnergyDelta, 0f))
        {
            lifecycleData.energyLevel = CreatureLifecycleCalculator.Clamp(
                lifecycleData.energyLevel + externalEnergyDelta,
                0f,
                lifecycleData.currentMaxEnergy);
            lifecycleData.starvationDespawnRequested = lifecycleData.energyLevel <= 0f;
            entityManager.SetComponentData(entity, lifecycleData);
        }

        creature.AgeManager?.ApplyECSLifecycle(
            lifecycleData.age,
            lifecycleData.maturityFraction,
            lifecycleData.speedAgeMultiplier,
            lifecycleData.senseAgeMultiplier,
            lifecycleData.desirabilityAgeMultiplier);
        creature.EnergyManager?.ApplyECSLifecycle(
            lifecycleData.energyLevel,
            lifecycleData.currentMaxEnergy);

        if (lifecycleData.oldAgeDespawnRequested)
        {
            lastEcsCreatureLifecycleDespawnRequestCount++;
            creature.Despawn(CreatureDeathReason.OldAge);
            return true;
        }

        if (lifecycleData.starvationDespawnRequested)
        {
            lastEcsCreatureLifecycleDespawnRequestCount++;
            creature.Despawn(CreatureDeathReason.EnergyDepleted);
            return true;
        }

        return false;
    }

    private void SyncFood(EntityManager entityManager, GameConfig config)
    {
        seenFoodIds.Clear();
        unresolvedFoodIds.Clear();
        lastEcsFoodLifecycleDespawnRequestCount = 0;

        FoodSpawner spawner = FoodSpawner.Instance;
        if (spawner != null && spawner.foods != null)
        {
            for (int i = 0; i < spawner.foods.Count; i++)
            {
                Food food = spawner.foods[i];
                if (food == null || !food.gameObject.activeInHierarchy || food.IsDespawnQueued)
                    continue;

                int instanceId = food.GetInstanceID();
                if (!seenFoodIds.Add(instanceId))
                    continue;

                if (!activeFoodByInstanceId.TryGetValue(instanceId, out Food cachedFood) ||
                    cachedFood != food)
                {
                    activeFoodByInstanceId[instanceId] = food;
                }

                unresolvedFoodIds.Remove(instanceId);
                Entity entity = GetOrCreateFoodEntity(entityManager, instanceId);

                if (config != null &&
                    config.useEcsFoodLifecycle &&
                    TryApplyECSFoodLifecycleResult(entityManager, entity, food, instanceId, config))
                {
                    if (entityManager.Exists(entity))
                    {
                        entityManager.DestroyEntity(entity);
                    }

                    foodEntities.Remove(instanceId);
                    activeFoodByInstanceId.Remove(instanceId);
                    unresolvedFoodIds.Remove(instanceId);
                    seenFoodIds.Remove(instanceId);
                    i--;
                    continue;
                }

                FoodMirrorData mirroredData = CreateFoodMirrorData(food, instanceId);
                if (config != null &&
                    config.useEcsFoodLifecycle &&
                    entityManager.HasComponent<FoodMirrorData>(entity))
                {
                    FoodMirrorData existingData = entityManager.GetComponentData<FoodMirrorData>(entity);
                    if (existingData.gameObjectInstanceId == instanceId)
                    {
                        mirroredData = MergeFoodMirrorRuntimeState(existingData, mirroredData);
                    }
                }

                entityManager.SetComponentData(entity, mirroredData);
            }
        }

        RemoveStaleEntities(entityManager, foodEntities, seenFoodIds);
        RemoveStaleObjects(activeFoodByInstanceId, seenFoodIds);
        lastActiveFoodCount = seenFoodIds.Count;
    }

    private Entity GetOrCreateFoodEntity(EntityManager entityManager, int instanceId)
    {
        if (foodEntities.TryGetValue(instanceId, out Entity entity) && entityManager.Exists(entity))
            return entity;

        entity = entityManager.CreateEntity();
        entityManager.AddComponent<FoodTag>(entity);
        entityManager.AddComponent<FoodMirrorData>(entity);
        foodEntities[instanceId] = entity;
        return entity;
    }

    private bool TryApplyECSFoodLifecycleResult(
        EntityManager entityManager,
        Entity entity,
        Food food,
        int instanceId,
        GameConfig config)
    {
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<FoodMirrorData>(entity))
        {
            return false;
        }

        FoodMirrorData lifecycleData = entityManager.GetComponentData<FoodMirrorData>(entity);
        if (lifecycleData.gameObjectInstanceId != instanceId)
            return false;

        bool isBeingEaten = food.IsBeingEaten;
        if (lifecycleData.despawnRequested && !isBeingEaten)
        {
            lastEcsFoodLifecycleDespawnRequestCount++;
            food.DestroyFromECSLifecycle();
            return true;
        }

        if (config != null)
        {
            if (lifecycleData.isBeingEaten)
            {
                if (!food.IsBeingEaten)
                    food.IsBeingEaten = true;
            }
            else if (food.IsBeingEaten)
            {
                food.StopEating();
            }
        }

        food.ApplyECSLifecycle(lifecycleData, !isBeingEaten && !lifecycleData.isBeingEaten);
        return false;
    }

    private void RemoveStaleEntities(
        EntityManager entityManager,
        Dictionary<int, Entity> entityByInstanceId,
        HashSet<int> activeInstanceIds)
    {
        staleIds.Clear();

        foreach (var pair in entityByInstanceId)
        {
            if (!activeInstanceIds.Contains(pair.Key))
                staleIds.Add(pair.Key);
        }

        for (int i = 0; i < staleIds.Count; i++)
        {
            int instanceId = staleIds[i];
            Entity entity = entityByInstanceId[instanceId];
            if (entityManager.Exists(entity))
            {
                entityManager.DestroyEntity(entity);
            }

            entityByInstanceId.Remove(instanceId);
            // No-op for food ids; only creatures register movement intents.
            CreatureMovementBatch.Instance?.Unregister(instanceId);
        }
    }

    private void RemoveStaleObjects<TObject>(
        Dictionary<int, TObject> objectByInstanceId,
        HashSet<int> activeInstanceIds)
        where TObject : class
    {
        staleIds.Clear();

        foreach (var pair in objectByInstanceId)
        {
            if (!activeInstanceIds.Contains(pair.Key))
                staleIds.Add(pair.Key);
        }

        for (int i = 0; i < staleIds.Count; i++)
        {
            objectByInstanceId.Remove(staleIds[i]);
        }
    }

    private void RemoveStaleData<TData>(
        Dictionary<int, TData> dataByInstanceId,
        HashSet<int> activeInstanceIds)
    {
        staleIds.Clear();

        foreach (var pair in dataByInstanceId)
        {
            if (!activeInstanceIds.Contains(pair.Key))
                staleIds.Add(pair.Key);
        }

        for (int i = 0; i < staleIds.Count; i++)
        {
            dataByInstanceId.Remove(staleIds[i]);
        }
    }

    private void DestroyAllMirroredEntities(EntityManager entityManager)
    {
        DestroyAllEntitiesIn(entityManager, creatureEntities);
        DestroyAllEntitiesIn(entityManager, foodEntities);
        activeCreaturesByInstanceId.Clear();
        activeFoodByInstanceId.Clear();
        unresolvedCreatureIds.Clear();
        unresolvedFoodIds.Clear();
        utilityDecisionByCreatureId.Clear();
        utilityContextByCreatureId.Clear();
        lastActiveCreatureCount = 0;
        lastActiveFoodCount = 0;
        RefreshMirrorDebugStatus();
    }

    private static void DestroyAllRequestEntities(EntityManager entityManager)
    {
        DestroyAllEntitiesWith<SpawnCreatureRequest>(entityManager);
        DestroyAllEntitiesWith<DespawnCreatureRequest>(entityManager);
        DestroyAllEntitiesWith<SpawnFoodRequest>(entityManager);
        DestroyAllEntitiesWith<DespawnFoodRequest>(entityManager);
    }

    private static void DestroyAllEntitiesWith<T>(EntityManager entityManager)
        where T : unmanaged, IComponentData
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            if (entityManager.Exists(entity))
            {
                entityManager.DestroyEntity(entity);
            }
        }
    }

    private static void EnsureCreatureComponents(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature)
    {
        AddComponentIfMissing<CreatureTag>(entityManager, entity);
        AddComponentIfMissing<CreatureIdentity>(entityManager, entity);
        AddComponentIfMissing<CreatureTransformMirror>(entityManager, entity);
        AddComponentIfMissing<CreatureAIContextData>(entityManager, entity);
        AddComponentIfMissing<CreatureUtilityBehaviorData>(entityManager, entity);
        AddComponentIfMissing<CreatureLifecycleData>(entityManager, entity);
        AddComponentIfMissing<CreatureObservationSensorData>(entityManager, entity);
        AddComponentIfMissing<CreatureObservationResultData>(entityManager, entity);
        AddComponentIfMissing<CreatureUtilityDecisionData>(entityManager, entity);
        AddComponentIfMissing<CreatureActionStateData>(entityManager, entity);
        AddComponentIfMissing<CreatureActionTargetData>(entityManager, entity);
        AddComponentIfMissing<CreatureActionTimerData>(entityManager, entity);
        AddComponentIfMissing<CreatureActionRequestData>(entityManager, entity);
        AddComponentIfMissing<CreatureWanderExecutionData>(entityManager, entity);
        AddComponentIfMissing<Genome>(entityManager, entity);
        AddComponentIfMissing<RandomState>(entityManager, entity);
        if (!entityManager.HasComponent<RejectedMateCooldown>(entity))
        {
            entityManager.AddBuffer<RejectedMateCooldown>(entity);
        }

        if (creature is HerbivoreBehaviour)
        {
            AddComponentIfMissing<HerbivoreTag>(entityManager, entity);
            if (!entityManager.HasComponent<FoodBlacklistCooldown>(entity))
            {
                entityManager.AddBuffer<FoodBlacklistCooldown>(entity);
            }
        }
        else if (creature is PredatorBehaviour)
        {
            AddComponentIfMissing<PredatorTag>(entityManager, entity);
            if (!entityManager.HasComponent<PreyBlacklistCooldown>(entity))
            {
                entityManager.AddBuffer<PreyBlacklistCooldown>(entity);
            }
        }
    }

    private static void AddComponentIfMissing<T>(EntityManager entityManager, Entity entity)
        where T : unmanaged, IComponentData
    {
        if (!entityManager.HasComponent<T>(entity))
        {
            entityManager.AddComponent<T>(entity);
        }
    }

    private bool TryGetUtilityAIContextFromMirror(BaseCreatureBehaviour creature, out UtilityAIContext context)
    {
        context = UtilityAIContext.Empty;

        int instanceId = creature.GetInstanceID();
        if (utilityContextByCreatureId.TryGetValue(instanceId, out context))
            return true;

        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (!creatureEntities.TryGetValue(instanceId, out Entity entity) ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<CreatureAIContextData>(entity))
        {
            return false;
        }

        CreatureAIContextData contextData = entityManager.GetComponentData<CreatureAIContextData>(entity);
        CreatureObservationResultData observationResult =
            entityManager.HasComponent<CreatureObservationResultData>(entity)
                ? entityManager.GetComponentData<CreatureObservationResultData>(entity)
                : default;

        context = UtilityAIContextFactory.FromECSMirrorData(contextData, observationResult);
        utilityContextByCreatureId[instanceId] = context;
        return true;
    }

    private bool TryGetUtilityAIDecisionFromMirror(
        BaseCreatureBehaviour creature,
        out CreatureUtilityDecisionData decisionData)
    {
        decisionData = default;

        int instanceId = creature.GetInstanceID();
        if (utilityDecisionByCreatureId.TryGetValue(instanceId, out decisionData))
            return true;

        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (!creatureEntities.TryGetValue(instanceId, out Entity entity) ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<CreatureUtilityDecisionData>(entity))
        {
            return false;
        }

        decisionData = entityManager.GetComponentData<CreatureUtilityDecisionData>(entity);
        utilityDecisionByCreatureId[instanceId] = decisionData;
        return true;
    }

    private bool TryGetCreatureActionStateFromMirror(
        BaseCreatureBehaviour creature,
        out CreatureActionStateData actionState)
    {
        actionState = default;

        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        int instanceId = creature.GetInstanceID();
        if (!creatureEntities.TryGetValue(instanceId, out Entity entity) ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<CreatureActionStateData>(entity))
        {
            return false;
        }

        actionState = entityManager.GetComponentData<CreatureActionStateData>(entity);
        return true;
    }

    private void UpdateECSObservationBridge(EntityManager entityManager, GameConfig config)
    {
        if (config == null || !config.useEcsObservation)
        {
            lastEcsObservation = 0f;
            return;
        }

        lastEcsObservation += Time.fixedDeltaTime;
        if (lastEcsObservation < config.updateInterval)
            return;

        lastEcsObservation = 0f;
        ApplyECSObservationResults(entityManager);
    }

    private void ApplyECSObservationResults(EntityManager entityManager)
    {
        lastEcsFoodObservationCount = 0;
        lastEcsPreyObservationCount = 0;
        lastEcsMateObservationCount = 0;

        foreach (BaseCreatureBehaviour creature in activeCreaturesByInstanceId.Values)
        {
            creature?.ObservationManager?.Observations.Clear();
        }

        foreach (var pair in creatureEntities)
        {
            int creatureInstanceId = pair.Key;
            Entity entity = pair.Value;
            if (!activeCreaturesByInstanceId.TryGetValue(creatureInstanceId, out BaseCreatureBehaviour creature) ||
                creature == null ||
                creature.ObservationManager == null ||
                !entityManager.Exists(entity) ||
                !entityManager.HasComponent<CreatureObservationResultData>(entity))
            {
                continue;
            }

            CreatureObservationResultData result = entityManager.GetComponentData<CreatureObservationResultData>(entity);

            if (creature is HerbivoreBehaviour &&
                result.closestFoodInstanceId != 0 &&
                activeFoodByInstanceId.TryGetValue(result.closestFoodInstanceId, out Food food) &&
                food != null &&
                food.gameObject.activeInHierarchy)
            {
                AddObservation(creature, ObservationType.Food, food.gameObject);
                lastEcsFoodObservationCount++;
            }

            if (creature is PredatorBehaviour &&
                result.closestPreyInstanceId != 0 &&
                activeCreaturesByInstanceId.TryGetValue(result.closestPreyInstanceId, out BaseCreatureBehaviour prey) &&
                prey != null &&
                prey.gameObject.activeInHierarchy)
            {
                AddObservation(creature, ObservationType.FoodCreature, prey.gameObject);
                lastEcsPreyObservationCount++;
            }

            if (result.closestMateInstanceId != 0 &&
                activeCreaturesByInstanceId.TryGetValue(result.closestMateInstanceId, out BaseCreatureBehaviour mate) &&
                mate != null &&
                mate.gameObject.activeInHierarchy &&
                creature.ReproductionManager != null &&
                creature.ReproductionManager.CanMateWith(mate))
            {
                AddObservation(creature, ObservationType.MatingCreature, mate.gameObject);
                lastEcsMateObservationCount++;
            }
        }
    }

    private static void AddObservation(
        BaseCreatureBehaviour creature,
        ObservationType type,
        GameObject observedObject)
    {
        ObservationData observationData = new ObservationData
        {
            observedObject = observedObject,
            type = type
        };

        creature.ObservationManager.Observations.Add(observationData);
    }

    private void RefreshMirrorDebugStatus()
    {
        lastMirroredCreatureCount = creatureEntities.Count;
        lastMirroredFoodCount = foodEntities.Count;
        lastMirroredEntityCount = lastMirroredCreatureCount + lastMirroredFoodCount;
        lastMirrorCountMatchesActiveObjects =
            lastMirroredCreatureCount == lastActiveCreatureCount &&
            lastMirroredFoodCount == lastActiveFoodCount;
        lastMirrorStatus =
            lastMirrorCountMatchesActiveObjects
                ? "OK"
                : "Mismatch";
    }

    private static void DestroyAllEntitiesIn(EntityManager entityManager, Dictionary<int, Entity> entityByInstanceId)
    {
        foreach (Entity entity in entityByInstanceId.Values)
        {
            if (entityManager.Exists(entity))
            {
                entityManager.DestroyEntity(entity);
            }
        }

        entityByInstanceId.Clear();
    }

    private static CreatureIdentity CreateCreatureIdentity(BaseCreatureBehaviour creature, int instanceId)
    {
        return new CreatureIdentity
        {
            gameObjectInstanceId = instanceId,
            creatureKind = GetCreatureKind(creature),
            sex = (int)creature.Sex
        };
    }

    private static int GetCreatureKind(BaseCreatureBehaviour creature)
    {
        if (creature is HerbivoreBehaviour)
            return ECSCreatureKind.Herbivore;

        if (creature is PredatorBehaviour)
            return ECSCreatureKind.Predator;

        return ECSCreatureKind.Unknown;
    }

    private static CreatureTransformMirror CreateCreatureTransformMirror(Transform source)
    {
        return new CreatureTransformMirror
        {
            position = ToFloat3(source.position),
            rotation = ToQuaternion(source.rotation),
            scale = ToFloat3(source.localScale)
        };
    }

    private static CreatureAIContextData CreateCreatureAIContextData(UtilityAIContext context)
    {
        return new CreatureAIContextData
        {
            energyPercent = context.energyPercent,
            isMature = context.isMature,
            isReproductionReady = context.isReproductionReady,
            currentState = context.currentState,
            hasKnownFood = context.hasKnownFood,
            hasKnownMate = context.hasKnownMate,
            hasKnownPrey = context.hasKnownPrey,
            isThreatened = context.isThreatened
        };
    }

    private static CreatureUtilityBehaviorData CreateCreatureUtilityBehaviorData(BaseCreatureBehaviour creature)
    {
        if (creature == null)
            return UtilityBehaviorScoring.DefaultProfile;

        return UtilityBehaviorScoring.Sanitize(creature.UtilityBehaviorProfile);
    }

    private static CreatureLifecycleData CreateCreatureLifecycleData(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        int instanceId)
    {
        CreatureLifecycleData previous = entityManager.HasComponent<CreatureLifecycleData>(entity)
            ? entityManager.GetComponentData<CreatureLifecycleData>(entity)
            : default;
        bool canPreservePrevious = previous.gameObjectInstanceId == instanceId;
        float currentMaxEnergy = creature.EnergyManager != null ? creature.EnergyManager.CurrentMaxEnergy : 0f;
        CreatureLifecycleData data = new CreatureLifecycleData
        {
            gameObjectInstanceId = instanceId,
            creatureKind = GetCreatureKind(creature),
            currentState = creature.CurrentStateType,
            age = creature.AgeManager != null ? creature.AgeManager.Age : 0f,
            maturityFraction = creature.AgeManager != null ? creature.AgeManager.MaturityFraction : 1f,
            energyLevel = creature.EnergyManager != null ? creature.EnergyManager.EnergyLevel : 0f,
            maxEnergy = creature.EnergyManager != null ? creature.EnergyManager.BaseMaxEnergy : creature.maxEnergy,
            currentMaxEnergy = currentMaxEnergy,
            weight = creature.Weight,
            baseMoveSpeed = creature.MovementManager != null ? creature.MovementManager.BaseMoveSpeed : 0.01f,
            baseSenseRadius = creature.ObservationManager != null ? creature.ObservationManager.BaseSenseRadius : 0.01f,
            energyConsumption = canPreservePrevious ? previous.energyConsumption : 0f,
            speedAgeMultiplier = canPreservePrevious ? previous.speedAgeMultiplier : 1f,
            senseAgeMultiplier = canPreservePrevious ? previous.senseAgeMultiplier : 1f,
            desirabilityAgeMultiplier = canPreservePrevious ? previous.desirabilityAgeMultiplier : 1f,
            accumulatedDeltaTime = canPreservePrevious ? previous.accumulatedDeltaTime : 0f,
            starvationDespawnRequested = false,
            oldAgeDespawnRequested = false
        };

        GameConfig config = GameConfig.Instance;
        if (config != null)
        {
            CreatureLifecycleCalculator.RefreshDerivedValues(
                ref data,
                CreatureLifecycleParameters.FromConfig(config));
        }

        return data;
    }

    private static CreatureObservationSensorData CreateCreatureObservationSensorData(BaseCreatureBehaviour creature)
    {
        return new CreatureObservationSensorData
        {
            senseRadius = creature.ObservationManager != null ? creature.ObservationManager.SenseRadius : 0f
        };
    }

    private static CreatureActionStateData CreateCreatureActionStateData(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        GameConfig config)
    {
        if (entityManager.HasComponent<CreatureActionStateData>(entity))
        {
            return entityManager.GetComponentData<CreatureActionStateData>(entity);
        }

        return new CreatureActionStateData
        {
            currentAction = CreatureAction.None,
            phase = CreatureActionPhase.Idle,
            status = CreatureActionStatus.None,
            legacyStateType = CreatureStateType.Idle,
            canCancel = false,
            canComplete = false
        };
    }

    private static CreatureActionTargetData CreateCreatureActionTargetData(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        GameConfig config)
    {
        if (entityManager.HasComponent<CreatureActionTargetData>(entity))
        {
            return entityManager.GetComponentData<CreatureActionTargetData>(entity);
        }

        return new CreatureActionTargetData
        {
            targetInstanceId = 0
        };
    }

    private static CreatureActionTimerData CreateCreatureActionTimerData(
        EntityManager entityManager,
        Entity entity,
        BaseCreatureBehaviour creature,
        GameConfig config)
    {
        if (entityManager.HasComponent<CreatureActionTimerData>(entity))
        {
            return entityManager.GetComponentData<CreatureActionTimerData>(entity);
        }

        return new CreatureActionTimerData
        {
            elapsedTime = 0f,
            remainingTime = -1f
        };
    }

    private static CreatureActionPhase GetRequestedPhaseForAction(CreatureAction action)
    {
        switch (action)
        {
            case CreatureAction.SearchFood:
            case CreatureAction.SearchMate:
            case CreatureAction.Hunt:
                return CreatureActionPhase.Searching;
            case CreatureAction.Wander:
                return CreatureActionPhase.Wandering;
            case CreatureAction.None:
            case CreatureAction.Flee:
            default:
                return CreatureActionPhase.None;
        }
    }

    private static CreatureUtilityDecisionData CreateCreatureUtilityDecisionData(BaseCreatureBehaviour creature)
    {
        UtilityDecision decision = creature.LastUtilityAIDecision;
        CreatureUtilityDecisionData decisionData = new CreatureUtilityDecisionData
        {
            hasDecision = decision != null && decision.HasSelection,
            selectedAction = creature.LastUtilityAISelectedAction,
            selectedScore = creature.LastUtilityAISelectedScore,
            decisionTime = creature.LastUtilityAIDecisionTime,
            transitionState = creature.LastUtilityAITransitionStateType,
            resultState = creature.LastUtilityAIResultStateType
        };

        IReadOnlyList<UtilityActionScore> actionScores = creature.LastUtilityAIActionScores;
        for (int i = 0; i < actionScores.Count; i++)
        {
            ApplyUtilityActionScore(ref decisionData, actionScores[i]);
        }

        return decisionData;
    }

    private static void ApplyUtilityActionScore(
        ref CreatureUtilityDecisionData decisionData,
        UtilityActionScore actionScore)
    {
        switch (actionScore.Action)
        {
            case CreatureAction.None:
                decisionData.keepCurrentStateScore = actionScore.Score;
                break;
            case CreatureAction.SearchFood:
            case CreatureAction.Hunt:
                decisionData.foodActionScore = actionScore.Score;
                break;
            case CreatureAction.SearchMate:
                decisionData.searchMateScore = actionScore.Score;
                break;
            case CreatureAction.Wander:
                decisionData.wanderScore = actionScore.Score;
                break;
        }
    }

    private static FoodMirrorData CreateFoodMirrorData(Food food, int instanceId)
    {
        BaseCreatureBehaviour eatingCreature = food.GetEatingCreature();
        float maxNutrition = food.MaxNutritionValue;
        float growthFraction = food.GrowthFraction;
        return new FoodMirrorData
        {
            gameObjectInstanceId = instanceId,
            nutritionValue = food.nutritionValue,
            maxNutritionValue = maxNutrition,
            nutritionPercent = maxNutrition > 0f ? math.clamp(food.nutritionValue / maxNutrition, 0f, 1f) : 0f,
            age = food.age,
            growthFraction = growthFraction,
            despawnRequested = false,
            isBeingEaten = food.IsBeingEaten,
            eatingCreatureInstanceId = eatingCreature != null ? eatingCreature.GetInstanceID() : 0,
            position = ToFloat3(food.transform.position),
            rotation = ToQuaternion(food.transform.rotation),
            scale = ToFloat3(food.transform.localScale)
        };
    }

    private static FoodMirrorData MergeFoodMirrorRuntimeState(
        FoodMirrorData existingData,
        FoodMirrorData mirroredData)
    {
        if (existingData.despawnRequested)
            mirroredData.despawnRequested = true;

        if (existingData.isBeingEaten)
        {
            mirroredData.isBeingEaten = true;
            if (mirroredData.eatingCreatureInstanceId == 0)
                mirroredData.eatingCreatureInstanceId = existingData.eatingCreatureInstanceId;
        }
        else if (mirroredData.eatingCreatureInstanceId != 0)
        {
            mirroredData.isBeingEaten = true;
        }

        return mirroredData;
    }

    private static float3 ToFloat3(Vector3 source)
    {
        return new float3(source.x, source.y, source.z);
    }

    private static quaternion ToQuaternion(Quaternion source)
    {
        return new quaternion(source.x, source.y, source.z, source.w);
    }
}
