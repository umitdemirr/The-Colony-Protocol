using UnityEngine;

public class BuildingSelectButton : MonoBehaviour
{
    public BuildingDefinition building;
    public MouseCheck mouseCheck;

    // Unity UI Button OnClick için parametresiz çağrı.
    public void Select()
    {
        if (mouseCheck == null) return;
        if (building == null)
        {
            Debug.LogWarning($"[BuildingSelectButton] building asset atanmadı: {name}");
            return;
        }

        Debug.Log($"[BuildingSelectButton] Seçim: {building.displayName} (obj: {name})");
        mouseCheck.SetBuilding(building);
    }
}