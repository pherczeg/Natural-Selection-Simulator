using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Resources/GameConfig")]
public class GameConfig : ScriptableObject
{
    private static GameConfig instance;

    public static GameConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameConfig>("GameConfig");
                if (instance == null)
                {
                    Debug.LogError("GameConfig not found in Resources!");
                }
            }
            return instance;
        }
    }

    [Header("Simulation")]
    public float updateInterval = .2f;

    [Header("Creature Spawner")]
    [Min(0)] public int initialHerbivoreCount = 5;
    [Min(0)] public int initialPredatorCount = 5;
    [Min(0)] public int maxHerbivoreCount = 0;
    [Min(0)] public int maxPredatorCount = 0;
    [Tooltip("Legacy combined cap used only when both species-specific caps are 0.")]
    [Min(0)] public int maxCreatureCount = 0;
    [Min(1)] public int creaturePoolSize = 100;

    [Header("Food Spawner")]
    [Min(0)] public int initialFoodCount = 20;
    [Min(0)] public int maxFoodCount = 0;
    [Min(1)] public int foodPoolSize = 2000;
    [Min(0.01f)] public float foodSpawnInterval = 5f;
    [Min(0)] public int foodSpawnBatchSize = 5;

    [Header("AI Migration")]
    public bool useUtilityAI = false;
    public bool useEcsObservation = false;
    public bool useEcsAIContext = false;
    public bool useEcsUtilityScoring = false;
    public bool useEcsFoodLifecycle = false;
    public bool useEcsCreatureLifecycle = false;
    public bool useEcsMovementExecution = false;
    [Tooltip("Requires useEcsCreatureLifecycle; aggregates core species stats from ECS data instead of polling creature lists.")]
    public bool useEcsStatistics = false;
    public bool logUtilityAIScores = false;
    [Min(0.01f)] public float utilityDecisionInterval = .2f;

    [Header("Benchmark")]
    public bool benchmarkEnabled = false;
    public int[] benchmarkCreatureCounts = { 500, 1000, 2000, 5000 };
    [Min(0f)] public float benchmarkWarmupSeconds = 10f;
    [Min(1f)] public float benchmarkMeasureSeconds = 30f;
    [Min(1)] public int benchmarkSpawnBatchPerFrame = 100;

    [Header("Utility Behavior Genetics")]
    [Range(0f, 2f)] public float utilityBehaviorWeightMin = 0.75f;
    [Range(0f, 2f)] public float utilityBehaviorWeightMax = 1.25f;

    [Header("Creature Lifecycle")]
    public float oldAgeStartAge = 120f;
    public float creatureMaxAge = 180f;
    public float maturityAge = 24f; // age at which creature reaches full size, max energy, and can reproduce
    public float oldAgeSpeedDecayPerSecond = 0.01f;
    public float oldAgeSenseDecayPerSecond = 0.008f;
    public float oldAgeDesirabilityDecayPerSecond = 0.012f;
    public float oldAgeMinSpeedMultiplier = 0.2f;
    public float oldAgeMinSenseMultiplier = 0.2f;
    public float oldAgeMinDesirabilityMultiplier = 0.05f;

    [Header("Creature Energy")]
    public float maxEnergy = 200f;
    public float herbivoreMaxEnergy = 200f;
    public float predatorMaxEnergy = 260f;
    public float initialEnergyPercentageHerbivore = 0.5f;
    public float initialEnergyPercentagePredator = 0.8f;
    public float eatingEnergyThreshold = 0.7f;
    public float energyConsumptionCoefficient = 0.0002f;
    public float predatorEnergyGainPerPreyWeight = 25f;

    [Header("Reproduction")]
    public float blackListDuration = .5f;

    [Header("Reproduction - Herbivore")]
    public float herbivoreReproductionTime = 1f;
    [Range(0f, 1f)] public float herbivoreReproductionEnergyThreshold = 0.6f;
    public float herbivoreFemaleReproductionCooldown = 45f;
    public float herbivoreMaleReproductionCooldown = 15f;
    public float herbivoreRejectedMateCooldown = 10f;
    [Range(0f, 1f)] public float herbivoreMinMateAcceptanceChance = 0.2f;
    [Range(0f, 1f)] public float herbivoreMaxMateAcceptanceChance = 0.95f;
    public int herbivoreMinOffspringPerReproduction = 1;
    public int herbivoreMaxOffspringPerReproduction = 5;
    public float herbivoreOffspringCountMean = 3f;
    public float herbivoreOffspringCountStdDev = 1f;

    [Header("Reproduction - Predator")]
    public float predatorReproductionTime = 1f;
    [Range(0f, 1f)] public float predatorReproductionEnergyThreshold = 0.6f;
    public float predatorFemaleReproductionCooldown = 45f;
    public float predatorMaleReproductionCooldown = 15f;
    public float predatorRejectedMateCooldown = 10f;
    [Range(0f, 1f)] public float predatorMinMateAcceptanceChance = 0.2f;
    [Range(0f, 1f)] public float predatorMaxMateAcceptanceChance = 0.95f;
    public int predatorMinOffspringPerReproduction = 1;
    public int predatorMaxOffspringPerReproduction = 5;
    public float predatorOffspringCountMean = 3f;
    public float predatorOffspringCountStdDev = 1f;

    [Header("Genetics")]
    public float mutationRate = 0.5f;
    public float mutationChance = 0.5f;
    public float sizeDifferentFactor = 1.5f;

    [Header("Initial Herbivore Ranges")]
    public float herbivoreWeightMin = 2f;
    public float herbivoreWeightMax = 10f;
    public float herbivoreSpeedMin = 7f;
    public float herbivoreSpeedMax = 20f;
    public float herbivoreSprintDurationMin = 0.6f;
    public float herbivoreSprintDurationMax = 1.8f;
    public float herbivoreSprintFactorMin = 1.15f;
    public float herbivoreSprintFactorMax = 1.9f;
    public float herbivoreSprintCooldownMin = 1.5f;
    public float herbivoreSprintCooldownMax = 4.0f;
    public float herbivoreSprintCooldownSpeedFactorMin = 0.7f;
    public float herbivoreSprintCooldownSpeedFactorMax = 0.95f;
    public float herbivoreSenseMin = 10f;
    public float herbivoreSenseMax = 30f;
    public float herbivoreDesirabilityMin = 0f;
    public float herbivoreDesirabilityMax = 1f;
    public float herbivoreAgilityMin = 0f;
    public float herbivoreAgilityMax = 1f;

    [Header("Initial Predator Ranges")]
    public float predatorWeightMin = 5f;
    public float predatorWeightMax = 12f;
    public float predatorSpeedMin = 10f;
    public float predatorSpeedMax = 25f;
    public float predatorSprintDurationMin = 0.5f;
    public float predatorSprintDurationMax = 1.6f;
    public float predatorSprintFactorMin = 1.2f;
    public float predatorSprintFactorMax = 2.0f;
    public float predatorSprintCooldownMin = 1.5f;
    public float predatorSprintCooldownMax = 4.5f;
    public float predatorSprintCooldownSpeedFactorMin = 0.65f;
    public float predatorSprintCooldownSpeedFactorMax = 0.95f;
    public float predatorSenseMin = 30f;
    public float predatorSenseMax = 40f;
    public float predatorStrengthMin = 0f;
    public float predatorStrengthMax = 1f;

    [Header("Predator vs Herbivore")]
    public float herbivoreFleeDistance = 8f;
    public float herbivoreFleeObstacleProbeDistance = 2f;
    public float herbivoreFleeMemoryDuration = 0.35f;
    public float herbivorePostEscapeFleeDuration = 2f;
    public float predatorEatingDuration = 2.5f;
    public float predatorFailedHuntSpeedMultiplier = 0.5f;
    public float predatorFailedHuntDebuffDuration = 2f;
    public float escapedPreyBlacklistDuration = 3f;
    [Min(0f)] public float predatorMaxChaseDuration = 3f;
    [Min(1f)] public float predatorPreySpeedGiveUpFactor = 1.15f;
    [Min(0f)] public float predatorMinChaseTimeBeforeSpeedCheck = 2f;
    public float predatorStrengthScoreWeight = 0.65f;
    public float predatorWeightScoreWeight = 0.35f;
    public float herbivoreAgilityScoreWeight = 0.60f;
    public float herbivoreWeightScoreWeight = 0.40f;
    public float predationBias = 0.6f;
    public float predationSharpness = 3f;
    [Range(0f, 1f)] public float minPredationSuccessChance = 0.10f;
    [Range(0f, 1f)] public float maxPredationSuccessChance = 0.95f;

    [Header("Herbivore Sex Visuals")]
    public Color herbivoreFemaleColor = new Color(1f, 0.45f, 0.65f, 1f);
    public Color herbivoreMaleColor = new Color(0.35f, 0.65f, 1f, 1f);

    [Header("Predator Sex Visuals")]
    public Color predatorFemaleColor = new Color(1f, 0.35f, 0.35f, 1f);
    public Color predatorMaleColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Food")]
    public float nutritionConsumptionRatePerSecond = 1.0f;
    public float minNutrionValue = 50f;
    public float maxNutrionValue = 200f;
    public float foodMaturityAge = 20f;
    public float foodMaxAge = 60f;

    public float GetReproductionTime(bool isPredator)
    {
        return isPredator
            ? predatorReproductionTime
            : herbivoreReproductionTime;
    }

    public float GetReproductionEnergyThreshold(bool isPredator)
    {
        return isPredator
            ? predatorReproductionEnergyThreshold
            : herbivoreReproductionEnergyThreshold;
    }

    public float GetReproductionCooldown(bool isPredator, bool isFemale)
    {
        if (isPredator)
            return isFemale ? predatorFemaleReproductionCooldown : predatorMaleReproductionCooldown;

        return isFemale ? herbivoreFemaleReproductionCooldown : herbivoreMaleReproductionCooldown;
    }

    public float GetRejectedMateCooldown(bool isPredator)
    {
        return isPredator
            ? predatorRejectedMateCooldown
            : herbivoreRejectedMateCooldown;
    }

    public float GetMinMateAcceptanceChance(bool isPredator)
    {
        return isPredator
            ? predatorMinMateAcceptanceChance
            : herbivoreMinMateAcceptanceChance;
    }

    public float GetMaxMateAcceptanceChance(bool isPredator)
    {
        return isPredator
            ? predatorMaxMateAcceptanceChance
            : herbivoreMaxMateAcceptanceChance;
    }

    public int GetMinOffspringPerReproduction(bool isPredator)
    {
        return isPredator
            ? predatorMinOffspringPerReproduction
            : herbivoreMinOffspringPerReproduction;
    }

    public int GetMaxOffspringPerReproduction(bool isPredator)
    {
        return isPredator
            ? predatorMaxOffspringPerReproduction
            : herbivoreMaxOffspringPerReproduction;
    }

    public float GetOffspringCountMean(bool isPredator)
    {
        return isPredator
            ? predatorOffspringCountMean
            : herbivoreOffspringCountMean;
    }

    public float GetOffspringCountStdDev(bool isPredator)
    {
        return isPredator
            ? predatorOffspringCountStdDev
            : herbivoreOffspringCountStdDev;
    }

    public int GetMaxCreatureCountForSpecies(bool isPredator)
    {
        int herbivoreCap = Mathf.Max(0, maxHerbivoreCount);
        int predatorCap = Mathf.Max(0, maxPredatorCount);

        if (isPredator)
        {
            if (predatorCap > 0)
                return predatorCap;
        }
        else
        {
            if (herbivoreCap > 0)
                return herbivoreCap;
        }

        if (herbivoreCap <= 0 && predatorCap <= 0)
            return Mathf.Max(0, maxCreatureCount);

        return 0;
    }
}
