using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Kaynakları Canvas üzerinde gösterir: Image (icon) + TMP_Text (miktar).
/// Her slot için Image ve Text objelerini ata.
/// </summary>
public class ResourceDisplayUI : MonoBehaviour
{
    public ResourceManager resourceManager;

    [System.Serializable]
    public struct ResourceSlot
    {
        public Image icon;
        public TMP_Text amountText;
    }

    [Header("Kaynak Slotları (Image + Text)")]
    public ResourceSlot metal;
    public ResourceSlot biyoplastik;
    public ResourceSlot spares;
    public ResourceSlot meal;
    public ResourceSlot medicalSupplies;

    void Start()
    {
        if (resourceManager == null) resourceManager = ResourceManager.Instance;
    }

    void LateUpdate()
    {
        if (resourceManager == null) return;

        SetSlot(metal, ResourceType.Metal);
        SetSlot(biyoplastik, ResourceType.Biyoplastik);
        SetSlot(spares, ResourceType.Spares);
        SetSlot(meal, ResourceType.Meal);
        SetSlot(medicalSupplies, ResourceType.MedicalSupplies);
    }

    void SetSlot(ResourceSlot slot, ResourceType type)
    {
        if (slot.amountText != null)
            slot.amountText.text = resourceManager.Get(type).ToString();
    }
}
