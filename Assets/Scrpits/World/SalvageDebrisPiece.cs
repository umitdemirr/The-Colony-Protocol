using UnityEngine;

/// <summary>
/// Söküm sonrası görsel parça (kaynak zaten envantere eklenir; etkileşim yok).
/// </summary>
public class SalvageDebrisPiece : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;

    public void Setup(Sprite icon)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && icon != null)
            spriteRenderer.sprite = icon;
    }
}
