using UnityEngine.Analytics;
using UnityEngine;
using System.Collections;

public abstract class Creature : MonoBehaviour
{
    private const float initialEnergyPercentage = 0.5f;

    public float moveSpeed = 5f;
    public float weight = 1f;
    public float energyLevel;
    public float maxEnergy = 100f;
    public float age = 0;
    public Gender gender;

    public Material energyBarMaterial;
    public GameObject energyBarObject;
    private EnergyBar energyBar;


    public CreatureState currentState = CreatureState.Idle;
    private Vector3 wanderTarget = Vector3.zero;

    protected virtual void Start()
    {
        energyLevel = initialEnergyPercentage * maxEnergy;
        if (energyBarObject)
        {
            energyBar = energyBarObject.GetComponent<EnergyBar>();
        }
        StartCoroutine(UpdateRoutine());
    }

    // protected virtual void Update()
    // {
    //     IncreaseAge();
    //     EnergyManagement();
    // }

    private IEnumerator UpdateRoutine()
    {
        while (true)
        {
            IncreaseAge();
            EnergyManagement();

            yield return new WaitForSeconds(1.0f); // Másodpercenként frissít
        }
    }

    protected void IncreaseAge()
    {
        age += Time.fixedDeltaTime;
    }

    protected void EnergyManagement()
    {
        float energyConsumption = (0.5f * weight * moveSpeed * moveSpeed * Time.fixedDeltaTime) * 0.05f; //E(movement) = (1/2*m*v2) but devide by 10 to normalize 
        energyLevel -= energyConsumption;

        UpdateEnergyBar();
    }

    protected void UpdateEnergyBar()
    {
        if (energyBar)
        {
            energyBar.SetEnergy(energyLevel, maxEnergy);
        }
    }

    // További közös metódusok...
}
