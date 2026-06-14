using Unity.Entities;

/// <summary>
/// Authoritative per-creature sprint / temporary-effect state for the ECS movement
/// pipeline (Phase 3 Increment 2). The entity owns the ticked sprint timers and the
/// resulting <see cref="currentMoveSpeed"/>; ECSMovementEffectsSystem advances the
/// timers every frame (wall-clock deltaTime) and recomputes the speed, reading the
/// age multiplier from the same entity's CreatureLifecycleData.speedAgeMultiplier.
///
/// The bridge ferries data each slow-sync interval: managed pending sprint/debuff
/// triggers (recorded by MovementManager) are pushed into the pending* fields here,
/// and <see cref="currentMoveSpeed"/> is read back into the manager so the execution
/// systems queue moves with the ticked speed.
///
/// baseMoveSpeed and the sprint-profile fields are written once at entity creation by
/// the bridge and stay constant afterward.
/// </summary>
public struct MovementState : IComponentData
{
    public float sprintRemaining;            // ticked
    public float sprintCooldownRemaining;    // ticked
    public float temporaryMultiplier;        // 1 = no debuff
    public float temporaryMultiplierRemaining;
    public float sprintDuration;             // profile (constant after init)
    public float sprintFactor;
    public float sprintCooldownDuration;
    public float sprintCooldownSpeedFactor;
    public float baseMoveSpeed;              // constant after init
    public float currentMoveSpeed;           // OUTPUT (read by bridge -> manager)
    public bool pendingSprintStart;          // set by bridge from manager; cleared by the system
    public float pendingTempMultiplier;      // <0 = none; >=0 = apply; cleared (-1) by the system
    public float pendingTempMultiplierDuration;
}
