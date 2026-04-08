using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Colony/Building Definition", fileName = "BuildingDefinition")]
public class BuildingDefinition : ScriptableObject
{
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

