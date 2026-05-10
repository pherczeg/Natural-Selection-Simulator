using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct UtilityActionScore
{
    [SerializeField] private string actionId;
    [SerializeField] private string displayName;
    [SerializeField] private CreatureAction action;
    [SerializeField] private float score;
    [SerializeField] private bool isSelected;

    public string ActionId => actionId;
    public string DisplayName => displayName;
    public CreatureAction Action => action;
    public float Score => score;
    public bool IsSelected => isSelected;

    public UtilityActionScore(UtilityAction utilityAction, float score, bool isSelected)
    {
        actionId = utilityAction != null ? utilityAction.Id : string.Empty;
        displayName = utilityAction != null ? utilityAction.DisplayName : string.Empty;
        action = utilityAction != null ? utilityAction.Action : CreatureAction.None;
        this.score = Mathf.Clamp01(score);
        this.isSelected = isSelected;
    }
}

[Serializable]
public class UtilityDecision
{
    private readonly List<UtilityActionScore> actionScores;

    public UtilityAction SelectedAction { get; }
    public float SelectedScore { get; }
    public float DecisionTime { get; }
    public IReadOnlyList<UtilityActionScore> ActionScores => actionScores;
    public bool HasSelection => SelectedAction != null;
    public CreatureAction SelectedCreatureAction => SelectedAction != null ? SelectedAction.Action : CreatureAction.None;

    public static UtilityDecision Empty { get; } = new UtilityDecision(null, 0f, null, -1f);

    public UtilityDecision(
        UtilityAction selectedAction,
        float selectedScore,
        IEnumerable<UtilityActionScore> actionScores,
        float decisionTime = -1f)
    {
        SelectedAction = selectedAction;
        SelectedScore = Mathf.Clamp01(selectedScore);
        DecisionTime = decisionTime;
        this.actionScores = actionScores != null
            ? new List<UtilityActionScore>(actionScores)
            : new List<UtilityActionScore>();
    }

    public bool TryGetActionScore(string actionId, out float score)
    {
        foreach (var actionScore in actionScores)
        {
            if (actionScore.ActionId == actionId)
            {
                score = actionScore.Score;
                return true;
            }
        }

        score = 0f;
        return false;
    }
}
