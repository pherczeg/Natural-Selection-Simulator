using Unity.Mathematics;
using UnityEngine;

public class MovementManager
{
    private const float BoundsInset = CreatureMovementCalculator.BoundsInset;

    private float baseMoveSpeed;
    private float currentMoveSpeed;
    private float ageMultiplier = 1f;
    private SprintProfileParameters sprintProfile = SprintProfileParameters.Default;
    private SprintEffectsState sprintState = SprintEffectsState.Default;
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
        sprintProfile = SprintProfileParameters.Create(duration, sprintFactor, cooldownDuration, cooldownSpeedFactor);
        // Legacy behavior: profile changes reset the sprint timers but keep any
        // active temporary multiplier.
        sprintState.sprintRemaining = 0f;
        sprintState.sprintCooldownRemaining = 0f;
        RecalculateCurrentSpeed();
    }

    public bool TryStartSprint()
    {
        if (!SprintEffectsCalculator.TryStartSprint(ref sprintState, in sprintProfile))
            return false;

        RecalculateCurrentSpeed();
        return true;
    }

    public void SetAgeMultiplier(float multiplier)
    {
        ageMultiplier = Mathf.Max(0f, multiplier);
        RecalculateCurrentSpeed();
    }

    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        SprintEffectsCalculator.ApplyTemporaryMultiplier(ref sprintState, multiplier, duration);
        RecalculateCurrentSpeed();
    }

    public void UpdateTemporaryEffects(float deltaTime)
    {
        if (SprintEffectsCalculator.Tick(ref sprintState, deltaTime, in sprintProfile))
            RecalculateCurrentSpeed();
    }

    private void RecalculateCurrentSpeed()
    {
        currentMoveSpeed = SprintEffectsCalculator.ComputeCurrentSpeed(in sprintState, baseMoveSpeed, ageMultiplier, in sprintProfile);
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

        Vector3 adjusted = direction;
        adjusted.y = 0f;

        float inset = Mathf.Max(BoundsInset, halfHeight * 0.5f);
        float edgeMargin = Mathf.Max(0.1f, margin);
        Vector3 position = creatureTransform.position;
        float minX = bounds.min.x + inset;
        float maxX = bounds.max.x - inset;
        float minZ = bounds.min.z + inset;
        float maxZ = bounds.max.z - inset;

        if ((position.x <= minX + edgeMargin && adjusted.x < 0f) ||
            (position.x >= maxX - edgeMargin && adjusted.x > 0f))
        {
            adjusted.x = 0f;
        }

        if ((position.z <= minZ + edgeMargin && adjusted.z < 0f) ||
            (position.z >= maxZ - edgeMargin && adjusted.z > 0f))
        {
            adjusted.z = 0f;
        }

        if (adjusted.sqrMagnitude < 0.0001f)
        {
            adjusted = bounds.center - position;
            adjusted.y = 0f;
        }

        return adjusted.sqrMagnitude > 0.0001f ? adjusted.normalized : direction.normalized;
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
