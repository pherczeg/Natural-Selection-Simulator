
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


internal class Scheduler : MonoBehaviour
{
    private FoodSpawner foodSpawner;
    private CreatureSpawner creatureSpawner;
    private void Awake()
    {
        foodSpawner = FindObjectOfType<FoodSpawner>();
        creatureSpawner = FindObjectOfType<CreatureSpawner>();
    }
    float lastObservation;
    private void Update()
    {
        if (GameConfig.Instance == null) return;
        lastObservation += Time.fixedDeltaTime;
        if (lastObservation >= GameConfig.Instance.updateInterval)
        {
            creatureSpawner.herbivorCreatures.RemoveAll(creature => creature == null);
            creatureSpawner.predatorCreatures.RemoveAll(creature => creature == null);
            foodSpawner.foods.RemoveAll(food => food == null);
            ScheduleFoodObservationJobs();
            ScheduleFoodCreatureObservationJobs();
            SchedulePossibleMatingObservationJobs();
            lastObservation = 0f;
        }
    }

    private void ScheduleFoodObservationJobs()
    {
        
        List<BaseCreatureBehaviour> creatures = creatureSpawner.herbivorCreatures;
        List<Food> foods = foodSpawner.foods;

        NativeArray<float3> creaturePositions = new NativeArray<float3>(creatures.Count, Allocator.TempJob);
        NativeArray<float3> foodPositions = new NativeArray<float3>(foods.Count, Allocator.TempJob);
        NativeArray<float> senseRadii = new NativeArray<float>(creatures.Count, Allocator.TempJob);
        for (int i = 0; i < creatures.Count; i++)
        {
            creaturePositions[i] = creatures[i].transform.position;
            senseRadii[i] = creatures[i].ObservationManager.SenseRadius;
        }
        for (int i = 0; i < foods.Count; i++)
        {
            foodPositions[i] = foods[i].transform.position;
        }
        NativeArray<int> closestFoods = new NativeArray<int>(creatures.Count, Allocator.TempJob);
        FindClosestFoodJob job = new FindClosestFoodJob
        {
            creaturePositions = creaturePositions,
            foodPositions = foodPositions,
            senseRadii = senseRadii,
            closestFoodIndices = closestFoods
        };
        JobHandle handle = job.Schedule(creatures.Count, 64);
        handle.Complete();
        for (int i = 0; i < creatures.Count; i++)
        {
            creatures[i].ObservationManager.Observations.Clear();
        }
        for (int i = 0; i < creatures.Count; i++)
        {
            var index = closestFoods[i];
            if (index < 0 || index >= foods.Count)
            {
                continue;
            }
            var food = foodSpawner.foods[index];
            ObservationData observationData = new();
            observationData.observedObject = food.gameObject;
            observationData.type = ObservationType.Food;
            creatures[i].ObservationManager.Observations.Add(observationData);
        }
        closestFoods.Dispose();
        senseRadii.Dispose();
        creaturePositions.Dispose();
        foodPositions.Dispose();
    }

    private void SchedulePossibleMatingObservationJobs()
    {
        SchedulePossibleMatingObservationJobsFor(creatureSpawner.herbivorCreatures);
        SchedulePossibleMatingObservationJobsFor(creatureSpawner.predatorCreatures);
    }

    private void SchedulePossibleMatingObservationJobsFor(List<BaseCreatureBehaviour> sourceCreatures)
    {
        if (sourceCreatures == null || sourceCreatures.Count == 0)
            return;

        List<BaseCreatureBehaviour> creatures = sourceCreatures
            .Where(c => c != null
                        && c.gameObject.activeInHierarchy
                        && c.ReproductionManager != null
                        && c.ReproductionManager.IsReadyToReproduction())
            .ToList();

        for (int i = 0; i < creatures.Count; i++)
        {
            BaseCreatureBehaviour creature = creatures[i];
            BaseCreatureBehaviour closestMate = null;
            float closestDistance = float.MaxValue;

            for (int j = 0; j < creatures.Count; j++)
            {
                BaseCreatureBehaviour candidate = creatures[j];
                if (candidate == creature || !creature.ReproductionManager.CanMateWith(candidate))
                    continue;

                float distance = Vector3.Distance(creature.transform.position, candidate.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestMate = candidate;
                }
            }

            if (closestMate == null)
                continue;

            ObservationData observationData = new();
            observationData.observedObject = closestMate.gameObject;
            observationData.type = ObservationType.MatingCreature;
            creature.ObservationManager.Observations.Add(observationData);
        }
    }

    private void ScheduleFoodCreatureObservationJobs()
    {
        List<BaseCreatureBehaviour> herbivoreCreatures = creatureSpawner.herbivorCreatures
            .Where(c => c != null && c.gameObject.activeInHierarchy)
            .ToList();
        List<BaseCreatureBehaviour> predatorCreatures = creatureSpawner.predatorCreatures
            .Where(c => c != null && c.gameObject.activeInHierarchy)
            .ToList();

        if (predatorCreatures.Count == 0)
            return;

        if (herbivoreCreatures.Count == 0)
        {
            for (int i = 0; i < predatorCreatures.Count; i++)
            {
                predatorCreatures[i].ObservationManager.Observations.Clear();
            }
            return;
        }

        NativeArray<float3> herbivoreCreaturePositions = new NativeArray<float3>(herbivoreCreatures.Count, Allocator.TempJob);
        NativeArray<float3> predatorCreaturePositions = new NativeArray<float3>(predatorCreatures.Count, Allocator.TempJob);
        for (int i = 0; i < herbivoreCreatures.Count; i++)
        {
            herbivoreCreaturePositions[i] = herbivoreCreatures[i].transform.position;
        }
        for (int i = 0; i < predatorCreatures.Count; i++)
        {
            predatorCreaturePositions[i] = predatorCreatures[i].transform.position;
        }
        NativeArray<int> closestFoodCreatureIndices = new NativeArray<int>(predatorCreatures.Count, Allocator.TempJob);
        FindClosestFoodCreatureJob job = new FindClosestFoodCreatureJob
        {
            herbivorPositions = herbivoreCreaturePositions,
            predatorPositions = predatorCreaturePositions,
            closestCreatureIndices = closestFoodCreatureIndices
        };
        JobHandle handle = job.Schedule(predatorCreatures.Count, 64);
        handle.Complete();

        for (int i = 0; i < predatorCreatures.Count; i++)
        {
            predatorCreatures[i].ObservationManager.Observations.Clear();
        }

        for (int i = 0; i < predatorCreatures.Count; i++)
        {
            var index = closestFoodCreatureIndices[i];
            if (index < 0 || index >= herbivoreCreatures.Count)
            {
                continue;
            }
            var foodCreature = herbivoreCreatures[index];
            ObservationData observationData = new();
            observationData.observedObject = foodCreature.gameObject;
            observationData.type = ObservationType.FoodCreature;
            predatorCreatures[i].ObservationManager.Observations.Add(observationData);
        }
        closestFoodCreatureIndices.Dispose();
        herbivoreCreaturePositions.Dispose();
        predatorCreaturePositions.Dispose();
    }
}

