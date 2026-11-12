using UnityEngine;

/// <summary>
/// Flips the enemy sprite toward its horizontal movement and desyncs the run loop
/// so a crowd of the same enemy does not animate in lockstep.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [SerializeField]
    [Tooltip("Tick when the source sprite sheet is drawn facing left.")]
    private bool spriteFacesLeft;

    [SerializeField, Min(0f)]
    [Tooltip("Horizontal speed below which the current facing is kept.")]
    private float flipThreshold = 0.05f;

    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.Play(0, 0, Random.value);
    }

    private void LateUpdate()
    {
        if (body == null || spriteRenderer == null)
            return;

        float vx = body.linearVelocity.x;

        if (Mathf.Abs(vx) < flipThreshold)
            return;

        bool movingLeft = vx < 0f;
        spriteRenderer.flipX = spriteFacesLeft ? !movingLeft : movingLeft;
    }
}
