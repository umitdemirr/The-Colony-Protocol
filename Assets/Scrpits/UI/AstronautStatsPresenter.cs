using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bilgi panelinde seçilen astronotun ihtiyaçlarını (Stats) gösteren sunucu.
/// Tasarımı tamamen Unity Editor/Inspector üzerinden el ile yapabilmeniz için
/// sürükle-bırak (SerializeField) alanları sunar.
/// Artık barların yanında herhangi bir yüzde metni bulunmaz (sadece bar ve tooltip hover çalışır).
/// Enerji (Sleep) statı tamamen çıkarılmıştır; Can, Oksijen, Yemek, Su ve Mutluluk statlarını gösterir.
/// </summary>
public class AstronautStatsPresenter : MonoBehaviour, IInfoCardContextReceiver
{
    private Astronaut _astronaut;

    [Header("Genel Bilgiler")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI taskStatusText; // NPC'nin mevcut görev durumu metni

    [Header("Can Seviyesi (Health)")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;

    [Header("Oksijen Seviyesi (O2)")]
    [SerializeField] private Slider oxygenSlider;
    [SerializeField] private Image oxygenFillImage;

    [Header("Beslenme Durumu (Food)")]
    [SerializeField] private Slider foodSlider;
    [SerializeField] private Image foodFillImage;

    [Header("Su Seviyesi (Water)")]
    [SerializeField] private Slider waterSlider;
    [SerializeField] private Image waterFillImage;

    [Header("Genel Mutluluk (Happiness)")]
    [SerializeField] private Slider happinessSlider;
    [SerializeField] private Image happinessFillImage;

    [Header("Normal Renk Temaları")]
    [SerializeField] private Color colorHealth = new Color(0.9f, 0.2f, 0.2f);      // Can - Canlı Kırmızı
    [SerializeField] private Color colorO2 = new Color(0.18f, 0.65f, 0.95f);      // Oksijen - Mavi
    [SerializeField] private Color colorFood = new Color(0.95f, 0.6f, 0.15f);     // Besin - Turuncu
    [SerializeField] private Color colorWater = new Color(0.15f, 0.85f, 0.95f);    // Su - Turkuaz
    [SerializeField] private Color colorHappy = new Color(0.2f, 0.82f, 0.45f);     // Mutluluk - Yeşil

    public void SetContextTarget(GameObject contextTarget)
    {
        _astronaut = contextTarget != null ? contextTarget.GetComponent<Astronaut>() : null;
        RefreshUI();
    }

    void LateUpdate()
    {
        if (_astronaut == null) return;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_astronaut == null) return;

        // 1. İsim ve Rol güncelleme
        if (nameText != null) nameText.text = _astronaut.astronautName;
        if (roleText != null)
        {
            string colorHex = "orange";
            if (_astronaut.role == NpcRole.Biologist) colorHex = "#55FF55";
            else if (_astronaut.role == NpcRole.Engineer) colorHex = "#55AAFF";
            roleText.text = $"<color={colorHex}>[{_astronaut.role}]</color>";
        }

        // 2. Mevcut Görev/Durum Güncelleme
        if (taskStatusText != null)
        {
            string stateText = "Müsait / Dinleniyor";
            if (_astronaut.health <= 0f)
            {
                stateText = "<color=#FF3333><b>Kritik: Bayıldı (Tıbbi Yardım Bekliyor!)</b></color>";
            }
            else if (_astronaut.state == AstronautState.MovingToStorage)
            {
                stateText = $"Depoya Gidiyor ({_astronaut.carryingResource})";
            }
            else if (_astronaut.state == AstronautState.MovingToConstructionSite)
            {
                stateText = $"{_astronaut.carryingResource} Taşınıyor";
            }
            taskStatusText.text = stateText;
        }

        // 3. Stat çubuklarını güncelle (5 adet stat, yüzdelik metinler kaldırıldı)
        UpdateStat(_astronaut.health, healthSlider, healthFillImage, colorHealth);
        UpdateStat(_astronaut.oxygen, oxygenSlider, oxygenFillImage, colorO2);
        UpdateStat(_astronaut.food, foodSlider, foodFillImage, colorFood);
        UpdateStat(_astronaut.water, waterSlider, waterFillImage, colorWater);
        UpdateStat(_astronaut.happiness, happinessSlider, happinessFillImage, colorHappy);
    }

    private void UpdateStat(float value, Slider slider, Image fillImg, Color normalColor)
    {
        // Slider Güncelleme
        if (slider != null)
        {
            // Slider'ın kendi min ve max değerlerine göre yüzdelik oranı eşle (Örn: 0-1 veya 0-100)
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, value / 100f);
            
            // Slider'ın içindeki dolgu görselini bulmaya çalış
            Image sliderFill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            ApplyColorAndAnimation(value, sliderFill, normalColor);
        }

        // Doğrudan Image Fill (Görsel Dolgu) Güncelleme
        if (fillImg != null)
        {
            fillImg.fillAmount = value / 100f;
            ApplyColorAndAnimation(value, fillImg, normalColor);
        }
    }

    private void ApplyColorAndAnimation(float value, Image img, Color normalColor)
    {
        if (img != null)
        {
            // %20 altı kritik durumlarda barın rengi kırmızı tonda yanıp söner (mikro animasyon)
            if (value < 20f)
            {
                float pulse = 0.65f + Mathf.PingPong(Time.time * 2.5f, 0.35f);
                img.color = new Color(pulse, 0.15f, 0.15f);
            }
            else if (value < 40f)
            {
                img.color = new Color(0.9f, 0.65f, 0.15f); // Orta seviye için sarımtırak uyarı
            }
            else
            {
                img.color = normalColor;
            }
        }
    }
}
