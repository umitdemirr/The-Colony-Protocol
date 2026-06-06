using UnityEngine;
using UnityEngine.UI;

public class BuildingSelectButton : MonoBehaviour
{
    public BuildingDefinition building;
    public MouseCheck mouseCheck;

    [Header("Limit Görseli")]
    [Tooltip("Buton bileşeni – limit aşılınca interactable=false yapılır.")]
    public Button button;

    void Awake()
    {
        if (mouseCheck == null)
            mouseCheck = FindFirstObjectByType<MouseCheck>();
        if (button == null)
            button = GetComponent<Button>();
    }

    void Update()
    {
        RefreshInteractable();
    }

    // Unity UI Button OnClick için parametresiz çağrı.
    public void Select()
    {
        if (mouseCheck == null)
            mouseCheck = FindFirstObjectByType<MouseCheck>();
        if (mouseCheck == null)
        {
            Debug.LogWarning($"[BuildingSelectButton] MouseCheck bulunamadı: {name}");
            return;
        }
        if (building == null)
        {
            Debug.LogWarning($"[BuildingSelectButton] building asset atanmadı: {name}");
            return;
        }

        if (!CanPlace())
        {
            Debug.LogWarning($"[BuildingSelectButton] Yerleştirme limiti doldu: {building.displayName}");
            return;
        }

        Debug.Log($"[BuildingSelectButton] Seçim: {building.displayName} (obj: {name})");
        mouseCheck.SetBuilding(building);
    }

    /// <summary>
    /// Bu bina türünden daha fazla yerleştirilebilir mi?
    /// </summary>
    bool CanPlace()
    {
        if (building == null) return false;

        // Bağımlılık kontrolü (gerekli binalar sahada var mı?)
        if (!building.AreDependenciesMet())
            return false;

        // Limit kontrolü (0 = sınırsız)
        if (building.maxPlacementCount <= 0) return true;
        int placed = CountPlacedById(building.GetSaveId());
        return placed < building.maxPlacementCount;
    }

    void RefreshInteractable()
    {
        if (button == null || building == null) return;
        button.interactable = CanPlace();
    }

    static int CountPlacedById(string saveId)
    {
        int count = 0;
        var all = FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].definitionId == saveId)
                count++;
        }
        return count;
    }
}