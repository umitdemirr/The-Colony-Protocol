using System;
using UnityEngine;

/// <summary>
/// Save/load koordinatörü. PlayerPrefs veya dosya kullanılabilir.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    const string SaveKey = "ColonySave";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Save()
    {
        var data = CollectSaveData();
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return;
        string json = PlayerPrefs.GetString(SaveKey);
        var data = JsonUtility.FromJson<GameSaveData>(json);
        ApplySaveData(data);
    }

    public bool HasSave() => PlayerPrefs.HasKey(SaveKey);

    GameSaveData CollectSaveData()
    {
        var data = new GameSaveData();

        if (ResourceManager.Instance != null)
            data.resources = ResourceManager.Instance.Inventory.ToSaveData();

        if (BuildingPlacementTracker.Instance != null)
            data.buildings = BuildingPlacementTracker.Instance.CollectSaveData();

        if (NpcSaveRegistry.Instance != null)
            data.npcs = NpcSaveRegistry.Instance.CollectSaveData();

        if (DayNightCycleController.Instance != null)
            data.dayNight = DayNightCycleController.Instance.ToSaveData();

        return data;
    }

    void ApplySaveData(GameSaveData data)
    {
        if (data == null) return;

        if (data.resources != null && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.LoadFromSaveData(data.resources);
        }

        if (BuildingPlacementTracker.Instance != null)
        {
            BuildingPlacementTracker.Instance.LoadFromSaveData(data.buildings);
        }

        if (NpcSaveRegistry.Instance != null && data.npcs != null)
        {
            NpcSaveRegistry.Instance.LoadFromSaveData(data.npcs);
        }

        if (DayNightCycleController.Instance != null && data.dayNight != null)
            DayNightCycleController.Instance.LoadFromSaveData(data.dayNight);
    }
}
