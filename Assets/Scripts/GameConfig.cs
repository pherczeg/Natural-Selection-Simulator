using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    public float maxAge = 60f;
    public float updateInterval = .2f;
    public float nutritionConsumptionRatePerSecond = 5.0f;
    public float eatingDuration = 1f;
    public float blackListDuration = 1f;
    public float eatingEnergyThreshold = 0.95f;
    public float initialEnergyPercentage = 0.5f;
    public float reproductionCooldown = 7f;
    public float reproductionAge = 20f;
    public float reproductionEnergyThreshold = 0.5f;
    public float mutationRate = 0.5f;
    public float mutationChance = 0.5f;
}