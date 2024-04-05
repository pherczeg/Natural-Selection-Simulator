
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
        lastObservation += Time.fixedDeltaTime;
        if (lastObservation >= GameConfig.Instance.updateInterval)
        {
            creatureSpawner.creatures.RemoveAll(creature => creature == null);
            foodSpawner.foods.RemoveAll(food => food == null);
            ScheduleFoodObservationJobs();
            SchedulePossibleMatingObservationJobs();
            lastObservation = 0f;
        }
    }

    private void ScheduleFoodObservationJobs()
    {
        
        List<CreatureBehaviour> creatures = creatureSpawner.creatures;
        List<Food> foods = foodSpawner.foods;

        NativeArray<float3> creaturePositions = new NativeArray<float3>(creatures.Count, Allocator.TempJob);
        NativeArray<float3> foodPositions = new NativeArray<float3>(foods.Count, Allocator.TempJob);
        for (int i = 0; i < creatures.Count; i++)
        {
            creaturePositions[i] = creatures[i].transform.position;
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
            if (index < 0 || index > foods.Count)
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
        creaturePositions.Dispose();
        foodPositions.Dispose();
    }

    private void SchedulePossibleMatingObservationJobs()
    {
        List<CreatureBehaviour> creatures = creatureSpawner.creatures.Where(c=>c.ReproductionManager.IsReadyToReproduction()).ToList();
        NativeArray<float3> creaturePositions = new NativeArray<float3>(creatures.Count, Allocator.TempJob);
        for (int i = 0; i < creatures.Count; i++)
        {
            creaturePositions[i] = creatures[i].transform.position;
        }
        NativeArray<int> closestReproductiveCreatures = new NativeArray<int>(creatures.Count, Allocator.TempJob);
        FindClosestReproductiveCreature job = new FindClosestReproductiveCreature
        {
            creaturePositions = creaturePositions,
            closestCreatureIndices = closestReproductiveCreatures
        };
        JobHandle handle = job.Schedule(creatures.Count, 64);
        handle.Complete();
        for (int i = 0; i < creatures.Count; i++)
        {
            var index = closestReproductiveCreatures[i];
            if (index < 0 || index > creatures.Count)
            {
                continue;
            }
            var creature = creatures[index];
            ObservationData observationData = new();
            observationData.observedObject = creature.gameObject;
            observationData.type = ObservationType.Creature;
            creatures[i].ObservationManager.Observations.Add(observationData);
        }
        closestReproductiveCreatures.Dispose();
        creaturePositions.Dispose();
    }
}

