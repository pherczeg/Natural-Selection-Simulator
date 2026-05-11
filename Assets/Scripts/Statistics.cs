using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public class SpeciesStatisticsSnapshot
{
    public string species;
    public int count;
    public int femaleCount;
    public int maleCount;
    public int doveCount;
    public int hawkCount;
    public float doveRatio;
    public float hawkRatio;

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

    public float averageUtilityKeepCurrentStateWeight;
    public float averageUtilityFoodActionWeight;
    public float averageUtilitySearchMateWeight;
    public float averageUtilityWanderWeight;
}

public enum CreatureDeathReason
{
    Unknown,
    EnergyDepleted,
    Predation,
    OldAge
}

[Serializable]
public class CreatureStateCount
{
    public CreatureStateType state;
    public int herbivoreCount;
    public int predatorCount;

    public int TotalCount => herbivoreCount + predatorCount;
}

[Serializable]
public class SimulationDiagnosticsSnapshot
{
    public float elapsedTime;

    public int aliveHerbivores;
    public int alivePredators;
    public int aliveDoves;
    public int aliveHawks;
    public int foodCount;

    public float averageEnergy;
    public float averageHerbivoreEnergy;
    public float averagePredatorEnergy;
    public float herbivoreUtilityKeepCurrentStateWeight;
    public float herbivoreUtilityFoodActionWeight;
    public float herbivoreUtilitySearchMateWeight;
    public float herbivoreUtilityWanderWeight;
    public float predatorUtilityKeepCurrentStateWeight;
    public float predatorUtilityFoodActionWeight;
    public float predatorUtilitySearchMateWeight;
    public float predatorUtilityWanderWeight;
    public int reproductionReadyHerbivores;
    public int reproductionReadyPredators;

    public int totalHerbivoresSpawned;
    public int totalPredatorsSpawned;
    public int totalFoodSpawned;
    public int totalReproductionEvents;
    public int totalOffspringBorn;
    public int totalPredationAttempts;
    public int totalPredationSuccesses;
    public int totalPredationEscapes;
    public int totalHerbivoreDeaths;
    public int totalPredatorDeaths;
    public int deathsByEnergy;
    public int deathsByPredation;
    public int deathsByOldAge;
    public int deathsUnknown;

    public float herbivoreSurvivalRate;
    public float predatorSurvivalRate;
    public float doveRatio;
    public float hawkRatio;

    public List<CreatureStateCount> stateDistribution = new List<CreatureStateCount>();

    public string GetStateDistributionText()
    {
        if (stateDistribution == null || stateDistribution.Count == 0)
            return "none";

        var builder = new StringBuilder();
        for (int i = 0; i < stateDistribution.Count; i++)
        {
            CreatureStateCount entry = stateDistribution[i];
            if (i > 0)
                builder.Append("; ");

            builder.Append(entry.state);
            builder.Append(" H=");
            builder.Append(entry.herbivoreCount);
            builder.Append(" P=");
            builder.Append(entry.predatorCount);
        }

        return builder.ToString();
    }
}

public class Statistics : MonoBehaviour
{
    public static Statistics Instance { get; private set; }

    public float updateInterval = 60f;
    public bool logDiagnostics = false;
    private float timer = 0f;
    private float elapsedTime = 0f;

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
    public List<SimulationDiagnosticsSnapshot> diagnosticsHistory;

    public SpeciesStatisticsSnapshot LastHerbivoreSnapshot { get; private set; }
    public SpeciesStatisticsSnapshot LastPredatorSnapshot { get; private set; }
    public SimulationDiagnosticsSnapshot LastDiagnosticsSnapshot { get; private set; }
    public float ElapsedTime => elapsedTime;

    public int totalHerbivoresSpawned;
    public int totalPredatorsSpawned;
    public int totalFoodSpawned;
    public int totalReproductionEvents;
    public int totalOffspringBorn;
    public int totalPredationAttempts;
    public int totalPredationSuccesses;
    public int totalPredationEscapes;
    public int totalHerbivoreDeaths;
    public int totalPredatorDeaths;
    public int deathsByEnergy;
    public int deathsByPredation;
    public int deathsByOldAge;
    public int deathsUnknown;

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
        elapsedTime += Time.fixedDeltaTime;
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
        LastDiagnosticsSnapshot = BuildDiagnosticsSnapshot();

        herbivoreHistory.Add(LastHerbivoreSnapshot);
        predatorHistory.Add(LastPredatorSnapshot);
        diagnosticsHistory.Add(LastDiagnosticsSnapshot);

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

        if (logDiagnostics)
        {
            Debug.Log(FormatDiagnosticsSnapshot(LastDiagnosticsSnapshot));
        }
    }

    public void CaptureSnapshot()
    {
        if (LastDiagnosticsSnapshot != null && Mathf.Abs(LastDiagnosticsSnapshot.elapsedTime - elapsedTime) <= 0.0001f)
            return;

        UpdateStatistics();
    }

    public void RecordCreatureSpawned(BaseCreatureBehaviour creature)
    {
        if (creature is PredatorBehaviour)
        {
            totalPredatorsSpawned++;
        }
        else if (creature is HerbivoreBehaviour)
        {
            totalHerbivoresSpawned++;
        }
    }

    public void RecordFoodSpawned()
    {
        totalFoodSpawned++;
    }

    public void RecordReproduction(int offspringCount)
    {
        totalReproductionEvents++;
        totalOffspringBorn += Mathf.Max(0, offspringCount);
    }

    public void RecordPredationAttempt()
    {
        totalPredationAttempts++;
    }

    public void RecordPredationResolved(bool predatorSucceeded)
    {
        if (predatorSucceeded)
        {
            totalPredationSuccesses++;
        }
        else
        {
            totalPredationEscapes++;
        }
    }

    public void RecordCreatureDeath(BaseCreatureBehaviour creature, CreatureDeathReason reason)
    {
        if (creature is PredatorBehaviour)
        {
            totalPredatorDeaths++;
        }
        else if (creature is HerbivoreBehaviour)
        {
            totalHerbivoreDeaths++;
        }

        switch (reason)
        {
            case CreatureDeathReason.EnergyDepleted:
                deathsByEnergy++;
                break;
            case CreatureDeathReason.Predation:
                deathsByPredation++;
                break;
            case CreatureDeathReason.OldAge:
                deathsByOldAge++;
                break;
            default:
                deathsUnknown++;
                break;
        }
    }

    public SimulationDiagnosticsSnapshot BuildDiagnosticsSnapshot()
    {
        return BuildDiagnosticsSnapshot(this);
    }

    public static SimulationDiagnosticsSnapshot BuildDiagnosticsSnapshot(Statistics statistics)
    {
        BaseCreatureBehaviour[] herbivores = GetActiveCreatures(CreatureSpawner.Instance?.herbivorCreatures);
        BaseCreatureBehaviour[] predators = GetActiveCreatures(CreatureSpawner.Instance?.predatorCreatures);
        BaseCreatureBehaviour[] allCreatures = herbivores.Concat(predators).ToArray();
        int totalHerbivoresSpawned = statistics?.totalHerbivoresSpawned ?? 0;
        int totalPredatorsSpawned = statistics?.totalPredatorsSpawned ?? 0;

        return new SimulationDiagnosticsSnapshot
        {
            elapsedTime = statistics != null ? statistics.elapsedTime : Time.time,
            aliveHerbivores = herbivores.Length,
            alivePredators = predators.Length,
            aliveDoves = CountHerbivoreStrategy(herbivores, HerbivoreSocialStrategy.Dove),
            aliveHawks = CountHerbivoreStrategy(herbivores, HerbivoreSocialStrategy.Hawk),
            foodCount = CountActiveFood(),
            averageEnergy = CalculateAverageEnergy(allCreatures),
            averageHerbivoreEnergy = CalculateAverageEnergy(herbivores),
            averagePredatorEnergy = CalculateAverageEnergy(predators),
            herbivoreUtilityKeepCurrentStateWeight = CalculateAverageUtilityActionWeight(herbivores, p => p.keepCurrentStateWeight),
            herbivoreUtilityFoodActionWeight = CalculateAverageUtilityActionWeight(herbivores, p => p.foodActionWeight),
            herbivoreUtilitySearchMateWeight = CalculateAverageUtilityActionWeight(herbivores, p => p.searchMateWeight),
            herbivoreUtilityWanderWeight = CalculateAverageUtilityActionWeight(herbivores, p => p.wanderWeight),
            predatorUtilityKeepCurrentStateWeight = CalculateAverageUtilityActionWeight(predators, p => p.keepCurrentStateWeight),
            predatorUtilityFoodActionWeight = CalculateAverageUtilityActionWeight(predators, p => p.foodActionWeight),
            predatorUtilitySearchMateWeight = CalculateAverageUtilityActionWeight(predators, p => p.searchMateWeight),
            predatorUtilityWanderWeight = CalculateAverageUtilityActionWeight(predators, p => p.wanderWeight),
            reproductionReadyHerbivores = CountReproductionReady(herbivores),
            reproductionReadyPredators = CountReproductionReady(predators),
            totalHerbivoresSpawned = totalHerbivoresSpawned,
            totalPredatorsSpawned = totalPredatorsSpawned,
            totalFoodSpawned = statistics?.totalFoodSpawned ?? 0,
            totalReproductionEvents = statistics?.totalReproductionEvents ?? 0,
            totalOffspringBorn = statistics?.totalOffspringBorn ?? 0,
            totalPredationAttempts = statistics?.totalPredationAttempts ?? 0,
            totalPredationSuccesses = statistics?.totalPredationSuccesses ?? 0,
            totalPredationEscapes = statistics?.totalPredationEscapes ?? 0,
            totalHerbivoreDeaths = statistics?.totalHerbivoreDeaths ?? 0,
            totalPredatorDeaths = statistics?.totalPredatorDeaths ?? 0,
            deathsByEnergy = statistics?.deathsByEnergy ?? 0,
            deathsByPredation = statistics?.deathsByPredation ?? 0,
            deathsByOldAge = statistics?.deathsByOldAge ?? 0,
            deathsUnknown = statistics?.deathsUnknown ?? 0,
            herbivoreSurvivalRate = CalculateSurvivalRate(herbivores.Length, totalHerbivoresSpawned),
            predatorSurvivalRate = CalculateSurvivalRate(predators.Length, totalPredatorsSpawned),
            doveRatio = CalculateRatio(CountHerbivoreStrategy(herbivores, HerbivoreSocialStrategy.Dove), herbivores.Length),
            hawkRatio = CalculateRatio(CountHerbivoreStrategy(herbivores, HerbivoreSocialStrategy.Hawk), herbivores.Length),
            stateDistribution = BuildStateDistribution(herbivores, predators)
        };
    }

    public static string FormatDiagnosticsSnapshot(SimulationDiagnosticsSnapshot snapshot)
    {
        if (snapshot == null)
            return "SimulationDiagnostics: snapshot unavailable";

        return
            $"SimulationDiagnostics t={snapshot.elapsedTime:F1}s | " +
            $"alive H={snapshot.aliveHerbivores} P={snapshot.alivePredators}, food={snapshot.foodCount}, " +
            $"social doves={snapshot.aliveDoves} hawks={snapshot.aliveHawks} hawkRatio={snapshot.hawkRatio:P0}, " +
            $"avgEnergy all={snapshot.averageEnergy:F1} H={snapshot.averageHerbivoreEnergy:F1} P={snapshot.averagePredatorEnergy:F1} | " +
            $"utility H(k/f/m/w)={snapshot.herbivoreUtilityKeepCurrentStateWeight:F2}/{snapshot.herbivoreUtilityFoodActionWeight:F2}/{snapshot.herbivoreUtilitySearchMateWeight:F2}/{snapshot.herbivoreUtilityWanderWeight:F2} " +
            $"P(k/f/m/w)={snapshot.predatorUtilityKeepCurrentStateWeight:F2}/{snapshot.predatorUtilityFoodActionWeight:F2}/{snapshot.predatorUtilitySearchMateWeight:F2}/{snapshot.predatorUtilityWanderWeight:F2} | " +
            $"reproReady H={snapshot.reproductionReadyHerbivores} P={snapshot.reproductionReadyPredators} | " +
            $"spawned H={snapshot.totalHerbivoresSpawned} P={snapshot.totalPredatorsSpawned} food={snapshot.totalFoodSpawned}, " +
            $"repro events={snapshot.totalReproductionEvents} offspring={snapshot.totalOffspringBorn}, " +
            $"predation attempts={snapshot.totalPredationAttempts} success={snapshot.totalPredationSuccesses} escapes={snapshot.totalPredationEscapes}, " +
            $"deaths energy={snapshot.deathsByEnergy} predation={snapshot.deathsByPredation} oldAge={snapshot.deathsByOldAge} unknown={snapshot.deathsUnknown}, " +
            $"survival H={snapshot.herbivoreSurvivalRate:P0} P={snapshot.predatorSurvivalRate:P0} | " +
            $"states: {snapshot.GetStateDistributionText()}";
    }

    private SpeciesStatisticsSnapshot BuildSnapshot(List<BaseCreatureBehaviour> source, string species)
    {
        BaseCreatureBehaviour[] creatures = GetActiveCreatures(source)
            .Where(c => c.MovementManager != null
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
        s.doveCount = creatures.Count(c => c is HerbivoreBehaviour h && h.SocialStrategy == HerbivoreSocialStrategy.Dove);
        s.hawkCount = creatures.Count(c => c is HerbivoreBehaviour h && h.SocialStrategy == HerbivoreSocialStrategy.Hawk);
        s.doveRatio = CalculateRatio(s.doveCount, creatures.Length);
        s.hawkRatio = CalculateRatio(s.hawkCount, creatures.Length);

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

        s.averageUtilityKeepCurrentStateWeight = creatures.Average(c => c.UtilityBehaviorProfile.keepCurrentStateWeight);
        s.averageUtilityFoodActionWeight = creatures.Average(c => c.UtilityBehaviorProfile.foodActionWeight);
        s.averageUtilitySearchMateWeight = creatures.Average(c => c.UtilityBehaviorProfile.searchMateWeight);
        s.averageUtilityWanderWeight = creatures.Average(c => c.UtilityBehaviorProfile.wanderWeight);

        return s;
    }

    private static BaseCreatureBehaviour[] GetActiveCreatures(List<BaseCreatureBehaviour> source)
    {
        if (source == null)
            return Array.Empty<BaseCreatureBehaviour>();

        return source
            .Where(c => c != null && c.gameObject.activeInHierarchy)
            .ToArray();
    }

    private static int CountActiveFood()
    {
        if (FoodSpawner.Instance == null || FoodSpawner.Instance.foods == null)
            return 0;

        return FoodSpawner.Instance.foods.Count(f => f != null && f.gameObject.activeInHierarchy);
    }

    private static float CalculateAverageEnergy(BaseCreatureBehaviour[] creatures)
    {
        float[] energies = creatures
            .Where(c => c.EnergyManager != null)
            .Select(c => c.EnergyManager.EnergyLevel)
            .ToArray();

        return energies.Length == 0 ? 0f : energies.Average();
    }

    private static int CountReproductionReady(BaseCreatureBehaviour[] creatures)
    {
        return creatures.Count(IsReproductionReadyForDiagnostics);
    }

    private static int CountHerbivoreStrategy(
        BaseCreatureBehaviour[] creatures,
        HerbivoreSocialStrategy strategy)
    {
        return creatures.Count(c => c is HerbivoreBehaviour herbivore && herbivore.SocialStrategy == strategy);
    }

    private static bool IsReproductionReadyForDiagnostics(BaseCreatureBehaviour creature)
    {
        if (creature == null ||
            !creature.gameObject.activeInHierarchy ||
            creature.ReproductionManager == null ||
            creature.AgeManager == null ||
            creature.EnergyManager == null)
        {
            return false;
        }

        GameConfig config = Resources.Load<GameConfig>("GameConfig");
        if (config == null)
            return false;

        CreatureStateType currentState = GetCurrentStateType(creature);
           bool isPredator = creature is PredatorBehaviour;
        return !creature.ReproductionManager.IsOnCooldown() &&
               creature.AgeManager.Age >= config.maturityAge &&
               currentState != CreatureStateType.Reproducting &&
               currentState != CreatureStateType.Eating &&
               currentState != CreatureStateType.Predation &&
               currentState != CreatureStateType.MovingToFood &&
               currentState != CreatureStateType.SearchingForFood &&
               creature.EnergyManager.EnergyLevel >= config.GetReproductionEnergyThreshold(isPredator) * creature.EnergyManager.CurrentMaxEnergy;
    }

    private static List<CreatureStateCount> BuildStateDistribution(
        BaseCreatureBehaviour[] herbivores,
        BaseCreatureBehaviour[] predators)
    {
        var stateDistribution = new List<CreatureStateCount>();

        foreach (CreatureStateType state in Enum.GetValues(typeof(CreatureStateType)))
        {
            int herbivoreCount = herbivores.Count(c => GetCurrentStateType(c) == state);
            int predatorCount = predators.Count(c => GetCurrentStateType(c) == state);

            if (herbivoreCount == 0 && predatorCount == 0)
                continue;

            stateDistribution.Add(new CreatureStateCount
            {
                state = state,
                herbivoreCount = herbivoreCount,
                predatorCount = predatorCount
            });
        }

        return stateDistribution;
    }

    private static CreatureStateType GetCurrentStateType(BaseCreatureBehaviour creature)
    {
        return creature != null ? creature.CurrentStateType : CreatureStateType.None;
    }

    private static float CalculateSurvivalRate(int aliveCount, int spawnedCount)
    {
        if (spawnedCount <= 0)
            return 0f;

        return (float)aliveCount / spawnedCount;
    }

    private static float CalculateRatio(int count, int total)
    {
        if (total <= 0)
            return 0f;

        return (float)count / total;
    }

    private static float CalculateAverageUtilityActionWeight(
        BaseCreatureBehaviour[] creatures,
        Func<CreatureUtilityBehaviorData, float> selector)
    {
        if (creatures == null || creatures.Length == 0 || selector == null)
            return 0f;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < creatures.Length; i++)
        {
            BaseCreatureBehaviour creature = creatures[i];
            if (creature == null)
                continue;

            CreatureUtilityBehaviorData profile = UtilityBehaviorScoring.Sanitize(creature.UtilityBehaviorProfile);
            sum += selector(profile);
            count++;
        }

        return count > 0 ? sum / count : 0f;
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
        averageSenseHistory = new List<float>();
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
        diagnosticsHistory = new List<SimulationDiagnosticsSnapshot>();
        LastHerbivoreSnapshot = null;
        LastPredatorSnapshot = null;
        LastDiagnosticsSnapshot = null;
        totalHerbivoresSpawned = 0;
        totalPredatorsSpawned = 0;
        totalFoodSpawned = 0;
        totalReproductionEvents = 0;
        totalOffspringBorn = 0;
        totalPredationAttempts = 0;
        totalPredationSuccesses = 0;
        totalPredationEscapes = 0;
        totalHerbivoreDeaths = 0;
        totalPredatorDeaths = 0;
        deathsByEnergy = 0;
        deathsByPredation = 0;
        deathsByOldAge = 0;
        deathsUnknown = 0;
        timer = 0f;
        elapsedTime = 0f;
    }
}
