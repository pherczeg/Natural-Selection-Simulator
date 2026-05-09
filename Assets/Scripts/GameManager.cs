using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public float restartTime;
    public int numberOfSimulations;
    private float timer;
    private static int counter;

    private void Awake()
    {
        if (FindObjectsOfType<GameManager>().Length > 1)
        {
            Destroy(gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }

        timer = 0;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= restartTime)
        {
            RestartGame();
        }
    }

    void RestartGame()
    {
        ExportStatisticsToCSV();
        timer = 0;
        counter++;
        if (counter >= numberOfSimulations)
        {
            Application.Quit();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ExportStatisticsToCSV()
    {
        Statistics statistics = Statistics.Instance;
        if (statistics == null)
        {
            Debug.LogWarning("Game statistics export skipped because Statistics instance is not available.");
            return;
        }

        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine(
            "Time,NumberOfCreatures,AverageWeight,AverageSpeed,AverageEnergy,AverageAge,AverageSenseRadius," +
            "AliveHerbivores,AlivePredators,FoodCount,AverageEnergyAll,AverageHerbivoreEnergy,AveragePredatorEnergy," +
            "TotalHerbivoresSpawned,TotalPredatorsSpawned,TotalFoodSpawned,TotalReproductionEvents,TotalOffspringBorn," +
            "TotalPredationAttempts,TotalPredationSuccesses,TotalPredationEscapes,TotalHerbivoreDeaths,TotalPredatorDeaths," +
            "DeathsByEnergy,DeathsByPredation,DeathsUnknown,HerbivoreSurvivalRate,PredatorSurvivalRate,StateDistribution");

        for (int i = 0; i < statistics.numberOfCreaturesHistory.Count; i++)
        {
            SimulationDiagnosticsSnapshot diagnostics = statistics.diagnosticsHistory != null && i < statistics.diagnosticsHistory.Count
                ? statistics.diagnosticsHistory[i]
                : null;

            csvContent.AppendLine(string.Join(",", new[]
            {
                FormatFloat(i * statistics.updateInterval),
                statistics.numberOfCreaturesHistory[i].ToString(CultureInfo.InvariantCulture),
                FormatFloat(statistics.averageWeightHistory[i]),
                FormatFloat(statistics.averageSpeedHistory[i]),
                FormatFloat(statistics.averageEnergyHistory[i]),
                FormatFloat(statistics.averageAgeHistory[i]),
                FormatFloat(statistics.averageSenseRadiusHistory[i]),
                FormatDiagnosticInt(diagnostics, d => d.aliveHerbivores),
                FormatDiagnosticInt(diagnostics, d => d.alivePredators),
                FormatDiagnosticInt(diagnostics, d => d.foodCount),
                FormatDiagnosticFloat(diagnostics, d => d.averageEnergy),
                FormatDiagnosticFloat(diagnostics, d => d.averageHerbivoreEnergy),
                FormatDiagnosticFloat(diagnostics, d => d.averagePredatorEnergy),
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
                FormatDiagnosticInt(diagnostics, d => d.deathsUnknown),
                FormatDiagnosticFloat(diagnostics, d => d.herbivoreSurvivalRate),
                FormatDiagnosticFloat(diagnostics, d => d.predatorSurvivalRate),
                CsvEscape(diagnostics != null ? diagnostics.GetStateDistributionText() : string.Empty)
            }));
        }

        string filePath = Path.Combine(Application.persistentDataPath, $"game_statistics_{counter}.csv");
        File.WriteAllText(filePath, csvContent.ToString());

        Debug.Log($"Game statistics exported to {filePath}");

        statistics.Reset();
    }

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
