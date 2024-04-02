using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public float restartTime;
    private float timer;
    private Statistics statistics;
    private static int counter;
    private void Awake()
    {
        if (FindObjectsOfType<GameManager>().Length > 1)
        {
            // Ha már létezik egy másik GameManager példány, akkor ezt elpusztítjuk,
            // hogy ne legyen több példány
            Destroy(gameObject);
        }
        else
        {
            // Megakadályozzuk ennek a GameObjectnek a megsemmisítését új jelenetek betöltésekor
            DontDestroyOnLoad(gameObject);
        }
        timer = 0;
        counter = 0;
        statistics = FindFirstObjectByType<Statistics>();
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ExportStatisticsToCSV()
    {
        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine("Time,NumberOfCreatures,AverageWeight,AverageSpeed,AverageEnergy,AverageAge,AverageSenseRadius");

        // Feltételezve, hogy a history listák hossza azonos
        for (int i = 0; i < statistics.numberOfCreaturesHistory.Count; i++)
        {
            csvContent.AppendLine($"{i * statistics.updateInterval},{statistics.numberOfCreaturesHistory[i]},{statistics.averageWeightHistory[i]},{statistics.averageSpeedHistory[i]},{statistics.averageEnergyHistory[i]},{statistics.averageAgeHistory[i]},{statistics.averageSenseRadiusHistory[i]}");
        }

        string filePath = Path.Combine(Application.persistentDataPath, $"game_statistics_{counter++}.csv");
        File.WriteAllText(filePath, csvContent.ToString());

        Debug.Log($"Game statistics exported to {filePath}");

        // Statistikák resetelése az újraindítás elõtt
        statistics.Reset();
    }
}
