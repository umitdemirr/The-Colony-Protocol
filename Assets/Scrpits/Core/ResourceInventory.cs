using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kaynak stoğu. Save/load uyumlu.
/// </summary>
[Serializable]
public class ResourceInventory
{
    [SerializeField] List<ResourceEntry> _entries = new List<ResourceEntry>();

    public ResourceInventory()
    {
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            Set(t, 0);
    }

    public int Get(ResourceType type)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].type == type)
                return _entries[i].amount;
        return 0;
    }

    public void Set(ResourceType type, int amount)
    {
        amount = Mathf.Max(0, amount);
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].type == type)
            {
                _entries[i].amount = amount;
                return;
            }
        }
        _entries.Add(new ResourceEntry { type = type, amount = amount });
    }

    public void Add(ResourceType type, int amount)
    {
        Set(type, Get(type) + amount);
    }

    public bool TryRemove(ResourceType type, int amount)
    {
        int current = Get(type);
        if (current < amount) return false;
        Set(type, current - amount);
        return true;
    }

    public bool Has(ResourceType type, int amount) => Get(type) >= amount;

    /// <summary>
    /// Save için serializable veri.
    /// </summary>
    public ResourceSaveData ToSaveData()
    {
        var d = new ResourceSaveData();
        d.entries = new List<ResourceEntry>(_entries);
        return d;
    }

    public void LoadFromSaveData(ResourceSaveData data)
    {
        if (data?.entries == null) return;
        _entries.Clear();
        foreach (var e in data.entries)
            Set(e.type, Mathf.Max(0, e.amount));
    }

    [Serializable]
    public class ResourceEntry
    {
        public ResourceType type;
        public int amount;
    }

    [Serializable]
    public class ResourceSaveData
    {
        public List<ResourceEntry> entries;
    }
}
