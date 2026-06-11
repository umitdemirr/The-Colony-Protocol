using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İnşaat işlerini yönetir ve boşta olan uygun NPC'lere (Astronot) görev atar.
/// </summary>
public class ConstructionManager : MonoBehaviour
{
    private static ConstructionManager _instance;
    private static bool _isCreating = false;
    private static bool _isShuttingDown = false;

    public static ConstructionManager Instance
    {
        get
        {
            if (_isShuttingDown)
            {
                return null;
            }
            if (_instance == null && !_isCreating)
            {
                _isCreating = true;
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<ConstructionManager>();
#else
                _instance = FindObjectOfType<ConstructionManager>();
#endif
                if (_instance == null)
                {
                    GameObject go = new GameObject("ConstructionManager");
                    _instance = go.AddComponent<ConstructionManager>();
                }
                _isCreating = false;
            }
            return _instance;
        }
    }

    public List<GhostBuilding> pendingBuildings = new List<GhostBuilding>();
    public List<Astronaut> astronauts = new List<Astronaut>();

    void Awake()
    {
        _isShuttingDown = false;
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Ignore physics collisions between NPC layer (layer 3) and Default layer (layer 0)
        // so that astronauts can walk right up to/into buildings without getting blocked by physics colliders.
        Physics2D.IgnoreLayerCollision(3, 0, true);

        // Also ignore NPC-to-NPC collision to prevent characters from pushing each other or getting stuck
        Physics2D.IgnoreLayerCollision(3, 3, true);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _isShuttingDown = true;
            _instance = null;
        }
    }

    void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    void Start()
    {
        // Sahnede önceden yerleştirilmiş tüm NPC'leri otomatik bul ve Astronaut bileşenlerini ekle
        var movers = FindObjectsByType<NpcMoverAStar2D>(FindObjectsSortMode.None);
        foreach (var mover in movers)
        {
            var ast = mover.GetComponent<Astronaut>();
            if (ast == null)
            {
                ast = mover.gameObject.AddComponent<Astronaut>();
                // Sahnede var olanlara rastgele isim ve rol tanımlayalım
                ast.role = (NpcRole)Random.Range(0, 3);
            }
        }
    }

    public void RegisterGhostBuilding(GhostBuilding gb)
    {
        if (!pendingBuildings.Contains(gb))
        {
            pendingBuildings.Add(gb);
        }
    }

    public void UnregisterGhostBuilding(GhostBuilding gb)
    {
        pendingBuildings.Remove(gb);
    }

    public void RegisterAstronaut(Astronaut ast)
    {
        if (!astronauts.Contains(ast))
        {
            astronauts.Add(ast);
            Debug.Log($"[ConstructionManager] Astronot başarıyla kaydedildi: '{ast.astronautName}' | Rol: {ast.role}");
        }
    }

    public void UnregisterAstronaut(Astronaut ast)
    {
        astronauts.Remove(ast);
    }

    private float _scanTimer = 0f;

    void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= 1.5f)
        {
            _scanTimer = 0f;
            EnsureAllNpcsHaveAstronautComponent();
        }
        AssignTasksToAstronauts();
    }

    private void EnsureAllNpcsHaveAstronautComponent()
    {
        var movers = FindObjectsByType<NpcMoverAStar2D>(FindObjectsSortMode.None);
        foreach (var mover in movers)
        {
            if (mover != null)
            {
                var ast = mover.GetComponent<Astronaut>();
                if (ast == null)
                {
                    ast = mover.gameObject.AddComponent<Astronaut>();
                    ast.role = (NpcRole)Random.Range(0, 3);
                }
                else
                {
                    // Zaten Astronaut bileşeni var, listede kayıtlı değilse listeye ekle
                    if (!astronauts.Contains(ast))
                    {
                        RegisterAstronaut(ast);
                    }
                }
            }
        }
    }

    private void AssignTasksToAstronauts()
    {
        if (pendingBuildings.Count == 0 || astronauts.Count == 0) return;

        foreach (var building in pendingBuildings)
        {
            if (building == null || building.IsConstructed) continue;

            // Bu bina türü için tercih edilen profesyonel rolü belirle
            NpcRole preferredRole = GetPreferredRoleForBuilding(building.definition);

            // Gerekli kaynakları döngüyle kontrol et
            foreach (var req in building.requiredResources)
            {
                if (building.NeedsResource(req.type, out int amountNeeded))
                {
                    for (int i = 0; i < amountNeeded; i++)
                    {
                        // En uygun boşta olan astronotu bul
                        Astronaut candidate = FindAvailableAstronaut(preferredRole);
                        if (candidate != null)
                        {
                            candidate.AssignTask(building, req.type);
                        }
                        else
                        {
                            // Şu an boşta astronot yok
                            break;
                        }
                    }
                }
            }
        }
    }

    private NpcRole GetPreferredRoleForBuilding(BuildingDefinition def)
    {
        string name = def.displayName.ToLowerInvariant();
        
        // Yaşam destek / Biyoloji binaları
        if (name.Contains("canteen") || name.Contains("medic") || name.Contains("oxygen") || name.Contains("extractor") || name.Contains("tank"))
        {
            return NpcRole.Biologist;
        }
        // Mühendislik / Enerji binaları
        else if (name.Contains("power") || name.Contains("solar") || name.Contains("wind") || name.Contains("plant") || name.Contains("mine"))
        {
            return NpcRole.Engineer;
        }
        
        // Genel koridorlar, yatakhane vb. binalar
        return NpcRole.Worker;
    }

    private Astronaut FindAvailableAstronaut(NpcRole preferredRole)
    {
        // 1. Tercih edilen rolden uygun olanı ara
        foreach (var ast in astronauts)
        {
            if (ast != null && ast.state == AstronautState.Idle && ast.role == preferredRole)
            {
                return ast;
            }
        }

        // 2. Eğer tercih edilen rol 'Worker' değilse, yedek olarak genel 'Worker' ara
        if (preferredRole != NpcRole.Worker)
        {
            foreach (var ast in astronauts)
            {
                if (ast != null && ast.state == AstronautState.Idle && ast.role == NpcRole.Worker)
                {
                    return ast;
                }
            }
        }

        // 3. Fallback: Boşta olan herhangi bir astronotu seç
        foreach (var ast in astronauts)
        {
            if (ast != null && ast.state == AstronautState.Idle)
            {
                return ast;
            }
        }

        return null;
    }

    /// <summary>
    /// Kaynakların alınacağı depo konumunu bulur.
    /// Kaynak türüne göre sahnede en yakın uzmanlaşmış binayı (örneğin Metal için Mine) arar.
    /// </summary>
    public Vector3 FindStoragePosition(ResourceType resourceType, Vector3 requesterPos)
    {
        var placed = FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
        PlacedBuilding bestBuilding = null;
        float bestDist = float.MaxValue;

        foreach (var pb in placed)
        {
            if (pb == null) continue;

            bool isMatch = false;
            string defId = pb.definitionId.ToLowerInvariant();
            string goName = pb.gameObject.name.ToLowerInvariant();

            // Kaynak türüne göre en uygun üretici/depolama binasını eşleştir
            switch (resourceType)
            {
                case ResourceType.Metal:
                    isMatch = defId.Contains("mine") || goName.Contains("mine") || 
                              defId.Contains("plant") || goName.Contains("plant") ||
                              defId.Contains("processing") || goName.Contains("processing");
                    break;
                case ResourceType.Biyoplastik:
                case ResourceType.Spares:
                    isMatch = defId.Contains("plant") || goName.Contains("plant") ||
                              defId.Contains("processing") || goName.Contains("processing");
                    break;
                case ResourceType.Meal:
                    isMatch = defId.Contains("canteen") || goName.Contains("canteen") ||
                              defId.Contains("extractor") || goName.Contains("extractor");
                    break;
                case ResourceType.MedicalSupplies:
                    isMatch = defId.Contains("medic") || goName.Contains("medic");
                    break;
                default:
                    isMatch = false;
                    break;
            }

            // Ayrıca roket her zaman genel bir depodur
            if (!isMatch)
            {
                isMatch = defId.Contains("rocket") || goName.Contains("rocket") ||
                          defId.Contains("harbor") || goName.Contains("harbor") ||
                          defId.Contains("depot") || goName.Contains("depot");
            }

            if (isMatch)
            {
                float dist = Vector3.Distance(pb.transform.position, requesterPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestBuilding = pb;
                }
            }
        }

        // Eğer en uygun uzmanlaşmış bina bulunduysa, onun konumunu döndür
        if (bestBuilding != null)
        {
            Vector3 pos = bestBuilding.transform.position;
            if (bestBuilding.gameObject.name.Contains("Rocket"))
            {
                pos.y -= 3.5f; // Roket tabanı düzeltmesi
            }
            return pos;
        }

        // Bulunamadıysa sahnede "Rocket" kelimesi geçen herhangi bir objeyi ara (statik objeler için)
        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go != null && go.name.Contains("Rocket"))
            {
                Vector3 pos = go.transform.position;
                pos.y -= 3.5f;
                return pos;
            }
        }

        // O da yoksa en yakın herhangi bir binayı seç (roketi kaldırmışlarsa en yakın bina depo görevi görür)
        PlacedBuilding nearestAny = null;
        float nearestDist = float.MaxValue;
        foreach (var pb in placed)
        {
            if (pb == null) continue;
            float dist = Vector3.Distance(pb.transform.position, requesterPos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestAny = pb;
            }
        }

        if (nearestAny != null)
        {
            return nearestAny.transform.position;
        }

        // Hiç bina yoksa, requester'ın kendi konumu (yerinde anında kaynak üretimi)
        return requesterPos;
    }

    public Sprite GetResourceSprite(ResourceType type)
    {
        var cargoUI = FindFirstObjectByType<RocketCargoGridContent>();
        if (cargoUI != null)
        {
            switch (type)
            {
                case ResourceType.Metal: return cargoUI.IconMetal;
                case ResourceType.Biyoplastik: return cargoUI.IconBiyoplastik;
                case ResourceType.Spares: return cargoUI.IconSpares;
                case ResourceType.Meal: return cargoUI.IconMeal;
                case ResourceType.MedicalSupplies: return cargoUI.IconMedicalSupplies;
            }
        }
        return null;
    }

    /// <summary>
    /// Bina sınırlarının dış sınırındaki en yakın, yürünebilir ve boş olan hücreyi bulur.
    /// Astronotların binaların tam merkezine yürüyüp içlerinde sıkışmalarını önler!
    /// </summary>
    public Vector3Int FindConstructionAccessCell(GhostBuilding building, Vector3 startPos)
    {
        if (building == null || building.gridManager == null || building.tilemap == null)
            return Vector3Int.zero;

        Vector3Int startCell = building.tilemap.WorldToCell(startPos);
        Vector3Int centerCell = building.tilemap.WorldToCell(building.targetPosition);

        // Binaların işgal ettiği tüm hücreleri (footprint) topla
        HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        GridOccupier2D occ = building.GetComponentInChildren<GridOccupier2D>(true);
        if (occ != null)
        {
            occupiedCells = occ.ComputeOccupiedCells(building.gridManager);
        }
        
        // Eğer footprint boşsa, sadece merkez hücreyi ekle
        if (occupiedCells.Count == 0)
        {
            occupiedCells.Add(centerCell);
        }

        // Binaların etrafındaki tüm komşu (adjacent) hücreleri bul
        HashSet<Vector3Int> neighbors = new HashSet<Vector3Int>();
        foreach (var cell in occupiedCells)
        {
            neighbors.Add(cell + Vector3Int.up);
            neighbors.Add(cell + Vector3Int.down);
            neighbors.Add(cell + Vector3Int.left);
            neighbors.Add(cell + Vector3Int.right);
        }

        // Kendi footprint hücrelerini komşu listesinden çıkart (sadece dış sınır kalacak)
        foreach (var cell in occupiedCells)
        {
            neighbors.Remove(cell);
        }

        // Sınır hücreleri arasından başlangıç konumuna en yakın, yürünebilir ve boş olanı bul
        Vector3Int bestCell = Vector3Int.zero;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var cell in neighbors)
        {
            GridNode node = building.gridManager.GetNode(cell);
            if (node != null && node.isWalkable && !node.isOccupied)
            {
                float dist = Vector3.Distance(building.gridManager.GetCellCenterWorld(cell), startPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCell = cell;
                    found = true;
                }
            }
        }

        // Eğer uygun dış sınır hücresi bulunduysa onu döndür
        if (found)
        {
            return bestCell;
        }

        // Fallback: Bulunamazsa en yakın herhangi bir yürünebilir hücreyi bul (maxRadius = 12)
        return PathfindingAStar2D.Instance != null 
            ? PathfindingAStar2D.Instance.FindNearestWalkableCell(centerCell, startCell, 12) 
            : centerCell;
    }

    /// <summary>
    /// Kayıt işlemi yapılmadan önce aktif olan tüm ghost binaları anında tamamlar.
    /// Bu sayede kayıt dosyalarının veri bütünlüğü %100 korunur.
    /// </summary>
    public void ForceCompleteAllActiveConstructions()
    {
        var activeGhosts = new List<GhostBuilding>(pendingBuildings);
        foreach (var gb in activeGhosts)
        {
            if (gb != null && !gb.IsConstructed)
            {
                gb.CompleteConstruction();
            }
        }
    }
}
