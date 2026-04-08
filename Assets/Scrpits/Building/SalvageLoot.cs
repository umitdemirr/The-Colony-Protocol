using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roket veya tanımsız prefablar: sökümde verilecek sabit kaynak miktarları.
/// </summary>
public class SalvageLoot : MonoBehaviour
{
    [SerializeField] BuildingDefinition.ResourceCost[] salvage;

    public void AddTotalsTo(Dictionary<ResourceType, int> totals)
    {
        if (totals == null || salvage == null || salvage.Length == 0)
            return;
        foreach (var c in salvage)
        {
            if (c.amount <= 0) continue;
            if (totals.ContainsKey(c.type))
                totals[c.type] += c.amount;
            else
                totals[c.type] = c.amount;
        }
    }
}
