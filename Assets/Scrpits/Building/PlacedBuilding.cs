using UnityEngine;

/// <summary>
/// Yerleştirilmiş binada save/load için tanım id tutar.
/// </summary>
public class PlacedBuilding : MonoBehaviour
{
    public string definitionId;
    public int energyNeed;
    public BuildingDefinition.EnergyProducerType energyProducerType;
    public float energyProductionBase;
    public int powerCollectorCapacity;
    [Range(0f, 1f)] public float energyRamp01 = 1f;

    [Header("Saglik")]
    [Min(1)] public int maxHealth = 100;
    [Min(0)] public int currentHealth = 100;

    [Header("Oksijen")]
    public bool isOxygenProducer = false;
    [Min(0f)] public float oxygenAmount = 0f;
    [Min(0f)] public float oxygenCapacity = 0f;
    [Min(0f)] public float oxygenProductionCurrent = 0f;
    [Min(0f)] public float oxygenProductionCapacity = 0f;

    public float Health01
    {
        get
        {
            if (maxHealth <= 0) return 0f;
            return Mathf.Clamp01((float)currentHealth / maxHealth);
        }
    }

    public float OxygenRow01
    {
        get
        {
            if (isOxygenProducer)
            {
                if (oxygenProductionCapacity <= 0f) return 0f;
                return Mathf.Clamp01(oxygenProductionCurrent / oxygenProductionCapacity);
            }

            if (oxygenCapacity <= 0f) return 0f;
            return Mathf.Clamp01(oxygenAmount / oxygenCapacity);
        }
    }
}
