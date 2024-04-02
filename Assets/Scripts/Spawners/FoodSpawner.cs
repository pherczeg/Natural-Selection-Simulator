using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    public GameObject foodPrefab;
    public int initialFoodCount = 20;
    public float spawnRate = 5f; // spawns food every 5 seconds by default
    public float spawnNumber = 5f;
    private GameObject ground;
    public List<Food> foods;
    private void Awake()
    {
        foods = new List<Food>();
        ground = GameObject.FindGameObjectWithTag("Ground");
        if (ground == null)
        {
            throw new System.Exception("Ground was not found for FoodSpawing");
        }
        for (int i = 0; i < initialFoodCount; i++)
        {
            SpawnFood();
        }

        // Start the spawning coroutine
        StartCoroutine(SpawnFoodAtRate());
    }

    void SpawnFood()
    {
        if (ground != null)
        {
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                Bounds bounds = groundRenderer.bounds;
                Vector3 randomPosition = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    1.5f, // Ez az érték függ az étel és a talaj magasságától
                    Random.Range(bounds.min.z, bounds.max.z)
                );

                // Ensure the place is not occupied.
                while (IsPlaceOccupied(randomPosition))
                {
                    randomPosition = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        1.5f, // Ez az érték függ az étel és a talaj magasságától
                        Random.Range(bounds.min.z, bounds.max.z)
                    );
                }

                var foodGameObject = Instantiate(foodPrefab, randomPosition, Quaternion.identity);
                foods.Add(foodGameObject.GetComponent<Food>());
            }
        }
    }

    public void RemoveFromList(Food food)
    {
        foods.Remove(food);
    }

    bool IsPlaceOccupied(Vector3 position)
    {
        float checkRadius = 1.5f; // adjust this value based on the size of your food objects
        Collider[] colliders = Physics.OverlapSphere(position, checkRadius);

        foreach (var collider in colliders)
        {
            if (!collider.isTrigger && collider.gameObject != gameObject && collider.gameObject.name != "Plane")
            {
                return true;
            }
        }

        return false;
    }


    IEnumerator SpawnFoodAtRate()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnRate);
            for (int i = 0; i < spawnNumber; i++)
                SpawnFood();
        }
    }
}
