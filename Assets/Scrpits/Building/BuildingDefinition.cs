using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Colony/Building Definition", fileName = "BuildingDefinition")]
public class BuildingDefinition : ScriptableObject
{
    public enum EnergyProducerType
    {
        None,
        Solar,
        Wind
    }

    public string displayName;

    [Tooltip("Save/load için benzersiz id. Boşsa displayName kullanılır.")]
    public string saveId;

    public GameObject buildingPrefab;
    public GameObject ghostPrefab;

    [Header("Inşaat Maliyeti")]
    public ResourceCost[] buildCosts = Array.Empty<ResourceCost>();

    [Header("Geridönüşüm")]
    [Range(0f, 1f)]
    [Tooltip("Sökülünce buildCosts miktarının bu oranı geri verilir (PlacedBuilding + tanım kaydı gerekir).")]
    public float salvageRatio = 0.5f;

    [Header("Enerji")]
    [Min(0)]
    [Tooltip("Bu binanın çalışması için gereken enerji miktarı.")]
    public int energyNeed = 0;

    [Header("Enerji Üretimi")]
    public EnergyProducerType energyProducerType = EnergyProducerType.None;
    [Min(0f)]
    [Tooltip("Üretici binalar için baz üretim değeri.")]
    public float energyProductionBase = 0f;
    [Header("Enerji Depolama")]
    [Min(0)]
    [Tooltip("Power Collector kapasitesi (kJ). 0 ise depolama sağlamaz.")]
    public int powerCollectorCapacity = 0;

    [Header("Genel Sağlık")]
    [Min(1)]
    [Tooltip("Binanın maksimum can değeri.")]
    public int maxHealth = 100;

    [Header("Su Şebekesi Üretim / Tüketim")]
    [Tooltip("Bu bina şebekeye su üretir mi?")]
    public bool isWaterProducer = false;
    [Tooltip("Saniyedeki su üretim miktarı.")]
    public float waterProductionRate = 0f;

    [Tooltip("Bu binanın çalışmak için suya ihtiyacı var mı?")]
    public bool requiresWater = false;
    [Tooltip("Saniyedeki su tüketim miktarı.")]
    public float waterConsumptionRate = 0f;

    [Header("Su Depolama")]
    [Tooltip("Bu bina su depolar mı?")]
    public bool storesWater = false;
    [Tooltip("Su depolama kapasitesi.")]
    public float waterCapacity = 0f;

    [Header("Oksijen Tanımları (Planetbase Tipi)")]
    [Tooltip("Bu bina yaşam alanı mıdır (oksijen barındırır)?")]
    public bool storesOxygen = false;
    [Tooltip("Bu bina oksijen üretir mi?")]
    public bool isOxygenProducer = false;
    [Tooltip("Bu oksijen üretecinin destekleyebileceği maksimum kişi sayısı.")]
    public int oxygenSupportCapacity = 0;

    [Header("Yerleştirme Kuralları")]
    [Min(0)]
    [Tooltip("Bu binadan en fazla kaç tane yerleştirilebilir. 0 = sınırsız.")]
    public int maxPlacementCount = 0;

    [Tooltip("True ise bu bina exterior (dış mekan) binasıdır ve boru ile bağlanır.")]
    public bool isExterior = false;

    [Header("Bina Bağımlılıkları")]
    [Tooltip("Bu binanın yerleştirilebilmesi için sahada en az bir tane bulunması gereken binalar. Inspector'da dropdown ile seçilir.")]
    public BuildingDefinition[] requiredBuildings = Array.Empty<BuildingDefinition>();

    [Header("UI")]
    public Sprite icon;

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public int amount;
    }

    public bool HasCosts => buildCosts != null && buildCosts.Length > 0;

    /// <summary>
    /// Bağımlı olduğu tüm binalar sahada yerleştirilmiş mi?
    /// </summary>
    public bool AreDependenciesMet()
    {
        if (requiredBuildings == null || requiredBuildings.Length == 0) return true;

        // Sahadaki tüm yerleştirilmiş binaların id'lerini topla
        var placed = UnityEngine.Object.FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);

        for (int i = 0; i < requiredBuildings.Length; i++)
        {
            var req = requiredBuildings[i];
            if (req == null) continue;

            string reqId = req.GetSaveId();
            bool found = false;
            for (int j = 0; j < placed.Length; j++)
            {
                if (placed[j] != null && placed[j].definitionId == reqId)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    public bool CanAfford(ResourceInventory inv)
    {
        if (buildCosts == null || buildCosts.Length == 0) return true;
        if (inv == null) return false;
        foreach (var c in buildCosts)
        {
            if (c.amount <= 0) continue;
            if (!inv.Has(c.type, c.amount)) return false;
        }
        return true;
    }

    public string GetSaveId() => string.IsNullOrEmpty(saveId) ? displayName : saveId;

    public void ConsumeCost(ResourceInventory inv)
    {
        if (inv == null || buildCosts == null) return;
        foreach (var c in buildCosts)
        {
            if (c.amount > 0)
                inv.TryRemove(c.type, c.amount);
        }
    }
}

