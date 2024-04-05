using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct FindClosestReproductiveCreature : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> creaturePositions;
    public NativeArray<int> closestCreatureIndices;

    public void Execute(int index)
    {
        float3 creaturePosition = creaturePositions[index];
        float closestDistance = float.MaxValue;
        int closestCreatureIndex = -1;

        for (int j = 0; j < creaturePositions.Length; j++)
        {
            if (j == index)
            {
                continue;
            }
            float distance = math.distance(creaturePosition, creaturePositions[j]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestCreatureIndex = j;
            }
        }
        closestCreatureIndices[index] = closestCreatureIndex;
    }
}
