using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public struct UpdateObservationJob : IJobParallelFor
{
    // Feltételezzük, hogy van egy módszerünk az azonosítók alapján hozzáférni a CreatureBehavior példányokhoz
    // Ez lehet egy statikus lista vagy egy kezelõ osztály
    [ReadOnly]
    public NativeArray<Vector3> positions;
    [ReadOnly]
    public NativeArray<Vector3> forwards;
    public int layerMask;
    public void Execute(int index)
    {
        Vector3 position = positions[index];
        Vector3 forward = forwards[index];
        // Itt megszerezheted a CreatureBehavior példányt az index alapján
        CreatureBehavior creature = CreatureManager.Instance.GetCreatureByIndex(index);
        creature.ScheduleObservationUpdate(position,forward,layerMask);
    }
}


public class ObservationsManager : MonoBehaviour
{
    private List<CreatureBehavior> creatures = new List<CreatureBehavior>();

    private float updateTimer = 0.1f;
    private float timer = 0f;
    private int layerMask;
    private void Awake()
    {
        layerMask = LayerMask.GetMask("Food", "Obstacle");
    }
    private void OnEnable()
    {
        CreatureBehavior.OnCreatureSpawned += RegisterCreature;
        CreatureBehavior.OnCreatureDestroyed += UnregisterCreature;
    }

    private void OnDisable()
    {
        CreatureBehavior.OnCreatureSpawned -= RegisterCreature;
        CreatureBehavior.OnCreatureDestroyed -= UnregisterCreature;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateTimer)
        {
            timer = 0f;
            UpdateAllObservations();
        }
    }

    public void RegisterCreature(CreatureBehavior creature)
    {
        if (!creatures.Contains(creature))
        {
            creatures.Add(creature);
        }
    }

    public void UnregisterCreature(CreatureBehavior creature)
    {
        if (creatures.Contains(creature))
        {
            creatures.Remove(creature);
        }
    }

    public void UpdateAllObservations()
    {
        // Létrehozunk egy NativeArray-t a pozíciók számára, amelyet a Job használni fog
        NativeArray<Vector3> positions = new NativeArray<Vector3>(creatures.Count, Allocator.TempJob);
        NativeArray<Vector3> forwards = new NativeArray<Vector3>(creatures.Count, Allocator.TempJob);
        // Töltjük fel a NativeArray-t a creature-ök pozícióival
        for (int i = 0; i < creatures.Count; i++)
        {
            positions[i] = creatures[i].transform.position;
            forwards[i] = creatures[i].transform.forward;
        }

        // Példányosítjuk és ütemezzük a Job-ot
        UpdateObservationJob observationJob = new UpdateObservationJob
        {
            positions = positions,
            layerMask = this.layerMask,
            forwards =forwards
        };

        JobHandle jobHandle = observationJob.Schedule(creatures.Count, 64);
        jobHandle.Complete();

        // Felszabadítjuk a NativeArray erõforrásait
        positions.Dispose();
    }

}
