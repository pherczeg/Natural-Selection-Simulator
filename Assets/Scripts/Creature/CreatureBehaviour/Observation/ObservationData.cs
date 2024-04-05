using UnityEngine;

public struct ObservationData
{
    public ObservationType type;
    public GameObject observedObject;
}

public enum ObservationType
{
    None,
    Food,
    Obstacle,
    Creature
}