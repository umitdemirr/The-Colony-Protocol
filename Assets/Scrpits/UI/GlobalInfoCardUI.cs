using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Tek bilgi kartı: üstte ikon + başlık, altta seçilen içerik gövdesi.
/// </summary>
public class GlobalInfoCardUI : MonoBehaviour
{
    public static GlobalInfoCardUI Instance { get; private set; }
    
    [System.Serializable]
    struct BodyEntry
    {
        public string key;
        public GameObject body;
    }

    [Header("Kök")]
    [SerializeField] GameObject rootPanel;

    [Header("Üst bilgi")]
    [SerializeField] Image headerIcon;
    [SerializeField] TextMeshProUGUI titleText;

    [Header("İçerik")]
    [SerializeField] Transform bodyContainer;
    [SerializeField] BodyEntry[] bodyEntries;

    readonly HashSet<Collider2D> _keepOpenIfHit = new HashSet<Collider2D>();
    readonly Dictionary<string, GameObject> _bodyByKey = new Dictionary<string, GameObject>();

    GameObject _contextTarget;

    /// <summary>
    /// Kartı açan dünya objesi (bina vb.); yok et butonu için.
    /// </summary>
    public GameObject ContextTarget => _contextTarget;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _bodyByKey.Clear();
        if (bodyEntries != null)
        {
            for (int i = 0; i < bodyEntries.Length; i++)
            {
                BodyEntry e = bodyEntries[i];
                if (string.IsNullOrWhiteSpace(e.key) || e.body == null)
                    continue;
                _bodyByKey[e.key.Trim()] = e.body;
            }
        }
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (rootPanel == null || !rootPanel.activeSelf)
            return;
        if (!Input.GetMouseButtonDown(0))
            return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 mousePos = Camera.main != null
            ? Camera.main.ScreenToWorldPoint(Input.mousePosition)
            : Vector2.zero;
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        if (hit.collider != null && hit.collider.GetComponentInParent<InfoCardInteractable>() != null)
            return;
        if (hit.collider != null && _keepOpenIfHit.Contains(hit.collider))
            return;

        Hide();
    }

    public void RegisterKeepOpenCollider(Collider2D c)
    {
        if (c != null)
            _keepOpenIfHit.Add(c);
    }

    public void UnregisterKeepOpenCollider(Collider2D c)
    {
        if (c != null)
            _keepOpenIfHit.Remove(c);
    }
    
    public GameObject ResolveBody(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        string normalized = key.Trim();
        return _bodyByKey.TryGetValue(normalized, out var body) ? body : null;
    }

    public void Show(Sprite icon, string title, GameObject bodyToShow, GameObject contextTarget = null)
    {
        if (rootPanel == null || bodyContainer == null)
            return;

        _contextTarget = contextTarget;

        if (headerIcon != null)
        {
            headerIcon.sprite = icon;
            headerIcon.enabled = icon != null;
        }
        if (titleText != null)
            titleText.text = title ?? "";

        // Runtime'da parent değiştirme: content hiyerarşisini bozmasın.
        // Önce kayıtlı tüm body'leri kapat.
        foreach (var kv in _bodyByKey)
        {
            if (kv.Value != null) kv.Value.SetActive(false);
        }
        // BodyContainer altındaki çocuklar da kapansın (map'te olmayan legacy içerikler için).
        for (int i = 0; i < bodyContainer.childCount; i++)
            bodyContainer.GetChild(i).gameObject.SetActive(false);

        // Sadece seçilen body açılsın.
        if (bodyToShow != null)
        {
            EnsureActiveInContainer(bodyToShow, bodyContainer);
            SetActiveRecursively(bodyToShow.transform, true);
        }

        rootPanel.SetActive(true);
    }

    static void EnsureActiveInContainer(GameObject target, Transform container)
    {
        if (target == null || container == null) return;
        Transform t = target.transform;
        while (t != null)
        {
            t.gameObject.SetActive(true);
            if (t == container) break;
            t = t.parent;
        }
    }

    static void SetActiveRecursively(Transform root, bool active)
    {
        if (root == null) return;
        root.gameObject.SetActive(active);
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
            SetActiveRecursively(root.GetChild(i), active);
    }

    public void Hide()
    {
        _contextTarget = null;
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;
}
