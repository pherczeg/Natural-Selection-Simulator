using System.Collections.Generic;
using UnityEngine;

public class PredatorBehaviour : BaseCreatureBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private readonly Dictionary<int, float> escapedPreyCooldowns = new Dictionary<int, float>();

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

    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        var config = GameConfig.Instance;
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
