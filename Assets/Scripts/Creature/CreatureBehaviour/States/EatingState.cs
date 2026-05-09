
using System.Collections;
using UnityEngine;

internal class EatingState : CreatureStateBase
{
    public bool isEating = false;

    public EatingState(BaseCreatureBehaviour creatureBehaviour) : base(creatureBehaviour, CreatureStateType.Eating) { }

    public override void EnterState()
    {
        base.EnterState();
    }

    public override void ExitState()
    {
        isEating = false;
        if (creature.EatingManager.eatingCoroutine != null) 
        { 
            creature.coroutineRunner.StopCoroutine(creature.EatingManager.eatingCoroutine);
        }
        creature.EatingManager.eatingCoroutine = null;
        creature.EatingManager.foodTarget = null;
        base.ExitState();
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

    private static readonly WaitForFixedUpdate WaitForFixedUpdateCached = new WaitForFixedUpdate();

    IEnumerator EatingRoutine(Food foodTarget)
    {
        if (foodTarget == null)
        {
            isEating = false;
            yield break;
        }

        if (!foodTarget.TryStartEating(creature))
        {
            isEating = false;
            yield break;
        }

        creature.EatingManager.foodTarget = foodTarget;
        isEating = true;

        while (isEating &&
               foodTarget != null &&
               foodTarget.nutritionValue > 0f &&
               creature.EnergyManager.EnergyLevel < creature.maxEnergy)
        {
            ProcessConsumptionInThisInterval(foodTarget);

            yield return WaitForFixedUpdateCached;
        }

        if (foodTarget != null)
        {
            foodTarget.StopEating();

            if (foodTarget.nutritionValue < 1f)
            {
                foodTarget.DestroyOnDepletion();
            }
        }

        creature.EatingManager.foodTarget = null;
        isEating = false;

        creature.stateMachine.TransitionToWandering();
    }

    private void ProcessConsumptionInThisInterval(Food foodTarget)
    {
        float energyDesiredThisFrame = GameConfig.Instance.nutritionConsumptionRatePerSecond * Time.fixedDeltaTime;
        if (energyDesiredThisFrame + creature.EnergyManager.EnergyLevel > creature.maxEnergy)
        {
            energyDesiredThisFrame = creature.maxEnergy - creature.EnergyManager.EnergyLevel;
        }
        var actualNutritionValue = foodTarget.GetMaxNutrition(energyDesiredThisFrame);
        creature.EnergyManager.GainEnergy(actualNutritionValue);
        foodTarget.ConsumeNutrition(actualNutritionValue);
    }
}
