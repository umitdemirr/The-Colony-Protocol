using UnityEngine;
using UnityEngine.Tilemaps;

public class ClickToMoveAStar2D : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Tilemap tilemap;

    [Header("Selection")]
    public LayerMask npcMask;

    NpcMoverAStar2D _selected;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (tilemap == null && PathfindingAStar2D.Instance != null) tilemap = PathfindingAStar2D.Instance.tilemap;
    }

    void Update()
    {
        if (cam == null || tilemap == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            var world = GetMouseWorldOnZ0();
            var hit = Physics2D.OverlapPoint(new Vector2(world.x, world.y), npcMask);
            if (hit != null)
            {
                _selected = hit.GetComponentInParent<NpcMoverAStar2D>();
            }
        }

        if (_selected == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            var world = GetMouseWorldOnZ0();
            Vector3Int targetCell = tilemap.WorldToCell(world);
            _selected.tilemap = tilemap;
            _selected.MoveToCell(targetCell);
        }
    }

    Vector3 GetMouseWorldOnZ0()
    {
        Vector3 mp = Input.mousePosition;
        if (!cam.orthographic)
        {
            // Kameradan Z=0 düzlemine projeksiyon
            mp.z = Mathf.Abs(cam.transform.position.z);
        }

        Vector3 world = cam.ScreenToWorldPoint(mp);
        world.z = 0f;
        return world;
    }
}

