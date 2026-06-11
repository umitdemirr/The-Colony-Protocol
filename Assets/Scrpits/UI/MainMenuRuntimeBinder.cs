using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuRuntimeBinder : MonoBehaviour
{
    [SerializeField] Button newGameButton;
    [SerializeField] Button continueButton;
    [SerializeField] Button loadGameButton; // Dinamik yükleme panelini açan buton
    [SerializeField] Button settingsButton;  // Ayarlar panelini açan buton
    [SerializeField] Button exitButton;
    [SerializeField] string gameplaySceneName = "SampleScene";

    [Header("Ses / Müzik Ayarları")]
    [SerializeField] Button musicToggleButton;
    [SerializeField] Sprite musicOnSprite;
    [SerializeField] Sprite musicOffSprite;

    [Header("UI Panelleri")]
    [Tooltip("Tüm kayıtların listelendiği panel")]
    public GameObject saveLoadPanel;
    [Tooltip("Ayarlar paneli")]
    public GameObject settingsPanel;

    [Header("Devam Et Bilgi Metni (Opsiyonel)")]
    [Tooltip("Devam Et butonunun alt metninde kayıt bilgisi göstermek için")]
    [SerializeField] TMP_Text continueInfoText;

    void Awake()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
            newGameButton.onClick.AddListener(OnNewGameClicked);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(OnLoadGameClicked);
            loadGameButton.onClick.AddListener(OnLoadGameClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
            exitButton.onClick.AddListener(OnExitClicked);
        }

        if (musicToggleButton != null)
        {
            musicToggleButton.onClick.RemoveListener(OnMusicToggleClicked);
            musicToggleButton.onClick.AddListener(OnMusicToggleClicked);
        }
    }

    void Start()
    {
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        UpdateContinueButton();
        UpdateMusicToggleButtonVisual();
    }

    void OnDestroy()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGameClicked);
        if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClicked);
        if (loadGameButton != null) loadGameButton.onClick.RemoveListener(OnLoadGameClicked);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitClicked);
        if (musicToggleButton != null) musicToggleButton.onClick.RemoveListener(OnMusicToggleClicked);
    }

    void OnSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    void UpdateContinueButton()
    {
        if (continueButton == null) return;

        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasAnySave();
        continueButton.interactable = hasSave;

        if (continueInfoText != null)
        {
            var latest = SaveManager.Instance != null ? SaveManager.Instance.GetLatestSave() : null;
            if (hasSave && latest != null)
            {
                continueInfoText.text = $"Son Kayıt: Gün {latest.Value.day}\n{latest.Value.timestamp}";
            }
            else
            {
                continueInfoText.text = "Kayıt bulunamadı";
            }
        }
    }

    void OnNewGameClicked()
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName)) return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadFileName = "";

        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadSceneAsync(gameplaySceneName);
        }
        else
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    void OnContinueClicked()
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName)) return;

        if (SaveManager.Instance == null || !SaveManager.Instance.HasAnySave()) return;

        var latest = SaveManager.Instance.GetLatestSave();
        if (latest == null) return;

        SaveManager.Instance.PendingLoadFileName = latest.Value.fileName;

        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadSceneAsync(gameplaySceneName);
        }
        else
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    void OnLoadGameClicked()
    {
        if (saveLoadPanel != null)
        {
            var ui = saveLoadPanel.GetComponent<SaveLoadPanelUI>();
            if (ui != null)
            {
                ui.OpenPanel(SaveLoadPanelUI.PanelMode.Load);
            }
            else
            {
                saveLoadPanel.SetActive(true);
            }
        }
    }

    void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnMusicToggleClicked()
    {
        if (AudioManager.Instance != null)
        {
            bool isMuted = AudioManager.Instance.IsMusicMuted();
            AudioManager.Instance.SetMusicMute(!isMuted);
            UpdateMusicToggleButtonVisual();
        }
        else
        {
            // Eğer sahnede AudioManager yoksa, sahnedeki AudioSource'u bul ve kontrol et
            AudioSource sceneMusic = FindObjectOfType<AudioSource>();
            if (sceneMusic != null)
            {
                sceneMusic.mute = !sceneMusic.mute;
                
                // Mute durumunu kaydet
                PlayerPrefs.SetInt("MusicMuted", sceneMusic.mute ? 1 : 0);
                PlayerPrefs.Save();
                
                UpdateMusicToggleButtonVisual();
            }
        }
    }

    void UpdateMusicToggleButtonVisual()
    {
        if (musicToggleButton == null) return;

        Image btnImage = musicToggleButton.GetComponent<Image>();
        if (btnImage == null) return;

        bool isMuted = false;
        if (AudioManager.Instance != null)
        {
            isMuted = AudioManager.Instance.IsMusicMuted();
        }
        else
        {
            // AudioManager yoksa PlayerPrefs'ten oku ve sahnedeki AudioSource'a uygula
            isMuted = PlayerPrefs.GetInt("MusicMuted", 0) == 1;
            AudioSource sceneMusic = FindObjectOfType<AudioSource>();
            if (sceneMusic != null)
            {
                sceneMusic.mute = isMuted;
            }
        }

        if (isMuted)
        {
            if (musicOffSprite != null)
                btnImage.sprite = musicOffSprite;
        }
        else
        {
            if (musicOnSprite != null)
                btnImage.sprite = musicOnSprite;
        }
    }
}

