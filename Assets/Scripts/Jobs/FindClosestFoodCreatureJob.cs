using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct FindClosestFoodCreatureJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> predatorPositions;
    [ReadOnly] public NativeArray<float3> herbivorPositions;
    public NativeArray<int> closestCreatureIndices;

    public void Execute(int index)
    {
        float3 creaturePosition = predatorPositions[index];
        float closestDistance = float.MaxValue;
        int closestCreatureIndex = -1;

        for (int j = 0; j < herbivorPositions.Length; j++)
        {
            float distance = math.distance(creaturePosition, herbivorPositions[j]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestCreatureIndex = j;
            }
        }
        closestCreatureIndices[index] = closestCreatureIndex;
    }
}
