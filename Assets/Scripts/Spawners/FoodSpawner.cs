using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    public static FoodSpawner Instance { get; private set; }

    public GameObject foodPrefab;
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

        GameConfig config = GameConfig.Instance;
        if (config == null)
        {
            Debug.LogError("GameConfig instance not found.");
            return;
        }

        int foodPoolSize = Mathf.Max(1, config.foodPoolSize);
        int initialFoodCount = Mathf.Max(0, config.initialFoodCount);

        PoolManager.Instance.CreatePool(foodPrefab, foodPoolSize);
        for (int i = 0; i < initialFoodCount; i++)
        {
            if (!CanSpawnFood())
                break;

            RequestSpawnFood();
        }

        StartCoroutine(SpawnFoodAtRate());
    }

    void RequestSpawnFood()
    {
        if (ground == null || !CanSpawnFood()) return;
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer == null) return;

        Bounds bounds = groundRenderer.bounds;
        Vector3 randomPosition = GetRandomGroundPosition(bounds);

        GameConfig config = GameConfig.Instance;
        if (config == null)
            return;

        float targetNutrition = UnityEngine.Random.Range(config.minNutrionValue, config.maxNutrionValue);
        SpawnFoodRequest request = new SpawnFoodRequest
        {
            position = new float3(randomPosition.x, randomPosition.y, randomPosition.z),
            targetNutrition = targetNutrition
        };

        if (!ECSMirrorBridge.TryRequestSpawnFood(request))
        {
            SpawnFoodFromRequest(request);
        }
    }

    public Food SpawnFoodFromRequest(SpawnFoodRequest request)
    {
        if (foods == null)
        {
            foods = new List<Food>();
        }

        if (!CanSpawnFood())
            return null;

        var foodGameObject = PoolManager.Instance.GetObject(foodPrefab);
        foodGameObject.transform.position = new Vector3(request.position.x, request.position.y, request.position.z);
        foodGameObject.transform.rotation = Quaternion.identity;
        var foodComponent = foodGameObject.GetComponent<Food>();
        foodComponent.Initialize(request.targetNutrition);
        foods.Add(foodComponent);
        Statistics.Instance?.RecordFoodSpawned();
        return foodComponent;
    }

    Vector3 GetRandomGroundPosition(Bounds bounds)
    {
        float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
        float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
        float halfHeight = GetHalfHeight(foodPrefab);
        float y = GroundSnapUtils.TryGetGroundY(x, z, out float groundY, halfHeight) ? groundY : halfHeight;
        return new Vector3(x, y, z);
    }

    private float GetHalfHeight(GameObject obj)
    {
        Renderer rendererComponent = obj.GetComponentInChildren<Renderer>();
        if (rendererComponent != null)
        {
            return rendererComponent.bounds.extents.y;
        }

        Collider colliderComponent = obj.GetComponentInChildren<Collider>();
        if (colliderComponent != null)
        {
            return colliderComponent.bounds.extents.y;
        }

        return 0.5f;
    }

    public void RemoveFromList(Food food)
    {
        foods.Remove(food);
    }

    public bool CanSpawnFood()
    {
        return GetRemainingFoodSlots() > 0;
    }

    public int GetRemainingFoodSlots()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null)
            return int.MaxValue;

        int maxFoodCount = Mathf.Max(0, config.maxFoodCount);
        if (maxFoodCount <= 0)
            return int.MaxValue;

        return Mathf.Max(0, maxFoodCount - GetActiveFoodCount());
    }

    public int GetActiveFoodCount()
    {
        return CountActiveFoods(foods);
    }

    private static int CountActiveFoods(List<Food> foodList)
    {
        if (foodList == null)
            return 0;

        int count = 0;
        for (int i = 0; i < foodList.Count; i++)
        {
            Food food = foodList[i];
            if (food != null &&
                food.gameObject.activeInHierarchy &&
                !food.IsDespawnQueued)
            {
                count++;
            }
        }

        return count;
    }

    IEnumerator SpawnFoodAtRate()
    {
        while (true)
        {
            GameConfig config = GameConfig.Instance;
            float spawnInterval = config != null ? Mathf.Max(0.01f, config.foodSpawnInterval) : 5f;
            int spawnBatchSize = config != null ? Mathf.Max(0, config.foodSpawnBatchSize) : 5;

            yield return new WaitForSeconds(spawnInterval);
            for (int i = 0; i < spawnBatchSize; i++)
            {
                if (!CanSpawnFood())
                    break;

                RequestSpawnFood();
            }
        }
    }
}
