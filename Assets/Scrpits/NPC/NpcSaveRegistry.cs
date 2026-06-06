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
            Destroy(this); // Sadece bileşeni sil, paylaşılan GameObject'i silme!
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
            
            var ast = m.GetComponent<Astronaut>();
            list.Add(new NpcSaveData
            {
                id = sid.id,
                posX = m.transform.position.x,
                posY = m.transform.position.y,
                astronautName = ast != null ? ast.astronautName : "",
                role = ast != null ? (int)ast.role : 0
            });
            idx++;
        }
        return list;
    }

    [Header("NPC Spawning")]
    [Tooltip("Kayıttan yüklerken sahnede eksik NPC varsa bunu yaratır.")]
    public GameObject npcPrefab;

    public void LoadFromSaveData(List<NpcSaveData> list)
    {
        if (list == null) return;
        
        if (npcPrefab != null)
        {
            var existingNpcs = FindObjectsOfType<NpcMoverAStar2D>();
            foreach (var n in existingNpcs)
            {
                n.gameObject.SetActive(false); // Anında deaktif et!
                Destroy(n.gameObject);
            }
            
            foreach (var d in list)
            {
                var go = Instantiate(npcPrefab, new Vector3(d.posX, d.posY, 0), Quaternion.identity);
                var sid = go.GetComponent<NpcSaveId>();
                if (sid == null) sid = go.AddComponent<NpcSaveId>();
                sid.id = d.id;

                var ast = go.GetComponent<Astronaut>();
                if (ast == null) ast = go.AddComponent<Astronaut>();
                if (!string.IsNullOrEmpty(d.astronautName)) ast.astronautName = d.astronautName;
                ast.role = (NpcRole)d.role;
            }
        }
        else
        {
            // Prefab yoksa, eski mantıkla sadece var olanların yerini değiştir
            foreach (var d in list)
            {
                var sid = FindNpcSaveId(d.id);
                if (sid == null) continue;
                var pos = sid.transform.position;
                pos.x = d.posX;
                pos.y = d.posY;
                sid.transform.position = pos;

                var ast = sid.GetComponent<Astronaut>();
                if (ast == null) ast = sid.gameObject.AddComponent<Astronaut>();
                if (!string.IsNullOrEmpty(d.astronautName)) ast.astronautName = d.astronautName;
                ast.role = (NpcRole)d.role;
            }
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
