using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Binaların ağ bağlantılarını (BFS), elektrik ve su şebekelerini, 
/// oksijen yayılımlarını ve çok yavaş sağlık aşınmalarını yöneten merkezi simülasyon kontrolcüsü.
/// </summary>
public class BuildingSimulationSystem : MonoBehaviour
{
    public static BuildingSimulationSystem Instance { get; private set; }

    [Header("Simülasyon Ayarları")]
    [Min(0.1f)]
    [SerializeField] float tickSeconds = 2f;
    [Tooltip("Binaların sağlığının (100 HP) sıfıra düşmesi için geçen süre (Saniye). 1200 = 20 dakika.")]
    [SerializeField] float healthDecayDuration = 1200f;


    [Header("Hata Ayıklama (Gözlemleme)")]
    [SerializeField] int activeNetworksCount = 0;
    [SerializeField] float globalWaterProduction = 0f;
    [SerializeField] float globalWaterConsumption = 0f;

    float _tickTimer;
    BuildingPlacementTracker _buildingTracker;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        _buildingTracker = BuildingPlacementTracker.Instance;
    }

    void Update()
    {
        if (_buildingTracker == null) _buildingTracker = BuildingPlacementTracker.Instance;
        if (_buildingTracker == null) return;

        _tickTimer += Time.deltaTime;
        if (_tickTimer < tickSeconds) return;

        _tickTimer = 0f;
        SimulateNetworks();
    }

    void SimulateNetworks()
    {
        if (GridManager.Instance == null) return;

        var allBuildings = FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
        var roadCells = ModularLShapeRoadGenerator.Instance != null 
            ? ModularLShapeRoadGenerator.Instance.GetAllRoadAndPipeCells() 
            : new HashSet<Vector3Int>();

        List<PlacedBuilding> globalNetwork = new List<PlacedBuilding>();
        List<PlacedBuilding> disconnectedBuildings = new List<PlacedBuilding>();

        foreach (var pb in allBuildings)
        {
            if (pb != null && pb.IsRealBuilding)
            {
                if (IsBuildingConnected(pb, roadCells))
                {
                    globalNetwork.Add(pb);
                }
                else
                {
                    disconnectedBuildings.Add(pb);
                }
            }
        }

        activeNetworksCount = globalNetwork.Count > 0 ? 1 : 0;
        globalWaterProduction = 0f;
        globalWaterConsumption = 0f;

        // Bağlantısı olmayan binaları pasif hale getir
        foreach (var pb in disconnectedBuildings)
        {
            pb.efficiency01 = 0f;
            pb.oxygenProductionCurrent = 0f;
            pb.storedEnergy = 0f;
            pb.waterAmount = 0f;
            pb.networkEnergyProduction = 0f;
            pb.networkEnergyConsumption = 0f;
            pb.networkWaterProduction = 0f;
            pb.networkWaterConsumption = 0f;

            if (!pb.isExterior && pb.storesOxygen)
            {
                // Bağlantısız binalarda oksijen hızla azalsın (saniyede 15 birim)
                pb.oxygenChangeRate = -15f;
            }

            // Kapalı binanın ışıklarını kapat
            var lights = pb.GetComponentsInChildren<Light2D>(true);
            foreach (var lt in lights)
            {
                lt.enabled = false;
            }
        }

        if (globalNetwork.Count == 0)
        {
            // Bağlantısı olmayan binalarda da HP aşınması uygula
            ApplyHpDecay(disconnectedBuildings);
            return;
        }

        // Küresel astronot sayısını bul
        var allAstronauts = FindObjectsByType<Astronaut>(FindObjectsSortMode.None);
        int astronautsInNetwork = allAstronauts != null ? allAstronauts.Length : 0;

        float sun = DayNightCycleController.Instance != null ? DayNightCycleController.Instance.GetSunStrength01() : 1f;
        int windSpeedMs = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentWindSpeedMs : 20;

        float netEnergyProduction = 0f;
        float netEnergyConsumption = 0f;
        float totalStoredEnergy = 0f;
        float totalStorageCapacity = 0f;

        float totalWaterProduction = 0f;
        float totalWaterConsumption = 0f;
        float totalStoredWater = 0f;
        float totalWaterCapacity = 0f;

        int networkO2SupportCapacity = 0; // Planetbase tipi kişi desteği kapasitesi

        // A. Küresel Üretim/Tüketim Kapasitelerini Hesapla (Bina verimlilik rampası ramp01 de dahil edilerek)
        foreach (var pb in globalNetwork)
        {
            float ramp = Mathf.Clamp01(pb.energyRamp01);

            // Enerji Hesabı
            if (pb.energyProducerType == BuildingDefinition.EnergyProducerType.Solar)
            {
                netEnergyProduction += 30f * sun * ramp; // Peak solar production
            }
            else if (pb.energyProducerType == BuildingDefinition.EnergyProducerType.Wind)
            {
                netEnergyProduction += Mathf.Min(20f, Mathf.Floor(windSpeedMs / 3f)) * ramp; // Peak wind production
            }
            else
            {
                netEnergyConsumption += pb.energyNeed * ramp;
            }

            totalStoredEnergy += pb.storedEnergy;
            totalStorageCapacity += pb.powerCollectorCapacity;

            // Su Hesabı
            if (pb.isWaterProducer)
            {
                totalWaterProduction += pb.waterProductionRate;
            }
            if (pb.requiresWater)
            {
                totalWaterConsumption += pb.waterConsumptionRate;
            }

            totalStoredWater += pb.waterAmount;
            totalWaterCapacity += pb.waterCapacity;
        }

        globalWaterProduction = totalWaterProduction;
        globalWaterConsumption = totalWaterConsumption;

        // B. Şebeke Tüketimlerini ve Depolarını Uygula (Enerji)
        float energyFlowKj = (netEnergyProduction - netEnergyConsumption) * tickSeconds;
        if (energyFlowKj >= 0f)
        {
            totalStoredEnergy = Mathf.Min(totalStorageCapacity, totalStoredEnergy + energyFlowKj);
        }
        else
        {
            float deficit = -energyFlowKj;
            float drawn = Mathf.Min(totalStoredEnergy, deficit);
            totalStoredEnergy -= drawn;
        }

        // Enerjiyi bağlı bataryalara dengeli şekilde geri dağıt
        if (totalStorageCapacity > 0f)
        {
            float ratio = totalStoredEnergy / totalStorageCapacity;
            foreach (var pb in globalNetwork)
            {
                if (pb != null && pb.powerCollectorCapacity > 0)
                {
                    pb.storedEnergy = pb.powerCollectorCapacity * ratio;
                }
            }
        }

        // Su Tüketimlerini ve Depolarını Uygula
        float waterFlow = (totalWaterProduction - totalWaterConsumption) * tickSeconds;
        if (waterFlow >= 0f)
        {
            totalStoredWater = Mathf.Min(totalWaterCapacity, totalStoredWater + waterFlow);
        }
        else
        {
            float deficit = -waterFlow;
            float drawn = Mathf.Min(totalStoredWater, deficit);
            totalStoredWater -= drawn;
        }

        // Suyu bağlı depolara dengeli şekilde geri dağıt
        if (totalWaterCapacity > 0f)
        {
            float ratio = totalStoredWater / totalWaterCapacity;
            foreach (var pb in globalNetwork)
            {
                if (pb != null && pb.waterCapacity > 0)
                {
                    pb.waterAmount = pb.waterCapacity * ratio;
                }
            }
        }

        // Şebekede güç/su var mı tespit et
        bool hasPower = (netEnergyProduction >= netEnergyConsumption) || (totalStoredEnergy > 0f);
        bool hasWater = (totalWaterProduction >= totalWaterConsumption) || (totalStoredWater > 0f);

        // C. Oksijen Üreticilerinin Çalışma Verimliliğini Hesapla
        foreach (var pb in globalNetwork)
        {
            if (pb.isOxygenProducer)
            {
                // O2 binasının çalışması için bağlı olduğu ağda elektrik ve su olmalı!
                if (hasPower && hasWater)
                {
                    pb.efficiency01 = 1f;
                    pb.oxygenProductionCurrent = pb.oxygenSupportCapacity;
                    networkO2SupportCapacity += pb.oxygenSupportCapacity;
                }
                else
                {
                    pb.efficiency01 = 0f;
                    pb.oxygenProductionCurrent = 0f;
                }
            }
            else if (pb.isWaterProducer)
            {
                // Su çıkarıcının çalışması için elektrik olmalı
                pb.efficiency01 = hasPower ? 1f : 0f;
            }
            else
            {
                // Tüketici binaların verimliliği
                bool waterMet = !pb.requiresWater || hasWater;
                pb.efficiency01 = (hasPower && waterMet) ? 1f : 0f;
            }
        }

        // D. Yaşam Alanlarındaki (Interior) Oksijen Miktarını Simüle Et (Planetbase Tipi)
        // Ağdaki toplam oksijen üretici kapasitesi toplam insan nüfusunu destekliyorsa oksijen vardır!
        float o2ChangeRate = -15f; // Varsayılan düşüş hızı (Havasızlık)

        if (networkO2SupportCapacity > 0)
        {
            if (astronautsInNetwork <= 0)
            {
                // Kimse yoksa ama üretim varsa hızlıca dolar
                o2ChangeRate = 15f;
            }
            else
            {
                // Kapasite / Astronot oranı
                float ratio = (float)networkO2SupportCapacity / astronautsInNetwork;
                if (ratio >= 1f)
                {
                    // Tam kapasite veya fazlası: Oksijen artar
                    o2ChangeRate = 15f;
                }
                else if (ratio > 0.5f)
                {
                    // %50-%100 arası: Yavaş düşüş (Yetersiz ama tamamen yok değil)
                    o2ChangeRate = -5f;
                }
                else
                {
                    // %50'den az: Hızlı düşüş
                    o2ChangeRate = -12f;
                }
            }
        }

        foreach (var pb in globalNetwork)
        {
            if (!pb.isExterior && pb.storesOxygen)
            {
                // Rate'i PlacedBuilding.Update() smooth olarak uygulayacak
                pb.oxygenChangeRate = o2ChangeRate;
            }

            // Ağ bazlı toplam elektrik/su üretim/tüketim verilerini gözlemlemek için binalara yaz
            pb.networkEnergyProduction = netEnergyProduction;
            pb.networkEnergyConsumption = netEnergyConsumption;
            pb.networkWaterProduction = totalWaterProduction;
            pb.networkWaterConsumption = totalWaterConsumption;
        }

        // E. Karanlık Mod Simülasyonu (Blackout)
        bool isNight = DayNightCycleController.Instance != null && DayNightCycleController.Instance.IsNight;
        foreach (var pb in globalNetwork)
        {
            // Gece olduğunda şebekede elektrik yoksa binalar karanlıkta kalsın
            bool shouldBeLit = !isNight || hasPower;
            var lights = pb.GetComponentsInChildren<Light2D>(true);
            foreach (var lt in lights)
            {
                lt.enabled = shouldBeLit;
            }
        }

        // 3. TÜM BİNALARIN ZAMANLA YAVAŞ AŞINMASI (HP DECAY)
        ApplyHpDecay(globalNetwork);
        ApplyHpDecay(disconnectedBuildings);
    }

    void ApplyHpDecay(List<PlacedBuilding> buildings)
    {
        float decayHp = (100f / healthDecayDuration) * tickSeconds;
        foreach (var pb in buildings)
        {
            if (pb == null) continue;
            // Binaların sağlığı zamanla çok yavaşça azalır (Weathering)
            if (pb.exactHealth < 0f)
            {
                pb.exactHealth = pb.currentHealth;
            }

            pb.exactHealth = Mathf.Max(0f, pb.exactHealth - decayHp);
            pb.currentHealth = Mathf.RoundToInt(pb.exactHealth);
        }
    }

    bool IsBuildingConnected(PlacedBuilding pb, HashSet<Vector3Int> roadCells)
    {
        if (pb == null) return false;

        // Başlangıç Roketi her zaman bağlı sayılır
        if (pb.gameObject.name.Contains("Rocket") || 
            (!string.IsNullOrEmpty(pb.definitionId) && pb.definitionId.ToLowerInvariant().Contains("rocket")))
        {
            return true;
        }

        // Binalar için footprint hücrelerini bul
        var cells = new HashSet<Vector3Int>();
        var occ = pb.GetComponentInChildren<GridOccupier2D>(true);
        if (occ != null)
        {
            cells = occ.ComputeOccupiedCells(GridManager.Instance);
        }
        else
        {
            Vector3Int rootCell = GridManager.Instance.visualTilemap.WorldToCell(pb.transform.position);
            cells.Add(rootCell);
        }

        // Eğer footprint veya rootCell roadCells ile doğrudan kesişiyorsa bağlıdır
        if (cells.Overlaps(roadCells)) return true;

        // Veya komşularından biri roadCells içindeyse bağlıdır (kenar bağlantıları için)
        foreach (var cell in cells)
        {
            Vector3Int[] neighbors = new Vector3Int[] {
                cell + Vector3Int.right,
                cell + Vector3Int.left,
                cell + Vector3Int.up,
                cell + Vector3Int.down
            };
            foreach (var nb in neighbors)
            {
                if (roadCells.Contains(nb)) return true;
            }
        }

        return false;
    }
}
