using System.Text;
using UnityEngine;

public class RealtimeSimulationStatsUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private bool showPanel = true;
    [SerializeField] private bool anchorToRight = true;
    [SerializeField] private Vector2 panelOffset = new Vector2(16f, 16f);
    [SerializeField] private float panelWidth = 440f;
    [SerializeField] private float panelHeight = 620f;
    [SerializeField] private int headerFontSize = 16;
    [SerializeField] private int bodyFontSize = 13;

    [Header("Refresh")]
    [Min(0.05f)]
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly StringBuilder builder = new StringBuilder(1024);

    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;
    private Vector2 bodyScrollPosition;

    private SimulationDiagnosticsSnapshot snapshot;
    private SpeciesAverages herbivoreAverages;
    private SpeciesAverages predatorAverages;
    private float nextRefreshAt;

    private struct SpeciesAverages
    {
        public int count;
        public int femaleCount;
        public int maleCount;
        public int doveCount;
        public int hawkCount;
        public float doveRatio;
        public float hawkRatio;
        public float averageWeight;
        public float averageSpeed;
        public float averageSense;
        public float averageAge;
        public float averageEnergy;
        public float averageDesirability;
        public float averageAgility;
        public float averageStrength;
        public float averageUtilityKeepCurrentStateWeight;
        public float averageUtilityFoodActionWeight;
        public float averageUtilitySearchMateWeight;
        public float averageUtilityWanderWeight;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindObjectOfType<RealtimeSimulationStatsUI>() != null)
            return;

        GameObject panelObject = new GameObject("RealtimeSimulationStatsUI");
        panelObject.AddComponent<RealtimeSimulationStatsUI>();
    }

    private void OnEnable()
    {
        RefreshSnapshot(force: true);
    }

    private void Update()
    {
        RefreshSnapshot(force: false);
    }

    private void RefreshSnapshot(bool force)
    {
        float now = Time.unscaledTime;
        if (!force && now < nextRefreshAt)
            return;

        nextRefreshAt = now + Mathf.Max(0.05f, refreshInterval);
        snapshot = Statistics.BuildDiagnosticsSnapshot(Statistics.Instance);
        herbivoreAverages = ComputeSpeciesAverages(CreatureSpawner.Instance?.herbivorCreatures);
        predatorAverages = ComputeSpeciesAverages(CreatureSpawner.Instance?.predatorCreatures);

        if (ECSStatisticsMirror.IsEnabled(GameConfig.Instance) &&
            ECSStatisticsMirror.TryGet(
                out EcsSpeciesAggregateData ecsHerbivores,
                out EcsSpeciesAggregateData ecsPredators,
                out _))
        {
            ApplyEcsAggregates(ref herbivoreAverages, ecsHerbivores);
            ApplyEcsAggregates(ref predatorAverages, ecsPredators);
        }
    }

    /// <summary>
    /// Overlays the fields covered by the ECS statistics aggregation; current
    /// speed/sense, desirability, social strategy, agility/strength and utility
    /// weights are not mirrored in ECS and keep their polled values.
    /// </summary>
    private static void ApplyEcsAggregates(ref SpeciesAverages averages, in EcsSpeciesAggregateData aggregates)
    {
        averages.count = aggregates.count;
        averages.femaleCount = aggregates.femaleCount;
        averages.maleCount = aggregates.maleCount;
        averages.averageWeight = aggregates.averageWeight;
        averages.averageAge = aggregates.averageAge;
        averages.averageEnergy = aggregates.averageEnergy;
    }

    private void OnGUI()
    {
        if (!showPanel)
            return;

        EnsureStyles();

        Rect panelRect = GetPanelRect();
        GUI.Box(panelRect, GUIContent.none);

        Rect contentRect = new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 20f, panelRect.height - 16f);
        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 24f);
        Rect bodyRect = new Rect(contentRect.x, contentRect.y + 26f, contentRect.width, contentRect.height - 26f);

        GUI.Label(headerRect, "Realtime simulation stats", headerStyle);

        if (snapshot == null)
        {
            GUI.Label(bodyRect, "Statistics snapshot unavailable.", bodyStyle);
            return;
        }

        BuildStatsText(snapshot, herbivoreAverages, predatorAverages, builder);
        string bodyText = builder.ToString();
        float contentWidth = Mathf.Max(10f, bodyRect.width - 20f);
        float contentHeight = bodyStyle.CalcHeight(new GUIContent(bodyText), contentWidth);
        Rect scrollViewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(bodyRect.height, contentHeight + 6f));

        bodyScrollPosition = GUI.BeginScrollView(bodyRect, bodyScrollPosition, scrollViewRect, false, true);
        GUI.Label(new Rect(0f, 0f, scrollViewRect.width, scrollViewRect.height), bodyText, bodyStyle);
        GUI.EndScrollView();
    }

    private Rect GetPanelRect()
    {
        float x = anchorToRight
            ? Mathf.Max(0f, Screen.width - panelWidth - panelOffset.x)
            : panelOffset.x;
        float y = panelOffset.y;

        return new Rect(x, y, panelWidth, panelHeight);
    }

    private void BuildStatsText(
        SimulationDiagnosticsSnapshot diagnostics,
        SpeciesAverages herbivore,
        SpeciesAverages predator,
        StringBuilder output)
    {
        output.Clear();

        int totalAlive = diagnostics.aliveHerbivores + diagnostics.alivePredators;
        int totalDeaths = diagnostics.totalHerbivoreDeaths + diagnostics.totalPredatorDeaths;
        float predationSuccessRate = CalculateRate(diagnostics.totalPredationSuccesses, diagnostics.totalPredationAttempts);

        output.Append("Time: ").Append(diagnostics.elapsedTime.ToString("0.0")).Append("s\n\n");

        output.Append("Alive total: ").Append(totalAlive).Append('\n');
        output.Append("Alive herbivores: ").Append(diagnostics.aliveHerbivores).Append('\n');
        output.Append("Alive predators: ").Append(diagnostics.alivePredators).Append('\n');
        output.Append("Doves / Hawks: ")
            .Append(diagnostics.aliveDoves)
            .Append(" / ")
            .Append(diagnostics.aliveHawks)
            .Append('\n');
        output.Append("Dove ratio: ").Append(FormatPercent(diagnostics.doveRatio)).Append('\n');
        output.Append("Hawk ratio: ").Append(FormatPercent(diagnostics.hawkRatio)).Append('\n');
        output.Append("Food count: ").Append(diagnostics.foodCount).Append("\n\n");

        output.Append("Avg energy all: ").Append(diagnostics.averageEnergy.ToString("0.0")).Append('\n');
        output.Append("Avg energy herbivore: ").Append(diagnostics.averageHerbivoreEnergy.ToString("0.0")).Append('\n');
        output.Append("Avg energy predator: ").Append(diagnostics.averagePredatorEnergy.ToString("0.0")).Append('\n');
        output.Append("Repro ready H/P: ")
            .Append(diagnostics.reproductionReadyHerbivores)
            .Append(" / ")
            .Append(diagnostics.reproductionReadyPredators)
            .Append("\n\n");

        output.Append("Spawned H/P/F: ")
            .Append(diagnostics.totalHerbivoresSpawned)
            .Append(" / ")
            .Append(diagnostics.totalPredatorsSpawned)
            .Append(" / ")
            .Append(diagnostics.totalFoodSpawned)
            .Append('\n');
        output.Append("Reproduction events: ").Append(diagnostics.totalReproductionEvents).Append('\n');
        output.Append("Offspring born: ").Append(diagnostics.totalOffspringBorn).Append('\n');
        output.Append("Predation attempts/success/escape: ")
            .Append(diagnostics.totalPredationAttempts)
            .Append(" / ")
            .Append(diagnostics.totalPredationSuccesses)
            .Append(" / ")
            .Append(diagnostics.totalPredationEscapes)
            .Append('\n');
        output.Append("Predation success rate: ")
            .Append(FormatPercent(predationSuccessRate))
            .Append("\n\n");

        output.Append("Deaths total H/P/all: ")
            .Append(diagnostics.totalHerbivoreDeaths)
            .Append(" / ")
            .Append(diagnostics.totalPredatorDeaths)
            .Append(" / ")
            .Append(totalDeaths)
            .Append('\n');
        output.Append("Deaths energy/predation/old/unknown: ")
            .Append(diagnostics.deathsByEnergy)
            .Append(" / ")
            .Append(diagnostics.deathsByPredation)
            .Append(" / ")
            .Append(diagnostics.deathsByOldAge)
            .Append(" / ")
            .Append(diagnostics.deathsUnknown)
            .Append('\n');
        output.Append("Survival H/P: ")
            .Append(FormatPercent(diagnostics.herbivoreSurvivalRate))
            .Append(" / ")
            .Append(FormatPercent(diagnostics.predatorSurvivalRate))
            .Append("\n\n");

        output.Append("Herbivore avg attr (n=").Append(herbivore.count).Append("): \n");
        output.Append("  Sex F/M: ").Append(herbivore.femaleCount).Append(" / ").Append(herbivore.maleCount).Append('\n');
        output.Append("  Social D/H: ").Append(herbivore.doveCount).Append(" / ").Append(herbivore.hawkCount).Append('\n');
        output.Append("  Social ratio D/H: ")
            .Append(FormatPercent(herbivore.doveRatio))
            .Append(" / ")
            .Append(FormatPercent(herbivore.hawkRatio))
            .Append('\n');
        output.Append("  Weight: ").Append(FormatFloat(herbivore.averageWeight)).Append('\n');
        output.Append("  Speed: ").Append(FormatFloat(herbivore.averageSpeed)).Append('\n');
        output.Append("  Sense: ").Append(FormatFloat(herbivore.averageSense)).Append('\n');
        output.Append("  Age: ").Append(FormatFloat(herbivore.averageAge)).Append('\n');
        output.Append("  Energy: ").Append(FormatFloat(herbivore.averageEnergy)).Append('\n');
        output.Append("  Desirability: ").Append(FormatFloat(herbivore.averageDesirability)).Append('\n');
        output.Append("  Agility: ").Append(FormatFloat(herbivore.averageAgility)).Append("\n\n");
        output.Append("  Utility keep/food/mate/wander: ")
            .Append(FormatFloat(herbivore.averageUtilityKeepCurrentStateWeight)).Append(" / ")
            .Append(FormatFloat(herbivore.averageUtilityFoodActionWeight)).Append(" / ")
            .Append(FormatFloat(herbivore.averageUtilitySearchMateWeight)).Append(" / ")
            .Append(FormatFloat(herbivore.averageUtilityWanderWeight)).Append("\n\n");

        output.Append("Predator avg attr (n=").Append(predator.count).Append("): \n");
        output.Append("  Sex F/M: ").Append(predator.femaleCount).Append(" / ").Append(predator.maleCount).Append('\n');
        output.Append("  Weight: ").Append(FormatFloat(predator.averageWeight)).Append('\n');
        output.Append("  Speed: ").Append(FormatFloat(predator.averageSpeed)).Append('\n');
        output.Append("  Sense: ").Append(FormatFloat(predator.averageSense)).Append('\n');
        output.Append("  Age: ").Append(FormatFloat(predator.averageAge)).Append('\n');
        output.Append("  Energy: ").Append(FormatFloat(predator.averageEnergy)).Append('\n');
        output.Append("  Desirability: ").Append(FormatFloat(predator.averageDesirability)).Append('\n');
        output.Append("  Strength: ").Append(FormatFloat(predator.averageStrength)).Append('\n');
        output.Append("  Utility keep/food/mate/wander: ")
            .Append(FormatFloat(predator.averageUtilityKeepCurrentStateWeight)).Append(" / ")
            .Append(FormatFloat(predator.averageUtilityFoodActionWeight)).Append(" / ")
            .Append(FormatFloat(predator.averageUtilitySearchMateWeight)).Append(" / ")
            .Append(FormatFloat(predator.averageUtilityWanderWeight)).Append("\n\n");

        output.Append("States: ").Append(diagnostics.GetStateDistributionText());
    }

    private static SpeciesAverages ComputeSpeciesAverages(System.Collections.Generic.List<BaseCreatureBehaviour> source)
    {
        if (source == null)
            return default;

        float sumWeight = 0f;
        float sumSpeed = 0f;
        float sumSense = 0f;
        float sumAge = 0f;
        float sumEnergy = 0f;
        float sumDesirability = 0f;
        float sumAgility = 0f;
        float sumStrength = 0f;
        float sumUtilityKeepCurrentStateWeight = 0f;
        float sumUtilityFoodActionWeight = 0f;
        float sumUtilitySearchMateWeight = 0f;
        float sumUtilityWanderWeight = 0f;
        int count = 0;
        int femaleCount = 0;
        int maleCount = 0;
        int doveCount = 0;
        int hawkCount = 0;

        for (int i = 0; i < source.Count; i++)
        {
            BaseCreatureBehaviour creature = source[i];
            if (creature == null ||
                !creature.gameObject.activeInHierarchy ||
                creature.IsDespawnQueued ||
                creature.MovementManager == null ||
                creature.ObservationManager == null ||
                creature.AgeManager == null ||
                creature.EnergyManager == null ||
                creature.ReproductionManager == null)
            {
                continue;
            }

            count++;
            if (creature.Sex == CreatureSex.Female)
                femaleCount++;
            else if (creature.Sex == CreatureSex.Male)
                maleCount++;

            sumWeight += creature.Weight;
            sumSpeed += creature.MovementManager.MoveSpeed;
            sumSense += creature.ObservationManager.SenseRadius;
            sumAge += creature.AgeManager.Age;
            sumEnergy += creature.EnergyManager.EnergyLevel;
            sumDesirability += creature.ReproductionManager.Desirability;
            CreatureUtilityBehaviorData utilityProfile = UtilityBehaviorScoring.Sanitize(creature.UtilityBehaviorProfile);
            sumUtilityKeepCurrentStateWeight += utilityProfile.keepCurrentStateWeight;
            sumUtilityFoodActionWeight += utilityProfile.foodActionWeight;
            sumUtilitySearchMateWeight += utilityProfile.searchMateWeight;
            sumUtilityWanderWeight += utilityProfile.wanderWeight;

            if (creature is HerbivoreBehaviour herbivore)
            {
                sumAgility += herbivore.Agility;

                if (herbivore.SocialStrategy == HerbivoreSocialStrategy.Dove)
                    doveCount++;
                else if (herbivore.SocialStrategy == HerbivoreSocialStrategy.Hawk)
                    hawkCount++;
            }
            else if (creature is PredatorBehaviour predator)
            {
                sumStrength += predator.Strength;
            }
        }

        if (count <= 0)
            return default;

        float divisor = count;
        return new SpeciesAverages
        {
            count = count,
            femaleCount = femaleCount,
            maleCount = maleCount,
            doveCount = doveCount,
            hawkCount = hawkCount,
            doveRatio = doveCount / divisor,
            hawkRatio = hawkCount / divisor,
            averageWeight = sumWeight / divisor,
            averageSpeed = sumSpeed / divisor,
            averageSense = sumSense / divisor,
            averageAge = sumAge / divisor,
            averageEnergy = sumEnergy / divisor,
            averageDesirability = sumDesirability / divisor,
            averageAgility = sumAgility / divisor,
            averageStrength = sumStrength / divisor,
            averageUtilityKeepCurrentStateWeight = sumUtilityKeepCurrentStateWeight / divisor,
            averageUtilityFoodActionWeight = sumUtilityFoodActionWeight / divisor,
            averageUtilitySearchMateWeight = sumUtilitySearchMateWeight / divisor,
            averageUtilityWanderWeight = sumUtilityWanderWeight / divisor
        };
    }

    private void EnsureStyles()
    {
        if (headerStyle != null && bodyStyle != null)
            return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, headerFontSize),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(9, bodyFontSize),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
    }

    private static string FormatPercent(float value)
    {
        return (value * 100f).ToString("0.0") + "%";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.00");
    }

    private static float CalculateRate(int numerator, int denominator)
    {
        if (denominator <= 0)
            return 0f;

        return (float)numerator / denominator;
    }
}