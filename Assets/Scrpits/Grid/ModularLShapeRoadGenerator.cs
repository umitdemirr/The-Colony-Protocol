using System.Collections.Generic;
using UnityEngine;

public class ModularLShapeRoadGenerator : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Grid grid;
    public Grid WorldGrid => grid;

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
    private readonly List<Vector3Int>                   _lastPath           = new();
    private readonly List<GameObject>                   _spawned            = new();
    private readonly HashSet<Vector3Int>                _cellsWithRoadPiece = new();
    private readonly Dictionary<Vector3Int, GameObject> _pieceByCell        = new();
    private int _roadGroupCounter;

    // ── Preview verisi ─────────────────────────────────────────
    public bool HasPreview => _previewGroup != null;
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
    }

    public void GenerateRoad(Vector3Int start, Vector3Int end) => AddRoadSegment(BuildStraightPath(start, end));

    public void AddRoadSegment(IReadOnlyList<Vector3Int> pathCells)
    {
        if (grid == null || pathCells == null || pathCells.Count == 0) return;
        _lastPath.Clear();
        for (int i = 0; i < pathCells.Count; i++) _lastPath.Add(pathCells[i]);
        TrimRenderPathDeterministic(_lastPath, trimCellsFromStart);

        Transform group = CreateRoadGroup(ResolveSpawnParent());
        SpawnPieces(_lastPath, group, null, _spawned, _cellsWithRoadPiece, _pieceByCell);
    }

    // ──────────────────────────────────────────────────────────
    //  Preview API
    // ──────────────────────────────────────────────────────────

    public void SpawnPreview(IReadOnlyList<Vector3Int> pathCells, Color previewColor)
    {
        CancelPreview();
        if (grid == null || pathCells == null || pathCells.Count == 0) return;

        _previewLastPath.Clear();
        for (int i = 0; i < pathCells.Count; i++) _previewLastPath.Add(pathCells[i]);
        TrimRenderPathDeterministic(_previewLastPath, trimCellsFromStart);

        _previewGroup = CreateRoadGroup(ResolveSpawnParent());
        _previewGroup.name += "_Preview";
        SpawnPieces(_previewLastPath, _previewGroup, previewColor, _previewSpawned, _previewCells, _previewPieceByCell);
    }

    /// <summary>Preview'i kalıcı yola çevirir. LastPath güncellenir.</summary>
    public void CommitPreview()
    {
        if (!HasPreview) return;

        _lastPath.Clear();
        _lastPath.AddRange(_previewLastPath);

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
        if (_previewGroup != null) { Destroy(_previewGroup.gameObject); _previewGroup = null; }
    }

    // ──────────────────────────────────────────────────────────
    //  Ortak spawn döngüsü
    // ──────────────────────────────────────────────────────────

    private void SpawnPieces(
        List<Vector3Int> path, Transform group, Color? colorOverride,
        List<GameObject> outSpawned, HashSet<Vector3Int> outCells,
        Dictionary<Vector3Int, GameObject> outByCell)
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
                ? PickStraightPrefab(straightDir)
                : PickEndPrefab(facingDir);
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

    private void OnDestroy() => ClearAllRoads();
}
