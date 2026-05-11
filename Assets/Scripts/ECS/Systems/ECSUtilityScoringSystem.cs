using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ECSObservationSystem))]
public partial class ECSUtilityScoringSystem : SystemBase
{
    private EntityQuery scoringQuery;
    private float scoringTimer;
    private bool hasScored;

    protected override void OnCreate()
    {
        scoringQuery = GetEntityQuery(
            ComponentType.ReadOnly<CreatureIdentity>(),
            ComponentType.ReadOnly<CreatureAIContextData>(),
            ComponentType.ReadOnly<CreatureUtilityBehaviorData>(),
            ComponentType.ReadOnly<CreatureObservationResultData>(),
            ComponentType.ReadWrite<CreatureUtilityDecisionData>());

        RequireForUpdate(scoringQuery);
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useUtilityAI || !config.useEcsUtilityScoring)
        {
            scoringTimer = 0f;
            hasScored = false;
            return;
        }

        scoringTimer += (float)World.Time.DeltaTime;
        if (hasScored && scoringTimer < math.max(0.01f, config.utilityDecisionInterval))
            return;

        scoringTimer = 0f;
        hasScored = true;

        Dependency = new ECSUtilityScoringJob
        {
            eatingEnergyThreshold = config.eatingEnergyThreshold,
            herbivoreReproductionEnergyThreshold = config.GetReproductionEnergyThreshold(false),
            predatorReproductionEnergyThreshold = config.GetReproductionEnergyThreshold(true),
            decisionTime = (float)World.Time.ElapsedTime
        }.ScheduleParallel(Dependency);

        Dependency.Complete();
    }
}

[BurstCompile]
public partial struct ECSUtilityScoringJob : IJobEntity
{
    public float eatingEnergyThreshold;
    public float herbivoreReproductionEnergyThreshold;
    public float predatorReproductionEnergyThreshold;
    public float decisionTime;

    public void Execute(
        in CreatureIdentity identity,
        in CreatureAIContextData contextData,
        in CreatureUtilityBehaviorData behaviorData,
        in CreatureObservationResultData observationResult,
        ref CreatureUtilityDecisionData decisionData)
    {
        float reproductionEnergyThreshold = identity.creatureKind == ECSCreatureKind.Predator
            ? predatorReproductionEnergyThreshold
            : herbivoreReproductionEnergyThreshold;

        UtilityAIScoringParameters parameters = new UtilityAIScoringParameters(
            eatingEnergyThreshold,
            reproductionEnergyThreshold);

        decisionData = UtilityAIDefaultScorer.Score(
            identity,
            contextData,
            behaviorData,
            observationResult,
            parameters,
            decisionTime);
    }
}
