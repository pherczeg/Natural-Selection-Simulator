using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EnergyBar : MonoBehaviour
{
    [SerializeField] private Renderer energyBarRenderer;

    private Camera mainCamera;

    private void Awake()
    {
        if (energyBarRenderer == null)
        {
            energyBarRenderer = GetComponent<Renderer>();
        }

        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    private void FaceCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                return;
            }
        }

        transform.rotation = mainCamera.transform.rotation;
    }

    public void SetEnergy(float currentEnergy, float maxEnergy)
    {
        float fillAmount = maxEnergy > 0f
            ? Mathf.Clamp01(currentEnergy / maxEnergy)
            : 0f;

        energyBarRenderer.material.SetFloat("_Energy", fillAmount);
    }
}