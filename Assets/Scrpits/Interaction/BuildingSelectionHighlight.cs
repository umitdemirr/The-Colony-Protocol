using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BuildingSelectionHighlight : MonoBehaviour
{
    [SerializeField] Color outlineColor = new Color(1f, 1f, 0.55f, 0.85f);
    [SerializeField] float outlinePadding = 0.06f;
    [SerializeField] float lineWidth = 0.06f;
    [SerializeField] int sortingOrder = 12000;
    [SerializeField] float zOffsetToCamera = -0.45f;

    LineRenderer _line;

    void Awake()
    {
        EnsureLine();
        RefreshBounds();
        SetHighlighted(false);
    }

    void LateUpdate()
    {
        if (_line != null && _line.enabled)
            RefreshBounds();
    }

    public void SetHighlighted(bool highlighted)
    {
        EnsureLine();
        if (_line == null) return;
        if (highlighted) RefreshBounds();
        _line.enabled = highlighted;
    }

    void EnsureLine()
    {
        if (_line != null) return;

        GameObject go = new GameObject("SelectionOutline");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _line = go.AddComponent<LineRenderer>();
        _line.loop = true;
        _line.useWorldSpace = true;
        _line.positionCount = 4;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth;
        _line.material = CreateAlwaysOnTopMaterial();
        _line.startColor = outlineColor;
        _line.endColor = outlineColor;
        _line.sortingLayerName = "Default";
        _line.sortingOrder = sortingOrder;
        _line.numCornerVertices = 4;
        _line.numCapVertices = 4;
        _line.textureMode = LineTextureMode.Stretch;
        _line.widthMultiplier = 1f;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.alignment = LineAlignment.View;
        ApplySortingFromTargetRenderers();
    }

    void RefreshBounds()
    {
        if (_line == null) return;
        if (TryBuildOutlineFromGridFootprint(out Vector3[] gridShapePoints) &&
            gridShapePoints != null && gridShapePoints.Length >= 3)
        {
            _line.positionCount = gridShapePoints.Length;
            _line.loop = true;
            for (int i = 0; i < gridShapePoints.Length; i++)
                _line.SetPosition(i, PushTowardCamera(gridShapePoints[i]));
            return;
        }

        if (TryBuildOutlineFromCollider(out Vector3[] shapePoints) && shapePoints != null && shapePoints.Length >= 3)
        {
            _line.positionCount = shapePoints.Length;
            _line.loop = true;
            for (int i = 0; i < shapePoints.Length; i++)
                _line.SetPosition(i, PushTowardCamera(shapePoints[i]));
            return;
        }

        if (!TryGetVisualBounds(out Bounds bounds))
        {
            _line.enabled = false;
            return;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        min.x -= outlinePadding;
        min.y -= outlinePadding;
        max.x += outlinePadding;
        max.y += outlinePadding;

        _line.positionCount = 4;
        _line.loop = true;
        _line.SetPosition(0, PushTowardCamera(new Vector3(min.x, min.y, bounds.center.z)));
        _line.SetPosition(1, PushTowardCamera(new Vector3(min.x, max.y, bounds.center.z)));
        _line.SetPosition(2, PushTowardCamera(new Vector3(max.x, max.y, bounds.center.z)));
        _line.SetPosition(3, PushTowardCamera(new Vector3(max.x, min.y, bounds.center.z)));
    }

    bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || r == _line) continue;
            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (found) return true;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null) continue;
            if (!found)
            {
                bounds = c.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        return found;
    }

    void ApplySortingFromTargetRenderers()
    {
        if (_line == null) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        int bestOrder = sortingOrder;
        int bestLayer = SortingLayer.NameToID("Default");
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r == _line) continue;

            if (!found)
            {
                bestLayer = r.sortingLayerID;
                bestOrder = r.sortingOrder;
                found = true;
                continue;
            }

            if (r.sortingOrder > bestOrder)
            {
                bestLayer = r.sortingLayerID;
                bestOrder = r.sortingOrder;
            }
        }

        _line.sortingLayerID = found ? bestLayer : bestLayer;
        _line.sortingOrder = (found ? bestOrder : sortingOrder) + 50;
    }

    Vector3 PushTowardCamera(Vector3 p)
    {
        Camera cam = Camera.main;
        if (cam == null) return p;

        // Konturu near-plane'e yakın z'ye al: world içindeki diğer renderer/collider'ların
        // önünde kalır, seçili bina üstünde net görünür.
        Vector3 nearWorld = cam.transform.position + cam.transform.forward * (cam.nearClipPlane + Mathf.Abs(zOffsetToCamera));
        p.z = nearWorld.z;
        return p;
    }

    bool TryBuildOutlineFromCollider(out Vector3[] worldPoints)
    {
        worldPoints = null;
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        float bestArea = 0f;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null) continue;

            if (c is PolygonCollider2D poly)
            {
                for (int p = 0; p < poly.pathCount; p++)
                {
                    Vector2[] local = poly.GetPath(p);
                    if (local == null || local.Length < 3) continue;
                    float area = Mathf.Abs(SignedArea(local));
                    if (area <= bestArea) continue;
                    bestArea = area;
                    worldPoints = ToWorld(poly.transform, local);
                }
            }
            else if (c is CompositeCollider2D comp)
            {
                for (int p = 0; p < comp.pathCount; p++)
                {
                    int count = comp.GetPathPointCount(p);
                    if (count < 3) continue;
                    Vector2[] local = new Vector2[count];
                    comp.GetPath(p, local);
                    float area = Mathf.Abs(SignedArea(local));
                    if (area <= bestArea) continue;
                    bestArea = area;
                    worldPoints = ToWorld(comp.transform, local);
                }
            }
        }

        return worldPoints != null;
    }

    static float SignedArea(Vector2[] pts)
    {
        float a = 0f;
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 p0 = pts[i];
            Vector2 p1 = pts[(i + 1) % pts.Length];
            a += (p0.x * p1.y) - (p1.x * p0.y);
        }
        return a * 0.5f;
    }

    static Vector3[] ToWorld(Transform t, Vector2[] local)
    {
        Vector3[] world = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = t.TransformPoint(new Vector3(local[i].x, local[i].y, 0f));
        return world;
    }

    bool TryBuildOutlineFromGridFootprint(out Vector3[] worldPoints)
    {
        worldPoints = null;
        GridOccupier2D occ = GetComponentInChildren<GridOccupier2D>(true);
        if (occ == null) return false;

        GridManager gm = occ.gridManagerOverride != null ? occ.gridManagerOverride : Object.FindObjectOfType<GridManager>();
        if (gm == null || gm.visualTilemap == null) return false;

        HashSet<Vector3Int> cells = occ.ComputeOccupiedCells(gm);
        if (cells == null || cells.Count == 0) return false;

        // İç kenarları silip sadece dış kontur kenarlarını tut (yönlü kenar seti).
        var edgeOut = new Dictionary<(int x, int y), List<(int x, int y)>>();
        foreach (var c in cells)
        {
            AddOrRemoveDirectedEdge(edgeOut, (c.x, c.y), (c.x + 1, c.y));
            AddOrRemoveDirectedEdge(edgeOut, (c.x + 1, c.y), (c.x + 1, c.y + 1));
            AddOrRemoveDirectedEdge(edgeOut, (c.x + 1, c.y + 1), (c.x, c.y + 1));
            AddOrRemoveDirectedEdge(edgeOut, (c.x, c.y + 1), (c.x, c.y));
        }
        if (CountEdges(edgeOut) == 0) return false;

        // Tek bir dış halka çıkar (en büyük loop).
        var loops = new List<List<(int x, int y)>>();
        while (CountEdges(edgeOut) > 0)
        {
            if (!TryTakeAnyEdge(edgeOut, out var start, out var current))
                break;

            var loop = new List<(int x, int y)> { start, current };
            var prev = start;
            int guard = 0;
            while (current != start && guard++ < 10000)
            {
                if (!edgeOut.TryGetValue(current, out var outs) || outs.Count == 0)
                    break;

                (int x, int y) next = outs[0];
                if (outs.Count > 1 && next == prev)
                {
                    next = outs[1];
                }

                RemoveDirectedEdge(edgeOut, current, next);
                prev = current;
                current = next;
                loop.Add(current);
            }

            if (loop.Count >= 4) loops.Add(loop);
        }
        if (loops.Count == 0) return false;

        List<(int x, int y)> best = loops[0];
        for (int i = 1; i < loops.Count; i++)
            if (Mathf.Abs(SignedArea(loops[i])) > Mathf.Abs(SignedArea(best))) best = loops[i];

        worldPoints = new Vector3[best.Count - 1];
        for (int i = 0; i < best.Count - 1; i++)
        {
            var p = best[i];
            Vector3 w = gm.visualTilemap.CellToWorld(new Vector3Int(p.x, p.y, 0));
            worldPoints[i] = new Vector3(w.x, w.y, 0f);
        }
        return true;
    }

    static void AddOrRemoveDirectedEdge(
        Dictionary<(int x, int y), List<(int x, int y)>> edgeOut,
        (int x, int y) a,
        (int x, int y) b)
    {
        // Ters kenar varsa iç kenar sayılır ve birbirini götürür.
        if (RemoveDirectedEdge(edgeOut, b, a))
        {
            return;
        }
        if (!edgeOut.TryGetValue(a, out var outs))
        {
            outs = new List<(int x, int y)>();
            edgeOut[a] = outs;
        }
        outs.Add(b);
    }

    static bool RemoveDirectedEdge(
        Dictionary<(int x, int y), List<(int x, int y)>> edgeOut,
        (int x, int y) from,
        (int x, int y) to)
    {
        if (!edgeOut.TryGetValue(from, out var outs)) return false;
        int idx = outs.IndexOf(to);
        if (idx < 0) return false;
        outs.RemoveAt(idx);
        if (outs.Count == 0) edgeOut.Remove(from);
        return true;
    }

    static bool TryTakeAnyEdge(
        Dictionary<(int x, int y), List<(int x, int y)>> edgeOut,
        out (int x, int y) from,
        out (int x, int y) to)
    {
        foreach (var kv in edgeOut)
        {
            if (kv.Value == null || kv.Value.Count == 0) continue;
            from = kv.Key;
            to = kv.Value[0];
            RemoveDirectedEdge(edgeOut, from, to);
            return true;
        }
        from = default;
        to = default;
        return false;
    }

    static int CountEdges(Dictionary<(int x, int y), List<(int x, int y)>> edgeOut)
    {
        int c = 0;
        foreach (var kv in edgeOut)
            c += kv.Value != null ? kv.Value.Count : 0;
        return c;
    }

    static float SignedArea(List<(int x, int y)> pts)
    {
        float a = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[i];
            var p1 = pts[i + 1];
            a += (p0.x * p1.y) - (p1.x * p0.y);
        }
        return a * 0.5f;
    }

    static Material CreateAlwaysOnTopMaterial()
    {
        Shader s = Shader.Find("Hidden/Internal-Colored");
        if (s == null) s = Shader.Find("Sprites/Default");
        var m = new Material(s);

        // Ön planda çizim: derinlik testini devre dışı bırak.
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        m.SetInt("_ZWrite", 0);
        m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        return m;
    }
}
