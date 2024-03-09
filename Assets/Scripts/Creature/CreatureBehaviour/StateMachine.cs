using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class StateMachine
{
    public ICreatureState CurrentState { get; private set; }
    private readonly CreatureBehaviour creatureBehaviour;

    private readonly IdleState idleState;
    private readonly SearchingForFoodState searchingForFoodState;
    private readonly SearchingForMateState searchingForMateState;
    private readonly MovingToFoodState movingToFoodState;
    private readonly MovingToMateState movingToMateState;
    private readonly WanderingState wanderingState;
    private readonly EatingState eatingState;
    private readonly ReproductionState reproductionState;

    public StateMachine(CreatureBehaviour creatureBehaviour)
    {
        idleState = new IdleState(creatureBehaviour);
        searchingForFoodState = new SearchingForFoodState(creatureBehaviour);
        movingToFoodState = new MovingToFoodState(creatureBehaviour);
        wanderingState = new WanderingState(creatureBehaviour);
        searchingForMateState  = new SearchingForMateState(creatureBehaviour);
        eatingState = new EatingState(creatureBehaviour);
        movingToMateState = new MovingToMateState(creatureBehaviour);
        reproductionState = new ReproductionState(creatureBehaviour);
        // Kezdõ állapot beállítása
        SetState(idleState);
    }
    public void SetState(ICreatureState newState)
    {
        CurrentState?.ExitState();
        CurrentState = newState;
        CurrentState.EnterState();
    }

    public void Update()
    {
        CurrentState?.UpdateState();
    }

    public void TransitionToIdle()
    {
        SetState(idleState);
    }
    public void TransitionToEating(Food food)
    {
        SetState(eatingState);
        eatingState.StartEatingCoroutine(food);
    }

    public void TransitionToMovingToFood(GameObject food)
    {
        SetState(movingToFoodState);
        movingToFoodState.SetTarget(food);
    }

    public void TransitionToSearchingForFood()
    {
        SetState(searchingForFoodState);
    }
    public void TransitionToSearchingForMate()
    {
        SetState(searchingForMateState);
    }

    public void TransitionToMovingToMate(GameObject target)
    {
        SetState(movingToMateState);
        movingToMateState.SetTarget(target);
    }
    public void TransitionToWandering()
    {
        SetState(wanderingState);
    }
    public  void TransitionToReproductionState(CreatureBehaviour otherCreature, bool isGivingBirth)
    {
        SetState(reproductionState);
        if (isGivingBirth)
        {
            reproductionState.StartReproductionCoroutine(otherCreature);
        }
    }
}

