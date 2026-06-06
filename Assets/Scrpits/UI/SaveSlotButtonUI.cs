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

    [Header("Tema Görselleri")]
    public Sprite mainButtonOrangeSprite;
    public Sprite mainButtonBlueSprite;
    public Sprite deleteButtonOrangeSprite;
    public Sprite deleteButtonBlueSprite;

    public void Setup(SaveManager.SaveFileInfo info, SaveLoadPanelUI panel, bool isSaveMode, bool useBlueTheme)
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
            if (isSaveMode)
            {
                loadButton.onClick.AddListener(() => _panel.OnSaveButtonClicked(_fileName));
            }
            else
            {
                loadButton.onClick.AddListener(() => _panel.OnLoadButtonClicked(_fileName));
            }
        }
        else
        {
            Debug.LogWarning("[SaveSlotButtonUI] loadButton reference is missing on " + gameObject.name, gameObject);
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(true);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => _panel.OnDeleteButtonClicked(_fileName));
        }
        else
        {
            Debug.LogWarning("[SaveSlotButtonUI] deleteButton reference is missing on " + gameObject.name, gameObject);
        }

        ApplyTheme(useBlueTheme);
    }

    public void SetupAsNewSave(SaveLoadPanelUI panel, bool useBlueTheme)
    {
        _fileName = "";
        _panel = panel;

        if (infoText != null)
        {
            infoText.text = "➕ Yeni Kayıt Oluştur";
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => _panel.OnSaveButtonClicked(""));
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(false);
        }

        ApplyTheme(useBlueTheme);
    }

    private void ApplyTheme(bool useBlueTheme)
    {
        if (loadButton != null)
        {
            Image img = loadButton.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = useBlueTheme ? mainButtonBlueSprite : mainButtonOrangeSprite;
            }
        }

        if (deleteButton != null)
        {
            Image img = deleteButton.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = useBlueTheme ? deleteButtonBlueSprite : deleteButtonOrangeSprite;
            }
        }
    }
}
