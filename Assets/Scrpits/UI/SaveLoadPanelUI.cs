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

    [Header("Opsiyonel Onay Paneli")]
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    private string _pendingLoadFileName;

    void OnEnable()
    {
        RefreshList();
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    /// <summary>ContentParent içindeki her şeyi silip güncel listeyi oluşturur.</summary>
    public void RefreshList()
    {
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

        List<SaveManager.SaveFileInfo> saves = SaveManager.Instance.GetAllSaves();

        foreach (var save in saves)
        {
            GameObject go = Instantiate(saveSlotPrefab, contentParent);
            SaveSlotButtonUI slotUI = go.GetComponent<SaveSlotButtonUI>();
            if (slotUI == null) slotUI = go.AddComponent<SaveSlotButtonUI>();

            slotUI.Setup(save, this);
        }
    }

    public void OnLoadButtonClicked(string fileName)
    {
        _pendingLoadFileName = fileName;

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
        ExecuteLoad();
    }

    public void CancelLoad()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void ExecuteLoad()
    {
        if (SaveManager.Instance == null) return;

        // Eğer oyundayken yüklersek doğrudan Load, ana menüdeysek sahneyi açıp Load.
        if (SceneManager.GetActiveScene().name == "StartScene")
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

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}
