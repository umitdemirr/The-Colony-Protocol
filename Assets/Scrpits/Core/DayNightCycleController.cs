using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Görsel gece-gündüz döngüsü. Kamera arka planı, ambient ve Global Light 2D'yi günceller.
/// </summary>
public class DayNightCycleController : MonoBehaviour
{
    public static DayNightCycleController Instance { get; private set; }

    [Header("Döngü")]
    [Tooltip("Oyun içi 24 saatin gerçek saniye karşılığı. 600 = ~10 dk")]
    public float dayDuration = 600f;

    [Header("Gündüz renkleri")]
    public Color daySkyColor = new Color(0.19f, 0.30f, 0.47f);
    public Color dayAmbientSky = new Color(0.21f, 0.23f, 0.26f);
    public Color dayAmbientEquator = new Color(0.11f, 0.13f, 0.13f);
    public Color dayAmbientGround = new Color(0.05f, 0.04f, 0.04f);
    public Color dayLightColor = Color.white;
    [Range(0.3f, 1.5f)]
    public float dayLightIntensity = 1f;

    [Header("Gece renkleri")]
    public Color nightSkyColor = new Color(0.02f, 0.02f, 0.08f);
    public Color nightAmbientSky = new Color(0.03f, 0.03f, 0.08f);
    public Color nightAmbientEquator = new Color(0.02f, 0.02f, 0.06f);
    public Color nightAmbientGround = new Color(0.01f, 0.01f, 0.03f);
    public Color nightLightColor = new Color(0.4f, 0.45f, 0.7f);
    [Range(0.1f, 0.6f)]
    public float nightLightIntensity = 0.25f;

    [Header("Referanslar (otomatik bulunur)")]
    public Camera targetCamera;
    public Light2D globalLight2D;

    float _dayProgress = 0.25f; // 0.25 = gündüz başlangıcı
    int _totalDays;

    public float DayProgress => _dayProgress;
    public bool IsNight => _dayProgress > 0.5f;
    public int TotalDays => _totalDays;

    public float GetSunStrength01()
    {
        // EvaluateCycle: 0 = tam gündüz, 1 = tam gece
        return 1f - EvaluateCycle(_dayProgress);
    }

    public void GetInGameTime(out int hour, out int minute)
    {
        float totalMinutes = _dayProgress * 24f * 60f;
        hour = ((int)totalMinutes / 60) % 24;
        minute = (int)totalMinutes % 60;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Sadece bileşeni sil, paylaşılan GameObject'i silme!
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (globalLight2D == null) globalLight2D = FindObjectOfType<Light2D>();
    }

    void Update()
    {
        _dayProgress += Time.deltaTime / dayDuration;
        if (_dayProgress >= 1f)
        {
            _dayProgress -= 1f;
            _totalDays++;
        }
        ApplyVisuals();
    }

    void ApplyVisuals()
    {
        float t = EvaluateCycle(_dayProgress);
        if (targetCamera != null)
            targetCamera.backgroundColor = Color.Lerp(daySkyColor, nightSkyColor, t);

        RenderSettings.ambientSkyColor = Color.Lerp(dayAmbientSky, nightAmbientSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(dayAmbientEquator, nightAmbientEquator, t);
        RenderSettings.ambientGroundColor = Color.Lerp(dayAmbientGround, nightAmbientGround, t);

        if (globalLight2D != null)
        {
            globalLight2D.color = Color.Lerp(dayLightColor, nightLightColor, t);
            globalLight2D.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, t);
        }
    }

    static float EvaluateCycle(float p)
    {
        if (p < 0.25f) return Mathf.Lerp(1f, 0f, p / 0.25f);       // gece->gündüz (şafak)
        if (p < 0.5f) return 0f;                                     // gündüz
        if (p < 0.75f) return Mathf.InverseLerp(0.5f, 0.75f, p);    // gündüz->gece (akşam)
        return 1f;                                                    // gece
    }

    public void SetDayProgress(float progress)
    {
        _dayProgress = Mathf.Clamp01(progress);
        ApplyVisuals();
    }

    public DayNightSaveData ToSaveData()
    {
        return new DayNightSaveData { dayProgress = _dayProgress, totalDays = _totalDays };
    }

    public void LoadFromSaveData(DayNightSaveData data)
    {
        if (data == null) return;
        _dayProgress = Mathf.Clamp01(data.dayProgress);
        _totalDays = Mathf.Max(0, data.totalDays);
        ApplyVisuals();
    }
}

[System.Serializable]
public class DayNightSaveData
{
    public float dayProgress;
    public int totalDays;
}
