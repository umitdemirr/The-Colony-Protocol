using UnityEngine;

public class BuildingSelectButton : MonoBehaviour
{
    public BuildingDefinition building;
    public MouseCheck mouseCheck;

    void Awake()
    {
        if (mouseCheck == null)
            mouseCheck = FindFirstObjectByType<MouseCheck>();
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

        Debug.Log($"[BuildingSelectButton] Seçim: {building.displayName} (obj: {name})");
        mouseCheck.SetBuilding(building);
    }
}