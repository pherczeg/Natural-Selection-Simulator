using Unity.Burst;
using Unity.Entities;

/// <summary>
/// Ticks every creature's <see cref="MovementState"/> sprint / temporary-effect timers
/// every frame (Phase 3 Increment 2), off the managed FixedUpdate path that used to live
/// in MovementManager.UpdateTemporaryEffects. The entity's MovementState is authoritative
/// for the sprint timers and the resulting currentMoveSpeed.
///
/// Each frame this applies any pending triggers ferried in by the bridge (a queued sprint
/// start, or a temporary speed debuff), advances the timers by the wall-clock frame delta
/// (matching the old per-FixedUpdate decay), reads the age multiplier from the same entity's
/// CreatureLifecycleData.speedAgeMultiplier, and writes currentMoveSpeed for the bridge to
/// read back into the manager. The pending fields are cleared once consumed.
///
/// The sprint/debuff math lives in the pure, Burst-compatible SprintEffectsCalculator,
/// shared with the legacy MovementManager path.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ECSMovementEffectsSystem : SystemBase
{
    private EntityQuery movementStateQuery;

    protected override void OnCreate()
    {
        movementStateQuery = GetEntityQuery(
            ComponentType.ReadWrite<MovementState>(),
            ComponentType.ReadOnly<CreatureLifecycleData>());

        RequireForUpdate(movementStateQuery);
    }

    protected override void OnUpdate()
    {
        float dt = (float)SystemAPI.Time.DeltaTime;

        new MovementEffectsJob
        {
            deltaTime = dt
        }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MovementEffectsJob : IJobEntity
{
    public float deltaTime;

    public void Execute(ref MovementState m, in CreatureLifecycleData life)
    {
        var profile = new SprintProfileParameters
        {
            duration = m.sprintDuration,
            sprintFactor = m.sprintFactor,
            cooldownDuration = m.sprintCooldownDuration,
            cooldownSpeedFactor = m.sprintCooldownSpeedFactor
        };

        var state = new SprintEffectsState
        {
            sprintRemaining = m.sprintRemaining,
            sprintCooldownRemaining = m.sprintCooldownRemaining,
            temporaryMultiplier = m.temporaryMultiplier,
            temporaryMultiplierRemaining = m.temporaryMultiplierRemaining
        };

        if (m.pendingSprintStart)
        {
            SprintEffectsCalculator.TryStartSprint(ref state, in profile);
            m.pendingSprintStart = false;
        }

        if (m.pendingTempMultiplier >= 0f)
        {
            SprintEffectsCalculator.ApplyTemporaryMultiplier(ref state, m.pendingTempMultiplier, m.pendingTempMultiplierDuration);
            m.pendingTempMultiplier = -1f;
            m.pendingTempMultiplierDuration = 0f;
        }

        SprintEffectsCalculator.Tick(ref state, deltaTime, in profile);

        m.sprintRemaining = state.sprintRemaining;
        m.sprintCooldownRemaining = state.sprintCooldownRemaining;
        m.temporaryMultiplier = state.temporaryMultiplier;
        m.temporaryMultiplierRemaining = state.temporaryMultiplierRemaining;

        // CreatureLifecycleData is only populated when useEcsCreatureLifecycle is ON; this system
        // always runs, so in the mono-lifecycle A/B mode the component stays at its default
        // (speedAgeMultiplier == 0), which would multiply currentMoveSpeed to 0 and freeze every
        // creature. Treat a non-positive multiplier as the neutral 1 (the ECS lifecycle calculator
        // always yields a value in [oldAgeMinSpeedMultiplier, 1] > 0, so this is a no-op in ECS mode
        // and only catches the unpopulated default / the one frame before the first sync).
        float ageMultiplier = life.speedAgeMultiplier > 0f ? life.speedAgeMultiplier : 1f;
        m.currentMoveSpeed = SprintEffectsCalculator.ComputeCurrentSpeed(in state, m.baseMoveSpeed, ageMultiplier, in profile);
    }
}
