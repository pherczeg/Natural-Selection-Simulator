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
    public float Age => age;
    public float Weight => weight;
    public float Energy
    {
        get
        {
            if (EnergyManagement == null) 
            {
                return default(float);
            }
            return EnergyManagement.EnergyLevel;
        }
    }
    public MovementManager MovementManager { get; set; }
    public EnergyManager EnergyManagement { get; set; }
    public ObservationManager ObservationManager { get; set; }
    public ReproductionManager Reproduction { get; set; }
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
        MovementManager = new MovementManager(this, moveSpeed);
        ObservationManager = new ObservationManager(this, senseRadius, numberOfRaycasts, angleBetweenRaycasts);
        stateMachine = new StateMachine(this);
        EnergyManagement = new EnergyManager(this, maxEnergy, maxEnergy);
        EnergyManagement.UpdateEnergyBar();
        EatingManager = new EatingManager(this);
    }

    void Update()
    {
        lastObservation += Time.deltaTime;
        if (lastObservation >= config.updateInterval)
        {
            ObservationManager.UpdateObservations();
            var energyConsumption = EnergyManagement.CalculateEnergyConsumption();
            EnergyManagement.ConsumeEnergy(energyConsumption);
            lastObservation = 0f;
        }
        if (EnergyManagement.IsEnergyDepleted())
        {
            Destroy(gameObject);
            return;
        }
        stateMachine.Update();
        CheckTransitions();
    }
    void OnTriggerEnter(Collider col)
    {
        //if (col.gameObject.CompareTag("Food") && ( CurrentStateType== CreatureStateType.MovingToFood|| CurrentStateType == CreatureStateType.SearchingForFood))
        //{
        //    Food foodComponent = col.gameObject.GetComponentInChildren<Food>();

        //    if (this.EatingManager.IsFoodBlacklisted(foodComponent))
        //    {
        //        stateMachine.TransitionToSearchingForFood();
        //        return;
        //    }
        //    if (foodComponent.isBeingEaten)
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
        if (EnergyManagement.EnergyLevel < config.energyThreshold * maxEnergy && (!(stateMachine.CurrentState.StateType == CreatureStateType.MovingToFood) && !(stateMachine.CurrentState.StateType == CreatureStateType.Eating)))
        {
            stateMachine.TransitionToSearchingForFood();
        }
    }
}
