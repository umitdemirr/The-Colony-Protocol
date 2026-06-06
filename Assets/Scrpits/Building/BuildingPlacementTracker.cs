using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Yerleştirilmiş binaları kaydeder; save/load ile geri yükler.
/// Tüm BuildingDefinition asset'lerini runtime'da otomatik bulur.
/// </summary>
public class BuildingPlacementTracker : MonoBehaviour
{
    public static BuildingPlacementTracker Instance { get; private set; }

    public GridManager gridManager;
    public Transform buildingParent;
    public Tilemap tilemap;

    [Tooltip("SaveId ile eşleşen BuildingDefinition asset'leri (otomatik doldurulur)")]
    public BuildingDefinition[] definitionRegistry;

    // Runtime'da tüm tanımları tutan önbellek (cache)
    private Dictionary<string, BuildingDefinition> _defCache;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Sadece bileşeni sil, GameObject'i silme!
            return;
        }
        Instance = this;
    }

    void Start()
    {
        BuildDefinitionCache();
    }

    /// <summary>
    /// Projede bulunan tüm BuildingDefinition asset'lerini tarayarak
    /// bir sözlüğe (Dictionary) ekler. Inspector'da elle eklenmemiş
    /// tanımları da otomatik olarak bulur.
    /// </summary>
    void BuildDefinitionCache()
    {
        _defCache = new Dictionary<string, BuildingDefinition>();

        // 1. Inspector'dan eklenen tanımları ekle
        if (definitionRegistry != null)
        {
            foreach (var d in definitionRegistry)
            {
                if (d == null) continue;
                string id = d.GetSaveId();
                if (!_defCache.ContainsKey(id))
                    _defCache[id] = d;
            }
        }

        // 2. Projede yüklü olan TÜM BuildingDefinition asset'lerini tara
        var allDefs = Resources.FindObjectsOfTypeAll<BuildingDefinition>();
        foreach (var d in allDefs)
        {
            if (d == null) continue;
            string id = d.GetSaveId();
            if (!_defCache.ContainsKey(id))
                _defCache[id] = d;
        }

        // 3. Inspector registry'sini de güncelle (eksik olanları ekle)
        var updatedList = new List<BuildingDefinition>(_defCache.Values);
        definitionRegistry = updatedList.ToArray();

        Debug.Log($"[BuildingPlacementTracker] Tanım önbelleği oluşturuldu: {_defCache.Count} bina tanımı bulundu.");
        foreach (var kv in _defCache)
            Debug.Log($"  → Tanım: '{kv.Key}' | Prefab: {(kv.Value.buildingPrefab != null ? kv.Value.buildingPrefab.name : "YOK!")}");
    }

    public void RegisterPlaced(GameObject obj, BuildingDefinition def)
    {
        var pb = obj.GetComponent<PlacedBuilding>();
        if (pb == null) pb = obj.AddComponent<PlacedBuilding>();
        pb.definitionId = def.GetSaveId();
        pb.energyNeed = Mathf.Max(0, def.energyNeed);
        pb.energyProducerType = def.energyProducerType;
        pb.energyProductionBase = Mathf.Max(0f, def.energyProductionBase);
        pb.powerCollectorCapacity = Mathf.Max(0, def.powerCollectorCapacity);
        pb.energyRamp01 = 0f;

        // Yeni statlar
        pb.maxHealth = def.maxHealth > 0 ? def.maxHealth : 100;
        pb.currentHealth = pb.maxHealth;

        pb.isExterior = def.isExterior;
        pb.isOxygenProducer = def.isOxygenProducer;
        pb.storesOxygen = def.storesOxygen;
        pb.oxygenCapacity = 100f; // Planetbase tipi sabit
        pb.oxygenAmount = pb.storesOxygen ? 100f : 0f;
        pb.oxygenSupportCapacity = def.oxygenSupportCapacity;
        pb.oxygenProductionCapacity = def.oxygenSupportCapacity;
        pb.oxygenProductionCurrent = 0f;

        pb.isWaterProducer = def.isWaterProducer;
        pb.waterProductionRate = Mathf.Max(0f, def.waterProductionRate);
        pb.requiresWater = def.requiresWater;
        pb.waterConsumptionRate = Mathf.Max(0f, def.waterConsumptionRate);
        pb.storesWater = def.storesWater;
        pb.waterCapacity = Mathf.Max(0f, def.waterCapacity);
        pb.waterAmount = pb.storesWater ? pb.waterCapacity : 0f;

        pb.storedEnergy = 0f;
        pb.efficiency01 = 1f;
    }

    public List<PlacedBuildingData> CollectSaveData()
    {
        var list = new List<PlacedBuildingData>();
        if (buildingParent == null)
        {
            Debug.LogWarning("[BuildingPlacementTracker] CollectSaveData: buildingParent null!");
            return list;
        }

        foreach (Transform t in buildingParent)
        {
            var pb = t.GetComponent<PlacedBuilding>();
            if (pb == null) continue;
            if (!pb.IsRealBuilding) continue; // Yol/boru parçalarını atla

            var d = new PlacedBuildingData
            {
                definitionId = pb.definitionId,
                energyNeed = Mathf.Max(0, pb.energyNeed),
                energyProducerType = (int)pb.energyProducerType,
                energyProductionBase = Mathf.Max(0f, pb.energyProductionBase),
                powerCollectorCapacity = Mathf.Max(0, pb.powerCollectorCapacity),
                posX = t.position.x,
                posY = t.position.y,
                posZ = t.position.z,
                rotZ = t.eulerAngles.z,

                maxHealth = Mathf.Max(1, pb.maxHealth),
                currentHealth = Mathf.Clamp(pb.currentHealth, 0, Mathf.Max(1, pb.maxHealth)),

                isOxygenProducer = pb.isOxygenProducer,
                oxygenAmount = Mathf.Max(0f, pb.oxygenAmount),
                oxygenCapacity = Mathf.Max(0f, pb.oxygenCapacity),
                oxygenProductionCurrent = Mathf.Max(0f, pb.oxygenProductionCurrent),
                oxygenProductionCapacity = Mathf.Max(0f, pb.oxygenProductionCapacity),

                // Yeni statlar
                waterAmount = Mathf.Max(0f, pb.waterAmount),
                waterCapacity = Mathf.Max(0f, pb.waterCapacity),
                storedEnergy = Mathf.Max(0f, pb.storedEnergy),
                efficiency01 = Mathf.Clamp01(pb.efficiency01)
            };
            list.Add(d);
        }

        Debug.Log($"[BuildingPlacementTracker] CollectSaveData: {list.Count} bina kaydedildi.");
        return list;
    }

    public void LoadFromSaveData(List<PlacedBuildingData> list)
    {
        Debug.Log($"[BuildingPlacementTracker] LoadFromSaveData başladı. Liste sayısı: {(list != null ? list.Count : -1)}");
        if (list == null) return;

        if (buildingParent == null || gridManager == null)
        {
            Debug.LogError($"[BuildingPlacementTracker] Yükleme başarısız: buildingParent={buildingParent != null}, gridManager={gridManager != null}");
            return;
        }

        // Tanım cache'i henüz oluşturulmadıysa hemen oluştur
        if (_defCache == null || _defCache.Count == 0)
            BuildDefinitionCache();

        // Eski binaları silerken GridOccupier2D engellerini düzgünce kaldır.
        int deletedCount = 0;
        var toDestroy = new List<GameObject>();
        foreach (Transform t in buildingParent)
        {
            var occupier = t.GetComponentInChildren<GridOccupier2D>(true);
            if (occupier != null)
            {
                occupier.autoOccupy = false;
                occupier.Release();
            }
            t.gameObject.SetActive(false); // Anında deaktif et ki FindObjectsOfType aramasından gizlensin!
            toDestroy.Add(t.gameObject);
            deletedCount++;
        }
        foreach (var go in toDestroy)
            Destroy(go);

        Debug.Log($"[BuildingPlacementTracker] {deletedCount} eski bina silindi.");

        // Yeni binaları yerleştirmeden önce grid'in geri kalanını da temizle
        gridManager.ResetOccupancy();

        int loadedCount = 0;
        int failedCount = 0;
        foreach (var d in list)
        {
            var def = GetDefinition(d.definitionId);
            if (def == null)
            {
                Debug.LogError($"[BuildingPlacementTracker] HATA: '{d.definitionId}' bina tanımı bulunamadı! Mevcut tanımlar: {string.Join(", ", _defCache.Keys)}");
                failedCount++;
                continue;
            }
            if (def.buildingPrefab == null)
            {
                Debug.LogError($"[BuildingPlacementTracker] HATA: '{d.definitionId}' binasının prefab'ı atanmamış!");
                failedCount++;
                continue;
            }

            var pos = new Vector3(d.posX, d.posY, d.posZ);
            var rot = Quaternion.Euler(0f, 0f, d.rotZ);
            var obj = Instantiate(def.buildingPrefab, pos, rot, buildingParent);

            var pb = obj.GetComponent<PlacedBuilding>();
            if (pb == null) pb = obj.AddComponent<PlacedBuilding>();
            pb.definitionId = d.definitionId;
            pb.energyNeed = d.energyNeed > 0 ? d.energyNeed : Mathf.Max(0, def.energyNeed);
            pb.energyProducerType = d.energyProducerType >= 0
                ? (BuildingDefinition.EnergyProducerType)d.energyProducerType
                : def.energyProducerType;
            pb.energyProductionBase = d.energyProductionBase > 0f ? d.energyProductionBase : Mathf.Max(0f, def.energyProductionBase);
            pb.powerCollectorCapacity = d.powerCollectorCapacity > 0 ? d.powerCollectorCapacity : Mathf.Max(0, def.powerCollectorCapacity);
            pb.energyRamp01 = 1f;

            pb.maxHealth = d.maxHealth > 0 ? d.maxHealth : pb.maxHealth;
            pb.currentHealth = Mathf.Clamp(d.currentHealth, 0, Mathf.Max(1, pb.maxHealth));
            
            // Yeni ve eski statların yüklenmesi
            pb.isExterior = def.isExterior;
            pb.isOxygenProducer = d.isOxygenProducer;
            pb.storesOxygen = def.storesOxygen;
            pb.oxygenAmount = Mathf.Max(0f, d.oxygenAmount);
            pb.oxygenCapacity = 100f; // Planetbase tipi sabit
            pb.oxygenSupportCapacity = def.oxygenSupportCapacity;
            pb.oxygenProductionCapacity = def.oxygenSupportCapacity;
            pb.oxygenProductionCurrent = Mathf.Max(0f, d.oxygenProductionCurrent);

            pb.isWaterProducer = def.isWaterProducer;
            pb.waterProductionRate = Mathf.Max(0f, def.waterProductionRate);
            pb.requiresWater = def.requiresWater;
            pb.waterConsumptionRate = Mathf.Max(0f, def.waterConsumptionRate);
            pb.storesWater = def.storesWater;
            pb.waterCapacity = Mathf.Max(0f, def.waterCapacity);
            pb.waterAmount = d.waterCapacity > 0f ? d.waterAmount : (pb.storesWater ? pb.waterCapacity : 0f);

            pb.storedEnergy = d.storedEnergy;
            pb.efficiency01 = d.efficiency01;

            var occ = obj.GetComponentInChildren<GridOccupier2D>(true);
            if (occ != null)
            {
                occ.autoOccupy = false;
                occ.Occupy(gridManager);
            }
            loadedCount++;
        }

        if (failedCount > 0)
            Debug.LogWarning($"[BuildingPlacementTracker] Yükleme tamamlandı: {loadedCount} başarılı, {failedCount} başarısız.");
        else
            Debug.Log($"[BuildingPlacementTracker] Yükleme tamamlandı: {loadedCount} bina başarıyla yerleştirildi.");
    }

    public BuildingDefinition GetDefinition(string saveId)
    {
        if (string.IsNullOrEmpty(saveId)) return null;

        // Önce cache'den bak
        if (_defCache != null && _defCache.TryGetValue(saveId, out var cached))
            return cached;

        // Cache yoksa veya bulunamadıysa Inspector registry'den bak
        if (definitionRegistry != null)
        {
            foreach (var d in definitionRegistry)
                if (d != null && d.GetSaveId() == saveId)
                    return d;
        }

        return null;
    }
}
