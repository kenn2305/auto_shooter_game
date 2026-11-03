using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(StatusEffectController))]
public sealed class EnemyFlowFieldAgent : MonoBehaviour, IKnockbackReceiver
{
    [Header("Flow Field")]
    [SerializeField] private FlowFieldService flowField;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float acceleration = 15f;

    [Header("Knockback")]
    [SerializeField, Min(0f)] private float knockbackRecovery = 10f;
    [SerializeField, Min(0f)] private float maximumKnockbackSpeed = 12f;

    [Header("Crowd Separation")]
    [SerializeField, Min(0f)] private float separationRadius = 1.25f;
    [SerializeField, Min(0f)] private float separationWeight = 0.7f;

    private Rigidbody2D body;
    private StatusEffectController statusEffects;
    private Vector2 navigationVelocity;
    private Vector2 externalVelocity;

    private Vector2 lastWalkablePosition;
    private bool hasLastWalkablePosition;

    private Vector2 recoveryTargetPosition;
    private bool hasRecoveryTargetPosition;

    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        statusEffects = GetComponent<StatusEffectController>();
    }

    private void OnEnable()
    {
        hasLastWalkablePosition = false;
        hasRecoveryTargetPosition = false;
        navigationVelocity = Vector2.zero;
        externalVelocity = Vector2.zero;
        EnemyCrowdManager.Instance?.Register(this);
    }

    private void Start()
    {
        if (flowField == null)
        {
            flowField = FlowFieldService.Instance;
        }

        if (flowField == null)
        {
            Debug.LogError("Enemy không tìm thấy FlowFieldService.", this);

            enabled = false;
            return;
        }

        EnemyCrowdManager.Instance?.Register(this);
    }

    private void FixedUpdate()
    {
        if (flowField == null)
        {
            return;
        }

        Vector2 flowDirection = Vector2.zero;

        bool hasFlowDirection = flowField.TryGetDirection(body.position, out flowDirection);

        bool isRecovering = false;

        if (hasFlowDirection)
        {
            lastWalkablePosition = body.position;
            hasLastWalkablePosition = true;
            hasRecoveryTargetPosition = false;
        }
        else
        {
            isRecovering = TryGetRecoveryDirection(out flowDirection);
        }

        Vector2 separationDirection = Vector2.zero;

        if (!isRecovering && EnemyCrowdManager.Instance != null)
        {
            separationDirection = EnemyCrowdManager.Instance.GetSeparation(this, separationRadius);
        }

        Vector2 combinedDirection = flowDirection + separationDirection * separationWeight;

        combinedDirection = Vector2.ClampMagnitude(combinedDirection, 1f);

        bool isStunned = statusEffects != null && statusEffects.IsMovementStunned;
        float speedMultiplier = statusEffects != null ? statusEffects.MovementSpeedMultiplier : 1f;
        Vector2 targetVelocity = isStunned ? Vector2.zero : combinedDirection * moveSpeed * speedMultiplier;

        navigationVelocity = Vector2.MoveTowards(
            navigationVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime
        );

        externalVelocity = Vector2.MoveTowards(externalVelocity, Vector2.zero, knockbackRecovery * Time.fixedDeltaTime);

        body.linearVelocity = navigationVelocity + externalVelocity;
    }

    public void ApplyKnockback(Vector2 impulse)
    {
        if (!isActiveAndEnabled || impulse.sqrMagnitude <= 0f)
        {
            return;
        }

        externalVelocity = Vector2.ClampMagnitude(externalVelocity + impulse, maximumKnockbackSpeed);
        body.WakeUp();
    }

    private bool TryGetRecoveryDirection(out Vector2 recoveryDirection)
    {
        recoveryDirection = Vector2.zero;

        if (!hasRecoveryTargetPosition)
        {
            if (hasLastWalkablePosition)
            {
                recoveryTargetPosition = lastWalkablePosition;
            }
            else if (!flowField.TryGetNearestWalkablePosition(body.position, out recoveryTargetPosition))
            {
                return false;
            }

            hasRecoveryTargetPosition = true;
        }

        Vector2 recoveryOffset = recoveryTargetPosition - body.position;

        if (recoveryOffset.sqrMagnitude <= 0.0001f)
        {
            hasRecoveryTargetPosition = false;
            return false;
        }

        recoveryDirection = recoveryOffset.normalized;
        return true;
    }

    private void OnDisable()
    {
        EnemyCrowdManager.Instance?.Unregister(this);

        if (body != null)
        {
            navigationVelocity = Vector2.zero;
            externalVelocity = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        EnemyCrowdManager.Instance?.Unregister(this);
    }
}
