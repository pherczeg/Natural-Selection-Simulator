using UnityEngine;

public class MovementManager
{
    private float moveSpeed;
    public float MoveSpeed  => moveSpeed;
    
    private CreatureBehaviour creature;
    private Transform creatureTransform;
    private Bounds bounds;
    public MovementManager(CreatureBehaviour creature, float moveSpeed)
    {
        if (GroundManager.Instance != null)
        {
            bounds = GroundManager.Instance.GroundBounds;
        }
        this.creature = creature;
        this.moveSpeed = moveSpeed;
        this.creatureTransform = creature.transform;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        Vector3 horizontalTargetPosition = new Vector3(targetPosition.x, creatureTransform.position.y, targetPosition.z);

        creatureTransform.position = Vector3.MoveTowards(creatureTransform.position, horizontalTargetPosition, moveSpeed * Time.fixedDeltaTime);

        Vector3 moveDirection = (horizontalTargetPosition - creatureTransform.position).normalized;

        if (moveDirection != Vector3.zero)
        {
            Quaternion newRotation = Quaternion.LookRotation(moveDirection);
            creatureTransform.rotation = newRotation;
        }
    }

    public Vector3 Wander()
    {
        var wanderTarget = ChooseNewWanderTarget();
        MoveTowards(wanderTarget);
        return wanderTarget;
    }

    public Vector3 ChooseNewWanderTarget()
    {
        if (bounds != null)
        {
            Vector3 spawnPosition = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                creature.transform.position.y,
                Random.Range(bounds.min.z, bounds.max.z)
            );

            return spawnPosition;
        }
        else
        {
            Debug.LogError("GroundManager instance not found.");
            return Vector3.zero; // Visszatérünk egy alapértelmezett értékkel, ha nem található GroundManager példány
        }
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
        return Vector3.Distance(creature.transform.position, target) < .01f;
    }
}
