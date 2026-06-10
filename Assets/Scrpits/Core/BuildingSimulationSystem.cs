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

        // 1. TÜM SİMÜLASYON HÜCRELERİNİ VE BİNALARI HARİTALANDIR
        Dictionary<Vector3Int, PlacedBuilding> cellToBuilding = new Dictionary<Vector3Int, PlacedBuilding>();
        HashSet<Vector3Int> simulationCells = new HashSet<Vector3Int>(roadCells);

        foreach (var pb in allBuildings)
        {
            if (pb == null) continue;

            if (pb.IsRealBuilding)
            {
                var occ = pb.GetComponentInChildren<GridOccupier2D>(true);
                if (occ != null)
                {
                    var footprint = occ.ComputeOccupiedCells(GridManager.Instance);
                    foreach (var cell in footprint)
                    {
                        cellToBuilding[cell] = pb;
                        simulationCells.Add(cell);
                    }
                }
                else
                {
                    // Fallback: GridOccupier yoksa merkez hücresini kullan
                    Vector3Int rootCell = GridManager.Instance.visualTilemap.WorldToCell(pb.transform.position);
                    cellToBuilding[rootCell] = pb;
                    simulationCells.Add(rootCell);
                }
            }
            else
            {
                // Yol/boru parçalarının hücrelerini de simülasyon ağında yürümek için ekle
                Vector3Int rootCell = GridManager.Instance.visualTilemap.WorldToCell(pb.transform.position);
                simulationCells.Add(rootCell);
            }
        }

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        List<List<PlacedBuilding>> networks = new List<List<PlacedBuilding>>();

        // 2. TÜM HÜCRELER ÜZERİNDEN BFS İLE BAĞLI ADALARI BUL
        foreach (var startCell in simulationCells)
        {
            if (visited.Contains(startCell)) continue;

            // Yeni bir ağ dalı bulduk
            List<PlacedBuilding> networkBuildings = new List<PlacedBuilding>();
            HashSet<GameObject> addedInThisNetwork = new HashSet<GameObject>();

            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(startCell);
            visited.Add(startCell);

            while (queue.Count > 0)
            {
                Vector3Int currCell = queue.Dequeue();

                // Eğer bu hücrede bir bina varsa ağa ekle
                if (cellToBuilding.TryGetValue(currCell, out var pb))
                {
                    if (pb != null && !addedInThisNetwork.Contains(pb.gameObject))
                    {
                        networkBuildings.Add(pb);
                        addedInThisNetwork.Add(pb.gameObject);
                    }
                }

                // 4 yönlü komşuları kontrol et
                Vector3Int[] neighbors = new Vector3Int[] {
                    currCell + Vector3Int.right,
                    currCell + Vector3Int.left,
                    currCell + Vector3Int.up,
                    currCell + Vector3Int.down
                };

                foreach (var nb in neighbors)
                {
                    if (visited.Contains(nb)) continue;
                    if (simulationCells.Contains(nb))
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

        activeNetworksCount = networks.Count;
        globalWaterProduction = 0f;
        globalWaterConsumption = 0f;

        // 2. HER BİR BAĞLI AĞ ADASINI KENDİ İÇİNDE SİMÜLE ET
        var allAstronauts = FindObjectsByType<Astronaut>(FindObjectsSortMode.None);
        var tilemap = GridManager.Instance.visualTilemap;

        foreach (var network in networks)
        {
            float netEnergyProduction = 0f;
            float netEnergyConsumption = 0f;
            float totalStoredEnergy = 0f;
            float totalStorageCapacity = 0f;

            float totalWaterProduction = 0f;
            float totalWaterConsumption = 0f;
            float totalStoredWater = 0f;
            float totalWaterCapacity = 0f;

            int networkO2SupportCapacity = 0; // Planetbase tipi kişi desteği kapasitesi

            // Bu ağdaki astronot sayısını bul (Binaların yakınındaki astronotlar)
            int astronautsInNetwork = 0;
            foreach (var astro in allAstronauts)
            {
                if (astro == null) continue;
                // Astronotun bastığı hücredeki binayı bul
                Vector3Int astroCell = tilemap.WorldToCell(astro.transform.position);
                var node = GridManager.Instance.GetNode(astroCell);
                if (node != null && node.placedObject != null)
                {
                    var pb = node.placedObject.GetComponentInParent<PlacedBuilding>();
                    if (pb != null && network.Contains(pb))
                    {
                        astronautsInNetwork++;
                    }
                }
            }

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

                totalStoredWater += pb.waterAmount;
                totalWaterCapacity += pb.waterCapacity;
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
                foreach (var pb in network)
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

            foreach (var pb in network)
            {
                if (pb == null || !pb.IsRealBuilding) continue;

                if (!pb.isExterior && pb.storesOxygen)
                {
                    pb.oxygenAmount = Mathf.Clamp(pb.oxygenAmount + o2ChangeRate * tickSeconds, 0f, 100f);
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
