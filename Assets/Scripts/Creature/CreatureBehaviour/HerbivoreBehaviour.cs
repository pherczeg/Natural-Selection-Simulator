using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class HerbivoreBehaviour : BaseCreatureBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private const float MinThreatScanInterval = 0.05f;
    private const float MaxThreatScanInterval = 0.25f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const double SlowFixedUpdateThresholdMs = 0.35d;
    private static readonly double StopwatchTicksToMs = 1000d / Stopwatch.Frequency;
    private static int lastSlowFixedUpdateLogFrame = -1;
    private string transitionPerfPath = "Unknown";
    private string transitionPerfStep = "None";
    private double transitionPerfLegacyMs;
    private double transitionPerfEnsureBrainMs;
    private double transitionPerfEcsDecisionMs;
    private double transitionPerfEvaluateMs;
    private double transitionPerfExecuteMs;
    private double transitionPerfRecordMs;
    private double transitionPerfLogScoresMs;
#endif
    private BaseCreatureBehaviour forcedThreat;
    private BaseCreatureBehaviour cachedThreat;
    private float forcedFleeUntilTime;
    private float activeFleeUntilTime;
    private float nextThreatScanTime;
    private float nextUtilityDecisionTime;
    private Vector3 lastFleeDirection;
    private BaseCreatureBehaviour capturePredator;
    [SerializeField] private HerbivoreSocialStrategy socialStrategy = HerbivoreSocialStrategy.Dove;

    public float Agility { get; private set; }
    public HerbivoreSocialStrategy SocialStrategy => socialStrategy;
    public bool IsHawk => socialStrategy == HerbivoreSocialStrategy.Hawk;
    public bool IsCaptured => capturePredator != null;
    public bool IsThreatened => forcedThreat != null || cachedThreat != null || Time.time < activeFleeUntilTime;

    public void SetSocialStrategy(HerbivoreSocialStrategy strategy)
    {
        socialStrategy = strategy;
    }

    public bool IsCapturedBy(BaseCreatureBehaviour predator)
    {
        return predator != null && capturePredator == predator;
    }

    protected override void OnSexChanged()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return;

        Color targetColor = Sex == CreatureSex.Female ? config.herbivoreFemaleColor : config.herbivoreMaleColor;
        ApplySexColor(targetColor);
    }

    private void ApplySexColor(Color targetColor)
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var propertyBlock = new MaterialPropertyBlock();

        foreach (var rendererComponent in renderers)
        {
            if (rendererComponent == null || rendererComponent.sharedMaterial == null)
                continue;

            rendererComponent.GetPropertyBlock(propertyBlock);

            if (rendererComponent.sharedMaterial.HasProperty(BaseColorPropertyId))
            {
                propertyBlock.SetColor(BaseColorPropertyId, targetColor);
            }
            else if (rendererComponent.sharedMaterial.HasProperty(ColorPropertyId))
            {
                propertyBlock.SetColor(ColorPropertyId, targetColor);
            }
            else
            {
                continue;
            }

            rendererComponent.SetPropertyBlock(propertyBlock);
        }
    }

    protected override void DestroyObject()
    {
        PoolManager.Instance.ReturnObject(CreatureSpawner.Instance.herbivorPrefab, this.gameObject);
        EatingManager.InterruptEating();
        creatureSpawner.RemoveFromList(this);
    }
    protected override void CheckTransitions()
    {
        var config = GameConfig.Instance;
        if (config != null && config.useUtilityAI)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transitionPerfPath = config.useEcsUtilityScoring ? "UtilityAI(ECS)" : "UtilityAI(Local)";
#endif
            UpdateUtilityAI(config);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfPath = "Legacy";
        long legacyStartTimestamp = Stopwatch.GetTimestamp();
#endif
        CheckLegacyTransitions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfLegacyMs = ElapsedMilliseconds(legacyStartTimestamp, Stopwatch.GetTimestamp());
        transitionPerfStep = "LegacyComplete";
#endif
    }

    private void CheckLegacyTransitions()
    {
        CreatureStateType currentState = CurrentStateType;
        if (currentState == CreatureStateType.MovingToFood || currentState == CreatureStateType.Eating || currentState == CreatureStateType.SearchingForFood || currentState == CreatureStateType.Reproducting)
        {
            return;
        }
        else if (EnergyManager.EnergyLevel < GameConfig.Instance.eatingEnergyThreshold * EnergyManager.CurrentMaxEnergy)
        {
            CreatureActionExecutor.Execute(this, CreatureAction.SearchFood);
        }
        else if (currentState == CreatureStateType.SearchingForMate || currentState == CreatureStateType.MovingToMate)
        {
            return;
        }
        else if (!ReproductionManager.IsOnCooldown() && ReproductionManager.IsReadyToReproduction())
        {
            CreatureActionExecutor.Execute(this, CreatureAction.SearchMate);
        }
        else if (currentState == CreatureStateType.Idle || currentState == CreatureStateType.None)
        {
            CreatureActionExecutor.Execute(this, CreatureAction.Wander);
        }
    }

    private void UpdateUtilityAI(GameConfig config)
    {
        if (Time.time < nextUtilityDecisionTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transitionPerfStep = "UtilityIntervalSkip";
#endif
            return;
        }

        float decisionInterval = Mathf.Max(0.01f, config.utilityDecisionInterval);

        if (config.useEcsUtilityScoring)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long ensureBrainStartTimestamp = Stopwatch.GetTimestamp();
#endif
            EnsureUtilityBrain();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transitionPerfEnsureBrainMs = ElapsedMilliseconds(ensureBrainStartTimestamp, Stopwatch.GetTimestamp());
            long ecsDecisionStartTimestamp = Stopwatch.GetTimestamp();
#endif
            bool executedDecision = TryExecuteECSUtilityDecision(config);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transitionPerfEcsDecisionMs = ElapsedMilliseconds(ecsDecisionStartTimestamp, Stopwatch.GetTimestamp());
            transitionPerfStep = executedDecision ? "ECSDecisionExecuted" : "ECSDecisionDeferred";
#endif
            float nextDecisionDelay = executedDecision
                ? GetJitteredUtilityDecisionDelay(decisionInterval, 0.35f)
                : GetJitteredUtilityDecisionDelay(Mathf.Max(0.02f, decisionInterval * 0.5f), 0.5f);
            nextUtilityDecisionTime = Time.time + nextDecisionDelay;
            return;
        }

        nextUtilityDecisionTime = Time.time + GetJitteredUtilityDecisionDelay(decisionInterval, 0.2f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long localEnsureBrainStartTimestamp = Stopwatch.GetTimestamp();
#endif
        EnsureUtilityBrain();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfEnsureBrainMs = ElapsedMilliseconds(localEnsureBrainStartTimestamp, Stopwatch.GetTimestamp());
#endif

        if (utilityBrain == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transitionPerfStep = "UtilityBrainMissing";
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long evaluateStartTimestamp = Stopwatch.GetTimestamp();
#endif
        UtilityDecision decision = utilityBrain.Evaluate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfEvaluateMs = ElapsedMilliseconds(evaluateStartTimestamp, Stopwatch.GetTimestamp());
        long executeStartTimestamp = Stopwatch.GetTimestamp();
#endif
        CreatureActionExecutionResult execution = ExecuteUtilityAIAction(decision);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfExecuteMs = ElapsedMilliseconds(executeStartTimestamp, Stopwatch.GetTimestamp());
        long recordStartTimestamp = Stopwatch.GetTimestamp();
#endif
        RecordUtilityAIExecution(decision, execution);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfRecordMs = ElapsedMilliseconds(recordStartTimestamp, Stopwatch.GetTimestamp());
#endif

        if (config.logUtilityAIScores)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long logScoresStartTimestamp = Stopwatch.GetTimestamp();
#endif
            LogUtilityAIScores(decision);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transitionPerfLogScoresMs = ElapsedMilliseconds(logScoresStartTimestamp, Stopwatch.GetTimestamp());
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionPerfStep = "UtilityComplete";
#endif
    }

    private void EnsureUtilityBrain()
    {
        CreatureUtilityBrain utilityBrain = EnsureUtilityBrainComponent();

        if (utilityBrain.Creature != this || utilityBrain.Actions.Count == 0)
        {
            utilityBrain.Initialize(this);
            utilityBrain.SetActions(CreateHerbivoreUtilityActions());
        }
    }

    private IEnumerable<UtilityAction> CreateHerbivoreUtilityActions()
    {
        return new[]
        {
            new UtilityAction(
                CreatureAction.None,
                "Keep Current State",
                new[]
                {
                    new UtilityConsideration("Current State Lockout", context => UtilityAIScoreRules.GetKeepCurrentStateScore(context, GetUtilityAIScoringParameters(), ECSCreatureKind.Herbivore))
                }),
            new UtilityAction(
                CreatureAction.SearchFood,
                "Search Food",
                new[]
                {
                    new UtilityConsideration("Hunger", context => UtilityAIScoreRules.GetHungerScore(context, GetUtilityAIScoringParameters()) * UtilityAIScoreRules.GetFoodSearchAvailabilityScore(context, GetUtilityAIScoringParameters(), ECSCreatureKind.Herbivore))
                }),
            new UtilityAction(
                CreatureAction.SearchMate,
                "Search Mate",
                new[]
                {
                    new UtilityConsideration("Reproduction Readiness", context => UtilityAIScoreRules.GetReproductionScore(context, GetUtilityAIScoringParameters()) * UtilityAIScoreRules.GetMateSearchAvailabilityScore(context))
                },
                UtilityAIDefaultScorer.SearchMateBaseScore),
            new UtilityAction(
                CreatureAction.Wander,
                "Wander",
                new[]
                {
                    new UtilityConsideration("Idle Wander", UtilityAIScoreRules.GetIdleWanderScore)
                },
                UtilityAIDefaultScorer.WanderBaseScore)
        };
    }

    private static UtilityAIScoringParameters GetUtilityAIScoringParameters()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return UtilityAIScoringParameters.Default;

        return new UtilityAIScoringParameters(
            config.eatingEnergyThreshold,
            config.GetReproductionEnergyThreshold(false));
    }

    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        var config = GameConfig.Instance;
        EnsureUtilityBehaviorProfileInitialized();
        ResetDespawnRequestState();
        ResetUtilityAIDebugState();
        nextUtilityDecisionTime = 0f;
        forcedThreat = null;
        cachedThreat = null;
        forcedFleeUntilTime = 0f;
        activeFleeUntilTime = 0f;
        nextThreatScanTime = Time.time + Random.Range(0f, GetThreatScanInterval(config));
        lastFleeDirection = Vector3.zero;
        capturePredator = null;
        maxEnergy = config.herbivoreMaxEnergy > 0f ? config.herbivoreMaxEnergy : config.maxEnergy;
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        AgeManager = new AgeManager(this);
        ReproductionManager = new ReproductionManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        EnergyManager = new EnergyManager(this, maxEnergy * config.initialEnergyPercentageHerbivore, maxEnergy);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
        SetAgility(Mathf.Lerp(config.herbivoreAgilityMin, config.herbivoreAgilityMax, 0.5f));
        if (config.useUtilityAI)
        {
            EnsureUtilityBrain();
            float initialDecisionDelay = GetJitteredUtilityDecisionDelay(
                Mathf.Max(0.02f, config.utilityDecisionInterval * 0.5f),
                0.75f);
            nextUtilityDecisionTime = Time.time + initialDecisionDelay;
        }
    }
    void FixedUpdate()
    {
        if (IsDespawnQueued)
            return;

        var config = GameConfig.Instance;
        if (config == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long fixedUpdateStartTimestamp = Stopwatch.GetTimestamp();
        double tempEffectsMs = 0d;
        double lifecycleMs = 0d;
        double fleeMs = 0d;
        double transitionsMs = 0d;
        string lifecycleSection = "Skipped";
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long tempEffectsStartTimestamp = Stopwatch.GetTimestamp();
#endif
        MovementManager.UpdateTemporaryEffects(Time.fixedDeltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        tempEffectsMs = ElapsedMilliseconds(tempEffectsStartTimestamp, Stopwatch.GetTimestamp());
#endif

        lastObservation += Time.fixedDeltaTime;

        if (lastObservation >= config.updateInterval)
        {
            float simulationDeltaTime = lastObservation;
            lastObservation = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long lifecycleStartTimestamp = Stopwatch.GetTimestamp();
#endif
            if (config.useEcsCreatureLifecycle)
            {
                ReproductionManager.UpdateReproductionCooldown(simulationDeltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                lifecycleSection = "ReproductionCooldown";
                lifecycleMs = ElapsedMilliseconds(lifecycleStartTimestamp, Stopwatch.GetTimestamp());
#endif
            }
            else
            {
                bool aborted = UpdateSlowSimulation(simulationDeltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                lifecycleSection = aborted ? "SlowSimulation(Aborted)" : "SlowSimulation";
                lifecycleMs = ElapsedMilliseconds(lifecycleStartTimestamp, Stopwatch.GetTimestamp());
                if (aborted)
                {
                    LogSlowFixedUpdateBreakdown(
                        fixedUpdateStartTimestamp,
                        tempEffectsMs,
                        lifecycleMs,
                        fleeMs,
                        transitionsMs,
                        lifecycleSection,
                        "SlowSimulationAbort");
                    return;
                }
#else
                if (aborted)
                {
                    return;
                }
#endif
            }
        }

        if (IsCaptured)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogSlowFixedUpdateBreakdown(
                fixedUpdateStartTimestamp,
                tempEffectsMs,
                lifecycleMs,
                fleeMs,
                transitionsMs,
                lifecycleSection,
                "Captured");
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long fleeStartTimestamp = Stopwatch.GetTimestamp();
#endif
        if (TryHandlePredatorFlee(config))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            fleeMs = ElapsedMilliseconds(fleeStartTimestamp, Stopwatch.GetTimestamp());
            LogSlowFixedUpdateBreakdown(
                fixedUpdateStartTimestamp,
                tempEffectsMs,
                lifecycleMs,
                fleeMs,
                transitionsMs,
                lifecycleSection,
                "PredatorFlee");
#endif
            return;
        }

        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        fleeMs = ElapsedMilliseconds(fleeStartTimestamp, Stopwatch.GetTimestamp());

        ResetTransitionPerfBreakdown();
        long transitionsStartTimestamp = Stopwatch.GetTimestamp();
#endif

        CheckTransitions();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        transitionsMs = ElapsedMilliseconds(transitionsStartTimestamp, Stopwatch.GetTimestamp());
        LogSlowFixedUpdateBreakdown(
            fixedUpdateStartTimestamp,
            tempEffectsMs,
            lifecycleMs,
            fleeMs,
            transitionsMs,
            lifecycleSection,
            "Completed");
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static double ElapsedMilliseconds(long startTimestamp, long endTimestamp)
    {
        return (endTimestamp - startTimestamp) * StopwatchTicksToMs;
    }

    private void ResetTransitionPerfBreakdown()
    {
        transitionPerfPath = "Unknown";
        transitionPerfStep = "None";
        transitionPerfLegacyMs = 0d;
        transitionPerfEnsureBrainMs = 0d;
        transitionPerfEcsDecisionMs = 0d;
        transitionPerfEvaluateMs = 0d;
        transitionPerfExecuteMs = 0d;
        transitionPerfRecordMs = 0d;
        transitionPerfLogScoresMs = 0d;
    }

    private string GetTransitionPerfSummary()
    {
        return
            $"transitionPath={transitionPerfPath}, step={transitionPerfStep}, legacy={transitionPerfLegacyMs:F3} ms, " +
            $"ensureBrain={transitionPerfEnsureBrainMs:F3} ms, ecsDecision={transitionPerfEcsDecisionMs:F3} ms, " +
            $"evaluate={transitionPerfEvaluateMs:F3} ms, execute={transitionPerfExecuteMs:F3} ms, " +
            $"record={transitionPerfRecordMs:F3} ms, logScores={transitionPerfLogScoresMs:F3} ms";
    }

    private void LogSlowFixedUpdateBreakdown(
        long fixedUpdateStartTimestamp,
        double tempEffectsMs,
        double lifecycleMs,
        double fleeMs,
        double transitionsMs,
        string lifecycleSection,
        string exitPoint)
    {
        double totalMs = ElapsedMilliseconds(fixedUpdateStartTimestamp, Stopwatch.GetTimestamp());
        if (totalMs < SlowFixedUpdateThresholdMs || lastSlowFixedUpdateLogFrame == Time.frameCount)
            return;

        lastSlowFixedUpdateLogFrame = Time.frameCount;
        UnityEngine.Debug.LogWarning(
            $"[Perf][Herbivore] Slow FixedUpdate {totalMs:F3} ms (exit={exitPoint}, lifecycle={lifecycleSection}) " +
            $"tempEffects={tempEffectsMs:F3} ms, lifecycle={lifecycleMs:F3} ms, flee={fleeMs:F3} ms, transitions={transitionsMs:F3} ms, " +
            GetTransitionPerfSummary(),
            this);
    }
#endif

    public void SetAgility(float agility)
    {
        var config = GameConfig.Instance;
        if (config == null)
        {
            Agility = Mathf.Max(0f, agility);
            return;
        }

        float min = Mathf.Min(config.herbivoreAgilityMin, config.herbivoreAgilityMax);
        float max = Mathf.Max(config.herbivoreAgilityMin, config.herbivoreAgilityMax);
        Agility = Mathf.Clamp(agility, min, max);
    }

    public bool TryStartCapture(BaseCreatureBehaviour predator)
    {
        if (predator == null)
            return false;

        if (capturePredator != null && capturePredator != predator)
            return false;

        capturePredator = predator;
        forcedThreat = null;
        cachedThreat = null;
        forcedFleeUntilTime = 0f;
        activeFleeUntilTime = 0f;
        nextThreatScanTime = Time.time;
        lastFleeDirection = Vector3.zero;

        if (CurrentStateType == CreatureStateType.Eating)
        {
            EatingManager.InterruptEating();
        }

        return true;
    }

    public void ReleaseCapture(BaseCreatureBehaviour predator)
    {
        if (capturePredator == null || capturePredator == predator)
        {
            capturePredator = null;
        }
    }

    public void TriggerEscapeFrom(BaseCreatureBehaviour predator)
    {
        forcedThreat = predator;
        cachedThreat = predator;
        forcedFleeUntilTime = Time.time + Mathf.Max(0f, GameConfig.Instance.herbivorePostEscapeFleeDuration);
        activeFleeUntilTime = forcedFleeUntilTime;
        nextThreatScanTime = Time.time;
        lastFleeDirection = GetFlatDirectionAwayFrom(predator);
    }

    private bool TryHandlePredatorFlee(GameConfig config)
    {
        BaseCreatureBehaviour predator = GetNearestPredatorThreat(config);
        Vector3 awayDirection;
        Vector3 threatForward;

        if (predator != null)
        {
            awayDirection = GetFlatDirectionAwayFrom(predator);
            threatForward = predator.transform.forward;
            activeFleeUntilTime = Time.time + Mathf.Max(0f, config.herbivoreFleeMemoryDuration);
        }
        else if (Time.time < activeFleeUntilTime && lastFleeDirection.sqrMagnitude > 0.0001f)
        {
            awayDirection = lastFleeDirection.normalized;
            threatForward = -awayDirection;
        }
        else
        {
            lastFleeDirection = Vector3.zero;
            return false;
        }

        if (CurrentStateType == CreatureStateType.Eating)
        {
            EatingManager.InterruptEating();
        }

        threatForward.y = 0f;
        if (awayDirection.sqrMagnitude < 0.0001f)
        {
            awayDirection = lastFleeDirection.sqrMagnitude > 0.0001f ? lastFleeDirection.normalized : transform.forward;
        }

        if (threatForward.sqrMagnitude < 0.0001f)
        {
            threatForward = -awayDirection;
        }

        Vector3 fleeDirection = GetObstacleAwareFleeDirection(awayDirection, threatForward, config.herbivoreFleeObstacleProbeDistance);
        fleeDirection = MovementManager.SteerDirectionInsideBounds(fleeDirection, config.herbivoreFleeObstacleProbeDistance);
        lastFleeDirection = fleeDirection;
        MovementManager.TryStartSprint();
        Vector3 fleeTarget = transform.position + fleeDirection * Mathf.Max(0.1f, config.herbivoreFleeDistance);
        MovementManager.MoveTowards(fleeTarget);
        return true;
    }

    private BaseCreatureBehaviour GetNearestPredatorThreat(GameConfig config)
    {
        if (forcedThreat != null && Time.time < forcedFleeUntilTime && forcedThreat.gameObject.activeInHierarchy)
            return forcedThreat;

        if (forcedThreat != null && (Time.time >= forcedFleeUntilTime || !forcedThreat.gameObject.activeInHierarchy))
        {
            forcedThreat = null;
        }

        if (creatureSpawner == null || creatureSpawner.predatorCreatures == null || ObservationManager == null)
            return null;

        if (cachedThreat != null && !IsValidThreat(cachedThreat))
        {
            cachedThreat = null;
        }

        if (Time.time < nextThreatScanTime)
        {
            return cachedThreat;
        }

        nextThreatScanTime = Time.time + GetThreatScanInterval(config);
        float maxDistance = ObservationManager.SenseRadius;
        float closestDistSq = maxDistance * maxDistance;
        BaseCreatureBehaviour closest = null;

        foreach (var predator in creatureSpawner.predatorCreatures)
        {
            if (predator == null || predator == this || !predator.gameObject.activeInHierarchy)
                continue;

            Vector3 delta = predator.transform.position - transform.position;
            delta.y = 0f;
            float distSq = delta.sqrMagnitude;
            if (distSq <= closestDistSq)
            {
                closestDistSq = distSq;
                closest = predator;
            }
        }

        cachedThreat = closest;
        return closest;
    }

    private bool IsValidThreat(BaseCreatureBehaviour predator)
    {
        if (predator == null || predator == this || !predator.gameObject.activeInHierarchy || ObservationManager == null)
            return false;

        Vector3 delta = predator.transform.position - transform.position;
        delta.y = 0f;
        float senseRadius = ObservationManager.SenseRadius;
        return delta.sqrMagnitude <= senseRadius * senseRadius;
    }

    private static float GetThreatScanInterval(GameConfig config)
    {
        if (config == null)
            return MaxThreatScanInterval;

        return Mathf.Clamp(config.updateInterval, MinThreatScanInterval, MaxThreatScanInterval);
    }

    private Vector3 GetFlatDirectionAwayFrom(BaseCreatureBehaviour threat)
    {
        if (threat == null)
            return Vector3.zero;

        Vector3 awayDirection = transform.position - threat.transform.position;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude < 0.0001f)
        {
            awayDirection = -threat.transform.forward;
            awayDirection.y = 0f;
        }

        return awayDirection.sqrMagnitude > 0.0001f ? awayDirection.normalized : Vector3.zero;
    }

    private Vector3 GetObstacleAwareFleeDirection(Vector3 awayDirection, Vector3 predatorForward, float probeDistance)
    {
        if (!IsBlockedInDirection(awayDirection, probeDistance))
            return awayDirection;

        Vector3 right = Vector3.Cross(Vector3.up, awayDirection).normalized;
        Vector3 left = -right;

        Vector3 preferred = Vector3.Dot(predatorForward, right) >= 0f ? right : left;
        Vector3 fallback = preferred == right ? left : right;

        if (!IsBlockedInDirection(preferred, probeDistance))
            return preferred;

        if (!IsBlockedInDirection(fallback, probeDistance))
            return fallback;

        return awayDirection;
    }

    private bool IsBlockedInDirection(Vector3 direction, float probeDistance)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        if (!Physics.Raycast(transform.position + Vector3.up * 0.2f, direction.normalized, out RaycastHit hit, Mathf.Max(0.1f, probeDistance)))
            return false;

        return !hit.collider.CompareTag("Ground") && !hit.collider.isTrigger;
    }

    // Returns true if the creature was destroyed (pooled) and FixedUpdate should abort.
    private bool UpdateSlowSimulation(float deltaTime)
    {
        var energyConsumption = EnergyManager.CalculateEnergyConsumption();
        EnergyManager.ConsumeEnergy(energyConsumption);

        AgeManager.UpdateAge(deltaTime);

        if (AgeManager.IsMaxAgeReached())
        {
            Despawn(CreatureDeathReason.OldAge);
            return true;
        }

        if (EnergyManager.IsEnergyDepleted())
        {
            Despawn(CreatureDeathReason.EnergyDepleted);
            return true;
        }

        ReproductionManager.UpdateReproductionCooldown(deltaTime);

        return false;
    }
}
