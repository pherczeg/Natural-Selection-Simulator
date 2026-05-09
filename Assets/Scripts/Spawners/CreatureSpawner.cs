using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

public class CreatureSpawner : MonoBehaviour
{
    private const float GroundRaycastOriginHeight = 500f;
    private const float GroundRaycastDistance = 1000f;

    public static CreatureSpawner Instance { get; private set; }

    public GameObject herbivorPrefab;
    public GameObject predatorPrefab;
    public int numberOfHerbivores = 5;
    public int numberOfPredators = 5;
    private int poolSizeOfCreatures = 100;
    public List<BaseCreatureBehaviour> herbivorCreatures;
    public List<BaseCreatureBehaviour> predatorCreatures;
    void Start()
    {
        herbivorCreatures = new List<BaseCreatureBehaviour>();
        predatorCreatures = new List<BaseCreatureBehaviour>();
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
            PoolManager.Instance.CreatePool(predatorPrefab, poolSizeOfCreatures);
            SpawnCreatures(bounds);
        }
        else
        {
            Debug.LogError("GroundManager instance not found.");
        }
    }

    void SpawnCreatures(Bounds bounds)
    {
        var config = GameConfig.Instance;
        if (config == null)
        {
            Debug.LogError("GameConfig instance not found.");
            return;
        }

        for (int i = 0; i < numberOfHerbivores; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(bounds, herbivorPrefab);
            BaseCreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition, herbivorPrefab);
            newCreatureBehaviour.SetSex(GetInitialSex(i));
            var weight = Random.Range(config.herbivoreWeightMin, config.herbivoreWeightMax);
            var moveSpeed = Random.Range(config.herbivoreSpeedMin, config.herbivoreSpeedMax);
            var sprintDuration = Random.Range(config.herbivoreSprintDurationMin, config.herbivoreSprintDurationMax);
            var sprintFactor = Random.Range(config.herbivoreSprintFactorMin, config.herbivoreSprintFactorMax);
            var sprintCooldown = Random.Range(config.herbivoreSprintCooldownMin, config.herbivoreSprintCooldownMax);
            var sprintCooldownSpeedFactor = Random.Range(config.herbivoreSprintCooldownSpeedFactorMin, config.herbivoreSprintCooldownSpeedFactorMax);
            var senseRange = Random.Range(config.herbivoreSenseMin, config.herbivoreSenseMax);
            var desirability = Random.Range(config.herbivoreDesirabilityMin, config.herbivoreDesirabilityMax);
            var agility = Random.Range(config.herbivoreAgilityMin, config.herbivoreAgilityMax);
            newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange);
            newCreatureBehaviour.MovementManager?.SetSprintProfile(sprintDuration, sprintFactor, sprintCooldown, sprintCooldownSpeedFactor);
            newCreatureBehaviour.AgeManager.SetInitialAge(Random.Range(0f, config.oldAgeStartAge));
            newCreatureBehaviour.ReproductionManager?.SetDesirability(desirability);
            if (newCreatureBehaviour is HerbivoreBehaviour herbivore)
            {
                herbivore.SetAgility(agility);
            }
        }
        for (int i = 0; i < numberOfPredators; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(bounds, predatorPrefab);
            BaseCreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition, predatorPrefab);
            newCreatureBehaviour.SetSex(GetInitialSex(i));
            var weight = Random.Range(config.predatorWeightMin, config.predatorWeightMax);
            var moveSpeed = Random.Range(config.predatorSpeedMin, config.predatorSpeedMax);
            var sprintDuration = Random.Range(config.predatorSprintDurationMin, config.predatorSprintDurationMax);
            var sprintFactor = Random.Range(config.predatorSprintFactorMin, config.predatorSprintFactorMax);
            var sprintCooldown = Random.Range(config.predatorSprintCooldownMin, config.predatorSprintCooldownMax);
            var sprintCooldownSpeedFactor = Random.Range(config.predatorSprintCooldownSpeedFactorMin, config.predatorSprintCooldownSpeedFactorMax);
            var senseRange = Random.Range(config.predatorSenseMin, config.predatorSenseMax);
            var strength = Random.Range(config.predatorStrengthMin, config.predatorStrengthMax);
            newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange);
            newCreatureBehaviour.MovementManager?.SetSprintProfile(sprintDuration, sprintFactor, sprintCooldown, sprintCooldownSpeedFactor);
            newCreatureBehaviour.AgeManager.SetInitialAge(Random.Range(0f, config.oldAgeStartAge));
            if (newCreatureBehaviour is PredatorBehaviour predator)
            {
                predator.SetStrength(strength);
                newCreatureBehaviour.ReproductionManager?.SetDesirability(strength);
            }
        }
    }
    public bool RemoveFromList(BaseCreatureBehaviour creature)
    {
        if (herbivorCreatures != null && herbivorCreatures.Remove(creature))
        {
            return true;
        }

        if (predatorCreatures != null && predatorCreatures.Remove(creature))
        {
            return true;
        }

        return false;
    }

    private CreatureSex GetInitialSex(int index)
    {
        return index % 2 == 0 ? CreatureSex.Female : CreatureSex.Male;
    }

    Vector3 GetSpawnPosition(Bounds bounds, GameObject prefab)
    {
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        Vector3 spawnPosition = new Vector3(x, GetGroundYForObject(x, z, prefab), z);
        int attempts = 0;

        while (IsPlaceOccupied(spawnPosition) && ++attempts < 100)
        {
            x = Random.Range(bounds.min.x, bounds.max.x);
            z = Random.Range(bounds.min.z, bounds.max.z);
            spawnPosition = new Vector3(x, GetGroundYForObject(x, z, prefab), z);
        }

        return spawnPosition;
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

    public BaseCreatureBehaviour SpawnCreature(Vector3 spawnPosition, GameObject prefab)
    {
        GameObject newCreature = PoolManager.Instance.GetObject(prefab);
        newCreature.transform.position = spawnPosition;
        var creatureBehaviour = newCreature.GetComponent<BaseCreatureBehaviour>();
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
            if (!collider.isTrigger
                && collider.gameObject != gameObject
                && !collider.CompareTag("Ground"))
                return true;
        }
        return false;
    }
}
