using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ECSObservationSystem : SystemBase
{
    private EntityQuery creatureQuery;
    private EntityQuery foodQuery;
    private float observationTimer;
    private bool hasObserved;

    protected override void OnCreate()
    {
        creatureQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
            ComponentType.ReadOnly<CreatureTransformMirror>(),
            ComponentType.ReadOnly<CreatureAIContextData>(),
            ComponentType.ReadOnly<CreatureObservationSensorData>(),
            ComponentType.ReadWrite<CreatureObservationResultData>());

        foodQuery = GetEntityQuery(ComponentType.ReadOnly<FoodMirrorData>());

        RequireForUpdate(creatureQuery);
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useEcsObservation)
        {
            observationTimer = 0f;
            hasObserved = false;
            return;
        }

        observationTimer += (float)World.Time.DeltaTime;
        if (hasObserved && observationTimer < math.max(0.0001f, config.updateInterval))
            return;

        observationTimer = 0f;
        hasObserved = true;

        int creatureCount = creatureQuery.CalculateEntityCount();
        if (creatureCount == 0)
            return;

        SimulationPerfCounters.AddSensorQueries(creatureCount);

        NativeArray<Entity> creatureEntities = creatureQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<CreatureIdentity> creatureIdentities = creatureQuery.ToComponentDataArray<CreatureIdentity>(Allocator.TempJob);
        NativeArray<CreatureTransformMirror> creatureTransforms = creatureQuery.ToComponentDataArray<CreatureTransformMirror>(Allocator.TempJob);
        NativeArray<CreatureAIContextData> creatureContexts = creatureQuery.ToComponentDataArray<CreatureAIContextData>(Allocator.TempJob);
        NativeArray<CreatureObservationSensorData> creatureSensors = creatureQuery.ToComponentDataArray<CreatureObservationSensorData>(Allocator.TempJob);
        NativeArray<FoodMirrorData> foods = foodQuery.ToComponentDataArray<FoodMirrorData>(Allocator.TempJob);
        NativeArray<CreatureObservationResultData> results = new NativeArray<CreatureObservationResultData>(creatureCount, Allocator.TempJob);

        float inverseCellSize = ComputeInverseCellSize(creatureSensors);
        NativeParallelMultiHashMap<int, int> foodCellIndices =
            new NativeParallelMultiHashMap<int, int>(math.max(1, foods.Length), Allocator.TempJob);
        NativeParallelMultiHashMap<int, int> herbivoreCellIndices =
            new NativeParallelMultiHashMap<int, int>(math.max(1, creatureCount), Allocator.TempJob);
        NativeParallelMultiHashMap<int, int> predatorCellIndices =
            new NativeParallelMultiHashMap<int, int>(math.max(1, creatureCount), Allocator.TempJob);
        NativeParallelMultiHashMap<int, int> reproductionReadyCellIndices =
            new NativeParallelMultiHashMap<int, int>(math.max(1, creatureCount), Allocator.TempJob);

        for (int i = 0; i < foods.Length; i++)
        {
            int2 foodCell = ECSObservationTargetSearchJob.PositionToCell(foods[i].position, inverseCellSize);
            foodCellIndices.Add(ECSObservationTargetSearchJob.HashCell(foodCell), i);
        }

        for (int i = 0; i < creatureCount; i++)
        {
            int2 creatureCell = ECSObservationTargetSearchJob.PositionToCell(creatureTransforms[i].position, inverseCellSize);
            int cellHash = ECSObservationTargetSearchJob.HashCell(creatureCell);

            if (creatureIdentities[i].creatureKind == ECSCreatureKind.Herbivore)
            {
                herbivoreCellIndices.Add(cellHash, i);
            }

            if (creatureIdentities[i].creatureKind == ECSCreatureKind.Predator)
            {
                predatorCellIndices.Add(cellHash, i);
            }

            if (creatureContexts[i].isReproductionReady)
            {
                reproductionReadyCellIndices.Add(cellHash, i);
            }
        }

        JobHandle searchHandle = new ECSObservationTargetSearchJob
        {
            creatureIdentities = creatureIdentities,
            creatureTransforms = creatureTransforms,
            creatureContexts = creatureContexts,
            creatureSensors = creatureSensors,
            foods = foods,
            foodCellIndices = foodCellIndices,
            herbivoreCellIndices = herbivoreCellIndices,
            predatorCellIndices = predatorCellIndices,
            reproductionReadyCellIndices = reproductionReadyCellIndices,
            inverseCellSize = inverseCellSize,
            useSpatialHash = true,
            results = results
        }.Schedule(creatureCount, 64, Dependency);

        JobHandle writeHandle = new ECSObservationWriteBackJob
        {
            creatureEntities = creatureEntities,
            results = results,
            observationResults = GetComponentLookup<CreatureObservationResultData>(false)
        }.Schedule(creatureCount, 64, searchHandle);

        JobHandle disposeHandle = results.Dispose(writeHandle);
        disposeHandle = reproductionReadyCellIndices.Dispose(disposeHandle);
        disposeHandle = predatorCellIndices.Dispose(disposeHandle);
        disposeHandle = herbivoreCellIndices.Dispose(disposeHandle);
        disposeHandle = foodCellIndices.Dispose(disposeHandle);
        disposeHandle = foods.Dispose(disposeHandle);
        disposeHandle = creatureSensors.Dispose(disposeHandle);
        disposeHandle = creatureContexts.Dispose(disposeHandle);
        disposeHandle = creatureTransforms.Dispose(disposeHandle);
        disposeHandle = creatureIdentities.Dispose(disposeHandle);
        disposeHandle = creatureEntities.Dispose(disposeHandle);

        Dependency = disposeHandle;
    }

    private static float ComputeInverseCellSize(NativeArray<CreatureObservationSensorData> creatureSensors)
    {
        float maxSenseRadius = 0f;
        for (int i = 0; i < creatureSensors.Length; i++)
        {
            maxSenseRadius = math.max(maxSenseRadius, creatureSensors[i].senseRadius);
        }

        float cellSize = math.max(1f, maxSenseRadius * 0.5f);
        return 1f / cellSize;
    }
}

[BurstCompile]
public struct ECSObservationWriteBackJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Entity> creatureEntities;
    [ReadOnly] public NativeArray<CreatureObservationResultData> results;

    [NativeDisableParallelForRestriction]
    public ComponentLookup<CreatureObservationResultData> observationResults;

    public void Execute(int index)
    {
        Entity entity = creatureEntities[index];
        if (!observationResults.HasComponent(entity))
            return;

        observationResults[entity] = results[index];
    }
}

[BurstCompile]
public struct ECSObservationTargetSearchJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<CreatureIdentity> creatureIdentities;
    [ReadOnly] public NativeArray<CreatureTransformMirror> creatureTransforms;
    [ReadOnly] public NativeArray<CreatureAIContextData> creatureContexts;
    [ReadOnly] public NativeArray<CreatureObservationSensorData> creatureSensors;
    [ReadOnly] public NativeArray<FoodMirrorData> foods;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> foodCellIndices;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> herbivoreCellIndices;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> predatorCellIndices;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> reproductionReadyCellIndices;
    public float inverseCellSize;
    public bool useSpatialHash;

    public NativeArray<CreatureObservationResultData> results;

    public void Execute(int index)
    {
        CreatureIdentity creature = creatureIdentities[index];
        float3 creaturePosition = creatureTransforms[index].position;
        CreatureObservationResultData result = CreateEmptyResult();

        if (creature.creatureKind == ECSCreatureKind.Herbivore)
        {
            FindClosestFood(index, creaturePosition, ref result);
            FindClosestThreat(index, creaturePosition, ref result);
        }
        else if (creature.creatureKind == ECSCreatureKind.Predator)
        {
            FindClosestPrey(index, creaturePosition, ref result);
        }

        if (creatureContexts[index].isReproductionReady)
        {
            FindClosestMate(index, creature, creaturePosition, ref result);
        }

        results[index] = result;
    }

    private void FindClosestFood(
        int creatureIndex,
        float3 creaturePosition,
        ref CreatureObservationResultData result)
    {
        float senseRadius = math.max(0f, creatureSensors[creatureIndex].senseRadius);
        float closestDistanceSq = senseRadius * senseRadius;

        if (useSpatialHash && foodCellIndices.IsCreated && inverseCellSize > 0f)
        {
            int2 centerCell = PositionToCell(creaturePosition, inverseCellSize);
            int cellRadius = (int)math.ceil(senseRadius * inverseCellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int2 cell = centerCell + new int2(dx, dz);
                    int cellHash = HashCell(cell);

                    if (!foodCellIndices.TryGetFirstValue(
                            cellHash,
                            out int foodIndex,
                            out NativeParallelMultiHashMapIterator<int> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        FoodMirrorData food = foods[foodIndex];
                        float distanceSq = math.lengthsq(food.position - creaturePosition);
                        if (distanceSq < closestDistanceSq)
                        {
                            closestDistanceSq = distanceSq;
                            result.closestFoodInstanceId = food.gameObjectInstanceId;
                            result.closestFoodDistanceSq = distanceSq;
                        }
                    }
                    while (foodCellIndices.TryGetNextValue(out foodIndex, ref iterator));
                }
            }

            return;
        }

        for (int i = 0; i < foods.Length; i++)
        {
            FoodMirrorData food = foods[i];
            float distanceSq = math.lengthsq(food.position - creaturePosition);
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                result.closestFoodInstanceId = food.gameObjectInstanceId;
                result.closestFoodDistanceSq = distanceSq;
            }
        }
    }

    private void FindClosestPrey(
        int creatureIndex,
        float3 predatorPosition,
        ref CreatureObservationResultData result)
    {
        float senseRadius = math.max(0f, creatureSensors[creatureIndex].senseRadius);
        float closestDistanceSq = senseRadius * senseRadius;

        if (useSpatialHash && herbivoreCellIndices.IsCreated && inverseCellSize > 0f)
        {
            int2 centerCell = PositionToCell(predatorPosition, inverseCellSize);
            int cellRadius = (int)math.ceil(senseRadius * inverseCellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int2 cell = centerCell + new int2(dx, dz);
                    int cellHash = HashCell(cell);

                    if (!herbivoreCellIndices.TryGetFirstValue(
                            cellHash,
                            out int candidateIndex,
                            out NativeParallelMultiHashMapIterator<int> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        CreatureIdentity candidate = creatureIdentities[candidateIndex];
                        float distanceSq = math.lengthsq(creatureTransforms[candidateIndex].position - predatorPosition);
                        if (distanceSq < closestDistanceSq)
                        {
                            closestDistanceSq = distanceSq;
                            result.closestPreyInstanceId = candidate.gameObjectInstanceId;
                            result.closestPreyDistanceSq = distanceSq;
                        }
                    }
                    while (herbivoreCellIndices.TryGetNextValue(out candidateIndex, ref iterator));
                }
            }

            return;
        }

        for (int i = 0; i < creatureIdentities.Length; i++)
        {
            CreatureIdentity candidate = creatureIdentities[i];
            if (candidate.creatureKind != ECSCreatureKind.Herbivore)
                continue;

            float distanceSq = math.lengthsq(creatureTransforms[i].position - predatorPosition);
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                result.closestPreyInstanceId = candidate.gameObjectInstanceId;
                result.closestPreyDistanceSq = distanceSq;
            }
        }
    }

    private void FindClosestThreat(
        int creatureIndex,
        float3 creaturePosition,
        ref CreatureObservationResultData result)
    {
        float senseRadius = math.max(0f, creatureSensors[creatureIndex].senseRadius);
        float closestDistanceSq = senseRadius * senseRadius;

        if (useSpatialHash && predatorCellIndices.IsCreated && inverseCellSize > 0f)
        {
            int2 centerCell = PositionToCell(creaturePosition, inverseCellSize);
            int cellRadius = (int)math.ceil(senseRadius * inverseCellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int2 cell = centerCell + new int2(dx, dz);
                    int cellHash = HashCell(cell);

                    if (!predatorCellIndices.TryGetFirstValue(
                            cellHash,
                            out int candidateIndex,
                            out NativeParallelMultiHashMapIterator<int> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        CreatureIdentity candidate = creatureIdentities[candidateIndex];
                        float distanceSq = math.lengthsq(creatureTransforms[candidateIndex].position - creaturePosition);
                        if (distanceSq < closestDistanceSq)
                        {
                            closestDistanceSq = distanceSq;
                            result.closestThreatInstanceId = candidate.gameObjectInstanceId;
                            result.closestThreatDistanceSq = distanceSq;
                        }
                    }
                    while (predatorCellIndices.TryGetNextValue(out candidateIndex, ref iterator));
                }
            }

            return;
        }

        for (int i = 0; i < creatureIdentities.Length; i++)
        {
            CreatureIdentity candidate = creatureIdentities[i];
            if (candidate.creatureKind != ECSCreatureKind.Predator)
                continue;

            float distanceSq = math.lengthsq(creatureTransforms[i].position - creaturePosition);
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                result.closestThreatInstanceId = candidate.gameObjectInstanceId;
                result.closestThreatDistanceSq = distanceSq;
            }
        }
    }

    private void FindClosestMate(
        int creatureIndex,
        CreatureIdentity creature,
        float3 creaturePosition,
        ref CreatureObservationResultData result)
    {
        float senseRadius = math.max(0f, creatureSensors[creatureIndex].senseRadius);
        float closestDistanceSq = senseRadius * senseRadius;

        if (useSpatialHash && reproductionReadyCellIndices.IsCreated && inverseCellSize > 0f)
        {
            int2 centerCell = PositionToCell(creaturePosition, inverseCellSize);
            int cellRadius = (int)math.ceil(senseRadius * inverseCellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int2 cell = centerCell + new int2(dx, dz);
                    int cellHash = HashCell(cell);

                    if (!reproductionReadyCellIndices.TryGetFirstValue(
                            cellHash,
                            out int candidateIndex,
                            out NativeParallelMultiHashMapIterator<int> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        if (candidateIndex == creatureIndex)
                            continue;

                        CreatureIdentity candidate = creatureIdentities[candidateIndex];
                        if (candidate.creatureKind != creature.creatureKind || candidate.sex == creature.sex)
                            continue;

                        float distanceSq = math.lengthsq(creatureTransforms[candidateIndex].position - creaturePosition);
                        if (distanceSq < closestDistanceSq)
                        {
                            closestDistanceSq = distanceSq;
                            result.closestMateInstanceId = candidate.gameObjectInstanceId;
                            result.closestMateDistanceSq = distanceSq;
                        }
                    }
                    while (reproductionReadyCellIndices.TryGetNextValue(out candidateIndex, ref iterator));
                }
            }

            return;
        }

        for (int i = 0; i < creatureIdentities.Length; i++)
        {
            if (i == creatureIndex)
                continue;

            CreatureIdentity candidate = creatureIdentities[i];
            if (candidate.creatureKind != creature.creatureKind ||
                candidate.sex == creature.sex ||
                !creatureContexts[i].isReproductionReady)
            {
                continue;
            }

            float distanceSq = math.lengthsq(creatureTransforms[i].position - creaturePosition);
            if (distanceSq < closestDistanceSq)
            {
                closestDistanceSq = distanceSq;
                result.closestMateInstanceId = candidate.gameObjectInstanceId;
                result.closestMateDistanceSq = distanceSq;
            }
        }
    }

    private static CreatureObservationResultData CreateEmptyResult()
    {
        return new CreatureObservationResultData
        {
            closestFoodDistanceSq = float.MaxValue,
            closestPreyDistanceSq = float.MaxValue,
            closestMateDistanceSq = float.MaxValue,
            closestThreatDistanceSq = float.MaxValue
        };
    }

    public static int2 PositionToCell(float3 position, float inverseCellSize)
    {
        float2 projected = new float2(position.x, position.z);
        return (int2)math.floor(projected * inverseCellSize);
    }

    public static int HashCell(int2 cell)
    {
        unchecked
        {
            return (cell.x * 73856093) ^ (cell.y * 19349663);
        }
    }
}
