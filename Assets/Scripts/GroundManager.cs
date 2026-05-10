using UnityEngine;

public class GroundManager : MonoBehaviour
{
    public static GroundManager Instance { get; private set; }

    public Bounds GroundBounds { get; private set; }
    public float GroundSurfaceY { get; private set; }
    public bool HasGroundBounds { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            InitializeBounds();
        }
    }

    private void InitializeBounds()
    {
        GameObject ground = GameObject.FindGameObjectWithTag("Ground");
        if (ground != null)
        {
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                GroundBounds = groundRenderer.bounds;
                GroundSurfaceY = GroundBounds.max.y;
                HasGroundBounds = true;
            }
            else
            {
                Debug.LogError("GroundManager: Ground object does not have a Renderer component.");
            }
        }
        else
        {
            Debug.LogError("GroundManager: No object found with the 'Ground' tag.");
        }
    }
}
