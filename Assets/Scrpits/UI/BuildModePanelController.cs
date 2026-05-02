using UnityEngine;

/// <summary>
/// UI'daki Build butonu ile BuildMode panelini aç/kapatır.
/// Panel kapalıyken ghost önizleme görünmez (MouseCheck buildMode=false olur).
/// </summary>
public class BuildModePanelController : MonoBehaviour
{
    public MouseCheck mouseCheck;
    public GameObject buildPanel; // Canvas içindeki panel/container

    public void OnBuildButtonClicked()
    {
        if (mouseCheck == null) return;
        // Paneli SetActive burada değiştirmiyoruz; MouseCheck event'ine bırakıyoruz.
        mouseCheck.SetBuildMode(!mouseCheck.buildMode);
    }

    public void SetBuildPanelActive(bool active)
    {
        if (buildPanel != null) buildPanel.SetActive(active);
    }

    void OnEnable()
    {
        if (mouseCheck == null) return;
        mouseCheck.OnBuildModeChanged += SetBuildPanelActive;
        // Sadece paneli senkronla (MouseCheck.SetBuildMode çağırma yok, event döngüsü olmaz).
        SetBuildPanelActive(mouseCheck.buildMode);
    }

    void OnDisable()
    {
        if (mouseCheck != null)
        {
            mouseCheck.OnBuildModeChanged -= SetBuildPanelActive;
        }
    }
}

