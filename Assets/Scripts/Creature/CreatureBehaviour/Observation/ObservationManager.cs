using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class ObservationManager
{
    private CreatureBehaviour creature;
    private Transform creatureTransform;
    private float senseRadius;
    private int numberOfRaycasts;
    private float angleBetweenRaycasts;

    public float SenseRadius => senseRadius;
    public Dictionary<float, ObservationData> Observations { get; private set; } = new Dictionary<float, ObservationData>();

    public ObservationManager(CreatureBehaviour creature, float senseRadius, int numberOfRaycasts, float angleBetweenRaycasts)
    {
        this.creature = creature;
        this.creatureTransform = creature.transform;
        this.senseRadius = senseRadius;
        this.numberOfRaycasts = numberOfRaycasts;
        this.angleBetweenRaycasts = angleBetweenRaycasts;
    }

    public void UpdateObservations()
    {
        creature.ObservationManager.Observations.Clear();
#if DEBUG

        //RaycastHit rayHit;
        //if (Physics.Raycast(creature.transform.position, creature.transform.forward, out rayHit, 100))
        //{
        //    Vector3 incomingDirection = creature.transform.forward;
        //    Vector3 reflectDirection = Vector3.Reflect(incomingDirection, rayHit.normal).normalized;
        //    Debug.DrawRay(creature.transform.position + Vector3.up * 0.1f, reflectDirection * rayHit.distance, Color.blue);
        //}
#endif
        for (int i = 0; i < numberOfRaycasts; i++)
        {
            float angle = ((2 * i + 1 - numberOfRaycasts) * angleBetweenRaycasts / 2);
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 rayDirection = rotation * creature.transform.forward;
            Vector3 rayStart = creature.transform.position + Vector3.up * 0.1f;

            ObservationData observationData = new();
            if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, senseRadius))
            {
                observationData.distance = hit.distance;
                observationData.observedObject = hit.collider.gameObject;

                if (hit.transform.gameObject.CompareTag("Food"))
                {
                    Debug.DrawRay(rayStart, rayDirection * hit.distance, Color.green);
                    observationData.type = ObservationType.Food;
                }
                else if (hit.transform.gameObject.CompareTag("Obstacle"))
                {
                    Debug.DrawRay(rayStart, rayDirection * hit.distance, Color.yellow);
                    observationData.type = ObservationType.Obstacle;
                }
                else if (hit.transform.gameObject.CompareTag("Creature"))
                {
                    Debug.DrawRay(rayStart, rayDirection * hit.distance, Color.blue);
                    observationData.type = ObservationType.Creature;
                }
                else
                {
                    Debug.DrawRay(rayStart, rayDirection * hit.distance, Color.red);
                    observationData.type = ObservationType.None;
                }
            }
            else
            {
                Debug.DrawRay(rayStart, rayDirection * senseRadius, Color.gray);
                observationData.type = ObservationType.None;
            }
            creature.ObservationManager.Observations[i] = observationData;
        }
    }

    private ObservationType DetermineObservationType(GameObject obj)
    {
        if (obj.CompareTag("Food")) return ObservationType.Food;
        if (obj.CompareTag("Obstacle")) return ObservationType.Obstacle;
        if (obj.CompareTag("Creature")) return ObservationType.Creature;
        return ObservationType.None;
    }
}
