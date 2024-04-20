using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Resources/GameConfig")]
public class GameConfig : ScriptableObject
{
    private static GameConfig instance;

    public static GameConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameConfig>("GameConfig");
                if (instance == null)
                {
                    Debug.LogError("GameConfig not found in Resources!");
                }
            }
            return instance;
        }
    }

    public float maxAge = 120f;
    public float maxEnergy = 200f;
    public float updateInterval = .2f;
    public float nutritionConsumptionRatePerSecond = 1.0f;
    public float blackListDuration = .5f;
    public float eatingEnergyThreshold = 0.7f;
    public float initialEnergyPercentage = 0.5f;
    public float reproductionCooldown = 30f;
    public float reproductionAge = 1f;
    public float reproductionTime = 1f;
    public float reproductionEnergyThreshold = 0.6f;
    public float mutationRate = 0.5f;
    public float mutationChance = 0.5f;
    public float energyConsumptionCoefficient = 0.0002f;
    public float sizeDifferentFactor = 1.5f;
    public float minNutrionValue = 50f;
    public float maxNutrionValue = 100f;
}