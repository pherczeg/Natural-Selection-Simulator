using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    private const float GroundRaycastOriginHeight = 500f;
    private const float GroundRaycastDistance = 1000f;

    public static FoodSpawner Instance { get; private set; }

    public GameObject foodPrefab;
    public int initialFoodCount = 20;
    public int poolSizeOfFood = 2000;
    public float spawnRate = 5f;
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

        StartCoroutine(SpawnFoodAtRate());
    }

    void SpawnFood()
    {
        if (ground == null) return;
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer == null) return;

        Bounds bounds = groundRenderer.bounds;
        Vector3 randomPosition = GetRandomGroundPosition(bounds);
        int attempts = 0;
        while (IsPlaceOccupied(randomPosition) && attempts++ < 100)
            randomPosition = GetRandomGroundPosition(bounds);

        var foodGameObject = PoolManager.Instance.GetObject(foodPrefab);
        foodGameObject.transform.position = randomPosition;
        foodGameObject.transform.rotation = Quaternion.identity;
        var foodComponent = foodGameObject.GetComponent<Food>();
        float targetNutrition = Random.Range(GameConfig.Instance.minNutrionValue, GameConfig.Instance.maxNutrionValue);
        foodComponent.Initialize(targetNutrition);
        foods.Add(foodComponent);
    }

    Vector3 GetRandomGroundPosition(Bounds bounds)
    {
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        float y = GetGroundYForObject(x, z, foodPrefab);
        return new Vector3(x, y, z);
    }

    private float GetGroundYForObject(float x, float z, GameObject obj)
    {
        float halfHeight = GetHalfHeight(obj);
        return GetGroundY(x, z, halfHeight, 0f);
    }

    private float GetGroundY(float x, float z, float surfaceOffset, float fallbackY)
    {
        Vector3 origin = new Vector3(x, GroundRaycastOriginHeight, z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, GroundRaycastDistance);

        float bestY = float.MinValue;
        bool found = false;

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Ground") && hit.point.y > bestY)
            {
                bestY = hit.point.y;
                found = true;
            }
        }

        return (found ? bestY : fallbackY) + surfaceOffset;
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

    bool IsPlaceOccupied(Vector3 position)
    {
        float checkRadius = 1.5f;
        Collider[] colliders = Physics.OverlapSphere(position, checkRadius);
        foreach (var collider in colliders)
        {
            if (!collider.isTrigger
                && collider.gameObject != gameObject
                && !collider.CompareTag("Ground"))
                return true;
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
