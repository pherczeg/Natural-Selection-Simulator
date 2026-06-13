using Unity.Entities;
using Unity.Mathematics;

// Per-creature genetic/behavioral payload mirrored onto creature entities.
// Pure data plus a thin managed factory so spawn systems can populate it without
// reaching back into the bridge for individual gene fields.
public struct Genome : IComponentData
{
    public int creatureKind;                 // ECSCreatureKind.*
    public int sex;                          // (int)CreatureSex
    public float weight;
    public float moveSpeed;                  // base move speed
    public float senseRadius;                // base sense radius
    public float sprintDuration;
    public float sprintFactor;
    public float sprintCooldown;
    public float sprintCooldownSpeedFactor;
    public float desirability;
    public float agility;                    // herbivore (0 for predator)
    public int herbivoreSocialStrategy;      // (int)HerbivoreSocialStrategy
    public float strength;                   // predator (0 for herbivore)
    public float utilityKeepCurrentStateWeight;
    public float utilityFoodActionWeight;
    public float utilitySearchMateWeight;
    public float utilityWanderWeight;
    public float initialAge;
}

// Deterministic per-entity RNG stream. Always seeded with a non-zero value.
public struct RandomState : IComponentData
{
    public Random rng;
}

// Mates this creature rejected (or was rejected by) recently; suppress re-courting.
[InternalBufferCapacity(4)]
public struct RejectedMateCooldown : IBufferElementData
{
    public int targetInstanceId;
    public float remaining;
}

// Food this creature failed to reach/eat recently; skip while on cooldown.
[InternalBufferCapacity(4)]
public struct FoodBlacklistCooldown : IBufferElementData
{
    public int foodInstanceId;
    public float remaining;
}

// Prey this predator failed to catch recently; skip while on cooldown.
[InternalBufferCapacity(4)]
public struct PreyBlacklistCooldown : IBufferElementData
{
    public int preyInstanceId;
    public float remaining;
}

public static class GenomeFactory
{
    // Pure: copies every gene field from a spawn request verbatim (position is not a gene).
    public static Genome FromSpawnRequest(in SpawnCreatureRequest r)
    {
        return new Genome
        {
            creatureKind = r.creatureKind,
            sex = r.sex,
            weight = r.weight,
            moveSpeed = r.moveSpeed,
            senseRadius = r.senseRadius,
            sprintDuration = r.sprintDuration,
            sprintFactor = r.sprintFactor,
            sprintCooldown = r.sprintCooldown,
            sprintCooldownSpeedFactor = r.sprintCooldownSpeedFactor,
            desirability = r.desirability,
            agility = r.agility,
            herbivoreSocialStrategy = r.herbivoreSocialStrategy,
            strength = r.strength,
            utilityKeepCurrentStateWeight = r.utilityKeepCurrentStateWeight,
            utilityFoodActionWeight = r.utilityFoodActionWeight,
            utilitySearchMateWeight = r.utilitySearchMateWeight,
            utilityWanderWeight = r.utilityWanderWeight,
            initialAge = r.initialAge
        };
    }

    // Managed: reads the live behaviour's composed managers into a Genome snapshot.
    public static Genome FromCreature(BaseCreatureBehaviour creature)
    {
        int creatureKind = ECSCreatureKind.Unknown;
        if (creature is HerbivoreBehaviour)
        {
            creatureKind = ECSCreatureKind.Herbivore;
        }
        else if (creature is PredatorBehaviour)
        {
            creatureKind = ECSCreatureKind.Predator;
        }

        MovementManager movement = creature.MovementManager;
        ObservationManager observation = creature.ObservationManager;
        ReproductionManager reproduction = creature.ReproductionManager;
        AgeManager age = creature.AgeManager;
        CreatureUtilityBehaviorData utility = creature.UtilityBehaviorProfile;

        HerbivoreBehaviour herbivore = creature as HerbivoreBehaviour;
        PredatorBehaviour predator = creature as PredatorBehaviour;

        return new Genome
        {
            creatureKind = creatureKind,
            sex = (int)creature.Sex,
            weight = creature.Weight,
            moveSpeed = movement != null ? movement.BaseMoveSpeed : 0f,
            senseRadius = observation != null ? observation.BaseSenseRadius : 0f,
            sprintDuration = movement != null ? movement.BaseSprintDuration : 0f,
            sprintFactor = movement != null ? movement.BaseSprintFactor : 0f,
            sprintCooldown = movement != null ? movement.BaseSprintCooldown : 0f,
            sprintCooldownSpeedFactor = movement != null ? movement.BaseSprintCooldownSpeedFactor : 0f,
            desirability = reproduction != null ? reproduction.BaseDesirability : 0f,
            agility = herbivore != null ? herbivore.Agility : 0f,
            herbivoreSocialStrategy = (int)(herbivore != null ? herbivore.SocialStrategy : HerbivoreSocialStrategy.Dove),
            strength = predator != null ? predator.Strength : 0f,
            utilityKeepCurrentStateWeight = utility.keepCurrentStateWeight,
            utilityFoodActionWeight = utility.foodActionWeight,
            utilitySearchMateWeight = utility.searchMateWeight,
            utilityWanderWeight = utility.wanderWeight,
            initialAge = age != null ? age.Age : 0f
        };
    }

    // Deterministic, guaranteed non-zero seed derived from the GameObject instance id + a salt.
    public static RandomState SeedRandomState(int instanceId, uint salt)
    {
        uint seed = math.hash(new int2(instanceId, (int)salt));
        if (seed == 0u)
        {
            seed = 0x9E3779B9u;
        }

        return new RandomState { rng = new Random(seed) };
    }
}
