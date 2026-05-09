using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UtilityAction
{
    [SerializeField] private string id = "Action";
    [SerializeField] private string displayName = "Action";
    [SerializeField] private CreatureStateType targetStateType = CreatureStateType.None;
    [SerializeField] private bool isEnabled = true;
    [SerializeField, Range(0f, 1f)] private float baseScore = 1f;
    [SerializeField] private List<UtilityConsideration> considerations = new List<UtilityConsideration>();

    public string Id => string.IsNullOrWhiteSpace(id) ? DisplayName : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
    public CreatureStateType TargetStateType => targetStateType;
    public bool IsEnabled => isEnabled;
    public float BaseScore => baseScore;
    public IReadOnlyList<UtilityConsideration> Considerations => considerations;
    public float LastScore { get; private set; }

    public UtilityAction()
    {
    }

    public UtilityAction(
        string id,
        string displayName,
        CreatureStateType targetStateType,
        IEnumerable<UtilityConsideration> considerations = null,
        float baseScore = 1f)
    {
        this.id = id;
        this.displayName = displayName;
        this.targetStateType = targetStateType;
        this.baseScore = Mathf.Clamp01(baseScore);

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
        this.baseScore = Mathf.Clamp01(baseScore);
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

    public float Evaluate(BaseCreatureBehaviour creature)
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

            scoreSum += consideration.Evaluate(creature) * weight;
            weightSum += weight;
        }

        float considerationScore = weightSum > 0f ? scoreSum / weightSum : 1f;
        LastScore = Mathf.Clamp01(baseScore) * Mathf.Clamp01(considerationScore);
        return LastScore;
    }
}
