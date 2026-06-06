using UnityEngine;

/// <summary>
/// Yerleştirilmiş binalardan enerji üretimini yönetir.
/// </summary>
public class EnergyProductionSystem : MonoBehaviour
{
    public enum PlanetClass
    {
        D,
        F,
        M,
        S
    }

    public static EnergyProductionSystem Instance { get; private set; }
    public float CurrentWindStrength01 => currentWindStrength01;
    public int CurrentWindSpeedMs => currentWindSpeedMs;
    public PlanetClass CurrentPlanetClass => planetClass;
    public float CurrentProductionKw => currentProductionKw;
    public float CurrentConsumptionKw => currentConsumptionKw;
    public float CurrentNetKw => currentNetKw;
    public float CurrentStoredEnergyKj => storedEnergyKj;
    public float CurrentMaxStorageKj => maxStorageKj;
    public bool HasPowerDeficit => hasPowerDeficit;

    [SerializeField] BuildingPlacementTracker buildingTracker;
    [SerializeField] ResourceManager resourceManager;
    [SerializeField] DayNightCycleController dayNight;

    [Header("Enerji Tick")]
    [Min(0.1f)]
    [SerializeField] float energyTickSeconds = 2f; // Planetbase benzeri: 30 tick/dk
    [Min(0.1f)]
    [SerializeField] float buildingRampDurationSeconds = 8f;

    [Header("Planet Profile")]
    [SerializeField] PlanetClass planetClass = PlanetClass.D;

    [Header("Runtime (Debug)")]
    [SerializeField] float currentProductionKw;
    [SerializeField] float currentConsumptionKw;
    [SerializeField] float currentNetKw;
    [SerializeField] int currentProducerCount;
    [SerializeField] float currentWindStrength01 = 1f;
    [SerializeField] int currentWindPercent;
    [SerializeField] int currentWindSpeedMs;
    [SerializeField] float storedEnergyKj;
    [SerializeField] float maxStorageKj;
    [SerializeField] bool hasPowerDeficit;

    [Header("Solar")]
    [Min(0f)]
    [SerializeField] float solarPeakProductionKw = 30f;
    [Header("Wind")]
    [Min(0f)]
    [SerializeField] float windPeakProductionKw = 20f;
    [Header("Wind Randomness")]
    [SerializeField] Vector2 windRandomRange = new Vector2(-0.03f, 0.03f);
    [Min(0.1f)]
    [SerializeField] float windRandomRefreshSeconds = 20f;
    [Min(0.1f)]
    [SerializeField] float windUpdateSeconds = 12f;
    [Range(0f, 1f)]
    [SerializeField] float windBaseMin = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] float windBaseMax = 0.85f;
    float _tickTimer;
    float _windRandomTimer;
    float _windRandomOffset;
    float _windUpdateTimer;
    float _targetWindStrength01;
    float _storageFromSaveKj = -1f;

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
        if (buildingTracker == null) buildingTracker = BuildingPlacementTracker.Instance;
        if (resourceManager == null) resourceManager = ResourceManager.Instance;
        if (dayNight == null) dayNight = DayNightCycleController.Instance;
        RefreshWindRandomOffset();
        _targetWindStrength01 = GetWindStrength01();
        currentWindSpeedMs = StrengthToWindSpeedMs(_targetWindStrength01);
        currentWindStrength01 = currentWindSpeedMs / 60f;
        currentWindPercent = Mathf.RoundToInt(currentWindStrength01 * 100f);
    }

    void Update()
    {
        if (resourceManager == null || resourceManager.Inventory == null) return;

        if (buildingTracker == null) buildingTracker = BuildingPlacementTracker.Instance;
        if (dayNight == null) dayNight = DayNightCycleController.Instance;
        UpdateBuildingRamps();

        _windUpdateTimer += Time.deltaTime;
        if (_windUpdateTimer >= windUpdateSeconds)
        {
            _windUpdateTimer = 0f;
            _targetWindStrength01 = GetWindStrength01();
            currentWindSpeedMs = StrengthToWindSpeedMs(_targetWindStrength01);
            currentWindStrength01 = currentWindSpeedMs / 60f;
            currentWindPercent = Mathf.RoundToInt(currentWindStrength01 * 100f);
        }

        currentProductionKw = Mathf.Round(CalculateProductionKw(out int producerCount));
        currentConsumptionKw = Mathf.Round(CalculateConsumptionKw());
        currentNetKw = currentProductionKw - currentConsumptionKw;
        currentProducerCount = producerCount;
        maxStorageKj = CalculateMaxStorageKj();

        if (_storageFromSaveKj >= 0f)
        {
            storedEnergyKj = Mathf.Clamp(_storageFromSaveKj, 0f, maxStorageKj);
            _storageFromSaveKj = -1f;
        }
        else
        {
            storedEnergyKj = Mathf.Clamp(storedEnergyKj, 0f, maxStorageKj);
        }

        _tickTimer += Time.deltaTime;
        if (_tickTimer < energyTickSeconds) return;

        _tickTimer = 0f;
        ApplyEnergyTick();
    }

    float CalculateProductionKw(out int producerCount)
    {
        producerCount = 0;
        if (buildingTracker == null || buildingTracker.buildingParent == null) return 0f;

        float sun = GetSolarMultiplier();
        int windSpeedMs = currentWindSpeedMs;
        float total = 0f;

        foreach (Transform t in buildingTracker.buildingParent)
        {
            if (t == null) continue;
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null || !pb.IsRealBuilding) continue;

            BuildingDefinition def = null;
            if (!string.IsNullOrEmpty(pb.definitionId))
                def = buildingTracker.GetDefinition(pb.definitionId);

            var producerType = def != null ? def.energyProducerType : pb.energyProducerType;
            float ramp = Mathf.Clamp01(pb.energyRamp01);
            float productionKw = 0f;
            if (producerType == BuildingDefinition.EnergyProducerType.Solar)
                productionKw = solarPeakProductionKw * sun;
            else if (producerType == BuildingDefinition.EnergyProducerType.Wind)
                productionKw = Mathf.Min(windPeakProductionKw, Mathf.Floor(windSpeedMs / 3f));
            else
                productionKw = 0f;

            float production = Mathf.Round(productionKw * ramp);
            if (production <= 0f) continue;

            total += production;
            producerCount++;
        }

        return total;
    }

    float CalculateConsumptionKw()
    {
        if (buildingTracker == null || buildingTracker.buildingParent == null) return 0f;
        float total = 0f;
        foreach (Transform t in buildingTracker.buildingParent)
        {
            if (t == null) continue;
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null || !pb.IsRealBuilding) continue;
            BuildingDefinition def = null;
            if (!string.IsNullOrEmpty(pb.definitionId))
                def = buildingTracker.GetDefinition(pb.definitionId);

            var producerType = def != null ? def.energyProducerType : pb.energyProducerType;
            if (producerType != BuildingDefinition.EnergyProducerType.None)
                continue; // Üretici binalar tüketim hanesine yazılmaz.

            int need = def != null ? def.energyNeed : pb.energyNeed;
            total += Mathf.Max(0, need) * Mathf.Clamp01(pb.energyRamp01);
        }
        return total;
    }

    void UpdateBuildingRamps()
    {
        if (buildingTracker == null || buildingTracker.buildingParent == null) return;
        float step = Time.deltaTime / Mathf.Max(0.1f, buildingRampDurationSeconds);
        foreach (Transform t in buildingTracker.buildingParent)
        {
            if (t == null) continue;
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null || !pb.IsRealBuilding) continue;
            if (pb.energyRamp01 >= 1f) continue;
            pb.energyRamp01 = Mathf.Clamp01(pb.energyRamp01 + step);
        }
    }

    float CalculateMaxStorageKj()
    {
        if (buildingTracker == null || buildingTracker.buildingParent == null) return 0f;
        float total = 0f;
        foreach (Transform t in buildingTracker.buildingParent)
        {
            if (t == null) continue;
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null || !pb.IsRealBuilding) continue;
            total += Mathf.Max(0, pb.powerCollectorCapacity);
        }
        return total;
    }

    float CalculateStoredEnergyKj()
    {
        if (buildingTracker == null || buildingTracker.buildingParent == null) return 0f;
        float total = 0f;
        foreach (Transform t in buildingTracker.buildingParent)
        {
            if (t == null) continue;
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null || !pb.IsRealBuilding) continue;
            total += pb.storedEnergy;
        }
        return total;
    }

    void ApplyEnergyTick()
    {
        // Şebeke simülasyonundan gelen depolanmış enerjiyi al
        storedEnergyKj = CalculateStoredEnergyKj();
        
        // Şebekede elektrik yetersizliği olup olmadığını kontrol et
        hasPowerDeficit = false;
        if (buildingTracker != null && buildingTracker.buildingParent != null)
        {
            foreach (Transform t in buildingTracker.buildingParent)
            {
                if (t == null) continue;
                var pb = t.GetComponent<PlacedBuilding>();
                if (pb == null || !pb.IsRealBuilding) continue;
                if (pb.energyNeed > 0 && pb.efficiency01 == 0f)
                {
                    hasPowerDeficit = true;
                    break;
                }
            }
        }

        if (resourceManager != null)
            resourceManager.Inventory.Set(ResourceType.Energy, Mathf.RoundToInt(storedEnergyKj));
    }

    float GetWindStrength01()
    {
        if (dayNight == null) return 1f;

        float p = dayNight.DayProgress + dayNight.TotalDays * 0.173f;
        float gustNoise = Mathf.PerlinNoise(p * 1.15f, 1.27f); // yavaş değişen rüzgar

        _windRandomTimer += Time.deltaTime;
        if (_windRandomTimer >= windRandomRefreshSeconds)
        {
            _windRandomTimer = 0f;
            RefreshWindRandomOffset();
        }

        float minBase = windBaseMin;
        float maxBase = windBaseMax;

        if (planetClass == PlanetClass.M) { minBase = 0f; maxBase = 0f; }      // Mars benzeri: rüzgar yok
        else if (planetClass == PlanetClass.F) { minBase = 0.45f; maxBase = 0.95f; } // Wind güçlü
        else if (planetClass == PlanetClass.S) { minBase = 0.10f; maxBase = 0.55f; } // Wind zayıf

        float wind = Mathf.Lerp(minBase, maxBase, gustNoise) + _windRandomOffset;
        return Mathf.Clamp01(wind);
    }

    int StrengthToWindSpeedMs(float windStrength01)
    {
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(windStrength01) * 60f), 0, 60);
    }

    float GetSolarMultiplier()
    {
        if (dayNight == null) return 1f;
        return dayNight.GetSunStrength01(); // gece 0, gündüz 0..1, zirvede 1
    }

    void RefreshWindRandomOffset()
    {
        float min = Mathf.Min(windRandomRange.x, windRandomRange.y);
        float max = Mathf.Max(windRandomRange.x, windRandomRange.y);
        _windRandomOffset = Random.Range(min, max);
    }

    public EnergySystemSaveData ToSaveData()
    {
        return new EnergySystemSaveData { storedEnergyKj = storedEnergyKj };
    }

    public void LoadFromSaveData(EnergySystemSaveData data)
    {
        if (data == null) return;
        _storageFromSaveKj = Mathf.Max(0f, data.storedEnergyKj);
    }
}
