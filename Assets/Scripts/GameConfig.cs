using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    public float blackListDuration = 3f;
    public float updateInterval = .2f;
    public float reproductionCooldown = 7f;
    public float matingAge = 20f;
    public float maxAge = 60f;
    public float eatingDuration = 1f;
    public float mutationRate = 0.5f;
    public float mutationChance = 0.5f;
    public float energyThreshold = 0.8f;
    public float initialEnergyPercentage = 0.5f;
    public float matingEnergyThreshold = 0.95f;
}