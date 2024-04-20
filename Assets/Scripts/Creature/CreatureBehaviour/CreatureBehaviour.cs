using System;
using Unity.IO.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;

public class CreatureBehaviour : MonoBehaviour
{
    [SerializeField]
    public float asdf 
    { 
        get 
        {
            if (MovementManager != null)
                return MovementManager.MoveSpeed;
            return 0;
        } 
    }
    [SerializeField,TextArea]
    public string DEBUG_string;
    private float age;
    private float weight;
    private int numberOfRaycasts = 30;
    private float angleBetweenRaycasts = 5f;
    private float matingCooldown;
    private CreatureSpawner creatureSpawner;
    float lastObservation = 0f;

    public GameObject energyBarObject;
    public float maxEnergy;
    public float Weight => weight;
    public float Energy
    {
        get
        {
            if (EnergyManager == null) 
            {
                return default(float);
            }
            return EnergyManager.EnergyLevel;
        }
    }
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
    public void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        maxEnergy = GameConfig.Instance.maxEnergy;
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        AgeManager = new AgeManager(this);
        ReproductionManager = new ReproductionManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        stateMachine = new StateMachine(this);
        EnergyManager = new EnergyManager(this, maxEnergy* GameConfig.Instance.initialEnergyPercentage, maxEnergy);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
    }
    //void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.blue; // Beállítjuk a gizmo színét
    //    Gizmos.DrawWireSphere(transform.position, ObservationManager.SenseRadius); // Rajzolunk egy drótváz gömböt, amely jelzi a vizsgált területet
    //}
    void FixedUpdate()
    {
        lastObservation += Time.fixedDeltaTime;
        if (lastObservation >= GameConfig.Instance.updateInterval)
        {
            //ObservationManager.UpdateObservations();
            var energyConsumption = EnergyManager.CalculateEnergyConsumption();
            EnergyManager.ConsumeEnergy(energyConsumption);
            AgeManager.UpdateAge(lastObservation);
            if (AgeManager.IsMaxAgeReached())
            {
                DestroyObject();
            }
            if (ReproductionManager.IsOnCooldown())
            {
                ReproductionManager.UpdateReproductionCooldown(lastObservation);
            }
            lastObservation = 0f;
        }
        if (EnergyManager.IsEnergyDepleted())
        {
            DestroyObject();
            return;
        }
        stateMachine.Update();
        CheckTransitions();
    }
    private void DestroyObject()
    {
        PoolManager.Instance.ReturnObject(CreatureSpawner.Instance.creaturePrefab, this.gameObject);
        EatingManager.InterruptEating();
        creatureSpawner.RemoveFromList(this);
    }
    void CheckTransitions()
    {
        if (stateMachine.CurrentState.StateType == CreatureStateType.MovingToFood || stateMachine.CurrentState.StateType == CreatureStateType.Eating || stateMachine.CurrentState.StateType == CreatureStateType.SearchingForFood || stateMachine.CurrentState.StateType == CreatureStateType.Reproducting)
        {
            return;
        }
        else if (EnergyManager.EnergyLevel < GameConfig.Instance.eatingEnergyThreshold * maxEnergy)
        {
            stateMachine.TransitionToSearchingForFood();
        }
        else  if ( stateMachine.CurrentState.StateType == CreatureStateType.SearchingForMate || stateMachine.CurrentState.StateType == CreatureStateType.MovingToMate)
        {
            return;
        }
        else if (!ReproductionManager.IsOnCooldown() && ReproductionManager.IsReadyToReproduction())
        {
            stateMachine.TransitionToSearchingForMate();
        }
        else if (stateMachine.CurrentState.StateType == CreatureStateType.Idle)
        {
            stateMachine.TransitionToWandering();
        }
    }
}
