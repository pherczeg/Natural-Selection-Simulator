using System;
using UnityEngine;

[Serializable]
public class EnergyManager
{
    private BaseCreatureBehaviour creature;
    public float EnergyLevel { get; private set; }
    private float maxEnergy;

    public EnergyManager(BaseCreatureBehaviour creature, float initialEnergy, float maxEnergy)
    {
        this.creature = creature;
        this.EnergyLevel = initialEnergy;
        this.maxEnergy = maxEnergy;
    }

    public float CalculateEnergyConsumption()
    {
        if (creature.CurrentStateType == CreatureStateType.Eating ||
            creature.CurrentStateType == CreatureStateType.Predation)
        {
            return 0;
        }

        float metabolicSpeed = creature.MovementManager != null
            ? Mathf.Max(0.01f, creature.MovementManager.BaseMoveSpeed)
            : 0.01f;
        float metabolicSense = creature.ObservationManager != null
            ? Mathf.Max(0.01f, creature.ObservationManager.BaseSenseRadius)
            : 0.01f;

        float energyConsumption = (0.5f *
                                      (float)Math.Pow(creature.Weight, 1) *
                                      (float)Math.Pow(metabolicSpeed, 2)) *
                                      //Time.fixedDeltaTime) *
                                      GameConfig.Instance.updateInterval *
                                      metabolicSense * 
                                      GameConfig.Instance.energyConsumptionCoefficient;
        return energyConsumption;
    }
    public void ConsumeEnergy(float amount)
    {
        EnergyLevel -= amount;
        if (EnergyLevel < 0) EnergyLevel = 0;
        if (EnergyLevel > CurrentMaxEnergy) EnergyLevel = CurrentMaxEnergy;
        UpdateEnergyBar();
    }

    public void GainEnergy(float amount)
    {
        EnergyLevel += amount;
        if (EnergyLevel > CurrentMaxEnergy) EnergyLevel = CurrentMaxEnergy;
        UpdateEnergyBar();
    }

    public float CurrentMaxEnergy
    {
        get
        {
            float maturity = creature.AgeManager?.MaturityFraction ?? 1f;
            float maxPercentage = creature is HerbivoreBehaviour ? 
                                  GameConfig.Instance.initialEnergyPercentageHerbivore : GameConfig.Instance.initialEnergyPercentagePredator;
            float startMax = maxEnergy * maxPercentage;
            return Mathf.Lerp(startMax, maxEnergy, maturity);
        }
    }

    public void UpdateEnergyBar()
    {
        if (creature.energyBarObject)
        {
            creature.energyBarObject.GetComponent<EnergyBar>().SetEnergy(EnergyLevel, CurrentMaxEnergy);
        }
    }

    public bool IsEnergyDepleted()
    {
        return EnergyLevel <= 0;
    }
}
