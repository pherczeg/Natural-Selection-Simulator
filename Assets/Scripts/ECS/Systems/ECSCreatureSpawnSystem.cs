using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Drains <see cref="SpawnCreatureRequest"/> entities created by the bridge and turns each
/// into a pooled creature GameObject via <see cref="CreatureSpawner"/>. Replaces the bridge's
/// former ProcessSpawnCreatureRequests/ProcessSpawnCreatureRequest, eliminating the per-frame
/// CreateEntityQuery(...).ToEntityArray() allocation by using SystemAPI.Query (codegen-cached).
///
/// This system NEVER creates or destroys a creature entity. The bridge remains the single
/// GameObject&lt;-&gt;entity linker; it creates and populates the creature entity (including
/// Genome/RandomState) on its next LateUpdate sync. Here we only make the managed GameObject
/// and destroy the request entity (always, even on early-continue) via the EntityCommandBuffer.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ECSCreatureSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<SpawnCreatureRequest>();
    }

    protected override void OnUpdate()
    {
        if (CreatureSpawner.Instance == null)
            return;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (requestRO, entity) in SystemAPI.Query<RefRO<SpawnCreatureRequest>>().WithEntityAccess())
        {
            ProcessSpawnCreatureRequest(requestRO.ValueRO);
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private static void ProcessSpawnCreatureRequest(in SpawnCreatureRequest request)
    {
        CreatureSpawner spawner = CreatureSpawner.Instance;
        GameObject prefab = GetCreaturePrefab(spawner, request.creatureKind);
        if (prefab == null)
            return;

        BaseCreatureBehaviour creature = spawner.SpawnCreature(
            new Vector3(request.position.x, request.position.y, request.position.z),
            prefab);
        if (creature == null)
            return;

        CreatureSex sex = request.sex == (int)CreatureSex.Male
            ? CreatureSex.Male
            : CreatureSex.Female;
        creature.SetSex(sex);
        creature.Initialize(request.moveSpeed, request.weight, request.senseRadius);
        creature.MovementManager?.SetSprintProfile(
            request.sprintDuration,
            request.sprintFactor,
            request.sprintCooldown,
            request.sprintCooldownSpeedFactor);
        if (request.initialAge > 0f)
        {
            creature.AgeManager?.SetInitialAge(request.initialAge);
        }

        creature.ReproductionManager?.SetDesirability(request.desirability);
        creature.SetUtilityBehaviorProfile(new CreatureUtilityBehaviorData
        {
            keepCurrentStateWeight = request.utilityKeepCurrentStateWeight,
            foodActionWeight = request.utilityFoodActionWeight,
            searchMateWeight = request.utilitySearchMateWeight,
            wanderWeight = request.utilityWanderWeight
        });
        if (creature is HerbivoreBehaviour herbivore)
        {
            herbivore.SetAgility(request.agility);
            herbivore.SetSocialStrategy((HerbivoreSocialStrategy)request.herbivoreSocialStrategy);
        }
        else if (creature is PredatorBehaviour predator)
        {
            predator.SetStrength(request.strength);
        }
    }

    private static GameObject GetCreaturePrefab(CreatureSpawner spawner, int creatureKind)
    {
        if (creatureKind == ECSCreatureKind.Predator)
            return spawner.predatorPrefab;

        if (creatureKind == ECSCreatureKind.Herbivore)
            return spawner.herbivorPrefab;

        return null;
    }
}
