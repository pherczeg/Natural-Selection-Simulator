using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Drains <see cref="DespawnCreatureRequest"/> and <see cref="DespawnFoodRequest"/> entities
/// created by the bridge and tears down the corresponding pooled GameObject via the managed
/// completion entry points. Replaces the bridge's former ProcessDespawnCreatureRequests/
/// ProcessDespawnFoodRequests, eliminating the per-frame CreateEntityQuery(...).ToEntityArray()
/// allocations by using SystemAPI.Query (codegen-cached).
///
/// This system NEVER destroys a creature or food entity directly. The bridge remains the single
/// GameObject&lt;-&gt;entity linker; CompleteDespawnFromBridge returns the GameObject to its pool
/// and the bridge unlinks/destroys the mirrored entity on its next LateUpdate sync. Here we only
/// destroy the request entity (always) via the EntityCommandBuffer.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ECSDespawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        // Run only when at least one despawn request exists (avoids an idle-path ECB allocation).
        RequireAnyForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<DespawnCreatureRequest>()),
            GetEntityQuery(ComponentType.ReadOnly<DespawnFoodRequest>()));
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        // Each loop is guarded by its own spawner so a missing one does not defer the other's
        // teardown; requests for an absent spawner are left to drain once it comes up.
        if (CreatureSpawner.Instance != null)
        {
            foreach (var (requestRO, entity) in SystemAPI.Query<RefRO<DespawnCreatureRequest>>().WithEntityAccess())
            {
                DespawnCreatureRequest request = requestRO.ValueRO;
                if (ECSMirrorBridge.TryGetCreatureByInstanceId(request.gameObjectInstanceId, out BaseCreatureBehaviour creature) &&
                    creature != null)
                {
                    creature.CompleteDespawnFromBridge((CreatureDeathReason)request.reason);
                }

                ecb.DestroyEntity(entity);
            }
        }

        if (FoodSpawner.Instance != null)
        {
            foreach (var (requestRO, entity) in SystemAPI.Query<RefRO<DespawnFoodRequest>>().WithEntityAccess())
            {
                DespawnFoodRequest request = requestRO.ValueRO;
                if (ECSMirrorBridge.TryGetFoodByInstanceId(request.gameObjectInstanceId, out Food food) &&
                    food != null)
                {
                    food.CompleteDespawnFromBridge((FoodDespawnReason)request.reason);
                }

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
