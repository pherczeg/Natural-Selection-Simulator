using UnityEngine;

public class CreatureSpawner : MonoBehaviour
{
    public static CreatureSpawner Instance { get; private set; }

    public GameObject creaturePrefab;
    public int numberOfCreatures = 5;

    void Awake()
    {
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
            CreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition);
            var weight = Random.Range(2f, 10f);
            var moveSpeed = Random.Range(7f, 20f);
            var senseRange = Random.Range(10f, 30f);
            newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange);
        }
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

    public CreatureBehaviour SpawnCreature(Vector3 spawnPosition)
    {
        GameObject newCreature = Instantiate(creaturePrefab, spawnPosition, Quaternion.identity);
        return newCreature.GetComponent<CreatureBehaviour>();
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
