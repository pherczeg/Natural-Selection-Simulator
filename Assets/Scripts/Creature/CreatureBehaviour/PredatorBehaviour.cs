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
        CreatureStateType currentState = stateMachine.CurrentState.StateType;
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
            stateMachine.TransitionToSearchingForFood();
        }
        else if (currentState == CreatureStateType.SearchingForMate || currentState == CreatureStateType.MovingToMate)
        {
            return;
        }
        else if (!ReproductionManager.IsOnCooldown() && ReproductionManager.IsReadyToReproduction())
        {
            stateMachine.TransitionToSearchingForMate();
        }
        else if (currentState == CreatureStateType.Idle)
        {
            stateMachine.TransitionToWandering();
        }
    }

    private void UpdateUtilityAI(GameConfig config)
    {
        if (Time.time < nextUtilityDecisionTime)
            return;

        nextUtilityDecisionTime = Time.time + Mathf.Max(0.01f, config.utilityDecisionInterval);
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
                    new UtilityConsideration("Current State Lockout", context => UtilityAIScoreRules.GetKeepCurrentStateScore(context, GetUtilityAIScoringParameters()))
                }),
            new UtilityAction(
                CreatureAction.Hunt,
                "Hunt / Search Food",
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
                0.85f),
            new UtilityAction(
                CreatureAction.Wander,
                "Wander",
                new[]
                {
                    new UtilityConsideration("Idle Wander", UtilityAIScoreRules.GetIdleWanderScore)
                },
                0.3f)
        };
    }

    private static UtilityAIScoringParameters GetUtilityAIScoringParameters()
    {
        var config = GameConfig.Instance;
        if (config == null)
            return UtilityAIScoringParameters.Default;

        return new UtilityAIScoringParameters(
            config.eatingEnergyThreshold,
            config.reproductionEnergyThreshold);
    }

    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        var config = GameConfig.Instance;
        ResetUtilityAIDebugState();
        nextUtilityDecisionTime = 0f;
        escapedPreyCooldowns.Clear();
        maxEnergy = config.predatorMaxEnergy > 0f ? config.predatorMaxEnergy : config.maxEnergy;
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        AgeManager = new AgeManager(this);
        ReproductionManager = new ReproductionManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        stateMachine = new StateMachine(this);
        EnergyManager = new EnergyManager(this, maxEnergy * config.initialEnergyPercentagePredator, maxEnergy);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
        SetStrength(Mathf.Lerp(config.predatorStrengthMin, config.predatorStrengthMax, 0.5f));
        stateMachine.TransitionToWandering();
        if (config.useUtilityAI)
        {
            EnsureUtilityBrain();
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
        var config = GameConfig.Instance;
        if (config == null || stateMachine == null) return;

        MovementManager.UpdateTemporaryEffects(Time.fixedDeltaTime);

        lastObservation += Time.fixedDeltaTime;
        if (lastObservation >= config.updateInterval)
        {
            float simulationDeltaTime = lastObservation;
            lastObservation = 0f;

            var energyConsumption = EnergyManager.CalculateEnergyConsumption();
            EnergyManager.ConsumeEnergy(energyConsumption);
            AgeManager.UpdateAge(simulationDeltaTime);

            if (EnergyManager.IsEnergyDepleted())
            {
                Despawn(CreatureDeathReason.EnergyDepleted);
                return;
            }

            ReproductionManager.UpdateReproductionCooldown(simulationDeltaTime);
        }

        stateMachine.Update();
        CheckTransitions();
    }
}
