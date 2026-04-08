using UnityEngine;
using TMPro;

/// <summary>
/// Koloni durumu: "👥 12 | Gün 5 14:32" formatında tek satır.
/// </summary>
public class ColonyStatusUI : MonoBehaviour
{
    public TMP_Text statusText;

    void LateUpdate()
    {
        if (statusText == null) return;

        int population = GetNpcCount();
        int day = DayNightCycleController.Instance != null ? DayNightCycleController.Instance.TotalDays : 0;
        int hour = 6, minute = 0;
        if (DayNightCycleController.Instance != null)
            DayNightCycleController.Instance.GetInGameTime(out hour, out minute);

        statusText.text = $"N:{population} | Gün {day} {hour:D2}:{minute:D2}";
    }

    static int GetNpcCount()
    {
        if (NpcSaveRegistry.Instance != null)
            return NpcSaveRegistry.Instance.GetNpcCount();
        return UnityEngine.Object.FindObjectsOfType<NpcMoverAStar2D>().Length;
    }
}
