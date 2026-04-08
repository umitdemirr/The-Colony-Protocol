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
            GameObject t = card.ContextTarget;
            SalvageUtility.DropSalvageAt(t, t.transform.position);
            Destroy(t);
        }

        card.Hide();
    }
}
