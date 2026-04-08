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
    public bool allowDiagonals = true;

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
            int currentF = GetG(gCost, current) + Heuristic(current, targetCell);

            for (int i = 1; i < openSet.Count; i++)
            {
                Vector3Int c = openSet[i];
                int f = GetG(gCost, c) + Heuristic(c, targetCell);
                int h = Heuristic(c, targetCell);
                int bestH = Heuristic(current, targetCell);
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

    static int Heuristic(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
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
}

