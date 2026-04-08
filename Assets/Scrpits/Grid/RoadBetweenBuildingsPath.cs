using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yalnızca eksen hizalı (aynı merkez X veya aynı merkez Y) bina çiftleri arasında
/// düz yol bulur. Dönüş yasak.
/// </summary>
public static class RoadBetweenBuildingsPath
{
    static readonly Vector3Int[] Cardinal =
        { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };

    // ──────────────────────────────────────────────────────────
    //  Ana yol bulma
    // ──────────────────────────────────────────────────────────

    public static bool TryFindPath(
        HashSet<Vector3Int> footprintA,
        HashSet<Vector3Int> footprintB,
        HashSet<Vector3Int> allBuildingCells,
        out List<Vector3Int> path)
    {
        path = null;
        if (footprintA == null || footprintB == null || allBuildingCells == null) return false;
        if (footprintA.Count == 0 || footprintB.Count == 0) return false;

        Vector2 cA = GetCenter(footprintA);
        Vector2 cB = GetCenter(footprintB);
        int ax = Mathf.RoundToInt(cA.x), ay = Mathf.RoundToInt(cA.y);
        int bx = Mathf.RoundToInt(cB.x), by = Mathf.RoundToInt(cB.y);

        bool sameX = ax == bx;
        bool sameY = ay == by;
        if (!sameX && !sameY) return false; // eksen hizalı değil → yol kurulamaz

        Vector3Int dir = sameX
            ? (by > ay ? Vector3Int.up : Vector3Int.down)
            : (bx > ax ? Vector3Int.right : Vector3Int.left);

        Vector3Int startConn = SelectFacingEdgeCenteredNonCornerBoundaryCell(footprintA, cB);
        Vector3Int endConn   = SelectFacingEdgeCenteredNonCornerBoundaryCell(footprintB, cA);

        // Bağlantı hücrelerinin aynı eksende olduğunu doğrula
        if (sameX && startConn.x != endConn.x) return false;
        if (sameY && startConn.y != endConn.y) return false;

        // Çıkış hücrelerinin bina dışında olduğunu doğrula
        if (footprintA.Contains(startConn + dir)) return false;
        if (footprintB.Contains(endConn   - dir)) return false;

        // Düz yolu inşa et: startConn → endConn
        var full = new List<Vector3Int>();
        if (sameX)
        {
            int x = startConn.x, dy = dir.y;
            for (int y = startConn.y; y != endConn.y + dy; y += dy)
                full.Add(new Vector3Int(x, y, startConn.z));
        }
        else
        {
            int y = startConn.y, dx = dir.x;
            for (int x = startConn.x; x != endConn.x + dx; x += dx)
                full.Add(new Vector3Int(x, y, startConn.z));
        }

        if (!ValidateFullPath(full, footprintA, footprintB, allBuildingCells)) return false;

        path = full;
        return true;
    }

    // ──────────────────────────────────────────────────────────
    //  Doğrulama
    // ──────────────────────────────────────────────────────────

    public static bool ValidateFullPath(
        List<Vector3Int> path,
        HashSet<Vector3Int> footprintA,
        HashSet<Vector3Int> footprintB,
        HashSet<Vector3Int> allBuildingCells)
    {
        int n = path.Count;
        if (n < 2) return false;
        if (!footprintA.Contains(path[0]))     return false;
        if (!footprintB.Contains(path[n - 1])) return false;

        for (int i = 1; i < n - 1; i++)
            if (allBuildingCells.Contains(path[i])) return false;

        for (int i = 0; i < n - 1; i++)
        {
            int md = Mathf.Abs(path[i].x - path[i + 1].x) + Mathf.Abs(path[i].y - path[i + 1].y);
            if (md != 1) return false;
        }
        return true;
    }

    // ──────────────────────────────────────────────────────────
    //  Bağlantı hücresi seçiciler (dışarıdan kullanılabilir)
    // ──────────────────────────────────────────────────────────

    public static Vector3Int SelectFacingEdgeCenteredNonCornerBoundaryCell(
        HashSet<Vector3Int> footprint, Vector2 targetCenter)
    {
        if (footprint == null || footprint.Count == 0) return Vector3Int.zero;

        GetBounds(footprint, out int minX, out int maxX, out int minY, out int maxY);
        Vector2 ownCenter = GetCenter(footprint);
        Vector2 delta = targetCenter - ownCenter;
        bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

        int edgeValue = horizontal
            ? (delta.x >= 0f ? maxX : minX)
            : (delta.y >= 0f ? maxY : minY);

        var edgeCells = new List<Vector3Int>();
        foreach (var c in footprint)
        {
            bool onEdge = horizontal ? c.x == edgeValue : c.y == edgeValue;
            if (!onEdge) continue;
            if (!IsNonCornerBoundaryCell(c, minX, maxX, minY, maxY)) continue;
            if (!HasExitNeighbor(c, footprint)) continue;
            edgeCells.Add(c);
        }
        if (edgeCells.Count == 0) return SelectNonCornerBoundaryCell(footprint, targetCenter);

        edgeCells.Sort((a, b) =>
        {
            int ka = horizontal ? a.y : a.x;
            int kb = horizontal ? b.y : b.x;
            return ka.CompareTo(kb);
        });
        return edgeCells[edgeCells.Count / 2];
    }

    public static Vector3Int SelectCenteredBoundaryCell(HashSet<Vector3Int> footprint, Vector2 targetCenter)
    {
        if (footprint == null || footprint.Count == 0) return Vector3Int.zero;

        Vector2 ownCenter = GetCenter(footprint);
        Vector2 delta = targetCenter - ownCenter;
        bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

        int edgeValue = horizontal
            ? (delta.x >= 0f ? int.MinValue : int.MaxValue)
            : (delta.y >= 0f ? int.MinValue : int.MaxValue);

        foreach (var c in footprint)
        {
            if (horizontal)
                edgeValue = delta.x >= 0f ? Mathf.Max(edgeValue, c.x) : Mathf.Min(edgeValue, c.x);
            else
                edgeValue = delta.y >= 0f ? Mathf.Max(edgeValue, c.y) : Mathf.Min(edgeValue, c.y);
        }

        var edgeCells = new List<Vector3Int>();
        foreach (var c in footprint)
        {
            bool onEdge = horizontal ? c.x == edgeValue : c.y == edgeValue;
            if (onEdge && HasExitNeighbor(c, footprint)) edgeCells.Add(c);
        }
        if (edgeCells.Count == 0) return GetAnyWithExit(footprint);

        edgeCells.Sort((a, b) =>
        {
            int ka = horizontal ? a.y : a.x;
            int kb = horizontal ? b.y : b.x;
            return ka.CompareTo(kb);
        });
        return edgeCells[edgeCells.Count / 2];
    }

    public static Vector3Int SelectNonCornerBoundaryCell(HashSet<Vector3Int> footprint, Vector2 targetCenter)
    {
        if (footprint == null || footprint.Count == 0) return Vector3Int.zero;
        GetBounds(footprint, out int minX, out int maxX, out int minY, out int maxY);

        Vector3Int best = Vector3Int.zero;
        float bestDist = float.MaxValue;
        bool found = false;
        foreach (var c in footprint)
        {
            bool isBoundary = c.x == minX || c.x == maxX || c.y == minY || c.y == maxY;
            if (!isBoundary) continue;
            bool isCorner = (c.x == minX || c.x == maxX) && (c.y == minY || c.y == maxY);
            if (isCorner) continue;
            if (!HasExitNeighbor(c, footprint)) continue;
            float d = Vector2.SqrMagnitude(new Vector2(c.x, c.y) - targetCenter);
            if (!found || d < bestDist) { bestDist = d; best = c; found = true; }
        }
        return found ? best : SelectCenteredBoundaryCell(footprint, targetCenter);
    }

    // ──────────────────────────────────────────────────────────
    //  Yardımcılar
    // ──────────────────────────────────────────────────────────

    static bool HasExitNeighbor(Vector3Int c, HashSet<Vector3Int> footprint)
    {
        foreach (var d in Cardinal) if (!footprint.Contains(c + d)) return true;
        return false;
    }

    static bool IsNonCornerBoundaryCell(Vector3Int c, int minX, int maxX, int minY, int maxY)
    {
        bool isBoundary = c.x == minX || c.x == maxX || c.y == minY || c.y == maxY;
        if (!isBoundary) return false;
        bool isCorner = (c.x == minX || c.x == maxX) && (c.y == minY || c.y == maxY);
        return !isCorner;
    }

    public static void GetBounds(HashSet<Vector3Int> footprint, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = int.MaxValue; maxX = int.MinValue; minY = int.MaxValue; maxY = int.MinValue;
        foreach (var c in footprint)
        {
            if (c.x < minX) minX = c.x; if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y; if (c.y > maxY) maxY = c.y;
        }
    }

    public static Vector2 GetCenter(HashSet<Vector3Int> footprint)
    {
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (var c in footprint) { sum += new Vector2(c.x, c.y); n++; }
        return n > 0 ? (sum / n) : Vector2.zero;
    }

    static Vector3Int GetAnyWithExit(HashSet<Vector3Int> footprint)
    {
        foreach (var c in footprint) if (HasExitNeighbor(c, footprint)) return c;
        foreach (var c in footprint) return c;
        return Vector3Int.zero;
    }
}
