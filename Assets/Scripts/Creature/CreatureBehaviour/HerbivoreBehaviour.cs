using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HerbivoreBehaviour : BaseCreatureBehaviour
{
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
        else if (EnergyManager.EnergyLevel < GameConfig.Instance.eatingEnergyThreshold * maxEnergy)
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
        maxEnergy = GameConfig.Instance.maxEnergy;
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        AgeManager = new AgeManager(this);
        ReproductionManager = new ReproductionManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        stateMachine = new StateMachine(this);
        EnergyManager = new EnergyManager(this, maxEnergy * GameConfig.Instance.initialEnergyPercentage, maxEnergy);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
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
            if (ReproductionManager.IsOnCooldown())
            {
                ReproductionManager.UpdateReproductionCooldown(lastObservation);
            }
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
}
