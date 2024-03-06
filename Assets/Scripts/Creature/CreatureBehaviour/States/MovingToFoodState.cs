using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingToFoodState : ICreatureState
{
    private readonly CreatureBehaviour creature;
    private GameObject targetFood; // A célzott étel objektum

    public MovingToFoodState(CreatureBehaviour creature)
    {
        this.creature = creature;
    }
    public CreatureStateType StateType => CreatureStateType.MovingToFood;
    public void EnterState()
    {
        Debug.Log($"Creature is moving to food.");
        // Itt lehet beállítani a mozgás kezdõ paramétereit, például a cél irányát
    }

    public void UpdateState()
    {
        if (targetFood != null)
        {
            if (ReachedFood())
            {
                // Amennyiben elérte az ételt, váltson az evés állapotra
                var foodComponent = targetFood.GetComponent<Food>();
                creature.stateMachine.TransitionToEating(foodComponent);
            }
            else
            { 
                MoveTowardsFood();
            }
        }
        else
        {
            creature.stateMachine.TransitionToIdle();
        }
    }

    public void ExitState()
    {
        Debug.Log($"Creature stops moving to food.");
        // Itt lehetõség van a mozgással kapcsolatos beállítások visszaállítására
    }
    public void SetTarget(GameObject targetFood)
    {
        this.targetFood = targetFood;
    }
    private void MoveTowardsFood()
    {
        creature.MovementManager.MoveTowards(targetFood.transform.position);
    }

    private bool ReachedFood()
    {
        // Ellenõrzi, hogy a lény elérte-e az ételt
        // Ez a logika függ a játék konkrét megvalósításától
        return Vector3.Distance(creature.transform.position, targetFood.transform.position) < 1.0f; // Példa érték
    }
}
