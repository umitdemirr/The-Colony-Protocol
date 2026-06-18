using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC pozisyonlarının save/load'u.
/// </summary>
public class NpcSaveRegistry : MonoBehaviour
{
    private static NpcSaveRegistry _instance;
    public static NpcSaveRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<NpcSaveRegistry>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("NpcSaveRegistry");
                    _instance = go.AddComponent<NpcSaveRegistry>();
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[NpcSaveRegistry] Sahnede birden fazla Instance bulundu! Eski olan eziliyor.");
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
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
            var sid = m.GetComponent<NpcSaveId>() ?? m.GetComponentInChildren<NpcSaveId>() ?? m.GetComponentInParent<NpcSaveId>();
            if (sid == null) sid = m.gameObject.AddComponent<NpcSaveId>();
            if (string.IsNullOrEmpty(sid.id)) sid.id = "npc_" + idx;
            
            var ast = m.GetComponent<Astronaut>() ?? m.GetComponentInChildren<Astronaut>() ?? m.GetComponentInParent<Astronaut>();
            var pos = m.transform.position;
            Debug.Log($"[NpcSaveRegistry] Saving NPC: id={sid.id}, name={ast?.astronautName}, pos=({pos.x}, {pos.y})");
            
            string pName = m.gameObject.name.Replace("(Clone)", "").Trim();
            if (pName.Equals("SuitOff", System.StringComparison.OrdinalIgnoreCase))
            {
                pName = "Astronout";
            }
            
            list.Add(new NpcSaveData
            {
                id = sid.id,
                posX = pos.x,
                posY = pos.y,
                astronautName = ast != null ? ast.astronautName : "",
                role = ast != null ? (int)ast.role : 0,
                prefabName = pName,
                health = ast != null ? ast.health : 100f,
                oxygen = ast != null ? ast.oxygen : 100f,
                food = ast != null ? ast.food : 100f,
                water = ast != null ? ast.water : 100f,
                happiness = ast != null ? ast.happiness : 100f,
                state = ast != null ? (int)ast.state : 0,
                carryingResource = ast != null ? (int)ast.carryingResource : 0,
                isCarrying = ast != null ? ast.isCarrying : false
            });
            idx++;
        }
        return list;
    }

    [Header("NPC Spawning Prefabs")]
    public GameObject workerPrefab;
    public GameObject biologistPrefab;
    public GameObject engineerPrefab;
    public GameObject medicalPrefab;
    [Tooltip("Kayıttan yüklerken eksik olan varsayılan NPC prefabı.")]
    public GameObject npcPrefab;

    private GameObject GetPrefabForRole(NpcRole role, string prefabName, GameObject fallbackSceneTemplate, Dictionary<NpcRole, GameObject> templatesByRole, Dictionary<string, GameObject> templatesByName)
    {
        GameObject prefab = null;

        if (!string.IsNullOrEmpty(prefabName) && prefabName.Equals("SuitOff", System.StringComparison.OrdinalIgnoreCase))
        {
            prefabName = "Astronout";
        }

#if UNITY_EDITOR
        string path = "";
        if (!string.IsNullOrEmpty(prefabName))
        {
            path = $"Assets/Prefabs/NPC/SuitOn-NPC/{prefabName}.prefab";
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        if (prefab == null)
        {
            switch (role)
            {
                case NpcRole.Worker:
                    path = "Assets/Prefabs/NPC/SuitOn-NPC/Astronout-Worker.prefab";
                    break;
                case NpcRole.Biologist:
                    path = "Assets/Prefabs/NPC/SuitOn-NPC/Astronout-Biologist.prefab";
                    break;
                case NpcRole.Engineer:
                    path = "Assets/Prefabs/NPC/SuitOn-NPC/Astronout-Engineer.prefab";
                    break;
                case NpcRole.Medical:
                    path = "Assets/Prefabs/NPC/SuitOn-NPC/Astronout-Medic.prefab";
                    break;
                default:
                    path = "Assets/Prefabs/NPC/SuitOn-NPC/Astronout.prefab";
                    break;
            }
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif

        if (prefab == null && !string.IsNullOrEmpty(prefabName))
        {
            string lowerName = prefabName.ToLowerInvariant();
            if (lowerName.Contains("worker")) prefab = workerPrefab;
            else if (lowerName.Contains("biologist")) prefab = biologistPrefab;
            else if (lowerName.Contains("engineer")) prefab = engineerPrefab;
            else if (lowerName.Contains("medic")) prefab = medicalPrefab;
            else if (lowerName == "astronout") prefab = npcPrefab;
        }

        if (prefab == null)
        {
            switch (role)
            {
                case NpcRole.Worker: prefab = workerPrefab; break;
                case NpcRole.Biologist: prefab = biologistPrefab; break;
                case NpcRole.Engineer: prefab = engineerPrefab; break;
                case NpcRole.Medical: prefab = medicalPrefab; break;
            }
        }

        if (prefab == null)
        {
            prefab = npcPrefab;
        }

        if (prefab == null && !string.IsNullOrEmpty(prefabName) && templatesByName != null && templatesByName.ContainsKey(prefabName))
        {
            prefab = templatesByName[prefabName];
        }

        if (prefab == null && templatesByRole != null && templatesByRole.ContainsKey(role))
        {
            prefab = templatesByRole[role];
        }

        if (prefab == null)
        {
            prefab = fallbackSceneTemplate;
        }

        return prefab;
    }

    public void LoadFromSaveData(List<NpcSaveData> list)
    {
        if (list == null) return;
        Debug.Log($"[NpcSaveRegistry] LoadFromSaveData called with {list.Count} NPCs.");

        var existingNpcs = FindObjectsOfType<NpcMoverAStar2D>();
        
        // Sahnede var olan NPC'leri rollerine ve isimlerine göre şablon olarak saklayalım
        Dictionary<NpcRole, GameObject> templatesByRole = new Dictionary<NpcRole, GameObject>();
        Dictionary<string, GameObject> templatesByName = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        GameObject genericTemplate = npcPrefab;

        foreach (var n in existingNpcs)
        {
            if (n == null) continue;
            var ast = n.GetComponent<Astronaut>() ?? n.GetComponentInChildren<Astronaut>() ?? n.GetComponentInParent<Astronaut>();
            string nameKey = n.gameObject.name.Replace("(Clone)", "").Trim();

            // SuitOff (kıyafetsiz) NPC'nin şablon veya fallback olarak kullanılmasını tamamen engelliyoruz
            if (nameKey.Equals("SuitOff", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!templatesByName.ContainsKey(nameKey))
            {
                templatesByName[nameKey] = n.gameObject;
            }

            if (ast != null && !templatesByRole.ContainsKey(ast.role))
            {
                templatesByRole[ast.role] = n.gameObject;
            }

            if (genericTemplate == null)
            {
                genericTemplate = n.gameObject;
            }
        }

        // Eğer genericTemplate hala null ise ve sahnede NPC varsa, SuitOff olmayan ilk NPC'yi seçmeye çalışalım
        if (genericTemplate == null && existingNpcs.Length > 0)
        {
            foreach (var n in existingNpcs)
            {
                if (n == null) continue;
                string nameKey = n.gameObject.name.Replace("(Clone)", "").Trim();
                if (!nameKey.Equals("SuitOff", System.StringComparison.OrdinalIgnoreCase))
                {
                    genericTemplate = n.gameObject;
                    break;
                }
            }
            // Eğer hepsi SuitOff ise mecbur ilkini seç
            if (genericTemplate == null)
            {
                genericTemplate = existingNpcs[0].gameObject;
            }
        }

        // Mevcut NPC'lerin referanslarını silinmek üzere kaydet ve deaktif et
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (var n in existingNpcs)
        {
            if (n != null)
            {
                n.gameObject.SetActive(false);
                toDestroy.Add(n.gameObject);
            }
        }

        foreach (var d in list)
        {
            NpcRole savedRole = (NpcRole)d.role;
            GameObject prefabToUse = GetPrefabForRole(savedRole, d.prefabName, genericTemplate, templatesByRole, templatesByName);

            if (prefabToUse != null)
            {
                Debug.Log($"[NpcSaveRegistry] Instantiating NPC: id={d.id}, name={d.astronautName}, role={savedRole}, using prefab={prefabToUse.name}");
                
                bool wasActive = prefabToUse.activeSelf;
                
                // Eğer prefab bir sahne objesiyse deaktif edip klonluyoruz, gerçek assets prefabı ise direkt klonluyoruz
                bool isSceneObject = prefabToUse.scene.IsValid();
                if (isSceneObject)
                {
                    prefabToUse.SetActive(false);
                }
                
                var go = Instantiate(prefabToUse, new Vector3(d.posX, d.posY, 0), Quaternion.identity);
                go.SetActive(true);
                
                if (isSceneObject)
                {
                    prefabToUse.SetActive(wasActive);
                }

                SetPositionSafely(go, new Vector3(d.posX, d.posY, 0));

                var sid = go.GetComponent<NpcSaveId>() ?? go.GetComponentInChildren<NpcSaveId>() ?? go.GetComponentInParent<NpcSaveId>();
                if (sid == null) sid = go.AddComponent<NpcSaveId>();
                sid.id = d.id;

                var ast = go.GetComponent<Astronaut>() ?? go.GetComponentInChildren<Astronaut>() ?? go.GetComponentInParent<Astronaut>();
                if (ast == null) ast = go.AddComponent<Astronaut>();
                
                ast.ResetStateForLoad();

                if (!string.IsNullOrEmpty(d.astronautName)) ast.astronautName = d.astronautName;
                ast.role = savedRole;
                ast.ApplySuitTint(); // Renklendirmeyi yüklenen yeni role göre uygula!

                // İstatistikleri ve durumları yükle
                bool isOldSave = (d.health == 0f && d.oxygen == 0f && d.food == 0f && d.water == 0f);
                ast.health = isOldSave ? 100f : d.health;
                ast.oxygen = isOldSave ? 100f : d.oxygen;
                ast.food = isOldSave ? 100f : d.food;
                ast.water = isOldSave ? 100f : d.water;
                ast.happiness = isOldSave ? 100f : d.happiness;
                ast.state = isOldSave ? AstronautState.Idle : (AstronautState)d.state;
                ast.carryingResource = isOldSave ? ResourceType.Energy : (ResourceType)d.carryingResource;
                ast.isCarrying = isOldSave ? false : d.isCarrying;
            }
            else
            {
                Debug.LogError($"[NpcSaveRegistry] Could not find any suitable prefab or template for role {savedRole}!");
            }
        }

        // Eski NPC'leri yok et
        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
    }

    void SetPositionSafely(GameObject go, Vector3 pos)
    {
        go.transform.position = pos;
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = pos;
            rb.velocity = Vector2.zero; // Kaymayı ve fizik uyuşmazlığını önlemek için hızı da sıfırlayalım
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
