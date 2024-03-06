using Unity.VisualScripting.FullSerializer;
using Unity.VisualScripting;
using UnityEngine;

public class CreatureSpawner : MonoBehaviour
{
    public GameObject creaturePrefab;
    public int numberOfCreatures = 5;
    public Vector2 spawnRange = new Vector2(10f, 10f);
    GameObject ground;
    void Awake()
    {
        ground = GameObject.FindGameObjectWithTag("Ground");
        if (ground != null)
        {
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                Bounds bounds = groundRenderer.bounds;

                for (int i = 0; i < numberOfCreatures; i++)
                {
                    Vector3 spawnPosition = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        1f, // Ez az érték függ a lény és a talaj magasságától
                        Random.Range(bounds.min.z, bounds.max.z)
                    );

                    // Ensure the place is not occupied.
                    while (IsPlaceOccupied(spawnPosition))
                    {
                        spawnPosition = new Vector3(
                            Random.Range(bounds.min.x, bounds.max.x),
                            1f, // Ez az érték függ a lény és a talaj magasságától
                            Random.Range(bounds.min.z, bounds.max.z)
                        );
                    }


                    CreatureBehaviour newCreatureBehaviour = SpawnCreature(spawnPosition);
                    var weight = Random.Range(2f, 10.0f);
                    var moveSpeed = Random.Range(1f, 3f);
                    var senseRange = Random.Range(10f, 30f);
                    newCreatureBehaviour.Initialize(moveSpeed, weight, senseRange );


                }
            }
        }
    }

    public CreatureBehaviour SpawnCreature(Vector3 spawnPosition)
    {
        GameObject newCreature = Instantiate(creaturePrefab, spawnPosition, Quaternion.identity);
        var newCreatureBehaviour = newCreature.GetComponent<CreatureBehaviour>();
        return newCreatureBehaviour;
    }

    bool IsPlaceOccupied(Vector3 position)
    {
        float checkRadius = 1.5f;
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
}
