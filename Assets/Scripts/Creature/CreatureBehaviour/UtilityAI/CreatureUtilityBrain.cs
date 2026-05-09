using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CreatureUtilityBrain : MonoBehaviour
{
    [SerializeField] private BaseCreatureBehaviour creature;
    [SerializeField] private List<UtilityAction> actions = new List<UtilityAction>();
    [SerializeField] private string lastSelectedActionId;
    [SerializeField] private CreatureStateType lastSelectedStateType = CreatureStateType.None;
    [SerializeField, Range(0f, 1f)] private float lastSelectedScore;
    [SerializeField] private List<UtilityActionScore> lastActionScores = new List<UtilityActionScore>();

    private UtilityDecision lastDecision = UtilityDecision.Empty;

    public BaseCreatureBehaviour Creature => creature;
    public IReadOnlyList<UtilityAction> Actions => actions;
    public UtilityDecision LastDecision => lastDecision;
    public IReadOnlyList<UtilityActionScore> LastActionScores => lastActionScores;
    public string LastSelectedActionId => lastSelectedActionId;
    public CreatureStateType LastSelectedStateType => lastSelectedStateType;
    public float LastSelectedScore => lastSelectedScore;

    private void Awake()
    {
        if (creature == null)
            creature = GetComponent<BaseCreatureBehaviour>();
    }

    private void OnValidate()
    {
        if (actions == null)
            actions = new List<UtilityAction>();

        if (lastActionScores == null)
            lastActionScores = new List<UtilityActionScore>();
    }

    public void Initialize(BaseCreatureBehaviour owner)
    {
        creature = owner != null ? owner : GetComponent<BaseCreatureBehaviour>();
        ClearLastDecision();
    }

    public void RegisterAction(UtilityAction action)
    {
        if (action == null)
            return;

        actions.Add(action);
        ClearLastDecision();
    }

    public void SetActions(IEnumerable<UtilityAction> newActions)
    {
        actions.Clear();

        if (newActions != null)
        {
            foreach (var action in newActions)
            {
                if (action != null)
                    actions.Add(action);
            }
        }

        ClearLastDecision();
    }

    public void ClearActions()
    {
        actions.Clear();
        ClearLastDecision();
    }

    public UtilityDecision Evaluate()
    {
        if (creature == null)
            creature = GetComponent<BaseCreatureBehaviour>();

        UtilityAction selectedAction = null;
        float selectedScore = 0f;

        for (int i = 0; i < actions.Count; i++)
        {
            UtilityAction action = actions[i];
            if (action == null)
                continue;

            float score = action.Evaluate(creature);
            if (action.IsEnabled && (selectedAction == null || score > selectedScore))
            {
                selectedAction = action;
                selectedScore = score;
            }
        }

        lastActionScores.Clear();
        for (int i = 0; i < actions.Count; i++)
        {
            UtilityAction action = actions[i];
            if (action == null)
                continue;

            lastActionScores.Add(new UtilityActionScore(action, action.LastScore, action == selectedAction));
        }

        lastSelectedActionId = selectedAction != null ? selectedAction.Id : string.Empty;
        lastSelectedStateType = selectedAction != null ? selectedAction.TargetStateType : CreatureStateType.None;
        lastSelectedScore = selectedAction != null ? selectedScore : 0f;
        lastDecision = new UtilityDecision(selectedAction, lastSelectedScore, lastActionScores);
        return lastDecision;
    }

    [ContextMenu("Evaluate Utility Scores")]
    private void EvaluateUtilityScoresContextMenu()
    {
        Evaluate();
    }

    public bool TryGetLastActionScore(string actionId, out float score)
    {
        if (lastDecision != null && lastDecision.TryGetActionScore(actionId, out score))
            return true;

        score = 0f;
        return false;
    }

    private void ClearLastDecision()
    {
        lastSelectedActionId = string.Empty;
        lastSelectedStateType = CreatureStateType.None;
        lastSelectedScore = 0f;
        lastActionScores.Clear();
        lastDecision = UtilityDecision.Empty;
    }
}
