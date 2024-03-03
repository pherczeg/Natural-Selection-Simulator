using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EnergyBar : MonoBehaviour
{
    private Renderer rend;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    public void SetEnergy(float currentEnergy, float maxEnergy)
    {
        float fillAmount = currentEnergy / maxEnergy;
        rend.material.SetFloat("_Energy", fillAmount);
    }
}
