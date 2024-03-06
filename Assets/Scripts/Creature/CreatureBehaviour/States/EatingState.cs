
using System.Collections;
using UnityEngine;

internal class EatingState : ICreatureState
{
    private readonly CreatureBehaviour creature;
    public bool isEating = false;

    public EatingState(CreatureBehaviour creatureBehaviour)
    {
        this.creature = creatureBehaviour;
    }

    public CreatureStateType StateType => CreatureStateType.Eating;

    public void EnterState()
    {
    }

    public void ExitState()
    {
        isEating = false;
        creature.coroutineRunner.StopCoroutine(creature.EatingManager.eatingCoroutine);
        creature.EatingManager.eatingCoroutine = null;
        creature.EatingManager.foodTarget = null;
    }

    public void UpdateState()
    {
    }

    public void StartEatingCoroutine(Food food)
    {
        creature.EatingManager.eatingCoroutine = creature.coroutineRunner.StartCoroutine(EatingRoutine(food));
    }

    IEnumerator EatingRoutine(Food foodTarget)
    {
        if (foodTarget.TryStartEating(creature))
        {
            creature.EatingManager.foodTarget = foodTarget;
            isEating = true;
            float elapsedTime = 0f;
            while (elapsedTime < creature.config.eatingDuration && isEating)
            {
                ProcessConsumptionInThisInterval(foodTarget);
                elapsedTime += Time.fixedDeltaTime;
                if (creature.EnergyManagement.EnergyLevel >= creature.maxEnergy)
                {
                    isEating = false;
                    break;
                }
                yield return null;
            }

            if (!isEating)
            {
                creature.stateMachine.TransitionToWandering();
                foodTarget.StopEating();
                Debug.Log($"Eating Interrupted");
            }
            else
            {
                // If finished eating
                if (foodTarget != null && foodTarget.nutritionValue < 1f)
                {
                    foodTarget.DestroyOnDepletion();
                }
                creature.EatingManager.foodTarget = null;
                creature.stateMachine.TransitionToIdle();
            }
        }
    }

    private void ProcessConsumptionInThisInterval(Food foodTarget)
    {
        float nutritionValue = foodTarget.nutritionValue;
        float energyPerSecond = nutritionValue / creature.config.eatingDuration;
        float energyDesiredThisFrame = energyPerSecond * Time.fixedDeltaTime;
        if (energyDesiredThisFrame + creature.EnergyManagement.EnergyLevel > creature.maxEnergy)
        {
            energyDesiredThisFrame = creature.maxEnergy - creature.EnergyManagement.EnergyLevel;
        }
        var actualNutritionValue = foodTarget.GetMaxNutrition(energyDesiredThisFrame);
        creature.EnergyManagement.GainEnergy(actualNutritionValue);
        foodTarget.ConsumeNutrition(actualNutritionValue);
    }
}
