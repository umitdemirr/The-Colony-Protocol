using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Tüm kayıtların listelendiği Panelin ana kontrolcüsü.
/// </summary>
public class SaveLoadPanelUI : MonoBehaviour
{
    [Header("Arayüz Referansları")]
    [Tooltip("Butonların ekleneceği ana Content objesi (ScrollView/Viewport/Content)")]
    public Transform contentParent;
    
    [Tooltip("Her bir kayıt için yaratılacak şablon buton (Prefab)")]
    public GameObject saveSlotPrefab;

    [Tooltip("Sahne yüklemek için ana sahne adı")]
    public string gameplaySceneName = "SampleScene";

    [Tooltip("Paneli kapatmak için kullanılacak geri/kapat butonu")]
    public Button closeButton;

    [Tooltip("Panel başlığını gösteren Image bileşeni")]
    public Image panelTitleImage;

    [Tooltip("Save (Kaydet) modu başlık görseli (Sprite)")]
    public Sprite saveTitleSprite;

    [Tooltip("Load (Yükle) modu başlık görseli (Sprite)")]
    public Sprite loadTitleSprite;

    [Header("Tema Görselleri (Turuncu / Mavi)")]
    [Tooltip("Kapat/Geri butonu için turuncu sprite")]
    public Sprite closeButtonOrangeSprite;
    [Tooltip("Kapat/Geri butonu için mavi sprite")]
    public Sprite closeButtonBlueSprite;

    [Tooltip("Save (Kaydet) modu mavi (gece) başlık görseli")]
    public Sprite saveTitleBlueSprite;
    [Tooltip("Load (Yükle) modu mavi (gece) başlık görseli")]
    public Sprite loadTitleBlueSprite;

    [Header("Opsiyonel Onay Paneli")]
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    public enum PanelMode { Save, Load }
    private PanelMode _currentMode = PanelMode.Load;

    private string _pendingLoadFileName;
    private string _pendingSaveFileName;

    public void OpenPanel(PanelMode mode)
    {
        _currentMode = mode;
        if (gameObject.activeSelf)
        {
            RefreshList();
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    void OnEnable()
    {
        RefreshList();
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    /// <summary>ContentParent içindeki her şeyi silip güncel listeyi oluşturur.</summary>
    public void RefreshList()
    {
        bool useBlue = IsNightTime();

        if (closeButton != null)
        {
            Image closeImg = closeButton.GetComponent<Image>();
            if (closeImg != null)
            {
                closeImg.sprite = useBlue ? closeButtonBlueSprite : closeButtonOrangeSprite;
            }
        }

        if (panelTitleImage != null)
        {
            if (_currentMode == PanelMode.Save)
            {
                panelTitleImage.sprite = useBlue ? saveTitleBlueSprite : saveTitleSprite;
            }
            else
            {
                panelTitleImage.sprite = useBlue ? loadTitleBlueSprite : loadTitleSprite;
            }
        }

        if (contentParent == null || saveSlotPrefab == null)
        {
            Debug.LogError("[SaveLoadPanelUI] Content Parent veya Prefab atanmamış!");
            return;
        }

        // Eski objeleri temizle
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        if (SaveManager.Instance == null) return;

        // EĞER SAVE MODUNDAYSAK YENİ KAYIT BUTONUNU EN ÜSTE EKLE
        if (_currentMode == PanelMode.Save)
        {
            GameObject go = Instantiate(saveSlotPrefab, contentParent);
            SaveSlotButtonUI slotUI = go.GetComponent<SaveSlotButtonUI>();
            if (slotUI == null) slotUI = go.AddComponent<SaveSlotButtonUI>();

            slotUI.SetupAsNewSave(this, useBlue);
        }

        List<SaveManager.SaveFileInfo> saves = SaveManager.Instance.GetAllSaves();

        foreach (var save in saves)
        {
            GameObject go = Instantiate(saveSlotPrefab, contentParent);
            SaveSlotButtonUI slotUI = go.GetComponent<SaveSlotButtonUI>();
            if (slotUI == null) slotUI = go.AddComponent<SaveSlotButtonUI>();

            slotUI.Setup(save, this, _currentMode == PanelMode.Save, useBlue);
        }
    }

    public void OnLoadButtonClicked(string fileName)
    {
        _pendingLoadFileName = fileName;
        _pendingSaveFileName = null;

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            if (confirmText != null)
                confirmText.text = "Bu kaydı yüklemek istediğinize emin misiniz?\nMevcut ilerleme kaybolacak.";
        }
        else
        {
            ExecuteLoad();
        }
    }

    public void OnSaveButtonClicked(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            // Yeni Kayıt Oluştur
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveNew();
                RefreshList();
            }
        }
        else
        {
            // Üzerine Yaz (Overwrite)
            _pendingSaveFileName = fileName;
            _pendingLoadFileName = null;

            if (confirmPanel != null)
            {
                confirmPanel.SetActive(true);
                if (confirmText != null)
                    confirmText.text = "Bu kaydın üzerine yazmak istediğinize emin misiniz?\nEski veri kaybolacak.";
            }
            else
            {
                ExecuteSave();
            }
        }
    }

    public void OnDeleteButtonClicked(string fileName)
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.DeleteSave(fileName);
            RefreshList(); // Listeyi yenile
        }
    }

    public void ConfirmLoad()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        
        if (!string.IsNullOrEmpty(_pendingLoadFileName))
        {
            ExecuteLoad();
        }
        else if (!string.IsNullOrEmpty(_pendingSaveFileName))
        {
            ExecuteSave();
        }
    }

    public void CancelLoad()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        _pendingLoadFileName = null;
        _pendingSaveFileName = null;
    }

    void ExecuteLoad()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SaveLoadPanelUI] SaveManager.Instance is NULL! Cannot load.");
            return;
        }

        string activeScene = SceneManager.GetActiveScene().name;

        // Eğer oyundayken yüklersek doğrudan Load, ana menüdeysek sahneyi açıp Load.
        if (activeScene == "StartScene")
        {
            SaveManager.Instance.PendingLoadFileName = _pendingLoadFileName;
            SceneManager.LoadScene(gameplaySceneName);
        }
        else
        {
            SaveManager.Instance.Load(_pendingLoadFileName);
            // Kapat
            gameObject.SetActive(false);
        }
    }

    void ExecuteSave()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SaveLoadPanelUI] SaveManager.Instance is NULL! Cannot save.");
            return;
        }

        SaveManager.Instance.Save(_pendingSaveFileName);
        _pendingSaveFileName = null;
        RefreshList();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private bool IsNightTime()
    {
        if (DayNightCycleController.Instance == null) return false;
        float p = DayNightCycleController.Instance.DayProgress;
        return p >= 0.75f; // Gece saatleri (PauseMenuController ile aynı)
    }
}
