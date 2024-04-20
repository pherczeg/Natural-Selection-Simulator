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
        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine("Time,NumberOfCreatures,AverageWeight,AverageSpeed,AverageEnergy,AverageAge,AverageSenseRadius");

        // Feltételezve, hogy a history listák hossza azonos
        for (int i = 0; i < Statistics.Instance.numberOfCreaturesHistory.Count; i++)
        {
            csvContent.AppendLine($"{i * Statistics.Instance.updateInterval},{Statistics.Instance.numberOfCreaturesHistory[i]},{Statistics.Instance.averageWeightHistory[i]},{Statistics.Instance.averageSpeedHistory[i]},{Statistics.Instance.averageEnergyHistory[i]},{Statistics.Instance.averageAgeHistory[i]},{Statistics.Instance.averageSenseRadiusHistory[i]}");
        }

        string filePath = Path.Combine(Application.persistentDataPath, $"game_statistics_{counter}.csv");
        File.WriteAllText(filePath, csvContent.ToString());

        Debug.Log($"Game statistics exported to {filePath}");

        // Statistikák resetelése az újraindítás elõtt
        Statistics.Instance.Reset();
    }
}
