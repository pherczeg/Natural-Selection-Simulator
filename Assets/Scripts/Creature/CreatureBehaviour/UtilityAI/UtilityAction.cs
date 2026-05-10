using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UtilityAction
{
    [SerializeField] private string id = "Action";
    [SerializeField] private string displayName = "Action";
    [SerializeField] private CreatureAction action = CreatureAction.None;
    [SerializeField] private bool isEnabled = true;
    [SerializeField, Range(0f, 1f)] private float baseScore = 1f;
    [SerializeField] private List<UtilityConsideration> considerations = new List<UtilityConsideration>();

    public string Id => string.IsNullOrWhiteSpace(id) ? action.ToString() : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
    public CreatureAction Action => action;
    public bool IsEnabled => isEnabled;
    public float BaseScore => baseScore;
    public IReadOnlyList<UtilityConsideration> Considerations => considerations;
    public float LastScore { get; private set; }

    public UtilityAction()
    {
    }

    public UtilityAction(
        CreatureAction action,
        string displayName,
        IEnumerable<UtilityConsideration> considerations = null,
        float baseScore = 1f,
        string id = null)
    {
        this.action = action;
        this.id = string.IsNullOrWhiteSpace(id) ? action.ToString() : id;
        this.displayName = displayName;
        this.baseScore = UtilityAIScoreRules.Clamp01(baseScore);

        if (considerations == null)
            return;

        foreach (var consideration in considerations)
        {
            AddConsideration(consideration);
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        this.isEnabled = isEnabled;
    }

    public void SetBaseScore(float baseScore)
    {
        this.baseScore = UtilityAIScoreRules.Clamp01(baseScore);
    }

    public void AddConsideration(UtilityConsideration consideration)
    {
        if (consideration == null)
            return;

        considerations.Add(consideration);
    }

    public void ClearConsiderations()
    {
        considerations.Clear();
        LastScore = 0f;
    }

    public float Evaluate(UtilityAIContext context)
    {
        if (!isEnabled)
        {
            LastScore = 0f;
            return LastScore;
        }

        float scoreSum = 0f;
        float weightSum = 0f;

        foreach (var consideration in considerations)
        {
            if (consideration == null)
                continue;

            float weight = consideration.Weight;
            if (weight <= 0f)
                continue;

            scoreSum += consideration.Evaluate(context) * weight;
            weightSum += weight;
        }

        float considerationScore = weightSum > 0f ? scoreSum / weightSum : 1f;
        LastScore = UtilityAIScoreRules.Clamp01(baseScore) * UtilityAIScoreRules.Clamp01(considerationScore);
        return LastScore;
    }
}
