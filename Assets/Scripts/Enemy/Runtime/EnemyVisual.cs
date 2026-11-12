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
        bool faceLeft;

        if (Mathf.Abs(vx) >= flipThreshold)
        {
            faceLeft = vx < 0f;
        }
        else
        {
            // Standing still (stationary enemies like Stump, stunned or jammed ones): look at the player.
            Transform target = FlowFieldService.Instance != null ? FlowFieldService.Instance.Target : null;
            if (target == null)
                return;

            float dx = target.position.x - body.position.x;
            if (Mathf.Abs(dx) < flipThreshold)
                return;

            faceLeft = dx < 0f;
        }

        spriteRenderer.flipX = spriteFacesLeft ? !faceLeft : faceLeft;
    }
}
