using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Yerleştirilen ama henüz inşa edilmemiş "Ghost" bina durumunu temsil eder.
/// NPC'ler buraya gerekli kaynakları taşıdıktan sonra gerçek bina prefabını oluşturur.
/// </summary>
public class GhostBuilding : MonoBehaviour
{
    public BuildingDefinition definition;
    public Vector3 targetPosition;
    public Quaternion targetRotation;
    public Transform buildingParent;
    public GridManager gridManager;
    public Tilemap tilemap;

    // Gerekli kaynaklar listesi
    public List<BuildingDefinition.ResourceCost> requiredResources = new List<BuildingDefinition.ResourceCost>();
    // Yoldaki/Atanmış kaynaklar listesi (çift atamayı önlemek için)
    public List<BuildingDefinition.ResourceCost> assignedResources = new List<BuildingDefinition.ResourceCost>();
    // Teslim edilmiş kaynaklar listesi
    public List<BuildingDefinition.ResourceCost> deliveredResources = new List<BuildingDefinition.ResourceCost>();

    private bool _isConstructed = false;

    public bool IsConstructed => _isConstructed;

    public void Initialize(BuildingDefinition def, Vector3 pos, Quaternion rot, Transform parent, GridManager grid, Tilemap tm)
    {
        definition = def;
        targetPosition = pos;
        targetRotation = rot;
        buildingParent = parent;
        gridManager = grid;
        tilemap = tm;

        // Tanımdaki maliyetleri kopyala
        foreach (var cost in def.buildCosts)
        {
            if (cost.amount > 0)
            {
                requiredResources.Add(cost);
                assignedResources.Add(new BuildingDefinition.ResourceCost { type = cost.type, amount = 0 });
                deliveredResources.Add(new BuildingDefinition.ResourceCost { type = cost.type, amount = 0 });
            }
        }

        // Mavi/yarı saydam bir inşaat alanı görseli uygula
        SetGhostVisualColor(new Color(0.2f, 0.6f, 1f, 0.6f));
    }

    private void SetGhostVisualColor(Color color)
    {
        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
                sr.color = color;
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }
    }

    public bool NeedsResource(ResourceType type, out int amountNeeded)
    {
        amountNeeded = 0;
        if (_isConstructed) return false;

        int req = 0;
        foreach (var r in requiredResources)
        {
            if (r.type == type) req += r.amount;
        }

        int ass = 0;
        foreach (var a in assignedResources)
        {
            if (a.type == type) ass += a.amount;
        }

        amountNeeded = req - ass;
        return amountNeeded > 0;
    }

    public void AssignResource(ResourceType type, int amount)
    {
        for (int i = 0; i < assignedResources.Count; i++)
        {
            if (assignedResources[i].type == type)
            {
                var val = assignedResources[i];
                val.amount += amount;
                assignedResources[i] = val;
                break;
            }
        }
    }

    public void DeliverResource(ResourceType type, int amount)
    {
        if (_isConstructed) return;

        for (int i = 0; i < deliveredResources.Count; i++)
        {
            if (deliveredResources[i].type == type)
            {
                var val = deliveredResources[i];
                val.amount += amount;
                deliveredResources[i] = val;
                break;
            }
        }

        CheckConstructionProgress();
    }

    private void CheckConstructionProgress()
    {
        bool complete = true;
        foreach (var req in requiredResources)
        {
            int del = 0;
            foreach (var d in deliveredResources)
            {
                if (d.type == req.type) del += d.amount;
            }
            if (del < req.amount)
            {
                complete = false;
                break;
            }
        }

        if (complete)
        {
            CompleteConstruction();
        }
    }

    public void CompleteConstruction()
    {
        if (_isConstructed) return;
        _isConstructed = true;

        if (ConstructionManager.Instance != null)
        {
            ConstructionManager.Instance.UnregisterGhostBuilding(this);
        }

        // Ghost'un grid üzerindeki işgalini serbest bırak ki yeni bina işgal edebilsin
        GridOccupier2D ghostOcc = GetComponentInChildren<GridOccupier2D>(true);
        if (ghostOcc != null)
        {
            ghostOcc.Release();
        }
        else
        {
            if (gridManager != null && tilemap != null)
            {
                Vector3Int cellPos = tilemap.WorldToCell(targetPosition);
                gridManager.SetOccupied(cellPos, false, null);
            }
        }

        // Gerçek binayı oluştur
        GameObject obj = Instantiate(definition.buildingPrefab, targetPosition, targetRotation, buildingParent);
        
        // Hücreye snaple
        SnapPlacedBuildingToCell(obj, targetPosition);

        // PlacedBuilding bileşenini ayarla
        var pb = obj.GetComponent<PlacedBuilding>();
        if (pb == null) pb = obj.AddComponent<PlacedBuilding>();
        pb.definitionId = definition.GetSaveId();
        pb.energyNeed = Mathf.Max(0, definition.energyNeed);
        pb.energyProducerType = definition.energyProducerType;
        pb.energyProductionBase = Mathf.Max(0f, definition.energyProductionBase);
        pb.powerCollectorCapacity = Mathf.Max(0, definition.powerCollectorCapacity);
        pb.energyRamp01 = 0f;

        // Grid engel işgali
        GridOccupier2D occ = obj.GetComponentInChildren<GridOccupier2D>(true);
        if (occ != null)
        {
            occ.autoOccupy = false;
            occ.Occupy(gridManager);
        }
        else
        {
            if (gridManager != null && tilemap != null)
            {
                Vector3Int cellPos = tilemap.WorldToCell(targetPosition);
                gridManager.SetOccupied(cellPos, true, obj);
            }
        }

        // Tracker kaydı
        if (BuildingPlacementTracker.Instance != null)
        {
            BuildingPlacementTracker.Instance.RegisterPlaced(obj, definition);
        }

        // Ghost objeyi yok et
        Destroy(gameObject);
    }

    private void SnapPlacedBuildingToCell(GameObject obj, Vector3 placePos)
    {
        if (obj == null) return;
        var anchor = obj.GetComponentInChildren<BuildPlacementAnchor>(true);
        if (anchor == null)
        {
            obj.transform.position = placePos;
            return;
        }
        Vector3 delta = placePos - anchor.transform.position;
        obj.transform.position += delta;
    }
}
