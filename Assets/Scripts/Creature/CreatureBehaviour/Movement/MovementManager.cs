using Unity.Mathematics;
using UnityEngine;

public class MovementManager
{
    private float baseMoveSpeed;
    private float currentMoveSpeed;
    private float ageMultiplier = 1f;
    private SprintProfileParameters sprintProfile = SprintProfileParameters.Default;
    private bool pendingSprintStart;
    private float pendingTempMultiplier = -1f;
    private float pendingTempMultiplierDuration;
    public float MoveSpeed => currentMoveSpeed;
    public float BaseMoveSpeed => baseMoveSpeed;
    public float BaseSprintDuration => sprintProfile.duration;
    public float BaseSprintFactor => sprintProfile.sprintFactor;
    public float BaseSprintCooldown => sprintProfile.cooldownDuration;
    public float BaseSprintCooldownSpeedFactor => sprintProfile.cooldownSpeedFactor;
    public float HalfHeight => halfHeight;

    private BaseCreatureBehaviour creature;
    private Transform creatureTransform;
    private Bounds bounds;
    private bool hasBounds;
    private float halfHeight;
    public MovementManager(BaseCreatureBehaviour creature, float moveSpeed)
    {
        this.creature = creature;
        this.baseMoveSpeed = moveSpeed;
        this.currentMoveSpeed = moveSpeed;
        this.creatureTransform = creature.transform;
        this.halfHeight = GroundSnapUtils.GetHalfHeight(creature.gameObject);
        RefreshBounds();
    }

    public void SetSprintProfile(float duration, float sprintFactor, float cooldownDuration, float cooldownSpeedFactor)
    {
        // The profile is still captured into the entity's MovementState and read by
        // the Base* getters; sprint timer ticking now lives in ECSMovementEffectsSystem.
        sprintProfile = SprintProfileParameters.Create(duration, sprintFactor, cooldownDuration, cooldownSpeedFactor);
    }

    public bool TryStartSprint()
    {
        // Records a pending sprint-start request; the bridge ferries it to the entity's
        // MovementState and ECSMovementEffectsSystem applies + ticks it.
        pendingSprintStart = true;
        return true;
    }

    public bool ConsumePendingSprintStart()
    {
        bool r = pendingSprintStart;
        pendingSprintStart = false;
        return r;
    }

    public void SetAgeMultiplier(float multiplier)
    {
        // Stored for parity; the authoritative age multiplier is read by
        // ECSMovementEffectsSystem from CreatureLifecycleData.speedAgeMultiplier.
        ageMultiplier = Mathf.Max(0f, multiplier);
    }

    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        // Records a pending temporary multiplier request; consumed by the bridge.
        // Clamp to [0,1] (debuffs are defined in that range, and the calculator clamps too)
        // so a stored value can never collide with the <0 "none" sentinel.
        pendingTempMultiplier = math.clamp(multiplier, 0f, 1f);
        pendingTempMultiplierDuration = duration;
    }

    public bool TryConsumePendingTemporaryMultiplier(out float multiplier, out float duration)
    {
        if (pendingTempMultiplier < 0f)
        {
            multiplier = 0f;
            duration = 0f;
            return false;
        }

        multiplier = pendingTempMultiplier;
        duration = pendingTempMultiplierDuration;
        pendingTempMultiplier = -1f;
        pendingTempMultiplierDuration = 0f;
        return true;
    }

    public void SetCurrentMoveSpeedFromEcs(float speed)
    {
        currentMoveSpeed = speed;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        // Movement is intentionally XZ-only; Y is resolved by the ground snap below.
        Vector3 currentPosition = creatureTransform.position;

        CreatureMovementCalculator.ComputeStep(
            currentPosition,
            targetPosition,
            currentMoveSpeed,
            Time.fixedDeltaTime,
            halfHeight,
            GetBoundsParameters(),
            out float3 stepPosition,
            out bool hasRotation,
            out quaternion rotation);

        // Y stays on GroundSnapUtils so the out-of-bounds / missing-ground raycast
        // fallback keeps working exactly as before.
        Vector3 nextPosition = new Vector3(stepPosition.x, currentPosition.y, stepPosition.z);
        if (GroundSnapUtils.TryGetGroundY(stepPosition.x, stepPosition.z, out float groundY, halfHeight))
        {
            nextPosition.y = groundY;
        }
        creatureTransform.position = nextPosition;

        if (hasRotation)
        {
            creatureTransform.rotation = rotation;
        }
    }

    /// <summary>
    /// Used by the ECS execution systems: queues the move into the shared movement
    /// batch when ECS movement execution is enabled, otherwise moves immediately.
    /// The flee path intentionally keeps calling MoveTowards directly.
    /// </summary>
    public void MoveTowardsOrQueue(Vector3 targetPosition, bool useEcsMovementExecution)
    {
        if (useEcsMovementExecution && TryQueueEcsMove(targetPosition))
            return;

        MoveTowards(targetPosition);
    }

    public bool TryQueueEcsMove(Vector3 targetPosition)
    {
        CreatureMovementBatch batch = CreatureMovementBatch.Instance;
        if (batch == null || creature == null)
            return false;

        return batch.TryQueueMove(
            creature.GetInstanceID(),
            creatureTransform,
            targetPosition,
            currentMoveSpeed,
            halfHeight);
    }

    public Vector3 Wander()
    {
        var wanderTarget = ChooseNewWanderTarget();
        MoveTowards(wanderTarget);
        return wanderTarget;
    }

    public Vector3 ChooseNewWanderTarget()
    {
        if (RefreshBounds())
        {
            float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
            float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
            return new Vector3(x, GroundSnapUtils.GetGroundY(x, z), z);
        }
        else
        {
            Debug.LogError("GroundManager instance not found.");
            return Vector3.zero;
        }
    }

    public Vector3 SteerDirectionInsideBounds(Vector3 direction, float margin)
    {
        if (direction.sqrMagnitude < 0.0001f || !RefreshBounds())
            return direction;

        return FleeSteeringCalculator.SteerInsideBounds(
            direction,
            creatureTransform.position,
            halfHeight,
            margin,
            hasBounds: true,
            bounds.min.x,
            bounds.max.x,
            bounds.min.z,
            bounds.max.z,
            bounds.center.x,
            bounds.center.z);
    }

    private bool RefreshBounds()
    {
        if (GroundManager.Instance == null)
            return hasBounds;

        Bounds groundBounds = GroundManager.Instance.GroundBounds;
        if (groundBounds.size.x <= 0f || groundBounds.size.z <= 0f)
            return hasBounds;

        bounds = groundBounds;
        hasBounds = true;
        return true;
    }

    private MovementBoundsParameters GetBoundsParameters()
    {
        if (!hasBounds && !RefreshBounds())
            return default;

        return new MovementBoundsParameters
        {
            hasBounds = true,
            minX = bounds.min.x,
            maxX = bounds.max.x,
            minZ = bounds.min.z,
            maxZ = bounds.max.z,
            centerX = bounds.center.x,
            centerZ = bounds.center.z,
            groundSurfaceY = GroundManager.Instance != null ? GroundManager.Instance.GroundSurfaceY : 0f
        };
    }

    bool IsPathClear(Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(creature.transform.position, direction.normalized, out hit, 1f))
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                return false;
            }
        }
        return true;
    }
    public bool IsTargetReached(Vector3 target)
    {
        // Compare only XZ distance — Y differs due to ground snapping
        Vector3 flat = new Vector3(creature.transform.position.x, 0, creature.transform.position.z);
        Vector3 flatTarget = new Vector3(target.x, 0, target.z);
        return Vector3.Distance(flat, flatTarget) < 1.5f;
    }
}
