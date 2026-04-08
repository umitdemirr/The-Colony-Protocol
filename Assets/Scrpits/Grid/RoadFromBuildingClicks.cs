using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// İki binaya tıklandığında yol önizlemesi gösterir; oyuncu onaylayınca yolu inşa eder.
/// Onay: Enter veya Confirm butonu. İptal: Escape / sağ tık / Cancel butonu.
/// </summary>
public class RoadFromBuildingClicks : MonoBehaviour
{
    [SerializeField] private ModularLShapeRoadGenerator roadGenerator;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Camera cam;
    [SerializeField] private MouseCheck mouseCheck;
    [Min(1)][SerializeField] private int corridorWidthCells = 3;

    [Header("Road Constraints")]
    [Min(1)][SerializeField] private int minCenterManhattanDistance = 3;
    [Min(1)][SerializeField] private int maxCenterManhattanDistance = 200;
    [Min(0)][SerializeField] private int minGapBetweenFootprints    = 1;

    [Header("Preview")]
    [SerializeField] private Color previewColor = new Color(1f, 0.85f, 0f, 0.75f);
    [SerializeField] private GameObject previewPanel;   // onay/iptal butonlarını içeren panel
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    enum State { Idle, Selecting, Previewing }
    State _state = State.Idle;

    PlacedBuilding _selectingBuilding;
    PlacedBuilding _pendingStartPb;
    PlacedBuilding _pendingEndPb;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmPreview);
        if (cancelButton  != null) cancelButton.onClick.AddListener(CancelPreview);
        if (previewPanel  != null) previewPanel.SetActive(false);
    }

    void Update()
    {
        if (roadGenerator == null || gridManager == null || cam == null) return;

        // Build moduna geçince preview'i otomatik iptal et
        if (_state == State.Previewing && mouseCheck != null && mouseCheck.buildMode)
        {
            ExecuteCancel();
            return;
        }

        // ── Preview onay/iptal girişleri ──────────────────────
        if (_state == State.Previewing)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                ConfirmPreview();
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                CancelPreview();
            return; // preview aktifken bina tıklamalarını engelle
        }

        // ── Bina tıklama algılama ─────────────────────────────
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (mouseCheck != null && mouseCheck.buildMode) return;

        if (Input.GetMouseButtonDown(1)) { ResetSelection(); return; }
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(world, Vector2.zero);
        if (hit.collider == null) return;

        var pb = hit.collider.GetComponentInParent<PlacedBuilding>();
        if (pb == null) return;

        if (_state == State.Idle)
        {
            _selectingBuilding = pb;
            _state = State.Selecting;
            return;
        }

        // ── İkinci bina seçildi ───────────────────────────────
        if (_selectingBuilding == pb) { ResetSelection(); return; }

        var fa  = GetFootprint(_selectingBuilding.gameObject);
        var fb  = GetFootprint(pb.gameObject);
        var all = CollectAllBuildingFootprints();

        if (!PassesConstraints(fa, fb)) { ResetSelection(); return; }

        if (!RoadBetweenBuildingsPath.TryFindPath(fa, fb, all, out var path))
        {
            Debug.LogWarning("[RoadFromBuildingClicks] Eksen hizalı düz yol bulunamadı.");
            ResetSelection();
            return;
        }

        // ── Preview başlat ────────────────────────────────────
        _pendingStartPb    = _selectingBuilding;
        _pendingEndPb      = pb;
        _selectingBuilding = null;
        _state             = State.Previewing;

        roadGenerator.SpawnPreview(path, previewColor);
        SetPanelActive(true);
    }

    // ──────────────────────────────────────────────────────────
    //  Onay / İptal
    // ──────────────────────────────────────────────────────────

    public void ConfirmPreview()
    {
        if (_state != State.Previewing) return;

        roadGenerator.CommitPreview();

        var rpath = roadGenerator.LastPath;
        ForceOpenCorridor(rpath, Mathf.Max(1, corridorWidthCells));

        Grid wg = roadGenerator.WorldGrid;
        if (wg != null && rpath.Count >= 2)
        {
            BuildingRoadWallClearer.ClearWallsForRoadConnection(
                _pendingStartPb.gameObject, rpath[0], rpath[1], wg, gridManager);
            BuildingRoadWallClearer.ClearWallsForRoadConnection(
                _pendingEndPb.gameObject, rpath[rpath.Count - 1], rpath[rpath.Count - 2], wg, gridManager);
        }

        FinishPreview();
    }

    public void CancelPreview()
    {
        if (_state != State.Previewing) return;
        ExecuteCancel();
    }

    void ExecuteCancel()
    {
        roadGenerator.CancelPreview();
        FinishPreview();
    }

    void FinishPreview()
    {
        SetPanelActive(false);
        _pendingStartPb = null;
        _pendingEndPb   = null;
        _state          = State.Idle;
    }

    void ResetSelection()
    {
        _selectingBuilding = null;
        _state             = State.Idle;
    }

    void SetPanelActive(bool active)
    {
        if (previewPanel != null) previewPanel.SetActive(active);
    }

    // ──────────────────────────────────────────────────────────
    //  Kısıt kontrolü
    // ──────────────────────────────────────────────────────────

    bool PassesConstraints(HashSet<Vector3Int> fa, HashSet<Vector3Int> fb)
    {
        Vector2 cA = RoadBetweenBuildingsPath.GetCenter(fa);
        Vector2 cB = RoadBetweenBuildingsPath.GetCenter(fb);
        int ax = Mathf.RoundToInt(cA.x), ay = Mathf.RoundToInt(cA.y);
        int bx = Mathf.RoundToInt(cB.x), by = Mathf.RoundToInt(cB.y);

        if (ax != bx && ay != by) return false; // eksen hizalaması zorunlu

        int centerDist = Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
        if (centerDist < minCenterManhattanDistance) return false;
        if (centerDist > maxCenterManhattanDistance) return false;

        if (ComputeManhattanGap(fa, fb) < minGapBetweenFootprints) return false;

        return true;
    }

    // ──────────────────────────────────────────────────────────
    //  Yardımcılar
    // ──────────────────────────────────────────────────────────

    static int ComputeManhattanGap(HashSet<Vector3Int> fa, HashSet<Vector3Int> fb)
    {
        int best = int.MaxValue;
        foreach (var a in fa)
            foreach (var b in fb)
            {
                int d = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) - 1;
                if (d < best) best = d;
            }
        return Mathf.Max(0, best);
    }

    HashSet<Vector3Int> GetFootprint(GameObject buildingRoot)
    {
        var occ = buildingRoot.GetComponentInChildren<GridOccupier2D>(true);
        return occ != null ? occ.ComputeOccupiedCells(gridManager) : new HashSet<Vector3Int>();
    }

    HashSet<Vector3Int> CollectAllBuildingFootprints()
    {
        var set = new HashSet<Vector3Int>();
        Transform parent = BuildingPlacementTracker.Instance?.buildingParent;

        if (parent != null)
        {
            foreach (Transform t in parent)
            {
                var occ = t.GetComponentInChildren<GridOccupier2D>(true);
                if (occ == null) continue;
                foreach (var c in occ.ComputeOccupiedCells(gridManager)) set.Add(c);
            }
        }
        else
        {
            foreach (var pb in FindObjectsOfType<PlacedBuilding>())
            {
                var occ = pb.GetComponentInChildren<GridOccupier2D>(true);
                if (occ == null) continue;
                foreach (var c in occ.ComputeOccupiedCells(gridManager)) set.Add(c);
            }
        }
        return set;
    }

    void ForceOpenCorridor(IReadOnlyList<Vector3Int> path, int width)
    {
        if (path == null || path.Count == 0 || gridManager == null) return;
        int half = width / 2;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3Int dir  = GetPathDirection(path, i);
            Vector3Int perp = new Vector3Int(-dir.y, dir.x, 0);
            for (int k = -half; k <= half; k++)
                gridManager.ForceOpenCell(path[i] + perp * k);
        }
    }

    Vector3Int GetPathDirection(IReadOnlyList<Vector3Int> path, int i)
    {
        if (path.Count <= 1) return Vector3Int.right;
        if (i == 0)              return Normalize(path[1] - path[0]);
        if (i == path.Count - 1) return Normalize(path[i] - path[i - 1]);
        return Normalize(path[i + 1] - path[i]);
    }

    static Vector3Int Normalize(Vector3Int d) => new Vector3Int(
        d.x == 0 ? 0 : (d.x > 0 ? 1 : -1),
        d.y == 0 ? 0 : (d.y > 0 ? 1 : -1), 0);
}
