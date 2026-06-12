using UnityEngine;

/// <summary>
/// Sanitized per-creature sprint gene profile. <see cref="Create"/> mirrors the
/// clamping historically done in MovementManager.SetSprintProfile, so a profile
/// built from raw gene values always satisfies the calculator's assumptions
/// (duration/cooldown >= 0, sprintFactor >= 1, cooldownSpeedFactor in [0, 1]).
/// </summary>
public struct SprintProfileParameters
{
    public float duration;
    public float sprintFactor;
    public float cooldownDuration;
    public float cooldownSpeedFactor;

    /// <summary>Profile with no sprint capability and neutral multipliers (the pre-SetSprintProfile state).</summary>
    public static SprintProfileParameters Default => new SprintProfileParameters
    {
        duration = 0f,
        sprintFactor = 1f,
        cooldownDuration = 0f,
        cooldownSpeedFactor = 1f
    };

    public static SprintProfileParameters Create(float duration, float sprintFactor, float cooldownDuration, float cooldownSpeedFactor)
    {
        return new SprintProfileParameters
        {
            duration = Mathf.Max(0f, duration),
            sprintFactor = Mathf.Max(1f, sprintFactor),
            cooldownDuration = Mathf.Max(0f, cooldownDuration),
            cooldownSpeedFactor = Mathf.Clamp(cooldownSpeedFactor, 0f, 1f)
        };
    }
}

/// <summary>
/// Mutable sprint / temporary-effect timer state for one creature. All timers in
/// seconds; a value of 0 means inactive. temporaryMultiplier is 1 when no debuff
/// is active.
/// </summary>
public struct SprintEffectsState
{
    public float sprintRemaining;
    public float sprintCooldownRemaining;
    public float temporaryMultiplier;
    public float temporaryMultiplierRemaining;

    public static SprintEffectsState Default => new SprintEffectsState
    {
        sprintRemaining = 0f,
        sprintCooldownRemaining = 0f,
        temporaryMultiplier = 1f,
        temporaryMultiplierRemaining = 0f
    };
}

/// <summary>
/// Pure sprint/temporary-effect timer state machine shared by MovementManager
/// (legacy per-creature path) and future ECS jobs. Must stay behaviorally identical
/// to the historical MovementManager implementation, quirks included.
/// </summary>
public static class SprintEffectsCalculator
{
    /// <summary>
    /// Advances all timers by deltaTime. Returns true when a phase transition
    /// happened and the current speed must be recalculated.
    /// Legacy ordering quirk preserved on purpose: the cooldown block runs after
    /// the sprint block, so on the tick where the sprint expires the freshly
    /// started cooldown is immediately reduced by the same deltaTime (a large
    /// enough deltaTime clears both the sprint and the entire cooldown in one
    /// call). Timer overshoot is otherwise discarded, not carried over.
    /// </summary>
    public static bool Tick(ref SprintEffectsState state, float deltaTime, in SprintProfileParameters profile)
    {
        bool requiresRecalc = false;

        if (state.temporaryMultiplierRemaining > 0f)
        {
            state.temporaryMultiplierRemaining -= deltaTime;
            if (state.temporaryMultiplierRemaining <= 0f)
            {
                state.temporaryMultiplier = 1f;
                state.temporaryMultiplierRemaining = 0f;
                requiresRecalc = true;
            }
        }

        if (state.sprintRemaining > 0f)
        {
            state.sprintRemaining -= deltaTime;
            if (state.sprintRemaining <= 0f)
            {
                state.sprintRemaining = 0f;
                if (profile.cooldownDuration > 0f)
                {
                    state.sprintCooldownRemaining = profile.cooldownDuration;
                }
                requiresRecalc = true;
            }
        }

        if (state.sprintCooldownRemaining > 0f)
        {
            state.sprintCooldownRemaining -= deltaTime;
            if (state.sprintCooldownRemaining <= 0f)
            {
                state.sprintCooldownRemaining = 0f;
                requiresRecalc = true;
            }
        }

        return requiresRecalc;
    }

    /// <summary>
    /// Starts a sprint when the profile allows sprinting (duration > 0 and
    /// factor > 1) and neither a sprint nor a cooldown is in progress.
    /// Returns true when started; callers must then recalculate speed.
    /// </summary>
    public static bool TryStartSprint(ref SprintEffectsState state, in SprintProfileParameters profile)
    {
        if (profile.duration <= 0f || profile.sprintFactor <= 1f)
            return false;

        if (state.sprintRemaining > 0f || state.sprintCooldownRemaining > 0f)
            return false;

        state.sprintRemaining = profile.duration;
        return true;
    }

    /// <summary>
    /// Applies a temporary speed debuff (multiplier clamped to [0, 1]).
    /// Legacy quirk preserved: with duration clamped to 0 the multiplier is
    /// stored but Tick never expires it, so the debuff sticks until the next
    /// ApplyTemporaryMultiplier call.
    /// </summary>
    public static void ApplyTemporaryMultiplier(ref SprintEffectsState state, float multiplier, float duration)
    {
        state.temporaryMultiplier = Mathf.Clamp(multiplier, 0f, 1f);
        state.temporaryMultiplierRemaining = Mathf.Max(0f, duration);
    }

    /// <summary>
    /// Current speed product. The sprint factor applies only while sprinting; the
    /// cooldown speed factor applies only when not sprinting AND a cooldown is
    /// active (sprint wins if both timers were ever > 0 simultaneously).
    /// </summary>
    public static float ComputeCurrentSpeed(in SprintEffectsState state, float baseSpeed, float ageMultiplier, in SprintProfileParameters profile)
    {
        float sprintMultiplier = state.sprintRemaining > 0f ? profile.sprintFactor : 1f;
        float cooldownMultiplier = state.sprintRemaining <= 0f && state.sprintCooldownRemaining > 0f
            ? profile.cooldownSpeedFactor
            : 1f;
        return Mathf.Max(0f, baseSpeed * ageMultiplier * state.temporaryMultiplier * sprintMultiplier * cooldownMultiplier);
    }
}
