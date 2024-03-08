
using System.Collections;
using UnityEngine;

internal class EatingState : CreatureStateBase
{
    public bool isEating = false;

    public EatingState(CreatureBehaviour creatureBehaviour) : base(creatureBehaviour, CreatureStateType.Eating) { }

    public override void EnterState()
    {
        base.EnterState();
    }

    public override void ExitState()
    {
        base.ExitState();
        isEating = false;
        if (creature.EatingManager.eatingCoroutine != null) 
        { 
            creature.coroutineRunner.StopCoroutine(creature.EatingManager.eatingCoroutine);
        }
        creature.EatingManager.eatingCoroutine = null;
        creature.EatingManager.foodTarget = null;
    }

    public override void UpdateState()
    {
        if (!isEating)
            creature.stateMachine.TransitionToWandering();
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
            while (isEating && foodTarget.nutritionValue >0)
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
                Debug.Log($"{creature.GetInstanceID()}Eating Interrupted");
            }
            else
            {
                foodTarget.IsBeingEaten = false;
                // If finished eating
                if (foodTarget != null && foodTarget.nutritionValue < 1f)
                {
                    foodTarget.DestroyOnDepletion();
                }
                creature.EatingManager.foodTarget = null;
                creature.stateMachine.TransitionToWandering();
            }
        }
        isEating = false;
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
