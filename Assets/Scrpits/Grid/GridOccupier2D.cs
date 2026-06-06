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
    Vector3 _lastPos;

    void Start()
    {
        _lastPos = transform.position;
        if (!autoOccupy) return;
        TryOccupy();
    }

    void Update()
    {
        if (!autoOccupy || _grid == null || _grid.grid == null) return;

        // Nesne hareket ediyorsa (Örn. roket yukarıdan aşağı iniyorsa) hücreleri anlık güncelle
        if ((transform.position - _lastPos).sqrMagnitude > 0.0001f)
        {
            Release();
            Occupy(_grid);
            _lastPos = transform.position;
        }
        else
        {
            // Konum sabitse ve önceden hücreler belirlendiyse, o hücrelerin dolu kaldığını koru
            // (Grid sıfırlanmasına veya yolların o hücreleri ezmesine karşı otomatik onarım)
            if (_occupiedCells.Count > 0)
            {
                foreach (var cell in _occupiedCells)
                {
                    // Yol olarak zorla açılmış hücreleri atla – yol walkable kalmalı
                    if (_grid.IsCellForcedOpen(cell)) continue;

                    var node = _grid.GetNode(cell);
                    if (node != null)
                    {
                        // Mutlak engel: Yol veya başka bir sistem zorla açmış olsa bile roketin alanını kesin olarak kapat
                        node.isOccupied = true;
                        node.isWalkable = false;
                        node.placedObject = gameObject;
                    }
                }
            }
            else
            {
                // Hücre listesi henüz boşsa tekrar doldurmayı dene
                Occupy(_grid);
            }
        }
    }

    void OnDestroy()
    {
        Release();
    }

    void TryOccupy()
    {
        _grid = gridManagerOverride != null ? gridManagerOverride : FindObjectOfType<GridManager>();
        if (_grid == null || _grid.grid == null || _grid.visualTilemap == null)
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

            // BoxCollider2D için fizik motorunun bounds'ı henüz Start'ta hesaplamadığı durumlara karşı kesin manuel matris hesabı
            if (col is BoxCollider2D box)
            {
                Vector3 worldCenter = box.transform.TransformPoint(box.offset);
                Vector3 lossy = box.transform.lossyScale;
                Vector2 worldSize = new Vector2(box.size.x * Mathf.Abs(lossy.x), box.size.y * Mathf.Abs(lossy.y));
                Vector2 min2D = new Vector2(worldCenter.x - worldSize.x * 0.5f, worldCenter.y - worldSize.y * 0.5f);
                Vector2 max2D = new Vector2(worldCenter.x + worldSize.x * 0.5f, worldCenter.y + worldSize.y * 0.5f);

                minCell = gm.visualTilemap.WorldToCell(new Vector3(min2D.x, min2D.y, 0f));
                maxCell = gm.visualTilemap.WorldToCell(new Vector3(max2D.x, max2D.y, 0f) - epsilon);
                x0 = Mathf.Min(minCell.x, maxCell.x);
                x1 = Mathf.Max(minCell.x, maxCell.x);
                y0 = Mathf.Min(minCell.y, maxCell.y);
                y1 = Mathf.Max(minCell.y, maxCell.y);
            }

            bool addedAnyForCollider = false;
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    Vector3 center = gm.GetCellCenterWorld(cell);

                    Vector2 p = new Vector2(center.x, center.y);
                    bool isInside = col.OverlapPoint(p);
                    if (!isInside && col is BoxCollider2D)
                    {
                        // Z ekseni uyuşmazlıklarını önlemek için tamamen 2D tabanlı AABB kontrolü
                        Vector2 min2D = b.min;
                        Vector2 max2D = b.max;
                        isInside = (min2D.x <= center.x && center.x <= max2D.x && min2D.y <= center.y && center.y <= max2D.y);
                    }
                    if (isInside)
                    {
                        result.Add(cell);
                        addedAnyForCollider = true;
                    }
                }
            }

            // BoxCollider2D ise ve nokta bazlı testlerden hiçbiri uymadıysa, o aralıktaki tüm hücreleri mutlak olarak ekle
            if (!addedAnyForCollider && col is BoxCollider2D && (x1 - x0 + 1) * (y1 - y0 + 1) > 0)
            {
                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        result.Add(new Vector3Int(x, y, 0));
                    }
                }
            }
        }

        // Eğer hiçbir collider hücre vermediyse (veya root üzerinde collider yoksa), nesnenin merkezini ve çevresini garanti ekle
        if (result.Count == 0)
        {
            // Roketin tabanı (collision katmanı) root nesnesinin yaklaşık 3.6 birim aşağısındadır
            Vector3 targetBasePos = transform.position;
            if (gameObject.name.Contains("Rocket"))
            {
                targetBasePos.y -= 3.592f;
            }

            Vector3Int rootCell = gm.visualTilemap.WorldToCell(targetBasePos);
            result.Add(rootCell);
            if (gameObject.name.Contains("Rocket"))
            {
                result.Add(rootCell + Vector3Int.right);
                result.Add(rootCell + Vector3Int.left);
                result.Add(rootCell + Vector3Int.up);
                result.Add(rootCell + Vector3Int.down);
                result.Add(rootCell + new Vector3Int(1, 1, 0));
                result.Add(rootCell + new Vector3Int(-1, 1, 0));
                result.Add(rootCell + new Vector3Int(1, -1, 0));
                result.Add(rootCell + new Vector3Int(-1, -1, 0));
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
            // Yol olarak zorla açılmış hücreleri atla
            if (gm.IsCellForcedOpen(cell)) continue;

            var node = gm.GetNode(cell);
            if (node != null)
            {
                node.isOccupied = true;
                node.isWalkable = false;
                node.placedObject = gameObject;
            }
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

