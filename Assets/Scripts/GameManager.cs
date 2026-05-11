using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Min(0.01f)]
    public float restartTime;
    [Min(1)]
    public int numberOfSimulations;

    private static GameManager instance;

    private float timer;
    private int completedSimulations;
    private bool hasFinished;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        timer = 0;
        completedSimulations = 0;
        hasFinished = false;
        Debug.Log($"Simulation batch started: {TotalSimulationCount} run(s), {SimulationDuration:0.###}s per run.");
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (hasFinished)
            return;

        timer += Time.deltaTime;
        if (timer >= SimulationDuration)
        {
            FinishCurrentSimulation();
        }
    }

    void FinishCurrentSimulation()
    {
        if (hasFinished)
            return;

        int simulationNumber = completedSimulations + 1;
        ExportStatisticsToCSV(simulationNumber);
        completedSimulations++;

        Debug.Log($"Simulation {completedSimulations}/{TotalSimulationCount} completed after {timer:0.###}s.");

        timer = 0;
        if (completedSimulations >= TotalSimulationCount)
        {
            FinishBatch();
            return;
        }

        Debug.Log($"Starting simulation {completedSimulations + 1}/{TotalSimulationCount}.");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void FinishBatch()
    {
        hasFinished = true;
        Debug.Log($"Simulation batch finished: {completedSimulations}/{TotalSimulationCount} run(s) exported.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ExportStatisticsToCSV(int simulationNumber)
    {
        Statistics statistics = Statistics.Instance;
        if (statistics == null)
        {
            Debug.LogWarning($"Simulation {simulationNumber} statistics export skipped because Statistics instance is not available.");
            return;
        }

        statistics.CaptureSnapshot();

        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine(
            "Time,NumberOfCreatures,AverageWeight,AverageSpeed,AverageEnergy,AverageAge,AverageSenseRadius," +
            "AliveHerbivores,AliveDoves,AliveHawks,DoveRatio,HawkRatio,AlivePredators,FoodCount," +
            "AverageEnergyAll,AverageHerbivoreEnergy,AveragePredatorEnergy," +
            "HerbivoreUtilityKeepWeight,HerbivoreUtilityFoodWeight,HerbivoreUtilityMateWeight,HerbivoreUtilityWanderWeight," +
            "PredatorUtilityKeepWeight,PredatorUtilityFoodWeight,PredatorUtilityMateWeight,PredatorUtilityWanderWeight," +
            "TotalHerbivoresSpawned,TotalPredatorsSpawned,TotalFoodSpawned,TotalReproductionEvents,TotalOffspringBorn," +
            "TotalPredationAttempts,TotalPredationSuccesses,TotalPredationEscapes,TotalHerbivoreDeaths,TotalPredatorDeaths," +
            "DeathsByEnergy,DeathsByPredation,DeathsByOldAge,DeathsUnknown,HerbivoreSurvivalRate,PredatorSurvivalRate,StateDistribution");

        for (int i = 0; i < statistics.numberOfCreaturesHistory.Count; i++)
        {
            SimulationDiagnosticsSnapshot diagnostics = statistics.diagnosticsHistory != null && i < statistics.diagnosticsHistory.Count
                ? statistics.diagnosticsHistory[i]
                : null;
            float sampleTime = diagnostics != null ? diagnostics.elapsedTime : i * statistics.updateInterval;

            csvContent.AppendLine(string.Join(",", new[]
            {
                FormatFloat(sampleTime),
                statistics.numberOfCreaturesHistory[i].ToString(CultureInfo.InvariantCulture),
                FormatFloat(statistics.averageWeightHistory[i]),
                FormatFloat(statistics.averageSpeedHistory[i]),
                FormatFloat(statistics.averageEnergyHistory[i]),
                FormatFloat(statistics.averageAgeHistory[i]),
                FormatFloat(statistics.averageSenseRadiusHistory[i]),
                FormatDiagnosticInt(diagnostics, d => d.aliveHerbivores),
                FormatDiagnosticInt(diagnostics, d => d.aliveDoves),
                FormatDiagnosticInt(diagnostics, d => d.aliveHawks),
                FormatDiagnosticFloat(diagnostics, d => d.doveRatio),
                FormatDiagnosticFloat(diagnostics, d => d.hawkRatio),
                FormatDiagnosticInt(diagnostics, d => d.alivePredators),
                FormatDiagnosticInt(diagnostics, d => d.foodCount),
                FormatDiagnosticFloat(diagnostics, d => d.averageEnergy),
                FormatDiagnosticFloat(diagnostics, d => d.averageHerbivoreEnergy),
                FormatDiagnosticFloat(diagnostics, d => d.averagePredatorEnergy),
                FormatDiagnosticFloat(diagnostics, d => d.herbivoreUtilityKeepCurrentStateWeight),
                FormatDiagnosticFloat(diagnostics, d => d.herbivoreUtilityFoodActionWeight),
                FormatDiagnosticFloat(diagnostics, d => d.herbivoreUtilitySearchMateWeight),
                FormatDiagnosticFloat(diagnostics, d => d.herbivoreUtilityWanderWeight),
                FormatDiagnosticFloat(diagnostics, d => d.predatorUtilityKeepCurrentStateWeight),
                FormatDiagnosticFloat(diagnostics, d => d.predatorUtilityFoodActionWeight),
                FormatDiagnosticFloat(diagnostics, d => d.predatorUtilitySearchMateWeight),
                FormatDiagnosticFloat(diagnostics, d => d.predatorUtilityWanderWeight),
                FormatDiagnosticInt(diagnostics, d => d.totalHerbivoresSpawned),
                FormatDiagnosticInt(diagnostics, d => d.totalPredatorsSpawned),
                FormatDiagnosticInt(diagnostics, d => d.totalFoodSpawned),
                FormatDiagnosticInt(diagnostics, d => d.totalReproductionEvents),
                FormatDiagnosticInt(diagnostics, d => d.totalOffspringBorn),
                FormatDiagnosticInt(diagnostics, d => d.totalPredationAttempts),
                FormatDiagnosticInt(diagnostics, d => d.totalPredationSuccesses),
                FormatDiagnosticInt(diagnostics, d => d.totalPredationEscapes),
                FormatDiagnosticInt(diagnostics, d => d.totalHerbivoreDeaths),
                FormatDiagnosticInt(diagnostics, d => d.totalPredatorDeaths),
                FormatDiagnosticInt(diagnostics, d => d.deathsByEnergy),
                FormatDiagnosticInt(diagnostics, d => d.deathsByPredation),
                FormatDiagnosticInt(diagnostics, d => d.deathsByOldAge),
                FormatDiagnosticInt(diagnostics, d => d.deathsUnknown),
                FormatDiagnosticFloat(diagnostics, d => d.herbivoreSurvivalRate),
                FormatDiagnosticFloat(diagnostics, d => d.predatorSurvivalRate),
                CsvEscape(diagnostics != null ? diagnostics.GetStateDistributionText() : string.Empty)
            }));
        }

        string filePath = Path.Combine(Application.persistentDataPath, $"game_statistics_{simulationNumber:000}.csv");
        File.WriteAllText(filePath, csvContent.ToString());

        Debug.Log($"Simulation {simulationNumber}/{TotalSimulationCount} statistics exported to {filePath}");

        statistics.Reset();
    }

    private int TotalSimulationCount => Mathf.Max(1, numberOfSimulations);
    private float SimulationDuration => Mathf.Max(0.01f, restartTime);

    private static string FormatDiagnosticInt(
        SimulationDiagnosticsSnapshot snapshot,
        Func<SimulationDiagnosticsSnapshot, int> selector)
    {
        return snapshot == null ? string.Empty : selector(snapshot).ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatDiagnosticFloat(
        SimulationDiagnosticsSnapshot snapshot,
        Func<SimulationDiagnosticsSnapshot, float> selector)
    {
        return snapshot == null ? string.Empty : FormatFloat(selector(snapshot));
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n") && !value.Contains("\r"))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
