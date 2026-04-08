using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Yol ucu (End prefab) bina köşesindeyken, o hücredeki duvar tile'larını siler (Harbor-Wall / Medic-Wall vb.).
/// </summary>
public static class BuildingRoadWallClearer
{
    const string WallNameToken = "Wall";
    const string GroundNameToken = "Ground";
    const int GateHalfWidthCells = 1; // 1 => toplam 3 hücre (sol-orta-sağ)

    /// <param name="connectionCell">Yolun bina üzerindeki uç hücresi (world grid).</param>
    /// <param name="roadNeighborCell">Bu hücreye komşu yol hücresi (aynı grid).</param>
    public static void ClearWallsForRoadConnection(
        GameObject buildingRoot,
        Vector3Int connectionCell,
        Vector3Int roadNeighborCell,
        Grid worldGrid,
        GridManager gridManager = null)
    {
        if (buildingRoot == null || worldGrid == null) return;

        var tilemaps = buildingRoot.GetComponentsInChildren<Tilemap>(true);
        Vector3Int dir = NormalizeCardinal(roadNeighborCell - connectionCell);
        Vector3Int perp = new Vector3Int(-dir.y, dir.x, 0);

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm == null) continue;
            if (!tm.gameObject.name.Contains(WallNameToken)) continue;

            // Girişte 3 hücrelik kapı aç: connection satırı + ilk yol satırı.
            ClearBand(tm, worldGrid, connectionCell, perp, GateHalfWidthCells);
            ClearBand(tm, worldGrid, roadNeighborCell, perp, GateHalfWidthCells);

            // Runtime'da tile silince collider geometriyi hemen yenile.
            var tmCol = tm.GetComponent<TilemapCollider2D>();
            if (tmCol != null)
                tmCol.ProcessTilemapChanges();
        }

        MakeGroundCollidersNonBlocking(buildingRoot);

        // Duvar açıldıktan sonra bina footprint'ini yeniden hesapla.
        if (gridManager != null)
        {
            // Collision tilemap (dünya) tabanlı walkable verisini runtime'da güncelle.
            gridManager.ForceOpenCell(connectionCell);
            gridManager.ForceOpenCell(roadNeighborCell);

            var occ = buildingRoot.GetComponentInChildren<GridOccupier2D>(true);
            if (occ != null)
            {
                occ.Release();
                occ.Occupy(gridManager);
            }
        }
    }

    static void ClearTileAtWorldCell(Tilemap wallMap, Grid worldGrid, Vector3Int worldCell)
    {
        Vector3 center = GetCellCenterWorld(worldGrid, worldCell);
        Vector3Int mapCell = wallMap.WorldToCell(center);
        wallMap.SetTile(mapCell, null);
    }

    static Vector3 GetCellCenterWorld(Grid grid, Vector3Int cell)
    {
        Vector3 corner = grid.CellToWorld(cell);
        return corner + grid.transform.TransformVector(0.5f * grid.cellSize);
    }

    static void ClearBand(Tilemap wallMap, Grid worldGrid, Vector3Int center, Vector3Int perp, int halfWidth)
    {
        for (int i = -halfWidth; i <= halfWidth; i++)
        {
            ClearTileAtWorldCell(wallMap, worldGrid, center + perp * i);
        }
    }

    static Vector3Int NormalizeCardinal(Vector3Int d)
    {
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            return new Vector3Int(d.x > 0 ? 1 : -1, 0, 0);
        if (Mathf.Abs(d.y) > 0)
            return new Vector3Int(0, d.y > 0 ? 1 : -1, 0);
        return Vector3Int.right;
    }

    static void MakeGroundCollidersNonBlocking(GameObject buildingRoot)
    {
        var tilemaps = buildingRoot.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm == null) continue;
            if (!tm.gameObject.name.Contains(GroundNameToken)) continue;

            var tmCol = tm.GetComponent<TilemapCollider2D>();
            if (tmCol != null) tmCol.enabled = false;

            var comp = tm.GetComponent<CompositeCollider2D>();
            if (comp != null) comp.enabled = false;
        }
    }
}
