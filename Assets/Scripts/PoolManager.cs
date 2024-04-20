using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    public void CreatePool(GameObject prefab, int poolSize)
    {
        if (!pools.ContainsKey(prefab))
        {
            var queue = new Queue<GameObject>();
            for (int i = 0; i < poolSize; i++)
            {
                var obj = Instantiate(prefab);
                obj.SetActive(false);
                queue.Enqueue(obj);
            }
            pools[prefab] = queue;
        }
    }

    public GameObject GetObject(GameObject prefab)
    {
        if (pools.ContainsKey(prefab) && pools[prefab].Count > 0)
        {
            var obj = pools[prefab].Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            return Instantiate(prefab);
        }
    }

    public void ReturnObject(GameObject prefab, GameObject obj)
    {
        if (!pools.ContainsKey(prefab))
        {
            Debug.LogError($"Pool for prefab {prefab.name} does not exist.");
            return;
        }

        obj.SetActive(false);
        pools[prefab].Enqueue(obj);
    }
}
