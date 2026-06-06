using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Save/load koordinatörü. Dinamik (sınırsız) kayıt sistemi.
/// </summary>
public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    private static bool _isCreating = false;

    public static SaveManager Instance
    {
        get
        {
            if (_instance == null && !_isCreating)
            {
                _isCreating = true;
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<SaveManager>();
#else
                _instance = FindObjectOfType<SaveManager>();
#endif
                if (_instance == null)
                {
                    GameObject go = new GameObject("SaveManager");
                    _instance = go.AddComponent<SaveManager>();
                }
                _isCreating = false;
            }
            return _instance;
        }
    }

    const string SaveFileNamePrefix = "colony_save_";
    const string SaveFileNameExtension = ".json";
    const string AutoSaveFileName = "colony_autosave.json";

    public event Action<string> OnSaveComplete;
    public event Action<string> OnLoadComplete;
    public event Action<string> OnSaveError;
    public event Action<string> OnLoadError;

    [Header("Otomatik Kayıt")]
    [Tooltip("Otomatik kayıt aralığı (saniye). 0 = kapalı.")]
    [Min(0f)]
    [SerializeField] float autoSaveIntervalSeconds = 300f; // 5 dk
    float _autoSaveTimer;

    /// <summary>Sahne yüklendikten sonra açılacak dosya adı. Boşsa yükleme yapılmaz.</summary>
    public string PendingLoadFileName { get; set; } = "";

    public struct SaveFileInfo
    {
        public string fileName;
        public string timestamp;
        public int day;
        public DateTime actualTime;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        if (gameObject.name != "SaveManager")
        {
            GameObject go = new GameObject("SaveManager");
            var newMgr = go.AddComponent<SaveManager>();
            newMgr.autoSaveIntervalSeconds = this.autoSaveIntervalSeconds;
            _instance = newMgr;
            DontDestroyOnLoad(go);
            
            Destroy(this);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Update()
    {
        if (autoSaveIntervalSeconds <= 0f) return;
        if (SceneManager.GetActiveScene().name == "StartScene") return;
        if (Time.timeScale == 0f) return;

        _autoSaveTimer += Time.deltaTime;
        if (_autoSaveTimer >= autoSaveIntervalSeconds)
        {
            _autoSaveTimer = 0f;
            Save(AutoSaveFileName);
            Debug.Log("[SaveManager] Otomatik kayıt tamamlandı.");
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(PendingLoadFileName)) return;
        if (scene.name == "StartScene") return;

        StartCoroutine(LoadAfterFrame(PendingLoadFileName));
    }

    System.Collections.IEnumerator LoadAfterFrame(string fileName)
    {
        yield return null;
        yield return null;

        PendingLoadFileName = "";
        Load(fileName);
    }

    // ─────────────────────────── FILE PATHS & QUERIES ───────────────────────────

    public string GetFilePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public bool HasAnySave()
    {
        if (!Directory.Exists(Application.persistentDataPath)) return false;
        var files = Directory.GetFiles(Application.persistentDataPath, "*.json");
        return files.Length > 0;
    }

    public List<SaveFileInfo> GetAllSaves()
    {
        var list = new List<SaveFileInfo>();
        if (!Directory.Exists(Application.persistentDataPath)) return list;

        var files = Directory.GetFiles(Application.persistentDataPath, "*.json");
        foreach (var path in files)
        {
            string fileName = Path.GetFileName(path);
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<GameSaveData>(json);
                if (data != null)
                {
                    DateTime time = DateTime.MinValue;
                    if (!string.IsNullOrEmpty(data.saveTimestamp))
                        DateTime.TryParse(data.saveTimestamp, out time);

                    int day = data.dayNight != null ? data.dayNight.totalDays : 0;

                    list.Add(new SaveFileInfo
                    {
                        fileName = fileName,
                        timestamp = data.saveTimestamp ?? "Bilinmiyor",
                        day = day,
                        actualTime = time
                    });
                }
            }
            catch { /* Bozuk dosyaları atla */ }
        }

        // En yeniler en üstte olacak şekilde sırala
        return list.OrderByDescending(x => x.actualTime).ToList();
    }

    public SaveFileInfo? GetLatestSave()
    {
        var allSaves = GetAllSaves();
        if (allSaves.Count > 0) return allSaves[0];
        return null;
    }

    // ─────────────────────────── SAVE / LOAD ───────────────────────────

    public void SaveNew()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{SaveFileNamePrefix}{timestamp}{SaveFileNameExtension}";
        Save(fileName);
    }

    public void Save(string fileName)
    {
        try
        {
            if (ConstructionManager.Instance != null)
            {
                ConstructionManager.Instance.ForceCompleteAllActiveConstructions();
            }

            var data = CollectSaveData();
            data.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string json = JsonUtility.ToJson(data, true);
            string path = GetFilePath(fileName);
            File.WriteAllText(path, json);
            Debug.Log($"[SaveManager] Oyun kaydedildi: {path}");
            OnSaveComplete?.Invoke(fileName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Kayıt hatası: {ex.Message}");
            OnSaveError?.Invoke(ex.Message);
        }
    }

    public void Load(string fileName)
    {
        try
        {
            string path = GetFilePath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] Dosya bulunamadı: {fileName}");
                OnLoadError?.Invoke("Kayıt dosyası bulunamadı.");
                return;
            }

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null)
            {
                Debug.LogError($"[SaveManager] Veri okunamadı: {fileName}");
                OnLoadError?.Invoke("Kayıt verisi bozuk.");
                return;
            }

            ApplySaveData(data);
            Debug.Log($"[SaveManager] Oyun yüklendi: {fileName}");
            OnLoadComplete?.Invoke(fileName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Yükleme hatası: {ex.Message}");
            OnLoadError?.Invoke(ex.Message);
        }
    }

    public void DeleteSave(string fileName)
    {
        string path = GetFilePath(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveManager] Silindi: {fileName}");
        }
    }

    // ─────────────────────────── COLLECT / APPLY ───────────────────────────

    GameSaveData CollectSaveData()
    {
        var data = new GameSaveData();

        Debug.Log("[SaveManager] === KAYIT TOPLAMA BAŞLADI ===");

        if (ResourceManager.Instance != null)
        {
            data.resources = ResourceManager.Instance.Inventory.ToSaveData();
            Debug.Log($"[SaveManager] ✓ Kaynaklar kaydedildi.");
        }
        else Debug.LogWarning("[SaveManager] ✗ ResourceManager.Instance null! Kaynaklar kaydedilemedi.");

        if (BuildingPlacementTracker.Instance != null)
        {
            data.buildings = BuildingPlacementTracker.Instance.CollectSaveData();
            Debug.Log($"[SaveManager] ✓ Binalar kaydedildi: {data.buildings.Count} bina.");
        }
        else Debug.LogWarning("[SaveManager] ✗ BuildingPlacementTracker.Instance null! Binalar kaydedilemedi.");

        if (NpcSaveRegistry.Instance != null)
        {
            data.npcs = NpcSaveRegistry.Instance.CollectSaveData();
            Debug.Log($"[SaveManager] ✓ NPC'ler kaydedildi: {data.npcs.Count} npc.");
        }
        else Debug.LogWarning("[SaveManager] ✗ NpcSaveRegistry.Instance null! NPC'ler kaydedilemedi.");

        if (DayNightCycleController.Instance != null)
        {
            data.dayNight = DayNightCycleController.Instance.ToSaveData();
            Debug.Log($"[SaveManager] ✓ Gece/Gündüz kaydedildi: Gün {data.dayNight.totalDays}, İlerleme {data.dayNight.dayProgress:F2}");
        }
        else Debug.LogWarning("[SaveManager] ✗ DayNightCycleController.Instance null! Gece/Gündüz kaydedilemedi.");

        if (EnergyProductionSystem.Instance != null)
        {
            data.energySystem = EnergyProductionSystem.Instance.ToSaveData();
            Debug.Log($"[SaveManager] ✓ Enerji kaydedildi: {data.energySystem.storedEnergyKj} kJ");
        }
        else Debug.LogWarning("[SaveManager] ✗ EnergyProductionSystem.Instance null! Enerji kaydedilemedi.");

        if (ModularLShapeRoadGenerator.Instance != null)
        {
            data.roads = new List<PlacedRoadData>();
            foreach (var r in ModularLShapeRoadGenerator.Instance.committedRoads)
            {
                if (r == null || r.pathCells == null) continue;
                var pr = new PlacedRoadData { usePipes = r.usePipes };
                foreach (var cell in r.pathCells)
                {
                    pr.pathCells.Add(new SerializableVector3Int(cell));
                }
                data.roads.Add(pr);
            }
            Debug.Log($"[SaveManager] ✓ Yollar kaydedildi: {data.roads.Count} yol şeridi.");
        }
        else Debug.LogWarning("[SaveManager] ✗ ModularLShapeRoadGenerator.Instance null! Yollar kaydedilemedi.");

        Debug.Log("[SaveManager] === KAYIT TOPLAMA BİTTİ ===");
        return data;
    }

    void ApplySaveData(GameSaveData data)
    {
        if (data == null) return;

        Debug.Log("[SaveManager] === VERİ YÜKLEME BAŞLADI ===");

        if (data.resources != null && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.LoadFromSaveData(data.resources);
            Debug.Log("[SaveManager] ✓ Kaynaklar yüklendi.");
        }
        else Debug.LogWarning($"[SaveManager] ✗ Kaynaklar yüklenemedi: resources={data.resources != null}, ResourceManager={ResourceManager.Instance != null}");

        if (BuildingPlacementTracker.Instance != null)
        {
            Debug.Log($"[SaveManager] Binalar yükleniyor: {(data.buildings != null ? data.buildings.Count : 0)} bina...");
            BuildingPlacementTracker.Instance.LoadFromSaveData(data.buildings);
        }
        else Debug.LogWarning("[SaveManager] ✗ BuildingPlacementTracker.Instance null! Binalar yüklenemedi.");

        if (NpcSaveRegistry.Instance != null && data.npcs != null)
        {
            NpcSaveRegistry.Instance.LoadFromSaveData(data.npcs);
            Debug.Log($"[SaveManager] ✓ NPC'ler yüklendi: {data.npcs.Count} npc.");
        }
        else Debug.LogWarning($"[SaveManager] ✗ NPC'ler yüklenemedi: npcs={data.npcs != null}, NpcSaveRegistry={NpcSaveRegistry.Instance != null}");

        if (DayNightCycleController.Instance != null && data.dayNight != null)
        {
            DayNightCycleController.Instance.LoadFromSaveData(data.dayNight);
            Debug.Log("[SaveManager] ✓ Gece/Gündüz yüklendi.");
        }
        else Debug.LogWarning($"[SaveManager] ✗ Gece/Gündüz yüklenemedi: dayNight={data.dayNight != null}, DayNight={DayNightCycleController.Instance != null}");

        if (EnergyProductionSystem.Instance != null && data.energySystem != null)
        {
            EnergyProductionSystem.Instance.LoadFromSaveData(data.energySystem);
            Debug.Log("[SaveManager] ✓ Enerji yüklendi.");
        }
        else Debug.LogWarning($"[SaveManager] ✗ Enerji yüklenemedi: energySystem={data.energySystem != null}, EnergySystem={EnergyProductionSystem.Instance != null}");

        if (ModularLShapeRoadGenerator.Instance != null)
        {
            StartCoroutine(LoadRoadsDeferred(data.roads));
        }
        else Debug.LogWarning("[SaveManager] ✗ ModularLShapeRoadGenerator.Instance null! Yollar yüklenemedi.");

        Debug.Log("[SaveManager] === VERİ YÜKLEME BİTTİ ===");
    }

    System.Collections.IEnumerator LoadRoadsDeferred(List<PlacedRoadData> roads)
    {
        // 1 frame bekle ki tüm binalar transform ve tilemap yapılarını tamamen güncelleyebilsin!
        yield return null;

        if (ModularLShapeRoadGenerator.Instance != null)
        {
            var savedRoads = new List<ModularLShapeRoadGenerator.CommittedRoadData>();
            if (roads != null)
            {
                foreach (var r in roads)
                {
                    if (r == null || r.pathCells == null) continue;
                    var rd = new ModularLShapeRoadGenerator.CommittedRoadData { usePipes = r.usePipes };
                    foreach (var cell in r.pathCells)
                    {
                        rd.pathCells.Add(cell.ToVector3Int());
                    }
                    savedRoads.Add(rd);
                }
            }
            Debug.Log($"[SaveManager] Yollar yükleniyor (1 frame gecikmeli): {savedRoads.Count} yol şeridi...");
            ModularLShapeRoadGenerator.Instance.LoadFromSaveData(savedRoads);
            Debug.Log("[SaveManager] ✓ Yollar yüklendi.");
        }
    }
}
