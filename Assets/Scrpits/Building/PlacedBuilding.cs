using UnityEngine;
using UnityEngine.Rendering.Universal;

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

    /// <summary>
    /// Bu objenin bir yol/boru parçası değil, gerçek bir bina olup olmadığını belirler.
    /// Yol/boru ucu prefablarında PlacedBuilding scripti bulunur fakat definitionId'leri boştur
    /// ve isimlerinde yol belirteçleri geçer.
    /// </summary>
    public bool IsRealBuilding
    {
        get
        {
            if (string.IsNullOrEmpty(definitionId)) return false;

            string nameLower = gameObject.name.ToLowerInvariant();
            if (nameLower.Contains("end-") || nameLower.Contains("staritgh-") || 
                nameLower.Contains("horizontal") || nameLower.Contains("vertical") || 
                nameLower.Contains("pipe") || nameLower.Contains("road_"))
            {
                return false;
            }

            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name.StartsWith("Road_"))
                    return false;
                current = current.parent;
            }

            if (BuildingPlacementTracker.Instance != null)
            {
                var def = BuildingPlacementTracker.Instance.GetDefinition(definitionId);
                if (def == null) return false;
            }

            return true;
        }
    }

    [Header("Saglik")]
    [Min(1)] public int maxHealth = 100;
    [Min(0)] public int currentHealth = 100;
    [HideInInspector] public float exactHealth = -1f;

    [Header("Oksijen")]
    public bool storesOxygen = false;
    public bool isOxygenProducer = false;
    [Min(0f)] public float oxygenAmount = 0f;
    [Min(0f)] public float oxygenCapacity = 100f;
    [Min(0f)] public float oxygenProductionCurrent = 0f;
    [Min(0f)] public float oxygenProductionCapacity = 0f;
    public int oxygenSupportCapacity = 0;

    [Header("Su Şebekesi")]
    public bool isWaterProducer = false;
    public float waterProductionRate = 0f;
    public bool requiresWater = false;
    public float waterConsumptionRate = 0f;
    public bool storesWater = false;
    [Min(0f)] public float waterAmount = 0f;
    [Min(0f)] public float waterCapacity = 0f;

    [Header("Simülasyon Durumu")]
    public float storedEnergy = 0f;
    public float efficiency01 = 1f;
    public bool isExterior = false;

    [Header("Ağ Şebeke Durumu (Gözlemleme)")]
    public float networkEnergyProduction = 0f;
    public float networkEnergyConsumption = 0f;
    public float networkWaterProduction = 0f;
    public float networkWaterConsumption = 0f;

    public float Health01
    {
        get
        {
            if (maxHealth <= 0) return 0f;
            return Mathf.Clamp01((float)currentHealth / maxHealth);
        }
    }
}
