using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prefab (bina) yerleştirildiğinde collider bounds'ının kapladığı grid hücrelerini engel yapar.
/// Kaldırılınca (OnDestroy) engeli geri alır.
/// </summary>
public class GridOccupier2D : MonoBehaviour
{
    [Header("Behavior")]
    public bool autoOccupy = true;
    public bool includeTriggers = false;

    [Header("Optional Override")]
    public GridManager gridManagerOverride;

    HashSet<Vector3Int> _occupiedCells = new HashSet<Vector3Int>();
    GridManager _grid;

    void Start()
    {
        if (!autoOccupy) return;
        TryOccupy();
    }

    void OnDestroy()
    {
        Release();
    }

    void TryOccupy()
    {
        _grid = gridManagerOverride != null ? gridManagerOverride : FindObjectOfType<GridManager>();
        if (_grid == null || _grid.visualTilemap == null)
        {
            // grid henüz hazır değilse bir sonraki frame'de tekrar dene
            StartCoroutine(WaitThenOccupy());
            return;
        }
        Occupy(_grid);
    }

    System.Collections.IEnumerator WaitThenOccupy()
    {
        while (_grid == null || _grid.grid == null || _grid.visualTilemap == null)
        {
            _grid = gridManagerOverride != null ? gridManagerOverride : FindObjectOfType<GridManager>();
            yield return null;
        }
        Occupy(_grid);
    }

    public HashSet<Vector3Int> ComputeOccupiedCells(GridManager gm)
    {
        var result = new HashSet<Vector3Int>();
        if (gm == null || gm.visualTilemap == null) return result;

        // Bounds tabanlı aday hücreleri buluyoruz; sonra her aday hücre merkezinin
        // gerçekten collider şeklinin içinde olup olmadığını tek nokta OverlapPoint ile doğruluyoruz.
        // Böylece CompositeCollider2D / TilemapCollider2D'nin boşlukları da engel gibi işaretlenmez.
        Collider2D[] colliders = includeTriggers
            ? GetComponentsInChildren<Collider2D>(true)
            : GetComponentsInChildren<Collider2D>();

        foreach (var col in colliders)
        {
            if (col == null) continue;
            if (ShouldIgnoreColliderForOccupancy(col)) continue;

            Bounds b = col.bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;

            // Off-by-one engellemek için max'i çok az küçült.
            Vector3 epsilon = Vector3.one * 0.001f;
            Vector3Int minCell = gm.visualTilemap.WorldToCell(new Vector3(min.x, min.y, 0f));
            Vector3Int maxCell = gm.visualTilemap.WorldToCell(new Vector3(max.x, max.y, 0f) - epsilon);

            int x0 = Mathf.Min(minCell.x, maxCell.x);
            int x1 = Mathf.Max(minCell.x, maxCell.x);
            int y0 = Mathf.Min(minCell.y, maxCell.y);
            int y1 = Mathf.Max(minCell.y, maxCell.y);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    Vector3 center = gm.GetCellCenterWorld(cell);

                    Vector2 p = new Vector2(center.x, center.y);
                    if (col.OverlapPoint(p)) result.Add(cell);
                }
            }
        }

        return result;
    }

    bool ShouldIgnoreColliderForOccupancy(Collider2D col)
    {
        // Harbor/Medic gibi binalarda root BoxCollider2D tıklama/etkileşim içindir.
        // Duvar tile'ları silinse bile bu tek collider yüzünden giriş hücresi kilitli kalmasın.
        if (col is BoxCollider2D && col.gameObject == gameObject)
        {
            var wallTilemaps = GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
            for (int i = 0; i < wallTilemaps.Length; i++)
            {
                var tm = wallTilemaps[i];
                if (tm != null && tm.gameObject.name.Contains("Wall"))
                    return true;
            }
        }
        return false;
    }

    public void Occupy(GridManager gm)
    {
        if (gm == null) return;
        _grid = gm;

        // Eski işgali temizlemeden yeni footprint'i hesapla.
        _occupiedCells = ComputeOccupiedCells(gm);
        foreach (var cell in _occupiedCells)
        {
            if (gm.IsCellForcedOpen(cell)) continue;
            gm.SetOccupied(cell, true, gameObject);
        }
    }

    public void Release()
    {
        if (_grid == null || _grid.grid == null) return;
        foreach (var cell in _occupiedCells)
        {
            _grid.SetOccupied(cell, false, null);
        }
        _occupiedCells.Clear();
    }
}

