using Unity.Mathematics;
using UnityEngine;

public struct MovementBoundsParameters
{
    public bool hasBounds;
    public float minX;
    public float maxX;
    public float minZ;
    public float maxZ;
    public float centerX;
    public float centerZ;
    public float groundSurfaceY;

    public static MovementBoundsParameters FromGroundManager(GroundManager groundManager)
    {
        if (groundManager == null || !groundManager.HasGroundBounds)
            return default;

        Bounds bounds = groundManager.GroundBounds;
        if (bounds.size.x <= 0f || bounds.size.z <= 0f)
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
            groundSurfaceY = groundManager.GroundSurfaceY
        };
    }
}

/// <summary>
/// Pure movement step math shared by MovementManager (legacy per-creature path) and
/// the batched ECS movement execution job. Must stay behaviorally identical to the
/// historical MovementManager.MoveTowards implementation.
/// </summary>
public static class CreatureMovementCalculator
{
    public const float BoundsInset = 0.25f;
    public const float RotationThresholdSq = 0.0001f;

    /// <summary>Exact port of UnityEngine.Vector3.MoveTowards semantics on the XZ plane.</summary>
    public static float2 MoveTowardsXZ(float2 current, float2 target, float maxDistanceDelta)
    {
        float2 toTarget = target - current;
        float sqDist = math.lengthsq(toTarget);
        if (sqDist == 0f || (maxDistanceDelta >= 0f && sqDist <= maxDistanceDelta * maxDistanceDelta))
            return target;

        float dist = math.sqrt(sqDist);
        return current + toTarget / dist * maxDistanceDelta;
    }

    public static float2 ClampXZToBounds(float2 position, float halfHeight, in MovementBoundsParameters bounds)
    {
        if (!bounds.hasBounds)
            return position;

        float inset = math.max(BoundsInset, halfHeight * 0.5f);
        float minX = bounds.minX + inset;
        float maxX = bounds.maxX - inset;
        float minZ = bounds.minZ + inset;
        float maxZ = bounds.maxZ - inset;

        if (minX > maxX)
        {
            minX = maxX = bounds.centerX;
        }

        if (minZ > maxZ)
        {
            minZ = maxZ = bounds.centerZ;
        }

        return new float2(
            math.clamp(position.x, minX, maxX),
            math.clamp(position.y, minZ, maxZ));
    }

    /// <summary>
    /// One movement step toward a target. XZ math matches MovementManager.MoveTowards;
    /// ground Y uses the flat-ground fast path (GroundSurfaceY + halfHeight) that
    /// GroundSnapUtils.TryGetGroundY resolves to for in-bounds positions. Without bounds
    /// the current Y is kept (the legacy path would fall back to a physics raycast there;
    /// callers only use this step when ground bounds exist).
    /// </summary>
    public static void ComputeStep(
        float3 currentPosition,
        float3 targetPosition,
        float moveSpeed,
        float deltaTime,
        float halfHeight,
        in MovementBoundsParameters bounds,
        out float3 nextPosition,
        out bool hasRotation,
        out quaternion rotation)
    {
        float2 currentXZ = ClampXZToBounds(new float2(currentPosition.x, currentPosition.z), halfHeight, bounds);
        float2 targetXZ = ClampXZToBounds(new float2(targetPosition.x, targetPosition.z), halfHeight, bounds);

        float2 nextXZ = MoveTowardsXZ(currentXZ, targetXZ, moveSpeed * deltaTime);
        nextXZ = ClampXZToBounds(nextXZ, halfHeight, bounds);

        float nextY = bounds.hasBounds ? bounds.groundSurfaceY + halfHeight : currentPosition.y;
        nextPosition = new float3(nextXZ.x, nextY, nextXZ.y);

        // Rotation faces the full remaining delta to the target, not the step delta,
        // matching the legacy implementation.
        float2 moveDelta = targetXZ - currentXZ;
        if (math.lengthsq(moveDelta) > RotationThresholdSq)
        {
            float2 direction = math.normalize(moveDelta);
            rotation = quaternion.LookRotationSafe(new float3(direction.x, 0f, direction.y), math.up());
            hasRotation = true;
        }
        else
        {
            rotation = quaternion.identity;
            hasRotation = false;
        }
    }
}
