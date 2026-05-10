using UnityEngine;

public class SimulationDiagnostics : MonoBehaviour
{
    [Min(0.1f)]
    public float logInterval = 5f;
    public bool logOnStart = true;

    private float timer;

    private void Start()
    {
        timer = 0f;

        if (logOnStart)
        {
            LogSnapshot();
        }
    }

    private void Update()
    {
        if (logInterval <= 0f)
            return;

        timer += Time.deltaTime;
        if (timer < logInterval)
            return;

        timer = 0f;
        LogSnapshot();
    }

    public void LogSnapshot()
    {
        SimulationDiagnosticsSnapshot snapshot = Statistics.BuildDiagnosticsSnapshot(Statistics.Instance);
        StackTraceLogType previousStackTraceLogType = Application.GetStackTraceLogType(LogType.Log);
        try
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Debug.Log(Statistics.FormatDiagnosticsSnapshot(snapshot));
        }
        finally
        {
            Application.SetStackTraceLogType(LogType.Log, previousStackTraceLogType);
        }
    }
}
