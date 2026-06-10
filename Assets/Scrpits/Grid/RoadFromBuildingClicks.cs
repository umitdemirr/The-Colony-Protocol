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
    PlacedBuilding _highlightedSelection;

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

        if (Input.GetMouseButtonDown(1))
        {
            ResetSelection();
            return;
        }
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
        if (!WorldClickResolver.TryGetPlacedBuildingAt(world, out var pb)) return;

        if (_state == State.Idle)
        {
            SetHighlightedSelection(pb);
            _selectingBuilding = pb;
            _state = State.Selecting;
            return;
        }

        // ── İkinci bina seçildi ───────────────────────────────
        if (_selectingBuilding == pb) { ResetSelection(); return; }

        var fa  = GetFootprint(_selectingBuilding.gameObject);
        var fb  = GetFootprint(pb.gameObject);
        var all = CollectAllBuildingFootprints();

        // Footprint boşsa bağlantı kurulamaz
        if (fa.Count == 0 || fb.Count == 0)
        {
            Debug.LogWarning("[Road] Footprint boş — GridOccupier2D eksik veya gridManager null.");
            ResetSelection();
            return;
        }

        bool extA = IsExteriorBuilding(_selectingBuilding);
        bool extB = IsExteriorBuilding(pb);

        if (!RoadBetweenBuildingsPath.TryFindPath(fa, fb, all, out var path))
        {
            Debug.LogWarning($"[Road] Düz yol bulunamadı — binalar aynı satır/sütunda hizalı sınır hücresi paylaşmıyor. A='{_selectingBuilding.gameObject.name}' B='{pb.gameObject.name}'");
            ResetSelection();
            return;
        }

        // ── Preview başlat ────────────────────────────────────
        _pendingStartPb    = _selectingBuilding;
        _pendingEndPb      = pb;
        _selectingBuilding = null;
        _state             = State.Previewing;

        // En az bir bina exterior ise boru prefabları kullan
        bool startExt = IsExteriorBuilding(_pendingStartPb);
        bool endExt   = IsExteriorBuilding(_pendingEndPb);
        bool usePipes = startExt || endExt;

        if (roadGenerator != null)
        {
            roadGenerator.SpawnPreview(path, previewColor, usePipes);
            SetPanelActive(true);
        }
    }

    static bool IsExteriorBuilding(PlacedBuilding pb)
    {
        return pb != null && pb.isExterior;
    }

    // ──────────────────────────────────────────────────────────
    //  Onay / İptal
    // ──────────────────────────────────────────────────────────

    public void ConfirmPreview()
    {
        if (_state != State.Previewing) return;

        roadGenerator.CommitPreview();

        var rpath = roadGenerator.LastPath;
        var allFootprints = CollectAllBuildingFootprints();

        bool startIsExterior = IsExteriorBuilding(_pendingStartPb);
        bool endIsExterior   = IsExteriorBuilding(_pendingEndPb);
        bool isPipeConnection = startIsExterior || endIsExterior;

        // Pipe bağlantısında interior binanın iç hücrelerini açma
        // (boru duvarın dışında kalır, interior'a girmez)
        if (isPipeConnection)
            ForceOpenCorridorExcludingInterior(rpath, Mathf.Max(1, corridorWidthCells), allFootprints,
                _pendingStartPb, startIsExterior, _pendingEndPb, endIsExterior);
        else
            ForceOpenCorridor(rpath, Mathf.Max(1, corridorWidthCells), allFootprints);

        Grid wg = roadGenerator.WorldGrid;
        if (wg != null && rpath.Count >= 2)
        {
            // Interior binanın duvarını KIRMA — boru sadece duvarın dışına kadar gelir
            // Exterior binanın duvarını kır (varsa)
            if (!startIsExterior && !isPipeConnection)
            {
                // İki interior bina: her iki taraf da duvar kır
                BuildingRoadWallClearer.ClearWallsForRoadConnection(
                    _pendingStartPb.gameObject, rpath[0], rpath[1], wg, gridManager);
            }
            else if (startIsExterior)
            {
                // Start exterior: duvarını kır
                BuildingRoadWallClearer.ClearWallsForRoadConnection(
                    _pendingStartPb.gameObject, rpath[0], rpath[1], wg, gridManager);
            }
            // else: start interior + pipe bağlantı → duvar kırma

            if (!endIsExterior && !isPipeConnection)
            {
                // İki interior bina: her iki taraf da duvar kır
                BuildingRoadWallClearer.ClearWallsForRoadConnection(
                    _pendingEndPb.gameObject, rpath[rpath.Count - 1], rpath[rpath.Count - 2], wg, gridManager);
            }
            else if (endIsExterior)
            {
                // End exterior: duvarını kır
                BuildingRoadWallClearer.ClearWallsForRoadConnection(
                    _pendingEndPb.gameObject, rpath[rpath.Count - 1], rpath[rpath.Count - 2], wg, gridManager);
            }
            // else: end interior + pipe bağlantı → duvar kırma
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
        SetHighlightedSelection(null);
        _pendingStartPb = null;
        _pendingEndPb   = null;
        _state          = State.Idle;
    }

    void ResetSelection()
    {
        SetHighlightedSelection(null);
        _selectingBuilding = null;
        _state             = State.Idle;
    }

    void SetPanelActive(bool active)
    {
        if (previewPanel != null) previewPanel.SetActive(active);
    }

    // ──────────────────────────────────────────────────────────
    //  Yardımcılar
    // ──────────────────────────────────────────────────────────

    HashSet<Vector3Int> GetFootprint(GameObject buildingRoot)
    {
        var occ = buildingRoot.GetComponentInChildren<GridOccupier2D>(true);
        if (occ != null)
        {
            return occ.ComputeOccupiedCells(gridManager);
        }
        else
        {
            var set = new HashSet<Vector3Int>();
            if (gridManager != null && gridManager.visualTilemap != null)
            {
                Vector3Int cell = gridManager.visualTilemap.WorldToCell(buildingRoot.transform.position);
                set.Add(cell);
            }
            return set;
        }
    }

    HashSet<Vector3Int> CollectAllBuildingFootprints()
    {
        var set = new HashSet<Vector3Int>();
        var placedBuildings = Object.FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
        foreach (var pb in placedBuildings)
        {
            if (pb == null || !pb.IsRealBuilding) continue;
            var occ = pb.GetComponentInChildren<GridOccupier2D>(true);
            if (occ != null)
            {
                foreach (var c in occ.ComputeOccupiedCells(gridManager)) set.Add(c);
            }
            else
            {
                if (gridManager != null && gridManager.visualTilemap != null)
                {
                    Vector3Int cell = gridManager.visualTilemap.WorldToCell(pb.transform.position);
                    set.Add(cell);
                }
            }
        }
        return set;
    }

    void ForceOpenCorridor(IReadOnlyList<Vector3Int> path, int width, HashSet<Vector3Int> allBuildingCells)
    {
        if (path == null || path.Count == 0 || gridManager == null) return;
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
                if (!atEndpoint && allBuildingCells != null && allBuildingCells.Contains(c))
                    continue;
                gridManager.ForceOpenCell(c);
            }
        }
    }

    /// <summary>
    /// Pipe bağlantısı için: interior binanın footprint hücrelerini açmaz.
    /// Boru, interior binanın duvarının hemen dışına kadar gelir.
    /// </summary>
    void ForceOpenCorridorExcludingInterior(
        IReadOnlyList<Vector3Int> path, int width, HashSet<Vector3Int> allBuildingCells,
        PlacedBuilding startPb, bool startIsExterior,
        PlacedBuilding endPb, bool endIsExterior)
    {
        if (path == null || path.Count == 0 || gridManager == null) return;

        // Interior binaların footprint'lerini topla — bu hücreleri açmayacağız
        HashSet<Vector3Int> interiorCells = new HashSet<Vector3Int>();
        if (!startIsExterior && startPb != null)
        {
            foreach (var c in GetFootprint(startPb.gameObject))
                interiorCells.Add(c);
        }
        if (!endIsExterior && endPb != null)
        {
            foreach (var c in GetFootprint(endPb.gameObject))
                interiorCells.Add(c);
        }

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

                // Interior bina hücrelerini atla — duvar bozulmasın
                if (interiorCells.Contains(c)) continue;

                if (!atEndpoint && allBuildingCells != null && allBuildingCells.Contains(c))
                    continue;
                gridManager.ForceOpenCell(c);
            }
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

    void SetHighlightedSelection(PlacedBuilding pb)
    {
        if (_highlightedSelection == pb) return;

        if (_highlightedSelection != null)
        {
            var oldHighlight = _highlightedSelection.GetComponent<BuildingSelectionHighlight>();
            if (oldHighlight != null) oldHighlight.SetHighlighted(false);
        }

        _highlightedSelection = pb;

        if (_highlightedSelection != null)
        {
            var newHighlight = _highlightedSelection.GetComponent<BuildingSelectionHighlight>();
            if (newHighlight == null) newHighlight = _highlightedSelection.gameObject.AddComponent<BuildingSelectionHighlight>();
            newHighlight.SetHighlighted(true);
        }
    }
}
