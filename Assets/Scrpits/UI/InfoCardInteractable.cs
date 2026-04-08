using UnityEngine;

/// <summary>
/// Tıklanınca GlobalInfoCardUI'de gösterilecek veriyi taşır.
/// Her etkileşilebilir objeye eklenir.
/// </summary>
public class InfoCardInteractable : MonoBehaviour
{
    [Header("Info Card Data")]
    public Sprite headerIcon;
    public string headerTitle = "Bilgi";
    public string bodyContentKey;
    public GameObject contextTargetOverride;

    public GameObject ResolveContextTarget()
    {
        return contextTargetOverride != null ? contextTargetOverride : gameObject;
    }
}
