using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm oyun verisinin save/load için tek yapısı.
/// </summary>
[Serializable]
public class GameSaveData
{
    public string version = "1";
    public ResourceInventory.ResourceSaveData resources;
    public List<PlacedBuildingData> buildings = new List<PlacedBuildingData>();
    public List<NpcSaveData> npcs = new List<NpcSaveData>();
    public DayNightSaveData dayNight;
}

[Serializable]
public class PlacedBuildingData
{
    public string definitionId;
    public float posX, posY, posZ;
    public float rotZ;
}

[Serializable]
public class NpcSaveData
{
    public string id;
    public float posX, posY;
}
