using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Per-species aggregates computed from mirrored ECS data. Covers only the values
/// present in CreatureLifecycleData/CreatureIdentity; everything else (current speed,
/// desirability, sprint profile, agility/strength, dove/hawk, utility weights) stays
/// on the MonoBehaviour polling path in Statistics.
/// </summary>
public struct EcsSpeciesAggregateData
{
    public int count;
    public int femaleCount;
    public int maleCount;

    public float averageWeight;
    public float minWeight;
    public float maxWeight;

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

    public float averageBaseSenseRadius;
    public float minBaseSenseRadius;
    public float maxBaseSenseRadius;
}

/// <summary>
/// Main-thread handoff point between ECSStatisticsSystem (writer) and
/// Statistics/RealtimeSimulationStatsUI (readers). Snapshots older than
/// MaxSnapshotAgeSeconds are treated as missing so readers fall back to
/// the MonoBehaviour polling path if the system stops running.
/// </summary>
public static class ECSStatisticsMirror
{
    public const float MaxSnapshotAgeSeconds = 2f;

    public static bool HasSnapshot { get; private set; }
    public static float LastWriteRealtime { get; private set; }
    public static EcsSpeciesAggregateData Herbivores { get; private set; }
    public static EcsSpeciesAggregateData Predators { get; private set; }
    public static int FoodCount { get; private set; }

    public static void Publish(
        EcsSpeciesAggregateData herbivores,
        EcsSpeciesAggregateData predators,
        int foodCount)
    {
        Herbivores = herbivores;
        Predators = predators;
        FoodCount = foodCount;
        LastWriteRealtime = UnityEngine.Time.realtimeSinceStartup;
        HasSnapshot = true;
    }

    public static void Clear()
    {
        HasSnapshot = false;
        Herbivores = default;
        Predators = default;
        FoodCount = 0;
    }

    public static bool TryGet(
        out EcsSpeciesAggregateData herbivores,
        out EcsSpeciesAggregateData predators,
        out int foodCount)
    {
        if (!HasSnapshot ||
            UnityEngine.Time.realtimeSinceStartup - LastWriteRealtime > MaxSnapshotAgeSeconds)
        {
            herbivores = default;
            predators = default;
            foodCount = 0;
            return false;
        }

        herbivores = Herbivores;
        predators = Predators;
        foodCount = FoodCount;
        return true;
    }

    public static bool IsEnabled(GameConfig config)
    {
        return config != null && config.useEcsStatistics && config.useEcsCreatureLifecycle;
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSCreatureLifecycleSystem))]
public partial class ECSStatisticsSystem : SystemBase
{
    private EntityQuery creatureQuery;
    private EntityQuery foodQuery;
    private float statisticsTimer;
    private bool hasAggregated;

    protected override void OnCreate()
    {
        creatureQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
            ComponentType.ReadOnly<CreatureLifecycleData>());
        foodQuery = GetEntityQuery(ComponentType.ReadOnly<FoodMirrorData>());

        RequireForUpdate(creatureQuery);
    }

    protected override void OnStopRunning()
    {
        ECSStatisticsMirror.Clear();
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (!ECSStatisticsMirror.IsEnabled(config))
        {
            statisticsTimer = 0f;
            hasAggregated = false;
            ECSStatisticsMirror.Clear();
            return;
        }

        statisticsTimer += (float)World.Time.DeltaTime;
        if (hasAggregated && statisticsTimer < math.max(0.0001f, config.updateInterval))
            return;

        statisticsTimer = 0f;
        hasAggregated = true;

        NativeArray<CreatureIdentity> identities = creatureQuery.ToComponentDataArray<CreatureIdentity>(Allocator.TempJob);
        NativeArray<CreatureLifecycleData> lifecycles = creatureQuery.ToComponentDataArray<CreatureLifecycleData>(Allocator.TempJob);
        NativeArray<EcsSpeciesAggregateData> results = new NativeArray<EcsSpeciesAggregateData>(2, Allocator.TempJob);

        new ECSStatisticsAggregationJob
        {
            identities = identities,
            lifecycles = lifecycles,
            results = results
        }.Run();

        ECSStatisticsMirror.Publish(results[0], results[1], foodQuery.CalculateEntityCount());

        results.Dispose();
        lifecycles.Dispose();
        identities.Dispose();
    }
}

[BurstCompile]
public struct ECSStatisticsAggregationJob : IJob
{
    public const int HerbivoreResultIndex = 0;
    public const int PredatorResultIndex = 1;

    [ReadOnly] public NativeArray<CreatureIdentity> identities;
    [ReadOnly] public NativeArray<CreatureLifecycleData> lifecycles;

    /// <summary>Length 2: [0] herbivores, [1] predators.</summary>
    public NativeArray<EcsSpeciesAggregateData> results;

    private struct SpeciesAccumulator
    {
        public int count;
        public int femaleCount;
        public int maleCount;
        public float weightSum, weightMin, weightMax;
        public float baseSpeedSum, baseSpeedMin, baseSpeedMax;
        public float energySum, energyMin, energyMax;
        public float currentMaxEnergySum, currentMaxEnergyMin, currentMaxEnergyMax;
        public float ageSum, ageMin, ageMax;
        public float baseSenseSum, baseSenseMin, baseSenseMax;

        public static SpeciesAccumulator Create()
        {
            return new SpeciesAccumulator
            {
                weightMin = float.MaxValue,
                weightMax = float.MinValue,
                baseSpeedMin = float.MaxValue,
                baseSpeedMax = float.MinValue,
                energyMin = float.MaxValue,
                energyMax = float.MinValue,
                currentMaxEnergyMin = float.MaxValue,
                currentMaxEnergyMax = float.MinValue,
                ageMin = float.MaxValue,
                ageMax = float.MinValue,
                baseSenseMin = float.MaxValue,
                baseSenseMax = float.MinValue
            };
        }

        public void Add(in CreatureIdentity identity, in CreatureLifecycleData lifecycle)
        {
            count++;
            if (identity.sex == 0)
            {
                femaleCount++;
            }
            else
            {
                maleCount++;
            }

            weightSum += lifecycle.weight;
            weightMin = math.min(weightMin, lifecycle.weight);
            weightMax = math.max(weightMax, lifecycle.weight);

            baseSpeedSum += lifecycle.baseMoveSpeed;
            baseSpeedMin = math.min(baseSpeedMin, lifecycle.baseMoveSpeed);
            baseSpeedMax = math.max(baseSpeedMax, lifecycle.baseMoveSpeed);

            energySum += lifecycle.energyLevel;
            energyMin = math.min(energyMin, lifecycle.energyLevel);
            energyMax = math.max(energyMax, lifecycle.energyLevel);

            currentMaxEnergySum += lifecycle.currentMaxEnergy;
            currentMaxEnergyMin = math.min(currentMaxEnergyMin, lifecycle.currentMaxEnergy);
            currentMaxEnergyMax = math.max(currentMaxEnergyMax, lifecycle.currentMaxEnergy);

            ageSum += lifecycle.age;
            ageMin = math.min(ageMin, lifecycle.age);
            ageMax = math.max(ageMax, lifecycle.age);

            baseSenseSum += lifecycle.baseSenseRadius;
            baseSenseMin = math.min(baseSenseMin, lifecycle.baseSenseRadius);
            baseSenseMax = math.max(baseSenseMax, lifecycle.baseSenseRadius);
        }

        /// <summary>Empty species yields all zeros, matching Statistics.FillTriplet.</summary>
        public EcsSpeciesAggregateData ToResult()
        {
            if (count == 0)
                return default;

            float inverseCount = 1f / count;
            return new EcsSpeciesAggregateData
            {
                count = count,
                femaleCount = femaleCount,
                maleCount = maleCount,
                averageWeight = weightSum * inverseCount,
                minWeight = weightMin,
                maxWeight = weightMax,
                averageBaseSpeed = baseSpeedSum * inverseCount,
                minBaseSpeed = baseSpeedMin,
                maxBaseSpeed = baseSpeedMax,
                averageEnergy = energySum * inverseCount,
                minEnergy = energyMin,
                maxEnergy = energyMax,
                averageCurrentMaxEnergy = currentMaxEnergySum * inverseCount,
                minCurrentMaxEnergy = currentMaxEnergyMin,
                maxCurrentMaxEnergy = currentMaxEnergyMax,
                averageAge = ageSum * inverseCount,
                minAge = ageMin,
                maxAge = ageMax,
                averageBaseSenseRadius = baseSenseSum * inverseCount,
                minBaseSenseRadius = baseSenseMin,
                maxBaseSenseRadius = baseSenseMax
            };
        }
    }

    public void Execute()
    {
        SpeciesAccumulator herbivores = SpeciesAccumulator.Create();
        SpeciesAccumulator predators = SpeciesAccumulator.Create();

        for (int i = 0; i < identities.Length; i++)
        {
            CreatureIdentity identity = identities[i];
            if (identity.creatureKind == ECSCreatureKind.Herbivore)
            {
                herbivores.Add(identity, lifecycles[i]);
            }
            else if (identity.creatureKind == ECSCreatureKind.Predator)
            {
                predators.Add(identity, lifecycles[i]);
            }
        }

        results[HerbivoreResultIndex] = herbivores.ToResult();
        results[PredatorResultIndex] = predators.ToResult();
    }
}
