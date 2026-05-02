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
    public EnergySystemSaveData energySystem;
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
}
