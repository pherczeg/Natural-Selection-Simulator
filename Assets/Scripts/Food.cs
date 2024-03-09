using UnityEngine;

public class Food : MonoBehaviour
{
    public static float minNutrionValue = 50f;
    public static float maxNutrionValue = 100f;
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
                FoodColor = Color.Lerp(Color.green, Color.red, (nutritionValue - minNutrionValue) / maxNutrionValue);
            }
        }
    }
    public float age = 0f;
    private CreatureBehaviour eatingCreature = null;
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
    void Start()
    {
        _renderer = GetComponentInChildren<Renderer>();

        nutritionValue = Random.Range(minNutrionValue, maxNutrionValue);

        Color foodColor = Color.Lerp(Color.green, Color.red, (nutritionValue - minNutrionValue) / maxNutrionValue);
        _renderer.material.color = foodColor;
    }

    void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (eatingCreature == null && age > maxAge && !IsBeingEaten)
        {
            Destroy(this.gameObject);
        }

    }
    public bool TryStartEating(CreatureBehaviour creature)
    {
        if (!IsBeingEaten)
        {
            IsBeingEaten = true;
            eatingCreature = creature;
            return true;
        }
        else if (eatingCreature != null) 
        {
            if (creature.Weight >= eatingCreature.Weight * creature.config.sizeDifferentFactor) 
            { 
                eatingCreature.EatingManager.InterruptEating();
                eatingCreature = creature;
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

    public CreatureBehaviour GetEatingCreature()
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

    public void DestroyOnDepletion()
    {
        Destroy(this.gameObject);
    }
}
