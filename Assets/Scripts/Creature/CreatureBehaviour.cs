using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using static UnityEditorInternal.VersionControl.ListControl;
using static UnityEngine.EventSystems.EventTrigger;

public enum CreatureState
{
    Idle,
    SearchingForFood,
    MovingToFood,
    Eating,
    Wandering,
    SearchingForMate,
    Mating
}
public enum ObservationType
{
    None,
    Food,
    Obstacle,
    Creature
}


public class CreatureBehavior : MonoBehaviour
{
    public struct ObservationData
    {
        public ObservationType type;
        public float distance;
        public GameObject observedObject;
    }
    public static event System.Action<CreatureBehavior> OnCreatureSpawned;
    public static event System.Action<CreatureBehavior> OnCreatureDestroyed;

    private bool isRaycastResultsDisposed = false; // Állapotjelzõ az erõforrások felszabadításának nyomon követésére

    // Struct to store observation data
    private CreatureSpawner creatureSpawner;
    private const float BLACKLIST_DURATION = 3f;
    private const float UPDATE_INTERVAL = .4f;
    private const float REPRODUCTION_COOLDOWN= 7f;// Time (in seconds) to remember a food to avoid
    private const float matingAge = 5f;
    private const float MAX_AGE = 30f;
    private const float eatingDuration = 2f; // Time in seconds that creature takes to eat

    private NativeArray<RaycastCommand> raycastCommands;
    private NativeArray<RaycastHit> raycastResults;
    private JobHandle raycastJobHandle;
    // Limits, durations and thresholds for the entity
    public float energyThreshold = 0.8f; // Adjust this value as required
    public float maxEnergy = 100f;
    float initialEnergyPercentage = 0.5f;
    private float matingEnergyThreshold = 0.95f;
    public float age = 0;
    public float reproductionCooldown = 0f;

    public CreatureState currentState = CreatureState.Idle;
    public float weight = 1f;
    public float moveSpeed = 5f;
    public float senseRadius = 15f;
    public float energyLevel;
    //public Material energyBarMaterial;
    public GameObject energyBarObject;  // Drag the EnergyBar Quad GameObject here in the inspector
    private EnergyBar energyBar;        // Reference to the EnergyBar component

    private Vector3 wanderTarget = Vector3.zero;
    private bool isEating = false;
    private GameObject objectTarget;

    private Coroutine eatingCoroutine = null;
    Dictionary<float, ObservationData> observations = new Dictionary<float, ObservationData>();
    private Dictionary<Food, float> blacklistedFoods = new Dictionary<Food, float>();


    public int numberOfRaycasts = 30;
    public float angleBetweenRaycasts = 5f;
    GameObject ground;
    void Start()
    {
        raycastJobHandle = new JobHandle();
        creatureSpawner = FindObjectOfType<CreatureSpawner>();
        ground = GameObject.FindGameObjectWithTag("Ground");
        //energyLevel = initialEnergyPercentage * maxEnergy;
        if (energyBarObject)
        {
            energyBar = energyBarObject.GetComponent<EnergyBar>();
        }
        
        StartCoroutine(RemoveExpiredBlacklistedFoods());
        StartCoroutine(UpdateRoutine());
        lastObservation = 0;
    }
    void OnDestroy()
    {
        SafeDisposeNativeArray(ref raycastResults);
        SafeDisposeNativeArray(ref raycastCommands);
    }
    private void SafeDisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
    {
        if (array.IsCreated && !isRaycastResultsDisposed)
        {
            array.Dispose();
            isRaycastResultsDisposed = true; // Jelezzük, hogy az erõforrást már felszabadítottuk
        }
    }
    void FixedUpdate()
    {
        //IncreaseAge();
        //EnergyManagement();
        if (energyLevel <= 0 || age > MAX_AGE)
        {
            Destroy(gameObject);
            return;
        }
        lastObservation += Time.deltaTime;

        // Ha eltelt .4 másodperc, frissítjük az észleléseket
        //if (lastObservation >= 0.4f)
        //{
        //    UpdateObservations();
        //    lastObservation = 0f; // Reseteljük az idõszámlálót
        //}
        if (energyLevel <= energyThreshold * maxEnergy)
        {
            currentState = CreatureState.SearchingForFood;
        }
        else if (age >= matingAge && energyLevel > maxEnergy * 0.5f && reproductionCooldown <= 0f)
        {
            currentState = CreatureState.SearchingForMate;
        }

        if (currentState == CreatureState.SearchingForMate)
        {
            FindMate();
        }

        if (currentState == CreatureState.MovingToFood)
        {
            if (objectTarget == null)
            {
                currentState = CreatureState.Wandering;
                return;
            }
            if (objectTarget.TryGetComponent<Food>(out Food component))
            {
                if (component.isBeingEaten)
                {
                    var other = component.GetEatingCreature();
                    if (other != null && other.weight > weight * 2)
                    {
                        BlacklistFood(component);
                        currentState = CreatureState.SearchingForFood;
                        return;
                    }
                }
            }
            MoveAndFaceDirection(objectTarget.transform.position);
        }
        else if (!isEating && currentState != CreatureState.MovingToFood)
        {
            MakeMovementDecision();
        }
    }
    public void ScheduleObservationUpdate(Vector3 creaturePosition, Vector3 forward,  int layerMask)
    {
        raycastCommands = new NativeArray<RaycastCommand>(numberOfRaycasts, Allocator.Temp);
        raycastResults = new NativeArray<RaycastHit>(numberOfRaycasts, Allocator.Temp);

        Vector3 position = creaturePosition + Vector3.up * 0.1f; // Kis eltolás

        for (int i = 0; i < numberOfRaycasts; i++)
        {
            float angle = ((2 * i + 1 - numberOfRaycasts) * angleBetweenRaycasts / 2);
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 direction = rotation * forward;

            // RaycastCommand inicializálása a saját QueryParameters használatával
            var queryParameters = new QueryParameters
            {
                layerMask = layerMask
            };

            raycastCommands[i] = new RaycastCommand(position, direction, queryParameters, senseRadius);
        }

        // Schedule the batch of raycasts
    }



    // Ezt a metódust hívja meg, amikor a Creature létrejön vagy aktiválódik a játékban
    private void OnEnable()
    {
        // Esemény kiváltása
        OnCreatureSpawned?.Invoke(this);
    }

    // Ezt a metódust hívja meg, amikor a Creature megszûnik vagy deaktiválódik a játékban
    private void OnDisable()
    {
        // Esemény kiváltása
        OnCreatureDestroyed?.Invoke(this);
    }

    //void LateUpdate()
    //{
    //    // Ellenõrizzük, hogy a raycast job befejezõdött-e
    //    if (raycastJobHandle.IsCompleted)
    //    {
    //        raycastJobHandle.Complete();

    //        List<ObservationData> observations = new List<ObservationData>(); // Lista az észlelések tárolására

    //        // Feldolgozzuk az eredményeket
    //        for (int i = 0; i < raycastResults.Length; i++)
    //        {
    //            RaycastHit hit = raycastResults[i];
    //            RaycastCommand command = raycastCommands[i]; // A raycast parancs, amely az eredeti ray adatokat tartalmazza



    //            if (hit.collider != null)
    //            {
    //                //Debug.DrawRay(command.from, command.direction * hit.distance, Color.green, .1f);
    //                // Hozzuk létre az ObservationData példányt a találati adatokkal
    //                ObservationData observation = new ObservationData
    //                {
    //                    distance = hit.distance,
    //                    observedObject = hit.collider.gameObject,
    //                    type = DetermineObservationType(hit.collider.gameObject)
    //                };

    //                observations.Add(observation); // Hozzáadjuk az észlelést a listához

    //                // Opcionális: Debug log a találatról
    //                Debug.Log($"Hit: {hit.collider.gameObject.name}, Distance: {hit.distance}, Type: {observation.type}");
    //            }
    //            else
    //            {
    //                // Nincs találat: piros szín, a teljes hatótávolságig
    //                //Debug.DrawRay(command.from, command.direction * command.distance, Color.red, .1f);
    //            }
    //        }

    //        // Tisztítás
    //        raycastCommands.Dispose();
    //        raycastResults.Dispose();
    //        raycastJobHandle = new JobHandle();
    //    }
    //}
    private ObservationType DetermineObservationType(GameObject obj)
    {
        if (obj.CompareTag("Food")) return ObservationType.Food;
        if (obj.CompareTag("Obstacle")) return ObservationType.Obstacle;
        if (obj.CompareTag("Creature")) return ObservationType.Creature;
        return ObservationType.None;
    }

    private IEnumerator UpdateRoutine()
    {
        while (true)
        {
            IncreaseAge();
            EnergyManagement();

            yield return new WaitForSeconds(UPDATE_INTERVAL); // Másodpercenként frissít
        }
    }
    void EnergyManagement()
    {
        if (currentState != CreatureState.Eating)
        {
            float energyConsumption = (0.5f * 
                                      (float)Math.Pow(weight, 3) * 
                                      (float)Math.Pow(moveSpeed, 2)) * 
                                      //Time.fixedDeltaTime) *
                                      UPDATE_INTERVAL*
                                      //numberOfRaycasts * senseRadius * 
                                      0.00001f;
            energyLevel -= energyConsumption;
        }
        UpdateEnergyBar();
    }
    void UpdateEnergyBar()
    {
        if (energyBar)
        {
            energyBar.SetEnergy(energyLevel, maxEnergy);
        }
    }
    private float lastObservation;
    void UpdateObservations()
    {
        // Reset observations
        observations.Clear();
        //Debugoláshoz
        //RaycastHit rayHit;
        //if (Physics.Raycast(transform.position, transform.forward, out rayHit, 100))
        //{
        //    Vector3 incomingDirection = transform.forward;
        //    Vector3 reflectDirection = Vector3.Reflect(incomingDirection, rayHit.normal).normalized;
        //    Debug.DrawRay(transform.position + Vector3.up * 0.1f, reflectDirection * rayHit.distance, Color.blue);
        //}
        for (int i = 0; i < numberOfRaycasts; i++)
        {
            float angle = ((2 * i + 1 - numberOfRaycasts) * angleBetweenRaycasts / 2);
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 rayDirection = rotation * transform.forward;
            Vector3 rayStart = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, senseRadius))
            {

                ObservationData observationData;
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

                observations[angle] = observationData;
            }
            else
            {
                Debug.DrawRay(rayStart, rayDirection * senseRadius, Color.gray);
            }
        }
    }
    void MakeMovementDecision()
    {
        if (currentState == CreatureState.SearchingForFood)
        {
            float closestFoodDistance = float.MaxValue;
            Vector3 closestFoodPosition = Vector3.zero;
            GameObject closestFood = null;

            foreach (var observation in observations)
            {
                if (observation.Value.type == ObservationType.Food && observation.Value.distance < closestFoodDistance)
                {
                    if (observation.Value.observedObject == null)
                    {
                        continue;
                    }
                    if (IsFoodBlacklisted(observation.Value.observedObject.GetComponent<Food>())) // If the food is blacklisted we ignore it
                    {
                        continue;
                    }
                    closestFoodDistance = observation.Value.distance;
                    closestFoodPosition = observation.Value.observedObject.transform.position;
                    closestFood = observation.Value.observedObject;
                }
            }

            if (closestFoodDistance < float.MaxValue)
            {
                SetState(CreatureState.MovingToFood);
                objectTarget = closestFood;
                MoveAndFaceDirection(closestFoodPosition);
                return; // exit after making a decision
            }
            else
            {
                // Ha nincs elérhetõ élelem, váltson Wandering állapotba
                SetState(CreatureState.Wandering);
            }
        }
        Wander();
    }
    void FindMate()
    {
        GameObject potentialMate = null;
        float closestMateDistance = float.MaxValue;

        foreach (var observation in observations.Where(x => x.Value.type == ObservationType.Creature))
        {
            if (observation.Value.observedObject == null)
            {
                continue;
            }
            CreatureBehavior otherCreature = observation.Value.observedObject.transform.parent.GetComponent<CreatureBehavior>();
            if (otherCreature != null && otherCreature.IsReadyToMate() && otherCreature != this)
            {
                float distance = Vector3.Distance(transform.position, otherCreature.transform.position);
                if (distance < closestMateDistance)
                {
                    closestMateDistance = distance;
                    potentialMate = otherCreature.gameObject;
                }
            }
        }

        if (potentialMate != null)
        {
            CreatureBehavior otherCreature = potentialMate.GetComponent<CreatureBehavior>();
            MoveAndFaceDirection(potentialMate.transform.position);
            if (closestMateDistance <= 1f && otherCreature.IsReadyToMate()) // Feltételezve, hogy 1 egység a párosodási távolság
            {
                Mate(potentialMate.GetComponent<CreatureBehavior>());
            }
        }
        else
        {
            Wander(); // Ha nincs potenciális társ a közelben, akkor folytassa a vándorlást
        }
    }
    void Mate(CreatureBehavior mate)
    {
  
        CreatureBehavior offspringBehavior = creatureSpawner.SpawnCreature(gameObject.transform.position);

        // Öröklõdés és mutáció
        offspringBehavior.weight = InheritWithMutation(this.weight, mate.weight, 2);
        offspringBehavior.moveSpeed = InheritWithMutation(this.moveSpeed, mate.moveSpeed, 2);
        offspringBehavior.senseRadius = InheritWithMutation(this.senseRadius, mate.senseRadius, 5);
        ///TODO többi öröklése is
        // Reprodukciós várakozási idõ beállítása
        this.reproductionCooldown = REPRODUCTION_COOLDOWN;
        mate.reproductionCooldown = REPRODUCTION_COOLDOWN;

        // Állapotok visszaállítása
        this.currentState = CreatureState.Wandering;
        mate.currentState = CreatureState.Wandering;
    }

    float InheritWithMutation(float trait1, float trait2, float minvalue)
    {
        float inheritedTrait = UnityEngine.Random.value < 0.5f ? trait1 : trait2;

        // Mutáció esélye
        float mutationChance = 0.25f; // 25% esély a mutációra
        if (UnityEngine.Random.value < mutationChance)
        {
            float mutationAmount = UnityEngine.Random.Range(-0.2f, 0.2f); // A mutáció mértéke
            inheritedTrait += mutationAmount;
        }

        // Biztosítja, hogy az örökölt tulajdonság nagyobb legyen mint 0
        if (inheritedTrait < minvalue)
        {
            inheritedTrait += (minvalue - inheritedTrait);
        }

        return inheritedTrait;
    }

    void MoveAndFaceDirection(Vector3 targetPosition)
    {
        // Csak a vízszintes irányban mozogjunk
        Vector3 horizontalTargetPosition = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);

        // Mozgás a célpont felé
        transform.position = Vector3.MoveTowards(transform.position, horizontalTargetPosition, moveSpeed * Time.fixedDeltaTime);

        // Kiszámítjuk a mozgás irányát
        Vector3 moveDirection = (horizontalTargetPosition - transform.position).normalized;

        // Ha az irányvektor nem nulla nagyságú, akkor állítsuk be az egyed forgását
        if (moveDirection != Vector3.zero)
        {
            Quaternion newRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = newRotation;
        }
    }
    bool IsWithinGroundBounds(Vector3 position)
    {
        // Feltételezve, hogy a 'ground' egy téglalap alakú terület
        Collider groundCollider = ground.GetComponent<Collider>();
        if (groundCollider != null)
        {
            // Létrehozunk egy új Vector3 objektumot, amely csak a X és Z koordinátákat tartalmazza
            Vector3 groundLevelPosition = new Vector3(position.x, groundCollider.bounds.center.y, position.z);
            return groundCollider.bounds.Contains(groundLevelPosition);
        }
        return false;
    }
    public void BlacklistFood(Food food)
    {
        if (blacklistedFoods.ContainsKey(food))
        {
            blacklistedFoods[food] = Time.time + BLACKLIST_DURATION;
        }
        else
        {
            blacklistedFoods.Add(food, Time.time + BLACKLIST_DURATION);
        }
    }
    public bool IsFoodBlacklisted(Food food)
    {
        if (blacklistedFoods.TryGetValue(food, out float blacklistTime))
        {
            if (Time.time <= blacklistTime)
            {
                return true;  // Food is still within the blacklist duration
            }
            else
            {
                // Remove the food from the blacklist as its duration has expired
                blacklistedFoods.Remove(food);
            }
        }
        return false;
    }
    void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.CompareTag("Food") && (currentState == CreatureState.MovingToFood || currentState == CreatureState.SearchingForFood))
        {
            Food foodComponent = col.gameObject.GetComponentInChildren<Food>();

            if (IsFoodBlacklisted(foodComponent))
            {
                SetState(CreatureState.SearchingForFood);
                return;
            }
            if (foodComponent.isBeingEaten)
            {
                CreatureBehavior otherCreature = foodComponent.GetEatingCreature();
                if (otherCreature != null)
                {
                    if (this.weight > otherCreature.weight * 1.5f)
                    {
                        otherCreature.InterruptEating();
                    }
                    else
                    {
                        BlacklistFood(foodComponent);
                        SetState(CreatureState.SearchingForFood);
                        return;
                    }
                }
            }

            SetState(CreatureState.Eating);
            if (foodComponent == null)
            {
                Console.WriteLine();
            }
            eatingCoroutine = StartCoroutine(EatingRoutine(foodComponent, foodComponent.nutritionValue));
        }
        else if (col.gameObject.CompareTag("Obstacle"))
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, senseRadius))
            {
                //    Vector3 incomingDirection = transform.forward;
                //    Vector3 reflectDirection = Vector3.Reflect(incomingDirection, hit.normal).normalized;
                Vector3 reflectDirection = Vector3.Reflect(transform.forward, hit.normal).normalized;
                MoveAndFaceDirection(transform.position + reflectDirection);
            }
        }
    }
    IEnumerator EatingRoutine(Food foodComponent, float nutritionValue)
    {
        if (foodComponent.TryStartEating(this))
        {
            isEating = true;
            SetState(CreatureState.Eating);
            float energyPerSecond = nutritionValue / eatingDuration;
            float elapsedTime = 0f;

            while (elapsedTime < eatingDuration && isEating)
            {
                float energyThisFrame = energyPerSecond * Time.fixedDeltaTime;
                if (energyThisFrame +  energyLevel > maxEnergy)
                {
                    energyThisFrame = maxEnergy - energyLevel;
                }
                energyLevel += energyThisFrame;

                foodComponent.nutritionValue -= energyThisFrame;  // Reduce the nutrition value of the food

                elapsedTime += Time.fixedDeltaTime;
                if (energyLevel >= maxEnergy)
                { 
                    isEating = false;
                    break;
                }
                yield return null;
            }

            if (!isEating)
            {
                // If interrupted before finishing eating
                SetState(CreatureState.Wandering); // Set the creature state to fleeing
                //BlacklistFood(objectTarget.GetComponent<Food>());
                foodComponent.StopEating();
                Debug.Log("Eating Interrupted");
            }
            else
            {
                // If finished eating
                if (foodComponent.nutritionValue <= 0)
                {
                    if (foodComponent != null)
                    {
                        Destroy(foodComponent.gameObject);
                    }
                }
                objectTarget = null;
                SetState(CreatureState.Wandering);
            }
            isEating = false;
        }
    }
    private IEnumerator RemoveExpiredBlacklistedFoods()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f); // wait for 10 seconds

            List<Food> foodsToRemove = new List<Food>();

            foreach (var entry in blacklistedFoods)
            {
                if (entry.Value <= Time.time)
                {
                    foodsToRemove.Add(entry.Key);
                }
            }

            foreach (Food food in foodsToRemove)
            {
                blacklistedFoods.Remove(food);
            }
        }
    }
    public void InterruptEating()
    {
        if (eatingCoroutine != null)
        {
            if (objectTarget != null)
            {
                var food = objectTarget.GetComponent<Food>();
                BlacklistFood(food);
                food.StopEating();
            }
            isEating = false;
            SetState(CreatureState.SearchingForFood);
        }
    }
    private void Wander()
    {
        // Ellenõrizzük, hogy elérte-e az egyed a célpontot, vagy ha nincs jelenlegi célpont
        if (wanderTarget == Vector3.zero || Vector3.Distance(transform.position, wanderTarget) < 1f)
        {
            ChooseNewWanderTarget();
        }

        MoveAndFaceDirection(wanderTarget);
    }
    private void ChooseNewWanderTarget()
    {
        float wanderRadius = UnityEngine.Random.Range(3f, 15f);
        Vector3 randomDirection;
        do
        {
            randomDirection = UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            randomDirection.y = transform.position.y;
        } while (!IsWithinGroundBounds(randomDirection));

        Vector3 forward = transform.forward;
        forward.y = 0;
        Vector3 toRandomDirection = randomDirection - transform.position;
        toRandomDirection.y = 0;

        if (Vector3.Angle(forward, toRandomDirection) > 30f)
        {
            toRandomDirection = Vector3.RotateTowards(forward, toRandomDirection, Mathf.Deg2Rad * 30f, 0f);
            randomDirection = transform.position + toRandomDirection.normalized * wanderRadius;
        }

        if (IsPathClear(toRandomDirection) && IsWithinGroundBounds(randomDirection))
        {
            wanderTarget = randomDirection;
        }
        else
        {
            wanderTarget = GetAlternativeDirection();
        }
    }
    private bool IsPathClear(Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction.normalized, out hit, 1f))
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

        // Keressük meg a legkisebb szögben álló akadályt
        foreach (var observation in observations)
        {
            if (observation.Value.type == ObservationType.Obstacle)
            {
                Vector3 toObstacle = observation.Value.observedObject.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, toObstacle);

                if (angle < minAngle)
                {
                    minAngle = angle;
                    closestObstacle = observation.Value;
                }
            }
        }

        if (closestObstacle.observedObject != null)
        {
            // Számítsuk ki a visszaverõdési irányt
            RaycastHit hit;
            if (Physics.Raycast(transform.position, closestObstacle.observedObject.transform.position - transform.position, out hit))
            {
                Vector3 incomingVec = hit.point - transform.position;
                Vector3 reflectVec = Vector3.Reflect(incomingVec, hit.normal);
                Vector3 newDirection = transform.position + reflectVec.normalized * (2f);
                newDirection.y = transform.position.y; // Állítsd be a Y koordinátát az egyed jelenlegi magasságára
                return newDirection;
            }
        }

        Vector3 randomDirection;
        do
        {
            randomDirection = transform.position + UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(3f, 15f);
            randomDirection.y = transform.position.y; // Állítsd be a Y koordinátát az egyed jelenlegi magasságára
        } while (!IsWithinGroundBounds(randomDirection)); // Megfordítottuk a feltételt

        return randomDirection;
    }
    void SetState(CreatureState newState)
    {
        //Debug.Log($"{this.GetInstanceID()}new state{newState.ToString()}");
        currentState = newState;
    }
    protected bool IsReadyToMate()
    {
        // Itt határozd meg a szaporodási képesség feltételeit
        var requiredEnergy = matingEnergyThreshold  *  maxEnergy;
        return energyLevel > requiredEnergy && age > matingAge;
    }
    void IncreaseAge()
    {
        age += UPDATE_INTERVAL; // Növeld az életkort minden frame-ben
        if (reproductionCooldown > 0f)
        {
            reproductionCooldown -= UPDATE_INTERVAL;
        }
    }
}