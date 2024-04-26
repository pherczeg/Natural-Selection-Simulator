using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

public class CreatureSpawner : MonoBehaviour
{
    public static CreatureSpawner Instance { get; private set; }

    public GameObject herbivorPrefab;
    public GameObject predatorPrefab;
    public int numberOfCreatures = 5;
    public int numberOfPredators = 5;
    private int poolSizeOfCreatures = 100;
    public List<BaseCreatureBehaviour> herbivorCreatures;
    public List<BaseCreatureBehaviour> predatorCreatures;
    void Start()
    {
        herbivorCreatures = new List<BaseCreatureBehaviour>();
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            InitializeSpawner();
        }
    }
    void InitializeSpawner()
    {
        if (GroundManager.Instance != null)
        {
            Bounds bounds = GroundManager.Instance.GroundBounds;
            PoolManager.Instance.CreatePool(herbivorPrefab, poolSizeOfCreatures);
            SpawnCreatures(bounds);
        }
        else
        {
            Debug.LogError("GroundManager instance not found.");
        }
    }

    void SpawnCreatures(Bounds bounds)
    {
        for (int i = 0; i < numberOfCreatures; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(bounds);
            BaseCreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition, herbivorPrefab);
            var weight = Random.Range(2f, 10f);
            var moveSpeed = Random.Range(7f, 20f);
            var senseRange = Random.Range(10f, 30f);
            newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange);
        }
        for (int i = 0; i < numberOfPredators; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(bounds);
            BaseCreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition, predatorPrefab);
            var weight = Random.Range(5f, 12f);
            var moveSpeed = Random.Range(10f, 25f);
            var senseRange = Random.Range(30f, 40f);
            newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange);
        }
    }
    public bool RemoveFromList(BaseCreatureBehaviour creature)
    {
        return herbivorCreatures.Remove(creature);
    }
    Vector3 GetSpawnPosition(Bounds bounds)
    {
        Vector3 spawnPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            1f,
            Random.Range(bounds.min.z, bounds.max.z)
        );

        while (IsPlaceOccupied(spawnPosition))
        {
            spawnPosition = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                1f,
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }

        return spawnPosition;
    }

    public BaseCreatureBehaviour SpawnCreature(Vector3 spawnPosition, GameObject prefab)
    {
        GameObject newCreature = PoolManager.Instance.GetObject(prefab);
        newCreature.transform.position = spawnPosition;
        var creatureBehaviour = newCreature.GetComponent<BaseCreatureBehaviour>();
        // ide lehet érdemesebb lenne a component alapján eldönteni, hogy melyik listába kerüljön
        if (prefab == herbivorPrefab)
        {
            herbivorCreatures.Add(creatureBehaviour);
        }
        else if (prefab == predatorPrefab)
        {
            predatorCreatures.Add(creatureBehaviour);
        }
        return creatureBehaviour;
    }
    bool IsPlaceOccupied(Vector3 position)
    {
        float checkRadius = .5f;
        Collider[] colliders = Physics.OverlapSphere(position, checkRadius);
        foreach (var collider in colliders)
        {
            if (!collider.isTrigger && collider.gameObject != gameObject)
            {
                return true;
            }
        }
        return false;
    }
}
