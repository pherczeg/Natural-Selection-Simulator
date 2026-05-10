using UnityEngine;

public static class GroundSnapUtils
{
    private const float RaycastOriginHeight = 500f;
    private const float RaycastDistance = 1000f;
    private const int GroundHitBufferSize = 32;
    private static readonly RaycastHit[] GroundHitBuffer = new RaycastHit[GroundHitBufferSize];

    /// <summary>
    /// Returns the Y position on the ground directly below (x, z), plus an optional offset.
    /// Only hits colliders tagged "Ground". Falls back to fallbackY if no ground is hit.
    /// </summary>
    public static float GetGroundY(float x, float z, float surfaceOffset = 0f, float fallbackY = 0f)
    {
        return TryGetGroundY(x, z, out float y, surfaceOffset) ? y : fallbackY;
    }

    public static bool TryGetGroundY(float x, float z, out float y, float surfaceOffset = 0f)
    {
        GroundManager groundManager = GroundManager.Instance;
        if (groundManager != null && groundManager.HasGroundBounds)
        {
            Bounds bounds = groundManager.GroundBounds;
            if (x >= bounds.min.x &&
                x <= bounds.max.x &&
                z >= bounds.min.z &&
                z <= bounds.max.z)
            {
                y = groundManager.GroundSurfaceY + surfaceOffset;
                return true;
            }
        }

        Vector3 origin = new Vector3(x, RaycastOriginHeight, z);
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHitBuffer, RaycastDistance);

        if (hitCount == GroundHitBuffer.Length)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, RaycastDistance);
            return TrySelectGroundY(hits, hits.Length, out y, surfaceOffset);
        }

        return TrySelectGroundY(GroundHitBuffer, hitCount, out y, surfaceOffset);
    }

    private static bool TrySelectGroundY(RaycastHit[] hits, int hitCount, out float y, float surfaceOffset)
    {
        float bestY = float.MinValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.CompareTag("Ground") && hit.point.y > bestY)
            {
                bestY = hit.point.y;
                found = true;
            }
        }

        y = found ? bestY + surfaceOffset : 0f;
        return found;
    }

    /// <summary>
    /// Returns ground Y with an automatic half-height offset derived from the object's renderer/collider.
    /// </summary>
    public static float GetGroundYForObject(float x, float z, GameObject obj, float fallbackY = 0f)
    {
        float halfHeight = GetHalfHeight(obj);
        return GetGroundY(x, z, halfHeight, fallbackY);
    }

    /// <summary>
    /// Snaps an existing transform to the ground, offset by half the object's height.
    /// </summary>
    public static void SnapToGround(Transform t, float surfaceOffset = 0f)
    {
        float y = GetGroundY(t.position.x, t.position.z, surfaceOffset, t.position.y);
        t.position = new Vector3(t.position.x, y, t.position.z);
    }

    public static float GetHalfHeight(GameObject obj)
    {
        // Prefer body colliders and ignore trigger volumes (e.g. detection/sense colliders).
        Collider rootCollider = obj.GetComponent<Collider>();
        if (rootCollider != null && !rootCollider.isTrigger)
            return rootCollider.bounds.extents.y;

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        float best = float.MaxValue;
        bool found = false;
        foreach (var c in colliders)
        {
            if (c == null || c.isTrigger)
                continue;

            float ext = c.bounds.extents.y;
            if (ext > 0f && ext < best)
            {
                best = ext;
                found = true;
            }
        }

        if (found)
            return best;

        Renderer rootRenderer = obj.GetComponent<Renderer>();
        if (rootRenderer != null)
            return rootRenderer.bounds.extents.y;

        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r != null)
            return r.bounds.extents.y;

        return 0.5f;
    }
}
