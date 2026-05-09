using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SpeciesStatisticsSnapshot
{
    public string species;
    public int count;
    public int femaleCount;
    public int maleCount;

    public float averageWeight;
    public float minWeight;
    public float maxWeight;

    public float averageSpeed;
    public float minSpeed;
    public float maxSpeed;

    public float averageBaseSpeed;
    public float minBaseSpeed;
    public float maxBaseSpeed;

    public float averageEnergy;
    public float minEnergy;
    public float maxEnergy;

    public float averageCurrentMaxEnergy;
    public float minCurrentMaxEnergy;
    public float maxCurrentMaxEnergy;

    public float averageAge;
    public float minAge;
    public float maxAge;

    public float averageSenseRadius;
    public float minSenseRadius;
    public float maxSenseRadius;

    public float averageBaseSenseRadius;
    public float minBaseSenseRadius;
    public float maxBaseSenseRadius;

    public float averageDesirability;
    public float minDesirability;
    public float maxDesirability;

    public float averageBaseDesirability;
    public float minBaseDesirability;
    public float maxBaseDesirability;

    public float averageSprintDuration;
    public float minSprintDuration;
    public float maxSprintDuration;

    public float averageSprintFactor;
    public float minSprintFactor;
    public float maxSprintFactor;

    public float averageSprintCooldown;
    public float minSprintCooldown;
    public float maxSprintCooldown;

    public float averageSprintCooldownSpeedFactor;
    public float minSprintCooldownSpeedFactor;
    public float maxSprintCooldownSpeedFactor;

    public float averageAgility;
    public float minAgility;
    public float maxAgility;

    public float averageStrength;
    public float minStrength;
    public float maxStrength;
}

public class Statistics : MonoBehaviour
{
    public static Statistics Instance { get; private set; }

    public float updateInterval = 60f;
    private float timer = 0f;

    // Legacy herbivore-only histories kept for compatibility with existing exporters/UI.
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
    public List<int> femaleCountHistory;
    public List<int> maleCountHistory;

    // New complete per-species analysis.
    public List<SpeciesStatisticsSnapshot> herbivoreHistory;
    public List<SpeciesStatisticsSnapshot> predatorHistory;

    public SpeciesStatisticsSnapshot LastHerbivoreSnapshot { get; private set; }
    public SpeciesStatisticsSnapshot LastPredatorSnapshot { get; private set; }

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
        if (CreatureSpawner.Instance == null)
            return;

        LastHerbivoreSnapshot = BuildSnapshot(
            CreatureSpawner.Instance.herbivorCreatures,
            "Herbivore");
        LastPredatorSnapshot = BuildSnapshot(
            CreatureSpawner.Instance.predatorCreatures,
            "Predator");

        herbivoreHistory.Add(LastHerbivoreSnapshot);
        predatorHistory.Add(LastPredatorSnapshot);

        // Keep old histories aligned to herbivore data for existing CSV/export logic.
        numberOfCreaturesHistory.Add(LastHerbivoreSnapshot.count);
        averageWeightHistory.Add(LastHerbivoreSnapshot.averageWeight);
        averageSpeedHistory.Add(LastHerbivoreSnapshot.averageSpeed);
        averageEnergyHistory.Add(LastHerbivoreSnapshot.averageEnergy);
        averageAgeHistory.Add(LastHerbivoreSnapshot.averageAge);
        averageSenseRadiusHistory.Add(LastHerbivoreSnapshot.averageSenseRadius);
        maxWeightHistory.Add(LastHerbivoreSnapshot.maxWeight);
        maxSpeedHistory.Add(LastHerbivoreSnapshot.maxSpeed);
        maxSenseHistory.Add(LastHerbivoreSnapshot.maxSenseRadius);
        minWeightHistory.Add(LastHerbivoreSnapshot.minWeight);
        minSpeedHistory.Add(LastHerbivoreSnapshot.minSpeed);
        minSenseHistory.Add(LastHerbivoreSnapshot.minSenseRadius);
        femaleCountHistory.Add(LastHerbivoreSnapshot.femaleCount);
        maleCountHistory.Add(LastHerbivoreSnapshot.maleCount);

        Debug.Log(
            $"Herbivorok: db={LastHerbivoreSnapshot.count}, nosteny={LastHerbivoreSnapshot.femaleCount}, him={LastHerbivoreSnapshot.maleCount}, " +
            $"atlag speed={LastHerbivoreSnapshot.averageSpeed:F2}, atlag energia={LastHerbivoreSnapshot.averageEnergy:F2}, atlag age={LastHerbivoreSnapshot.averageAge:F2}, " +
            $"atlag agility={LastHerbivoreSnapshot.averageAgility:F2}, atlag desirability={LastHerbivoreSnapshot.averageDesirability:F2} | " +
            $"Predatorok: db={LastPredatorSnapshot.count}, nosteny={LastPredatorSnapshot.femaleCount}, him={LastPredatorSnapshot.maleCount}, " +
            $"atlag speed={LastPredatorSnapshot.averageSpeed:F2}, atlag energia={LastPredatorSnapshot.averageEnergy:F2}, atlag age={LastPredatorSnapshot.averageAge:F2}, " +
            $"atlag strength={LastPredatorSnapshot.averageStrength:F2}, atlag desirability={LastPredatorSnapshot.averageDesirability:F2}");
    }

    private SpeciesStatisticsSnapshot BuildSnapshot(List<BaseCreatureBehaviour> source, string species)
    {
        BaseCreatureBehaviour[] creatures = source
            .Where(c => c != null
                        && c.gameObject.activeInHierarchy
                        && c.MovementManager != null
                        && c.ObservationManager != null
                        && c.EnergyManager != null
                        && c.AgeManager != null
                        && c.ReproductionManager != null)
            .ToArray();

        var s = new SpeciesStatisticsSnapshot { species = species, count = creatures.Length };
        if (creatures.Length == 0)
            return s;

        s.femaleCount = creatures.Count(c => c.Sex == CreatureSex.Female);
        s.maleCount = creatures.Count(c => c.Sex == CreatureSex.Male);

        FillTriplet(creatures.Select(c => c.Weight), out s.averageWeight, out s.minWeight, out s.maxWeight);
        FillTriplet(creatures.Select(c => c.MovementManager.MoveSpeed), out s.averageSpeed, out s.minSpeed, out s.maxSpeed);
        FillTriplet(creatures.Select(c => c.MovementManager.BaseMoveSpeed), out s.averageBaseSpeed, out s.minBaseSpeed, out s.maxBaseSpeed);
        FillTriplet(creatures.Select(c => c.EnergyManager.EnergyLevel), out s.averageEnergy, out s.minEnergy, out s.maxEnergy);
        FillTriplet(creatures.Select(c => c.EnergyManager.CurrentMaxEnergy), out s.averageCurrentMaxEnergy, out s.minCurrentMaxEnergy, out s.maxCurrentMaxEnergy);
        FillTriplet(creatures.Select(c => c.AgeManager.Age), out s.averageAge, out s.minAge, out s.maxAge);
        FillTriplet(creatures.Select(c => c.ObservationManager.SenseRadius), out s.averageSenseRadius, out s.minSenseRadius, out s.maxSenseRadius);
        FillTriplet(creatures.Select(c => c.ObservationManager.BaseSenseRadius), out s.averageBaseSenseRadius, out s.minBaseSenseRadius, out s.maxBaseSenseRadius);
        FillTriplet(creatures.Select(c => c.ReproductionManager.Desirability), out s.averageDesirability, out s.minDesirability, out s.maxDesirability);
        FillTriplet(creatures.Select(c => c.ReproductionManager.BaseDesirability), out s.averageBaseDesirability, out s.minBaseDesirability, out s.maxBaseDesirability);
        FillTriplet(creatures.Select(c => c.MovementManager.BaseSprintDuration), out s.averageSprintDuration, out s.minSprintDuration, out s.maxSprintDuration);
        FillTriplet(creatures.Select(c => c.MovementManager.BaseSprintFactor), out s.averageSprintFactor, out s.minSprintFactor, out s.maxSprintFactor);
        FillTriplet(creatures.Select(c => c.MovementManager.BaseSprintCooldown), out s.averageSprintCooldown, out s.minSprintCooldown, out s.maxSprintCooldown);
        FillTriplet(creatures.Select(c => c.MovementManager.BaseSprintCooldownSpeedFactor), out s.averageSprintCooldownSpeedFactor, out s.minSprintCooldownSpeedFactor, out s.maxSprintCooldownSpeedFactor);

        FillTriplet(
            creatures.Select(c => c is HerbivoreBehaviour h ? h.Agility : 0f),
            out s.averageAgility,
            out s.minAgility,
            out s.maxAgility);

        FillTriplet(
            creatures.Select(c => c is PredatorBehaviour p ? p.Strength : 0f),
            out s.averageStrength,
            out s.minStrength,
            out s.maxStrength);

        return s;
    }

    private static void FillTriplet(IEnumerable<float> source, out float avg, out float min, out float max)
    {
        float[] values = source.ToArray();
        if (values.Length == 0)
        {
            avg = 0f;
            min = 0f;
            max = 0f;
            return;
        }

        avg = values.Average();
        min = values.Min();
        max = values.Max();
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
        femaleCountHistory = new List<int>();
        maleCountHistory = new List<int>();
        herbivoreHistory = new List<SpeciesStatisticsSnapshot>();
        predatorHistory = new List<SpeciesStatisticsSnapshot>();
        LastHerbivoreSnapshot = null;
        LastPredatorSnapshot = null;
        timer = 0f;
    }
}
