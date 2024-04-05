using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct FindClosestFoodJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> creaturePositions;
    [ReadOnly] public NativeArray<float3> foodPositions;
    public NativeArray<int> closestFoodIndices;

    public void Execute(int index)
    {
        float3 creaturePosition = creaturePositions[index];
        float closestDistance = float.MaxValue;
        int closestFoodIndex = -1;

        for (int j = 0; j < foodPositions.Length; j++)
        {
            float distance = math.distance(creaturePosition, foodPositions[j]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestFoodIndex = j; 
            }
        }

        closestFoodIndices[index] = closestFoodIndex;
    }
}
