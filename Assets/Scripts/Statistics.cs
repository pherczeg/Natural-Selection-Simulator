using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class Statistics : MonoBehaviour
{
    public float updateInterval = 10f;
    private float timer;

    private List<int> numberOfCreaturesHistory = new List<int>();
    private List<float> averageWeightHistory = new List<float>();
    private List<float> averageSpeedHistory = new List<float>();
    private List<float> averageEnergyHistory = new List<float>();
    private List<float> averageAgeHistory = new List<float>();
    private List<float> averageSenseRadiusHistory = new List<float>();

    void Update()
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
            float averageWeight = creatures.Average(creature => creature.weight);
            float averageSpeed = creatures.Average(creature => creature.moveSpeed);
            float averageEnergy = creatures.Average(creature => creature.energyLevel);
            float averageAge = creatures.Average(creature => creature.age);
            float averageSenseRadius = creatures.Average(creature => creature.senseRadius);

            numberOfCreaturesHistory.Add(creatureCount);
            averageWeightHistory.Add(averageWeight);
            averageSpeedHistory.Add(averageSpeed);
            averageEnergyHistory.Add(averageEnergy);
            averageAgeHistory.Add(averageAge);
            averageSenseRadiusHistory.Add(averageSenseRadius);

            // Itt kezeld a statisztikákat, például kiírhatod a konzolra
            Debug.Log($"Egyedek száma: {creatureCount}, Átlag súly: {averageWeight}, Átlag sebesség: {averageSpeed}, Átlag energia: {averageEnergy}, Átlag érzékelés: {averageSenseRadius}, Átlag életkor: {averageAge}");
        }
    }

    // További metódusok az adatok megjelenítésére
}
