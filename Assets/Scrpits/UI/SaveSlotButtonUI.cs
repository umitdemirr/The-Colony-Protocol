using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dinamik olarak üretilen her bir kayıt butonu için yardımcı script.
/// </summary>
public class SaveSlotButtonUI : MonoBehaviour
{
    public TMP_Text infoText;
    public Button loadButton;
    public Button deleteButton;

    private string _fileName;
    private SaveLoadPanelUI _panel;

    public void Setup(SaveManager.SaveFileInfo info, SaveLoadPanelUI panel)
    {
        _fileName = info.fileName;
        _panel = panel;

        if (infoText == null)
        {
            Debug.LogWarning("[SaveSlotButtonUI] infoText reference is missing on " + gameObject.name, gameObject);
        }
        else
        {
            string type = info.fileName.Contains("autosave") ? "[Oto Kayıt]" : "[Manuel Kayıt]";
            infoText.text = $"{type}\nGün {info.day} - {info.timestamp}";
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => _panel.OnLoadButtonClicked(_fileName));
        }
        else
        {
            Debug.LogWarning("[SaveSlotButtonUI] loadButton reference is missing on " + gameObject.name, gameObject);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => _panel.OnDeleteButtonClicked(_fileName));
        }
        else
        {
            Debug.LogWarning("[SaveSlotButtonUI] deleteButton reference is missing on " + gameObject.name, gameObject);
        }
    }
}
