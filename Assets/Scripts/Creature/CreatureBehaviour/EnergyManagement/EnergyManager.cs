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
        if (creature.CurrentStateType == CreatureStateType.Eating) 
            return 0;
        float energyConsumption = (0.5f *
                                      (float)Math.Pow(creature.Weight, 1) *
                                      (float)Math.Pow(creature.MovementManager.MoveSpeed, 2)) *
                                      //Time.fixedDeltaTime) *
                                      GameConfig.Instance.updateInterval *
                                      creature.ObservationManager.SenseRadius * 
                                      GameConfig.Instance.energyConsumptionCoefficient;
        return energyConsumption;
    }
    public void ConsumeEnergy(float amount)
    {
        EnergyLevel -= amount;
        if (EnergyLevel < 0) EnergyLevel = 0;
        UpdateEnergyBar();
    }

    public void GainEnergy(float amount)
    {
        EnergyLevel += amount;
        if (EnergyLevel > maxEnergy) EnergyLevel = maxEnergy;
        UpdateEnergyBar();
    }

    public void UpdateEnergyBar()
    {
        if (creature.energyBarObject)
        {
            creature.energyBarObject.GetComponent<EnergyBar>().SetEnergy(EnergyLevel, maxEnergy);
        }
    }

    public bool IsEnergyDepleted()
    {
        return EnergyLevel <= 0;
    }
    // További energiakezelési logika...
}