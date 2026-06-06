using UnityEngine;

public static class WorldClickResolver
{
    public static bool TryGetPlacedBuildingAt(Vector2 worldPos, out PlacedBuilding placedBuilding)
    {
        placedBuilding = null;

        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
        if (hit.collider != null)
        {
            placedBuilding = hit.collider.GetComponentInParent<PlacedBuilding>();
            if (placedBuilding != null && placedBuilding.IsRealBuilding)
                return true;
            placedBuilding = null;
        }

        PlacedBuilding[] all = Object.FindObjectsOfType<PlacedBuilding>();
        float bestArea = float.MaxValue;
        for (int i = 0; i < all.Length; i++)
        {
            PlacedBuilding pb = all[i];
            if (pb == null || !pb.gameObject.activeInHierarchy || !pb.IsRealBuilding) continue;
            if (!TryGetVisualBounds(pb.gameObject, out Bounds bounds)) continue;
            if (!BoundsContains2D(bounds, worldPos)) continue;

            float area = bounds.size.x * bounds.size.y;
            if (area < bestArea)
            {
                bestArea = area;
                placedBuilding = pb;
            }
        }

        return placedBuilding != null;
    }

    public static bool TryGetInfoCardInteractableAt(Vector2 worldPos, out InfoCardInteractable interactable)
    {
        interactable = null;

        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
        if (hit.collider != null)
        {
            interactable = hit.collider.GetComponentInParent<InfoCardInteractable>();
            if (interactable != null)
                return true;
        }

        InfoCardInteractable[] all = Object.FindObjectsOfType<InfoCardInteractable>();
        float bestArea = float.MaxValue;
        for (int i = 0; i < all.Length; i++)
        {
            InfoCardInteractable candidate = all[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
            if (!TryGetVisualBounds(candidate.gameObject, out Bounds bounds)) continue;
            if (!BoundsContains2D(bounds, worldPos)) continue;

            float area = bounds.size.x * bounds.size.y;
            if (area < bestArea)
            {
                bestArea = area;
                interactable = candidate;
            }
        }

        return interactable != null;
    }

    static bool TryGetVisualBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null) return false;

        bool found = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled) continue;

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

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
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

    static bool BoundsContains2D(Bounds bounds, Vector2 worldPos)
    {
        Vector3 p = new Vector3(worldPos.x, worldPos.y, bounds.center.z);
        return bounds.Contains(p);
    }
}
