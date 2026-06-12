using System.Collections.Generic;
using UnityEngine;

public class CreatureSpawner : MonoBehaviour
{
    private const float GroundRaycastOriginHeight = 500f;
    private const float GroundRaycastDistance = 1000f;
    private int cachedPopulationFrame = -1;
    private int cachedActiveHerbivoreCount;
    private int cachedActivePredatorCount;

    public static CreatureSpawner Instance { get; private set; }

    public GameObject herbivorPrefab;
    public GameObject predatorPrefab;
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
        var config = GameConfig.Instance;
        if (config == null)
        {
            Debug.LogError("GameConfig instance not found.");
            return;
        }

        if (GroundManager.Instance != null)
        {
            Bounds bounds = GroundManager.Instance.GroundBounds;
            int creaturePoolSize = Mathf.Max(1, config.creaturePoolSize);
            PoolManager.Instance.CreatePool(herbivorPrefab, creaturePoolSize);
            PoolManager.Instance.CreatePool(predatorPrefab, creaturePoolSize);
            SpawnCreatures(bounds, config);
        }
        else
        {
            Debug.LogError("GroundManager instance not found.");
        }
    }

    void SpawnCreatures(Bounds bounds, GameConfig config)
    {
        int initialHerbivoreCount = Mathf.Max(0, config.initialHerbivoreCount);
        int initialPredatorCount = Mathf.Max(0, config.initialPredatorCount);

        for (int i = 0; i < initialHerbivoreCount; i++)
        {
            if (SpawnRandomCreature(false, GetInitialSex(i)) == null)
                break;
        }
        for (int i = 0; i < initialPredatorCount; i++)
        {
            if (SpawnRandomCreature(true, GetInitialSex(i)) == null)
                break;
        }
    }

    /// <summary>
    /// Spawns one creature with traits randomized from the config ranges, exactly like
    /// the initial population. Respects population caps (returns null when full).
    /// Also used by SimulationBenchmark to spawn up to a target population.
    /// </summary>
    public BaseCreatureBehaviour SpawnRandomCreature(bool isPredator, CreatureSex sex)
    {
        var config = GameConfig.Instance;
        if (config == null || GroundManager.Instance == null)
            return null;

        Bounds bounds = GroundManager.Instance.GroundBounds;
        GameObject prefab = isPredator ? predatorPrefab : herbivorPrefab;
        Vector3 spawnPosition = GetSpawnPosition(bounds, prefab);
        BaseCreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition, prefab);
        if (newCreatureBehaviour == null)
            return null;

        newCreatureBehaviour.SetSex(sex);
        if (isPredator)
        {
            var weight = Random.Range(config.predatorWeightMin, config.predatorWeightMax);
            var moveSpeed = Random.Range(config.predatorSpeedMin, config.predatorSpeedMax);
            var sprintDuration = Random.Range(config.predatorSprintDurationMin, config.predatorSprintDurationMax);
            var sprintFactor = Random.Range(config.predatorSprintFactorMin, config.predatorSprintFactorMax);
            var sprintCooldown = Random.Range(config.predatorSprintCooldownMin, config.predatorSprintCooldownMax);
            var sprintCooldownSpeedFactor = Random.Range(config.predatorSprintCooldownSpeedFactorMin, config.predatorSprintCooldownSpeedFactorMax);
            var senseRange = Random.Range(config.predatorSenseMin, config.predatorSenseMax);
            var strength = Random.Range(config.predatorStrengthMin, config.predatorStrengthMax);
            newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange);
            newCreatureBehaviour.RandomizeUtilityBehaviorProfile(config);
            newCreatureBehaviour.MovementManager?.SetSprintProfile(sprintDuration, sprintFactor, sprintCooldown, sprintCooldownSpeedFactor);
            newCreatureBehaviour.AgeManager.SetInitialAge(Random.Range(0f, config.oldAgeStartAge));
            if (newCreatureBehaviour is PredatorBehaviour predator)
            {
                predator.SetStrength(strength);
                newCreatureBehaviour.ReproductionManager?.SetDesirability(strength);
            }
        }
        else
        {
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
            newCreatureBehaviour.RandomizeUtilityBehaviorProfile(config);
            newCreatureBehaviour.MovementManager?.SetSprintProfile(sprintDuration, sprintFactor, sprintCooldown, sprintCooldownSpeedFactor);
            newCreatureBehaviour.AgeManager.SetInitialAge(Random.Range(0f, config.oldAgeStartAge));
            newCreatureBehaviour.ReproductionManager?.SetDesirability(desirability);
            if (newCreatureBehaviour is HerbivoreBehaviour herbivore)
            {
                herbivore.SetAgility(agility);
                herbivore.SetSocialStrategy(Random.value < 0.5f ? HerbivoreSocialStrategy.Hawk : HerbivoreSocialStrategy.Dove);
                //  herbivore.SetSocialStrategy(HerbivoreSocialStrategy.Hawk);
            }
        }

        return newCreatureBehaviour;
    }
    public bool RemoveFromList(BaseCreatureBehaviour creature)
    {
        if (herbivorCreatures != null && herbivorCreatures.Remove(creature))
        {
            InvalidatePopulationCache();
            return true;
        }

        if (predatorCreatures != null && predatorCreatures.Remove(creature))
        {
            InvalidatePopulationCache();
            return true;
        }

        return false;
    }

    public bool CanSpawnCreature()
    {
        return CanSpawnCreature(false) || CanSpawnCreature(true);
    }

    public bool CanSpawnCreature(bool isPredator)
    {
        return GetRemainingCreatureSlots(isPredator) > 0;
    }

    public int GetRemainingCreatureSlots()
    {
        int herbivoreSlots = GetRemainingCreatureSlots(false);
        int predatorSlots = GetRemainingCreatureSlots(true);

        if (herbivoreSlots == int.MaxValue || predatorSlots == int.MaxValue)
            return int.MaxValue;

        long combinedSlots = (long)herbivoreSlots + predatorSlots;
        return combinedSlots > int.MaxValue
            ? int.MaxValue
            : (int)combinedSlots;
    }

    public int GetRemainingCreatureSlots(bool isPredator)
    {
        GameConfig config = GameConfig.Instance;
        if (config == null)
            return int.MaxValue;

        int maxCreatureCount = config.GetMaxCreatureCountForSpecies(isPredator);
        if (maxCreatureCount <= 0)
            return int.MaxValue;

        int activeCount = GetActiveSpeciesCountCached(isPredator);
        return Mathf.Max(0, maxCreatureCount - activeCount);
    }

    public float GetSpeciesPopulationUsage01(bool isPredator)
    {
        GameConfig config = GameConfig.Instance;
        if (config == null)
            return 0f;

        int maxCreatureCount = config.GetMaxCreatureCountForSpecies(isPredator);
        if (maxCreatureCount <= 0)
            return 0f;

        int activeCount = GetActiveSpeciesCountCached(isPredator);
        return Mathf.Clamp01(activeCount / (float)maxCreatureCount);
    }

    public float GetSpeciesMatingIntentMultiplier(bool isPredator, float falloffStartUsage = 0.9f)
    {
        float usage = GetSpeciesPopulationUsage01(isPredator);
        float startUsage = Mathf.Clamp(falloffStartUsage, 0f, 0.9999f);
        if (usage <= startUsage)
            return 1f;

        return Mathf.Clamp01((1f - usage) / (1f - startUsage));
    }

    public int GetActiveCreatureCount()
    {
        RefreshPopulationCacheIfNeeded();
        return cachedActiveHerbivoreCount + cachedActivePredatorCount;
    }

    private int GetActiveSpeciesCountCached(bool isPredator)
    {
        RefreshPopulationCacheIfNeeded();
        return isPredator
            ? cachedActivePredatorCount
            : cachedActiveHerbivoreCount;
    }

    private void RefreshPopulationCacheIfNeeded()
    {
        int frame = Time.frameCount;
        if (cachedPopulationFrame == frame)
            return;

        cachedActiveHerbivoreCount = CountActiveCreatures(herbivorCreatures);
        cachedActivePredatorCount = CountActiveCreatures(predatorCreatures);
        cachedPopulationFrame = frame;
    }

    private void InvalidatePopulationCache()
    {
        cachedPopulationFrame = -1;
    }

    private static int CountActiveCreatures(List<BaseCreatureBehaviour> creatures)
    {
        if (creatures == null)
            return 0;

        int count = 0;
        for (int i = 0; i < creatures.Count; i++)
        {
            BaseCreatureBehaviour creature = creatures[i];
            if (creature != null &&
                creature.gameObject.activeInHierarchy &&
                !creature.IsDespawnQueued)
            {
                count++;
            }
        }

        return count;
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
        if (herbivorCreatures == null)
        {
            herbivorCreatures = new List<BaseCreatureBehaviour>();
        }

        if (predatorCreatures == null)
        {
            predatorCreatures = new List<BaseCreatureBehaviour>();
        }

        bool isPredatorPrefab = prefab == predatorPrefab;
        bool isHerbivorePrefab = prefab == herbivorPrefab;

        if (isPredatorPrefab)
        {
            if (!CanSpawnCreature(true))
                return null;
        }
        else if (isHerbivorePrefab)
        {
            if (!CanSpawnCreature(false))
                return null;
        }
        else if (!CanSpawnCreature())
        {
            return null;
        }

        GameObject newCreature = PoolManager.Instance.GetObject(prefab);
        newCreature.transform.position = spawnPosition;
        var creatureBehaviour = newCreature.GetComponent<BaseCreatureBehaviour>();
        if (prefab == herbivorPrefab)
        {
            herbivorCreatures.Add(creatureBehaviour);
            InvalidatePopulationCache();
        }
        else if (prefab == predatorPrefab)
        {
            predatorCreatures.Add(creatureBehaviour);
            InvalidatePopulationCache();
        }
        Statistics.Instance?.RecordCreatureSpawned(creatureBehaviour);
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
