using UnityEngine;

/// <summary>
/// Koloni kaynaklarını yönetir. Save/load için merkezi erişim.
/// </summary>
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Header("Başlangıç Stoku (Opsiyonel)")]
    [SerializeField] int startMetal = 50;
    [SerializeField] int startBiyoplastik = 30;
    [SerializeField] int startSpares = 20;
    [SerializeField] int startMeal = 40;
    [SerializeField] int startMedicalSupplies = 10;
    [SerializeField] int startEnergy = 0;

    ResourceInventory _inventory = new ResourceInventory();

    public ResourceInventory Inventory => _inventory;

    [Header("Runtime Stok (Görüntüleme)")]
    [SerializeField] int _viewMetal;
    [SerializeField] int _viewBiyoplastik;
    [SerializeField] int _viewSpares;
    [SerializeField] int _viewMeal;
    [SerializeField] int _viewMedicalSupplies;
    [SerializeField] int _viewEnergy;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeStartResources();
    }

    void InitializeStartResources()
    {
        _inventory.Set(ResourceType.Metal, startMetal);
        _inventory.Set(ResourceType.Biyoplastik, startBiyoplastik);
        _inventory.Set(ResourceType.Spares, startSpares);
        _inventory.Set(ResourceType.Meal, startMeal);
        _inventory.Set(ResourceType.MedicalSupplies, startMedicalSupplies);
        _inventory.Set(ResourceType.Energy, startEnergy);
    }

    public int Get(ResourceType type) => _inventory.Get(type);
    public void Add(ResourceType type, int amount) => _inventory.Add(type, amount);
    public bool TryRemove(ResourceType type, int amount) => _inventory.TryRemove(type, amount);
    public bool Has(ResourceType type, int amount) => _inventory.Has(type, amount);

    public void LoadFromSaveData(ResourceInventory.ResourceSaveData data)
    {
        if (data == null) return;
        _inventory.LoadFromSaveData(data);
    }

    void Update()
    {
        _viewMetal = _inventory.Get(ResourceType.Metal);
        _viewBiyoplastik = _inventory.Get(ResourceType.Biyoplastik);
        _viewSpares = _inventory.Get(ResourceType.Spares);
        _viewMeal = _inventory.Get(ResourceType.Meal);
        _viewMedicalSupplies = _inventory.Get(ResourceType.MedicalSupplies);
        _viewEnergy = _inventory.Get(ResourceType.Energy);
    }
}
