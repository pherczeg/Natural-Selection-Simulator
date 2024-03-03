using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Food : MonoBehaviour
{
    public static float minNutrionValue = 50f;
    public static float maxNutrionValue = 100f;
    public static float maxAge = 20f;

    public float nutritionValue = 10f;
    public bool isBeingEaten = false;
    public float age = 0f;
    private CreatureBehaviour eatingCreature = null;
    private Renderer _renderer;
    // Start is called before the first frame update
    void Start()
    {
        _renderer = GetComponentInChildren<Renderer>();

        // Assign a random nutrition value
        nutritionValue = Random.Range(minNutrionValue, maxNutrionValue);

        // Lerp between green and red based on the nutrition value
        Color foodColor = Color.Lerp(Color.green, Color.red, (nutritionValue - minNutrionValue) / maxNutrionValue);
        _renderer.material.color = foodColor;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (eatingCreature == null && age > maxAge)
        {
            Destroy(this.gameObject);
        }
        else if (eatingCreature != null && isBeingEaten == true)
            isBeingEaten=false;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool TryStartEating(CreatureBehaviour creature)
    {
        if (!isBeingEaten)
        {
            isBeingEaten = true;
            eatingCreature = creature;
            return true;
        }
        return false;
    }

    public void StopEating()
    {
        isBeingEaten = false;
        eatingCreature = null;
    }

    public CreatureBehaviour GetEatingCreature()
    {
        return eatingCreature;
    }

    public void UpdateNutrition()
    {
    }
}
