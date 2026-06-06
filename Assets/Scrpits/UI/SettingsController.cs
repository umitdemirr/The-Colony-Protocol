using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsController : MonoBehaviour
{
    [Header("Ses UI Elemanları")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Görüntü/Grafik UI Elemanları")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Navigasyon")]
    [SerializeField] private Button backButton;
    [SerializeField] private PauseMenuController pauseMenuController;

    [Header("Tema Görselleri (Turuncu / Mavi)")]
    [SerializeField] private Image panelTitleImage;
    [SerializeField] private Sprite settingsTitleOrangeSprite;
    [SerializeField] private Sprite settingsTitleBlueSprite;
    [SerializeField] private Sprite backButtonOrangeSprite;
    [SerializeField] private Sprite backButtonBlueSprite;
    [SerializeField] private Sprite fullscreenToggleOrangeSprite;
    [SerializeField] private Sprite fullscreenToggleBlueSprite;
    [Tooltip("Toggle'ın arka plan görseli (Boşsa targetGraphic kullanılır)")]
    [SerializeField] private Image fullscreenToggleImage;

    private Resolution[] _resolutions;
    private List<Resolution> _filteredResolutions;

    private const string MasterVolKey = "MasterVolume";
    private const string MusicVolKey = "MusicVolume";
    private const string SfxVolKey = "SfxVolume";
    private const string QualityKey = "QualityIndex";
    private const string FullscreenKey = "IsFullscreen";
    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";

    void Awake()
    {
        // Buton dinleyicisi
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        // Dinleyicileri kur
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    void Start()
    {
        InitializeResolutions();
        InitializeQualityLevels();
        LoadAndApplySettings();
    }

    private void InitializeResolutions()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        _filteredResolutions = new List<Resolution>();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        // Benzersiz çözünürlükleri filtrele (Yenileme hızından bağımsız sadece en-boy oranı/çözünürlük)
        HashSet<string> uniqueResStrings = new HashSet<string>();

        for (int i = 0; i < _resolutions.Length; i++)
        {
            string option = _resolutions[i].width + " x " + _resolutions[i].height;
            if (!uniqueResStrings.Contains(option))
            {
                uniqueResStrings.Add(option);
                _filteredResolutions.Add(_resolutions[i]);
                options.Add(option);

                // Şu anki çözünürlükle eşleşiyor mu kontrol et
                if (_resolutions[i].width == Screen.currentResolution.width &&
                    _resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = _filteredResolutions.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);

        // Eğer daha önce kaydedilen çözünürlük varsa onu seç, yoksa mevcut olanı seç
        int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);
        
        int matchedIndex = -1;
        for (int i = 0; i < _filteredResolutions.Count; i++)
        {
            if (_filteredResolutions[i].width == savedWidth && _filteredResolutions[i].height == savedHeight)
            {
                matchedIndex = i;
                break;
            }
        }

        if (matchedIndex != -1)
        {
            resolutionDropdown.value = matchedIndex;
        }
        else
        {
            resolutionDropdown.value = currentResolutionIndex;
        }
        resolutionDropdown.RefreshShownValue();
    }

    private void InitializeQualityLevels()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        string[] qualityNames = QualitySettings.names;
        List<string> options = new List<string>(qualityNames);
        
        qualityDropdown.AddOptions(options);
        
        int savedQuality = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        qualityDropdown.value = savedQuality;
        qualityDropdown.RefreshShownValue();
    }

    private void LoadAndApplySettings()
    {
        // 1. Genel Ses (Master Volume)
        float savedMaster = PlayerPrefs.GetFloat(MasterVolKey, 1f);
        if (masterVolumeSlider != null) masterVolumeSlider.value = savedMaster;
        AudioListener.volume = savedMaster;

        // 2. Müzik Sesi (Music Volume)
        float savedMusic = PlayerPrefs.GetFloat(MusicVolKey, 0.7f);
        if (musicVolumeSlider != null) musicVolumeSlider.value = savedMusic;
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(savedMusic);

        // 3. Efekt Sesi (SFX Volume)
        float savedSfx = PlayerPrefs.GetFloat(SfxVolKey, 0.7f);
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = savedSfx;
        if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(savedSfx);

        // 4. Grafik Kalitesi
        int savedQuality = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(savedQuality, true);

        // 5. Tam Ekran
        bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        if (fullscreenToggle != null) fullscreenToggle.isOn = savedFullscreen;
        Screen.fullScreen = savedFullscreen;
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(MasterVolKey, volume);
    }

    public void SetMusicVolume(float volume)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(volume);
        PlayerPrefs.SetFloat(MusicVolKey, volume);
    }

    public void SetSfxVolume(float volume)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(volume);
        PlayerPrefs.SetFloat(SfxVolKey, volume);
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex, true);
        PlayerPrefs.SetInt(QualityKey, qualityIndex);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
    }

    public void SetResolution(int resolutionIndex)
    {
        if (_filteredResolutions == null || resolutionIndex < 0 || resolutionIndex >= _filteredResolutions.Count) return;

        Resolution resolution = _filteredResolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        
        PlayerPrefs.SetInt(ResolutionWidthKey, resolution.width);
        PlayerPrefs.SetInt(ResolutionHeightKey, resolution.height);
    }

    private void OnBackClicked()
    {
        // Değişiklikleri diske yaz
        PlayerPrefs.Save();

        // PauseMenuController varsa CloseSettings() çağır, yoksa sadece paneli kapat
        if (pauseMenuController != null)
        {
            pauseMenuController.CloseSettings();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        bool useBlue = IsNightTime();

        if (backButton != null)
        {
            Image btnImg = backButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.sprite = useBlue ? backButtonBlueSprite : backButtonOrangeSprite;
            }
        }

        if (panelTitleImage != null)
        {
            panelTitleImage.sprite = useBlue ? settingsTitleBlueSprite : settingsTitleOrangeSprite;
        }

        Image toggleImg = fullscreenToggleImage != null ? fullscreenToggleImage : (fullscreenToggle != null ? fullscreenToggle.targetGraphic as Image : null);
        if (toggleImg != null)
        {
            toggleImg.sprite = useBlue ? fullscreenToggleBlueSprite : fullscreenToggleOrangeSprite;
        }
    }

    private bool IsNightTime()
    {
        if (DayNightCycleController.Instance == null) return false;
        float p = DayNightCycleController.Instance.DayProgress;
        return p >= 0.75f; // Gece saatleri (PauseMenuController ile aynı)
    }
}
