
using System.Collections.Generic;
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
    private void Update()
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

        NativeArray<int> closestFoods= new NativeArray<int>(creatures.Count, Allocator.TempJob);

        FindClosestFoodJob job = new FindClosestFoodJob
        {
            creaturePositions = creaturePositions,
            foodPositions = foodPositions,
            closestFoodIndices = closestFoods
        };

        JobHandle handle = job.Schedule(creatures.Count, 64);
        handle.Complete();

        // Eredmények visszaillesztése
        for (int i = 0; i < creatures.Count; i++)
        {
            //creatures[i].SetClosestFoodDistance(closestDistances[i]);
        }

        // Erőforrások felszabadítása
        creaturePositions.Dispose();
        foodPositions.Dispose();
        closestFoods.Dispose();
    }
}

