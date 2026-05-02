using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MouseCheck : MonoBehaviour
{
    [Header("Build Mode")]
    public bool buildMode = false;
    public GameObject buildPanel; // Canvas içindeki butonların bulunduğu panel
    public KeyCode cancelBuildKey = KeyCode.Escape;

    [Header("Prefabs & References")]
    public GameObject buildingPrefab;
    public GameObject ghostPrefab;
    public ResourceManager resourceManager;
    public Tilemap tilemap;
    public GridManager gridManager;
    public Transform buildingParent;
    public Camera cam;

    public bool allowRotate = true;
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;
    public float ghostAlpha = 0.55f;
    public int rotateStepDegrees = 90;
    public Vector3 placementOffset = Vector3.zero;
    [Header("Road Potential Rule")]
    [Min(0)] public int minGapForConnectionPotential = 1;
    [Min(1)] public int minCenterManhattanDistanceForPotential = 3;
    [Min(1)] public int maxCenterManhattanDistanceForPotential = 200;

    public System.Action<bool> OnBuildModeChanged;

    PanelManager _panelManagerCache;

    BuildingDefinition _currentDef;
    GameObject currentGhost;
    int _currentRotation;
    Transform _ghostRoot;
    Transform _ghostAnchor;
    GridOccupier2D _ghostOccupier;
    bool _cachedPotentialValid;
    Vector3Int _cachedPotentialCell;
    int _cachedPotentialRotation;
    int _cachedPlacedCount;
    bool _cachedHasConnectionPotential;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        SetBuildMode(buildMode);
    }

    public void SetBuilding(BuildingDefinition def)
    {
        if (def == null) return;

        _currentDef = def;
        buildingPrefab = def.buildingPrefab;
        ghostPrefab = def.ghostPrefab;

        // UI'dan bina seçildiğinde build mode kapalı olsa bile yerleştirme akışı başlasın.
        if (!buildMode)
            SetBuildMode(true);

        // Seçim değişince ghost'u yeniden üret.
        if (buildMode)
        {
            if (currentGhost != null) Destroy(currentGhost);
            currentGhost = null;
            _ghostRoot = null;
            _ghostAnchor = null;
            _ghostOccupier = null;
            _currentRotation = 0;
            EnsureGhost();
        }
    }

    void Update()
    {
        if (!buildMode) return;
        if (tilemap == null || gridManager == null || cam == null) return;
        if (currentGhost == null) EnsureGhost();
        if (currentGhost == null) return;

        // Mouse pozisyonu → world
        Vector3 mousePos = Input.mousePosition;
        if (!cam.orthographic) mousePos.z = Mathf.Abs(cam.transform.position.z);
        Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);
        worldPos.z = 0f;

        // Tilemap hücresi ve ghost pozisyonu
        Vector3Int cellPos = tilemap.WorldToCell(worldPos);

        // Bunun, hücrenin gerçek dünyadaki merkezini döndürmesi gerekir.
        Vector3 placePos = tilemap.GetCellCenterWorld(cellPos);

        // 2D katmanı için z sabitleme; tilemap z'nin 0 olduğunu varsayıyoruz.
        placePos.z = 0f;

        currentGhost.transform.rotation = Quaternion.Euler(0f, 0f, _currentRotation);
        SnapGhostToCell(placePos);

        // Grid + kaynak kontrolü
        bool canPlace = false;
        if (_ghostOccupier != null)
        {
            canPlace = IsFootprintPlaceable(_ghostOccupier, cellPos) && buildingPrefab != null;
        }
        else
        {
            canPlace = gridManager.CanPlaceAt(cellPos) && buildingPrefab != null;
        }
        var rm = resourceManager != null ? resourceManager : ResourceManager.Instance;
        var inv = rm?.Inventory;
        bool canAfford = _currentDef != null && (inv != null && _currentDef.CanAfford(inv));
        bool hasConnectionPotential = HasConnectionPotentialCached(cellPos);
        canPlace = canPlace && canAfford && hasConnectionPotential;
        SetGhostColor(canPlace ? Color.green : Color.red);

        if (Input.GetKeyDown(cancelBuildKey))
        {
            SetBuildMode(false);
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            SetBuildMode(false);
            return;
        }

        if (allowRotate)
        {
            if (Input.GetKeyDown(rotateLeftKey)) _currentRotation = WrapRotation(_currentRotation - rotateStepDegrees);
            if (Input.GetKeyDown(rotateRightKey)) _currentRotation = WrapRotation(_currentRotation + rotateStepDegrees);
        }

        // Sol tık ile bina yerleştirme (_currentDef zorunlu; butondan seçim gerekli)
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (canPlace && _currentDef != null && rm != null)
            {
                _currentDef.ConsumeCost(rm.Inventory);

                GameObject obj = Instantiate(buildingPrefab, placePos, Quaternion.Euler(0f, 0f, _currentRotation), buildingParent);
                SnapPlacedBuildingToCell(obj, placePos);

                // Tracker olmasa bile enerji sistemi üretim/tüketim verisini okuyabilsin.
                var pb = obj.GetComponent<PlacedBuilding>();
                if (pb == null) pb = obj.AddComponent<PlacedBuilding>();
                pb.definitionId = _currentDef.GetSaveId();
                pb.energyNeed = Mathf.Max(0, _currentDef.energyNeed);
                pb.energyProducerType = _currentDef.energyProducerType;
                pb.energyProductionBase = Mathf.Max(0f, _currentDef.energyProductionBase);
                pb.powerCollectorCapacity = Mathf.Max(0, _currentDef.powerCollectorCapacity);
                pb.energyRamp01 = 0f;

                // Tek hücre yerine, prefabın collider bounds'ının kapladığı tüm hücreleri engel yap.
                GridOccupier2D occ = obj.GetComponentInChildren<GridOccupier2D>(true);
                if (occ != null)
                {
                    occ.autoOccupy = false; // Start'ta tekrar işleme yapmasın
                    occ.Occupy(gridManager);
                }
                else
                {
                    // Fallback: hiç footprint bileşeni yoksa sadece anchor hücresi.
                    gridManager.SetOccupied(cellPos, true, obj);
                }

                if (BuildingPlacementTracker.Instance != null && _currentDef != null)
                    BuildingPlacementTracker.Instance.RegisterPlaced(obj, _currentDef);
            }
        }
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        // Mouse için yeterli (tıklanan pointer'ın UI üzerinde olup olmadığı).
        return EventSystem.current.IsPointerOverGameObject();
    }

    public void SetBuildMode(bool enabled)
    {
        buildMode = enabled;
        // buildPanel toggling'i burada yapmıyoruz.
        // UI aç/kapa işi BuildModePanelController üzerinden OnBuildModeChanged event'iyle yönetiliyor.

        if (!buildMode)
        {
            if (currentGhost != null) Destroy(currentGhost);
            currentGhost = null;
            InvalidateConnectionPotentialCache();
            OnBuildModeChanged?.Invoke(buildMode);
            return;
        }

        if (_currentDef == null)
        {
            buildingPrefab = null;
            ghostPrefab = null;
            if (currentGhost != null) Destroy(currentGhost);
            currentGhost = null;
        }

        EnsureGhost();
        _currentRotation = 0;

        OnBuildModeChanged?.Invoke(buildMode);
    }

    void CancelCurrentSelection()
    {
        _currentDef = null;
        buildingPrefab = null;
        ghostPrefab = null;
        _currentRotation = 0;
        InvalidateConnectionPotentialCache();

        if (currentGhost != null)
            Destroy(currentGhost);

        currentGhost = null;
        _ghostRoot = null;
        _ghostAnchor = null;
        _ghostOccupier = null;
    }

    void EnsureGhost()
    {
        if (currentGhost != null) return;
        if (ghostPrefab == null || buildingParent == null) return;
        currentGhost = Instantiate(ghostPrefab, buildingParent);
        _ghostRoot = currentGhost.transform;
        _ghostAnchor = FindAnchorTransform(currentGhost);

        _ghostOccupier = currentGhost.GetComponentInChildren<GridOccupier2D>(true);
        if (_ghostOccupier != null) _ghostOccupier.autoOccupy = false; // ghost grid'e yazmasın
    }

    void SetGhostColor(Color color)
    {
        if (currentGhost == null) return;
        color.a = ghostAlpha;

        var spriteRenderers = currentGhost.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null) continue;
            spriteRenderers[i].color = color;
        }

        var tilemaps = currentGhost.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] == null) continue;
            tilemaps[i].color = color;
        }

        var renderers = currentGhost.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);
        }
    }

    int WrapRotation(int deg)
    {
        deg %= 360;
        if (deg < 0) deg += 360;
        return deg;
    }

    Transform FindAnchorTransform(GameObject obj)
    {
        if (obj == null) return null;
        var anchor = obj.GetComponentInChildren<BuildPlacementAnchor>(true);
        return anchor != null ? anchor.transform : null;
    }

    void SnapGhostToCell(Vector3 placePos)
    {
        Vector3 targetWorld = placePos + placementOffset;
        if (_ghostAnchor == null || _ghostRoot == null)
        {
            _ghostRoot.position = targetWorld;
            return;
        }

        // Rotasyon uygulandıktan sonra anchor'ı tekrar hedefe snap'liyoruz.
        Vector3 delta = targetWorld - _ghostAnchor.position;
        _ghostRoot.position += delta;
    }

    void SnapPlacedBuildingToCell(GameObject obj, Vector3 placePos)
    {
        if (obj == null) return;

        Vector3 targetWorld = placePos + placementOffset;
        var anchor = obj.GetComponentInChildren<BuildPlacementAnchor>(true);
        if (anchor == null)
        {
            obj.transform.position = targetWorld;
            return;
        }

        Vector3 delta = targetWorld - anchor.transform.position;
        obj.transform.position += delta;
    }

    bool IsFootprintPlaceable(GridOccupier2D occupier, Vector3Int anchorCell)
    {
        if (occupier == null || gridManager == null) return false;

        var cells = occupier.ComputeOccupiedCells(gridManager);
        if (cells == null || cells.Count == 0) return gridManager.CanPlaceAt(anchorCell);

        foreach (var cell in cells)
        {
            GridNode node = gridManager.GetNode(cell);
            if (node == null) return false;
            if (!node.isWalkable) return false;
            if (node.isOccupied)
                return false;
        }

        return true;
    }

    bool HasConnectionPotentialCached(Vector3Int anchorCell)
    {
        int placedCount = GetPlacedBuildingCount();
        if (_cachedPotentialValid
            && _cachedPotentialCell == anchorCell
            && _cachedPotentialRotation == _currentRotation
            && _cachedPlacedCount == placedCount)
            return _cachedHasConnectionPotential;

        _cachedPotentialCell = anchorCell;
        _cachedPotentialRotation = _currentRotation;
        _cachedPlacedCount = placedCount;
        _cachedHasConnectionPotential = HasAnyRoadConnectionPotential();
        _cachedPotentialValid = true;
        return _cachedHasConnectionPotential;
    }

    void InvalidateConnectionPotentialCache()
    {
        _cachedPotentialValid = false;
    }

    int GetPlacedBuildingCount()
    {
        if (buildingParent == null) return 0;
        int count = 0;
        foreach (Transform t in buildingParent)
        {
            if (t == null) continue;
            if (currentGhost != null && t.gameObject == currentGhost) continue;
            if (t.GetComponentInParent<PlacedBuilding>() != null || t.GetComponent<PlacedBuilding>() != null)
                count++;
        }
        return count;
    }

    bool HasAnyRoadConnectionPotential()
    {
        int placedCount = GetPlacedBuildingCount();
        if (placedCount == 0)
        {
            Debug.Log("[RoadPotential] PASS first-building-free");
            return true; // İlk bina serbest.
        }
        if (_ghostOccupier == null || gridManager == null)
        {
            Debug.LogWarning($"[RoadPotential] FAIL missing-ref ghostOccupier={(_ghostOccupier != null ? 1 : 0)} gridManager={(gridManager != null ? 1 : 0)}");
            return false;
        }

        HashSet<Vector3Int> ghostFootprint = _ghostOccupier.ComputeOccupiedCells(gridManager);
        if (ghostFootprint == null || ghostFootprint.Count == 0)
        {
            Debug.LogWarning("[RoadPotential] FAIL ghost-footprint-empty");
            return false;
        }

        HashSet<Vector3Int> all = CollectAllBuildingFootprints();
        Vector2 ghostCenter = GetCenter(ghostFootprint);
        Debug.Log($"[RoadPotential] CHECK placed={placedCount} ghostCells={ghostFootprint.Count} allCells={all.Count} minGap={minGapForConnectionPotential} centerMin={minCenterManhattanDistanceForPotential} centerMax={maxCenterManhattanDistanceForPotential}");

        foreach (Transform t in buildingParent)
        {
            if (t == null) continue;
            if (currentGhost != null && t.gameObject == currentGhost) continue;
            GridOccupier2D occ = t.GetComponentInChildren<GridOccupier2D>(true);
            if (occ == null)
            {
                Debug.Log($"[RoadPotential] SKIP {t.name} reason=no-occupier");
                continue;
            }

            HashSet<Vector3Int> other = occ.ComputeOccupiedCells(gridManager);
            if (other == null || other.Count == 0)
            {
                Debug.Log($"[RoadPotential] SKIP {t.name} reason=empty-footprint");
                continue;
            }
            int gap = ComputeManhattanGap(ghostFootprint, other);
            if (gap < minGapForConnectionPotential)
            {
                Debug.Log($"[RoadPotential] SKIP {t.name} reason=gap-too-small gap={gap}");
                continue;
            }

            Vector2 otherCenter = GetCenter(other);
            int centerDist = ComputeCenterManhattanDistance(ghostCenter, otherCenter);
            if (centerDist < minCenterManhattanDistanceForPotential || centerDist > maxCenterManhattanDistanceForPotential)
            {
                Debug.Log($"[RoadPotential] SKIP {t.name} reason=center-distance-out-of-range dist={centerDist}");
                continue;
            }
            List<Vector3Int> testPath;
            bool ok = RoadBetweenBuildingsPath.TryFindPath(ghostFootprint, other, all, out testPath);
            if (ok)
            {
                Debug.Log($"[RoadPotential] PASS {t.name} pathLen={(testPath != null ? testPath.Count : 0)}");
                return true;
            }

            Debug.Log($"[RoadPotential] FAIL {t.name} reason=no-valid-path gap={gap} centerDist={centerDist}");
        }

        Debug.Log("[RoadPotential] FAIL no-candidate-passed");
        return false;
    }

    HashSet<Vector3Int> CollectAllBuildingFootprints()
    {
        var set = new HashSet<Vector3Int>();
        if (buildingParent == null) return set;
        foreach (Transform t in buildingParent)
        {
            if (t == null) continue;
            if (currentGhost != null && t.gameObject == currentGhost) continue;
            var occ = t.GetComponentInChildren<GridOccupier2D>(true);
            if (occ == null) continue;
            foreach (var c in occ.ComputeOccupiedCells(gridManager))
                set.Add(c);
        }
        return set;
    }

    static Vector2 GetCenter(HashSet<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (var c in cells)
        {
            sum += new Vector2(c.x, c.y);
            n++;
        }
        return sum / Mathf.Max(1, n);
    }

    static int ComputeManhattanGap(HashSet<Vector3Int> a, HashSet<Vector3Int> b)
    {
        int best = int.MaxValue;
        foreach (var ca in a)
        {
            foreach (var cb in b)
            {
                int d = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y) - 1;
                if (d < best) best = d;
            }
        }
        return Mathf.Max(0, best);
    }

    static int ComputeCenterManhattanDistance(Vector2 a, Vector2 b)
    {
        int ax = Mathf.RoundToInt(a.x);
        int ay = Mathf.RoundToInt(a.y);
        int bx = Mathf.RoundToInt(b.x);
        int by = Mathf.RoundToInt(b.y);
        return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }
}