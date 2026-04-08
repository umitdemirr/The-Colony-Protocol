using UnityEngine;

/// <summary>
/// 4 yönlü (yukarı/aşağı/sol/sağ) hareket + idle animasyonu için Animator parametrelerini günceller.
/// Yön path'ten alınır; kardinal snap ile kararsız geçişler önlenir.
/// Animator parametreleri: MoveX (float), MoveY (float), Speed (float)
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCAnimator4Dir : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public NpcMoverAStar2D mover;

    [Header("Parameters")]
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";
    public string paramSpeed = "Speed";

    float _lastMoveX;
    float _lastMoveY;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (mover == null) mover = GetComponent<NpcMoverAStar2D>();
    }

    void LateUpdate()
    {
        float speed = 0f;
        float moveX = _lastMoveX;
        float moveY = _lastMoveY;

        if (mover != null && mover.IsMoving)
        {
            Vector2 dir = mover.MovementDirection;
            SnapToCardinal(dir, out moveX, out moveY);
            _lastMoveX = moveX;
            _lastMoveY = moveY;
            speed = 1f;
        }

        if (animator != null)
        {
            animator.SetFloat(paramMoveX, moveX);
            animator.SetFloat(paramMoveY, moveY);
            animator.SetFloat(paramSpeed, speed);
        }
    }

    /// <summary>
    /// Diyagonalda yatay yöne (sol/sağ) bak, dikeyde yukarı/aşağı.
    /// Sol-aşağı/sol-yukarı → sola, sağ-aşağı/sağ-yukarı → sağa.
    /// </summary>
    void SnapToCardinal(Vector2 dir, out float moveX, out float moveY)
    {
        if (dir.sqrMagnitude < 0.01f)
        {
            moveX = _lastMoveX;
            moveY = _lastMoveY;
            return;
        }

        float ax = Mathf.Abs(dir.x);

        // Yatay bileşen varsa (diyagonal dahil) → sola veya sağa bak
        if (ax >= 0.01f)
        {
            moveX = dir.x > 0f ? 1f : -1f;
            moveY = 0f;
        }
        else
        {
            // Tam dikey → yukarı veya aşağı bak
            moveX = 0f;
            moveY = dir.y > 0f ? 1f : -1f;
        }
    }
}
