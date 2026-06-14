using UnityEngine;

/// <summary>
/// Pure bounds-steering math shared by MovementManager (legacy flee path) and the
/// Phase 3 ECS flee system. Must stay behaviorally identical to the historical
/// MovementManager.SteerDirectionInsideBounds implementation; MovementManager
/// delegates to it the same way MoveTowards delegates to CreatureMovementCalculator.
/// </summary>
public static class FleeSteeringCalculator
{
    /// <summary>
    /// Steers a desired flee direction so it does not push the creature past the
    /// inset ground bounds. When the steered direction collapses to zero, it points
    /// back toward the ground center. Returns a normalized direction (or the
    /// original direction normalized as a fallback).
    /// </summary>
    public static Vector3 SteerInsideBounds(
        Vector3 direction,
        Vector3 position,
        float halfHeight,
        float margin,
        bool hasBounds,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        float centerX,
        float centerZ)
    {
        if (direction.sqrMagnitude < 0.0001f || !hasBounds)
            return direction;

        Vector3 adjusted = direction;
        adjusted.y = 0f;

        float inset = Mathf.Max(CreatureMovementCalculator.BoundsInset, halfHeight * 0.5f);
        float edgeMargin = Mathf.Max(0.1f, margin);
        float localMinX = minX + inset;
        float localMaxX = maxX - inset;
        float localMinZ = minZ + inset;
        float localMaxZ = maxZ - inset;

        if ((position.x <= localMinX + edgeMargin && adjusted.x < 0f) ||
            (position.x >= localMaxX - edgeMargin && adjusted.x > 0f))
        {
            adjusted.x = 0f;
        }

        if ((position.z <= localMinZ + edgeMargin && adjusted.z < 0f) ||
            (position.z >= localMaxZ - edgeMargin && adjusted.z > 0f))
        {
            adjusted.z = 0f;
        }

        if (adjusted.sqrMagnitude < 0.0001f)
        {
            adjusted = new Vector3(centerX, 0f, centerZ) - position;
            adjusted.y = 0f;
        }

        return adjusted.sqrMagnitude > 0.0001f ? adjusted.normalized : direction.normalized;
    }
}
