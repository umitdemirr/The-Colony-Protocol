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
    const int AxisAlignmentToleranceCells = 1;

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

        bool preferVertical = Mathf.Abs(ax - bx) <= AxisAlignmentToleranceCells;
        bool preferHorizontal = Mathf.Abs(ay - by) <= AxisAlignmentToleranceCells;

        if (TryBuildStraightPath(footprintA, footprintB, allBuildingCells, cA, cB, preferVertical, out path))
            return true;
        if (TryBuildStraightPath(footprintA, footprintB, allBuildingCells, cA, cB, false, out path))
            return true;

        return false;
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

        // Tam ortadaki hücreyi seç: bounds merkezine en yakın eksen değerine göre
        float boundsCenter = horizontal
            ? (minY + maxY) * 0.5f
            : (minX + maxX) * 0.5f;

        edgeCells.Sort((a, b) =>
        {
            float da = Mathf.Abs((horizontal ? a.y : a.x) - boundsCenter);
            float db = Mathf.Abs((horizontal ? b.y : b.x) - boundsCenter);
            return da.CompareTo(db);
        });
        return edgeCells[0];
    }

    public static Vector3Int SelectCenteredBoundaryCell(HashSet<Vector3Int> footprint, Vector2 targetCenter)
    {
        if (footprint == null || footprint.Count == 0) return Vector3Int.zero;

        GetBounds(footprint, out int minX, out int maxX, out int minY, out int maxY);
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

        // Bounds merkezine en yakın hücreyi seç
        float boundsCenter = horizontal
            ? (minY + maxY) * 0.5f
            : (minX + maxX) * 0.5f;

        edgeCells.Sort((a, b) =>
        {
            float da = Mathf.Abs((horizontal ? a.y : a.x) - boundsCenter);
            float db = Mathf.Abs((horizontal ? b.y : b.x) - boundsCenter);
            return da.CompareTo(db);
        });
        return edgeCells[0];
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

    static bool TryBuildStraightPath(
        HashSet<Vector3Int> footprintA,
        HashSet<Vector3Int> footprintB,
        HashSet<Vector3Int> allBuildingCells,
        Vector2 centerA,
        Vector2 centerB,
        bool preferVertical,
        out List<Vector3Int> path)
    {
        path = null;

        var startCandidates = GetBoundaryCellsWithExit(footprintA, centerB);
        var endCandidates = GetBoundaryCellsWithExit(footprintB, centerA);
        if (startCandidates.Count == 0 || endCandidates.Count == 0) return false;

        List<Vector3Int> bestPath = null;
        int bestScore = int.MaxValue;

        for (int i = 0; i < startCandidates.Count; i++)
        {
            Vector3Int startConn = startCandidates[i];
            for (int j = 0; j < endCandidates.Count; j++)
            {
                Vector3Int endConn = endCandidates[j];

                bool sameX = startConn.x == endConn.x;
                bool sameY = startConn.y == endConn.y;
                if (!sameX && !sameY) continue;

                bool vertical = sameX;
                if (preferVertical != vertical)
                {
                    int centerDx = Mathf.Abs(Mathf.RoundToInt(centerA.x) - Mathf.RoundToInt(centerB.x));
                    int centerDy = Mathf.Abs(Mathf.RoundToInt(centerA.y) - Mathf.RoundToInt(centerB.y));
                    if (preferVertical && centerDx > AxisAlignmentToleranceCells) continue;
                    if (!preferVertical && centerDy > AxisAlignmentToleranceCells) continue;
                }

                Vector3Int dir = vertical
                    ? (endConn.y > startConn.y ? Vector3Int.up : Vector3Int.down)
                    : (endConn.x > startConn.x ? Vector3Int.right : Vector3Int.left);

                if (footprintA.Contains(startConn + dir)) continue;
                if (footprintB.Contains(endConn - dir)) continue;

                var full = BuildStraightPath(startConn, endConn);
                if (!ValidateFullPath(full, footprintA, footprintB, allBuildingCells)) continue;

                // Skor: yol uzunluğu + merkezden sapma (ortadan geçen yolu tercih et)
                GetBounds(footprintA, out int aMinX, out int aMaxX, out int aMinY, out int aMaxY);
                GetBounds(footprintB, out int bMinX, out int bMaxX, out int bMinY, out int bMaxY);
                float aCenterAxis = vertical ? (aMinY + aMaxY) * 0.5f : (aMinX + aMaxX) * 0.5f;
                float bCenterAxis = vertical ? (bMinY + bMaxY) * 0.5f : (bMinX + bMaxX) * 0.5f;
                float startDeviation = Mathf.Abs((vertical ? startConn.y : startConn.x) - aCenterAxis);
                float endDeviation   = Mathf.Abs((vertical ? endConn.y : endConn.x) - bCenterAxis);

                int score = full.Count + Mathf.RoundToInt((startDeviation + endDeviation) * 100);
                if (preferVertical == vertical) score -= 100000;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPath = full;
                }
            }
        }

        path = bestPath;
        return path != null;
    }

    static List<Vector3Int> GetBoundaryCellsWithExit(HashSet<Vector3Int> footprint, Vector2 targetCenter)
    {
        var cells = new List<Vector3Int>();
        if (footprint == null || footprint.Count == 0) return cells;

        foreach (var c in footprint)
        {
            if (!HasExitNeighbor(c, footprint)) continue;
            cells.Add(c);
        }

        cells.Sort((a, b) =>
        {
            float da = Vector2.SqrMagnitude(new Vector2(a.x, a.y) - targetCenter);
            float db = Vector2.SqrMagnitude(new Vector2(b.x, b.y) - targetCenter);
            return da.CompareTo(db);
        });

        return cells;
    }

    static List<Vector3Int> BuildStraightPath(Vector3Int start, Vector3Int end)
    {
        var path = new List<Vector3Int>();
        if (start == end)
        {
            path.Add(start);
            return path;
        }

        if (start.x == end.x)
        {
            int stepY = end.y > start.y ? 1 : -1;
            for (int y = start.y; y != end.y + stepY; y += stepY)
                path.Add(new Vector3Int(start.x, y, start.z));
            return path;
        }

        if (start.y == end.y)
        {
            int stepX = end.x > start.x ? 1 : -1;
            for (int x = start.x; x != end.x + stepX; x += stepX)
                path.Add(new Vector3Int(x, start.y, start.z));
            return path;
        }

        return path;
    }

    static Vector3Int GetAnyWithExit(HashSet<Vector3Int> footprint)
    {
        foreach (var c in footprint) if (HasExitNeighbor(c, footprint)) return c;
        foreach (var c in footprint) return c;
        return Vector3Int.zero;
    }
}
