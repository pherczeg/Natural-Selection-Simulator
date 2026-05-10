using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ECSObservationSystem))]
public partial class ECSFoodLifecycleSystem : SystemBase
{
    private EntityQuery foodQuery;

    protected override void OnCreate()
    {
        foodQuery = GetEntityQuery(ComponentType.ReadWrite<FoodMirrorData>());
        RequireForUpdate(foodQuery);
    }

    protected override void OnUpdate()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || !config.useEcsFoodLifecycle)
            return;

        Dependency = new ECSFoodLifecycleJob
        {
            deltaTime = (float)World.Time.DeltaTime,
            foodMaturityAge = config.foodMaturityAge,
            foodMaxAge = config.foodMaxAge
        }.ScheduleParallel(Dependency);

        Dependency.Complete();
    }
}

[BurstCompile]
public partial struct ECSFoodLifecycleJob : IJobEntity
{
    public float deltaTime;
    public float foodMaturityAge;
    public float foodMaxAge;

    public void Execute(ref FoodMirrorData food)
    {
        food = FoodLifecycleCalculator.Step(
            food,
            deltaTime,
            foodMaturityAge,
            foodMaxAge);
    }
}
