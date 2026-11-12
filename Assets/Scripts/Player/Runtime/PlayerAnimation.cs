using UnityEngine;

/// <summary>
/// Plays Run while the player moves and Idle otherwise, and flips the sprite to face the movement direction.
/// The sprite art faces right; the last facing is kept when the player stops or moves straight up or down.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimation : MonoBehaviour
{
    private const float MovementThreshold = 0.01f;
    private static readonly int IsRunning = Animator.StringToHash("isRunning");

    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    /// <summary>Drives another model, keeping the current facing. Used by characters with several forms.</summary>
    public void SetModel(Animator newAnimator, SpriteRenderer newSpriteRenderer)
    {
        if (newAnimator == null || newSpriteRenderer == null)
        {
            return;
        }

        if (spriteRenderer != null)
        {
            newSpriteRenderer.flipX = spriteRenderer.flipX;
        }

        animator = newAnimator;
        spriteRenderer = newSpriteRenderer;
    }

    private void Update()
    {
        Vector2 input = movement.MovementInput;
        animator.SetBool(IsRunning, input.sqrMagnitude > MovementThreshold * MovementThreshold);

        if (Mathf.Abs(input.x) > MovementThreshold)
        {
            spriteRenderer.flipX = input.x < 0f;
        }
    }
}
