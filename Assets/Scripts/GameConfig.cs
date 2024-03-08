using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    public float maxAge = 60f;
    public float updateInterval = .2f;
    public float nutritionConsumptionRatePerSecond = 100.0f;
    public float eatingDuration = 1f;
    public float blackListDuration = .5f;
    public float eatingEnergyThreshold = 0.75f;
    public float initialEnergyPercentage = 0.5f;
    public float reproductionCooldown = 7f;
    public float reproductionAge = 2f;
    public float reproductionTime = 1f;
    public float reproductionEnergyThreshold = 0.5f;
    public float mutationRate = 0.5f;
    public float mutationChance = 0.5f;
    public float energyConsumptionCoefficient = 0.0005f;
}