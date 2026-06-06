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
    public string saveTimestamp;
    public ResourceInventory.ResourceSaveData resources;
    public List<PlacedBuildingData> buildings = new List<PlacedBuildingData>();
    public List<NpcSaveData> npcs = new List<NpcSaveData>();
    public DayNightSaveData dayNight;
    public EnergySystemSaveData energySystem;
    public List<PlacedRoadData> roads = new List<PlacedRoadData>();
}

[Serializable]
public struct SerializableVector3Int
{
    public int x;
    public int y;
    public int z;

    public SerializableVector3Int(Vector3Int v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3Int ToVector3Int()
    {
        return new Vector3Int(x, y, z);
    }
}

[Serializable]
public class PlacedRoadData
{
    public List<SerializableVector3Int> pathCells = new List<SerializableVector3Int>();
    public bool usePipes;
}

[Serializable]
public class PlacedBuildingData
{
    public string definitionId;
    public int energyNeed;
    public int energyProducerType;
    public float energyProductionBase;
    public int powerCollectorCapacity;
    public float posX, posY, posZ;
    public float rotZ;

    // Panel gösterimi (sağlık/oksijen) save/load'u
    public int maxHealth;
    public int currentHealth;
    public bool isOxygenProducer;
    public float oxygenAmount;
    public float oxygenCapacity;
    public float oxygenProductionCurrent;
    public float oxygenProductionCapacity;
    public float waterAmount;
    public float waterCapacity;
    public float storedEnergy;
    public float efficiency01;
}

[Serializable]
public class EnergySystemSaveData
{
    public float storedEnergyKj;
}

[Serializable]
public class NpcSaveData
{
    public string id;
    public float posX, posY;
    public string astronautName;
    public int role;
}
