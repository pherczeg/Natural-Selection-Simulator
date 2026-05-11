using UnityEngine;
using Unity.Mathematics;

public enum FoodDespawnReason
{
    Unknown = 0,
    Depletion = 1,
    OldAge = 2
}

public class Food : MonoBehaviour
{
    public float nutritionValue = 10f;
    private float maxNutritionValue = 10f;
    public float MaxNutritionValue => maxNutritionValue;
    private bool isBeingEaten = false;
    public bool IsBeingEaten
    {
        get { return isBeingEaten; }
        set
        {
            isBeingEaten = value;
            if (isBeingEaten)
                FoodColor = Color.red;
            else
                UpdateColor();
        }
    }
    public float age = 0f;
    public float GrowthFraction { get; private set; }
    private BaseCreatureBehaviour eatingCreature = null;
    private Renderer _renderer;
    private Color foodColor;
    private bool despawnQueued;
    public bool IsDespawnQueued => despawnQueued;

    Color FoodColor
    {
        get => foodColor;
        set
        {
            foodColor = value;
            if (_renderer != null)
                _renderer.material.color = foodColor;
        }
    }

    void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
    }

    public void Initialize(float targetNutrition)
    {
        despawnQueued = false;
        maxNutritionValue = targetNutrition;
        age = 0f;
        GrowthFraction = 0f;
        transform.localScale = Vector3.one * FoodLifecycleCalculator.StartScale;
        nutritionValue = targetNutrition * FoodLifecycleCalculator.StartScale;
        UpdateColor();
    }

    void FixedUpdate()
    {
        if (despawnQueued)
            return;

        GameConfig config = GameConfig.Instance;
        if (config == null || config.useEcsFoodLifecycle)
            return;

        age += Time.fixedDeltaTime;

        if (!IsBeingEaten)
            ApplyGrowth(config);

        if (eatingCreature == null && age > config.foodMaxAge && !IsBeingEaten)
            RequestDespawn(FoodDespawnReason.OldAge);
    }

    private void ApplyGrowth(GameConfig config)
    {
        GrowthFraction = FoodLifecycleCalculator.CalculateGrowthFraction(age, config.foodMaturityAge);
        transform.localScale = Vector3.one * FoodLifecycleCalculator.CalculateScale(GrowthFraction);
        nutritionValue = FoodLifecycleCalculator.CalculateNutritionValue(maxNutritionValue, GrowthFraction);
        UpdateColor(config);
    }

    private void UpdateColor()
    {
        UpdateColor(GameConfig.Instance);
    }

    private void UpdateColor(GameConfig config)
    {
        if (config == null)
            return;

        float range = config.maxNutrionValue - config.minNutrionValue;
        float t = range > 0 ? (nutritionValue - config.minNutrionValue) / range : 0f;
        FoodColor = Color.Lerp(Color.green, Color.yellow, Mathf.Clamp01(t));
    }

    public void ApplyECSLifecycle(FoodMirrorData data, bool applyGrowthVisuals)
    {
        age = data.age;
        GrowthFraction = data.growthFraction;
        maxNutritionValue = data.maxNutritionValue;
        nutritionValue = Mathf.Max(0f, data.nutritionValue);

        if (applyGrowthVisuals)
            transform.localScale = ToVector3(data.scale);

        if (IsBeingEaten)
            FoodColor = Color.red;
        else
            UpdateColor();
    }

    private void DestroyObject()
    {
        PoolManager.Instance.ReturnObject(FoodSpawner.Instance.foodPrefab, this.gameObject);
        FoodSpawner.Instance.RemoveFromList(this);
    }

    public bool TryStartEating(BaseCreatureBehaviour creature)
    {
        if (despawnQueued)
            return false;

        if (creature == null)
            return false;

        if (!IsBeingEaten)
        {
            IsBeingEaten = true;
            eatingCreature = creature;
            return true;
        }

        if (eatingCreature == null)
        {
            IsBeingEaten = true;
            eatingCreature = creature;
            return true;
        }

        if (eatingCreature == creature)
            return true;

        if (TryResolveHerbivoreFoodCompetition(creature, eatingCreature, out bool challengerCanEat, out bool shouldTakeOver))
        {
            if (!challengerCanEat)
            {
                creature.EatingManager?.BlacklistFood(this);
                return false;
            }

            if (!shouldTakeOver)
            {
                return true;
            }

            if (eatingCreature != null && !eatingCreature.IsDespawnQueued)
            {
                eatingCreature.EatingManager?.InterruptEating();
            }

            eatingCreature = creature;
            IsBeingEaten = true;
            return true;
        }

        if (creature.Weight >= eatingCreature.Weight * GameConfig.Instance.sizeDifferentFactor)
        {
            eatingCreature.EatingManager.InterruptEating();
            eatingCreature = creature;
            IsBeingEaten = true;
            return true;
        }

        creature.EatingManager?.BlacklistFood(this);
        return false;
    }

    private bool TryResolveHerbivoreFoodCompetition(
        BaseCreatureBehaviour challenger,
        BaseCreatureBehaviour currentOwner,
        out bool challengerCanEat,
        out bool shouldTakeOver)
    {
        challengerCanEat = false;
        shouldTakeOver = false;

        if (!(challenger is HerbivoreBehaviour challengerHerbivore) ||
            !(currentOwner is HerbivoreBehaviour ownerHerbivore))
        {
            return false;
        }

        if (!challengerHerbivore.IsHawk && !ownerHerbivore.IsHawk)
        {
            challengerCanEat = true;
            shouldTakeOver = false;
            return true;
        }

        if (!challengerHerbivore.IsHawk && ownerHerbivore.IsHawk)
        {
            challengerCanEat = false;
            shouldTakeOver = false;
            return true;
        }

        if (challengerHerbivore.IsHawk && !ownerHerbivore.IsHawk)
        {
            challengerCanEat = true;
            shouldTakeOver = true;
            return true;
        }

        BaseCreatureBehaviour winner = HerbivoreSocialDynamics.ResolveHawkFight(challenger, currentOwner);
        nutritionValue = Mathf.Max(0f, nutritionValue * HerbivoreSocialDynamics.HawkFightFoodRetentionFactor);

        if (winner == challenger)
        {
            challengerCanEat = !challenger.IsDespawnQueued;
            shouldTakeOver = challengerCanEat;
            return true;
        }

        if (winner == currentOwner)
        {
            challengerCanEat = false;
            shouldTakeOver = false;
            return true;
        }

        StopEating();
        challengerCanEat = false;
        shouldTakeOver = false;
        return true;
    }

    public void StopEating()
    {
        IsBeingEaten = false;
        eatingCreature = null;
    }

    public BaseCreatureBehaviour GetEatingCreature() => eatingCreature;

    public void ConsumeNutrition(float value)
    {
        if (IsBeingEaten)
        {
            nutritionValue -= value;
            if (nutritionValue < 0) nutritionValue = 0;
        }
    }

    public float GetMaxNutrition(float desiredValue)
    {
        if (IsBeingEaten)
            return desiredValue <= nutritionValue ? desiredValue : nutritionValue;
        return 0;
    }

    private void OnDestroy()
    {
        FoodSpawner.Instance?.RemoveFromList(this);
    }

    public void DestroyOnDepletion()
    {
        RequestDespawn(FoodDespawnReason.Depletion);
    }

    public void DestroyFromECSLifecycle()
    {
        RequestDespawn(FoodDespawnReason.OldAge);
    }

    internal void CompleteDespawnFromBridge(FoodDespawnReason reason)
    {
        DestroyObject();
    }

    private void RequestDespawn(FoodDespawnReason reason)
    {
        if (despawnQueued || !gameObject.activeInHierarchy)
            return;

        despawnQueued = true;
        if (ECSMirrorBridge.TryRequestDespawnFood(this, reason))
            return;

        CompleteDespawnFromBridge(reason);
    }

    private static Vector3 ToVector3(float3 source)
    {
        return new Vector3(source.x, source.y, source.z);
    }
}
