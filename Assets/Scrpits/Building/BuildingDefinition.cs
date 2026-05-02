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

    [Header("UI")]
    public Sprite icon;

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public int amount;
    }

    public bool HasCosts => buildCosts != null && buildCosts.Length > 0;

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

