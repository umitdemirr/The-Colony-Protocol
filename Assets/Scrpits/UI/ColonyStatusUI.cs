using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Koloni durumu ve zaman döngüsü: Gelişmiş grafiksel zaman (Gündüz/Gece slider'ı),
/// astronot sayısı ve rüzgar göstergelerini yönetir.
/// </summary>
public class ColonyStatusUI : MonoBehaviour
{
    [Header("Eski Tek Satır Metin (Geriye Dönük Uyumluluk)")]
    public TMP_Text statusText;

    [Header("Astronot Göstergesi")]
    [SerializeField] private Image astronautIcon;
    [SerializeField] private TMP_Text astronautCountText;

    [Header("Gün / Saat (Zaman) Göstergesi")]
    [SerializeField] private Image timeIcon;
    [SerializeField] private Sprite daySprite;
    [SerializeField] private Sprite nightSprite;
    [SerializeField] private Slider timeSlider;
    [SerializeField] private Image timeFillImage;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timeText;

    [Header("Rüzgar Göstergesi")]
    [SerializeField] private Image windIcon;
    [SerializeField] private Slider windSlider;
    [SerializeField] private Image windFillImage;
    [SerializeField] private TMP_Text windText;

    void Start()
    {
        // Slider limitlerini koddan garanti altına alıyoruz (Unity Editör ayarlarından bağımsız kılmak için)
        if (timeSlider != null)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 1f;
        }
        if (windSlider != null)
        {
            windSlider.minValue = 0f;
            windSlider.maxValue = 1f;
        }
    }

    void LateUpdate()
    {
        // 1. Verileri Topla
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

        // 2. Eski Tek Satır Metni Güncelle (Eğer Atanmışsa)
        if (statusText != null)
        {
            statusText.text = $"N:{population} | Gün {day} {hour:D2}:{minute:D2} | P:{pClass} | Rüzgar {windSpeedMs} m/s | Ü:{prod:0} T:{cons:0} | Depo:{stored:0}/{cap:0}{noStorage}{deficit}";
        }

        // 3. Astronot Göstergesi
        if (astronautCountText != null)
        {
            astronautCountText.text = population.ToString();
        }
        if (astronautIcon != null)
        {
            astronautIcon.enabled = astronautIcon.sprite != null;
        }

        // 4. Gün ve Saat (Zaman) Göstergesi
        if (dayText != null)
        {
            dayText.text = $"Gün {day}";
        }
        if (timeText != null)
        {
            timeText.text = $"{hour:D2}:{minute:D2}";
        }

        if (timeSlider != null || timeFillImage != null)
        {
            float currentHourFloat = hour + (minute / 60f);
            float timeRatio = 0f;

            // Gündüz Döngüsü: 06:00 ile 18:00 arası (12 Saat)
            if (currentHourFloat >= 6f && currentHourFloat < 18f)
            {
                if (timeIcon != null && daySprite != null)
                {
                    timeIcon.sprite = daySprite;
                    timeIcon.enabled = true;
                }
                // Slider 6'da 0 olur, 18'de 1 olur (fullenir)
                timeRatio = (currentHourFloat - 6f) / 12f;
            }
            // Gece Döngüsü: 18:00 ile şafak (06:00) arası (12 Saat)
            else
            {
                if (timeIcon != null && nightSprite != null)
                {
                    timeIcon.sprite = nightSprite;
                    timeIcon.enabled = true;
                }
                // Slider 18'de 0 olur, gece yarısında 0.5 olur, 6'da 1 olur (fullenir)
                if (currentHourFloat >= 18f)
                {
                    timeRatio = (currentHourFloat - 18f) / 12f;
                }
                else
                {
                    timeRatio = (currentHourFloat + 6f) / 12f;
                }
            }

            if (timeSlider != null)
            {
                timeSlider.value = timeRatio;
            }
            if (timeFillImage != null)
            {
                timeFillImage.fillAmount = timeRatio;
            }
        }

        // 5. Rüzgar Göstergesi
        if (windText != null)
        {
            windText.text = $"{windSpeedMs} m/s";
        }
        
        float windRatio = Mathf.Clamp01((float)windSpeedMs / 60f);
        if (windSlider != null)
        {
            windSlider.value = windRatio;
        }
        if (windFillImage != null)
        {
            windFillImage.fillAmount = windRatio;
        }

        if (windIcon != null)
        {
            windIcon.enabled = windIcon.sprite != null;
        }
    }

    static int GetNpcCount()
    {
        if (NpcSaveRegistry.Instance != null)
            return NpcSaveRegistry.Instance.GetNpcCount();
#pragma warning disable CS0618 // Type or member is obsolete
        return UnityEngine.Object.FindObjectsOfType<NpcMoverAStar2D>().Length;
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
