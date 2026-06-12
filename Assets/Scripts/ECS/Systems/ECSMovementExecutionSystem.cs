using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

/// <summary>
/// Applies all movement intents queued into CreatureMovementBatch by the ECS execution
/// systems in one Burst-compiled IJobParallelForTransform, replacing the per-creature
/// main-thread MoveTowards calls when useEcsMovementExecution is enabled.
/// The job completes inside OnUpdate so positions are final before the bridge's
/// LateUpdate sync and before the next FixedUpdate (flee path).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSFoodSearchExecutionSystem))]
[UpdateAfter(typeof(ECSMateSearchExecutionSystem))]
[UpdateAfter(typeof(ECSWanderExecutionSystem))]
public partial class ECSMovementExecutionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        CreatureMovementBatch batch = CreatureMovementBatch.Instance;
        if (batch == null || batch.RegisteredCount == 0)
            return;

        // Drain pending intents even if the flag was just switched off so no queued
        // move is lost or applied with stale data later.
        if (!batch.HasPendingIntents)
            return;

        MovementBoundsParameters bounds = MovementBoundsParameters.FromGroundManager(GroundManager.Instance);

        JobHandle handle = new CreatureMoveStepJob
        {
            intents = batch.IntentsArray,
            bounds = bounds,
            // Matches the legacy MoveTowards step, which uses fixedDeltaTime even though
            // the execution systems tick once per rendered frame (pre-existing behavior).
            deltaTime = UnityEngine.Time.fixedDeltaTime
        }.Schedule(batch.Transforms);

        handle.Complete();
        batch.ClearIntents();
    }
}

[BurstCompile]
public struct CreatureMoveStepJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<CreatureMoveIntent> intents;
    public MovementBoundsParameters bounds;
    public float deltaTime;

    public void Execute(int index, TransformAccess transform)
    {
        CreatureMoveIntent intent = intents[index];
        if (!intent.hasIntent || !transform.isValid)
            return;

        CreatureMovementCalculator.ComputeStep(
            transform.position,
            intent.targetPosition,
            intent.moveSpeed,
            deltaTime,
            intent.halfHeight,
            in bounds,
            out float3 nextPosition,
            out bool hasRotation,
            out quaternion rotation);

        transform.position = nextPosition;
        if (hasRotation)
        {
            transform.rotation = rotation;
        }
    }
}
