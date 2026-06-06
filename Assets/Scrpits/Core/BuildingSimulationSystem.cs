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
        if (GridManager.Instance == null || GridManager.Instance.visualTilemap == null) return;

        var tilemap = GridManager.Instance.visualTilemap;
        BoundsInt bounds = tilemap.cellBounds;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        List<List<PlacedBuilding>> networks = new List<List<PlacedBuilding>>();

        // 1. GRID ÜZERİNDE BFS İLE BİRBİRİNE BAĞLI AĞLARI TESPİT ET
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (visited.Contains(cell)) continue;

                var node = GridManager.Instance.GetNode(cell);
                if (node != null && node.placedObject != null)
                {
                    // Yeni bir ağ dalı bulduk, BFS başlat
                    List<PlacedBuilding> networkBuildings = new List<PlacedBuilding>();
                    HashSet<GameObject> addedObjects = new HashSet<GameObject>();

                    Queue<Vector3Int> queue = new Queue<Vector3Int>();
                    queue.Enqueue(cell);
                    visited.Add(cell);

                    while (queue.Count > 0)
                    {
                        Vector3Int currCell = queue.Dequeue();
                        var currNode = GridManager.Instance.GetNode(currCell);
                        if (currNode != null && currNode.placedObject != null)
                        {
                            var pb = currNode.placedObject.GetComponentInParent<PlacedBuilding>();
                            if (pb != null && !addedObjects.Contains(pb.gameObject))
                            {
                                networkBuildings.Add(pb);
                                addedObjects.Add(pb.gameObject);
                            }
                        }

                        // 4 yönlü komşuları kontrol et (Yukarı, Aşağı, Sol, Sağ)
                        Vector3Int[] neighbors = new Vector3Int[] {
                            new Vector3Int(currCell.x + 1, currCell.y, 0),
                            new Vector3Int(currCell.x - 1, currCell.y, 0),
                            new Vector3Int(currCell.x, currCell.y + 1, 0),
                            new Vector3Int(currCell.x, currCell.y - 1, 0)
                        };

                        foreach (var nb in neighbors)
                        {
                            if (visited.Contains(nb)) continue;
                            var nbNode = GridManager.Instance.GetNode(nb);
                            if (nbNode != null && nbNode.placedObject != null)
                            {
                                queue.Enqueue(nb);
                                visited.Add(nb);
                            }
                        }
                    }

                    if (networkBuildings.Count > 0)
                    {
                        networks.Add(networkBuildings);
                    }
                }
            }
        }

        activeNetworksCount = networks.Count;
        globalWaterProduction = 0f;
        globalWaterConsumption = 0f;

        // Oksijen tüketim talebi için toplam astronot sayısını bul
        int totalAstronauts = FindObjectsByType<Astronaut>(FindObjectsSortMode.None).Length;

        // 2. HER BİR BAĞLI AĞ ADASINI KENDİ İÇİNDE SİMÜLE ET
        foreach (var network in networks)
        {
            float netEnergyProduction = 0f;
            float netEnergyConsumption = 0f;
            float totalStoredEnergy = 0f;
            float totalStorageCapacity = 0f;

            float totalWaterProduction = 0f;
            float totalWaterConsumption = 0f;

            int networkO2SupportCapacity = 0; // Planetbase tipi kişi desteği kapasitesi

            float sun = DayNightCycleController.Instance != null ? DayNightCycleController.Instance.GetSunStrength01() : 1f;
            int windSpeedMs = EnergyProductionSystem.Instance != null ? EnergyProductionSystem.Instance.CurrentWindSpeedMs : 20;

            // A. Ağın Toplam Üretim/Tüketim Kapasitelerini Hesapla
            foreach (var pb in network)
            {
                if (pb == null || !pb.IsRealBuilding) continue;

                // Enerji Hesabı
                if (pb.energyProducerType == BuildingDefinition.EnergyProducerType.Solar)
                {
                    netEnergyProduction += 30f * sun; // Peak solar production
                }
                else if (pb.energyProducerType == BuildingDefinition.EnergyProducerType.Wind)
                {
                    netEnergyProduction += Mathf.Min(20f, Mathf.Floor(windSpeedMs / 3f)); // Peak wind production
                }
                else
                {
                    netEnergyConsumption += pb.energyNeed;
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
            }

            globalWaterProduction += totalWaterProduction;
            globalWaterConsumption += totalWaterConsumption;

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
                foreach (var pb in network)
                {
                    if (pb != null && pb.powerCollectorCapacity > 0)
                    {
                        pb.storedEnergy = pb.powerCollectorCapacity * ratio;
                    }
                }
            }

            // Şebekede güç/su var mı tespit et
            bool hasPower = (netEnergyProduction >= netEnergyConsumption) || (totalStoredEnergy > 0f);
            bool hasWater = totalWaterProduction >= totalWaterConsumption;

            // C. Oksijen Üreticilerinin Çalışma Verimliliğini Hesapla
            foreach (var pb in network)
            {
                if (pb == null || !pb.IsRealBuilding) continue;

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
            bool hasOxygen = networkO2SupportCapacity > 0 && networkO2SupportCapacity >= totalAstronauts;

            foreach (var pb in network)
            {
                if (pb == null || !pb.IsRealBuilding) continue;

                if (!pb.isExterior && pb.storesOxygen)
                {
                    pb.oxygenAmount = hasOxygen ? 100f : 0f;
                }

                // Ağ bazlı toplam elektrik/su üretim/tüketim verilerini gözlemlemek için binalara yaz
                pb.networkEnergyProduction = netEnergyProduction;
                pb.networkEnergyConsumption = netEnergyConsumption;
                pb.networkWaterProduction = totalWaterProduction;
                pb.networkWaterConsumption = totalWaterConsumption;
            }

            // E. Karanlık Mod Simülasyonu (Blackout)
            bool isNight = DayNightCycleController.Instance != null && DayNightCycleController.Instance.IsNight;
            foreach (var pb in network)
            {
                if (pb == null) continue;

                // Gece olduğunda şebekede elektrik yoksa binalar karanlıkta kalsın
                bool shouldBeLit = !isNight || hasPower;
                var lights = pb.GetComponentsInChildren<Light2D>(true);
                foreach (var lt in lights)
                {
                    lt.enabled = shouldBeLit;
                }
            }
        }

        // 3. TÜM BİNALARIN ZAMANLA YAVAŞ AŞINMASI (HP DECAY)
        float decayHp = (100f / healthDecayDuration) * tickSeconds;
        foreach (Transform t in _buildingTracker.buildingParent)
        {
            if (t == null) continue;
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null || !pb.IsRealBuilding) continue;

            // Binaların sağlığı zamanla çok yavaşça azalır (Weathering)
            if (pb.exactHealth < 0f)
            {
                pb.exactHealth = pb.currentHealth;
            }

            pb.exactHealth = Mathf.Max(0f, pb.exactHealth - decayHp);
            pb.currentHealth = Mathf.RoundToInt(pb.exactHealth);
        }
    }
}
