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

        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        if (hit.collider == null) return;

        InfoCardInteractable interactable = hit.collider.GetComponentInParent<InfoCardInteractable>();
        if (interactable == null) return;

        GameObject body = card.ResolveBody(interactable.bodyContentKey);
        if (body == null) return;

        card.Show(interactable.headerIcon, interactable.headerTitle, body, interactable.ResolveContextTarget());
    }
}
