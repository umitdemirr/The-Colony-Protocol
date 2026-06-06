using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PathfindingAStar2D : MonoBehaviour
{
    public static PathfindingAStar2D Instance { get; private set; }

    [Header("References")]
    public GridManager gridManager;
    public Tilemap tilemap;

    [Header("Neighbors")]
    public bool allowDiagonals = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool TryFindPath(Vector3Int startCell, Vector3Int targetCell, out List<Vector3Int> cellPath) =>
        TryFindPath(startCell, targetCell, out cellPath, allowDiagonals);

    public bool TryFindPath(Vector3Int startCell, Vector3Int targetCell, out List<Vector3Int> cellPath, bool useDiagonals)
    {
        cellPath = null;
        if (gridManager == null || tilemap == null) return false;

        GridNode startNode = gridManager.GetNode(startCell);
        GridNode targetNode = gridManager.GetNode(targetCell);
        if (startNode == null || targetNode == null) return false;
        // Başlangıç hücresinin isWalkable/isOccupied olmasını sorgulamıyoruz (sıkışan astronotların kaçabilmesi için)
        if (!targetNode.isWalkable || targetNode.isOccupied) return false;

        var openSet = new List<Vector3Int>();
        var openSetHash = new HashSet<Vector3Int>();
        var closedSet = new HashSet<Vector3Int>();

        var gCost = new Dictionary<Vector3Int, int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        gCost[startCell] = 0;
        openSet.Add(startCell);
        openSetHash.Add(startCell);

        while (openSet.Count > 0)
        {
            Vector3Int current = openSet[0];
            int currentF = GetG(gCost, current) + Heuristic(current, targetCell, useDiagonals);

            for (int i = 1; i < openSet.Count; i++)
            {
                Vector3Int c = openSet[i];
                int f = GetG(gCost, c) + Heuristic(c, targetCell, useDiagonals);
                int h = Heuristic(c, targetCell, useDiagonals);
                int bestH = Heuristic(current, targetCell, useDiagonals);
                if (f < currentF || (f == currentF && h < bestH))
                {
                    current = c;
                    currentF = f;
                }
            }

            openSet.Remove(current);
            openSetHash.Remove(current);
            closedSet.Add(current);

            if (current == targetCell)
            {
                cellPath = ReconstructPath(cameFrom, startCell, targetCell);
                return true;
            }

            foreach (var neighbor in GetNeighbors(current, useDiagonals))
            {
                if (closedSet.Contains(neighbor)) continue;

                if (useDiagonals && IsDiagonalStep(current, neighbor) &&
                    !CanTraverseDiagonal(gridManager, current, neighbor))
                    continue;

                GridNode nNode = gridManager.GetNode(neighbor);
                if (nNode == null) continue;
                if (!nNode.isWalkable || nNode.isOccupied) continue;

                int tentativeG = GetG(gCost, current) + MoveCost(current, neighbor);
                if (!gCost.ContainsKey(neighbor) || tentativeG < gCost[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gCost[neighbor] = tentativeG;

                    if (!openSetHash.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                        openSetHash.Add(neighbor);
                    }
                }
            }
        }

        return false;
    }

    IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell, bool diagonals = true)
    {
        yield return new Vector3Int(cell.x + 1, cell.y, 0);
        yield return new Vector3Int(cell.x - 1, cell.y, 0);
        yield return new Vector3Int(cell.x, cell.y + 1, 0);
        yield return new Vector3Int(cell.x, cell.y - 1, 0);

        if (!diagonals) yield break;

        yield return new Vector3Int(cell.x + 1, cell.y + 1, 0);
        yield return new Vector3Int(cell.x + 1, cell.y - 1, 0);
        yield return new Vector3Int(cell.x - 1, cell.y + 1, 0);
        yield return new Vector3Int(cell.x - 1, cell.y - 1, 0);
    }

    static int GetG(Dictionary<Vector3Int, int> gCost, Vector3Int cell)
    {
        return gCost.TryGetValue(cell, out int v) ? v : int.MaxValue / 4;
    }

    static int Heuristic(Vector3Int a, Vector3Int b, bool diagonalsAllowed)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        if (!diagonalsAllowed)
            return 10 * (dx + dy);
        int diag = Mathf.Min(dx, dy);
        int straight = Mathf.Abs(dx - dy);
        return 14 * diag + 10 * straight;
    }

    static int MoveCost(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx == 1 && dy == 1) ? 14 : 10;
    }

    static bool IsDiagonalStep(Vector3Int from, Vector3Int to)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);
        return dx == 1 && dy == 1;
    }

    /// <summary>
    /// Çapraz adım; iki yan komşu hücre de geçilebilir olmalı (tek hücrelik koridorlarda duvar köşesinden sızmaz).
    /// </summary>
    static bool CanTraverseDiagonal(GridManager gm, Vector3Int from, Vector3Int to)
    {
        int sx = to.x > from.x ? 1 : (to.x < from.x ? -1 : 0);
        int sy = to.y > from.y ? 1 : (to.y < from.y ? -1 : 0);
        var sideA = new Vector3Int(from.x + sx, from.y, from.z);
        var sideB = new Vector3Int(from.x, from.y + sy, from.z);
        GridNode na = gm.GetNode(sideA);
        GridNode nb = gm.GetNode(sideB);
        if (na == null || nb == null) return false;
        if (!na.isWalkable || na.isOccupied) return false;
        if (!nb.isWalkable || nb.isOccupied) return false;
        return true;
    }

    static List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int start, Vector3Int target)
    {
        var path = new List<Vector3Int>();
        Vector3Int current = target;
        while (current != start)
        {
            path.Add(current);
            if (!cameFrom.TryGetValue(current, out current))
                break;
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Hedef hücre dolu veya yürünemez ise, spiral arama ile o hedefe en yakın yürünebilir/boş hücreyi bulur.
    /// </summary>
    public Vector3Int FindNearestWalkableCell(Vector3Int targetCell, Vector3Int startCell, int maxRadius = 12)
    {
        if (gridManager == null) return startCell;

        GridNode baseNode = gridManager.GetNode(targetCell);
        if (baseNode != null && baseNode.isWalkable && !baseNode.isOccupied)
        {
            return targetCell;
        }

        Vector3Int bestCell = startCell;
        float bestDist = float.MaxValue;
        bool foundAny = false;

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;

                    var candidate = new Vector3Int(targetCell.x + x, targetCell.y + y, 0);
                    GridNode node = gridManager.GetNode(candidate);
                    if (node != null && node.isWalkable && !node.isOccupied)
                    {
                        float dist = x * x + y * y;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestCell = candidate;
                            foundAny = true;
                        }
                    }
                }
            }

            if (foundAny)
            {
                return bestCell;
            }
        }

        return startCell;
    }
}

