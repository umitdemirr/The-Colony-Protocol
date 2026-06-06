using System.Collections.Generic;
using UnityEngine;

public class ModularLShapeRoadGenerator : MonoBehaviour
{
    public static ModularLShapeRoadGenerator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();
        
        trimCellsFromStart = 1; // Reverted back to 1 to match the original gameplay road system!
    }

    [System.Serializable]
    public class CommittedRoadData
    {
        public List<Vector3Int> pathCells = new List<Vector3Int>();
        public bool usePipes;
    }

    public List<CommittedRoadData> committedRoads = new List<CommittedRoadData>();

    [Header("Grid")]
    [SerializeField] private Grid grid;
    public Grid WorldGrid => grid;
    [SerializeField] private GridManager gridManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject straightPrefab;
    [SerializeField] private GameObject straightHorizontalPrefab;
    [SerializeField] private GameObject straightVerticalPrefab;
    [SerializeField] private GameObject endPrefab;
    [Header("Directional End Prefabs (Optional)")]
    [SerializeField] private GameObject endUpPrefab;
    [SerializeField] private GameObject endDownPrefab;
    [SerializeField] private GameObject endLeftPrefab;
    [SerializeField] private GameObject endRightPrefab;

    [Header("Pipe Prefabs (Exterior Bağlantı)")]
    [Tooltip("Exterior bina bağlantısı için yatay boru sprite'ı")]
    [SerializeField] private GameObject pipeStraightHorizontalPrefab;
    [Tooltip("Exterior bina bağlantısı için dikey boru sprite'ı")]
    [SerializeField] private GameObject pipeStraightVerticalPrefab;
    [Tooltip("Exterior bina bağlantısı için genel düz boru sprite'ı (fallback)")]
    [SerializeField] private GameObject pipeStraightPrefab;
    [Tooltip("Exterior bina bağlantısı için boru bitiş sprite'ı")]
    [SerializeField] private GameObject pipeEndPrefab;
    [Tooltip("Exterior bina bağlantısı için yönlü bitiş sprite'ları (opsiyonel)")]
    [SerializeField] private GameObject pipeEndUpPrefab;
    [SerializeField] private GameObject pipeEndDownPrefab;
    [SerializeField] private GameObject pipeEndLeftPrefab;
    [SerializeField] private GameObject pipeEndRightPrefab;

    [Header("Organization")]
    [SerializeField] private Transform roadsRoot;
    [SerializeField] private string roadsRootName = "Roads";

    [Header("Offsets")]
    [SerializeField] private Vector3 straightLocalOffset           = Vector3.zero;
    [SerializeField] private Vector3 straightHorizontalLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 straightVerticalLocalOffset   = Vector3.zero;
    [SerializeField] private Vector3 endLocalOffset                = Vector3.zero;
    [SerializeField] private Vector3 endUpLocalOffset              = Vector3.zero;
    [SerializeField] private Vector3 endDownLocalOffset            = Vector3.zero;
    [SerializeField] private Vector3 endLeftLocalOffset            = Vector3.zero;
    [SerializeField] private Vector3 endRightLocalOffset           = Vector3.zero;

    [Header("Shape Tuning")]
    [SerializeField] private int trimCellsFromStart = 1;

    // ── Kalıcı yol verisi ──────────────────────────────────────
    public IReadOnlyList<Vector3Int> LastPath => _lastPath;
    public bool LastPathUsedPipes => _lastUsedPipes;
    private readonly List<Vector3Int>                   _lastPath           = new();
    private readonly List<GameObject>                   _spawned            = new();
    private readonly HashSet<Vector3Int>                _cellsWithRoadPiece = new();
    private readonly Dictionary<Vector3Int, GameObject> _pieceByCell        = new();
    private int _roadGroupCounter;
    private bool _lastUsedPipes;

    // ── Preview verisi ─────────────────────────────────────────
    public bool HasPreview => _previewGroup != null;
    private readonly List<Vector3Int>                   _previewOriginalPath = new();
    private readonly List<Vector3Int>                   _previewLastPath    = new();
    private readonly List<GameObject>                   _previewSpawned     = new();
    private readonly HashSet<Vector3Int>                _previewCells       = new();
    private readonly Dictionary<Vector3Int, GameObject> _previewPieceByCell = new();
    private Transform _previewGroup;

    // ──────────────────────────────────────────────────────────
    //  Path yardımcıları
    // ──────────────────────────────────────────────────────────

    public static List<Vector3Int> BuildStraightPath(Vector3Int start, Vector3Int end)
    {
        var path = new List<Vector3Int>();
        int sx = start.x, sy = start.y, ex = end.x, ey = end.y, sz = start.z;
        if (sx == ex && sy == ey) { path.Add(start); return path; }
        if (sx == ex)
        {
            int dy = ey > sy ? 1 : -1;
            for (int y = sy; y != ey + dy; y += dy) path.Add(new Vector3Int(sx, y, sz));
        }
        else
        {
            int dx = ex > sx ? 1 : -1;
            for (int x = sx; x != ex + dx; x += dx) path.Add(new Vector3Int(x, sy, sz));
        }
        return path;
    }

    public static List<Vector3Int> BuildLShapePathHorizontalFirst(Vector3Int s, Vector3Int e)
        => BuildStraightPath(s, e);

    // ──────────────────────────────────────────────────────────
    //  Kalıcı yol ekleme / temizleme
    // ──────────────────────────────────────────────────────────

    public void ClearAllRoads()
    {
        CancelPreview();
        for (int i = 0; i < _spawned.Count; i++) if (_spawned[i] != null) Destroy(_spawned[i]);
        _spawned.Clear();
        _cellsWithRoadPiece.Clear();
        _pieceByCell.Clear();
        committedRoads.Clear();
        _roadGroupCounter = 0;

        Transform parent = ResolveSpawnParent();
        if (parent != null)
        {
            var toDestroy = new List<GameObject>();
            foreach (Transform t in parent)
            {
                if (t != null && t.name.StartsWith("Road_"))
                {
                    toDestroy.Add(t.gameObject);
                }
            }
            foreach (var go in toDestroy)
            {
                if (go != null) go.SetActive(false); // Anında deaktif et!
                Destroy(go);
            }
        }
    }

    public void GenerateRoad(Vector3Int start, Vector3Int end) => AddRoadSegment(BuildStraightPath(start, end));

    public void AddRoadSegment(IReadOnlyList<Vector3Int> pathCells, bool usePipes = false)
    {
        if (grid == null || pathCells == null || pathCells.Count == 0) return;

        committedRoads.Add(new CommittedRoadData {
            pathCells = new List<Vector3Int>(pathCells),
            usePipes = usePipes
        });

        SpawnRoadInternal(pathCells, usePipes);
    }

    private void SpawnRoadInternal(IReadOnlyList<Vector3Int> pathCells, bool usePipes)
    {
        _lastPath.Clear();
        _lastUsedPipes = usePipes;
        for (int i = 0; i < pathCells.Count; i++) _lastPath.Add(pathCells[i]);
        TrimRenderPathDeterministic(_lastPath, trimCellsFromStart);

        Transform group = CreateRoadGroup(ResolveSpawnParent());
        SpawnPieces(_lastPath, group, null, _spawned, _cellsWithRoadPiece, _pieceByCell, usePipes);
    }

    // ──────────────────────────────────────────────────────────
    //  Preview API
    // ──────────────────────────────────────────────────────────

    private bool _previewUsePipes;

    public void SpawnPreview(IReadOnlyList<Vector3Int> pathCells, Color previewColor, bool usePipes = false)
    {
        CancelPreview();
        if (grid == null || pathCells == null || pathCells.Count == 0) return;

        _previewUsePipes = usePipes;

        _previewOriginalPath.Clear();
        _previewOriginalPath.AddRange(pathCells);

        _previewLastPath.Clear();
        for (int i = 0; i < pathCells.Count; i++) _previewLastPath.Add(pathCells[i]);
        TrimRenderPathDeterministic(_previewLastPath, trimCellsFromStart);

        _previewGroup = CreateRoadGroup(ResolveSpawnParent());
        _previewGroup.name += "_Preview";
        SpawnPieces(_previewLastPath, _previewGroup, previewColor, _previewSpawned, _previewCells, _previewPieceByCell, usePipes);
    }

    /// <summary>Preview'i kalıcı yola çevirir. LastPath güncellenir.</summary>
    public void CommitPreview()
    {
        if (!HasPreview) return;

        _lastPath.Clear();
        _lastPath.AddRange(_previewLastPath);
        _lastUsedPipes = _previewUsePipes;

        committedRoads.Add(new CommittedRoadData {
            pathCells = new List<Vector3Int>(_previewOriginalPath),
            usePipes = _previewUsePipes
        });

        foreach (var go in _previewSpawned)
            if (go != null) { ResetColor(go); _spawned.Add(go); }
        foreach (var cell in _previewCells) _cellsWithRoadPiece.Add(cell);
        foreach (var kvp in _previewPieceByCell) _pieceByCell[kvp.Key] = kvp.Value;

        if (_previewGroup != null)
            _previewGroup.name = _previewGroup.name.Replace("_Preview", "");

        _previewSpawned.Clear();
        _previewCells.Clear();
        _previewPieceByCell.Clear();
        _previewLastPath.Clear();
        _previewOriginalPath.Clear();
        _previewGroup = null;
    }

    /// <summary>Preview'i iptal edip nesneleri siler.</summary>
    public void CancelPreview()
    {
        foreach (var go in _previewSpawned) if (go != null) Destroy(go);
        _previewSpawned.Clear();
        _previewCells.Clear();
        _previewPieceByCell.Clear();
        _previewLastPath.Clear();
        _previewOriginalPath.Clear();
        if (_previewGroup != null) { Destroy(_previewGroup.gameObject); _previewGroup = null; }
    }

    // ──────────────────────────────────────────────────────────
    //  Ortak spawn döngüsü
    // ──────────────────────────────────────────────────────────

    private void SpawnPieces(
        List<Vector3Int> path, Transform group, Color? colorOverride,
        List<GameObject> outSpawned, HashSet<Vector3Int> outCells,
        Dictionary<Vector3Int, GameObject> outByCell, bool usePipes = false)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Vector3Int cell = path[i];
            RoadPieceKind kind = ClassifyPiece(path, i, out var inDir, out var outDir);

            Vector3Int facingDir = Vector3Int.zero, straightDir = Vector3Int.zero;
            if (kind == RoadPieceKind.StartCap || kind == RoadPieceKind.EndCap)
                facingDir = GetEndFacingDir(path, kind, inDir, outDir);
            else
                straightDir = outDir != Vector3Int.zero ? outDir : inDir;

            GameObject prefab = kind == RoadPieceKind.Straight
                ? (usePipes ? PickPipeStraightPrefab(straightDir) : PickStraightPrefab(straightDir))
                : (usePipes ? PickPipeEndPrefab(facingDir) : PickEndPrefab(facingDir));
            if (prefab == null) continue;

            if (outCells.Contains(cell))
            {
                if (kind == RoadPieceKind.StartCap || kind == RoadPieceKind.EndCap)
                {
                    if (outByCell.TryGetValue(cell, out var old) && old != null) Destroy(old);
                    outByCell.Remove(cell);
                    outCells.Remove(cell);
                }
                else continue;
            }

            Quaternion rot;
            Vector3 offset;
            if (kind == RoadPieceKind.StartCap || kind == RoadPieceKind.EndCap)
            {
                rot    = Quaternion.Euler(0f, 0f, ResolveEndRotationZ(facingDir));
                offset = endLocalOffset + ResolveEndOffset(facingDir);
            }
            else
            {
                rot    = Quaternion.Euler(0f, 0f, ResolveStraightRotationZ(straightDir));
                offset = straightLocalOffset + ResolveStraightOffset(straightDir);
            }

            Vector3 pos = GetCellCenterWorld(cell) + (rot * offset);
            GameObject instance = Instantiate(prefab, pos, rot, group);
            instance.name = $"{prefab.name}_{cell.x}_{cell.y}";

            if (colorOverride.HasValue) ApplyColor(instance, colorOverride.Value);

            outSpawned.Add(instance);
            outCells.Add(cell);
            outByCell[cell] = instance;
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Renk yardımcıları
    // ──────────────────────────────────────────────────────────

    private static void ApplyColor(GameObject go, Color color)
    {
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            sr.color = color;
        var mpb = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", color);
            r.SetPropertyBlock(mpb);
        }
    }

    private static void ResetColor(GameObject go)
    {
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            sr.color = Color.white;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer) continue;
            r.SetPropertyBlock(null);
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Prefab seçimi
    // ──────────────────────────────────────────────────────────

    private GameObject PickStraightPrefab(Vector3Int dir)
    {
        if (dir.x != 0 && straightHorizontalPrefab != null) return straightHorizontalPrefab;
        if (dir.y != 0 && straightVerticalPrefab   != null) return straightVerticalPrefab;
        return straightPrefab;
    }

    private GameObject PickEndPrefab(Vector3Int dir)
    {
        if (dir.y > 0 && endUpPrefab    != null) return endUpPrefab;
        if (dir.y < 0 && endDownPrefab  != null) return endDownPrefab;
        if (dir.x < 0 && endLeftPrefab  != null) return endLeftPrefab;
        if (dir.x > 0 && endRightPrefab != null) return endRightPrefab;
        return endPrefab;
    }

    private GameObject PickPipeStraightPrefab(Vector3Int dir)
    {
        if (dir.x != 0 && pipeStraightHorizontalPrefab != null) return pipeStraightHorizontalPrefab;
        if (dir.y != 0 && pipeStraightVerticalPrefab   != null) return pipeStraightVerticalPrefab;
        return pipeStraightPrefab != null ? pipeStraightPrefab : PickStraightPrefab(dir);
    }

    private GameObject PickPipeEndPrefab(Vector3Int dir)
    {
        if (dir.y > 0 && pipeEndUpPrefab    != null) return pipeEndUpPrefab;
        if (dir.y < 0 && pipeEndDownPrefab  != null) return pipeEndDownPrefab;
        if (dir.x < 0 && pipeEndLeftPrefab  != null) return pipeEndLeftPrefab;
        if (dir.x > 0 && pipeEndRightPrefab != null) return pipeEndRightPrefab;
        return pipeEndPrefab != null ? pipeEndPrefab : PickEndPrefab(dir);
    }

    // ──────────────────────────────────────────────────────────
    //  Rotasyon & offset
    // ──────────────────────────────────────────────────────────

    private float ResolveStraightRotationZ(Vector3Int dir)
    {
        bool hasDir = (dir.x != 0 && straightHorizontalPrefab != null)
                   || (dir.y != 0 && straightVerticalPrefab   != null);
        return hasDir ? 0f : ZRotationForStraightSegment(dir);
    }

    private Vector3 ResolveStraightOffset(Vector3Int dir)
    {
        if (dir.x != 0) return straightHorizontalLocalOffset;
        if (dir.y != 0) return straightVerticalLocalOffset;
        return Vector3.zero;
    }

    private float ResolveEndRotationZ(Vector3Int dir)
    {
        bool hasDir = (dir.y > 0 && endUpPrefab    != null)
                   || (dir.y < 0 && endDownPrefab  != null)
                   || (dir.x < 0 && endLeftPrefab  != null)
                   || (dir.x > 0 && endRightPrefab != null);
        return hasDir ? 0f : ZRotationForStraightSegment(dir);
    }

    private Vector3 ResolveEndOffset(Vector3Int dir)
    {
        if (dir.x < 0) return endLeftLocalOffset;
        if (dir.x > 0) return endRightLocalOffset;
        if (dir.y > 0) return endUpLocalOffset;
        if (dir.y < 0) return endDownLocalOffset;
        return Vector3.zero;
    }

    // ──────────────────────────────────────────────────────────
    //  Sınıflandırma
    // ──────────────────────────────────────────────────────────

    private enum RoadPieceKind { StartCap, EndCap, Straight }

    private static RoadPieceKind ClassifyPiece(
        IReadOnlyList<Vector3Int> path, int i, out Vector3Int inDir, out Vector3Int outDir)
    {
        inDir = outDir = Vector3Int.zero;
        int n = path.Count;
        if (n == 1) { outDir = Vector3Int.right; return RoadPieceKind.StartCap; }
        if (i == 0)     { outDir = NormalizeStep(path[1] - path[0]); return RoadPieceKind.StartCap; }
        if (i == n - 1) { inDir  = NormalizeStep(path[i] - path[i - 1]); outDir = inDir; return RoadPieceKind.EndCap; }
        inDir  = NormalizeStep(path[i] - path[i - 1]);
        outDir = NormalizeStep(path[i + 1] - path[i]);
        return RoadPieceKind.Straight;
    }

    private static Vector3Int GetEndFacingDir(
        IReadOnlyList<Vector3Int> path, RoadPieceKind kind,
        Vector3Int inDir, Vector3Int outDir)
    {
        int n = path != null ? path.Count : 0;
        if (n <= 1) return outDir != Vector3Int.zero ? outDir : Vector3Int.right;
        if (kind == RoadPieceKind.StartCap) return NormalizeStep(path[1] - path[0]);
        if (kind == RoadPieceKind.EndCap)   return NormalizeStep(path[n - 2] - path[n - 1]);
        return outDir != Vector3Int.zero ? outDir : -inDir;
    }

    private static Vector3Int NormalizeStep(Vector3Int d) => new Vector3Int(
        d.x == 0 ? 0 : (d.x > 0 ? 1 : -1),
        d.y == 0 ? 0 : (d.y > 0 ? 1 : -1),
        d.z == 0 ? 0 : (d.z > 0 ? 1 : -1));

    public static float ZRotationForStraightSegment(Vector3Int dir)
    {
        if (dir.x != 0 && dir.y == 0) return dir.x > 0 ? 0f : 180f;
        if (dir.y != 0 && dir.x == 0) return dir.y > 0 ? 90f : -90f;
        return 0f;
    }

    // ──────────────────────────────────────────────────────────
    //  Dahili yardımcılar
    // ──────────────────────────────────────────────────────────

    private Transform ResolveSpawnParent()
    {
        if (roadsRoot != null) return roadsRoot;
        if (BuildingPlacementTracker.Instance?.buildingParent != null)
            return BuildingPlacementTracker.Instance.buildingParent;
        var mc = FindObjectOfType<MouseCheck>();
        if (mc?.buildingParent != null) return mc.buildingParent;
        var found = GameObject.Find(roadsRootName);
        if (found != null) return found.transform;
        return new GameObject(roadsRootName).transform;
    }

    private Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        Vector3 cellCorner = grid.CellToWorld(cell);
        return cellCorner + grid.transform.TransformVector(0.5f * grid.cellSize);
    }

    private Transform CreateRoadGroup(Transform root)
    {
        _roadGroupCounter++;
        var go = new GameObject($"Road_{_roadGroupCounter:D3}");
        go.transform.SetParent(root, false);
        return go.transform;
    }

    private static void TrimRenderPathDeterministic(List<Vector3Int> path, int trimCount)
    {
        if (path == null || trimCount <= 0 || path.Count < 3) return;
        bool trimStart = ShouldTrimFromStart(path);
        while (trimCount-- > 0 && path.Count > 2)
        {
            if (trimStart) path.RemoveAt(0);
            else path.RemoveAt(path.Count - 1);
        }
    }

    private static bool ShouldTrimFromStart(List<Vector3Int> path)
    {
        Vector3Int a = path[0], b = path[path.Count - 1];
        if (a.y != b.y) return a.y > b.y;
        if (a.x != b.x) return a.x > b.x;
        return a.z >= b.z;
    }

    // ──────────────────────────────────────────────────────────
    //  Kayıttan Yükleme & Koridor Açma / Duvar Kırma
    // ──────────────────────────────────────────────────────────

    public void LoadFromSaveData(List<CommittedRoadData> savedRoads)
    {
        ClearAllRoads();
        if (savedRoads == null) return;

        // Koridor açmak için bina alanlarını topla
        var allFootprints = new HashSet<Vector3Int>();
        var interiorFootprints = new HashSet<Vector3Int>();
        
        if (gridManager != null)
        {
#if UNITY_2023_1_OR_NEWER
            var placedBuildings = FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
#else
            var placedBuildings = FindObjectsOfType<PlacedBuilding>();
#endif
            foreach (var pb in placedBuildings)
            {
                if (pb == null || !pb.IsRealBuilding) continue;
                var occ = pb.GetComponentInChildren<GridOccupier2D>(true);
                if (occ != null)
                {
                    bool isExt = IsExteriorBuilding(pb);
                    foreach (var c in occ.ComputeOccupiedCells(gridManager))
                    {
                        allFootprints.Add(c);
                        if (!isExt)
                            interiorFootprints.Add(c);
                    }
                }
            }
        }

        foreach (var r in savedRoads)
        {
            if (r == null || r.pathCells == null || r.pathCells.Count == 0) continue;
            
            committedRoads.Add(new CommittedRoadData {
                pathCells = new List<Vector3Int>(r.pathCells),
                usePipes = r.usePipes
            });

            SpawnRoadInternal(r.pathCells, r.usePipes);
            
            // Grid'de koridoru aç
            ForceOpenCorridorForRoad(r.pathCells, r.usePipes, allFootprints, interiorFootprints);

            // Binaların bağlantı noktalarında duvarları kır
            if (gridManager != null && grid != null && _lastPath.Count >= 2 && r.pathCells.Count >= 2)
            {
                Vector3Int untrimmedStart = r.pathCells[0];
                Vector3Int untrimmedEnd = r.pathCells[r.pathCells.Count - 1];

                Vector3Int startCell = _lastPath[0];
                Vector3Int startNeighbor = _lastPath[1];
                Vector3Int endCell = _lastPath[_lastPath.Count - 1];
                Vector3Int endNeighbor = _lastPath[_lastPath.Count - 2];

#if UNITY_2023_1_OR_NEWER
                var placedBuildings = FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
#else
                var placedBuildings = FindObjectsOfType<PlacedBuilding>();
#endif
                foreach (var pb in placedBuildings)
                {
                    if (pb == null || !pb.IsRealBuilding) continue;
                    var occ = pb.GetComponentInChildren<GridOccupier2D>(true);
                    if (occ != null)
                    {
                        var footprint = occ.ComputeOccupiedCells(gridManager);
                        if (footprint.Contains(untrimmedStart))
                        {
                            ClearWallsForRoadEndpoint(pb, startCell, startNeighbor, r.usePipes);
                        }
                        if (footprint.Contains(untrimmedEnd))
                        {
                            ClearWallsForRoadEndpoint(pb, endCell, endNeighbor, r.usePipes);
                        }
                    }
                }
            }
        }
    }

    private void ForceOpenCorridorForRoad(IReadOnlyList<Vector3Int> path, bool usePipes, HashSet<Vector3Int> allBuildingCells, HashSet<Vector3Int> interiorCells)
    {
        if (path == null || path.Count == 0 || gridManager == null) return;
        
        int width = 3; // Varsayılan koridor genişliği
        int half = width / 2;
        int last = path.Count - 1;
        
        for (int i = 0; i < path.Count; i++)
        {
            Vector3Int dir  = GetPathDirection(path, i);
            Vector3Int perp = new Vector3Int(-dir.y, dir.x, 0);
            bool atEndpoint = i == 0 || i == last;
            for (int k = -half; k <= half; k++)
            {
                Vector3Int c = path[i] + perp * k;

                // Boru bağlantısında interior bina hücrelerini delme (duvarlar korunsun)
                if (usePipes && interiorCells != null && interiorCells.Contains(c))
                    continue;

                if (!atEndpoint && allBuildingCells != null && allBuildingCells.Contains(c))
                    continue;

                gridManager.ForceOpenCell(c);
            }
        }
    }

    private Vector3Int GetPathDirection(IReadOnlyList<Vector3Int> path, int i)
    {
        if (path.Count <= 1) return Vector3Int.right;
        if (i == 0)              return NormalizeStep(path[1] - path[0]);
        if (i == path.Count - 1) return NormalizeStep(path[i] - path[i - 1]);
        return NormalizeStep(path[i + 1] - path[i]);
    }

    private void ClearWallsForRoadEndpoint(PlacedBuilding pb, Vector3Int endpoint, Vector3Int neighbor, bool usePipes)
    {
        if (pb == null || grid == null || gridManager == null) return;
        
        bool isExt = IsExteriorBuilding(pb);
        if (usePipes && !isExt)
        {
            // Boru bağlantısında interior binanın duvarı kırılmaz
            return;
        }

        BuildingRoadWallClearer.ClearWallsForRoadConnection(pb.gameObject, endpoint, neighbor, grid, gridManager);
    }

    private static bool IsExteriorBuilding(PlacedBuilding pb)
    {
        if (pb == null) return false;
        
        string objName = pb.gameObject.name.ToLower();
        string defId = (pb.definitionId ?? "").ToLower();

        if (objName.Contains("exterior") || objName.Contains("boru") || 
            objName.Contains("pipe") || objName.Contains("out")) 
            return true;

        var tracker = BuildingPlacementTracker.Instance;
        if (tracker != null)
        {
            var def = tracker.GetDefinition(pb.definitionId);
            if (def != null && def.isExterior) return true;
        }

        return false;
    }

    private void OnDestroy() => ClearAllRoads();
}
