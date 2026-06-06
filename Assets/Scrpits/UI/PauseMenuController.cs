using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

[System.Serializable]
public struct MenuButtonVisual
{
    public Image buttonImage;
    public Sprite orangeSprite;
    public Sprite blueSprite;
}

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [Header("UI Referansları")]
    public GameObject pauseMenuCanvas;
    public GameObject buttonsPanel;
    public GameObject settingsPanel;
    public Image backgroundImage;

    [Header("Arka Plan Görselleri")]
    public Sprite dayBackground;
    public Sprite nightBackground;
    public Sprite transitionBackground;

    [Header("Buton Görselleri")]
    public MenuButtonVisual[] menuButtons;

    [Header("Save/Load Buton Yazıları")]
    public TMP_Text saveButtonText;
    
    [Header("Dinamik Yükleme Paneli")]
    [Tooltip("Tüm kayıtların listelendiği SaveLoadPanelUI objesi")]
    public GameObject saveLoadPanel;

    private bool isPaused = false;

    void OnEnable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnLoadComplete += OnGameLoaded;
        }
    }

    void OnDisable()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnLoadComplete -= OnGameLoaded;
        }
    }

    void OnGameLoaded(string fileName)
    {
        ResumeGame();
    }

    void Start()
    {
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;

        // Kaydedilen ses seviyesini açılışta yükle
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = savedMaster;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                if (saveLoadPanel != null && saveLoadPanel.activeSelf)
                {
                    saveLoadPanel.SetActive(false);
                    return;
                }
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
                else
                {
                    ResumeGame();
                }
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; 
        
        UpdateVisuals(); 
        
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(true);
        if (buttonsPanel != null) buttonsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; 
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
    }

    private void UpdateVisuals()
    {
        if (DayNightCycleController.Instance == null) return;
        float p = DayNightCycleController.Instance.DayProgress;
        
        bool isDay = p >= 0.25f && p < 0.5f;
        bool isNight = p >= 0.75f;
        bool isTransition = (p >= 0f && p < 0.25f) || (p >= 0.5f && p < 0.75f);

        if (backgroundImage != null)
        {
            if (isDay) backgroundImage.sprite = dayBackground;
            else if (isNight) backgroundImage.sprite = nightBackground;
            else if (isTransition) backgroundImage.sprite = transitionBackground;
        }

        bool useBlueButtons = isNight;
        if (menuButtons != null)
        {
            foreach (var btnVisual in menuButtons)
            {
                if (btnVisual.buttonImage != null)
                    btnVisual.buttonImage.sprite = useBlueButtons ? btnVisual.blueSprite : btnVisual.orangeSprite;
            }
        }
    }

    public void OpenSettings()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (buttonsPanel != null) buttonsPanel.SetActive(true);
    }

    // ─────────────────────────── SAVE ───────────────────────────

    public void SaveGame()
    {
        if (saveLoadPanel != null)
        {
            var ui = saveLoadPanel.GetComponent<SaveLoadPanelUI>();
            if (ui != null)
            {
                ui.OpenPanel(SaveLoadPanelUI.PanelMode.Save);
            }
            else
            {
                saveLoadPanel.SetActive(true);
            }
        }
    }

    // ─────────────────────────── LOAD ───────────────────────────

    public void OpenLoadPanel()
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

    // ─────────────────────────── MAIN MENU / EXIT ───────────────────────────

    public void LoadMainMenu()
    {
        Time.timeScale = 1f; 
        SceneManager.LoadScene("StartScene");
    }

    public void ExitGame()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save("colony_autosave.json");

        Application.Quit();
    }

    // ─────────────────────────── UI FEEDBACK ───────────────────────────

    void ShowButtonFeedback(TMP_Text text, string message, Color color)
    {
        if (text == null) return;
        StartCoroutine(ButtonFeedbackCoroutine(text, message, color));
    }

    IEnumerator ButtonFeedbackCoroutine(TMP_Text text, string message, Color color)
    {
        string original = text.text;
        Color originalColor = text.color;
        text.text = message;
        text.color = color;
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        text.text = original;
        text.color = originalColor;
    }
}

