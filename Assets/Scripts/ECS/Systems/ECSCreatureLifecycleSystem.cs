using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ECSObservationSystem))]
public partial class ECSCreatureLifecycleSystem : SystemBase
{
    private EntityQuery lifecycleQuery;

    protected override void OnCreate()
    {
        lifecycleQuery = GetEntityQuery(ComponentType.ReadWrite<CreatureLifecycleData>());
        RequireForUpdate(lifecycleQuery);
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useEcsCreatureLifecycle)
            return;

        Dependency = new ECSCreatureLifecycleJob
        {
            parameters = CreatureLifecycleParameters.FromConfig(config),
            deltaTime = (float)World.Time.DeltaTime
        }.ScheduleParallel(Dependency);

        Dependency.Complete();
    }
}

[BurstCompile]
public partial struct ECSCreatureLifecycleJob : IJobEntity
{
    public CreatureLifecycleParameters parameters;
    public float deltaTime;

    public void Execute(ref CreatureLifecycleData lifecycle)
    {
        lifecycle = CreatureLifecycleCalculator.Step(
            lifecycle,
            parameters,
            deltaTime);
    }
}
