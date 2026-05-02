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
        int windSpeedMs = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentWindSpeedMs : 0;
        float prod = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentProductionKw : 0f;
        float cons = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentConsumptionKw : 0f;
        float stored = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentStoredEnergyKj : 0f;
        float cap = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentMaxStorageKj : 0f;
        string pClass = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentPlanetClass.ToString() : "-";
        string deficit = (EnergyProductionSystem.Instance != null && EnergyProductionSystem.Instance.HasPowerDeficit) ? " | KESİNTİ" : "";
        string noStorage = (EnergyProductionSystem.Instance != null && cap <= 0.01f) ? " | DEPO YOK" : "";

        statusText.text = $"N:{population} | Gün {day} {hour:D2}:{minute:D2} | P:{pClass} | Rüzgar {windSpeedMs} m/s | Ü:{prod:0} T:{cons:0} | Depo:{stored:0}/{cap:0}{noStorage}{deficit}";
    }

    static int GetNpcCount()
    {
        if (NpcSaveRegistry.Instance != null)
            return NpcSaveRegistry.Instance.GetNpcCount();
        return UnityEngine.Object.FindObjectsOfType<NpcMoverAStar2D>().Length;
    }
}
