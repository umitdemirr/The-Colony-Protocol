using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sahneye bir kez eklenir; tıklanan InfoCardInteractable için panel açar.
/// </summary>
public class GlobalInfoCardClickRouter : MonoBehaviour
{
    [SerializeField] private GlobalInfoCardUI infoCard;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        GlobalInfoCardUI card = infoCard != null ? infoCard : GlobalInfoCardUI.Instance;
        if (card == null) return;

        Vector2 mousePos = Camera.main != null
            ? Camera.main.ScreenToWorldPoint(Input.mousePosition)
            : Vector2.zero;

        if (!WorldClickResolver.TryGetInfoCardInteractableAt(mousePos, out InfoCardInteractable interactable))
            return;

        GameObject body = card.ResolveBody(interactable.bodyContentKey);
        if (body == null) return;

        card.Show(interactable.headerIcon, interactable.headerTitle, body, interactable.ResolveContextTarget());
    }
}
