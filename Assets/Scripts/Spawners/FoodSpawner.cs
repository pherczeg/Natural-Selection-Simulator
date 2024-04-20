using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    public static FoodSpawner Instance { get; private set; }

    public GameObject foodPrefab;
    public int initialFoodCount = 20;
    public int poolSizeOfFood = 2000;
    public float spawnRate = 5f; // spawns food every 5 seconds by default
    public float spawnNumber = 5f;
    private GameObject ground;
    public List<Food> foods;
    private void Start()
    {
        foods = new List<Food>();
        ground = GameObject.FindGameObjectWithTag("Ground");
        if (ground == null)
        {
            throw new System.Exception("Ground was not found for FoodSpawing");
        }
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        
        PoolManager.Instance.CreatePool(foodPrefab, poolSizeOfFood);
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
                var foodGameObject = PoolManager.Instance.GetObject(foodPrefab);
                foodGameObject.transform.position = randomPosition;
                foodGameObject.transform.rotation = Quaternion.identity;
                var foodComponent = foodGameObject.GetComponent<Food>();
                foodComponent.nutritionValue = Random.Range(GameConfig.Instance.minNutrionValue, GameConfig.Instance.maxNutrionValue);

                foodComponent.age = 0;
                foods.Add(foodComponent);
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
