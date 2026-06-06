using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingInfoCardStatusPresenter : MonoBehaviour, IInfoCardContextReceiver
{
    [Header("Saglik Satiri")]
    [SerializeField] Image healthIcon;
    [SerializeField] Slider healthProgress;
    [SerializeField] Image healthFillImage;
    [SerializeField] TextMeshProUGUI healthValueText;

    [Header("O2/Uretim Satiri")]
    [SerializeField] Image oxygenIcon;
    [SerializeField] Slider oxygenProgress;
    [SerializeField] Image oxygenFillImage;
    [SerializeField] TextMeshProUGUI oxygenValueText;

    [Header("Dinamik İkonlar")]
    [SerializeField] Sprite oxygenSprite;
    [SerializeField] Sprite energySprite;
    [SerializeField] Sprite waterSprite;

    GameObject _contextTarget;
    PlacedBuilding _placedBuilding;

    void Start()
    {
        // Slider limitlerini koddan garanti altına alıyoruz (Editör ayarlarından bağımsız olması için)
        if (healthProgress != null)
        {
            healthProgress.minValue = 0f;
            healthProgress.maxValue = 1f;
        }
        if (oxygenProgress != null)
        {
            oxygenProgress.minValue = 0f;
            oxygenProgress.maxValue = 1f;
        }
    }

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
        if (_placedBuilding == null) return;

        // 1. Sağlık Satırı
        float health01 = _placedBuilding.Health01;
        if (healthProgress != null)
            healthProgress.value = health01;
        if (healthFillImage != null)
            healthFillImage.fillAmount = health01;
        if (healthValueText != null)
            healthValueText.text = $"{Mathf.Clamp(_placedBuilding.currentHealth, 0, _placedBuilding.maxHealth)}/{Mathf.Max(1, _placedBuilding.maxHealth)} HP";
        if (healthIcon != null) healthIcon.enabled = healthIcon.sprite != null;

        // 2. İkinci Satır: İç Mekan (O2) veya Dış Mekan (Üretim/Verimlilik)
        float targetValue01 = 0f;
        bool hasSecondRow = false;

        if (!_placedBuilding.isExterior)
        {
            // İç Mekan (Yaşam Alanı) -> Oksijen Seviyesi
            hasSecondRow = true;
            if (oxygenIcon != null && oxygenSprite != null)
            {
                oxygenIcon.sprite = oxygenSprite;
                oxygenIcon.enabled = true;
            }

            float cap = _placedBuilding.oxygenCapacity;
            targetValue01 = cap > 0f ? Mathf.Clamp01(_placedBuilding.oxygenAmount / cap) : 0f;

            if (oxygenValueText != null)
            {
                oxygenValueText.text = _placedBuilding.oxygenAmount > 0f ? "Oksijen: GÜVENLİ" : "Oksijen: YETERSİZ!";
            }
        }
        else
        {
            // Dış Mekan -> Üretici Verimliliği veya Üretim Hızı
            if (_placedBuilding.isWaterProducer)
            {
                hasSecondRow = true;
                if (oxygenIcon != null && waterSprite != null)
                {
                    oxygenIcon.sprite = waterSprite;
                    oxygenIcon.enabled = true;
                }
                float prod = _placedBuilding.networkWaterProduction;
                float cons = _placedBuilding.networkWaterConsumption;
                targetValue01 = prod > 0f ? Mathf.Clamp01((prod - cons) / prod) : 0f;
                if (oxygenValueText != null)
                {
                    oxygenValueText.text = $"{Mathf.RoundToInt(cons)}/{Mathf.RoundToInt(prod)} m³/s";
                }
            }
            else if (_placedBuilding.energyProducerType != BuildingDefinition.EnergyProducerType.None)
            {
                hasSecondRow = true;
                if (oxygenIcon != null && energySprite != null)
                {
                    oxygenIcon.sprite = energySprite;
                    oxygenIcon.enabled = true;
                }
                float prod = _placedBuilding.networkEnergyProduction;
                float cons = _placedBuilding.networkEnergyConsumption;
                targetValue01 = prod > 0f ? Mathf.Clamp01((prod - cons) / prod) : 0f;
                if (oxygenValueText != null)
                {
                    oxygenValueText.text = $"{Mathf.RoundToInt(cons)}/{Mathf.RoundToInt(prod)} kW";
                }
            }
            else if (_placedBuilding.isOxygenProducer)
            {
                hasSecondRow = true;
                if (oxygenIcon != null && oxygenSprite != null)
                {
                    oxygenIcon.sprite = oxygenSprite;
                    oxygenIcon.enabled = true;
                }
                targetValue01 = _placedBuilding.efficiency01;
                if (oxygenValueText != null)
                {
                    oxygenValueText.text = $"{_placedBuilding.oxygenSupportCapacity} Kişilik Üretim (Planetbase)";
                }
            }
            else
            {
                // Genel Diğer Yapılar -> Genel Verimlilik
                hasSecondRow = true;
                if (oxygenIcon != null && energySprite != null)
                {
                    oxygenIcon.sprite = energySprite;
                    oxygenIcon.enabled = true;
                }
                targetValue01 = _placedBuilding.efficiency01;
                if (oxygenValueText != null)
                {
                    oxygenValueText.text = $"Verimlilik: {Mathf.RoundToInt(_placedBuilding.efficiency01 * 100f)}%";
                }
            }
        }

        if (hasSecondRow)
        {
            if (oxygenProgress != null)
                oxygenProgress.value = targetValue01;
            if (oxygenFillImage != null)
                oxygenFillImage.fillAmount = targetValue01;
        }
    }
}
