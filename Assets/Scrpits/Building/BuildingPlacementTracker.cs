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
                posX = t.position.x,
                posY = t.position.y,
                posZ = t.position.z,
                rotZ = t.eulerAngles.z
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
