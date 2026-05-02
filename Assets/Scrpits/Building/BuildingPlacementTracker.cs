using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Yerleştirilmiş binaları kaydeder; save/load ile geri yükler.
/// </summary>
public class BuildingPlacementTracker : MonoBehaviour
{
    public static BuildingPlacementTracker Instance { get; private set; }

    public GridManager gridManager;
    public Transform buildingParent;
    public Tilemap tilemap;

    [Tooltip("SaveId ile eşleşen BuildingDefinition asset'leri")]
    public BuildingDefinition[] definitionRegistry;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterPlaced(GameObject obj, BuildingDefinition def)
    {
        var pb = obj.GetComponent<PlacedBuilding>();
        if (pb == null) pb = obj.AddComponent<PlacedBuilding>();
        pb.definitionId = def.GetSaveId();
        pb.energyNeed = Mathf.Max(0, def.energyNeed);
        pb.energyProducerType = def.energyProducerType;
        pb.energyProductionBase = Mathf.Max(0f, def.energyProductionBase);
        pb.powerCollectorCapacity = Mathf.Max(0, def.powerCollectorCapacity);
        pb.energyRamp01 = 0f;
    }

    public List<PlacedBuildingData> CollectSaveData()
    {
        var list = new List<PlacedBuildingData>();
        if (buildingParent == null) return list;

        foreach (Transform t in buildingParent)
        {
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null) continue;

            var d = new PlacedBuildingData
            {
                definitionId = pb.definitionId,
                energyNeed = Mathf.Max(0, pb.energyNeed),
                energyProducerType = (int)pb.energyProducerType,
                energyProductionBase = Mathf.Max(0f, pb.energyProductionBase),
                powerCollectorCapacity = Mathf.Max(0, pb.powerCollectorCapacity),
                posX = t.position.x,
                posY = t.position.y,
                posZ = t.position.z,
                rotZ = t.eulerAngles.z,

                maxHealth = Mathf.Max(1, pb.maxHealth),
                currentHealth = Mathf.Clamp(pb.currentHealth, 0, Mathf.Max(1, pb.maxHealth)),

                isOxygenProducer = pb.isOxygenProducer,
                oxygenAmount = Mathf.Max(0f, pb.oxygenAmount),
                oxygenCapacity = Mathf.Max(0f, pb.oxygenCapacity),
                oxygenProductionCurrent = Mathf.Max(0f, pb.oxygenProductionCurrent),
                oxygenProductionCapacity = Mathf.Max(0f, pb.oxygenProductionCapacity)
            };
            list.Add(d);
        }
        return list;
    }

    public void LoadFromSaveData(List<PlacedBuildingData> list)
    {
        if (list == null || buildingParent == null || gridManager == null) return;

        gridManager.ResetOccupancy();
        foreach (Transform t in buildingParent)
            Destroy(t.gameObject);

        foreach (var d in list)
        {
            var def = GetDefinition(d.definitionId);
            if (def == null || def.buildingPrefab == null) continue;

            var pos = new Vector3(d.posX, d.posY, d.posZ);
            var rot = Quaternion.Euler(0f, 0f, d.rotZ);
            var obj = Instantiate(def.buildingPrefab, pos, rot, buildingParent);

            var pb = obj.GetComponent<PlacedBuilding>();
            if (pb == null) pb = obj.AddComponent<PlacedBuilding>();
            pb.definitionId = d.definitionId;
            pb.energyNeed = d.energyNeed > 0 ? d.energyNeed : Mathf.Max(0, def.energyNeed);
            pb.energyProducerType = d.energyProducerType >= 0
                ? (BuildingDefinition.EnergyProducerType)d.energyProducerType
                : def.energyProducerType;
            pb.energyProductionBase = d.energyProductionBase > 0f ? d.energyProductionBase : Mathf.Max(0f, def.energyProductionBase);
            pb.powerCollectorCapacity = d.powerCollectorCapacity > 0 ? d.powerCollectorCapacity : Mathf.Max(0, def.powerCollectorCapacity);
            pb.energyRamp01 = 1f;

            // Panel değerlerini save'den geri al.
            pb.maxHealth = d.maxHealth > 0 ? d.maxHealth : pb.maxHealth;
            pb.currentHealth = Mathf.Clamp(d.currentHealth, 0, Mathf.Max(1, pb.maxHealth));
            pb.isOxygenProducer = d.isOxygenProducer;
            pb.oxygenAmount = Mathf.Max(0f, d.oxygenAmount);
            pb.oxygenCapacity = Mathf.Max(0f, d.oxygenCapacity);
            pb.oxygenProductionCurrent = Mathf.Max(0f, d.oxygenProductionCurrent);
            pb.oxygenProductionCapacity = Mathf.Max(0f, d.oxygenProductionCapacity);

            var occ = obj.GetComponentInChildren<GridOccupier2D>(true);
            if (occ != null)
            {
                occ.autoOccupy = false;
                occ.Occupy(gridManager);
            }
        }
    }

    public BuildingDefinition GetDefinition(string saveId)
    {
        if (definitionRegistry == null) return null;
        foreach (var d in definitionRegistry)
            if (d != null && d.GetSaveId() == saveId) return d;
        return null;
    }
}
