using NUnit.Framework;

public class UtilityAIScoreRulesTests
{
    private static readonly UtilityAIScoringParameters Parameters = new UtilityAIScoringParameters(0.7f, 0.6f);

    [TestCase(0.7f, 0f)]
    [TestCase(0.35f, 0.95f)]
    [TestCase(0f, 1f)]
    public void HungerScore_UsesEatingEnergyThreshold(float energyPercent, float expectedScore)
    {
        UtilityAIContext context = CreateContext(energyPercent: energyPercent);

        float score = UtilityAIScoreRules.GetHungerScore(context, Parameters);

        Assert.That(score, Is.EqualTo(expectedScore).Within(0.0001f));
    }

    [Test]
    public void ReproductionScore_RequiresMatureReadyAndEnoughEnergy()
    {
        Assert.That(
            UtilityAIScoreRules.GetReproductionScore(CreateContext(energyPercent: 1f, isMature: false), Parameters),
            Is.EqualTo(0f));
        Assert.That(
            UtilityAIScoreRules.GetReproductionScore(CreateContext(energyPercent: 1f, isReproductionReady: false), Parameters),
            Is.EqualTo(0f));
        Assert.That(
            UtilityAIScoreRules.GetReproductionScore(CreateContext(energyPercent: 0.65f), Parameters),
            Is.EqualTo(0f));
        Assert.That(
            UtilityAIScoreRules.GetReproductionScore(CreateContext(energyPercent: 1f), Parameters),
            Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void AvailabilityScores_BlockLockedStatesAndThreats()
    {
        Assert.That(
            UtilityAIScoreRules.GetFoodSearchAvailabilityScore(CreateContext(currentState: CreatureStateType.SearchingForFood)),
            Is.EqualTo(0f));
        Assert.That(
            UtilityAIScoreRules.GetMateSearchAvailabilityScore(CreateContext(currentState: CreatureStateType.SearchingForMate)),
            Is.EqualTo(0f));
        Assert.That(
            UtilityAIScoreRules.GetFoodSearchAvailabilityScore(CreateContext(isThreatened: true)),
            Is.EqualTo(0f));
        Assert.That(
            UtilityAIScoreRules.GetMateSearchAvailabilityScore(CreateContext(currentState: CreatureStateType.Idle)),
            Is.EqualTo(1f));
    }

    [Test]
    public void KeepCurrentStateScore_ProtectsLockedAndMateSearchStates()
    {
        Assert.That(
            UtilityAIScoreRules.GetKeepCurrentStateScore(CreateContext(currentState: CreatureStateType.Eating), Parameters),
            Is.EqualTo(1f));
        Assert.That(
            UtilityAIScoreRules.GetKeepCurrentStateScore(CreateContext(energyPercent: 1f, currentState: CreatureStateType.SearchingForMate), Parameters),
            Is.EqualTo(0.85f).Within(0.0001f));
        Assert.That(
            UtilityAIScoreRules.GetKeepCurrentStateScore(CreateContext(energyPercent: 0.2f, currentState: CreatureStateType.SearchingForMate), Parameters),
            Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(
            UtilityAIScoreRules.GetKeepCurrentStateScore(CreateContext(currentState: CreatureStateType.Idle), Parameters),
            Is.EqualTo(0f));
    }

    [Test]
    public void UtilityAction_EvaluatesAgainstPlainContext()
    {
        var searchFood = new UtilityAction(
            CreatureAction.SearchFood,
            "Search Food",
            new[]
            {
                new UtilityConsideration(
                    "Hunger",
                    context => UtilityAIScoreRules.GetHungerScore(context, Parameters) * UtilityAIScoreRules.GetFoodSearchAvailabilityScore(context))
            });

        Assert.That(searchFood.Evaluate(CreateContext(energyPercent: 0f, currentState: CreatureStateType.Idle)), Is.EqualTo(1f));
        Assert.That(searchFood.Evaluate(CreateContext(energyPercent: 0f, currentState: CreatureStateType.Eating)), Is.EqualTo(0f));
    }

    private static UtilityAIContext CreateContext(
        float energyPercent = 1f,
        bool isMature = true,
        bool isReproductionReady = true,
        CreatureStateType currentState = CreatureStateType.Idle,
        bool hasKnownFood = false,
        bool hasKnownMate = false,
        bool hasKnownPrey = false,
        bool isThreatened = false)
    {
        return new UtilityAIContext(
            energyPercent,
            isMature,
            isReproductionReady,
            currentState,
            hasKnownFood,
            hasKnownMate,
            hasKnownPrey,
            isThreatened);
    }
}
