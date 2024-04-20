using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SimpleLineChart : MonoBehaviour
{
    public static SimpleLineChart Instance { get; private set; }

    public RectTransform chartContainer;
    public GameObject pointPrefab;

    private List<GameObject> points;
    private List<GameObject> lines;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        //chartContainer.anchorMin = new Vector2(0, 0);
        //chartContainer.anchorMax = new Vector2(0, 0);

        //// Állítsd be a pozíciót és a méretet
        //chartContainer.anchoredPosition = Vector2.zero; // Ez a bal alsó sarokhoz helyezi
        //chartContainer.sizeDelta = new Vector2(300, 200);
    }
    void Start()
    {

        points = new List<GameObject>();
        lines = new List<GameObject>();
        for (int i = 0; i < 300; i++)
        {
            AddPoint(new Vector2(i*100, Random.Range(0,10)));
        }
    }

    public void AddPoint(Vector2 dataPoint)
    {
        GameObject point = Instantiate(pointPrefab, chartContainer);
        point.GetComponent<RectTransform>().anchoredPosition = new Vector2(dataPoint.x, dataPoint.y);
        points.Add(point);

        if (points.Count > 1)
        {
            GameObject line = new GameObject("Line");
            line.transform.SetParent(chartContainer, false);
            LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            lineRenderer.startWidth = .5f;
            lineRenderer.endWidth = .5f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = false;
            lineRenderer.SetPosition(0, points[points.Count - 2].GetComponent<RectTransform>().anchoredPosition);
            lineRenderer.SetPosition(1, points[points.Count - 1].GetComponent<RectTransform>().anchoredPosition);
            lines.Add(line);
        }
    }
    public void ClearChart()
    {
        foreach (GameObject point in points)
        {
            Destroy(point);
        }
        points.Clear();

        foreach (GameObject line in lines)
        {
            Destroy(line);
        }
        lines.Clear();
    }
}
