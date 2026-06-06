using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Tilemaps")]
    public Tilemap visualTilemap;
    public Tilemap collisionTilemap;

    public GridNode[,] grid;
    private BoundsInt bounds;
    private readonly System.Collections.Generic.HashSet<Vector3Int> forcedOpenCells = new System.Collections.Generic.HashSet<Vector3Int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (visualTilemap == null) { Debug.LogError("Visual Tilemap NULL!"); return; }
        bounds = visualTilemap.cellBounds;
        int width = bounds.size.x;
        int height = bounds.size.y;
        grid = new GridNode[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int cellPos = new Vector3Int(x + bounds.xMin, y + bounds.yMin, 0);
                GridNode node = new GridNode();
                if (collisionTilemap != null && collisionTilemap.HasTile(cellPos))
                {
                    node.isWalkable = false;
                    node.isOccupied = true;
                }
                grid[x, y] = node;
            }
        }
    }

    public GridNode GetNode(Vector3Int cellPos)
    {
        if (grid == null) return null;
        int x = cellPos.x - bounds.xMin;
        int y = cellPos.y - bounds.yMin;
        if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1)) return null;
        return grid[x, y];
    }

    public bool CanPlaceAt(Vector3Int cellPos)
    {
        if (forcedOpenCells.Contains(cellPos)) return false;
        GridNode node = GetNode(cellPos);
        return node != null && node.isWalkable && !node.isOccupied;
    }

    public void SetOccupied(Vector3Int cellPos, bool occupied, GameObject placedObject = null)
    {
        GridNode node = GetNode(cellPos);
        if (node == null) return;
        if (forcedOpenCells.Contains(cellPos) && occupied) return;
        node.isOccupied = occupied;
        node.placedObject = placedObject;
    }

    public void SetWalkable(Vector3Int cellPos, bool walkable)
    {
        GridNode node = GetNode(cellPos);
        if (node == null) return;
        if (forcedOpenCells.Contains(cellPos) && !walkable) return;
        node.isWalkable = walkable;
        if (!walkable) node.isOccupied = true;
    }

    public void ForceOpenCell(Vector3Int cellPos)
    {
        GridNode node = GetNode(cellPos);
        if (node == null) return;
        forcedOpenCells.Add(cellPos);
        node.isWalkable = true;
        node.isOccupied = false;
        node.placedObject = null;
    }

    public bool IsCellForcedOpen(Vector3Int cellPos)
    {
        return forcedOpenCells.Contains(cellPos);
    }

    public Vector3 GetCellCenterWorld(Vector3Int cellPos)
    {
        if (visualTilemap == null) return Vector3.zero;
        return visualTilemap.GetCellCenterWorld(cellPos);
    }

    public bool GetWorldBounds(out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        if (visualTilemap == null) return false;
        var b = visualTilemap.cellBounds;
        var cMin = new Vector3Int(b.xMin, b.yMin, 0);
        var cMax = new Vector3Int(b.xMax - 1, b.yMax - 1, 0);
        min = GetCellCenterWorld(cMin);
        max = GetCellCenterWorld(cMax);
        return true;
    }

    /// <summary>
    /// Load öncesi yerleştirilmiş objelerden gelen occupancy'yi temizler.
    /// Collision tilemap'ten gelen isWalkable değerleri korunur.
    /// </summary>
    public void ResetOccupancy()
    {
        if (grid == null || collisionTilemap == null) return;
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Vector3Int cellPos = new Vector3Int(x + bounds.xMin, y + bounds.yMin, 0);
                bool fromCollision = collisionTilemap.HasTile(cellPos);
                grid[x, y].isOccupied = fromCollision;
                if (!fromCollision) grid[x, y].placedObject = null;
            }
        }
    }
}