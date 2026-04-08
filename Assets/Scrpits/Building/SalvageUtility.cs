using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Söküm miktarını hesaplar; dünyaya düşürür (SalvageDropManager) veya envantere yedekler.
/// </summary>
public static class SalvageUtility
{
    public static void DropSalvageAt(GameObject obj, Vector3 worldPosition)
    {
        if (obj == null)
            return;
        var totals = new Dictionary<ResourceType, int>();
        if (!TryComputeSalvageTotals(obj, totals) || totals.Count == 0)
            return;

        if (SalvageDropManager.Instance != null)
            SalvageDropManager.Instance.SpawnDropsAt(worldPosition, totals);
        else
            RefundToInventory(totals);
    }

    static void RefundToInventory(Dictionary<ResourceType, int> totals)
    {
        ResourceManager rm = ResourceManager.Instance;
        if (rm == null) return;
        foreach (var kv in totals)
        {
            if (kv.Value > 0)
                rm.Add(kv.Key, kv.Value);
        }
    }

    static bool TryComputeSalvageTotals(GameObject obj, Dictionary<ResourceType, int> totals)
    {
        totals.Clear();
        if (obj == null)
            return false;

        PlacedBuilding pb = obj.GetComponent<PlacedBuilding>();
        if (pb == null) pb = obj.GetComponentInParent<PlacedBuilding>();
        if (pb == null) pb = obj.GetComponentInChildren<PlacedBuilding>(true);

        if (pb != null && !string.IsNullOrEmpty(pb.definitionId))
        {
            BuildingPlacementTracker tr = BuildingPlacementTracker.Instance;
            if (tr != null)
            {
                BuildingDefinition def = tr.GetDefinition(pb.definitionId);
                if (def != null && def.buildCosts != null && def.buildCosts.Length > 0)
                {
                    float ratio = Mathf.Clamp01(def.salvageRatio);
                    foreach (BuildingDefinition.ResourceCost c in def.buildCosts)
                    {
                        if (c.amount <= 0) continue;
                        int back = Mathf.FloorToInt(c.amount * ratio);
                        if (back > 0)
                            AddTotal(totals, c.type, back);
                    }
                    return totals.Count > 0;
                }
            }
        }

        SalvageLoot loot = obj.GetComponent<SalvageLoot>();
        if (loot == null) loot = obj.GetComponentInParent<SalvageLoot>();
        if (loot == null) loot = obj.GetComponentInChildren<SalvageLoot>(true);
        if (loot != null)
            loot.AddTotalsTo(totals);

        return totals.Count > 0;
    }

    static void AddTotal(Dictionary<ResourceType, int> totals, ResourceType type, int add)
    {
        if (add <= 0) return;
        if (totals.ContainsKey(type))
            totals[type] += add;
        else
            totals[type] = add;
    }
}
