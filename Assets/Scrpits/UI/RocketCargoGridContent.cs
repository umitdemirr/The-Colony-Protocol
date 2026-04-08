using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>
/// Global bilgi kartında roket kargo ızgarası (150 yuva); alan içine sığacak şekilde ölçeklenir.
/// </summary>
public class RocketCargoGridContent : MonoBehaviour
{
    public const int CargoCapacity = 150;
    const string SlotsRootName = "RocketCargoSlots";

    [Header("Info Card")]
    [FormerlySerializedAs("rocketBodyContent")]
    [SerializeField] string bodyContentKey;
    [SerializeField] InfoCardInteractable targetInteractable;

    [Header("Kargo grid")]
    [Tooltip("0 = yükseklik/genişliğe göre en iyi sütun sayısı")]
    [SerializeField] int gridColumns;
    [SerializeField] Vector2 cellSpacing = new Vector2(2f, 2f);
    [Tooltip("0 = sınır yok (alan kadar büyür)")]
    [SerializeField] float maxCellSide;

    [Header("Kaynak ikonları")]
    [SerializeField] Sprite iconMetal;
    [SerializeField] Sprite iconBiyoplastik;
    [SerializeField] Sprite iconSpares;
    [SerializeField] Sprite iconMeal;
    [SerializeField] Sprite iconMedicalSupplies;

    [Header("Boş yuva")]
    [SerializeField] Sprite emptySlotIcon;
    [SerializeField] Color emptySlotTint = new Color(1f, 1f, 1f, 0.35f);

    [SerializeField] ResourceManager resourceManager;
    InfoCardInteractable _interactable;

    static readonly ResourceType[] ResourceOrder =
    {
        ResourceType.Metal,
        ResourceType.Biyoplastik,
        ResourceType.Spares,
        ResourceType.Meal,
        ResourceType.MedicalSupplies
    };

    readonly List<Image> _slotIcons = new List<Image>(CargoCapacity);
    RectTransform _slotsRoot;

    const int PadL = 4, PadR = 4, PadT = 4, PadB = 4;
    static int PadHorizontal => PadL + PadR;
    static int PadVertical => PadT + PadB;

    void Awake()
    {
        if (resourceManager == null)
            resourceManager = ResourceManager.Instance;
        BindBodyKey();
        BuildSlotsIfNeeded();
        RelayoutGrid();
    }

    void Start()
    {
        StartCoroutine(DelayedRelayout());
    }

    void OnRectTransformDimensionsChange()
    {
        if (_slotIcons.Count > 0)
            RelayoutGrid();
    }

    IEnumerator DelayedRelayout()
    {
        RelayoutGrid();
        yield return null;
        RelayoutGrid();
        yield return null;
        RelayoutGrid();
    }

    void OnEnable()
    {
        BindBodyKey();
        RefreshCargoDisplay();
        RelayoutGrid();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            BindBodyKey();
    }

    void Update()
    {
        if (gameObject.activeInHierarchy)
            RefreshCargoDisplay();
    }

    void BindBodyKey()
    {
        if (_interactable == null)
        {
            _interactable = targetInteractable != null
                ? targetInteractable
                : GetComponentInParent<InfoCardInteractable>();
        }
        if (_interactable == null) return;
        _interactable.bodyContentKey = bodyContentKey;
    }

    void BuildSlotsIfNeeded()
    {
        if (_slotIcons.Count > 0)
            return;

        var panelRt = GetComponent<RectTransform>();
        if (panelRt == null)
            return;

        Transform slotsRootTr = panelRt.Find(SlotsRootName);
        if (slotsRootTr == null)
        {
            var rootGo = new GameObject(SlotsRootName, typeof(RectTransform));
            _slotsRoot = rootGo.GetComponent<RectTransform>();
            _slotsRoot.SetParent(panelRt, false);
            _slotsRoot.anchorMin = Vector2.zero;
            _slotsRoot.anchorMax = Vector2.one;
            _slotsRoot.offsetMin = new Vector2(2f, 2f);
            _slotsRoot.offsetMax = new Vector2(-2f, -2f);
        }
        else
            _slotsRoot = slotsRootTr as RectTransform;

        if (_slotsRoot == null)
            return;

        ClearLayoutGroups(_slotsRoot);

        Vector2 cell = Vector2.one * 16f;
        int columns = gridColumns > 0 ? gridColumns : 10;
        float stepX = cell.x + cellSpacing.x;
        float stepY = cell.y + cellSpacing.y;

        for (int i = 0; i < CargoCapacity; i++)
        {
            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            var childRt = slotGo.GetComponent<RectTransform>();
            childRt.SetParent(_slotsRoot, false);
            childRt.anchorMin = new Vector2(0f, 1f);
            childRt.anchorMax = new Vector2(0f, 1f);
            childRt.pivot = new Vector2(0f, 1f);
            childRt.sizeDelta = cell;
            int col = i % columns;
            int row = i / columns;
            childRt.anchoredPosition = new Vector2(
                PadL + col * stepX,
                -PadT - row * stepY);

            var img = slotGo.GetComponent<Image>();
            ApplyEmptySlotVisual(img);
            img.raycastTarget = false;
            img.preserveAspect = true;
            _slotIcons.Add(img);
        }
    }

    static void ClearLayoutGroups(RectTransform rt)
    {
        var gl = rt.GetComponent<GridLayoutGroup>();
        if (gl != null) Destroy(gl);
        var hl = rt.GetComponent<HorizontalLayoutGroup>();
        if (hl != null) Destroy(hl);
        var vl = rt.GetComponent<VerticalLayoutGroup>();
        if (vl != null) Destroy(vl);
    }

    void ComputeFit(float innerW, float innerH, out int columns, out Vector2 cell)
    {
        innerW = Mathf.Max(1f, innerW - PadHorizontal);
        innerH = Mathf.Max(1f, innerH - PadVertical);
        float sx = cellSpacing.x;
        float sy = cellSpacing.y;
        float cap = maxCellSide > 0.01f ? maxCellSide : float.MaxValue;

        if (gridColumns > 0)
        {
            columns = Mathf.Max(1, gridColumns);
            int rows = (CargoCapacity + columns - 1) / columns;
            float cw = columns > 0 ? (innerW - (columns - 1) * sx) / columns : innerW;
            float ch = rows > 0 ? (innerH - (rows - 1) * sy) / rows : innerH;
            float side = Mathf.Max(2f, Mathf.Min(Mathf.Min(cw, ch), cap));
            cell = new Vector2(side, side);
            return;
        }

        int bestCols = 1;
        float bestSide = 0f;
        for (int cols = 1; cols <= CargoCapacity; cols++)
        {
            int rows = (CargoCapacity + cols - 1) / cols;
            float cw = Mathf.Max(0.01f, (innerW - (cols - 1) * sx) / cols);
            float ch = Mathf.Max(0.01f, (innerH - (rows - 1) * sy) / rows);
            float side = Mathf.Min(cw, ch);
            if (side > bestSide)
            {
                bestSide = side;
                bestCols = cols;
            }
        }

        columns = bestCols;
        float finalSide = Mathf.Max(2f, Mathf.Min(bestSide, cap));
        cell = new Vector2(finalSide, finalSide);
    }

    void RelayoutGrid()
    {
        if (_slotsRoot == null || _slotIcons.Count == 0)
            return;

        var panelRt = GetComponent<RectTransform>();
        if (panelRt == null)
            return;
        if (panelRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);

        float insetX = Mathf.Abs(_slotsRoot.offsetMin.x) + Mathf.Abs(_slotsRoot.offsetMax.x);
        float insetY = Mathf.Abs(_slotsRoot.offsetMin.y) + Mathf.Abs(_slotsRoot.offsetMax.y);
        float w = Mathf.Max(1f, panelRt.rect.width - insetX);
        float h = Mathf.Max(1f, panelRt.rect.height - insetY);

        ComputeFit(w, h, out int columns, out Vector2 cell);
        float stepX = cell.x + cellSpacing.x;
        float stepY = cell.y + cellSpacing.y;

        for (int i = 0; i < CargoCapacity; i++)
        {
            RectTransform childRt = _slotIcons[i].rectTransform;
            int col = i % columns;
            int row = i / columns;
            childRt.sizeDelta = cell;
            childRt.anchoredPosition = new Vector2(
                PadL + col * stepX,
                -PadT - row * stepY);
        }
    }

    void RefreshCargoDisplay()
    {
        if (resourceManager == null || _slotIcons.Count == 0)
            return;

        int slotIndex = 0;
        for (int r = 0; r < ResourceOrder.Length && slotIndex < CargoCapacity; r++)
        {
            ResourceType type = ResourceOrder[r];
            int amount = resourceManager.Get(type);
            Sprite sprite = GetIcon(type);
            for (int u = 0; u < amount && slotIndex < CargoCapacity; u++, slotIndex++)
            {
                Image img = _slotIcons[slotIndex];
                if (img == null) continue;
                img.sprite = sprite;
                img.color = sprite != null ? Color.white : emptySlotTint;
                img.enabled = true;
            }
        }

        for (int i = slotIndex; i < _slotIcons.Count; i++)
        {
            Image img = _slotIcons[i];
            if (img == null) continue;
            ApplyEmptySlotVisual(img);
        }
    }

    void ApplyEmptySlotVisual(Image img)
    {
        if (emptySlotIcon != null)
        {
            img.sprite = emptySlotIcon;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = emptySlotTint;
        }
    }

    Sprite GetIcon(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Metal: return iconMetal;
            case ResourceType.Biyoplastik: return iconBiyoplastik;
            case ResourceType.Spares: return iconSpares;
            case ResourceType.Meal: return iconMeal;
            case ResourceType.MedicalSupplies: return iconMedicalSupplies;
            default: return null;
        }
    }
}
