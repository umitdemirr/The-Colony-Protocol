using UnityEngine;

/// <summary>
/// Button OnClick'e bağla: paneli kapatır; istenirse Show(..., context) ile gelen hedefi Destroy eder.
/// </summary>
public class InfoCardPanelButton : MonoBehaviour
{
    [SerializeField] GlobalInfoCardUI infoCard;
    [SerializeField] bool destroyContextTarget = true;

    public void OnClick()
    {
        GlobalInfoCardUI card = infoCard != null ? infoCard : GlobalInfoCardUI.Instance;
        if (card == null)
            return;

        if (destroyContextTarget && card.ContextTarget != null)
        {
            // ContextTarget çoğu zaman bir "UI/etkileşilebilir parça" olabiliyor.
            // Grid/occupancy ve diğer panel-sistemleri için gerçek hedef `PlacedBuilding` root'udur.
            GameObject ctx = card.ContextTarget;
            PlacedBuilding placed = ctx != null ? ctx.GetComponentInParent<PlacedBuilding>() : null;
            GameObject destroyTarget = placed != null ? placed.gameObject : ctx;

            SalvageUtility.DropSalvageAt(destroyTarget, destroyTarget.transform.position);
            Destroy(destroyTarget);
        }

        card.Hide();
    }
}
