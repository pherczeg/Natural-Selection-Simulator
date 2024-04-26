using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class ObservationManager
{
    private BaseCreatureBehaviour creature;
    private Transform creatureTransform;
    private float senseRadius;
    private int numberOfRaycasts;
    private float angleBetweenRaycasts;

    public float SenseRadius => senseRadius;
    public List<ObservationData> Observations { get; private set; } = new List<ObservationData>();

    public ObservationManager(BaseCreatureBehaviour creature, float senseRadius, int numberOfRaycasts, float angleBetweenRaycasts)
    {
        this.creature = creature;
        this.creatureTransform = creature.transform;
        this.senseRadius = senseRadius;
        this.numberOfRaycasts = numberOfRaycasts;
        this.angleBetweenRaycasts = angleBetweenRaycasts;
    }
}
