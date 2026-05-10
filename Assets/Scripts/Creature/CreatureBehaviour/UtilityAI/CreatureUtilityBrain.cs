using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class CreatureUtilityBrain : MonoBehaviour
{
    [SerializeField] private BaseCreatureBehaviour creature;
    [SerializeField] private List<UtilityAction> actions = new List<UtilityAction>();
    [SerializeField] private string lastSelectedActionId;
    [SerializeField] private CreatureAction lastSelectedCreatureAction = CreatureAction.None;
    [SerializeField, Range(0f, 1f)] private float lastSelectedScore;
    [SerializeField] private float lastDecisionTime = -1f;
    [SerializeField, TextArea(2, 4)] private string lastDecisionSummary;
    [SerializeField] private List<UtilityActionScore> lastActionScores = new List<UtilityActionScore>();

    private UtilityDecision lastDecision = UtilityDecision.Empty;
    private UtilityAIContext lastContext = UtilityAIContext.Empty;

    public BaseCreatureBehaviour Creature => creature;
    public IReadOnlyList<UtilityAction> Actions => actions;
    public UtilityDecision LastDecision => lastDecision;
    public IReadOnlyList<UtilityActionScore> LastActionScores => lastActionScores;
    public string LastSelectedActionId => lastSelectedActionId;
    public CreatureAction LastSelectedCreatureAction => lastSelectedCreatureAction;
    public float LastSelectedScore => lastSelectedScore;
    public float LastDecisionTime => lastDecisionTime;
    public string LastDecisionSummary => lastDecisionSummary;

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

        return Evaluate(UtilityAIContextFactory.FromCreature(creature));
    }

    public UtilityDecision Evaluate(UtilityAIContext context)
    {
        lastContext = context;
        UtilityAction selectedAction = null;
        float selectedScore = 0f;

        for (int i = 0; i < actions.Count; i++)
        {
            UtilityAction action = actions[i];
            if (action == null)
                continue;

            float score = action.Evaluate(context);
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
        lastSelectedCreatureAction = selectedAction != null ? selectedAction.Action : CreatureAction.None;
        lastSelectedScore = selectedAction != null ? selectedScore : 0f;
        lastDecisionTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        lastDecision = new UtilityDecision(selectedAction, lastSelectedScore, lastActionScores, lastDecisionTime);
        lastDecisionSummary = BuildLastDecisionSummary();
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

    public void ClearLastDecision()
    {
        lastSelectedActionId = string.Empty;
        lastSelectedCreatureAction = CreatureAction.None;
        lastSelectedScore = 0f;
        lastDecisionTime = -1f;
        lastDecisionSummary = string.Empty;
        lastActionScores.Clear();
        lastDecision = UtilityDecision.Empty;
    }

    private string BuildLastDecisionSummary()
    {
        var builder = new StringBuilder(160);
        string selectedAction = string.IsNullOrEmpty(lastSelectedActionId) ? "None" : lastSelectedActionId;

        builder.Append("UtilityAI t=");
        builder.Append(lastDecisionTime.ToString("0.00"));
        builder.Append("s");

        builder.Append(" state=");
        builder.Append(lastContext.currentState);

        builder.Append(" action=");
        builder.Append(lastSelectedCreatureAction);

        builder.Append(" selected=");
        builder.Append(selectedAction);
        builder.Append(" score=");
        builder.Append(lastSelectedScore.ToString("0.000"));

        if (lastActionScores.Count > 0)
        {
            builder.Append(" | ");
            for (int i = 0; i < lastActionScores.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                UtilityActionScore actionScore = lastActionScores[i];
                if (actionScore.IsSelected)
                    builder.Append("*");

                builder.Append(actionScore.ActionId);
                builder.Append("=");
                builder.Append(actionScore.Score.ToString("0.000"));
            }
        }

        return builder.ToString();
    }
}
