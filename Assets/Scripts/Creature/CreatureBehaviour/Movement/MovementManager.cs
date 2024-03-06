using UnityEngine;

public class MovementManager
{
    private float moveSpeed;
    public float MoveSpeed  => moveSpeed;
    
    private CreatureBehaviour creature;
    private Transform creatureTransform;

    private Collider groundCollider;
    public MovementManager(CreatureBehaviour creature, float moveSpeed)
    {
        this.creature = creature;
        this.moveSpeed = moveSpeed;
        this.creatureTransform = creature.transform;
        //this.moveSpeed = creature.moveSpeed;
        GameObject ground = GameObject.FindGameObjectWithTag("Ground");
        groundCollider = ground.GetComponent<Collider>();
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
        Vector3 wanderTarget;
        float wanderRadius = UnityEngine.Random.Range(3f, 15f);
        Vector3 randomDirection;
        do
        {
            randomDirection = UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomDirection += creature.transform.position;
            randomDirection.y = creature.transform.position.y;
        } while (!IsWithinGroundBounds(randomDirection));

        Vector3 forward = creature.transform.forward;
        forward.y = 0;
        Vector3 toRandomDirection = randomDirection - creature.transform.position;
        toRandomDirection.y = 0;

        if (Vector3.Angle(forward, toRandomDirection) > 30f)
        {
            toRandomDirection = Vector3.RotateTowards(forward, toRandomDirection, Mathf.Deg2Rad * 30f, 0f);
            randomDirection = creature.transform.position + toRandomDirection.normalized * wanderRadius;
        }

        if (IsPathClear(toRandomDirection) && IsWithinGroundBounds(randomDirection))
        {
            wanderTarget = randomDirection;
        }
        else
        {
            wanderTarget = GetAlternativeDirection();
        }
        return  wanderTarget;
    }

    bool IsWithinGroundBounds(Vector3 position)
    {
        if (groundCollider != null)
        {
            // Létrehozunk egy új Vector3 objektumot, amely csak a X és Z koordinátákat tartalmazza
            Vector3 groundLevelPosition = new Vector3(position.x, groundCollider.bounds.center.y, position.z);
            return groundCollider.bounds.Contains(groundLevelPosition);
        }
        return false;
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
    Vector3 GetAlternativeDirection()
    {
        ObservationData closestObstacle = new ObservationData { distance = float.MaxValue };
        float minAngle = float.MaxValue;

        foreach (var observation in creature.ObservationManager.Observations)
        {
            if (observation.Value.type == ObservationType.Obstacle)
            {
                Vector3 toObstacle = observation.Value.observedObject.transform.position - creature.transform.position;
                float angle = Vector3.Angle(creature.transform.forward, toObstacle);

                if (angle < minAngle)
                {
                    minAngle = angle;
                    closestObstacle = observation.Value;
                }
            }
        }

        if (closestObstacle.observedObject != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(creature.transform.position, closestObstacle.observedObject.transform.position - creature.transform.position, out hit))
            {
                Vector3 incomingVec = hit.point - creature.transform.position;
                Vector3 reflectVec = Vector3.Reflect(incomingVec, hit.normal);
                Vector3 newDirection = creature.transform.position + reflectVec.normalized * (2f);
                newDirection.y = creature.transform.position.y;
                return newDirection;
            }
        }

        Vector3 randomDirection;
        do
        {
            randomDirection = creature.transform.position + UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(3f, 15f);
            randomDirection.y = creature.transform.position.y; 
        } while (!IsWithinGroundBounds(randomDirection));

        return randomDirection;
    }
    public bool IsTargetReached(Vector3 target)
    {
        return Vector3.Distance(creature.transform.position, target) < 1.0f;
    }
}
