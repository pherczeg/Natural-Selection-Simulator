using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class Statistics : MonoBehaviour
{
    public static Statistics Instance { get; private set; }

    public float updateInterval = 1f;
    private float timer = 0f;
    private int counter = 0;
    public List<int> numberOfCreaturesHistory;
    public List<float> averageWeightHistory;
    public List<float> averageSpeedHistory;
    public List<float> averageEnergyHistory;
    public List<float> averageSenseHistory;
    public List<float> averageAgeHistory;
    public List<float> averageSenseRadiusHistory;
    public List<float> maxWeightHistory;
    public List<float> maxSpeedHistory;
    public List<float> maxSenseHistory;
    public List<float> minWeightHistory;
    public List<float> minSpeedHistory;
    public List<float> minSenseHistory;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        Reset();
    }
    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;
        if (timer >= updateInterval)
        {
            UpdateStatistics();
            timer = 0;
        }
    }

    void UpdateStatistics()
    {
        CreatureBehaviour[] creatures = FindObjectsOfType<CreatureBehaviour>();
        int creatureCount = creatures.Length;

        if (creatureCount > 0)
        {
            float averageWeight = creatures.Average(creature => creature.Weight);
            float averageSpeed = creatures.Average(creature => creature.MovementManager.MoveSpeed);
            float averageEnergy = creatures.Average(creature => creature.EnergyManager.EnergyLevel);
            float averageAge = creatures.Average(creature => creature.AgeManager.Age);
            float averageSenseRadius = creatures.Average(creature => creature.ObservationManager.SenseRadius);
            float maxWeigh = creatures.Max(creature => creature.Weight);
            float maxSpeed = creatures.Max(creature => creature.MovementManager.MoveSpeed);
            float maxSense = creatures.Max(creature => creature.ObservationManager.SenseRadius);
            float minWeight = creatures.Min(creature => creature.Weight);
            float minSpeed = creatures.Min(creature => creature.MovementManager.MoveSpeed);
            float minSense = creatures.Min(creature => creature.ObservationManager.SenseRadius);
            numberOfCreaturesHistory.Add(creatureCount);
            averageWeightHistory.Add(averageWeight);
            averageSpeedHistory.Add(averageSpeed);
            averageEnergyHistory.Add(averageEnergy);
            averageAgeHistory.Add(averageAge);
            averageSenseRadiusHistory.Add(averageSenseRadius);
            maxWeightHistory.Add(maxWeigh);
            maxSpeedHistory.Add(maxSpeed);
            maxSenseHistory.Add(maxSense);
            minWeightHistory.Add(minWeight);
            minSpeedHistory.Add(minSpeed);
            minSenseHistory.Add(minSense);

            Debug.Log($"Egyedek száma: {creatureCount}, Átlag súly: {averageWeight}, Átlag sebesség: {averageSpeed}, Átlag energia: {averageEnergy}, Átlag érzékelés: {averageSenseRadius}, Átlag életkor: {averageAge}");
        }
    }

    public void Reset()
    {
        numberOfCreaturesHistory = new List<int>();
        averageWeightHistory = new List<float>();
        averageSpeedHistory = new List<float>();
        averageEnergyHistory = new List<float>();
        averageAgeHistory = new List<float>();
        averageSenseRadiusHistory = new List<float>();
        maxWeightHistory = new List<float>();
        maxSpeedHistory = new List<float>();
        maxSenseHistory = new List<float>();
        minWeightHistory = new List<float>();
        minSpeedHistory = new List<float>();
        minSenseHistory = new List<float>();
        timer = 0f;
    }
}
