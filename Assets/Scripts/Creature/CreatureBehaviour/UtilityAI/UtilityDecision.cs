using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct UtilityActionScore
{
    [SerializeField] private string actionId;
    [SerializeField] private string displayName;
    [SerializeField] private CreatureStateType targetStateType;
    [SerializeField] private float score;
    [SerializeField] private bool isSelected;

    public string ActionId => actionId;
    public string DisplayName => displayName;
    public CreatureStateType TargetStateType => targetStateType;
    public float Score => score;
    public bool IsSelected => isSelected;

    public UtilityActionScore(UtilityAction action, float score, bool isSelected)
    {
        actionId = action != null ? action.Id : string.Empty;
        displayName = action != null ? action.DisplayName : string.Empty;
        targetStateType = action != null ? action.TargetStateType : CreatureStateType.None;
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
    public IReadOnlyList<UtilityActionScore> ActionScores => actionScores;
    public bool HasSelection => SelectedAction != null;

    public static UtilityDecision Empty { get; } = new UtilityDecision(null, 0f, null);

    public UtilityDecision(
        UtilityAction selectedAction,
        float selectedScore,
        IEnumerable<UtilityActionScore> actionScores)
    {
        SelectedAction = selectedAction;
        SelectedScore = Mathf.Clamp01(selectedScore);
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
