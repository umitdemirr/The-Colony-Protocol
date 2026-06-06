using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Astronot (NPC) özelliklerini, rolünü ve taşımacılık görev mantığını yönetir.
/// </summary>
public class Astronaut : MonoBehaviour
{
    [Header("Astronot Bilgileri")]
    public string astronautName;
    public NpcRole role = NpcRole.Worker;

    [Header("İhtiyaçlar (Stats)")]
    [Range(0f, 100f)] public float health = 100f;
    [Range(0f, 100f)] public float oxygen = 100f;
    [Range(0f, 100f)] public float food = 100f;
    [Range(0f, 100f)] public float water = 100f;
    [Range(0f, 100f)] public float happiness = 100f;

    [Header("İhtiyaç Tüketim Hızları")]
    [Tooltip("Oksijenin saniyedeki temel azalma miktarı (Varsayılan: 0.35f, yakl. 4.7 dakika sürer)")]
    public float oxygenDecayRate = 0.35f;

    [Tooltip("Açlığın saniyedeki temel azalma miktarı (Varsayılan: 0.05f)")]
    public float foodDecayRate = 0.05f;

    [Tooltip("Susuzluğun saniyedeki temel azalma miktarı (Varsayılan: 0.08f)")]
    public float waterDecayRate = 0.08f;

    [Tooltip("Kıyafet koruması aktifken (oksijen > 0) açlık ve susuzluğun azalma hızını çarpan olarak düşürür (0 = hiç azalmaz, 0.1 = %10 hızında azalır)")]
    public float suitProtectionMultiplier = 0.1f;

    [Header("İş Durumu")]
    public AstronautState state = AstronautState.Idle;
    public GhostBuilding currentTask;
    public ResourceType carryingResource;
    public bool isCarrying = false;

    private NpcMoverAStar2D _mover;
    private NPCAnimator4Dir _animator;
    private Text _uiText;
    private Image _carryingIcon;
    private Canvas _worldCanvas;

    private static readonly string[] Names = {
        "Commander Shepard", "Dr. Brand", "Cooper", "Dr. Mann", "Mark Watney", 
        "Ellen Ripley", "Dave Bowman", "Alex Vance", "Sarah Connor", "Chris Hadfield"
    };

    private bool _initialized = false;

    void Awake()
    {
        InitializeIfNeeded();
    }

    void Start()
    {
        InitializeIfNeeded();

        // ConstructionManager'a kendini kaydet
        if (ConstructionManager.Instance != null)
        {
            Debug.Log($"[Astronaut] '{astronautName}' kendini ConstructionManager'a kaydediyor.");
            ConstructionManager.Instance.RegisterAstronaut(this);
        }
        else
        {
            Debug.LogWarning($"[Astronaut] '{astronautName}' ConstructionManager.Instance NULL olduğu için kendisini kaydedemedi!");
        }

        ApplySuitTint();

        // Tıklanabilirlik (Info Panel) ayarı
        var interactable = GetComponent<InfoCardInteractable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<InfoCardInteractable>();
        }
        interactable.bodyContentKey = "Npc";
        interactable.headerTitle = astronautName;
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) interactable.headerIcon = sr.sprite;
    }

    private void InitializeIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        _mover = GetComponent<NpcMoverAStar2D>();
        _animator = GetComponent<NPCAnimator4Dir>();

        if (string.IsNullOrEmpty(astronautName))
        {
            astronautName = Names[Random.Range(0, Names.Length)];
        }

        CreateWorldUI();
    }

    void OnDestroy()
    {
        if (ConstructionManager.Instance != null)
        {
            ConstructionManager.Instance.UnregisterAstronaut(this);
        }
    }

    /// <summary>
    /// Rolüne göre astronotun sprite rengini hafifçe tintler (renklendirir).
    /// </summary>
    private void ApplySuitTint()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            switch (role)
            {
                case NpcRole.Biologist:
                    sr.color = new Color(0.6f, 1f, 0.6f); // Yeşil
                    break;
                case NpcRole.Engineer:
                    sr.color = new Color(0.6f, 0.8f, 1f); // Mavi
                    break;
                case NpcRole.Worker:
                    sr.color = new Color(1f, 0.8f, 0.5f); // Sarı/Turuncu
                    break;
            }
        }
    }

    /// <summary>
    /// Astronotun başının üzerinde dinamik olarak dünya alanında (World Space Canvas)
    /// isim, rol ve güncel durum etiketi oluşturur.
    /// </summary>
    private void CreateWorldUI()
    {
        GameObject canvasGo = new GameObject("NpcWorldUI", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);

        _worldCanvas = canvasGo.GetComponent<Canvas>();
        _worldCanvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(200f, 50f);
        canvasRt.localScale = new Vector3(0.006f, 0.006f, 1f); // Küçük ölçek

        // Yazı etiketi
        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        
        _uiText = textGo.GetComponent<Text>();
        
        Font font = null;
        try
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch
        {
            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                // Fallback to null (Unity will use its default built-in font)
            }
        }
        if (font != null)
        {
            _uiText.font = font;
        }
        _uiText.fontSize = 16;
        _uiText.alignment = TextAnchor.MiddleCenter;
        _uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _uiText.verticalOverflow = VerticalWrapMode.Overflow;
        
        // Gölgelendirme (OutLine)
        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        // Taşınan Kaynak İkon Görseli
        GameObject iconGo = new GameObject("CarryingIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(canvasGo.transform, false);
        iconGo.transform.localPosition = new Vector3(0f, 25f, 0f);

        _carryingIcon = iconGo.GetComponent<Image>();
        _carryingIcon.rectTransform.sizeDelta = new Vector2(20f, 20f);
        _carryingIcon.enabled = false;
    }

    void Update()
    {
        UpdateNeedsAndStats();
        UpdateUI();
    }

    private void UpdateNeedsAndStats()
    {
        // 1. İhtiyaçların zamanla tükenmesi
        float currentFoodDecay = foodDecayRate;
        float currentWaterDecay = waterDecayRate;

        // Kıyafet koruması kontrolü: Oksijen varsa açlık ve susuzluk çok daha yavaş azalır
        if (oxygen > 0f)
        {
            currentFoodDecay *= suitProtectionMultiplier;
            currentWaterDecay *= suitProtectionMultiplier;
        }

        food = Mathf.Max(0f, food - currentFoodDecay * Time.deltaTime);
        water = Mathf.Max(0f, water - currentWaterDecay * Time.deltaTime);
        oxygen = Mathf.Max(0f, oxygen - oxygenDecayRate * Time.deltaTime);

        // 2. Çevredeki binalara göre yenilenme kontrolü
        bool nearCanteen = false;
        bool nearO2 = false;
        bool nearWater = false;
        bool nearMedic = false;

        var placedBuildings = FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
        foreach (var pb in placedBuildings)
        {
            if (pb == null) continue;
            float dist = Vector3.Distance(transform.position, pb.transform.position);
            if (dist <= 3.0f)
            {
                string defId = pb.definitionId.ToLowerInvariant();
                string goName = pb.gameObject.name.ToLowerInvariant();

                if (defId.Contains("canteen") || goName.Contains("canteen"))
                {
                    nearCanteen = true;
                }
                
                if (defId.Contains("extractor") || goName.Contains("extractor") || defId.Contains("water") || goName.Contains("water"))
                {
                    nearWater = true;
                }

                if (defId.Contains("medic") || goName.Contains("medic"))
                {
                    nearMedic = true;
                }

                // Binaların içinden oksijen alma: Eğer iç mekan binasındaysak ve binada oksijen varsa al
                if (pb != null && !pb.isExterior && pb.storesOxygen && pb.oxygenAmount > 0f)
                {
                    nearO2 = true;
                    // Astronot binadan oksijen tüketir
                    pb.oxygenAmount = Mathf.Max(0f, pb.oxygenAmount - oxygenDecayRate * Time.deltaTime);
                }
            }
        }

        // İhtiyaçları yenile
        if (nearCanteen) food = Mathf.Min(100f, food + 10f * Time.deltaTime);
        if (nearO2) oxygen = Mathf.Min(100f, oxygen + 15f * Time.deltaTime);
        if (nearWater || nearCanteen) water = Mathf.Min(100f, water + 12f * Time.deltaTime); // Yemekhaneden de su içebilirler

        // 3. Can (Health) Mekaniği (Can barı sadece oksijen bittikten sonra azalır)
        float damage = 0f;
        if (oxygen <= 0f)
        {
            damage += 3.0f; // Oksijensizlik hasarı
            if (food <= 0f) damage += 1.5f; // Açlık hasarı (sadece oksijen bittikten sonra etki eder)
            if (water <= 0f) damage += 2.0f; // Susuzluk hasarı (sadece oksijen bittikten sonra etki eder)
        }

        if (damage > 0f)
        {
            health = Mathf.Max(0f, health - damage * Time.deltaTime);
        }
        else if (nearMedic)
        {
            health = Mathf.Min(100f, health + 10f * Time.deltaTime);
        }

        // 4. Mutluluk hesaplama (Ortalama)
        happiness = Mathf.Clamp((oxygen + food + water) / 3f, 0f, 100f);

        // 5. Statlara bağlı etkiler (Hız yavaşlaması)
        if (_mover != null)
        {
            // Kritik ihtiyaçlardan biri çok düşükse hız düşer
            if (oxygen < 20f || food < 20f || water < 20f || health < 20f)
            {
                _mover.moveSpeed = 1.75f; // Yavaş yürüme
            }
            else
            {
                _mover.moveSpeed = 3.5f; // Normal hız
            }
        }
    }

    private void UpdateUI()
    {
        var interactable = GetComponent<InfoCardInteractable>();
        if (interactable != null)
        {
            interactable.headerTitle = astronautName;
            if (interactable.headerIcon == null)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null) interactable.headerIcon = sr.sprite;
            }
        }

        if (_uiText == null || _carryingIcon == null) return;

        string roleColor = "orange";
        if (role == NpcRole.Biologist) roleColor = "#55FF55";
        else if (role == NpcRole.Engineer) roleColor = "#55AAFF";

        string stateText = "Müsait";
        if (health <= 0f)
        {
            stateText = "<color=red>BAYILDI! (Revir Gerekli)</color>";
        }
        else if (state == AstronautState.MovingToStorage)
        {
            stateText = $"Depoya Gidiyor ({carryingResource})";
        }
        else if (state == AstronautState.MovingToConstructionSite)
        {
            stateText = $"{carryingResource} Götürüyor";
        }

        _uiText.text = $"{astronautName}\n<color={roleColor}>[{role}]</color> - {stateText}";

        // Taşıdığı kaynak görselini göster
        if (isCarrying)
        {
            Sprite rSprite = ConstructionManager.Instance != null ? ConstructionManager.Instance.GetResourceSprite(carryingResource) : null;
            if (rSprite != null)
            {
                _carryingIcon.sprite = rSprite;
                _carryingIcon.enabled = true;
            }
            else
            {
                _carryingIcon.enabled = false;
            }
        }
        else
        {
            _carryingIcon.enabled = false;
        }
    }

    public bool AssignTask(GhostBuilding building, ResourceType resource)
    {
        if (state != AstronautState.Idle || health <= 0f) return false;

        currentTask = building;
        carryingResource = resource;
        state = AstronautState.MovingToStorage;

        // İnşaat alanında kaynağı bu astronota rezerve et
        building.AssignResource(resource, 1);

        // Depo hücresini bul (Meslek/kaynak bazlı en yakın depo!)
        Vector3 storagePos = ConstructionManager.Instance != null 
            ? ConstructionManager.Instance.FindStoragePosition(resource, transform.position) 
            : Vector3.zero;
        
        var tilemap = PathfindingAStar2D.Instance != null ? PathfindingAStar2D.Instance.tilemap : _mover.tilemap;
        Vector3Int storageCell = tilemap != null ? tilemap.WorldToCell(storagePos) : Vector3Int.zero;

        Debug.Log($"[Astronaut] '{astronautName}' görevi aldı: '{resource}' -> '{building.definition.displayName}'. Hedef Depo konumu: {storagePos} (hücre: {storageCell})");

        if (!_mover.MoveToCell(storageCell))
        {
            Debug.LogWarning($"[Astronaut] '{astronautName}' depoya ({storageCell}) yol bulamadığı için görev iptal edildi!");
            CancelTask();
            return false;
        }

        return true;
    }

    private void CancelTask()
    {
        if (currentTask != null)
        {
            currentTask.AssignResource(carryingResource, -1);
        }
        currentTask = null;
        carryingResource = ResourceType.Energy;
        state = AstronautState.Idle;
        isCarrying = false;
    }

    // NpcMoverAStar2D hedefe ulaştığında tetiklenir
    void OnMovementDestinationReached()
    {
        Debug.Log($"[Astronaut] '{astronautName}' hedefe ulaştı tetiklendi. Durum: {state}");

        if (state == AstronautState.MovingToStorage)
        {
            // Depoya ulaştık mı kontrol et (Mover'ın hedefe ulaşıp ulaşmadığına bak)
            if (!_mover.DidReachDestination)
            {
                Debug.LogWarning($"[Astronaut] '{astronautName}' depoya giden yolda engellendi! Görev iptal ediliyor.");
                CancelTask();
                return;
            }

            // Depoya geldik, kaynağı yüklendik
            isCarrying = true;
            state = AstronautState.MovingToConstructionSite;
            Debug.Log($"[Astronaut] '{astronautName}' depodan '{carryingResource}' kaynağını başarıyla teslim aldı. Şimdi inşaat alanına ({currentTask.targetPosition}) gidiyor.");

            if (currentTask != null)
            {
                // Dış sınırda durup inşa etmesi için en yakın komşu/sınır hücresini al
                Vector3Int buildCell = ConstructionManager.Instance != null 
                    ? ConstructionManager.Instance.FindConstructionAccessCell(currentTask, transform.position)
                    : (PathfindingAStar2D.Instance != null ? PathfindingAStar2D.Instance.tilemap.WorldToCell(currentTask.targetPosition) : Vector3Int.zero);

                if (!_mover.MoveToCell(buildCell))
                {
                    Debug.LogWarning($"[Astronaut] '{astronautName}' inşaat alanına ({buildCell}) yol bulamadığı için kaynağı iade edip görevi iptal ediyor!");
                    // Yol bulma başarısız olursa kaynağı depoya iade et ve görevi iptal et
                    ResourceManager.Instance.Inventory.Add(carryingResource, 1);
                    CancelTask();
                }
            }
            else
            {
                CancelTask();
            }
        }
        else if (state == AstronautState.MovingToConstructionSite)
        {
            // İnşaat sahasına ulaştık mı kontrol et
            if (currentTask != null)
            {
                if (!_mover.DidReachDestination)
                {
                    Debug.LogWarning($"[Astronaut] '{astronautName}' inşaat alanına giden yolda engellendi! Görev iptal ediliyor.");
                    // Kaynağı depoya geri iade et ve görevi iptal et
                    ResourceManager.Instance.Inventory.Add(carryingResource, 1);
                    CancelTask();
                    return;
                }

                if (!currentTask.IsConstructed)
                {
                    Debug.Log($"[Astronaut] '{astronautName}' inşaat alanına ulaştı. '{carryingResource}' kaynağını başarıyla teslim etti!");
                    currentTask.DeliverResource(carryingResource, 1);
                }
            }
            
            // Temizleme ve boşta konumuna geçiş
            currentTask = null;
            carryingResource = ResourceType.Energy;
            isCarrying = false;
            state = AstronautState.Idle;
        }
    }
}

public enum AstronautState
{
    Idle,
    MovingToStorage,
    MovingToConstructionSite
}
