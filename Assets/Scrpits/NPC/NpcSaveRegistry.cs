using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC pozisyonlarının save/load'u.
/// </summary>
public class NpcSaveRegistry : MonoBehaviour
{
    public static NpcSaveRegistry Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public int GetNpcCount()
    {
        return FindObjectsOfType<NpcMoverAStar2D>().Length;
    }

    public List<NpcSaveData> CollectSaveData()
    {
        var list = new List<NpcSaveData>();
        var movers = FindObjectsOfType<NpcMoverAStar2D>();
        int idx = 0;
        foreach (var m in movers)
        {
            var sid = m.GetComponent<NpcSaveId>();
            if (sid == null) sid = m.gameObject.AddComponent<NpcSaveId>();
            if (string.IsNullOrEmpty(sid.id)) sid.id = "npc_" + idx;
            list.Add(new NpcSaveData
            {
                id = sid.id,
                posX = m.transform.position.x,
                posY = m.transform.position.y
            });
            idx++;
        }
        return list;
    }

    public void LoadFromSaveData(List<NpcSaveData> list)
    {
        if (list == null) return;
        foreach (var d in list)
        {
            var sid = FindNpcSaveId(d.id);
            if (sid == null) continue;
            var pos = sid.transform.position;
            pos.x = d.posX;
            pos.y = d.posY;
            sid.transform.position = pos;
        }
    }

    NpcSaveId FindNpcSaveId(string id)
    {
        var all = FindObjectsOfType<NpcSaveId>();
        foreach (var s in all)
            if (s.id == id) return s;
        return null;
    }
}
