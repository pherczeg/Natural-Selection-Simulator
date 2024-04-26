using UnityEngine;

public class Food : MonoBehaviour
{
    public static float maxAge = 20f;

    public float nutritionValue = 10f;
    private bool isBeingEaten = false;
    public bool IsBeingEaten
    {
        get { return isBeingEaten; }
        set 
        { 
            isBeingEaten = value;
            if (isBeingEaten ) 
            {
                FoodColor = Color.red;
            }
            else
            {
                FoodColor = Color.Lerp(Color.green, Color.red, (nutritionValue - GameConfig.Instance.minNutrionValue) / GameConfig.Instance.maxNutrionValue);
            }
        }
    }
    public float age = 0f;
    private BaseCreatureBehaviour eatingCreature = null;
    private Renderer _renderer;
    private Color foodColor;
    Color FoodColor 
    {
        get 
        {
            return foodColor;
        }
        set 
        { 
            foodColor = value;
            if (_renderer != null ) 
            {
                _renderer.material.color = foodColor; 
            }
        }
    }
    void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        Color foodColor = Color.Lerp(Color.green, Color.red, (nutritionValue - GameConfig.Instance.minNutrionValue) / GameConfig.Instance.maxNutrionValue);
        _renderer.material.color = foodColor;
    }

    void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (eatingCreature == null && age > maxAge && !IsBeingEaten)
        {
            DestroyObject();
        }

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

    public BaseCreatureBehaviour GetEatingCreature()
    {
        return eatingCreature;
    }

    public void ConsumeNutrition(float value)
    {
        if (IsBeingEaten)
        {
            nutritionValue -= value;
        }
    }
    public float GetMaxNutrition(float desiredValue)
    {
        if (IsBeingEaten)
        {
            if (desiredValue  <= nutritionValue)
            {
                return desiredValue;
            }
            else 
            {
                var overFlow = desiredValue -nutritionValue;
                return nutritionValue+overFlow; 
            }
        }
        else 
        { 
            return 0; 
        }   
    }
    private void OnDestroy()
    {
        FoodSpawner.Instance.RemoveFromList(this);
    }
    public void DestroyOnDepletion()
    {
        //Destroy(this.gameObject);
        DestroyObject();
        //PoolManager.Instance.ReturnObject(FoodSpawner.Instance.foodPrefab, this.gameObject);
    }
}
