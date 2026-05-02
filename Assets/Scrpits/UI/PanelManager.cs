using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public GameObject interiorPanel;
    public GameObject exteriorPanel;
    public GameObject structurePanel;

    MouseCheck _mouseCheck;

    enum PanelKind { None, Interior, Exterior, Structure }
    PanelKind _lastOpened = PanelKind.None;

    bool _prevInteriorOn;
    bool _prevExteriorOn;
    bool _prevStructureOn;

    void LateUpdate()
    {
        if (interiorPanel == null || exteriorPanel == null || structurePanel == null) return;

        bool interiorOn = interiorPanel.activeSelf;
        bool exteriorOn = exteriorPanel.activeSelf;
        bool structureOn = structurePanel.activeSelf;

        int count = (interiorOn ? 1 : 0) + (exteriorOn ? 1 : 0) + (structureOn ? 1 : 0);
        if (count <= 1) return;

        // Aynı anda birden fazla panel açık kaldıysa:
        // - _lastOpened ile hangi panelin hedeflendiğini biliyoruz; onu açık zorla.
        // - Eğer hedef panel active değilse, mevcut active panel'lerden birini seç.
        PanelKind keep = _lastOpened;

        if (keep == PanelKind.Interior && !interiorOn) keep = exteriorOn ? PanelKind.Exterior : structureOn ? PanelKind.Structure : PanelKind.Interior;
        if (keep == PanelKind.Exterior && !exteriorOn) keep = interiorOn ? PanelKind.Interior : structureOn ? PanelKind.Structure : PanelKind.Exterior;
        if (keep == PanelKind.Structure && !structureOn) keep = exteriorOn ? PanelKind.Exterior : interiorOn ? PanelKind.Interior : PanelKind.Structure;
        if (keep == PanelKind.None)
            keep = exteriorOn ? PanelKind.Exterior : interiorOn ? PanelKind.Interior : PanelKind.Structure;

        interiorPanel.SetActive(keep == PanelKind.Interior);
        exteriorPanel.SetActive(keep == PanelKind.Exterior);
        structurePanel.SetActive(keep == PanelKind.Structure);
    }

    bool _initialized;

    void OnEnable()
    {
        // PanelManager enable olunca sürekli structure açma davranışı interior/exterior geçişini bozuyor.
        // Sadece ilk kez sahneye gelirken yapılsın.
        if (!_initialized)
        {
            _initialized = true;
            OpenStructure();
        }
    }
    public void OpenInterior()
    {
        CloseAll();
        interiorPanel.SetActive(true);
        _lastOpened = PanelKind.Interior;
        DisableBuildModeUI();
    }

    public void OpenExterior()
    {
        CloseAll();
        exteriorPanel.SetActive(true);
        _lastOpened = PanelKind.Exterior;
        DisableBuildModeUI();
    }

    public void OpenStructure()
    {
        CloseAll();
        structurePanel.SetActive(true);
        _lastOpened = PanelKind.Structure;
        DisableBuildModeUI();
    }

    void CloseAll()
    {
        interiorPanel.SetActive(false);
        exteriorPanel.SetActive(false);
        structurePanel.SetActive(false);
    }

    void DisableBuildModeUI()
    {
        if (_mouseCheck == null)
            _mouseCheck = FindFirstObjectByType<MouseCheck>();
        if (_mouseCheck == null) return;

        // Panel açıkken build panel/ghost arka planda kalmasın diye build mode'u kapat.
        if (_mouseCheck.buildMode)
            _mouseCheck.SetBuildMode(false);
    }
}