using System;
using UnityEngine;

[Serializable]
public class UtilityConsideration
{
    [SerializeField] private string name = "Consideration";
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField, Range(0f, 1f)] private float fallbackScore;
    [SerializeField] private bool invertScore;
    [SerializeField] private AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Func<BaseCreatureBehaviour, float> evaluator;

    public string Name => string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
    public float Weight => Mathf.Max(0f, weight);
    public float FallbackScore => fallbackScore;
    public bool InvertScore => invertScore;
    public float LastRawScore { get; private set; }
    public float LastScore { get; private set; }

    public UtilityConsideration()
    {
    }

    public UtilityConsideration(string name, Func<BaseCreatureBehaviour, float> evaluator, float weight = 1f)
    {
        this.name = name;
        this.evaluator = evaluator;
        this.weight = Mathf.Max(0f, weight);
    }

    public void SetEvaluator(Func<BaseCreatureBehaviour, float> evaluator)
    {
        this.evaluator = evaluator;
    }

    public float Evaluate(BaseCreatureBehaviour creature)
    {
        float rawScore = evaluator != null ? evaluator(creature) : fallbackScore;
        LastRawScore = ClampUtilityScore(rawScore);

        float curvedScore = responseCurve != null ? responseCurve.Evaluate(LastRawScore) : LastRawScore;
        LastScore = ClampUtilityScore(invertScore ? 1f - curvedScore : curvedScore);
        return LastScore;
    }

    private static float ClampUtilityScore(float score)
    {
        if (float.IsNaN(score) || float.IsInfinity(score))
            return 0f;

        return Mathf.Clamp01(score);
    }
}
