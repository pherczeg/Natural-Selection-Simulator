using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Drains <see cref="SpawnFoodRequest"/> entities created by the bridge and turns each into a
/// pooled food GameObject via <see cref="FoodSpawner"/>. Replaces the bridge's former
/// ProcessSpawnFoodRequests, eliminating the per-frame CreateEntityQuery(...).ToEntityArray()
/// allocation by using SystemAPI.Query (codegen-cached).
///
/// This system NEVER creates or destroys a food entity. The bridge remains the single
/// GameObject&lt;-&gt;entity linker and creates the food entity on its next LateUpdate sync.
/// Here we only make the managed GameObject and destroy the request entity via the
/// EntityCommandBuffer.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ECSFoodSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<SpawnFoodRequest>();
    }

    protected override void OnUpdate()
    {
        if (FoodSpawner.Instance == null)
            return;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (requestRO, entity) in SystemAPI.Query<RefRO<SpawnFoodRequest>>().WithEntityAccess())
        {
            FoodSpawner.Instance.SpawnFoodFromRequest(requestRO.ValueRO);
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
