using UnityEngine;

public class PredatorBehaviour : BaseCreatureBehaviour
{
    public override void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        maxEnergy = GameConfig.Instance.maxEnergy;
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        AgeManager = new AgeManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        stateMachine = new StateMachine(this);
        EnergyManager = new EnergyManager(this, maxEnergy * GameConfig.Instance.initialEnergyPercentage, maxEnergy);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
        stateMachine.TransitionToWandering();
    }

    void FixedUpdate()
    {
        lastObservation += Time.fixedDeltaTime;
        if (lastObservation >= GameConfig.Instance.updateInterval)
        {
            //ObservationManager.UpdateObservations();
            var energyConsumption = EnergyManager.CalculateEnergyConsumption();
            EnergyManager.ConsumeEnergy(energyConsumption);
            AgeManager.UpdateAge(lastObservation);
            if (AgeManager.IsMaxAgeReached())
            {
                DestroyObject();
            }
            //if (ReproductionManager.IsOnCooldown())
            //{
            //    ReproductionManager.UpdateReproductionCooldown(lastObservation);
            //}
            lastObservation = 0f;
        }
        if (EnergyManager.IsEnergyDepleted())
        {
            DestroyObject();
            return;
        }
        stateMachine.Update();
        CheckTransitions();
    }

    protected override void CheckTransitions()
    {
        
    }

    protected override void DestroyObject()
    {
    }
}
