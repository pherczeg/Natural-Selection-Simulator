using System.Collections.Generic;
using UnityEngine;

public class PredatorBehaviour : BaseCreatureBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private readonly Dictionary<int, float> escapedPreyCooldowns = new Dictionary<int, float>();
    private float nextUtilityDecisionTime;

    public float Strength { get; private set; }

    protected override void OnSexChanged()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return;

        Color targetColor = Sex == CreatureSex.Female ? config.predatorFemaleColor : config.predatorMaleColor;
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
        PoolManager.Instance.ReturnObject(CreatureSpawner.Instance.predatorPrefab, this.gameObject);
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
        if (currentState == CreatureStateType.MovingToFood ||
            currentState == CreatureStateType.Eating ||
            currentState == CreatureStateType.Predation ||
            currentState == CreatureStateType.SearchingForFood ||
            currentState == CreatureStateType.Reproducting)
        {
            return;
        }

        if (EnergyManager.EnergyLevel < GameConfig.Instance.eatingEnergyThreshold * EnergyManager.CurrentMaxEnergy)
        {
            CreatureActionExecutor.Execute(this, CreatureAction.Hunt);
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
            bool executedDecision = TryExecuteECSUtilityDecision(config);
            float nextDecisionDelay = executedDecision
                ? GetJitteredUtilityDecisionDelay(decisionInterval, 0.35f)
                : GetJitteredUtilityDecisionDelay(Mathf.Max(0.02f, decisionInterval * 0.5f), 0.5f);
            nextUtilityDecisionTime = Time.time + nextDecisionDelay;
            return;
        }

        nextUtilityDecisionTime = Time.time + GetJitteredUtilityDecisionDelay(decisionInterval, 0.2f);
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
            utilityBrain.SetActions(CreatePredatorUtilityActions());
        }
    }

    private IEnumerable<UtilityAction> CreatePredatorUtilityActions()
    {
        return new[]
        {
            new UtilityAction(
                CreatureAction.None,
                "Keep Current State",
                new[]
                {
                    new UtilityConsideration("Current State Lockout", context => UtilityAIScoreRules.GetKeepCurrentStateScore(context, GetUtilityAIScoringParameters(), ECSCreatureKind.Predator))
                }),
            new UtilityAction(
                CreatureAction.Hunt,
                "Hunt / Search Food",
                new[]
                {
                    new UtilityConsideration("Hunger", context => UtilityAIScoreRules.GetHungerScore(context, GetUtilityAIScoringParameters()) * UtilityAIScoreRules.GetFoodSearchAvailabilityScore(context, GetUtilityAIScoringParameters(), ECSCreatureKind.Predator))
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
            config.GetReproductionEnergyThreshold(true));
    }

    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        var config = GameConfig.Instance;
        EnsureUtilityBehaviorProfileInitialized();
        ResetDespawnRequestState();
        ResetUtilityAIDebugState();
        nextUtilityDecisionTime = 0f;
        escapedPreyCooldowns.Clear();
        maxEnergy = config.predatorMaxEnergy > 0f ? config.predatorMaxEnergy : config.maxEnergy;
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        AgeManager = new AgeManager(this);
        ReproductionManager = new ReproductionManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        EnergyManager = new EnergyManager(this, maxEnergy * config.initialEnergyPercentagePredator, maxEnergy);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
        SetStrength(Mathf.Lerp(config.predatorStrengthMin, config.predatorStrengthMax, 0.5f));
        CreatureActionExecutor.Execute(this, CreatureAction.Wander);
        if (config.useUtilityAI)
        {
            EnsureUtilityBrain();
            float initialDecisionDelay = GetJitteredUtilityDecisionDelay(
                Mathf.Max(0.02f, config.utilityDecisionInterval * 0.5f),
                0.75f);
            nextUtilityDecisionTime = Time.time + initialDecisionDelay;
        }
    }

    public void SetStrength(float strength)
    {
        var config = GameConfig.Instance;
        if (config == null)
        {
            Strength = Mathf.Max(0f, strength);
            return;
        }

        float min = Mathf.Min(config.predatorStrengthMin, config.predatorStrengthMax);
        float max = Mathf.Max(config.predatorStrengthMin, config.predatorStrengthMax);
        Strength = Mathf.Clamp(strength, min, max);
    }

    public bool IsPreyBlacklisted(BaseCreatureBehaviour prey)
    {
        if (prey == null)
            return false;

        int preyId = prey.GetInstanceID();
        if (!escapedPreyCooldowns.TryGetValue(preyId, out float blacklistEndTime))
            return false;

        if (Time.time <= blacklistEndTime)
            return true;

        escapedPreyCooldowns.Remove(preyId);
        return false;
    }

    public void BlacklistPrey(BaseCreatureBehaviour prey)
    {
        if (prey == null)
            return;

        var config = GameConfig.Instance;
        if (config == null)
            return;

        escapedPreyCooldowns[prey.GetInstanceID()] = Time.time + Mathf.Max(0f, config.escapedPreyBlacklistDuration);
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
            else
            {
                var energyConsumption = EnergyManager.CalculateEnergyConsumption();
                EnergyManager.ConsumeEnergy(energyConsumption);
                AgeManager.UpdateAge(simulationDeltaTime);

                if (AgeManager.IsMaxAgeReached())
                {
                    Despawn(CreatureDeathReason.OldAge);
                    return;
                }

                if (EnergyManager.IsEnergyDepleted())
                {
                    Despawn(CreatureDeathReason.EnergyDepleted);
                    return;
                }

                ReproductionManager.UpdateReproductionCooldown(simulationDeltaTime);
            }
        }

        CheckTransitions();
    }
}
