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
    private Vector3 lastFleeDirection;
    private BaseCreatureBehaviour capturePredator;

    public float Agility { get; private set; }
    public bool IsCaptured => capturePredator != null;

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
        if (stateMachine.CurrentState.StateType == CreatureStateType.MovingToFood || stateMachine.CurrentState.StateType == CreatureStateType.Eating || stateMachine.CurrentState.StateType == CreatureStateType.SearchingForFood || stateMachine.CurrentState.StateType == CreatureStateType.Reproducting)
        {
            return;
        }
        else if (EnergyManager.EnergyLevel < GameConfig.Instance.eatingEnergyThreshold * EnergyManager.CurrentMaxEnergy)
        {
            stateMachine.TransitionToSearchingForFood();
        }
        else if (stateMachine.CurrentState.StateType == CreatureStateType.SearchingForMate || stateMachine.CurrentState.StateType == CreatureStateType.MovingToMate)
        {
            return;
        }
        else if (!ReproductionManager.IsOnCooldown() && ReproductionManager.IsReadyToReproduction())
        {
            stateMachine.TransitionToSearchingForMate();
        }
        else if (stateMachine.CurrentState.StateType == CreatureStateType.Idle)
        {
            stateMachine.TransitionToWandering();
        }
    }
    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        var config = GameConfig.Instance;
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
        stateMachine = new StateMachine(this);
        EnergyManager = new EnergyManager(this, maxEnergy * config.initialEnergyPercentageHerbivore, maxEnergy);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
        SetAgility(Mathf.Lerp(config.herbivoreAgilityMin, config.herbivoreAgilityMax, 0.5f));
    }
    // void FixedUpdate()
    // {
    //     if (GameConfig.Instance == null) return;
    //     lastObservation += Time.fixedDeltaTime;
    //     if (lastObservation >= GameConfig.Instance.updateInterval)
    //     {
    //         //ObservationManager.UpdateObservations();
    //         var energyConsumption = EnergyManager.CalculateEnergyConsumption();
    //         EnergyManager.ConsumeEnergy(energyConsumption);
    //         AgeManager.UpdateAge(lastObservation);
    //         if (AgeManager.IsMaxAgeReached())
    //         {
    //             DestroyObject();
    //         }
    //         if (ReproductionManager.IsOnCooldown())
    //         {
    //             ReproductionManager.UpdateReproductionCooldown(lastObservation);
    //         }
    //         lastObservation = 0f;
    //     }
    //     if (EnergyManager.IsEnergyDepleted())
    //     {
    //         DestroyObject();
    //         return;
    //     }
    //     stateMachine.Update();
    //     CheckTransitions();
    // }
    void FixedUpdate()
    {
        var config = GameConfig.Instance;
        if (config == null || stateMachine == null) return;

        MovementManager.UpdateTemporaryEffects(Time.fixedDeltaTime);

        lastObservation += Time.fixedDeltaTime;

        if (lastObservation >= config.updateInterval)
        {
            float simulationDeltaTime = lastObservation;
            lastObservation = 0f;
            if (UpdateSlowSimulation(simulationDeltaTime)) return;
        }

        if (IsCaptured)
            return;

        if (TryHandlePredatorFlee(config))
            return;

        stateMachine.Update();
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

        if (stateMachine?.CurrentState?.StateType == CreatureStateType.Eating)
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

        if (stateMachine.CurrentState.StateType == CreatureStateType.Eating)
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

        if (EnergyManager.IsEnergyDepleted())
        {
            DestroyObject();
            return true;
        }

        ReproductionManager.UpdateReproductionCooldown(deltaTime);

        return false;
    }
}
