using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public struct CreatureMoveIntent
{
    public bool hasIntent;
    public float3 targetPosition;
    public float moveSpeed;
    public float halfHeight;
}

/// <summary>
/// Shared registry of creature transforms and per-frame movement intents used when
/// useEcsMovementExecution is enabled. The ECS execution systems queue intents instead
/// of moving creatures directly; ECSMovementExecutionSystem applies all of them in one
/// Burst IJobParallelForTransform. Lifetime is owned by ECSMirrorBridge.
/// All methods are main-thread only.
/// </summary>
public class CreatureMovementBatch
{
    private const int InitialCapacity = 256;

    private TransformAccessArray transforms;
    private NativeList<CreatureMoveIntent> intents;
    private readonly Dictionary<int, int> indexByInstanceId = new Dictionary<int, int>();
    private readonly List<int> instanceIdByIndex = new List<int>();
    private int pendingIntentCount;

    public static CreatureMovementBatch Instance { get; private set; }

    public static void EnsureCreated()
    {
        if (Instance == null)
        {
            Instance = new CreatureMovementBatch();
        }
    }

    public static void DisposeShared()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private CreatureMovementBatch()
    {
        transforms = new TransformAccessArray(InitialCapacity);
        intents = new NativeList<CreatureMoveIntent>(InitialCapacity, Allocator.Persistent);
    }

    public int RegisteredCount => instanceIdByIndex.Count;
    public bool HasPendingIntents => pendingIntentCount > 0;
    public TransformAccessArray Transforms => transforms;
    public NativeArray<CreatureMoveIntent> IntentsArray => intents.AsArray();

    public bool TryQueueMove(int instanceId, Transform transform, Vector3 targetPosition, float moveSpeed, float halfHeight)
    {
        if (!transforms.isCreated || transform == null)
            return false;

        if (!indexByInstanceId.TryGetValue(instanceId, out int index))
        {
            index = instanceIdByIndex.Count;
            indexByInstanceId.Add(instanceId, index);
            instanceIdByIndex.Add(instanceId);
            transforms.Add(transform);
            intents.Add(default);
        }

        if (!intents[index].hasIntent)
        {
            pendingIntentCount++;
        }

        intents[index] = new CreatureMoveIntent
        {
            hasIntent = true,
            targetPosition = targetPosition,
            moveSpeed = moveSpeed,
            halfHeight = halfHeight
        };
        return true;
    }

    public void Unregister(int instanceId)
    {
        if (!transforms.isCreated || !indexByInstanceId.TryGetValue(instanceId, out int index))
            return;

        if (intents[index].hasIntent)
        {
            pendingIntentCount--;
        }

        int lastIndex = instanceIdByIndex.Count - 1;
        int movedInstanceId = instanceIdByIndex[lastIndex];

        transforms.RemoveAtSwapBack(index);
        intents.RemoveAtSwapBack(index);
        instanceIdByIndex[index] = movedInstanceId;
        instanceIdByIndex.RemoveAt(lastIndex);
        indexByInstanceId.Remove(instanceId);

        if (index <= lastIndex - 1)
        {
            indexByInstanceId[movedInstanceId] = index;
        }
    }

    public void ClearIntents()
    {
        if (pendingIntentCount == 0)
            return;

        for (int i = 0; i < intents.Length; i++)
        {
            if (intents[i].hasIntent)
            {
                intents[i] = default;
            }
        }

        pendingIntentCount = 0;
    }

    public void Dispose()
    {
        if (transforms.isCreated)
        {
            transforms.Dispose();
        }

        if (intents.IsCreated)
        {
            intents.Dispose();
        }

        indexByInstanceId.Clear();
        instanceIdByIndex.Clear();
        pendingIntentCount = 0;
    }
}
