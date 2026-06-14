using Unity.Entities;
using UnityEngine;

/// <summary>
/// Drives herbivore flee from the closest threatening predator sensed inside the Burst
/// spatial-hash observation job (CreatureObservationResultData.closestThreatInstanceId),
/// replacing the per-herbivore O(H x P) predator scan that used to live in
/// HerbivoreBehaviour. This is a managed main-thread loop: it resolves the managed
/// herbivore and threat via the bridge and delegates the actual steering/sprint/move to
/// HerbivoreBehaviour.UpdateEcsFlee, which calls MovementManager.MoveTowards immediately.
/// Ordered after ECSMovementExecutionSystem so the immediate flee write wins over any
/// queued action movement, preserving the legacy "flee overrides action" behavior.
/// Captured herbivores are skipped (capture stays Mono until Phase 5).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSMovementExecutionSystem))]
public partial class ECSFleeSystem : SystemBase
{
    private EntityQuery fleeQuery;

    protected override void OnCreate()
    {
        fleeQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
            ComponentType.ReadOnly<CreatureObservationResultData>(),
            ComponentType.ReadOnly<HerbivoreTag>());

        RequireForUpdate(fleeQuery);
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null)
            return;

        foreach (var (identityRO, observationRO)
                 in SystemAPI.Query<RefRO<CreatureIdentity>, RefRO<CreatureObservationResultData>>()
                     .WithAll<HerbivoreTag>())
        {
            CreatureIdentity identity = identityRO.ValueRO;

            if (!ECSMirrorBridge.TryGetCreatureByInstanceId(identity.gameObjectInstanceId, out BaseCreatureBehaviour creature) ||
                creature == null ||
                creature.IsDespawnQueued)
                continue;

            if (!(creature is HerbivoreBehaviour herbivore) || herbivore.IsCaptured)
                continue;   // captured herbivores do not flee (capture stays Mono until Phase 5)

            int threatId = observationRO.ValueRO.closestThreatInstanceId;
            BaseCreatureBehaviour threat = null;
            if (threatId != 0)
                ECSMirrorBridge.TryGetCreatureByInstanceId(threatId, out threat);

            herbivore.UpdateEcsFlee(threat, config);
        }
    }
}
