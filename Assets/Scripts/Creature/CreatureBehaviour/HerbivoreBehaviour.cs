using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HerbivoreBehaviour : BaseCreatureBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private const float MinThreatScanInterval = 0.05f;
    private const float MaxThreatScanInterval = 0.25f;
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
            UpdateUtilityAI(config);
            return;
        }

        CheckLegacyTransitions();
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
            return;

        float decisionInterval = Mathf.Max(0.01f, config.utilityDecisionInterval);

        if (config.useEcsUtilityScoring)
        {
            EnsureUtilityBrain();
            nextUtilityDecisionTime = Time.time + (TryExecuteECSUtilityDecision(config) ? decisionInterval : 0.01f);
            return;
        }

        nextUtilityDecisionTime = Time.time + decisionInterval;
        EnsureUtilityBrain();

        if (utilityBrain == null)
            return;

        UtilityDecision decision = utilityBrain.Evaluate();
        CreatureActionExecutionResult execution = ExecuteUtilityAIAction(decision);
        RecordUtilityAIExecution(decision, execution);

        if (config.logUtilityAIScores)
        {
            LogUtilityAIScores(decision);
        }
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
                    new UtilityConsideration("Current State Lockout", context => UtilityAIScoreRules.GetKeepCurrentStateScore(context, GetUtilityAIScoringParameters()))
                }),
            new UtilityAction(
                CreatureAction.SearchFood,
                "Search Food",
                new[]
                {
                    new UtilityConsideration("Hunger", context => UtilityAIScoreRules.GetHungerScore(context, GetUtilityAIScoringParameters()) * UtilityAIScoreRules.GetFoodSearchAvailabilityScore(context))
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
        }
    }
    void FixedUpdate()
    {
        if (IsDespawnQueued)
            return;

        var config = GameConfig.Instance;
        if (config == null) return;

        MovementManager.UpdateTemporaryEffects(Time.fixedDeltaTime);

        lastObservation += Time.fixedDeltaTime;

        if (lastObservation >= config.updateInterval)
        {
            float simulationDeltaTime = lastObservation;
            lastObservation = 0f;
            if (config.useEcsCreatureLifecycle)
            {
                ReproductionManager.UpdateReproductionCooldown(simulationDeltaTime);
            }
            else if (UpdateSlowSimulation(simulationDeltaTime))
            {
                return;
            }
        }

        if (IsCaptured)
            return;

        if (TryHandlePredatorFlee(config))
            return;

        CheckTransitions();
    }

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
