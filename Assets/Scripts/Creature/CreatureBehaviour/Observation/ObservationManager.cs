using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class ObservationManager
{
    private BaseCreatureBehaviour creature;
    private Transform creatureTransform;
    private float baseSenseRadius;
    private float currentSenseRadius;
    private int numberOfRaycasts;
    private float angleBetweenRaycasts;

    public float SenseRadius => currentSenseRadius;
    public float BaseSenseRadius => baseSenseRadius;
    public List<ObservationData> Observations { get; private set; } = new List<ObservationData>();

    public ObservationManager(BaseCreatureBehaviour creature, float senseRadius, int numberOfRaycasts, float angleBetweenRaycasts)
    {
        this.creature = creature;
        this.creatureTransform = creature.transform;
        this.baseSenseRadius = senseRadius;
        this.currentSenseRadius = senseRadius;
        this.numberOfRaycasts = numberOfRaycasts;
        this.angleBetweenRaycasts = angleBetweenRaycasts;
    }

    public void SetAgeMultiplier(float multiplier)
    {
        currentSenseRadius = Mathf.Max(0f, baseSenseRadius * multiplier);
    }
}
