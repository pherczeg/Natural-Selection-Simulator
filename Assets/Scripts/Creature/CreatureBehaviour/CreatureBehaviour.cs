using System;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class CreatureBehaviour : MonoBehaviour
{
    private float age;
    private float weight;
    private int numberOfRaycasts = 30;
    private float angleBetweenRaycasts = 5f;
    private float matingCooldown;

    float lastObservation = 0f;

    public GameObject energyBarObject;
    //private EnergyBar energyBar;
    public float maxEnergy = 100f;
    //public float Age => age;
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
    // Felelõs az állapotok és komponensek kezeléséért
    public StateMachine stateMachine;
    public GameConfig config;
    public CoroutineRunner coroutineRunner;
    void Start()
    {

    }
    public void Initialize(float moveSpeed, float weight, float senseRadius)
    {
        this.weight = weight;
        coroutineRunner = gameObject.GetComponent<CoroutineRunner>() ?? gameObject.AddComponent<CoroutineRunner>();
        config = ScriptableObject.CreateInstance<GameConfig>();
        AgeManager = new AgeManager(this);
        ReproductionManager = new ReproductionManager(this);
        MovementManager = new MovementManager(this, moveSpeed);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        stateMachine = new StateMachine(this);
        EnergyManager = new EnergyManager(this, maxEnergy, maxEnergy);
        EnergyManager.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
    }

    void Update()
    {
        lastObservation += Time.deltaTime;
        if (lastObservation >= config.updateInterval)
        {
            ObservationManager.UpdateObservations();
            var energyConsumption = EnergyManager.CalculateEnergyConsumption();
            EnergyManager.ConsumeEnergy(energyConsumption);
            AgeManager.UpdateAge(lastObservation);
            if (ReproductionManager.IsOnCooldown())
            {
                ReproductionManager.UpdateReproductionCooldown(lastObservation);
            }
            lastObservation = 0f;
        }
        if (EnergyManager.IsEnergyDepleted())
        {
            Destroy(gameObject);
            return;
        }
        stateMachine.Update();
        CheckTransitions();
    }
    void OnTriggerEnter(Collider col)
    {
        //if (col.gameObject.CompareTag("CreatureBehaviour") && ( CurrentStateType== CreatureStateType.MovingToFood|| CurrentStateType == CreatureStateType.SearchingForFood))
        //{
        //    CreatureBehaviour foodComponent = col.gameObject.GetComponentInChildren<CreatureBehaviour>();

        //    if (this.EatingManager.IsFoodBlacklisted(foodComponent))
        //    {
        //        stateMachine.TransitionToSearchingForFood();
        //        return;
        //    }
        //    if (foodComponent.IsBeingEaten)
        //    {
        //        CreatureBehaviour otherCreature = foodComponent.GetEatingCreature();
        //        if (otherCreature != null)
        //        {
        //            if (this.weight > otherCreature.weight * 1.5f)
        //            {
        //                otherCreature.EatingManager.InterruptEating();
        //            }
        //            else
        //            {
        //                BlacklistFood(foodComponent);
        //                SetState(CreatureState.SearchingForFood);
        //                return;
        //            }
        //        }
        //    }

        //    SetState(CreatureState.Eating);
        //    if (foodComponent == null)
        //    {
        //        Console.WriteLine();
        //    }
        //    eatingCoroutine = StartCoroutine(EatingRoutine(foodComponent, foodComponent.nutritionValue));
        //}
        //else if (col.gameObject.CompareTag("Obstacle"))
        //{
        //    RaycastHit hit;
        //    if (Physics.Raycast(transform.position, transform.forward, out hit, senseRadius))
        //    {
        //        //    Vector3 incomingDirection = transform.forward;
        //        //    Vector3 reflectDirection = Vector3.Reflect(incomingDirection, hit.normal).normalized;
        //        Vector3 reflectDirection = Vector3.Reflect(transform.forward, hit.normal).normalized;
        //        MoveAndFaceDirection(transform.position + reflectDirection);
        //    }
        //}
    }


    void CheckTransitions()
    {
        if (EnergyManager.EnergyLevel < config.eatingEnergyThreshold * maxEnergy &&
            (!(stateMachine.CurrentState.StateType == CreatureStateType.MovingToFood) && !(stateMachine.CurrentState.StateType == CreatureStateType.Eating) && !(stateMachine.CurrentState.StateType == CreatureStateType.SearchingForFood)))
        {
            stateMachine.TransitionToSearchingForFood();
        }
        else if(stateMachine.CurrentState.StateType == CreatureStateType.MovingToFood || stateMachine.CurrentState.StateType == CreatureStateType.Eating || stateMachine.CurrentState.StateType == CreatureStateType.SearchingForFood)
        {
            return;
        }
        else if (!ReproductionManager.IsOnCooldown() && ReproductionManager.IsReadyToReproduction() &&
            (!(stateMachine.CurrentState.StateType == CreatureStateType.MovingToMate) && !(stateMachine.CurrentState.StateType == CreatureStateType.Reproducting) && !(stateMachine.CurrentState.StateType == CreatureStateType.SearchingForMate)))
        {
            stateMachine.TransitionToSearchingForMate();
        }
        else if(stateMachine.CurrentState.StateType == CreatureStateType.Idle)
        {
            stateMachine.TransitionToWandering();
        }
    }
}
