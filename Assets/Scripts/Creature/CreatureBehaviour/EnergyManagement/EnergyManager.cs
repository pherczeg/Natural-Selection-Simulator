using System;
using UnityEngine;

[Serializable]
public class EnergyManager
{
    private BaseCreatureBehaviour creature;
    public float EnergyLevel { get; private set; }
    private float maxEnergy;
    private bool hasEcsCurrentMaxEnergy;
    private float ecsCurrentMaxEnergy;
    private float pendingEcsExternalEnergyDelta;

    public EnergyManager(BaseCreatureBehaviour creature, float initialEnergy, float maxEnergy)
    {
        this.creature = creature;
        this.EnergyLevel = initialEnergy;
        this.maxEnergy = maxEnergy;
    }

    public float CalculateEnergyConsumption()
    {
        if (CreatureActionStatusAdapter.IsEnergyDrainBlocked(creature))
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
        float previousEnergyLevel = EnergyLevel;
        EnergyLevel -= amount;
        if (EnergyLevel < 0) EnergyLevel = 0;
        if (EnergyLevel > CurrentMaxEnergy) EnergyLevel = CurrentMaxEnergy;
        TrackECSExternalEnergyDelta(EnergyLevel - previousEnergyLevel);
        UpdateEnergyBar();
    }

    public void GainEnergy(float amount)
    {
        float previousEnergyLevel = EnergyLevel;
        EnergyLevel += amount;
        if (EnergyLevel > CurrentMaxEnergy) EnergyLevel = CurrentMaxEnergy;
        TrackECSExternalEnergyDelta(EnergyLevel - previousEnergyLevel);
        UpdateEnergyBar();
    }

    public float CurrentMaxEnergy
    {
        get
        {
            if (GameConfig.Instance != null &&
                GameConfig.Instance.useEcsCreatureLifecycle &&
                hasEcsCurrentMaxEnergy)
            {
                return ecsCurrentMaxEnergy;
            }

            float maturity = creature.AgeManager?.MaturityFraction ?? 1f;
            float maxPercentage = creature is HerbivoreBehaviour ? 
                                  GameConfig.Instance.initialEnergyPercentageHerbivore : GameConfig.Instance.initialEnergyPercentagePredator;
            float startMax = maxEnergy * maxPercentage;
            return Mathf.Lerp(startMax, maxEnergy, maturity);
        }
    }

    public float BaseMaxEnergy => maxEnergy;

    public void ApplyECSLifecycle(float energyLevel, float currentMaxEnergy)
    {
        hasEcsCurrentMaxEnergy = true;
        ecsCurrentMaxEnergy = Mathf.Max(0f, currentMaxEnergy);
        EnergyLevel = Mathf.Clamp(energyLevel, 0f, ecsCurrentMaxEnergy);
        UpdateEnergyBar();
    }

    public float ConsumePendingECSExternalEnergyDelta()
    {
        float delta = pendingEcsExternalEnergyDelta;
        pendingEcsExternalEnergyDelta = 0f;
        return delta;
    }

    private void TrackECSExternalEnergyDelta(float delta)
    {
        if (Mathf.Approximately(delta, 0f))
            return;

        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useEcsCreatureLifecycle)
            return;

        pendingEcsExternalEnergyDelta += delta;
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
