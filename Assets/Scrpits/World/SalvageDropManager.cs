using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Söküm: her kaynak birimi için 1 görsel parça + anında envantere +1.
/// </summary>
public class SalvageDropManager : MonoBehaviour
{
    public static SalvageDropManager Instance { get; private set; }

    [Tooltip("SpriteRenderer; her birim için bir kopya")]
    [SerializeField] SalvageDebrisPiece debrisPrefab;

    [Tooltip("Boşsa bu objenin altına koyar")]
    [SerializeField] Transform dropParent;

    [Tooltip("ResourceType sırasıyla (Metal=0, ...)")]
    [SerializeField] Sprite[] iconByResourceType;

    [Header("Yerleşim (ızgara istif)")]
    [Tooltip("Satır başına hücre sayısı")]
    [SerializeField] int gridColumns = 8;
    [SerializeField] Vector2 stackCellSpacing = new Vector2(0.32f, 0.32f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (dropParent == null)
            dropParent = transform;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SpawnDropsAt(Vector3 center, Dictionary<ResourceType, int> totals)
    {
        if (totals == null || totals.Count == 0)
            return;
        ResourceManager rm = ResourceManager.Instance;
        if (rm == null)
            return;

        int totalPieces = 0;
        foreach (var kv in totals)
        {
            if (kv.Value > 0)
                totalPieces += kv.Value;
        }
        if (totalPieces <= 0)
            return;

        int cols = Mathf.Max(1, gridColumns);
        int rows = (totalPieces + cols - 1) / cols;

        int index = 0;
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (!totals.TryGetValue(type, out int count) || count <= 0)
                continue;
            for (int u = 0; u < count; u++)
            {
                rm.Add(type, 1);
                if (debrisPrefab != null)
                {
                    Vector3 pos = GridPosition(center, index, cols, rows, stackCellSpacing);
                    SalvageDebrisPiece piece = Instantiate(debrisPrefab, pos, Quaternion.identity, dropParent);
                    piece.Setup(GetIcon(type));
                }
                index++;
            }
        }
    }

    static Vector3 GridPosition(Vector3 center, int index, int cols, int rows, Vector2 spacing)
    {
        int col = index % cols;
        int row = index / cols;
        float w = (cols - 1) * spacing.x;
        float h = (rows - 1) * spacing.y;
        Vector3 origin = center + new Vector3(-w * 0.5f, h * 0.5f, 0f);
        return origin + new Vector3(col * spacing.x, -row * spacing.y, 0f);
    }

    Sprite GetIcon(ResourceType t)
    {
        int idx = (int)t;
        if (iconByResourceType == null || idx < 0 || idx >= iconByResourceType.Length)
            return null;
        return iconByResourceType[idx];
    }
}
