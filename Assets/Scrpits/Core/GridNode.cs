using UnityEngine;

[System.Serializable]
public class GridNode
{
    public bool isWalkable;
    public bool isOccupied;
    public GameObject placedObject;

    public GridNode() { isWalkable = true; isOccupied = false; placedObject = null; }
    public GridNode(bool walkable) { isWalkable = walkable; isOccupied = !walkable; placedObject = null; }
}