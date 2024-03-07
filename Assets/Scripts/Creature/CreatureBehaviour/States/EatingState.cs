
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
                if (creature.EnergyManager.EnergyLevel >= creature.maxEnergy)
                {
                    isEating = false;
                    break;
                }
                yield return null;
            }

            if (!isEating)
            {
                foodTarget.StopEating();
                if (foodTarget != null && foodTarget.nutritionValue < 1f)
                {
                    foodTarget.DestroyOnDepletion();
                }
                creature.stateMachine.TransitionToWandering();
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
                creature.stateMachine.TransitionToWandering();
            }
        }
    }

    private void ProcessConsumptionInThisInterval(Food foodTarget)
    {
        float energyDesiredThisFrame = creature.config.nutritionConsumptionRatePerSecond * Time.fixedDeltaTime;
        if (energyDesiredThisFrame + creature.EnergyManager.EnergyLevel > creature.maxEnergy)
        {
            energyDesiredThisFrame = creature.maxEnergy - creature.EnergyManager.EnergyLevel;
        }
        var actualNutritionValue = foodTarget.GetMaxNutrition(energyDesiredThisFrame);
        creature.EnergyManager.GainEnergy(actualNutritionValue);
        foodTarget.ConsumeNutrition(actualNutritionValue);
    }
}
