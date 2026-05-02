using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingInfoCardStatusPresenter : MonoBehaviour, IInfoCardContextReceiver
{
    [Header("Saglik Satiri")]
    [SerializeField] Image healthIcon;
    [SerializeField] Slider healthProgress;
    [SerializeField] TextMeshProUGUI healthValueText;

    [Header("O2/Uretim Satiri")]
    [SerializeField] Image oxygenIcon;
    [SerializeField] Slider oxygenProgress;
    [SerializeField] TextMeshProUGUI oxygenValueText;

    GameObject _contextTarget;
    PlacedBuilding _placedBuilding;

    public void SetContextTarget(GameObject contextTarget)
    {
        _contextTarget = contextTarget;
        _placedBuilding = _contextTarget != null ? _contextTarget.GetComponentInParent<PlacedBuilding>() : null;
        RefreshUi();
    }

    void LateUpdate()
    {
        if (_placedBuilding == null) return;
        RefreshUi();
    }

    void RefreshUi()
    {
        float health01 = _placedBuilding != null ? _placedBuilding.Health01 : 0f;
        if (healthProgress != null)
            healthProgress.value = health01;
        if (healthValueText != null)
            healthValueText.text = _placedBuilding != null
                ? $"{Mathf.Clamp(_placedBuilding.currentHealth, 0, _placedBuilding.maxHealth)}/{Mathf.Max(1, _placedBuilding.maxHealth)}"
                : "0/0";

        float oxygen01 = _placedBuilding != null ? _placedBuilding.OxygenRow01 : 0f;
        if (oxygenProgress != null)
            oxygenProgress.value = oxygen01;

        if (oxygenValueText != null)
        {
            if (_placedBuilding == null)
                oxygenValueText.text = "N/A";
            else if (_placedBuilding.isOxygenProducer)
            {
                float cap = _placedBuilding.oxygenProductionCapacity;
                oxygenValueText.text = cap > 0f
                    ? $"{Mathf.RoundToInt(oxygen01 * 100f)}%"
                    : "N/A";
            }
            else
            {
                float cap = _placedBuilding.oxygenCapacity;
                oxygenValueText.text = cap > 0f
                    ? $"{Mathf.RoundToInt(_placedBuilding.oxygenAmount)}/{Mathf.RoundToInt(cap)}"
                    : "N/A";
            }
        }

        if (healthIcon != null) healthIcon.enabled = healthIcon.sprite != null;
        if (oxygenIcon != null)
            oxygenIcon.enabled = oxygenIcon.sprite != null &&
                                  _placedBuilding != null &&
                                  (_placedBuilding.isOxygenProducer
                                      ? _placedBuilding.oxygenProductionCapacity > 0f
                                      : _placedBuilding.oxygenCapacity > 0f);
    }
}
