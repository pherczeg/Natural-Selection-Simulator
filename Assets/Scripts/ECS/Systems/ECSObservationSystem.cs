using Unity.Burst;
using Unity.Collections;
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

        NativeArray<Entity> creatureEntities = creatureQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<CreatureIdentity> creatureIdentities = creatureQuery.ToComponentDataArray<CreatureIdentity>(Allocator.TempJob);
        NativeArray<CreatureTransformMirror> creatureTransforms = creatureQuery.ToComponentDataArray<CreatureTransformMirror>(Allocator.TempJob);
        NativeArray<CreatureAIContextData> creatureContexts = creatureQuery.ToComponentDataArray<CreatureAIContextData>(Allocator.TempJob);
        NativeArray<CreatureObservationSensorData> creatureSensors = creatureQuery.ToComponentDataArray<CreatureObservationSensorData>(Allocator.TempJob);
        NativeArray<FoodMirrorData> foods = foodQuery.ToComponentDataArray<FoodMirrorData>(Allocator.TempJob);
        NativeArray<CreatureObservationResultData> results = new NativeArray<CreatureObservationResultData>(creatureCount, Allocator.TempJob);

        JobHandle handle = new ECSObservationTargetSearchJob
        {
            creatureIdentities = creatureIdentities,
            creatureTransforms = creatureTransforms,
            creatureContexts = creatureContexts,
            creatureSensors = creatureSensors,
            foods = foods,
            results = results
        }.Schedule(creatureCount, 64, Dependency);

        handle.Complete();

        for (int i = 0; i < creatureEntities.Length; i++)
        {
            EntityManager.SetComponentData(creatureEntities[i], results[i]);
        }

        results.Dispose();
        foods.Dispose();
        creatureSensors.Dispose();
        creatureContexts.Dispose();
        creatureTransforms.Dispose();
        creatureIdentities.Dispose();
        creatureEntities.Dispose();
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

    public NativeArray<CreatureObservationResultData> results;

    public void Execute(int index)
    {
        CreatureIdentity creature = creatureIdentities[index];
        float3 creaturePosition = creatureTransforms[index].position;
        CreatureObservationResultData result = CreateEmptyResult();

        if (creature.creatureKind == ECSCreatureKind.Herbivore)
        {
            FindClosestFood(index, creaturePosition, ref result);
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

    private void FindClosestMate(
        int creatureIndex,
        CreatureIdentity creature,
        float3 creaturePosition,
        ref CreatureObservationResultData result)
    {
        float senseRadius = math.max(0f, creatureSensors[creatureIndex].senseRadius);
        float closestDistanceSq = senseRadius * senseRadius;

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
            closestMateDistanceSq = float.MaxValue
        };
    }
}
