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

    [Header("Creature Lifecycle")]
    public float oldAgeStartAge = 120f;
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
    public float reproductionTime = 1f;
    public float reproductionEnergyThreshold = 0.6f;
    public float blackListDuration = .5f;
    public float femaleReproductionCooldown = 45f;
    public float maleReproductionCooldown = 15f;
    public float rejectedMateCooldown = 10f;
    public float minMateAcceptanceChance = 0.2f;
    public float maxMateAcceptanceChance = 0.95f;
    public float reproductionCooldown = 30f; // legacy/general cooldown value
    public int minOffspringPerReproduction = 1;
    public int maxOffspringPerReproduction = 5;
    public float offspringCountMean = 3f;
    public float offspringCountStdDev = 1f;

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
}
