using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class NpcMoverAStar2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float reachDistance = 0.05f;

    [Header("References")]
    public Tilemap tilemap;

    [Header("Collision (Opsiyonel)")]
    [Tooltip("Collider overlap engellemek için layer mask. Boşsa transform ile hareket.")]
    public LayerMask collisionMask;
    public float colliderRadius = 0.3f;
    [Tooltip("A* grid zaten engelleri çözdüğü için varsayılan kapalı. Açarsan fizik overlap ile ek blok kontrolü yapar.")]
    public bool usePhysicsBlockCheck = false;

    List<Vector3Int> _cellPath;
    int _index;
    bool _moving;

    public bool IsMoving => _moving;

    public Vector2 MovementDirection { get; private set; } = Vector2.down;
    public bool DidReachDestination { get; private set; } = false;

    void Update()
    {
        if (!_moving || _cellPath == null || _index >= _cellPath.Count) return;
        if (tilemap == null) return;

        // Yürüyüş esnasında önümüzdeki hücreye aniden bir engel (Örn. inen roket) geldiyse dinamik çarpışma/blok kontrolü
        var gm = PathfindingAStar2D.Instance?.gridManager;
        if (gm != null)
        {
            var upcomingNode = gm.GetNode(_cellPath[_index]);
            if (upcomingNode != null && upcomingNode.isOccupied)
            {
                Vector3Int currentCell = tilemap.WorldToCell(transform.position);
                Vector3Int finalTarget = _cellPath[_cellPath.Count - 1];
                // Eğer nihai hedefimizin kendisi dolduysa veya alternatif yol bulabiliyorsak rotayı güncelle
                if (currentCell != finalTarget && finalTarget != _cellPath[_index])
                {
                    if (MoveToCell(finalTarget)) return;
                }
                // Alternatif yol yoksa veya doğrudan engele geldiyse dur
                _moving = false;
                DidReachDestination = false;
                SendMessage("OnMovementDestinationReached", SendMessageOptions.DontRequireReceiver);
                return;
            }
        }

        Vector3 targetWorld = tilemap.GetCellCenterWorld(_cellPath[_index]);
        targetWorld.z = transform.position.z;

        Vector3 pos = transform.position;
        Vector2 toTarget = new Vector2(targetWorld.x - pos.x, targetWorld.y - pos.y);
        if (toTarget.sqrMagnitude > 0.0001f)
            MovementDirection = toTarget.normalized;

        Vector3 next = Vector3.MoveTowards(pos, targetWorld, moveSpeed * Time.deltaTime);

        if (usePhysicsBlockCheck && collisionMask != 0)
        {
            var overlap = Physics2D.OverlapCircle(next, colliderRadius, collisionMask);
            if (overlap != null && !overlap.transform.IsChildOf(transform))
                return;
        }

        transform.position = next;

        if ((targetWorld - next).sqrMagnitude <= reachDistance * reachDistance)
        {
            _index++;
            if (_index >= _cellPath.Count)
            {
                _moving = false;
                DidReachDestination = true;
                SendMessage("OnMovementDestinationReached", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    public bool MoveToCell(Vector3Int targetCell)
    {
        if (PathfindingAStar2D.Instance == null)
        {
            Debug.LogWarning($"[NpcMoverAStar2D] PathfindingAStar2D.Instance NULL on '{gameObject.name}'!");
            return false;
        }
        if (tilemap == null) tilemap = PathfindingAStar2D.Instance.tilemap;
        if (tilemap == null)
        {
            Debug.LogWarning($"[NpcMoverAStar2D] Tilemap NULL on '{gameObject.name}'!");
            return false;
        }

        Vector3Int startCell = tilemap.WorldToCell(transform.position);

        if (!PathfindingAStar2D.Instance.TryFindPath(startCell, targetCell, out var path, useDiagonals: false))
        {
            Vector3Int nearestValid = PathfindingAStar2D.Instance.FindNearestWalkableCell(targetCell, startCell, maxRadius: 12);
            if (nearestValid == startCell)
            {
                path = new List<Vector3Int>();
            }
            else if (!PathfindingAStar2D.Instance.TryFindPath(startCell, nearestValid, out path, useDiagonals: false))
            {
                Debug.LogWarning($"[NpcMoverAStar2D] '{gameObject.name}' en yakın noktaya ({nearestValid}) giden yol da bulamadı! Yol engelli.");
                return false;
            }
        }

        _cellPath = path;
        _index = 0;
        _moving = _cellPath.Count > 0;
        DidReachDestination = !_moving; // Yol 0 uzunluğunda ise zaten oradayız!
        if (!_moving) SendMessage("OnMovementDestinationReached", SendMessageOptions.DontRequireReceiver);
        return true;
    }

    public void StopMovement()
    {
        _moving = false;
        _cellPath = null;
        _index = 0;
        DidReachDestination = false;
        MovementDirection = Vector2.zero;
    }
}

