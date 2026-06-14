using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HerbivoreBehaviour : BaseCreatureBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private BaseCreatureBehaviour forcedThreat;
    private float forcedFleeUntilTime;
    private float activeFleeUntilTime;
    private Vector3 lastFleeDirection;
    private BaseCreatureBehaviour capturePredator;
    [SerializeField] private HerbivoreSocialStrategy socialStrategy = HerbivoreSocialStrategy.Dove;

    public float Agility { get; private set; }
    public HerbivoreSocialStrategy SocialStrategy => socialStrategy;
    public bool IsHawk => socialStrategy == HerbivoreSocialStrategy.Hawk;
    public bool IsCaptured => capturePredator != null;
    public bool IsThreatened => forcedThreat != null || Time.time < activeFleeUntilTime;

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
        CreatureStateType currentState = CurrentStateType;
        if (currentState == CreatureStateType.MovingToFood ||
            currentState == CreatureStateType.Eating ||
            currentState == CreatureStateType.SearchingForFood ||
            currentState == CreatureStateType.Reproducting)
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
    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        var config = GameConfig.Instance;
        ResetDespawnRequestState();
        forcedThreat = null;
        forcedFleeUntilTime = 0f;
        activeFleeUntilTime = 0f;
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
        if (IsDespawnQueued)
            return;

        var config = GameConfig.Instance;
        if (config == null) return;

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

        if (IsThreatened)
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
        forcedFleeUntilTime = 0f;
        activeFleeUntilTime = 0f;
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
        forcedFleeUntilTime = Time.time + Mathf.Max(0f, GameConfig.Instance.herbivorePostEscapeFleeDuration);
        activeFleeUntilTime = forcedFleeUntilTime;
        lastFleeDirection = GetFlatDirectionAwayFrom(predator);
    }

    public bool UpdateEcsFlee(BaseCreatureBehaviour sensedThreat, GameConfig config)
    {
        if (config == null) return false;
        BaseCreatureBehaviour predator = GetEffectiveThreat(sensedThreat);
        Vector3 awayDirection;
        if (predator != null)
        {
            awayDirection = GetFlatDirectionAwayFrom(predator);
            activeFleeUntilTime = Time.time + Mathf.Max(0f, config.herbivoreFleeMemoryDuration);
        }
        else if (Time.time < activeFleeUntilTime && lastFleeDirection.sqrMagnitude > 0.0001f)
        {
            awayDirection = lastFleeDirection.normalized;
        }
        else
        {
            lastFleeDirection = Vector3.zero;
            return false;
        }
        if (CurrentStateType == CreatureStateType.Eating) EatingManager.InterruptEating();
        if (awayDirection.sqrMagnitude < 0.0001f)
            awayDirection = lastFleeDirection.sqrMagnitude > 0.0001f ? lastFleeDirection.normalized : transform.forward;
        Vector3 fleeDirection = MovementManager.SteerDirectionInsideBounds(awayDirection, config.herbivoreFleeObstacleProbeDistance);
        lastFleeDirection = fleeDirection;
        MovementManager.TryStartSprint();
        Vector3 fleeTarget = transform.position + fleeDirection * Mathf.Max(0.1f, config.herbivoreFleeDistance);
        MovementManager.MoveTowards(fleeTarget);
        return true;
    }

    private BaseCreatureBehaviour GetEffectiveThreat(BaseCreatureBehaviour sensedThreat)
    {
        if (forcedThreat != null && Time.time < forcedFleeUntilTime && forcedThreat.gameObject.activeInHierarchy)
            return forcedThreat;
        if (forcedThreat != null && (Time.time >= forcedFleeUntilTime || !forcedThreat.gameObject.activeInHierarchy))
            forcedThreat = null;
        if (sensedThreat != null && sensedThreat != this && sensedThreat.gameObject.activeInHierarchy)
            return sensedThreat;
        return null;
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
