using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Scaling benchmark for comparing the MonoBehaviour and ECS simulation paths.
///
/// Usage: enable GameConfig.benchmarkEnabled before entering play mode, or press F9
/// during play. Each configured creature count is reached by spawning extra herbivores
/// (population caps are raised temporarily and restored afterwards), then frame
/// metrics are measured for benchmarkMeasureSeconds. Population dynamics keep running
/// during measurement, so actual live counts are reported next to the target.
///
/// Results: Unity console summary per scenario plus CSV and Markdown files under
/// BenchmarkResults/ at the repository root.
/// </summary>
public class SimulationBenchmark : MonoBehaviour
{
    private const string ResultsFolderName = "BenchmarkResults";

    private bool isRunning;
    private bool configOverrideActive;
    private int savedMaxHerbivoreCount;
    private int savedMaxPredatorCount;
    private readonly List<ScenarioResult> results = new List<ScenarioResult>();

    private struct ScenarioResult
    {
        public string timestamp;
        public int targetCreatures;
        public int herbivoresStart;
        public int predatorsStart;
        public int herbivoresEnd;
        public int predatorsEnd;
        public int foodStart;
        public int foodEnd;
        public string flags;
        public int frames;
        public float avgFps;
        public float avgFrameMs;
        public float avgMainThreadMs;
        public float avgGcAllocBytesPerFrame;
        public long sensorQueries;
        public int creaturesSpawned;
        public int deaths;
        public int reproductionEvents;
        public int predationAttempts;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindObjectOfType<SimulationBenchmark>() != null)
            return;

        GameObject benchmarkObject = new GameObject("SimulationBenchmark");
        benchmarkObject.AddComponent<SimulationBenchmark>();
    }

    private void Awake()
    {
        // Survive GameManager scene reloads so a running benchmark can always
        // restore the config overrides it made.
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        GameConfig config = GameConfig.Instance;
        if (config != null && config.benchmarkEnabled)
        {
            TryStartBenchmark();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            TryStartBenchmark();
        }
    }

    private void OnDestroy()
    {
        RestoreConfigOverrides();
    }

    private void OnApplicationQuit()
    {
        RestoreConfigOverrides();
    }

    public void TryStartBenchmark()
    {
        if (isRunning)
        {
            Debug.LogWarning("SimulationBenchmark: a benchmark run is already in progress.");
            return;
        }

        if (GameConfig.Instance == null || CreatureSpawner.Instance == null)
        {
            Debug.LogWarning("SimulationBenchmark: GameConfig or CreatureSpawner unavailable; cannot start.");
            return;
        }

        StartCoroutine(RunBenchmark());
    }

    private IEnumerator RunBenchmark()
    {
        isRunning = true;
        results.Clear();

        GameConfig config = GameConfig.Instance;
        int[] targets = config.benchmarkCreatureCounts != null && config.benchmarkCreatureCounts.Length > 0
            ? config.benchmarkCreatureCounts
            : new[] { 500, 1000, 2000, 5000 };

        Debug.Log($"SimulationBenchmark: starting run, targets=[{string.Join(", ", targets)}], flags=({BuildFlagsText(config)})");

        try
        {
            foreach (int target in targets)
            {
                yield return SpawnUpToTarget(config, target);
                yield return WaitUnscaled(Mathf.Max(0f, config.benchmarkWarmupSeconds));
                yield return MeasureScenario(config, target);
            }
        }
        finally
        {
            RestoreConfigOverrides();
            isRunning = false;
        }

        WriteResultFiles();
    }

    private IEnumerator SpawnUpToTarget(GameConfig config, int targetCreatures)
    {
        CreatureSpawner spawner = CreatureSpawner.Instance;
        if (spawner == null)
            yield break;

        EnsureConfigOverrides(config, targetCreatures);

        int batchSize = Mathf.Max(1, config.benchmarkSpawnBatchPerFrame);
        int spawnBudget = Mathf.Max(targetCreatures * 3, 1000);
        int spawnedThisScenario = 0;

        while (spawner.GetActiveCreatureCount() < targetCreatures && spawnBudget > 0)
        {
            int missing = targetCreatures - spawner.GetActiveCreatureCount();
            int batch = Mathf.Min(batchSize, missing);
            bool anySpawned = false;

            for (int i = 0; i < batch && spawnBudget > 0; i++)
            {
                spawnBudget--;
                CreatureSex sex = spawnedThisScenario % 2 == 0 ? CreatureSex.Female : CreatureSex.Male;
                if (spawner.SpawnRandomCreature(false, sex) != null)
                {
                    spawnedThisScenario++;
                    anySpawned = true;
                }
            }

            if (!anySpawned && !spawner.CanSpawnCreature(false))
            {
                Debug.LogWarning($"SimulationBenchmark: herbivore cap reached before target {targetCreatures}; continuing with current population.");
                break;
            }

            yield return null;
        }

        if (spawner.GetActiveCreatureCount() < targetCreatures)
        {
            Debug.LogWarning(
                $"SimulationBenchmark: target {targetCreatures} not fully reached " +
                $"(live={spawner.GetActiveCreatureCount()}); measuring with actual population.");
        }
    }

    private IEnumerator MeasureScenario(GameConfig config, int targetCreatures)
    {
        CreatureSpawner spawner = CreatureSpawner.Instance;
        Statistics statistics = Statistics.Instance;

        int herbivoresStart = CountActive(spawner?.herbivorCreatures);
        int predatorsStart = CountActive(spawner?.predatorCreatures);
        int foodStart = CountActiveFood();

        long sensorQueriesStart = SimulationPerfCounters.TotalSensorQueries;
        int spawnedStart = (statistics?.totalHerbivoresSpawned ?? 0) + (statistics?.totalPredatorsSpawned ?? 0);
        int deathsStart = (statistics?.totalHerbivoreDeaths ?? 0) + (statistics?.totalPredatorDeaths ?? 0);
        int reproductionStart = statistics?.totalReproductionEvents ?? 0;
        int predationStart = statistics?.totalPredationAttempts ?? 0;

        ProfilerRecorder mainThreadRecorder = default;
        ProfilerRecorder gcAllocRecorder = default;
        try
        {
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        }
        catch (Exception)
        {
            // Recorder unavailable in this player/editor configuration; metric stays 0.
        }

        try
        {
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        }
        catch (Exception)
        {
        }

        int frames = 0;
        float elapsed = 0f;
        double mainThreadNsSum = 0;
        int mainThreadSamples = 0;
        double gcBytesSum = 0;
        int gcSamples = 0;
        float measureSeconds = Mathf.Max(1f, config.benchmarkMeasureSeconds);

        while (elapsed < measureSeconds)
        {
            yield return null;

            frames++;
            elapsed += Time.unscaledDeltaTime;

            if (mainThreadRecorder.Valid && mainThreadRecorder.LastValue > 0)
            {
                mainThreadNsSum += mainThreadRecorder.LastValue;
                mainThreadSamples++;
            }

            if (gcAllocRecorder.Valid && gcAllocRecorder.LastValue >= 0)
            {
                gcBytesSum += gcAllocRecorder.LastValue;
                gcSamples++;
            }
        }

        if (mainThreadRecorder.Valid)
            mainThreadRecorder.Dispose();
        if (gcAllocRecorder.Valid)
            gcAllocRecorder.Dispose();

        var result = new ScenarioResult
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            targetCreatures = targetCreatures,
            herbivoresStart = herbivoresStart,
            predatorsStart = predatorsStart,
            herbivoresEnd = CountActive(spawner?.herbivorCreatures),
            predatorsEnd = CountActive(spawner?.predatorCreatures),
            foodStart = foodStart,
            foodEnd = CountActiveFood(),
            flags = BuildFlagsText(config),
            frames = frames,
            avgFps = elapsed > 0f ? frames / elapsed : 0f,
            avgFrameMs = frames > 0 ? elapsed * 1000f / frames : 0f,
            avgMainThreadMs = mainThreadSamples > 0 ? (float)(mainThreadNsSum / mainThreadSamples / 1_000_000.0) : 0f,
            avgGcAllocBytesPerFrame = gcSamples > 0 ? (float)(gcBytesSum / gcSamples) : 0f,
            sensorQueries = SimulationPerfCounters.TotalSensorQueries - sensorQueriesStart,
            creaturesSpawned = (statistics?.totalHerbivoresSpawned ?? 0) + (statistics?.totalPredatorsSpawned ?? 0) - spawnedStart,
            deaths = (statistics?.totalHerbivoreDeaths ?? 0) + (statistics?.totalPredatorDeaths ?? 0) - deathsStart,
            reproductionEvents = (statistics?.totalReproductionEvents ?? 0) - reproductionStart,
            predationAttempts = (statistics?.totalPredationAttempts ?? 0) - predationStart
        };

        results.Add(result);

        Debug.Log(
            $"SimulationBenchmark: target={result.targetCreatures} " +
            $"creatures(start/end)={result.herbivoresStart + result.predatorsStart}/{result.herbivoresEnd + result.predatorsEnd} " +
            $"food(start/end)={result.foodStart}/{result.foodEnd} | " +
            $"avgFPS={result.avgFps:F1} avgFrame={result.avgFrameMs:F2}ms mainThread={result.avgMainThreadMs:F2}ms " +
            $"gcAlloc/frame={result.avgGcAllocBytesPerFrame:F0}B | " +
            $"sensorQueries={result.sensorQueries} spawned={result.creaturesSpawned} deaths={result.deaths} " +
            $"repro={result.reproductionEvents} predation={result.predationAttempts} | flags=({result.flags})");
    }

    private void EnsureConfigOverrides(GameConfig config, int targetCreatures)
    {
        if (!configOverrideActive)
        {
            savedMaxHerbivoreCount = config.maxHerbivoreCount;
            savedMaxPredatorCount = config.maxPredatorCount;
            configOverrideActive = true;
        }

        // Only raise caps; never lower the configured ones.
        config.maxHerbivoreCount = Mathf.Max(config.maxHerbivoreCount, targetCreatures);
    }

    private void RestoreConfigOverrides()
    {
        if (!configOverrideActive)
            return;

        GameConfig config = GameConfig.Instance;
        if (config != null)
        {
            config.maxHerbivoreCount = savedMaxHerbivoreCount;
            config.maxPredatorCount = savedMaxPredatorCount;
        }

        configOverrideActive = false;
    }

    private void WriteResultFiles()
    {
        if (results.Count == 0)
            return;

        string folder = Path.Combine(Application.dataPath, "..", ResultsFolderName);
        string fileStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        try
        {
            Directory.CreateDirectory(folder);

            string csvPath = Path.Combine(folder, $"benchmark_{fileStamp}.csv");
            File.WriteAllText(csvPath, BuildCsv(), Encoding.UTF8);

            string markdownPath = Path.Combine(folder, $"benchmark_{fileStamp}.md");
            File.WriteAllText(markdownPath, BuildMarkdown(), Encoding.UTF8);

            Debug.Log($"SimulationBenchmark: results written to {Path.GetFullPath(csvPath)} and {Path.GetFullPath(markdownPath)}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"SimulationBenchmark: failed to write result files: {exception.Message}");
        }
    }

    private string BuildCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "timestamp,targetCreatures,herbivoresStart,predatorsStart,herbivoresEnd,predatorsEnd,foodStart,foodEnd," +
            "flags,frames,avgFps,avgFrameMs,avgMainThreadMs,avgGcAllocBytesPerFrame,sensorQueries," +
            "creaturesSpawned,deaths,reproductionEvents,predationAttempts");

        foreach (ScenarioResult r in results)
        {
            builder.AppendLine(string.Join(",",
                r.timestamp,
                r.targetCreatures.ToString(CultureInfo.InvariantCulture),
                r.herbivoresStart.ToString(CultureInfo.InvariantCulture),
                r.predatorsStart.ToString(CultureInfo.InvariantCulture),
                r.herbivoresEnd.ToString(CultureInfo.InvariantCulture),
                r.predatorsEnd.ToString(CultureInfo.InvariantCulture),
                r.foodStart.ToString(CultureInfo.InvariantCulture),
                r.foodEnd.ToString(CultureInfo.InvariantCulture),
                "\"" + r.flags + "\"",
                r.frames.ToString(CultureInfo.InvariantCulture),
                r.avgFps.ToString("F2", CultureInfo.InvariantCulture),
                r.avgFrameMs.ToString("F3", CultureInfo.InvariantCulture),
                r.avgMainThreadMs.ToString("F3", CultureInfo.InvariantCulture),
                r.avgGcAllocBytesPerFrame.ToString("F0", CultureInfo.InvariantCulture),
                r.sensorQueries.ToString(CultureInfo.InvariantCulture),
                r.creaturesSpawned.ToString(CultureInfo.InvariantCulture),
                r.deaths.ToString(CultureInfo.InvariantCulture),
                r.reproductionEvents.ToString(CultureInfo.InvariantCulture),
                r.predationAttempts.ToString(CultureInfo.InvariantCulture)));
        }

        return builder.ToString();
    }

    private string BuildMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Simulation benchmark results");
        builder.AppendLine();
        builder.AppendLine($"Run finished: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine($"Active flags: `{(results.Count > 0 ? results[0].flags : BuildFlagsText(GameConfig.Instance))}`");
        builder.AppendLine();
        builder.AppendLine("| Target | Creatures start→end | Food start→end | Avg FPS | Avg frame (ms) | Main thread (ms) | GC alloc/frame (B) | Sensor queries | Spawned | Deaths | Repro | Predation |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (ScenarioResult r in results)
        {
            builder.AppendLine(
                $"| {r.targetCreatures} " +
                $"| {r.herbivoresStart + r.predatorsStart}→{r.herbivoresEnd + r.predatorsEnd} " +
                $"| {r.foodStart}→{r.foodEnd} " +
                $"| {r.avgFps:F1} " +
                $"| {r.avgFrameMs:F2} " +
                $"| {r.avgMainThreadMs:F2} " +
                $"| {r.avgGcAllocBytesPerFrame:F0} " +
                $"| {r.sensorQueries} " +
                $"| {r.creaturesSpawned} " +
                $"| {r.deaths} " +
                $"| {r.reproductionEvents} " +
                $"| {r.predationAttempts} |");
        }

        builder.AppendLine();
        builder.AppendLine("Notes: population dynamics (reproduction, death, food growth) keep running during");
        builder.AppendLine("measurement, so live counts drift from the target. Compare runs with identical flags.");
        return builder.ToString();
    }

    private static string BuildFlagsText(GameConfig config)
    {
        if (config == null)
            return "config unavailable";

        return
            $"useUtilityAI={config.useUtilityAI} " +
            $"useEcsObservation={config.useEcsObservation} " +
            $"useEcsAIContext={config.useEcsAIContext} " +
            $"useEcsUtilityScoring={config.useEcsUtilityScoring} " +
            $"useEcsFoodLifecycle={config.useEcsFoodLifecycle} " +
            $"useEcsCreatureLifecycle={config.useEcsCreatureLifecycle} " +
            $"useEcsMovementExecution={config.useEcsMovementExecution} " +
            $"useEcsStatistics={config.useEcsStatistics}";
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private static int CountActive(List<BaseCreatureBehaviour> creatures)
    {
        if (creatures == null)
            return 0;

        int count = 0;
        for (int i = 0; i < creatures.Count; i++)
        {
            BaseCreatureBehaviour creature = creatures[i];
            if (creature != null && creature.gameObject.activeInHierarchy && !creature.IsDespawnQueued)
                count++;
        }

        return count;
    }

    private static int CountActiveFood()
    {
        List<Food> foods = FoodSpawner.Instance?.foods;
        if (foods == null)
            return 0;

        int count = 0;
        for (int i = 0; i < foods.Count; i++)
        {
            Food food = foods[i];
            if (food != null && food.gameObject.activeInHierarchy)
                count++;
        }

        return count;
    }
}
