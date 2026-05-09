using UnityEngine;

public class Food : MonoBehaviour
{
    private const float StartScale = 0.1f;

    public float nutritionValue = 10f;
    private float maxNutritionValue = 10f;
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
    private BaseCreatureBehaviour eatingCreature = null;
    private Renderer _renderer;
    private Color foodColor;

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
        maxNutritionValue = targetNutrition;
        age = 0f;
        transform.localScale = Vector3.one * StartScale;
        nutritionValue = targetNutrition * StartScale;
        UpdateColor();
    }

    void FixedUpdate()
    {
        age += Time.fixedDeltaTime;

        if (!IsBeingEaten)
            ApplyGrowth();

        if (eatingCreature == null && age > GameConfig.Instance.foodMaxAge && !IsBeingEaten)
            DestroyObject();
    }

    private void ApplyGrowth()
    {
        float t = Mathf.Clamp01(age / GameConfig.Instance.foodMaturityAge);
        float smooth = Mathf.SmoothStep(0f, 1f, t);
        transform.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, smooth);
        nutritionValue = Mathf.Lerp(maxNutritionValue * StartScale, maxNutritionValue, smooth);
        UpdateColor();
    }

    private void UpdateColor()
    {
        float range = GameConfig.Instance.maxNutrionValue - GameConfig.Instance.minNutrionValue;
        float t = range > 0 ? (nutritionValue - GameConfig.Instance.minNutrionValue) / range : 0f;
        FoodColor = Color.Lerp(Color.green, Color.yellow, Mathf.Clamp01(t));
    }

    private void DestroyObject()
    {
        PoolManager.Instance.ReturnObject(FoodSpawner.Instance.foodPrefab, this.gameObject);
        FoodSpawner.Instance.RemoveFromList(this);
    }

    public bool TryStartEating(BaseCreatureBehaviour creature)
    {
        if (!IsBeingEaten)
        {
            IsBeingEaten = true;
            eatingCreature = creature;
            return true;
        }
        else if (eatingCreature != null)
        {
            if (creature.Weight >= eatingCreature.Weight * GameConfig.Instance.sizeDifferentFactor)
            {
                eatingCreature.EatingManager.InterruptEating();
                eatingCreature = creature;
                IsBeingEaten = true;
                return true;
            }
            else
            {
                creature.EatingManager.BlacklistFood(this);
            }
        }
        return false;
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
        FoodSpawner.Instance.RemoveFromList(this);
    }

    public void DestroyOnDepletion()
    {
        DestroyObject();
    }
}
