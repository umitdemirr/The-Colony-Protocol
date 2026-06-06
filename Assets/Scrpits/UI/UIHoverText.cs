using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Arayüzdeki slider veya görsellerin üzerine mouse ile gelindiğinde
/// açıklayıcı metin (tooltip) gösterilmesini sağlar.
/// </summary>
public class UIHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Arayüz Elemanları")]
    [Tooltip("Mouse üzerine gelince aktif edilecek GameObject (Örn: Altındaki Text veya Tooltip objesi)")]
    [SerializeField] private GameObject tooltipObject;
    
    [Tooltip("Metin içeriğinin yazılacağı TMPro component'ı (Opsiyonel)")]
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Header("Gösterilecek Metin")]
    [SerializeField] private string textToShow = "Örn: Can / Sağlık";

    private void Awake()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipText != null)
        {
            tooltipText.text = textToShow;
        }
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }
}
