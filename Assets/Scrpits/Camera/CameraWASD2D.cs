using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraWASD2D : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Header("Keyboard Move")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 2f;

    [Header("Edge Scroll")]
    public bool edgeScrollEnabled = true;
    public float edgeSizePx = 18f;
    public float edgeScrollMultiplier = 1f;

    [Header("Mouse Drag Pan")]
    public bool dragPanEnabled = true;
    public int dragMouseButton = 2; // 2 = middle
    public float dragPanMultiplier = 1f;

    [Header("Smoothing")]
    public bool smoothMove = true;
    [Range(0.01f, 0.5f)] public float smoothTime = 0.12f;

    [Header("Zoom (Orthographic)")]
    public bool zoomEnabled = true;
    public float zoomSpeed = 8f;
    public float minOrthoSize = 3.5f;
    public float maxOrthoSize = 25f;
    public bool zoomToCursor = true;

    [Header("Harita Sınırları")]
    public bool useBounds = true;
    [Tooltip("Sınırlar bu tilemap'ten alınır. Boşsa GridManager.visualTilemap kullanılır")]
    public Tilemap groundTilemap;
    public GridManager gridManager;
    public Vector2 minBounds = new Vector2(-100f, -100f);
    public Vector2 maxBounds = new Vector2(100f, 100f);

    Vector3 _targetPos;
    Vector3 _moveVel;
    Vector3 _dragLastWorld;
    bool _dragging;

    void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
        _targetPos = transform.position;
    }

    void Start()
    {
        UpdateBoundsFromGrid();
    }

    void Update()
    {
        if (cam == null) return;

        HandleZoom();

        Vector2 dir = GetKeyboardDir() + GetEdgeDir();
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        speed *= (dir == Vector2.zero) ? 1f : 1f;

        _targetPos += new Vector3(dir.x, dir.y, 0f) * speed * Time.deltaTime;

        HandleDragPan();
        _targetPos.z = transform.position.z;

        if (useBounds)
        {
            GetEffectiveBounds(out float xMin, out float xMax, out float yMin, out float yMax);
            _targetPos.x = Mathf.Clamp(_targetPos.x, xMin, xMax);
            _targetPos.y = Mathf.Clamp(_targetPos.y, yMin, yMax);
        }

        if (smoothMove)
        {
            transform.position = Vector3.SmoothDamp(transform.position, _targetPos, ref _moveVel, smoothTime);
        }
        else
        {
            transform.position = _targetPos;
        }
    }

    Vector2 GetKeyboardDir()
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;

        return new Vector2(x, y);
    }

    Vector2 GetEdgeDir()
    {
        if (!edgeScrollEnabled) return Vector2.zero;

        Vector3 m = Input.mousePosition;
        float x = 0f;
        float y = 0f;

        if (m.x <= edgeSizePx) x -= 1f;
        else if (m.x >= Screen.width - edgeSizePx) x += 1f;

        if (m.y <= edgeSizePx) y -= 1f;
        else if (m.y >= Screen.height - edgeSizePx) y += 1f;

        Vector2 dir = new Vector2(x, y);
        if (dir == Vector2.zero) return Vector2.zero;
        return dir * edgeScrollMultiplier;
    }

    void HandleDragPan()
    {
        if (!dragPanEnabled) return;

        if (Input.GetMouseButtonDown(dragMouseButton))
        {
            _dragging = true;
            _dragLastWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            _dragLastWorld.z = 0f;
        }

        if (Input.GetMouseButtonUp(dragMouseButton))
        {
            _dragging = false;
        }

        if (_dragging)
        {
            Vector3 nowWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            nowWorld.z = 0f;
            Vector3 delta = (_dragLastWorld - nowWorld) * dragPanMultiplier;
            _targetPos += new Vector3(delta.x, delta.y, 0f);
            _dragLastWorld = nowWorld;
        }
    }

    void HandleZoom()
    {
        if (!zoomEnabled) return;
        if (!cam.orthographic) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.0001f) return;

        Vector3 beforeWorld = Vector3.zero;
        if (zoomToCursor)
        {
            beforeWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            beforeWorld.z = 0f;
        }

        float size = cam.orthographicSize;
        size -= scroll * zoomSpeed;
        size = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
        cam.orthographicSize = size;

        if (zoomToCursor)
        {
            Vector3 afterWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            afterWorld.z = 0f;
            Vector3 diff = beforeWorld - afterWorld;
            _targetPos += new Vector3(diff.x, diff.y, 0f);
        }
    }

    void UpdateBoundsFromGrid()
    {
        if (!useBounds) return;
        var tm = groundTilemap != null ? groundTilemap : (gridManager != null ? gridManager.visualTilemap : null);
        if (tm != null && GetTilemapWorldBounds(tm, out Vector2 gMin, out Vector2 gMax))
        {
            minBounds = gMin;
            maxBounds = gMax;
        }
    }

    static bool GetTilemapWorldBounds(Tilemap tm, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        if (tm == null) return false;
        var b = tm.cellBounds;
        var cMin = new Vector3Int(b.xMin, b.yMin, 0);
        var cMax = new Vector3Int(b.xMax - 1, b.yMax - 1, 0);
        min = tm.GetCellCenterWorld(cMin);
        max = tm.GetCellCenterWorld(cMax);
        return true;
    }

    void GetEffectiveBounds(out float xMin, out float xMax, out float yMin, out float yMax)
    {
        xMin = minBounds.x;
        xMax = maxBounds.x;
        yMin = minBounds.y;
        yMax = maxBounds.y;
        if (cam != null && cam.orthographic)
        {
            float aspect = (float)Screen.width / Screen.height;
            float halfH = cam.orthographicSize;
            float halfW = halfH * aspect;
            xMin += halfW;
            xMax -= halfW;
            yMin += halfH;
            yMax -= halfH;
            if (xMin > xMax) { float m = (xMin + xMax) * 0.5f; xMin = xMax = m; }
            if (yMin > yMax) { float m = (yMin + yMax) * 0.5f; yMin = yMax = m; }
        }
    }
}

