using System;
using Unity.IO.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;
public abstract class BaseCreatureBehaviour : MonoBehaviour
{
    [SerializeField,TextArea]
    public string DEBUG_string;
    private float age;
    protected float weight;
    protected int numberOfRaycasts = 30;
    protected float angleBetweenRaycasts = 5f;
    private float matingCooldown;
    protected CreatureSpawner creatureSpawner;
    protected float lastObservation = 0f;

    public GameObject energyBarObject;
    public float maxEnergy;
    public float Weight => weight;
    public MovementManager MovementManager { get; set; }
    public AgeManager AgeManager { get; set; }
    public EnergyManager EnergyManager { get; set; }
    public ObservationManager ObservationManager { get; set; }
    public ReproductionManager ReproductionManager { get; set; }
    public EatingManager EatingManager { get; set; }

    public CreatureStateType CurrentStateType
    {
        get 
        {
            if (stateMachine == null)
                return CreatureStateType.None;
            return this.stateMachine.CurrentState.StateType; 
        }
    }
    public StateMachine stateMachine;
    public CoroutineRunner coroutineRunner;
    void Awake()
    {
        creatureSpawner = FindObjectOfType<CreatureSpawner>();
    }
    
    public abstract void Initialize(float moveSpeed, float weight, float senseRadius);
    protected abstract void DestroyObject();    
    protected abstract void CheckTransitions();
    
}
