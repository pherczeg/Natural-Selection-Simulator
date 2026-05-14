using System;
using System.Collections.Generic;
using UnityEngine;

public enum CreatureSex
{
    Female,
    Male
}

public abstract class BaseCreatureBehaviour : MonoBehaviour
{
    private static readonly IReadOnlyList<UtilityActionScore> EmptyUtilityAIActionScores = new List<UtilityActionScore>(0);
    private static CreatureSpawner cachedCreatureSpawner;

    [SerializeField,TextArea]
    public string DEBUG_string;
    [SerializeField] protected CreatureUtilityBrain utilityBrain;
    [SerializeField] private CreatureUtilityBehaviorData utilityBehaviorProfile = new CreatureUtilityBehaviorData
    {
        keepCurrentStateWeight = UtilityBehaviorScoring.DefaultWeight,
        foodActionWeight = UtilityBehaviorScoring.DefaultWeight,
        searchMateWeight = UtilityBehaviorScoring.DefaultWeight,
        wanderWeight = UtilityBehaviorScoring.DefaultWeight
    };
    [SerializeField, TextArea(2, 4)] private string utilityAIDebugString;
    [SerializeField] private CreatureAction lastUtilityAISelectedAction = CreatureAction.None;
    [SerializeField] private CreatureStateType lastUtilityAITransitionStateType = CreatureStateType.None;
    [SerializeField] private CreatureStateType lastUtilityAIResultStateType = CreatureStateType.None;
    private float age;
    protected float weight;
    protected int numberOfRaycasts = 30;
    protected float angleBetweenRaycasts = 5f;
    private float matingCooldown;
    protected CreatureSpawner creatureSpawner;
    protected float lastObservation = 0f;
    private bool despawnQueued;
    private float utilityDecisionPhase = -1f;

    public GameObject energyBarObject;
    public float maxEnergy;
    public float Weight => weight;
    public CreatureSex Sex { get; private set; }
    public MovementManager MovementManager { get; set; }
    public AgeManager AgeManager { get; set; }
    public EnergyManager EnergyManager { get; set; }
    public ObservationManager ObservationManager { get; set; }
    public ReproductionManager ReproductionManager { get; set; }
    public EatingManager EatingManager { get; set; }
    public CreatureUtilityBrain UtilityBrain => utilityBrain;
    public UtilityDecision LastUtilityAIDecision => utilityBrain != null ? utilityBrain.LastDecision : UtilityDecision.Empty;
    public IReadOnlyList<UtilityActionScore> LastUtilityAIActionScores => utilityBrain != null ? utilityBrain.LastActionScores : EmptyUtilityAIActionScores;
    public string LastUtilityAISelectedActionId => utilityBrain != null ? utilityBrain.LastSelectedActionId : string.Empty;
    public CreatureAction LastUtilityAISelectedAction => utilityBrain != null ? utilityBrain.LastSelectedCreatureAction : lastUtilityAISelectedAction;
    public CreatureUtilityBehaviorData UtilityBehaviorProfile => utilityBehaviorProfile;
    public CreatureStateType LastUtilityAISelectedStateType => lastUtilityAITransitionStateType;
    public CreatureStateType LastUtilityAITransitionStateType => lastUtilityAITransitionStateType;
    public CreatureStateType LastUtilityAIResultStateType => lastUtilityAIResultStateType;
    public float LastUtilityAISelectedScore => utilityBrain != null ? utilityBrain.LastSelectedScore : 0f;
    public float LastUtilityAIDecisionTime => utilityBrain != null ? utilityBrain.LastDecisionTime : -1f;
    public string UtilityAIDebugString => utilityAIDebugString;
    public bool IsDespawnQueued => despawnQueued;

    public CreatureStateType CurrentStateType
    {
        get { return CreatureActionStatusAdapter.GetLegacyStateType(this); }
    }
    public CoroutineRunner coroutineRunner;
    void Awake()
    {
        creatureSpawner = CreatureSpawner.Instance != null
            ? CreatureSpawner.Instance
            : cachedCreatureSpawner;

        if (creatureSpawner == null)
        {
            creatureSpawner = FindObjectOfType<CreatureSpawner>();
            cachedCreatureSpawner = creatureSpawner;
        }
    }

    public void SetSex(CreatureSex sex)
    {
        Sex = sex;
        OnSexChanged();
    }

    public void SetUtilityBehaviorProfile(CreatureUtilityBehaviorData profile)
    {
        utilityBehaviorProfile = UtilityBehaviorScoring.Sanitize(profile);
    }

    public void RandomizeUtilityBehaviorProfile(GameConfig config)
    {
        utilityBehaviorProfile = UtilityBehaviorGenetics.CreateRandomProfile(config);
    }

    protected void EnsureUtilityBehaviorProfileInitialized()
    {
        utilityBehaviorProfile = UtilityBehaviorScoring.Sanitize(utilityBehaviorProfile);
    }

    protected virtual void OnSexChanged()
    {
        // Subclasses decide how sex-based visuals are applied.
    }
    
    public abstract void Initialize(float moveSpeed, float weight, float senseRadius);
    public void Despawn(CreatureDeathReason reason = CreatureDeathReason.Unknown)
    {
        if (!TryBeginDespawn())
            return;

        if (ECSMirrorBridge.TryRequestDespawnCreature(this, reason))
            return;

        CompleteDespawnFromBridge(reason);
    }

    internal void CompleteDespawnFromBridge(CreatureDeathReason reason)
    {
        Statistics.Instance?.RecordCreatureDeath(this, reason);
        DestroyObject();
    }

    protected void ResetDespawnRequestState()
    {
        despawnQueued = false;
    }

    private bool TryBeginDespawn()
    {
        if (despawnQueued || !gameObject.activeInHierarchy)
            return false;

        despawnQueued = true;
        return true;
    }

    protected abstract void DestroyObject();    
    protected abstract void CheckTransitions();

    protected CreatureUtilityBrain EnsureUtilityBrainComponent()
    {
        if (utilityBrain == null)
        {
            utilityBrain = GetComponent<CreatureUtilityBrain>() ?? gameObject.AddComponent<CreatureUtilityBrain>();
        }

        return utilityBrain;
    }

    protected void ResetUtilityAIDebugState()
    {
        utilityAIDebugString = string.Empty;
        lastUtilityAISelectedAction = CreatureAction.None;
        lastUtilityAITransitionStateType = CreatureStateType.None;
        lastUtilityAIResultStateType = CreatureStateType.None;

        if (utilityBrain == null)
            utilityBrain = GetComponent<CreatureUtilityBrain>();

        utilityBrain?.ClearLastDecision();
    }

    protected void RecordUtilityAIDecision(UtilityDecision decision)
    {
        CreatureAction action = decision != null ? decision.SelectedCreatureAction : CreatureAction.None;
        RecordUtilityAIExecution(
            decision,
            CreatureActionExecutionResult.Skipped(
                action,
                CurrentStateType,
                CreatureActionExecutor.GetTargetStateType(action)));
    }

    protected CreatureActionExecutionResult ExecuteUtilityAIAction(UtilityDecision decision)
    {
        CreatureAction action = decision != null ? decision.SelectedCreatureAction : CreatureAction.None;

        if (decision == null || !decision.HasSelection || decision.SelectedScore <= 0f)
        {
            return CreatureActionExecutionResult.Skipped(
                action,
                CurrentStateType,
                CreatureActionExecutor.GetTargetStateType(action));
        }

        return CreatureActionExecutor.Execute(this, action);
    }

    protected float GetJitteredUtilityDecisionDelay(float interval, float jitterFraction)
    {
        float clampedInterval = Mathf.Max(0.01f, interval);
        float clampedJitter = Mathf.Clamp01(jitterFraction);
        float phaseOffset = (GetUtilityDecisionPhase01() - 0.5f) * 2f;
        float factor = 1f + phaseOffset * clampedJitter;
        return Mathf.Max(0.01f, clampedInterval * factor);
    }

    private float GetUtilityDecisionPhase01()
    {
        if (utilityDecisionPhase >= 0f)
            return utilityDecisionPhase;

        uint hash = (uint)GetInstanceID();
        hash ^= 2747636419u;
        hash *= 2654435769u;
        hash ^= hash >> 16;
        hash *= 2246822519u;
        hash ^= hash >> 13;

        utilityDecisionPhase = (hash & 0x00FFFFFFu) / 16777215f;
        return utilityDecisionPhase;
    }

    protected bool TryExecuteECSUtilityDecision(GameConfig config)
    {
        if (utilityBrain == null)
            return false;

        if (!ECSMirrorBridge.TryGetUtilityAIDecision(this, out CreatureUtilityDecisionData ecsDecision) ||
            !ecsDecision.hasDecision)
        {
            return false;
        }

        if (ecsDecision.decisionTime <= LastUtilityAIDecisionTime + 0.0001f)
            return false;

        UtilityAIContext context = ECSMirrorBridge.TryGetUtilityAIContext(this, out UtilityAIContext ecsContext)
            ? ecsContext
            : UtilityAIContextFactory.FromMonoCreature(this);

        if (config == null || !config.logUtilityAIScores)
        {
            return TryExecuteECSUtilityDecisionFast(context, ecsDecision);
        }

        UtilityDecision decision = utilityBrain.ApplyExternalDecision(
            context,
            UtilityAIDefaultScorer.ECSScoringContextSource,
            ecsDecision);

        CreatureActionExecutionResult execution = ExecuteUtilityAIAction(decision);
        RecordUtilityAIExecution(decision, execution);

        if (config != null && config.logUtilityAIScores)
        {
            LogUtilityAIScores(decision);
        }

        return true;
    }

    private bool TryExecuteECSUtilityDecisionFast(
        UtilityAIContext context,
        CreatureUtilityDecisionData ecsDecision)
    {
        if (!utilityBrain.ApplyExternalDecisionFast(
                context,
                UtilityAIDefaultScorer.ECSScoringContextSource,
                ecsDecision,
                out CreatureAction action,
                out float selectedScore))
        {
            return false;
        }

        CreatureStateType currentState = CurrentStateType;
        CreatureStateType targetState = CreatureActionExecutor.GetTargetStateType(action);
        CreatureActionExecutionResult execution = selectedScore > 0f
            ? CreatureActionExecutor.Execute(this, action)
            : CreatureActionExecutionResult.Skipped(action, currentState, targetState);

        lastUtilityAISelectedAction = execution.Action;
        lastUtilityAITransitionStateType = execution.TargetStateType;
        lastUtilityAIResultStateType = execution.ResultStateType;
        utilityAIDebugString = string.Empty;
        return true;
    }

    protected void RecordUtilityAIExecution(UtilityDecision decision, CreatureActionExecutionResult execution)
    {
        if (utilityBrain != null)
        {
            utilityAIDebugString = utilityBrain.LastDecisionSummary;
        }
        else if (decision == null || !decision.HasSelection)
        {
            utilityAIDebugString = "UtilityAI action=None selected=None score=0.000";
        }
        else
        {
            utilityAIDebugString = $"UtilityAI t={decision.DecisionTime:0.00}s action={decision.SelectedCreatureAction} selected={decision.SelectedAction.Id} score={decision.SelectedScore:0.000}";
        }

        lastUtilityAISelectedAction = execution.Action;
        lastUtilityAITransitionStateType = execution.TargetStateType;
        lastUtilityAIResultStateType = execution.ResultStateType;

        utilityAIDebugString = $"{utilityAIDebugString} -> state transition {execution.TargetStateType} ({execution.PreviousStateType}->{execution.ResultStateType}{(execution.WasTransitionApplied ? string.Empty : ", skipped")})";
    }

    protected void LogUtilityAIScores(UtilityDecision decision)
    {
        if (decision == null)
            decision = UtilityDecision.Empty;

        string summary = !string.IsNullOrEmpty(utilityAIDebugString)
            ? utilityAIDebugString
            : $"UtilityAI t={decision.DecisionTime:0.00}s selected={(decision.HasSelection ? decision.SelectedAction.Id : "None")} score={decision.SelectedScore:0.000}";

        Debug.Log($"{name} {summary}", this);
    }

}
